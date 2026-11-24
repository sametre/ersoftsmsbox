namespace ERSoftSMSBox.Integrations;

/// <summary>
/// Holds the account configuration supplied by the İleti Merkezi panel (API key and hash).
/// </summary>
public sealed record IletiMerkeziSettings(bool IsActive = false, string Key = "", string Hash = "", string Sender = "")
{
    /// <summary>
    /// Returns diagnostic information without exposing account credentials.
    /// </summary>
    public override string ToString() => $"IletiMerkeziSettings {{ IsActive = {IsActive} }}";
}
