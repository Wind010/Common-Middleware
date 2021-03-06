using Microsoft.AspNetCore.Builder;

using System;

namespace Common.Middleware.Extensions
{
    using Middleware;

    using Serilog;

    public static class RequestResponseLoggingMiddlewareExtension
    {
        public const string AddingRequestResponseLoggingMiddleware =
            "Adding request and response logging middleware.";

        /// <summary>
        /// This should be added in Startup.cs first.  
        /// Next should be the exception logging middleware.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="logger"></param>
        public static void ConfigureRequestResponseLoggingMiddleware(this IApplicationBuilder app
            , ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            logger.Information(AddingRequestResponseLoggingMiddleware);

            app.UseMiddleware<RequestResponseLoggingMiddleware>(logger);
        }
    }
}