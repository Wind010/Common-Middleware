using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.Serialization;

namespace Common.Middleware
{
    /// <summary>
    /// Abstract class for custom exceptions to be derived from.
    /// </summary>
    [Serializable]
    [ExcludeFromCodeCoverage]
    public abstract class BaseException : Exception
    {
        /// <summary>
        /// The status code to return to the client
        /// </summary>
        public HttpStatusCode StatusCode { get; set; }

        public string ErrorCode { get; set; }

        public string ResourceReferenceProperty { get; set; }

        public BaseException()
        {
        }

        public BaseException(string message)
            : this(message, HttpStatusCode.InternalServerError, string.Empty, null)
        {
            StatusCode = HttpStatusCode.InternalServerError;
        }

        public BaseException(string message, Exception inner)
            : this(message, HttpStatusCode.InternalServerError, string.Empty, inner)
        {
        }

        public BaseException(string message, HttpStatusCode statusCode)
            : this(message, statusCode, string.Empty, null)
        {
        }

        public BaseException(string message, string errorCode)
            : this(message, HttpStatusCode.InternalServerError, errorCode, null)
        {
        }

        public BaseException(string message, HttpStatusCode statusCode, string errorCode)
            : this(message, statusCode, errorCode, null)
        {
        }

        public BaseException(string message, HttpStatusCode statusCode, string errorCode, Exception inner)
            : base(message, inner)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }

        protected BaseException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ResourceReferenceProperty = info.GetString(nameof(ResourceReferenceProperty));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }
            info.AddValue(nameof(ResourceReferenceProperty), ResourceReferenceProperty);
            base.GetObjectData(info, context);
        }
    }
}

