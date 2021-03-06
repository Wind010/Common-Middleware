using Microsoft.AspNetCore.Builder;

using System;
using System.Diagnostics.CodeAnalysis;

namespace Common.Middleware.Extensions
{
    using Middleware;

    using Serilog;

    /// <summary>
    /// Middleware which handles global exceptions.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class ExceptionMiddlewareExtensions
    {
        public const string AddingExceptionMiddlewareMiddleware = "Adding exception middleware.";

        public static void ConfigureExceptionHandlingMiddleware(this IApplicationBuilder app
            , ILogger logger)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            logger.Information(AddingExceptionMiddlewareMiddleware);

            app.UseMiddleware<ExceptionMiddleware>(logger);
        }

    }
}
