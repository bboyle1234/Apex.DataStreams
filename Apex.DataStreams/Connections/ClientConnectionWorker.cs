using Apex.ServiceUtilities;
using Apex.TimeStamps;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static Apex.TaskUtilities.Tasks;

namespace Apex.DataStreams.Connections {

    /// <summary>
    /// Used the by Client, this class helps to maintain a continuous connection to a single endpoint, reconnecting when necessary.
    /// If a client is configured to use multiple endpoints, it will run one of these objects for each endpoint.
    /// </summary>
    internal sealed class ClientConnectionWorker : ServiceBase {

        readonly ILogger Log;
        readonly ClientContext ClientContext;
        readonly RecentEventCounter<DisconnectionEvent> RecentDisconnections;

        Connection _connection = null;

        public ClientConnectionWorker(IServiceProvider serviceProvider, ClientContext clientContext) {
            ClientContext = clientContext;
            Log = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger($"{nameof(ClientConnectionWorker)}_{ClientContext.PublisherEndPoint}");
            RecentDisconnections = new RecentEventCounter<DisconnectionEvent>(TimeSpan.FromMinutes(1));
        }

        protected override void CustomStart() {
            FireAndForget(ConnectWorker);
        }

        protected override void CustomDispose(bool disposing) {
            RecentDisconnections.Dispose();
        }

        /// <summary>
        /// Runs until we are disposed.
        /// Continually reconnects to the DataStream publisher after disconnection.
        /// </summary>
        async Task ConnectWorker() {

            Socket socket = null;

            /// Initialise this flag "true" so that we can immediately connect on the first go-around.
            /// Afterwards, this flag is set either by a) the failed connection exception handler, or b) the disconnection callback.
            var connectionNeeded = new AsyncAutoResetEvent(true);

            while (true) {
                try {
                    await connectionNeeded.WaitAsync(DisposedToken).ConfigureAwait(false);

                    var parts = ClientContext.PublisherEndPoint.Split(':');
                    var host = parts[0];
                    var port = int.Parse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture);
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.LingerState = new LingerOption(false, 0);
                    await socket.ConnectAsync(host, port).ConfigureAwait(false);

                    _connection = new Connection(ClientContext, socket, OnDisconnected);
                    _connection.Start();

                } catch (OperationCanceledException) {

                    /// Happens when we are disposed.
                    Cleanup();
                    return; /// Careful to make sure we exit the method entirely.

                } catch (Exception x) {

                    /// Happens when we failed to establish a connection.
                    await OnDisconnected(null, x).ConfigureAwait(false);
                    /// Now go back to the top of the while loop and wait for the "connectionNeeded" event to be set.
                }
            }

            /// Handles disconnection. 
            Task OnDisconnected(Connection r, Exception x) {
                Log.LogError(x, $"Disconnected from '{ClientContext.PublisherEndPoint}'.");

                /// Adds a disconnection event to the disconnection event counter. 
                /// We do this for status reporting, so an administrator can see if the connection is behaving nicely, 
                /// or getting regularly disconnected.
                RecentDisconnections.Increment(new DisconnectionEvent {
                    TimeStamp = TimeStamp.Now,
                    Exception = x,
                    RemoteEndPoint = ClientContext.PublisherEndPoint,
                });

                Cleanup();

                /// Allows the connection worker to initiate a new connection after the given delay.
                FireAndForget(TimeSpan.FromSeconds(1), connectionNeeded.Set, DisposedToken);
                return Task.CompletedTask;
            }

            void Cleanup() {

                /*****************************************************************************
                 * Since "_remote" owns the socket and disposes the socket, it would normally
                 * be sufficient in most cases to only call dispose on the "_connection". 
                 * But if an exception happened above after the socket was created but before
                 * the "_connection" was created, (such as on a failed connection attempt) we would 
                 * be left with an undisposed socket.
                 * Therefore the cleanup code below calls dispose on both the _connection and the socket.
                *****************************************************************************/

                try { _connection?.Dispose(); } catch { }
                try { socket?.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// Gets a user-friendly summary of the connection's performance.
        /// </summary>
        public async Task<ConnectionWorkerStatus> GetStatusAsync() {
            var current = _connection; // grab a reference to prevent a thread race between checking for null and calling the GetStatusAsync method
            return new ConnectionWorkerStatus {
                EndPoint = ClientContext.PublisherEndPoint,
                RecentDisconnections = RecentDisconnections.GetRecentEvents().ToList(),
                CurrentConnection = current is null ? null : await current.GetStatusAsync().ConfigureAwait(false)
            };
        }
    }
}
