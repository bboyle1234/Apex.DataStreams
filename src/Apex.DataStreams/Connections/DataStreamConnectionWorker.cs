using Nito.AsyncEx;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CT = System.Threading.CancellationToken;
using CTS = System.Threading.CancellationTokenSource;
using static Apex.TaskUtilities.Tasks;
using Apex.DataStreams.Definitions;
using Apex.DataStreams.Encoding;
using System.Net;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Apex.TimeStamps;
using System.Collections.Immutable;

namespace Apex.DataStreams.Connections {

    /// <summary>
    /// Used the by Client, this class helps to continually reconnect to the server.
    /// </summary>
    internal sealed class DataStreamConnectionWorker : IDisposable {

        /// <summary>
        /// Size of receive buffer before blocking occurs.
        /// </summary>
        private const int ReceiveBufferSize = 8 * 1024; // bytes

        /// <summary>
        /// Time in which a connection must either succeed or fail.
        /// </summary>
        public const int ConnectTimeout = 3000; // milliseconds

        readonly string PublisherEndPoint;
        readonly DataStreamDefinition DataStreamDefinition;
        readonly CTS Disposed;
        readonly CT DisposedToken;
        readonly IEncoder Encoder;
        readonly AsyncProducerConsumerQueue<MessageEnvelope> ReceiveQueue;
        readonly ILogger Log;

        long _started = 0;
        long _disposed = 0;
        DataStreamConnection _remote = null;
        ImmutableList<DisconnectionEvent> _disconnections = ImmutableList<DisconnectionEvent>.Empty;

        public DataStreamConnectionWorker(IServiceProvider serviceProvider, string publisherEndPoint, DataStreamDefinition dataStreamDefinition, AsyncProducerConsumerQueue<MessageEnvelope> receiveQueue) {
            Log = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger($"{nameof(DataStreamConnectionWorker)}_{publisherEndPoint}");
            PublisherEndPoint = publisherEndPoint;
            DataStreamDefinition = dataStreamDefinition;
            ReceiveQueue = receiveQueue;
            Disposed = new CTS();
            DisposedToken = Disposed.Token;
            Encoder = serviceProvider.GetRequiredService<IEncoder>();
        }

        public void Start() {
            if (Interlocked.CompareExchange(ref _started, 1, 0) == 1) throw new InvalidOperationException("Cannot be started more than once.");
            FireAndForget(ConnectWorker);
        }

        /// <summary>
        /// Runs until we are disposed.
        /// Continually reconnects to the DataStream publisher after disconnection.
        /// </summary>
        async Task ConnectWorker() {

            IPEndPoint endPoint = null;
            Socket socket = null;

            /// Initialise this flag "true" so that we can immediately connect on the first go-around.
            /// Afterwards, this flag is set either by a) Failed connection attempt, or b) Disconnection.
            var connectionNeeded = new AsyncAutoResetEvent(true);
            while (true) {
                try {
                    await connectionNeeded.WaitAsync(DisposedToken).ConfigureAwait(false);
                    /// We go through the whole process of resolving the ip from scratch every time because, in the docker environment, 
                    /// the target container's ip can change at any time when it's re-instantiated.
                    await SetIPEndPoint().ConfigureAwait(false);
                    /// Actually connect the socket
                    await EstablishConnection().ConfigureAwait(false);
                    /// Then create and start the DataStreamConnection that will be handling the underlying data transfer protocol
                    var context = new DataStreamConnectionContext {
                        Definition = DataStreamDefinition,
                        Encoder = Encoder,
                        Socket = socket,
                        ReceiveQueue = ReceiveQueue,
                        DisconnectedCallback = OnDisconnected,
                    };
                    _remote = new DataStreamConnection(context);
                    _remote.Start();
                } catch (OperationCanceledException) { // Happens when we are disposed.
                    /// Shuts down the current, established connection if it exists.
                    try { _remote?.Dispose(); } catch { }
                    try { socket?.Dispose(); } catch { } // Disposing the remote also disposes the socket, but it's possible for the socket to exist when the remote doesn't, and in that circumstance, we need to make sure we also dispose the socket.
                    /// Our work is done --- let's get outta the method!!
                    return;
                } catch (Exception x) { // Happens when connection failed.
                    Log.LogError(x, $"Unable to establish connection to '{PublisherEndPoint}'.");
                    AddDisconnectionEvent(x);
                    /// Cleans up partially-created objects.
                    try { _remote?.Dispose(); } catch { }
                    try { socket?.Dispose(); } catch { } // Disposing the remote also disposes the socket, but it's possible for the socket to exist when the remote doesn't, and in that circumstance, we need to make sure we also dispose the socket.
                    /// Wait a second before signalling that we need to attempt another connection, because this one failed.
                    SetConnectionNeeded(TimeSpan.FromSeconds(1));
                }
            }

            /// Figures out the IP End Point from values supplied in the configuration, and throws exceptions with good explanations.
            async Task SetIPEndPoint() {
                try {
                    var parts = PublisherEndPoint.Split(':');
                    var ip = (await Dns.GetHostAddressesAsync(parts[0]).ConfigureAwait(false)).First(x => x.AddressFamily == AddressFamily.InterNetwork);
                    var port = int.Parse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                    endPoint = new IPEndPoint(ip, port);
                } catch (Exception x) {
                    throw new Exception($"Error parsing IPEndpoint from value '{PublisherEndPoint}'. Expected format is '[host|ip]:port'.", x);
                }
            }

            /// Responsible for handling connection logic and throwing exceptions with good explanations.
            async Task EstablishConnection() {
                try {
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.ReceiveBufferSize = ReceiveBufferSize;
                    socket.NoDelay = true;
                    socket.LingerState = new LingerOption(false, 0);
                    await socket.ConnectAsync(endPoint).ConfigureAwait(false);
                } catch (Exception x) {
                    throw new Exception($"Error connecting to '{endPoint}'.", x);
                }
            }

            /// Handles disconnection. 
            async Task OnDisconnected(DataStreamConnection r, Exception x) {
                Log.LogError(x, $"Disconnected from '{PublisherEndPoint}' @ '{endPoint}'.");
                AddDisconnectionEvent(x);
                /// Dispose the remote and the socket.
                /// It was coded to automatically dispose itself and the socket on disconnection, these lines of code are just "being polite", 
                /// but they are included anyways because well, you never know, maybe the implementation of the DataStreamConnection class could
                /// change one day.
                try { _remote?.Dispose(); } catch { } // Socket and remote should never be null here, but I coded it like this anyways.
                try { socket?.Dispose(); } catch { }
                /// Wait a second before setting the signal to create a new connection.
                SetConnectionNeeded(TimeSpan.FromSeconds(1));
                /// Just keeps the compiler happy.
                await Task.Yield();
            }

            /// Sets the ConnectionNeeded event after the given wait time if we have not been disposed first.
            void SetConnectionNeeded(TimeSpan wait) {
                FireAndForget(async () => {
                    /// The OperationCanceledException thrown due to disposal is eaten by the FireAndForget method.
                    await Task.Delay(wait, DisposedToken);
                    /// Allows the ConnectionWorker task to start setting up a new connection.
                    connectionNeeded.Set();
                });
            }
        }

        void AddDisconnectionEvent(Exception x) {
            var tenMinutesAgo = TimeStamp.Now.Subtract(TimeSpan.FromMinutes(10));
            _disconnections = _disconnections.Add(new DisconnectionEvent {
                TimeStamp = TimeStamp.Now,
                Exception = x,
                RemoteEndPoint = PublisherEndPoint,
            }).RemoveAll(d => d.TimeStamp < tenMinutesAgo);
        }

        public async Task<DataStreamConnectionWorkerStatus> GetStatusAsync() {
            var status = new DataStreamConnectionWorkerStatus();
            status.EndPoint = PublisherEndPoint;
            var current = _remote;
            if (null != current) {
                status.CurrentConnection = await current.GetStatusAsync().ConfigureAwait(false);
            }
            var tenMinutesAgo = TimeStamp.Now.Subtract(TimeSpan.FromMinutes(10));
            status.RecentDisconnections = _disconnections.Where(d => d.TimeStamp > tenMinutesAgo).ToList();
            return status;
        }

        #region IDisposable

        public void Dispose() {
            /// If we've already been disposed, quietly exit
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 1) return;
            Disposed.Cancel();
            Disposed.Dispose();
            GC.SuppressFinalize(this);
        }

        ~DataStreamConnectionWorker() => Dispose();

        #endregion
    }
}
