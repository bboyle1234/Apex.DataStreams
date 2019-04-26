using Apex.DataStreams.Encoding;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams {

    public static class ServiceProviderExtensions {

        public static IServiceCollection AddDataStreams(this IServiceCollection services) {
            if (services.HasDataStreams()) return services;
            services.AddSingleton<ISerializer, DefaultSerializer>();
            services.AddSingleton<IEncoder, DefaultEncoder>();
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

        static bool HasDataStreams(this IServiceCollection services) {
            return services.Any(sd => sd.ServiceType == typeof(IEncoder));
        }


        public static IDataStreamClient CreateIDataStreamClient(this IServiceProvider serviceProvider, IEnumerable<ClientContext> contexts)
            => new Client(serviceProvider, contexts);

        public static IDataStreamClient CreateIDataStreamClient(this IServiceProvider serviceProvider, params ClientContext[] contexts)
            => serviceProvider.CreateIDataStreamClient(contexts as IEnumerable<ClientContext>);

        public static IDataStreamPublisher CreateIDataStreamPublisher(this IServiceProvider serviceProvider, PublisherConfiguration configuration, Func<IDataStreamPublisher, Exception, Task> errorCallback)
            => new Publisher(serviceProvider, configuration, errorCallback);
    }
}
