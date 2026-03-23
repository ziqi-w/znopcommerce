namespace WS.Plugin.Payments.Latipay.Services.Api;

/// <summary>
/// Represents Latipay integration failure categories.
/// </summary>
public enum LatipayApiFailureKind
{
    Unknown = 0,
    Configuration = 10,
    RequestValidation = 20,
    Timeout = 30,
    Transport = 40,
    HttpStatus = 50,
    Provider = 60,
    ResponseValidation = 70,
    SignatureValidation = 80
}
