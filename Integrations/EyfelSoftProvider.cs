namespace ERSoftSMSBox.Integrations;

/// <summary>
/// Eyfelsoft SMS PHP API'sini paylaşılan SmsProvider sözleşmesine bağlar.
/// </summary>
public sealed class EyfelSoftProvider : SmsProvider
{
    private readonly IntegrationSettingsStore<EyfelSoftSettings> store =
        new("eyfelsoft.dat", () => new EyfelSoftSettings());

    public override string Key => "EyfelSoft";

    public override async Task<SmsProviderDraft> LoadAsync(CancellationToken cancellationToken)
    {
        EyfelSoftSettings settings = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        return new SmsProviderDraft(settings.IsActive, settings.ApiUrl, "", settings.Sender);
    }

    public override Task SaveAsync(SmsProviderDraft draft, CancellationToken cancellationToken)
        => store.SaveAsync(ToSettings(draft), cancellationToken);

    public override Task<SmsProviderAccount> GetAccountAsync(HttpClient httpClient, SmsProviderDraft draft,
        CancellationToken cancellationToken)
        => new EyfelSoftClient(httpClient).CheckAsync(ToSettings(draft), cancellationToken);

    internal Task<SmsProviderAccount> CheckAsync(HttpClient httpClient, CancellationToken cancellationToken)
        => store.LoadAsync(cancellationToken)
               .ContinueWith(t => new EyfelSoftClient(httpClient).CheckAsync(t.Result, cancellationToken),
                   cancellationToken, System.Threading.Tasks.TaskContinuationOptions.OnlyOnRanToCompletion,
                   System.Threading.Tasks.TaskScheduler.Default)
               .Unwrap();

    private static EyfelSoftSettings ToSettings(SmsProviderDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return new EyfelSoftSettings(draft.IsActive, draft.Identifier, draft.Sender);
    }
}
