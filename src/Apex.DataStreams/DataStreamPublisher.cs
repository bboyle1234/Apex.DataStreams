using Apex.DataStreams.Connections;
using Apex.DataStreams.Definitions;
using Apex.DataStreams.Encoding;
using Apex.DataStreams.Operations;
using Apex.DataStreams.Topics;
using Apex.LoggingUtils;
using Apex.TimeStamps;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static Apex.TaskUtilities.Tasks;
using CTS = System.Threading.CancellationTokenSource;

namespace Apex.DataStreams {

    internal sealed class DataStreamPublisher : IDataStreamPublisher, IDisposable {

        /// <summary>
        /// The maximum length of the pending connections queue.
        /// </summary>
        private const int MaxPendingConnectionsBacklog = 10;

        /// <inheritdoc />
        public DataStreamPublisherConfiguration Configuration { get; }

        readonly ILogger Log;
        readonly CTS Disposed;
        readonly AsyncLock Lock;
        readonly Socket Listener;
        readonly IEncoder Encoder;
        readonly List<Topic> Topics;
        readonly IServiceProvider ServiceProvider;
        readonly List<DataStreamConnection> Clients;
        readonly Func<IDataStreamPublisher, Exception, Task> ErrorCallback;

        long _started = 0;
        long _errored = 0;
        long _disposed = 0;
        ImmutableList<DisconnectionEvent> _recentDisconnections = ImmutableList<DisconnectionEvent>.Empty;

        public DataStreamPublisher(IServiceProvider serviceProvider, DataStreamPublisherConfiguration configuration, Func<IDataStreamPublisher, Exception, Task> errorCallback) {

            if (null == configuration) throw new ArgumentNullException(nameof(configuration));
            if (null == errorCallback) throw new ArgumentNullException(nameof(errorCallback));

            ServiceProvider = serviceProvider;
            Configuration = configuration;
            ErrorCallback = errorCallback;

            Encoder = ServiceProvider.GetRequiredService<IEncoder>();
            Log = ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger($"{GetType().Name}_{Configuration.DataStreamDefinition.Name}");

            Disposed = new CTS();
            Topics = new List<Topic>();
            Clients = new List<DataStreamConnection>();
            Lock = new AsyncLock();
            Listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) {
                NoDelay = true,
                LingerState = new LingerOption(true, 1),
            };
        }

        /// <summary>
        /// Makes the publisher start listening for incoming connections.
        /// Does not throw exceptions if it fails - instead, you must have a handler for the errorCallback.
        /// </summary>
        public void Start() {
            if (Interlocked.CompareExchange(ref _started, 1, 0) == 1) throw new InvalidOperationException("Cannot Start more than once.");
            using (Log.BeginDataScopeUsingMethodName()) {
                Log.SetScopeData("configuration", Configuration);
                try {
                    Listener.Bind(new IPEndPoint(IPAddress.Any, Configuration.ListenPort));
                    Listener.Listen(MaxPendingConnectionsBacklog);
                    Log.LogInformation($"Started.");
                } catch (Exception x) {
                    /// We don't rethrow the exception, because OnErrored will call the ErrorCallback method, notifying the 
                    /// calling object that way. Currently, OnErrored is setup to kickoff a new task that triggers the ErrorCallback. 
                    /// If OnErrored is ever modified to wait for the ErrorCallback to complete before it completes, we would be in danger
                    /// of causing a threadlock situation with the calling code, which might be running within a lock right now. So ... 
                    /// if the OnErrored method is every modified in this way, we'll need to kickoff a task right here for calling OnErrored.
                    OnErrored(x);
                    return;
                }
            }
            /// Get outside of the Log's data scope before firing off this long-running task,
            /// or the data scope would be included in every log entry by the AcceptConnectionWorker.
            FireAndForget(AcceptConnectionWorker);
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
                    /// Initialise and store the object that will handle communication with the new client.
                    var client = new DataStreamConnection(new DataStreamConnectionContext {
                        Definition = Configuration.DataStreamDefinition,
                        Encoder = Encoder,
                        Socket = socket,
                        ReceiveQueue = null, // Publishers (at this time) don't receive any messages from the clients
                        DisconnectedCallback = OnClientDisconnected,
                    });
                    client.Start();
                    using (await Lock.LockAsync().ConfigureAwait(false)) {
                        Clients.Add(client);
                        /// Adding the client to a topic can take some time, because the topic has to 
                        /// enqueue its current topic summary into the new client, so we let this run in parallel.
                        var clientArray = new[] { client };
                        var tasks = Topics.Select(async t => await t.AddClientsAsync(clientArray).ConfigureAwait(false));
                        await Task.WhenAll(tasks).ConfigureAwait(false);
                    }
                }
            } catch (Exception x) {
                /// To my knowledge, the only way code can get here is after we have been disposed. 
                /// However, it's possible that my knowledge is limited and the AcceptAsync method could throw 
                /// an exception for a different reason. If so, we need to ensure that the exception is logged.
                OnErrored(x);
            }
        }

        /// <summary>
        /// This is the callback that client connections use to notify us that the connection has been dropped.
        /// </summary>
        Task OnClientDisconnected(DataStreamConnection client, Exception x) {
            /// There's no need to make the client wait around for all this to happen.
            /// And we make sure no thread-lock issues happen when Dispose is using the Lock as well.
            FireAndForget(async () => {
                using (await Lock.LockAsync().ConfigureAwait(false)) {

                    /// A side-effect of the Dispose method is that all clients will call OnClientDisconnected.
                    /// We check for disposal here inside the lock to make sure we get the logic right. 
                    if (Disposed.IsCancellationRequested) return;

                    Log.LogInformation(x, $"Remote '{client.RemoteEndPoint}' disconnected.");

                    var tenMinutesAgo = TimeStamp.Now.Subtract(TimeSpan.FromMinutes(10));
                    _recentDisconnections = _recentDisconnections.Add(new DisconnectionEvent {
                        TimeStamp = TimeStamp.Now,
                        RemoteEndPoint = client.RemoteEndPoint.ToString(),
                        Exception = x,
                    }).RemoveAll(d => d.TimeStamp < tenMinutesAgo);

                    var tasks = Topics.Select(async (topic) => await topic.RemoveClientAsync(client).ConfigureAwait(false));
                    await Task.WhenAll(tasks).ConfigureAwait(false);

                    Clients.Remove(client);

                    /// Client DataStreamConnection has been coded to auto-dispose itself,
                    /// but we're doing this here to be "polite" and also to do the right thing in case the DataStreamConnection implementation ever changes.
                    client.Dispose();
                }
            });
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task<IDataStreamTopic> CreateTopicAsync(DataStreamTopicDefinition definition, IDataStreamTopicSummary topicManager) {
            using (await Lock.LockAsync().ConfigureAwait(false)) {
                if (Disposed.IsCancellationRequested) throw new ObjectDisposedException(nameof(DataStreamPublisher));
                var topic = new Topic(this, definition, Encoder, topicManager);
                await topic.AddClientsAsync(Clients).ConfigureAwait(false);
                Topics.Add(topic);
                return topic;
            }
        }

        /// <inheritdoc />
        public async Task RemoveTopicAsync(IDataStreamTopic topic) {
            var theTopic = (Topic)topic;
            using (await Lock.LockAsync().ConfigureAwait(false)) {
                Topics.Remove(theTopic);
            }
            await theTopic.ShutdownAsync().ConfigureAwait(false);
        }

        public async Task ClearTopicsAsync() {
            using (await Lock.LockAsync().ConfigureAwait(false)) {
                var tasks = Topics.Select(t => t.ShutdownAsync());
                await Task.WhenAll(tasks).ConfigureAwait(false);
                Topics.Clear();
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
            if (!Disposed.IsCancellationRequested) {
                Log.LogError(x, $"Exception thrown.");
                FireAndForget(Dispose);
            }

            /// Kickoff a call to the ErrorCallback. There's no need to wait around for it to complete.
            /// Also prevents potential thread locks if this method were called by the Start method.
            Task.Run(async () => {
                /// Swallow, (but log) any errors thrown by the ErrorCallback.
                try {
                    await ErrorCallback(this, x).ConfigureAwait(false);
                } catch (Exception y) {
                    Log.LogError(y, $"Exception thrown by the {nameof(ErrorCallback)} handler.");
                }
            });
        }

        #region IDisposable

        public void Dispose() {
            /// Quietly exit if this method has been called before.
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 1) return;

            using (Lock.Lock()) {
                Disposed.Cancel();
                OnErrored(new Exception("Disposing."));
                Disposed.Dispose();
                try { Listener?.Close(); } catch { }
                try { Listener?.Dispose(); } catch { }
                Clients.ForEach(r => r.Dispose());
                Topics.ForEach(t => t.ShutdownAsync().GetAwaiter().GetResult());
                Clients.Clear();
                Topics.Clear();
            }
            Log.LogInformation("Disposed.");
            GC.SuppressFinalize(this);
        }

        ~DataStreamPublisher() => Dispose();

        #endregion

        public async Task<DataStreamPublisherStatus> GetStatusAsync() {
            var status = new DataStreamPublisherStatus();
            var connectionStatusTasks = Clients.Select(c => c.GetStatusAsync());
            await Task.WhenAll(connectionStatusTasks).ConfigureAwait(false);
            status.Connections = connectionStatusTasks.Select(t => t.Result).ToList();
            var tenMinutesAgo = TimeStamp.Now.Subtract(TimeSpan.FromMinutes(10));
            status.RecentDisconnections = _recentDisconnections.Where(d => d.TimeStamp > tenMinutesAgo).ToList();
            return status;
        }

        #region class Topic

        private sealed class Topic : IDataStreamTopic {

            readonly AsyncLock Lock;
            readonly IEncoder Encoder;
            readonly DataStreamPublisher Publisher;
            readonly List<DataStreamConnection> Clients;
            readonly DataStreamTopicDefinition Definition;
            readonly IDataStreamTopicSummary TopicSummaryManager;

            bool _shutdown = false;

            public Topic(DataStreamPublisher publisher, DataStreamTopicDefinition definition, IEncoder encoder, IDataStreamTopicSummary topicSummaryManager) {
                Publisher = publisher;
                Definition = definition;
                Encoder = encoder;
                TopicSummaryManager = topicSummaryManager;
                Lock = new AsyncLock();
                Clients = new List<DataStreamConnection>();
            }

            #region **** Methods called by the publisher ****
            #endregion


            /// <summary>
            /// Called by the publisher when new clients have connected. The new clients need to be sent a topic summary message and be added
            /// to our private client list. Also called when the topic is newly created and clients already exist. 
            /// </summary>
            internal async Task AddClientsAsync(IEnumerable<DataStreamConnection> newClients) {
                using (await Lock.LockAsync().ConfigureAwait(false)) {

                    /// New clients are added to the main list first, because they need to be present in the main list
                    /// for removal by the <see cref="ActuallyEnqueueAsync(IEnumerable{DataStreamConnection}, MessageEnvelope)"/> method
                    /// if they fail to enqueue the topicSummary message.
                    Clients.AddRange(newClients);

                    /// Then we automatically send the topic summary message to all new clients.
                    var topicSummary = await TopicSummaryManager.GetTopicSummary().ConfigureAwait(false);
                    if (null != topicSummary) {
                        /// Enqueues the topic summary to all the new clients, and removes clients from the main 
                        /// client list if the topic summary fails to be enqueued.
                        await ActuallyEnqueueAsync(newClients, topicSummary).ConfigureAwait(false);
                    }
                }
            }

            /// <summary>
            /// Removes the client from the client list if it exists there.
            /// Called by the publisher when a client DataStreamConnection has been disconnected.
            /// </summary>
            internal async Task RemoveClientAsync(DataStreamConnection client) {
                using (await Lock.LockAsync().ConfigureAwait(false)) {
                    Clients.Remove(client);
                }
            }

            /// <summary>
            /// Called by the publisher in either of the following 2 circumstances:
            /// a) The app using the publisher has called the publisher's RemoveTopicAsync method for this topic, or,
            /// b) The publisher is being disposed and needs to shutdown all the topics.
            /// </summary>
            /// <returns></returns>
            internal async Task ShutdownAsync() {
                using (await Lock.LockAsync().ConfigureAwait(false)) {
                    _shutdown = true;
                    TopicSummaryManager.Dispose();
                }
            }


            #region **** Methods called by the user ****
            #endregion


            /// <summary>
            /// Enqueues the message for sending to all clients.
            /// </summary>
            /// <returns>IOperations representing the sending completion to each client.</returns>
            /// <exception cref="ObjectDisposedException">Thrown if the topic has been shutdown.</exception>
            public async Task<IEnumerable<IOperation>> EnqueueAsync(object message) {
                var envelope = Encoder.Encode(Definition, message);
                using (await Lock.LockAsync().ConfigureAwait(false)) {
                    if (_shutdown) throw new ObjectDisposedException(nameof(IDataStreamTopic));
                    /// Inform the topic summary manager of the message, so that it can update the topic summary
                    /// message that it creates for newly-connecting clients.
                    await TopicSummaryManager.OnMessage(envelope).ConfigureAwait(false);
                    /// And actually enqueue the message to the clients that are already connected.
                    return await ActuallyEnqueueAsync(Clients, envelope).ConfigureAwait(false);
                }
            }


            #region **** Common helper methods ****
            #endregion


            /// <summary>
            /// Call this method ONLY from within the Lock!!
            /// Enqueues the given message envelope to the given clients, removing any clients that fail to enqueue it.
            /// Returns IOperations representing the completed sending of the message - just in case the sender wants to track
            /// actual arrival of the messages.
            /// </summary>
            async Task<IEnumerable<IOperation>> ActuallyEnqueueAsync(IEnumerable<DataStreamConnection> clients, MessageEnvelope envelope) {
                var operations = new ConcurrentBag<IOperation>();
                var failedClients = new ConcurrentBag<DataStreamConnection>();
                var tasks = clients.Select(async (c) => {
                    try {
                        operations.Add(await c.EnqueueAsync(envelope).ConfigureAwait(false));
                    } catch (Exception x) {
                        /// Removing failed clients immediately could result in the "collection was modified" exception,
                        /// so we save a list of the failed clients instead, and remove them after the enumeration has completed.
                        failedClients.Add(c);
                        operations.Add(TaskOperation.FromException(new Exception($"Connection to '{c.RemoteEndPoint}' was already closed.", x)));
                    }
                });
                await Task.WhenAll(tasks).ConfigureAwait(false);

                /// Remove clients from the main client list if they failed to enqueue the topic summary message.
                /// Message enqueing failures happen when the client DataStreamConnection has been disposed. We don't 
                /// have to remove them from the Publisher itself - the DataStreamConnection will have already reported itself 
                /// failed to the Publisher when it disposed itself.
                foreach (var failedClient in failedClients)
                    Clients.Remove(failedClient);

                return operations;
            }
        }

        #endregion
    }
}
