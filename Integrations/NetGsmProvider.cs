namespace ERSoftSMSBox.Integrations;

/// <summary>
/// Adapts the NetGSM client and settings to the shared provider contract.
/// </summary>
public sealed class NetGsmProvider : SmsProvider
{
    private readonly IntegrationSettingsStore<NetGsmSettings> store = new("netgsm.dat", () => new NetGsmSettings());

    /// <inheritdoc />
    public override string Key => "NetGsm";

    /// <inheritdoc />
    public override async Task<SmsProviderDraft> LoadAsync(CancellationToken cancellationToken)
    {
        NetGsmSettings settings = await store.LoadAsync(cancellationToken).ConfigureAwait(false);

        return new SmsProviderDraft(settings.IsActive, settings.UserName, settings.Password, settings.Sender);
    }

    /// <inheritdoc />
    public override Task SaveAsync(SmsProviderDraft draft, CancellationToken cancellationToken)
        => store.SaveAsync(ToSettings(draft), cancellationToken);

    /// <inheritdoc />
    public override Task<SmsProviderAccount> GetAccountAsync(HttpClient httpClient, SmsProviderDraft draft,
        CancellationToken cancellationToken)
        => new NetGsmClient(httpClient).GetAccountAsync(ToSettings(draft), cancellationToken);

    private static NetGsmSettings ToSettings(SmsProviderDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return new NetGsmSettings(draft.IsActive, draft.Identifier, draft.Secret, draft.Sender);
    }
}
