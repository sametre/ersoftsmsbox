namespace ERSoftSMSBox.Integrations;

/// <summary>
/// Eyfelsoft SMS PHP API'sini paylaşılan SmsProvider sözleşmesine bağlar.
/// </summary>
public sealed class EyfelSoftProvider : SmsProvider
{
    private readonly IntegrationSettingsStore<EyfelSoftSettings> store;

    public EyfelSoftProvider() : this(null) { }

    internal EyfelSoftProvider(string? storageDirectory)
        => store = new("eyfelsoft.dat", () => new EyfelSoftSettings(), storageDirectory);

    public override string Key => "EyfelSoft";

    public override async Task<SmsProviderDraft> LoadAsync(CancellationToken cancellationToken)
    {
        EyfelSoftSettings settings = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        return new SmsProviderDraft(settings.IsActive, settings.ApiUrl, settings.Password, settings.Sender,
            settings.Username, settings.Vendor);
    }

    public override Task SaveAsync(SmsProviderDraft draft, CancellationToken cancellationToken)
    {
        ValidateDraft(draft);
        return store.SaveAsync(ToSettings(draft), cancellationToken);
    }

    public override void ValidateDraft(SmsProviderDraft draft)
    {
        if (draft.IsActive) ToSettings(draft).Validate();
    }

    public async Task<EyfelSoftSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
        => await store.LoadAsync(cancellationToken).ConfigureAwait(false);

    public Task SaveSettingsAsync(EyfelSoftSettings settings, CancellationToken cancellationToken = default)
    {
        if (settings.IsActive) settings.Validate();
        return store.SaveAsync(settings, cancellationToken);
    }

    public override Task<SmsProviderAccount> GetAccountAsync(HttpClient httpClient, SmsProviderDraft draft,
        CancellationToken cancellationToken)
        => new EyfelSoftClient(httpClient).CheckAsync(ToSettings(draft), cancellationToken);

    internal async Task<SmsProviderAccount> CheckAsync(HttpClient httpClient, CancellationToken cancellationToken)
        => await new EyfelSoftClient(httpClient).CheckAsync(
            await store.LoadAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

    private static EyfelSoftSettings ToSettings(SmsProviderDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return new EyfelSoftSettings(draft.IsActive, draft.Identifier, draft.Sender,
            draft.Username, draft.Secret, draft.Vendor);
    }
}
