using Apex.LoggingUtils;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CT = System.Threading.CancellationToken;
using CTS = System.Threading.CancellationTokenSource;
using CTR = System.Threading.CancellationTokenRegistration;
using static Apex.TaskUtilities.Tasks;
using Apex.TimeStamps;
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using Apex.DataStreams.AdminMessages;
using Apex.DataStreams.Encoding;
using Apex.DataStreams.Operations;
using System.Net;
using System.Diagnostics;
using Apex.ServiceUtilities;

namespace Apex.DataStreams.Connections {

    /// <summary>
    /// This connection is used both by the Publisher AND the Client.
    /// It encapsulates all the logic for sending/receiving messages and administration topic messages such as heart beats.
    /// This DataStreamConnection will automatically dispose itself when there is a disconnection event.
    /// This DataStreamConnection will dispose the socket you passed in its constructor when it is disposed.
    /// </summary>
    internal sealed class Connection : ServiceBase, IDisposable {

        static readonly TimeSpan HeartBeatSendInterval = TimeSpan.FromSeconds(1);
        static readonly TimeSpan HeartBeatReceiveInterval = TimeSpan.FromSeconds(30);

        readonly ConnectionContext Context;
        readonly AsyncProducerConsumerQueue<SendOperation> SendQueue;
        readonly MessageEnvelope HeartBeat;
        readonly System.Timers.Timer RxTimer;
        readonly System.Timers.Timer TxTimer;
        readonly StatusManager Status;
        readonly byte AdminTopicCode;

        long _disconnected = 0;

        public EndPoint RemoteEndPoint { get; }

        /// <summary>
        /// Creates the DataStreamConnection using the given definition, encoder, and socket. 
        /// </summary>
        public Connection(ConnectionContext context) {
            Context = context;
            RemoteEndPoint = Context.Socket.RemoteEndPoint;
            HeartBeat = Context.Encoder.Encode(Context.Definition.GetAdminTopic(), new HeartBeat());
            SendQueue = new AsyncProducerConsumerQueue<SendOperation>();
            Status = new StatusManager(context.Socket.RemoteEndPoint);
            AdminTopicCode = context.Definition.GetAdminTopic().TopicCode;
            RxTimer = new System.Timers.Timer(Debugger.IsAttached ? TimeSpan.FromMinutes(10).TotalMilliseconds : HeartBeatReceiveInterval.TotalMilliseconds) { AutoReset = false };
            TxTimer = new System.Timers.Timer(HeartBeatSendInterval.TotalMilliseconds) { AutoReset = false };
            RxTimer.Elapsed += RxTimer_Elapsed;
            TxTimer.Elapsed += TxTimer_Elapsed;
        }

        protected override void CustomStart() {
            RxTimer.Start();
            TxTimer.Start();
            FireAndForget(SendWorker);
            FireAndForget(ReceiveWorker);
        }

        protected override void CustomDispose(bool disposing) {
            /// If we're being disposed by a calling object or the GC finalizer, this needs to happen.
            OnDisconnection(new Exception("Disposing.")).GetAwaiter().GetResult();
            /// And dispose our managed disposables.
            RxTimer.Dispose();
            TxTimer.Dispose();
            try { Context.Socket.Shutdown(SocketShutdown.Both); } catch { }
            try { Context.Socket.Close(); } catch { }
            try { Context.Socket.Dispose(); } catch { }
        }

        /// <summary>
        /// Causes a disconnection if no messages have been received within the heart beat receive interval.
        /// The RxTimer is reset every time a message is received. So, if this method runs, we definitely need to cause a disconnect.
        /// </summary>
        void RxTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
            OnDisconnection(new Exception("No messages received.")).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Sends a heart beat message periodically, if no other messages have been sent.
        /// </summary>
        void TxTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
            /// Timer automatically stops since its <see cref="System.Timers.Timer.AutoReset"/> property is False.
            /// It will be restarted by the send worker as soon as it dequeues the heart beat message.
            /// Using a stopped timer allows us to kick off an async task for enqueuing the operation instead of blocking this threadpool thread
            /// while waiting for the operation to be enqueued.

            /// Kick off the async enqueue. An exception can be thrown if we are disconnecting at the time, so we use FireAndForget to observe and swallow it. 
            FireAndForget(async () => {
                await EnqueueAsync(HeartBeat).ConfigureAwait(false);
            });
        }

        /// <summary>
        /// Enqueues a message for sending.
        /// Returns an <see cref="IOperation"/> object that you can use to await actual completion of the send operation.
        /// Throws an exception if we have been disconnected.
        /// </summary>
        /// <exception cref="Exception">Thrown if we have been disconnected and messages can no longer be enqueued.</exception>
        public async Task<IOperation> EnqueueAsync(MessageEnvelope envelope) {

            /// First level of disconnection exception throwing.
            if (Interlocked.Read(ref _disconnected) == 1)
                throw new Exception("Disconnected");

            var operation = new SendOperation(envelope);
            try {
                /// Actually queue the send operation. If we are currently disconnecting, the <see cref="SendQueue"/> will refuse 
                /// to have more operations added and will throw an <see cref="InvalidOperationException"/>.
                await SendQueue.EnqueueAsync(operation).ConfigureAwait(false);
                Status.OnMessageEnqueued();
            } catch (InvalidOperationException) {
                /// It's not necessary to fail the operation for the benefit of the calling object, because it's never returned to the calling object,
                /// however we remain aware that the operation does contain an uncompleted TaskCompletionSource object. To my understanding, there are no
                /// consequences for leaving it uncompleted, so we don't have to fail the operation here. Just go ahead and let the calling object know 
                /// that the send operation was NOT enqueued.
                throw new Exception("Disconnected");
            }
            return operation;
        }

        /// <summary>
        /// Runs until we are disposed. Causes disposal if an exception occurs.
        /// Takes messages out of the <see cref="SendQueue"/> and sends them via the socket.
        /// Causes a disconnection when the message sending time exceeds a threshold.
        /// </summary>
        async Task SendWorker() {
            try {
                while (true) {
                    /// Wait for an operation to become available, or throw an exception when we are disposed.
                    /// If calling objects don't enqueue messages, the TxTimer will be enqueuing heart beat messages,
                    /// so we'll never wait here longer than the heart beat sending interval.
                    var operation = await SendQueue.DequeueAsync(DisposedToken).ConfigureAwait(false);
                    try {
                        /// Check if the message has been waiting a long time to send. If so, cause a disconnection.
                        var timeWaitingInTicks = TimeStamp.Now.TicksUtc - operation.EnqueuedAt.TicksUtc;
                        if (timeWaitingInTicks > (Debugger.IsAttached ? TimeSpan.FromMinutes(10).Ticks : Context.MaxMessageSendTimeInSeconds * TimeSpan.TicksPerSecond))
                            throw new Exception($"Message waited longer than '{Context.MaxMessageSendTimeInSeconds}' seconds to be sent.");

                        /// Starts the heart beat sending timer if it is stopped. Restarts it if it is already running.
                        /// A restart would be required to prevent sending unecessary heart beat messages.
                        TxTimer.Stop(); // Just learned that the timer is not restarted by the "Start()" method alone. Need to be stopped first. 
                        TxTimer.Start();

                        /// Actually send the message over the wire.
                        await Context.Socket.SendAllBytes(operation.Envelope.MessageBytes).ConfigureAwait(false);
                        var isAdminMessage = operation.Envelope.TopicCode == AdminTopicCode;
                        Status.OnMessageSent(isAdminMessage, operation.Envelope.MessageBytes.Length);

                        /// Notify anybody waiting on the send operation that it has been completed.
                        operation.Succeed();

                    } catch (Exception x) {
                        /// Notify anybody waiting on the send operation that it has failed.
                        operation.Fail(x);
                        /// And rethrow the exception to the outer handler.
                        ExceptionDispatchInfo.Capture(x).Throw();
                    }
                }
            } catch (Exception x) {
                /// Cause a disconnection when sending fails.
                await OnDisconnection(x).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Runs until we are disposed. Causes disposal if an exception occurs.
        /// Reads messages out of the socket and places them in the <see cref="ConnectionContext.ReceiveQueue"/>.
        /// </summary>
        async Task ReceiveWorker() {
            try {
                while (true) {

                    /// Read a message from the socket. The socket will throw an ObjectDisposedException when 
                    /// we are disposed, ensuring this method exits the while(true) loop at the correct time.
                    /// The encoder also throws an exception if there are any problems reading from the socket.
                    var envelope = await Context.Encoder.ReadMessageFromSocketAsync(Context.Definition, Context.Socket).ConfigureAwait(false);
                    var isAdminMessage = envelope.TopicCode == AdminTopicCode;
                    Status.OnMessageReceived(isAdminMessage, envelope.MessageBytes.Length);
                    if (!isAdminMessage) {
                        await Context.ReceiveQueue.EnqueueAsync(envelope).ConfigureAwait(false);
                    }


                    /// Starts the message receive timer if it is stopped. Restarts it if it is already running.
                    /// A restart would be required to prevent unecessary disconnection.
                    RxTimer.Stop(); // Just learned that the timer is not restarted by the "Start()" method alone. Need to be stopped first. 
                    RxTimer.Start();
                }
            } catch (Exception x) {
                await OnDisconnection(x).ConfigureAwait(false);
            }
        }

        async Task OnDisconnection(Exception x) {
            /// Since the OnDisconnection method is called from multiple places (Disposed, SendWorker, ReceiveWorker, RxTimer), 
            /// we need to make sure it runs only once. Also prevents a stack dive between Dispose and OnDisconnected.
            if (Interlocked.CompareExchange(ref _disconnected, 1, 0) == 1) return;

            /// Prevent any more messages being added to the SendQueue.
            /// This will cause our <see cref="EnqueueAsync(MessageEnvelope)"/> method to throw exceptions, notifying calling objects that 
            /// we are no longer connected.
            SendQueue.CompleteAdding();

            /// Now that messages can no longer be added to the SendQueue,
            /// it is safe to begin failing all the operations remaining in it, knowing that no more will be added.
            foreach (var operation in SendQueue.GetConsumingEnumerable())
                operation.Fail(x);

            Status.OnDisconnection(x.UnwindMessage());

            if (null != Context.DisconnectedCallback) {
                await Context.DisconnectedCallback(this, x).ConfigureAwait(false);
            }

            /// This object auto-disposes itself when it becomes disconnected.
            Dispose();
        }

        public Task<ConnectionStatus> GetStatusAsync()
            => Status.GetStatusAsync();

        class StatusManager : IDisposable {

            readonly TimeStamp ConnectedAt;
            readonly EndPoint RemoteEndPoint;
            readonly RecentEventCounter BytesReceivedCounter = new RecentEventCounter(TimeSpan.FromSeconds(60));
            readonly RecentEventCounter BytesSentCounter = new RecentEventCounter(TimeSpan.FromSeconds(60));
            readonly RecentEventCounter MessagesReceivedCounter = new RecentEventCounter(TimeSpan.FromSeconds(60));
            readonly RecentEventCounter MessagesSentCounter = new RecentEventCounter(TimeSpan.FromSeconds(60));

            long _sendQueueLength = 0;
            string _disconnectionErrorMessage = null;

            public StatusManager(EndPoint remoteEndPoint) {
                ConnectedAt = TimeStamp.Now;
                RemoteEndPoint = remoteEndPoint;
            }

            public void OnMessageEnqueued() {
                Interlocked.Increment(ref _sendQueueLength);
            }

            public void OnMessageSent(bool isAdminMessage, int numBytes) {
                Interlocked.Decrement(ref _sendQueueLength);
                BytesSentCounter.Increment(numBytes);
                if (!isAdminMessage)
                    MessagesSentCounter.Increment(1);
            }

            public void OnMessageReceived(bool isAdminMessage, int numBytes) {
                BytesReceivedCounter.Increment(numBytes);
                if (!isAdminMessage)
                    MessagesReceivedCounter.Increment(1);
            }

            public void OnDisconnection(string disconnectionErrorMessage) {
                _disconnectionErrorMessage = disconnectionErrorMessage;
            }

            public Task<ConnectionStatus> GetStatusAsync() {
                return Task.FromResult(new ConnectionStatus {
                    ConnectedAt = ConnectedAt,
                    BytesReceived = BytesReceivedCounter.GetCount(),
                    BytesSent = BytesSentCounter.GetCount(),
                    MessagesReceived = MessagesReceivedCounter.GetCount(),
                    MessagesSent = MessagesSentCounter.GetCount(),
                    DisconnectionErrorMessage = _disconnectionErrorMessage,
                    SendQueueLength = Interlocked.Read(ref _sendQueueLength),
                    RemoteEndPoint = RemoteEndPoint,
                });
            }

            public void Dispose() {
                BytesReceivedCounter.Dispose();
                BytesSentCounter.Dispose();
                MessagesReceivedCounter.Dispose();
                MessagesSentCounter.Dispose();
            }
        }
    }
}
