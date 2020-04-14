using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using System;
using System.Threading.Tasks;

namespace Apex.DataStreams.Benchmarking {
    class Program {
        static async Task Main(string[] args) {
            //var summary = BenchmarkRunner.Run<SpeedTest>(new DebugBuildConfig());

            var test = new SpeedTest();
            await test.Setup().ConfigureAwait(false);
            await test.Run().ConfigureAwait(false);
        }
    }
}
