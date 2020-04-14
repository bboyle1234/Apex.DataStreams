using Apex.DataStreams.Topics;
using Apex.PipeEncoding;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Apex.DataStreams.Benchmarking {

    [MemoryDiagnoser]
    public class SpeedTest {

        IServiceProvider Services;
        Publisher Publisher;
        Client Client;
        Channel<object> ReceiveQueue;
        ITopic Topic;

        [GlobalSetup]
        public async Task Setup() {

            Services = new ServiceCollection()
                .AddDataStreams()
                .AddPipeEncoders(Assembly.GetExecutingAssembly())
                .AddPipeDecoders(Assembly.GetExecutingAssembly())
                .UseServiceProviderPipeDecoderFactory()
                .UseServiceProviderPipeEncoderFactory()
                .AddLogging(builder => {
                    builder.SetMinimumLevel(LogLevel.Trace);
                    builder.AddConsole(options => options.IncludeScopes = true);
                    builder.AddFilter(LoggerFilter);
                    builder.AddApplicationInsights(options => {
                        options.IncludeScopes = true;
                        options.TrackExceptionsAsExceptionTelemetry = true;
                    });
                })
                .BuildServiceProvider();

            ReceiveQueue = Channel.CreateUnbounded<object>();

            var schema = Schema.Create()
                .Add(1, typeof(SomeObject))
                .Build();

            Publisher = new Publisher(Services, new PublisherConfiguration {
                ListenPort = 81,
                Schema = schema,
            }, (p, x) => Task.CompletedTask);
            Publisher.Start();
            Topic = await Publisher.CreateTopic(new SimpleTopicSummary()).ConfigureAwait(false);

            Client = new Client(Services, new[] {
                new ClientContext {
                     PublisherEndPoint="localhost:81",
                     Schema = schema,
                     Services = Services,
                     ReceiveQueue = ReceiveQueue.Writer,
                }
            });
            Client.Start();

            while (true) {
                var clientStatus = await Client.GetStatus().ConfigureAwait(false);
                if (clientStatus.Connections[0].CurrentConnection is object) {
                    return;
                }
            }
        }

        [Benchmark]
        public async Task Run() {
            var sw = Stopwatch.StartNew();
            var task1 = Task.Run(async () => {
                for (var i = 0; i < 400000; i++) {
                    await Topic.Enqueue(new SomeObject {
                        Count = i,
                        MyMessage = "hello worldhello worldhello worldhello worldhello worldhello world"
                    }).ConfigureAwait(false);
                }
            });
            var task2 = Task.Run(async () => {
                for (var i = 0; i < 400000; i++) {
                    var value = await ReceiveQueue.Reader.ReadAsync().ConfigureAwait(false);
                    switch (value) {
                        case SomeObject so: {
                            if (so.Count != i) throw new Exception();
                            break;
                        }
                        default: throw new NotSupportedException();
                    }
                }
            });
            await task1.ConfigureAwait(false);
            await task2.ConfigureAwait(false);
            sw.Stop();
            Console.WriteLine((int)sw.Elapsed.TotalMilliseconds);
            Console.ReadLine();
        }

        public class SomeObject {
            public int Count;
            public string MyMessage;
        }

        public class SomeObjectEncoder : EncoderBase<SomeObject> {

            public override async ValueTask<SomeObject> Decode(PipeReader reader) {
                if (!await reader.ReadBool().ConfigureAwait(false)) return null;
                var value = new SomeObject();
                value.Count = await reader.ReadInt().ConfigureAwait(false);
                value.MyMessage = await reader.ReadString().ConfigureAwait(false);
                return value;
            }

            public override void Encode(PipeWriter writer, SomeObject value) {
                if (value is null) {
                    writer.WriteBool(false);
                } else {
                    writer.WriteBool(true);
                    writer.WriteInt(value.Count);
                    writer.WriteString(value.MyMessage);
                }
            }
        }

        [DebuggerStepThrough]
        static bool LoggerFilter(string providerName, string loggerName, LogLevel logLevel) {
            return true;
        }

    }
}
