namespace ERSoftSMSBox.Integrations;

/// <summary>
/// Adapts the İleti Merkezi client and settings to the shared provider contract.
/// </summary>
public sealed class IletiMerkeziProvider : SmsProvider
{
    private readonly IntegrationSettingsStore<IletiMerkeziSettings> store = new("iletimerkezi.dat", () => new IletiMerkeziSettings());

    /// <inheritdoc />
    public override string Key => "IletiMerkezi";

    /// <inheritdoc />
    public override async Task<SmsProviderDraft> LoadAsync(CancellationToken cancellationToken)
    {
        IletiMerkeziSettings settings = await store.LoadAsync(cancellationToken).ConfigureAwait(false);

        return new SmsProviderDraft(settings.IsActive, settings.Key, settings.Hash, settings.Sender);
    }

    /// <inheritdoc />
    public override Task SaveAsync(SmsProviderDraft draft, CancellationToken cancellationToken)
        => store.SaveAsync(ToSettings(draft), cancellationToken);

    /// <inheritdoc />
    public override Task<SmsProviderAccount> GetAccountAsync(HttpClient httpClient, SmsProviderDraft draft,
        CancellationToken cancellationToken)
        => new IletiMerkeziClient(httpClient).GetAccountAsync(ToSettings(draft), cancellationToken);

    private static IletiMerkeziSettings ToSettings(SmsProviderDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return new IletiMerkeziSettings(draft.IsActive, draft.Identifier, draft.Secret, draft.Sender);
    }
}
