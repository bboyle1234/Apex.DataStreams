using Apex.DataStreams.Connections;
using Apex.DataStreams.Topics;
using Apex.ServiceUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static Apex.TaskUtilities.Tasks;

namespace Apex.DataStreams {

    public sealed class Publisher : ServiceBase/*, IPublisher*/ {

        /// <summary>
        /// The maximum length of the pending connections queue.
        /// </summary>
        private const int MaxPendingConnectionsBacklog = 10;

        readonly ILogger Log;
        readonly IServiceProvider Services;
        readonly Socket Listener;
        readonly AsyncLock Mutex = new AsyncLock();
        readonly PublisherConfiguration Configuration;
        readonly List<Topic> Topics = new List<Topic>();
        readonly Func<Publisher, Exception, Task> ErrorCallback;
        readonly RecentEventCounter<DisconnectionEvent> RecentDisconnections = new RecentEventCounter<DisconnectionEvent>(TimeSpan.FromMinutes(1));

        int _errored = 0;
        ImmutableList<Connection> Connections = ImmutableList<Connection>.Empty;

        public Publisher(IServiceProvider services, PublisherConfiguration configuration, Func<Publisher, Exception, Task> errorCallback) {
            Services = services;
            Configuration = configuration;
            ErrorCallback = errorCallback;
            Log = services.GetRequiredService<ILoggerFactory>().CreateLogger<Publisher>();
            Listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) {
                NoDelay = true,
                LingerState = new LingerOption(true, 1),
            };
        }

        protected override void CustomStart() {
            try {
                Log.LogInformation($"Starting. Configuration: {JsonConvert.SerializeObject(Configuration)}");
                Listener.Bind(new IPEndPoint(IPAddress.Any, Configuration.ListenPort));
                Listener.Listen(MaxPendingConnectionsBacklog);
                Log.LogInformation($"Listener socket is started.");
                FireAndForget(AcceptConnectionWorker);
            } catch (Exception x) {
                Log.LogCritical(x, "Exception thrown while starting.");
                OnErrored(x);
            }
        }

        protected override void CustomDispose(bool disposing) {
            try { Listener.Dispose(); } catch { }
            foreach (var connection in Connections) {
                connection.Dispose();
            }
            RecentDisconnections.Dispose();
        }

        /// <summary>
        /// Worker method that accepts incoming client connections.
        /// </summary>
        async Task AcceptConnectionWorker() {
            try {
                while (true) {
                    /// <see cref="Socket.AcceptAsync(SocketAsyncEventArgs)"/> can't take a cancellation token,
                    /// but it does throw an exception when the socket is disposed.
                    var socket = await Listener.AcceptAsync().ConfigureAwait(false);
                    Log.LogInformation($"Accepted connection from '{socket.RemoteEndPoint}'.");
                    var connection = new Connection(new ClientContext {
                        Services = Services,
                        Schema = Configuration.Schema,
                        PublisherEndPoint = null, // Not for use at the publisher end.
                        ReceiveQueue = null, // Publishers (at this time) don't receive messages from the clients
                    }, socket, null);
                    connection.Start();
                    await AddConnection(connection).ConfigureAwait(false);
                }
            } catch (Exception x) {
                /// To my knowledge, the only way code can get here is after we have been disposed. 
                /// However, it's possible that my knowledge is limited and the AcceptAsync method could throw 
                /// an exception for a different reason. If so, we need to ensure that the exception is logged.
                OnErrored(x);
            }
        }

        /// <summary>
        /// This method is called when the publisher is no longer able to accept any more connections.
        /// Automatically disposes the publisher if necessary.
        /// </summary>
        void OnErrored(Exception x) {
            /// Quietly exit if this method has been called before.
            if (Interlocked.CompareExchange(ref _errored, 1, 0) == 1) return;

            /// If we're being called by the Dispose method, there's no need to log the error or fire off the Dispose method.
            if (!IsDisposeStarted) {
                Log.LogError(x, $"Exception thrown.");
                FireAndForget(Dispose);
            }

            /// Kickoff a call to the ErrorCallback. There's no need to wait around for it to complete.
            /// Also prevents potential thread locks if this method were called by the Start method.
            FireAndForget(async () => {
                /// Swallow, (but log) any errors thrown by the ErrorCallback.
                try {
                    if (ErrorCallback is object) {
                        await ErrorCallback(this, x).ConfigureAwait(false);
                    }
                } catch (Exception y) {
                    Log.LogError(y, $"Exception thrown by the {nameof(ErrorCallback)} handler.");
                }
            });
        }

        async ValueTask AddConnection(Connection connection) {
            using (await Mutex.LockAsync().ConfigureAwait(false)) {
                Connections = Connections.Add(connection);
                foreach (var topic in Topics) {
                    await topic.AddConnection(connection).ConfigureAwait(false);
                }
            }
            connection.DisposedTask.ContinueWith(async _ => {
                using (await Mutex.LockAsync().ConfigureAwait(false)) {
                    foreach (var topic in Topics) {
                        await topic.RemoveConnection(connection).ConfigureAwait(false);
                    }
                    Connections = Connections.Remove(connection);
                }
            }).Ignore();
        }

        public async ValueTask<ITopic> CreateTopic(ITopicSummary summary) {
            using var _ = await Mutex.LockAsync().ConfigureAwait(false);
            var topic = new Topic(summary, Connections);
            Topics.Add(topic);
            topic.Start();
            return topic;
        }

        public async ValueTask RemoveTopic(ITopic topic) {
            using var _ = await Mutex.LockAsync().ConfigureAwait(false);
            var theTopic = (Topic)topic;
            Topics.Remove(theTopic);
            theTopic.Dispose();
        }

        public async ValueTask RemoveAllTopics() {
            using var _ = await Mutex.LockAsync().ConfigureAwait(false);
            foreach (var topic in Topics)
                topic.Dispose();
            Topics.Clear();
        }

        public async Task<PublisherStatus> GetStatus() {
            var status = new PublisherStatus();
            status.Connections = new List<ConnectionStatus>();
            foreach (var connection in Connections)
                status.Connections.Add(await connection.GetStatusAsync().ConfigureAwait(false));
            status.RecentDisconnections = RecentDisconnections.GetRecentEvents().ToList();
            return status;
        }


        #region sealed class Topic

        sealed class Topic : ServiceBase, ITopic {

            readonly ITopicSummary Summary;
            readonly List<Connection> Connections = new List<Connection>();
            readonly Channel<object> MessageQueue = Channel.CreateUnbounded<object>();

            public Topic(ITopicSummary summary, IEnumerable<Connection> connections) {
                Summary = summary;
                Connections = connections.ToList();
            }

            protected override void CustomStart() {
                Task.Run(Work);
            }

            public ValueTask AddConnection(Connection connection)
                => MessageQueue.Writer.WriteAsync(connection);

            public ValueTask RemoveConnection(Connection connection)
                => MessageQueue.Writer.WriteAsync(new RemoveConnectionJob(connection));

            public ValueTask Enqueue(object message)
                => MessageQueue.Writer.WriteAsync(message);

            async Task Work() {
                try {
                    while (true) {
                        DisposedToken.ThrowIfCancellationRequested();
                        var message = await MessageQueue.Reader.ReadAsync(DisposedToken).ConfigureAwait(false);
                        switch (message) {

                            case Connection newConnection: {
                                Connections.Add(newConnection);
                                var summaryMessages = await Summary.GetTopicSummary().ConfigureAwait(false);
                                if (summaryMessages is object && summaryMessages.Length > 0) {
                                    foreach (var summaryMessage in summaryMessages) {
                                        if (summaryMessage is object) {
                                            await newConnection.Enqueue(summaryMessage).ConfigureAwait(false);
                                        }
                                    }
                                }
                                break;
                            }

                            case RemoveConnectionJob removeConnection:
                                Connections.Remove(removeConnection.Connection);
                                break;

                            default: {
                                foreach (var connection in Connections) {
                                    await connection.Enqueue(message).ConfigureAwait(false); ;
                                }
                                break;
                            }
                        }
                    }
                } catch (OperationCanceledException) { }
            }

            readonly struct RemoveConnectionJob {
                public readonly Connection Connection;
                public RemoveConnectionJob(Connection connection) => Connection = connection;
            }
        }

        #endregion
    }
}
