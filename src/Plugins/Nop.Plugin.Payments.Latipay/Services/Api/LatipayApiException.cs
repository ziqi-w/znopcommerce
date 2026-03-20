using System.Net;

namespace Nop.Plugin.Payments.Latipay.Services.Api;

/// <summary>
/// Represents a Latipay integration failure.
/// </summary>
public class LatipayApiException : Exception
{
    public LatipayApiException(string message,
        LatipayApiFailureKind failureKind,
        bool isTransient = false,
        string providerCode = null,
        HttpStatusCode? httpStatusCode = null,
        Exception innerException = null)
        : base(message, innerException)
    {
        FailureKind = failureKind;
        IsTransient = isTransient;
        ProviderCode = providerCode;
        HttpStatusCode = httpStatusCode;
    }

    public LatipayApiFailureKind FailureKind { get; }

    public bool IsTransient { get; }

    public string ProviderCode { get; }

    public HttpStatusCode? HttpStatusCode { get; }
}
