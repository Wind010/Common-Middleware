using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;


//[assembly: InternalsVisibleTo("Common.Middleware.Unit")]
namespace Common.Middleware
{
    using Serilog;
    using Serilog.Context;
    using Serilog.Events;

    /// <summary>
    /// This class is only expected to specific values of the request upon error.
    /// Logging of request and response should be done on the controller level to allow
    /// for proper destructuring as to avoid sensitive information being logged.
    /// </summary>
    public class RequestResponseLoggingMiddleware
    {
        // Option to whitelist headers, can be added to a config.
        internal const string Authorization = "Authorization";
        internal const string ContentType = "Content-Type";
        internal const string ContentLength = "Content-Length";
        internal const string UserAgent = "User-Agent";
        internal const string CorrelationId = "X-Correlation-ID";
        internal const string AllowAll = "*";

        // Default to allow all.
        internal HashSet<string> HeaderWhitelist = new HashSet<string> { AllowAll };

        private readonly ILogger _logger;
        private readonly RequestDelegate _next;

        public const string ExceptionInRequestResponseMiddleware = "Exception in RequestResponseLoggingMiddleware.";
        public const string ResponseErrorMessageTemplate = "HTTP '{requestMethod}' '{requestPath}' responded with '{statusCode}' in '{elapsedMs}' ms.";
        public const string RequestMetadataMessageTemplate = "HTTP Method '{requestMethod}' to '{requestPath}' in '{elapsedMs}' ms.";
        public const string ResponseMetadataMessageTemplate = "HTTP StatusCode '{responseStatusCode}' to '{requestPath}' with {headers}in '{elapsedMs}' ms.";
        public const string RequestBodyMessageTemplate = "API request content: {@request}";
        public const string ResponseBodyMessageTemplate = "API response content: {@response}";
        public const string ExceptionWhenLoggingResponse = "Exception when logging response";

        /// <summary>
        /// Logs the incoming request and outgoing response.
        /// </summary>
        /// <param name="next"><see cref="RequestDelegate"/></param>
        /// <param name="logger"><see cref="IWalletLogger"/></param>
        public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }


        public async Task InvokeAsync(HttpContext httpContext)
        {
            var start = Stopwatch.GetTimestamp();

            if (httpContext == null) { return; }

            Stream originalRequestBody = httpContext.Request?.Body;
            Stream originalResponseBody = httpContext.Response?.Body;

            using var responseBody = new MemoryStream();
            try
            {
                if (originalRequestBody == null || originalResponseBody == null)
                {
                    return;
                }

                double elapsedMs = GetElapsedMilliseconds(start, Stopwatch.GetTimestamp());

                // The actual logging of request and responses should be done 
                // in controller if destructuring of objects is needed.
                httpContext.Request.EnableBuffering();
                await LogRequestAsync(httpContext, elapsedMs)
                    .ConfigureAwait(false);

                httpContext.Response.Body = responseBody;

                // End of incoming request pipeline
                await _next(httpContext);
                // Start outgoing response pipeline

                await LogResponseAsync(httpContext, start);
            }
            catch (Exception ex)
            {
                // Exception middleware to handle unexpected exception previously.
                // An exception here would be internal to logging.

                double elapsedMs = GetElapsedMilliseconds(start, Stopwatch.GetTimestamp());
                string requestMethod = httpContext.Request.Method;
                int? statusCode = httpContext.Response.StatusCode;

                _logger.Error(ExceptionInRequestResponseMiddleware, ex);

                _logger.Error(ResponseErrorMessageTemplate, requestMethod, GetPath(httpContext)
                    , statusCode, elapsedMs);
            }
            finally
            {
                if (originalResponseBody != null)
                {
                    await responseBody.CopyToAsync(originalResponseBody).ConfigureAwait(false);
                    httpContext.Response.Body = originalResponseBody;
                }
            }
        }

        internal async Task LogResponseAsync(HttpContext httpContext, long startTime)
        {
            if (httpContext.Response.HasStarted || !httpContext.Response.Body.CanWrite)
            {
                return;
            }

            var level = GetLogLevelFromStatusCode(httpContext.Response.StatusCode);
            double elapsedMs = GetElapsedMilliseconds(startTime, Stopwatch.GetTimestamp());

            // Check here in case number of requests create too much noise.
            if (level > LogEventLevel.Information)
            {
                await LogRequestAsync(httpContext, elapsedMs).ConfigureAwait(false);
            }

            _logger.Write(level, ResponseBodyMessageTemplate
                , await FormatResponseAsync(httpContext.Response).ConfigureAwait(false));

            LogResponseHeaders(httpContext, elapsedMs);
        }

        internal IHeaderDictionary GetAllowedHeaders(IHeaderDictionary headers)
        {
            if (headers == null) { return new HeaderDictionary(); }

            if (HeaderWhitelist.Contains(AllowAll))
            {
                return headers;
            }

            if (_logger.IsEnabled(LogEventLevel.Debug) || _logger.IsEnabled(LogEventLevel.Verbose))
            {
                return headers;
            }

            var allowedHeaders = new HeaderDictionary();
            foreach (var v in headers)
            {
                if (HeaderWhitelist.Contains(v.Key, StringComparer.OrdinalIgnoreCase))
                {
                    allowedHeaders.Add(v.Key, v.Value);
                };
            }

            // Sanitize any paths or header values.

            return allowedHeaders;
        }

        internal string GetPath(HttpContext httpContext)
        {
            string rawTarget = httpContext.Features.Get<IHttpRequestFeature>()?.RawTarget;

            if (string.IsNullOrWhiteSpace(rawTarget))
            {
                rawTarget = httpContext.Request.Path.ToString();
            }

            return rawTarget;
        }

        private async Task LogRequestAsync(HttpContext httpContext, double elapsedMs)
        {
            HttpRequest request = httpContext.Request;

            var allowedHeaders = GetAllowedHeaders(request.Headers);

            // Add others as needed.
            using (LogContext.PushProperty("RequestHeaders", allowedHeaders, destructureObjects: true))
            using (LogContext.PushProperty("RequestProtocol", request.Protocol))
            using (LogContext.PushProperty("RequestScheme", request.Scheme))
            using (LogContext.PushProperty("RequestHost", request.Host))
            using (LogContext.PushProperty("RequestQueryString", request.QueryString))
            {
                var level = GetLogLevelFromStatusCode(httpContext.Response.StatusCode);
                _logger.Write(level, RequestMetadataMessageTemplate, request.Method, GetPath(httpContext)
                    , elapsedMs);
                await LogRequestBody(request, level).ConfigureAwait(false);
            }
        }

        private void LogResponseHeaders(HttpContext httpContext, double elapsedMs)
        {
            HttpResponse response = httpContext.Response;
            var allowedHeaders = GetAllowedHeaders(response.Headers);
            using (LogContext.PushProperty("ResponseHeaders", allowedHeaders))
            {
                LogResponseMessageTemplate(response.StatusCode, GetPath(httpContext), elapsedMs);
            }
        }

        private double GetElapsedMilliseconds(long start, long stop)
        {
            return (stop - start) * 1000 / (double)Stopwatch.Frequency;
        }

        private async Task LogRequestBody(HttpRequest request, LogEventLevel level)
        {
            if (request.Body == null)
            {
                return;
            }

            // Note that a HttpGet can technically contain a body.
            request.Body.Position = 0;
            using (var sr = new StreamReader(request.Body, leaveOpen: true))
            {
                string requestBody = await sr.ReadToEndAsync();
                _logger.Write(level, RequestBodyMessageTemplate, requestBody);
                request.Body.Position = 0;
            }
        }

        private async Task<string> FormatResponseAsync(HttpResponse response)
        {
            if (!response.Body.CanSeek)
            {
                return string.Empty;
            }

            response.Body.Seek(0, SeekOrigin.Begin);
            var text = await new StreamReader(response.Body).ReadToEndAsync()
                .ConfigureAwait(false);
            response.Body.Seek(0, SeekOrigin.Begin);

            return text;
        }

        private void LogResponseMessageTemplate(int statusCode, string path
            , double elapsedMs)
        {
            var level = GetLogLevelFromStatusCode(statusCode);
            _logger.Write(level, ResponseMetadataMessageTemplate, statusCode, path
                , elapsedMs);
        }

        private LogEventLevel GetLogLevelFromStatusCode(int statusCode)
        {
            return (statusCode > 399 && statusCode < 599) ? LogEventLevel.Error
                : LogEventLevel.Information;
        }

    }
}