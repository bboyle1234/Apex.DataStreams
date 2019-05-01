using Apex.DataStreams.AdminMessages;
using Apex.DataStreams.Encoding;
using Apex.ValueCompression;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
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
            services.AddSingleton<ICompressor<HeartBeat>, HeartBeatCompressor>();
            services.AddSingleton<IDecompressor<HeartBeat>, HeartBeatCompressor>();
            return services;
        }

        public static IServiceCollection UseDataStreamsSerializer<TSerializer>(this IServiceCollection services) where TSerializer : class, ISerializer {
            if (!services.HasDataStreams()) throw new Exception($"You must '{nameof(AddDataStreams)}' before adding custom service implementations for DataStreams.");
            services.AddSingleton<ISerializer, TSerializer>();
            return services;
        }

        public static IServiceCollection UseDataStreamsSerializer(this IServiceCollection services, Func<IServiceProvider, ISerializer> factory) {
            if (!services.HasDataStreams()) throw new Exception($"You must '{nameof(AddDataStreams)}' before adding custom service implementations for DataStreams.");
            services.AddSingleton<ISerializer>(factory);
            return services;
        }

        public static IServiceCollection UseDataStreamsEncoder<TEncoder>(this IServiceCollection services) where TEncoder : class, IEncoder {
            if (!services.HasDataStreams()) throw new Exception($"You must '{nameof(AddDataStreams)}' before adding custom service implementations for DataStreams.");
            services.AddSingleton<IEncoder, TEncoder>();
            return services;
        }

        public static IServiceCollection UseDataStreamsEncoder(this IServiceCollection services, Func<IServiceProvider, IEncoder> factory) {
            if (!services.HasDataStreams()) throw new Exception($"You must '{nameof(AddDataStreams)}' before adding custom service implementations for DataStreams.");
            services.AddSingleton<IEncoder>(factory);
            return services;
        }

        public static IServiceCollection UseCompressorsIn(this IServiceCollection services, Assembly assembly) {
            foreach (var type in assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract)) {

                var compressorInterfaceType = type.GetInterfaces()
                    .Where(i => i.IsConstructedGenericType)
                    .Where(i => i.GetGenericTypeDefinition() == typeof(ICompressor<>))
                    .FirstOrDefault();
                if (null != compressorInterfaceType)
                    services.AddSingleton(compressorInterfaceType, type);

                var decompressorInterfaceType = type.GetInterfaces()
                    .Where(i => i.IsConstructedGenericType)
                    .Where(i => i.GetGenericTypeDefinition() == typeof(IDecompressor<>))
                    .FirstOrDefault();
                if (null != decompressorInterfaceType)
                    services.AddSingleton(decompressorInterfaceType, type);
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
