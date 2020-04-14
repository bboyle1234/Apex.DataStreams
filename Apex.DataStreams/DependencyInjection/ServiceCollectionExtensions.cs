using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Microsoft.Extensions.DependencyInjection {

    public static class ServiceCollectionExtensions {

        public static IServiceCollection AddDataStreams(this IServiceCollection services) {
            return services
                .AddPipeEncoders(Assembly.GetExecutingAssembly())
                .AddPipeDecoders(Assembly.GetExecutingAssembly());
        }
    }
}
