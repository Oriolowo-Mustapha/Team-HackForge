namespace FarmHand.Application.Common.Exceptions;

/// <summary>An external integration (storage, payments, email) failed. Mapped to 503 with a safe message.</summary>
public class ExternalServiceException : Exception
{
    public string ServiceName { get; }

    public ExternalServiceException(string serviceName, string publicMessage, Exception? inner = null) : base(publicMessage, inner)
    {
        ServiceName = serviceName;
    }
}
