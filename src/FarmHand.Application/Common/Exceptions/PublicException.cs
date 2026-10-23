namespace FarmHand.Application.Common.Exceptions;

/// <summary>
/// An exception whose message is safe to return to API clients.
/// Use this for expected, user-actionable failures instead of relying on
/// framework exception messages, which may leak implementation details.
/// </summary>
public class PublicException : Exception
{
    public int StatusCode { get; }

    /// <summary>Optional machine-readable code clients can branch on (e.g. "password_change_required").</summary>
    public string? Code { get; }

    public PublicException(int statusCode, string message, string? code = null) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public static PublicException BadRequest(string message) => new(400, message);

    public static PublicException Forbidden(string message) => new(403, message);

    public static PublicException NotFound(string message) => new(404, message);

    public static PublicException Conflict(string message) => new(409, message);

    public static PublicException Upstream(string message) => new(502, message);
    public static PublicException Unavailable(string message, string? code = null) => new(503, message, code);
}
