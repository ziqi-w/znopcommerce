namespace WS.Plugin.Payments.Latipay.Services.Interfaces;

/// <summary>
/// Generates and validates documented Latipay signatures.
/// </summary>
public interface ILatipaySignatureService
{
    string CreateRequestSignature(IDictionary<string, string> values, string secret);

    string CreateSortedParameterSignature(IDictionary<string, string> values, string secret, bool urlEncodeValues = false);

    string CreateHostedResponseSignature(string nonce, string hostUrl, string secret);

    string CreateStatusSignature(string merchantReference, string paymentMethod, string status, string currency, string amount, string secret);

    bool IsHostedResponseSignatureValid(string nonce, string hostUrl, string signature, string secret);

    bool IsStatusSignatureValid(string merchantReference, string paymentMethod, string status, string currency, string amount, string signature, string secret);

    bool IsSortedParameterSignatureValid(IDictionary<string, string> values, string signature, string secret, bool urlEncodeValues = false);

    bool AreEqual(string left, string right);
}
