namespace ERSoftSMSBox.Integrations;

/// <summary>
/// Read-only account summary shared by every SMS provider: TL balance, remaining SMS count and approved sender IDs.
/// </summary>
public sealed record SmsProviderAccount(decimal Balance, long Sms, IReadOnlyList<string> Senders,
    bool CredentialsVerified = true);

/// <summary>
/// Provider-neutral values edited on the integration screen. Each provider maps these onto its own settings record.
/// </summary>
public sealed record SmsProviderDraft(bool IsActive = false, string Identifier = "", string Secret = "", string Sender = "",
    string Username = "", string Vendor = "")
{
    /// <summary>
    /// Returns diagnostic information without exposing account credentials.
    /// </summary>
    public override string ToString() => $"SmsProviderDraft {{ IsActive = {IsActive} }}";
}

/// <summary>
/// Describes one SMS provider: its resource texts, encrypted settings storage and read-only connection check.
/// Resource keys follow the <c>{Key}{Suffix}</c> convention, e.g. <c>NetGsmDescription</c>.
/// </summary>
public abstract class SmsProvider
{
    /// <summary>
    /// Gets every supported provider in menu order. Add new providers here and to the ribbon icon map.
    /// </summary>
    public static IReadOnlyList<SmsProvider> All { get; } = [new IletiMerkeziProvider(), new NetGsmProvider(), new EyfelSoftProvider()];

    /// <summary>
    /// Gets the provider shown when the integration screen opens without an explicit choice.
    /// </summary>
    public static SmsProvider Default => All[0];

    /// <summary>
    /// Finds a provider by its key, e.g. <c>NetGsm</c>.
    /// </summary>
    public static SmsProvider Find(string key)
        => All.FirstOrDefault(provider => string.Equals(provider.Key, key, StringComparison.Ordinal))
            ?? throw new ArgumentException($"Unknown SMS provider '{key}'.", nameof(key));

    /// <summary>
    /// Gets the stable identifier used for resource keys, ribbon buttons and the settings file name.
    /// </summary>
    public abstract string Key { get; }

    /// <summary>
    /// Gets the localized display name.
    /// </summary>
    public string Name => IntegrationText.Get(Key);

    /// <summary>
    /// Builds a provider-specific resource key such as <c>NetGsmAuthError</c>.
    /// </summary>
    public string TextKey(string suffix) => Key + suffix;

    /// <summary>
    /// Reads the saved configuration; returns an inactive draft when nothing has been saved yet.
    /// </summary>
    public abstract Task<SmsProviderDraft> LoadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Persists the configuration encrypted for the current Windows user.
    /// </summary>
    public abstract Task SaveAsync(SmsProviderDraft draft, CancellationToken cancellationToken);

    public virtual void ValidateDraft(SmsProviderDraft draft)
    {
        if (draft.IsActive && (string.IsNullOrWhiteSpace(draft.Identifier) || string.IsNullOrWhiteSpace(draft.Secret)))
            throw new ArgumentException(IntegrationText.Format("IntegrationCredentialsRequired",
                IntegrationText.Get(TextKey("Identifier")), IntegrationText.Get(TextKey("Secret"))));
    }

    public bool CanCheckAccount(SmsProviderDraft draft)
    {
        if (!draft.IsActive) return false;
        try { ValidateDraft(draft); return true; }
        catch (ArgumentException) { return false; }
    }

    /// <summary>
    /// Verifies the credentials by reading balance and sender IDs; never sends SMS.
    /// </summary>
    public abstract Task<SmsProviderAccount> GetAccountAsync(HttpClient httpClient, SmsProviderDraft draft,
        CancellationToken cancellationToken);

    /// <inheritdoc />
    public override string ToString() => Key;
}
