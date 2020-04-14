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
using System.Net;
using System.Diagnostics;
using Apex.ServiceUtilities;
using System.Threading.Channels;
using Nerdbank.Streams;
using System.IO.Pipelines;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.Extensions.DependencyInjection;
using Apex.PipeEncoding;

namespace Apex.DataStreams.Connections {

    /// <summary>
    /// This connection is used both by the Publisher AND the Client.
    /// It encapsulates all the logic for sending/receiving messages and administration topic messages such as heart beats.
    /// This Connection will automatically dispose itself when there is a disconnection event.
    /// This Connection will dispose the socket you passed in its constructor when it is disposed.
    /// </summary>
    internal sealed class Connection : ServiceBase, IDisposable {

        readonly IDuplexPipe Pipe;
        readonly StatusManager Status;
        readonly ClientContext Context;
        readonly Channel<object> SendQueue;
        readonly IPipeEncoderFactory PipeEncoderFactory;
        readonly IPipeDecoderFactory PipeDecoderFactory;
        readonly Socket Socket;
        readonly Func<Connection, Exception, Task> DisconnectedCallback;

        long _disconnected = 0;
        volatile bool _messageSent = false;
        volatile bool _messageReceived = false;

        public Connection(ClientContext context, Socket socket, Func<Connection, Exception, Task> disconnectedCallback) {
            Context = context;
            Socket = socket;
            DisconnectedCallback = disconnectedCallback;
            SendQueue = Channel.CreateUnbounded<object>();
            Status = new StatusManager(Socket.RemoteEndPoint);

            PipeEncoderFactory = Context.Services.GetRequiredService<IPipeEncoderFactory>();
            PipeDecoderFactory = Context.Services.GetRequiredService<IPipeDecoderFactory>();

            var networkStream = new NetworkStream(Socket, ownsSocket: false);
            Pipe = networkStream.UsePipe(cancellationToken: DisposedToken);
        }

        protected override void CustomStart() {
            FireAndForget(SendWorker);
            FireAndForget(ReceiveWorker);
            FireAndForget(ReceiveTimer);
            FireAndForget(HeartBeatTimer);
        }

        protected override void CustomDispose(bool disposing) {
            OnDisconnection(new Exception("Disposing.")).GetAwaiter().GetResult();
            Status.Dispose();
            try { Socket.Shutdown(SocketShutdown.Both); } catch { }
            try { Socket.Close(); } catch { }
            try { Socket.Dispose(); } catch { }
        }

        async Task ReceiveTimer() {
            while (true) {
                await Task.Delay(10000, DisposedToken).ConfigureAwait(false);
                if (_messageReceived) {
                    _messageReceived = false;
                } else {
                    OnDisconnection(new Exception("No messages received.")).GetAwaiter().GetResult();
                }
            }
        }

        async Task HeartBeatTimer() {
            while (true) {
                await Task.Delay(1000, DisposedToken).ConfigureAwait(false);
                if (_messageSent) {
                    _messageSent = false;
                } else {
                    await Enqueue(new HeartBeat()).ConfigureAwait(false);
                }
            }
        }

        public ValueTask Enqueue(object message) {
            Status.OnMessageEnqueued();
            return SendQueue.Writer.WriteAsync(message);
        }

        /// <summary>
        /// Runs until we are disposed. Causes disposal if an exception occurs.
        /// Takes messages out of the <see cref="SendQueue"/> and sends them via the socket.
        /// </summary>
        async Task SendWorker() {
            var pipeWriter = Pipe.Output;
            try {
                while (true) {
                    DisposedToken.ThrowIfCancellationRequested();
                    var message = await SendQueue.Reader.ReadAsync(DisposedToken).ConfigureAwait(false);
                    var messageType = message.GetType();
                    var messageTypeCode = Context.Schema.GetMessageTypeCode(messageType);
                    pipeWriter.GetSpan(1)[0] = messageTypeCode;
                    pipeWriter.Advance(1);
                    var encoder = PipeEncoderFactory.GetPipeWriter(messageType);
                    encoder.Encode(pipeWriter, message);
                    await pipeWriter.FlushAsync().ConfigureAwait(false);
                    Status.OnMessageSent(isAdminMessage: messageType == typeof(HeartBeat));
                    _messageSent = true;
                }
            } catch (Exception x) {
                await OnDisconnection(x).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Runs until we are disposed. Causes disposal if an exception occurs.
        /// Reads messages out of the socket and places them in the <see cref="ConnectionContext.ReceiveQueue"/>.
        /// </summary>
        async Task ReceiveWorker() {
            var pipeReader = Pipe.Input;
            try {
                while (true) {
                    var readResult = await pipeReader.ReadAsync(DisposedToken).ConfigureAwait(false);
                    if (readResult.IsCanceled) throw new Exception("Reader was canceled.");
                    var buffer = readResult.Buffer;
                    if (buffer.Length == 0 && readResult.IsCompleted) throw new Exception("Buffer is completed.");
                    var messageType = Context.Schema.GetMessageType(buffer.First.Span[0]);
                    pipeReader.AdvanceTo(buffer.GetPosition(1));
                    var decoder = PipeDecoderFactory.GetPipeReader(messageType);
                    var message = await decoder.Decode(pipeReader).ConfigureAwait(false);
                    var isAdminMessage = messageType == typeof(HeartBeat);
                    Status.OnMessageReceived(isAdminMessage);
                    if (!isAdminMessage) {
                        await Context.ReceiveQueue.WriteAsync(message).ConfigureAwait(false);
                    }
                    _messageReceived = true;
                }
            } catch (Exception x) {
                await OnDisconnection(x).ConfigureAwait(false);
            }
        }


        async Task OnDisconnection(Exception x) {
            /// Since the OnDisconnection method is called from multiple places (Disposed, SendWorker, ReceiveWorker, RxTimer), 
            /// we need to make sure it runs only once. Also prevents a stack dive between Dispose and OnDisconnection.
            if (Interlocked.CompareExchange(ref _disconnected, 1, 0) == 1) return;
            Status.OnDisconnection(x.UnwindMessage());
            if (DisconnectedCallback is object) {
                await DisconnectedCallback(this, x).ConfigureAwait(false);
            }
            Dispose();
        }

        public Task<ConnectionStatus> GetStatusAsync()
            => Status.GetStatusAsync();

        class StatusManager : IDisposable {

            readonly TimeStamp ConnectedAt;
            readonly EndPoint RemoteEndPoint;
            readonly RecentEventCounter MessagesReceivedCounter = new RecentEventCounter(TimeSpan.FromSeconds(60));
            readonly RecentEventCounter MessagesSentCounter = new RecentEventCounter(TimeSpan.FromSeconds(60));

            long _sendQueueLength = 0;
            string _disconnectionErrorMessage = null;

            public StatusManager(EndPoint remoteEndPoint) {
                ConnectedAt = TimeStamp.Now;
                RemoteEndPoint = remoteEndPoint;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnMessageEnqueued()
                => Interlocked.Increment(ref _sendQueueLength);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnMessageSent(bool isAdminMessage) {
                Interlocked.Decrement(ref _sendQueueLength);
                if (!isAdminMessage)
                    MessagesSentCounter.Increment(1);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnMessageReceived(bool isAdminMessage) {
                if (!isAdminMessage)
                    MessagesReceivedCounter.Increment(1);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OnDisconnection(string disconnectionErrorMessage)
                => _disconnectionErrorMessage = disconnectionErrorMessage;

            public Task<ConnectionStatus> GetStatusAsync() {
                return Task.FromResult(new ConnectionStatus {
                    ConnectedAt = ConnectedAt,
                    MessagesReceived = MessagesReceivedCounter.GetCount(),
                    MessagesSent = MessagesSentCounter.GetCount(),
                    DisconnectionErrorMessage = _disconnectionErrorMessage,
                    SendQueueLength = Interlocked.Read(ref _sendQueueLength),
                    RemoteEndPoint = RemoteEndPoint,
                });
            }

            public void Dispose() {
                MessagesReceivedCounter.Dispose();
                MessagesSentCounter.Dispose();
            }
        }
    }
}
