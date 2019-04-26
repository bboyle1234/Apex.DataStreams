using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Debug;
using Microsoft.Extensions.Logging.Configuration;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using Apex.DataStreams.Definitions;
using Nito.AsyncEx;
using Apex.DataStreams.Encoding;
using Apex.DataStreams.Topics;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Apex.DataStreams.Tests {

    [TestClass]
    public class Connectivity {

        static IServiceProvider ServiceProvider;
        static DataStreamDefinition DataStreamDefinition;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context) {
            ServiceProvider = new ServiceCollection()
                .AddDataStreams()
                .AddLogging(builder => {
                    builder.AddDebug();
                    builder.AddConsole(options => {
                        options.IncludeScopes = true;
                    });
                    builder.AddFilter(LogFilter);
                })
                .BuildServiceProvider();

            DataStreamDefinition = new DataStreamDefinitionBuilder("Connectivity Tests") {
                { "Topic1", 1 },
                { "Topic1", 0, typeof(MyMessageClass), 256 },
            };
        }

        [DebuggerStepThrough]
        static bool LogFilter(string loggerName, LogLevel level) {
            return true;
        }

        [ClassCleanup]
        public static void ClassCleanup() {
        }

        [TestMethod]
        public async Task Test1() {
            var clientMessages = new AsyncProducerConsumerQueue<MessageEnvelope>();
            var messageCounter = new Dictionary<string, int>();
            var @event = new AsyncAutoResetEvent();
            _ = Task.Run(ReceiveWorker);

            var publisher = ServiceProvider.CreateIDataStreamPublisher(new PublisherConfiguration {
                ListenPort = 6116,
                DataStreamDefinition = DataStreamDefinition,
            }, OnPublisherError);
            publisher.Start();

            var client1 = ServiceProvider.CreateIDataStreamClient(new ClientContext {
                DataStreamDefinition = DataStreamDefinition,
                PublisherEndPoint = "localhost:6116",
                ReceiveQueue = clientMessages,
            });
            client1.Start();

            var t1 = await publisher.CreateTopicAsync(DataStreamDefinition.GetTopicByName("Topic1"), new SimpleTopicSummary()).ConfigureAwait(false);
            var sendOperations = (await t1.EnqueueAsync(new MyMessageClass { MessageString = "hello world" }).ConfigureAwait(false)).ToList();

            await @event.WaitAsync().ConfigureAwait(false);

            lock (messageCounter) {
                Assert.AreEqual(messageCounter["hello world"], 1);
            }

            client1.Dispose();
            publisher.Dispose();


            async Task OnPublisherError(IDataStreamPublisher p, Exception x) {
                await Task.Yield();
            }

            async Task ReceiveWorker() {
                while (true) {
                    var envelope = await clientMessages.DequeueAsync().ConfigureAwait(false);
                    var message = (envelope.Message as MyMessageClass).MessageString;
                    lock (messageCounter) {
                        try { messageCounter[message]++; } catch { messageCounter[message] = 1; }
                    }
                    @event.Set();
                }
            }
        }

        class MyMessageClass {
            public string MessageString;
            public int MessageInt;
        }
    }
}
