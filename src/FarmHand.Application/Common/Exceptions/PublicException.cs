namespace FarmHand.Application.Common.Exceptions;

/// <summary>
/// An exception whose message is safe to return to API clients.
/// Use this for expected, user-actionable failures instead of relying on
/// framework exception messages, which may leak implementation details.
/// </summary>
public class PublicException : Exception
{
    public int StatusCode { get; }

    public PublicException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public static PublicException BadRequest(string message) => new(400, message);

    public static PublicException Forbidden(string message) => new(403, message);

    public static PublicException NotFound(string message) => new(404, message);

    public static PublicException Conflict(string message) => new(409, message);

    public static PublicException Upstream(string message) => new(502, message);
}
