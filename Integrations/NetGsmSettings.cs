namespace ERSoftSMSBox.Integrations;

/// <summary>
/// Holds the account configuration supplied by the NetGSM portal (API sub-user name and password).
/// </summary>
public sealed record NetGsmSettings(bool IsActive = false, string UserName = "", string Password = "", string Sender = "")
{
    /// <summary>
    /// Returns diagnostic information without exposing account credentials.
    /// </summary>
    public override string ToString() => $"NetGsmSettings {{ IsActive = {IsActive} }}";
}
