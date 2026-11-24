namespace ERSoftSMSBox.Integrations;

/// <summary>
/// Supports the legacy PHP endpoint and the AjansWebSMS ASMX send_sms endpoint.
/// </summary>
public sealed record EyfelSoftSettings(bool IsActive = false, string ApiUrl = "", string Sender = "",
    string Username = "", string Password = "", string Vendor = "")
{
    public bool IsAsmx => Uri.TryCreate(ApiUrl, UriKind.Absolute, out Uri? uri)
        && (uri.AbsolutePath.EndsWith("/send_sms", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.EndsWith(".asmx", StringComparison.OrdinalIgnoreCase));

    internal void Validate()
    {
        if (!Uri.TryCreate(ApiUrl, UriKind.Absolute, out Uri? uri)
            || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length > 0 || uri.Fragment.Length > 0)
            throw new ArgumentException(IntegrationText.Get("EyfelSoftUrlRequired"));
        if (IsAsmx && (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password)
            || string.IsNullOrWhiteSpace(Vendor) || string.IsNullOrWhiteSpace(Sender)))
            throw new ArgumentException(IntegrationText.Get("EyfelSoftCredentialsRequired"));
    }

    public override string ToString() => $"EyfelSoftSettings {{ IsActive = {IsActive} }}";
}
