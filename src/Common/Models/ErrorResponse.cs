using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Common.Middleware.Models
{
    /// <summary>
    /// General error response.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ErrorResponse
    {
        public ErrorResponse() { }

        public ErrorResponse(string errorCode, string errorMessage)
        {
            Code = errorCode;
            Message = errorMessage;
        }

        /// <summary>
        /// Standard OAPI error code
        /// </summary>
        [JsonPropertyName("code")]
        public string Code { get; set; }

        /// <summary>
        /// Standard OAPI error message
        /// </summary>
        [JsonPropertyName("message")]
        [StringLength(2048)]
        public string Message { get; set; }
    }
}
