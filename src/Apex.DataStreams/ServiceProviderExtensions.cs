using Apex.DataStreams.Encoding;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams {

    /// <summary>
    /// Use the methods in this class to setup DataStreams on your service provider.
    /// </summary>
    public static class ServiceProviderExtensions {
		
        /// <summary>
        /// Adds default DataStreams implementation to the give service collection.
        /// This method must be called before calling any of the customization methods below.
        /// </summary>
        public static IServiceCollection AddDataStreams(this IServiceCollection services) {
            if (services.HasDataStreams()) return services;
            services.AddSingleton<ISerializer, DefaultSerializer>();
            services.AddSingleton<IEncoder, DefaultEncoder>();
            services.AddSingleton<IDataStreamClientFactory, DataStreamClientFactory>();
            services.AddSingleton<IDataStreamPublisherFactory, DataStreamPublisherFactory>();
            return services;
        }

        /// <summary>
        /// Replaces the default DataStreams serializer with the given serializer. 
        /// Use this if you need to customize the serialization/deserialization of your particular message types.
        /// You must call <see cref="AddDataStreams(IServiceCollection)"/> first.
        /// </summary>
        public static IServiceCollection UseDataStreamsSerializer<TSerializer>(this IServiceCollection services) where TSerializer : class, ISerializer {
            if (!services.HasDataStreams()) throw new Exception($"You must '{nameof(AddDataStreams)}' before adding custom service implementations for DataStreams.");
            services.AddSingleton<ISerializer, TSerializer>();
            return services;
        }

        /// <summary>
        /// Replaces the default DataStreams serializer with the given serializer. 
        /// Use this if you need to customize the serialization/deserialization of your particular message types.
        /// You must call <see cref="AddDataStreams(IServiceCollection)"/> first.
        /// </summary>
        public static IServiceCollection UseDataStreamsSerializer(this IServiceCollection services, Func<IServiceProvider, ISerializer> factory) {
            if (!services.HasDataStreams()) throw new Exception($"You must '{nameof(AddDataStreams)}' before adding custom service implementations for DataStreams.");
            services.AddSingleton<ISerializer>(factory);
            return services;
        }

        /// <summary>
        /// Replaces the default DataStreams message envelope encoder with the given encoder
        /// Use this if you need to customize the encoding of your particular message packets.
        /// You must call <see cref="AddDataStreams(IServiceCollection)"/> first.
        /// </summary>
        public static IServiceCollection UseDataStreamsEncoder<TEncoder>(this IServiceCollection services) where TEncoder : class, IEncoder {
            if (!services.HasDataStreams()) throw new Exception($"You must '{nameof(AddDataStreams)}' before adding custom service implementations for DataStreams.");
            services.AddSingleton<IEncoder, TEncoder>();
            return services;
        }

        /// <summary>
        /// Replaces the default DataStreams message envelope encoder with the given encoder
        /// Use this if you need to customize the encoding of your particular message packets.
        /// You must call <see cref="AddDataStreams(IServiceCollection)"/> first.
        /// </summary>
        public static IServiceCollection UseDataStreamsEncoder(this IServiceCollection services, Func<IServiceProvider, IEncoder> factory) {
            if (!services.HasDataStreams()) throw new Exception($"You must '{nameof(AddDataStreams)}' before adding custom service implementations for DataStreams.");
            services.AddSingleton<IEncoder>(factory);
            return services;
        }

        static bool HasDataStreams(this IServiceCollection services) {
            return services.Any(sd => sd.ServiceType == typeof(IDataStreamClientFactory));
        }
    }
}
