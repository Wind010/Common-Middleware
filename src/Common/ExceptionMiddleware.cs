using Microsoft.AspNetCore.Http;

using System;
using System.Net;
using System.Net.Mime;
using System.Threading.Tasks;
using System.Text.Json;

namespace Common.Middleware
{
    using Models;

    using Serilog;

    /// <summary>
    /// Used for handling any exceptions thrown by API.
    /// </summary>
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public const string ExceptionOccurredInExceptionMiddleware 
            = "Exception occured in ExceptionMiddleware {exception}";

        public const string ExceptionDidNotInheritFromBaseException 
            = "Exception did not inherit from BaseException";

        public const int StatusCodeNotProvided = 0;

        public ExceptionMiddleware(RequestDelegate next, ILogger logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(httpContext, ex, _logger);
            }
        }

        public async Task HandleExceptionAsync(HttpContext context, Exception exception, ILogger logger)
        {
            logger.Error(exception, ExceptionOccurredInExceptionMiddleware);

            if (context.Response.HasStarted || !context.Response.Body.CanWrite)
            {
                // Response already written earlier in the pipeline

                // Just to get rid of warning for now:
                await Task.CompletedTask;

                return;
            }

            var errorResponse = GetErrorResponse(exception);
            SetHttpStatusCodeFromError(context, exception);

            if (!context.Response.HasStarted)
            {
                context.Response.ContentType = MediaTypeNames.Application.Json;
                await JsonSerializer.SerializeAsync(context.Response.Body, errorResponse);
            }
        }

        internal ErrorResponse GetErrorResponse(Exception exception)
        {
            var errorResponse = new ErrorResponse(((int)HttpStatusCode.InternalServerError).ToString()
                , HttpStatusCode.InternalServerError.ToString());

            // All exceptions used should derive from BaseException.
            // If needed, handle all other exceptions here.
            if (exception is BaseException baseException)
            {
                int? errorCode = null;
                if (int.TryParse(baseException.ErrorCode, out int code) && code != 0)
                {
                    errorCode = code;
                }

                return new ErrorResponse(errorCode.ToString(), exception.Message);
            }
            else
            {
                errorResponse.Message = exception.Message;
                _logger.Warning(ExceptionDidNotInheritFromBaseException, exception);
            }

            return errorResponse;
        }

        private void SetHttpStatusCodeFromError(HttpContext context, Exception exception)
        {
            var statusCode = HttpStatusCode.InternalServerError;

            if (exception is BaseException baseException
                && baseException.StatusCode != StatusCodeNotProvided)
            {
                statusCode = baseException.StatusCode;
            }

            context.Response.StatusCode = (int)statusCode;
        }

    }

}
