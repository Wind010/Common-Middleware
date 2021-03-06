using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;

namespace Common.Middleware.Extensions
{
    using Destructurama;

    using Serilog;
    using Serilog.Events;

    public static class SerilogExtension
    {
        /// <summary>
        /// Adds required services to register serilog as our logging provider
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        public static ILogger AddSerilog(this IServiceCollection services, IConfiguration configuration)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            Log.Logger = CreateLogConfiguration(configuration).CreateLogger();

            AppDomain.CurrentDomain.ProcessExit += (s, e) => Log.CloseAndFlush();

            // This must be a singleton due to logging to file.
            services.AddSingleton(Log.Logger);

            return Log.Logger;
        }

        /// <summary>
        /// This is used in UnitTests to verify that destructuring is set correctly.
        /// This is IMPORTANT for not logging and masking of sensitive data.
        /// </summary>
        internal static LoggerConfiguration CreateLogConfiguration(IConfiguration configuration)
        {
            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Destructure.ByTransforming<WebException>(response => new
                {
                    response.Status,
                    response.Message,
                    response.StackTrace,
                    response.InnerException
                })
                .Destructure.ByTransforming<Exception>(response => new
                {
                    response.Message,
                    response.StackTrace,
                    response.InnerException
                })
                // CorrelationId Enrichment can be added here.
                .Destructure.UsingAttributes() // Very important that we have this line to mask/remove properties that should not be logged.
                .Enrich.FromLogContext()
                .Enrich.WithProperty("MachineName", Environment.MachineName)
                .ReadFrom.Configuration(configuration);

            return loggerConfiguration;
        }




    }
}
