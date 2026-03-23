namespace WS.Plugin.Payments.Latipay.Services.Api.Requests;

/// <summary>
/// Represents the server-side billing and contact fields required by Latipay hosted card payments.
/// </summary>
public class CardPayerDetails
{
    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string Address { get; set; }

    public string CountryCode { get; set; }

    public string State { get; set; }

    public string City { get; set; }

    public string Postcode { get; set; }

    public string Email { get; set; }

    public string Phone { get; set; }
}
