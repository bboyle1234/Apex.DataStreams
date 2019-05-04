using Apex.DataStreams.AdminMessages;
using Apex.DataStreams.Encoding;
using Apex.ValueCompression;
using Apex.ValueCompression.Compressors;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Apex.DataStreams {

    public static class ServiceProviderExtensions {

        class DataStreamMarker { }

        static bool HasDataStreams(this IServiceCollection services) {
            return services.Any(s => s.ServiceType == typeof(DataStreamMarker));
        }

        public static IServiceCollection AddDataStreams(this IServiceCollection services) {
            if (services.HasDataStreams()) return services;
            services.AddSingleton<DataStreamMarker>();
            services.AddSingleton<IEncoder, Encoder>();
            services.AddSingleton<ICompressorFactory, ServiceProviderCompressorFactory>();
            services.AddSingleton<ICompressor<HeartBeat>, HeartBeatCompressor>();
            services.AddSingleton<IDecompressor<HeartBeat>, HeartBeatCompressor>();
            return services;
        }

        public static IServiceCollection AddCompressorsFrom(this IServiceCollection services, Assembly assembly) {
            foreach (var x in assembly.GetCompressorTypeData()) {
                services.AddSingleton(x.ServiceType, x.ImplementationType);
            }
            foreach (var x in assembly.GetDecompressorTypeData()) {
                services.AddSingleton(x.ServiceType, x.ImplementationType);
            }
            return services;
        }

        public static IDataStreamClient CreateIDataStreamClient(this IServiceProvider serviceProvider, IEnumerable<ClientContext> contexts)
            => new Client(serviceProvider, contexts);

        public static IDataStreamClient CreateIDataStreamClient(this IServiceProvider serviceProvider, params ClientContext[] contexts)
            => serviceProvider.CreateIDataStreamClient(contexts as IEnumerable<ClientContext>);

        public static IDataStreamPublisher CreateIDataStreamPublisher(this IServiceProvider serviceProvider, PublisherConfiguration configuration, Func<IDataStreamPublisher, Exception, Task> errorCallback)
            => new Publisher(serviceProvider, configuration, errorCallback);
    }
}
