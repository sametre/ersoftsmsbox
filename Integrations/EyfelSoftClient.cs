using System.Web;

namespace ERSoftSMSBox.Integrations;

/// <summary>
/// Eyfelsoft SMS PHP API ile iletişim kurar.
/// URL yapısı: http://host/api2/sms/developer/{hesap}.send.php?gsm=...&amp;mesaj=...&amp;baslik=...
/// Yanıt düz metin olarak gelir; başarı durumunda mesaj ID'si, hata durumunda "Hata : ..." döner.
/// </summary>
public sealed class EyfelSoftClient
{
    private readonly HttpClient httpClient;

    public EyfelSoftClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        this.httpClient = httpClient;
    }

    /// <summary>
    /// API erişilebilirliğini doğrular: GSM parametresi göndermeden çağırır,
    /// "GSM no eksik" yanıtı geri dönüyorsa API çalışıyor demektir.
    /// </summary>
    public async Task<SmsProviderAccount> CheckAsync(EyfelSoftSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsActive)
            throw new InvalidOperationException(IntegrationText.Get("IntegrationDisabledError"));
        if (string.IsNullOrWhiteSpace(settings.ApiUrl))
            throw new ArgumentException(IntegrationText.Get("EyfelSoftUrlRequired"), nameof(settings));

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using HttpResponseMessage response = await httpClient
                .GetAsync(settings.ApiUrl, timeout.Token)
                .ConfigureAwait(false);

            string body = (await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false)).Trim();

            // "Hata : GSM no eksik." → URL doğru, API erişilebilir
            if (body.Contains("GSM", StringComparison.OrdinalIgnoreCase))
            {
                string sender = string.IsNullOrWhiteSpace(settings.Sender) ? "" : settings.Sender;
                return new SmsProviderAccount(0, 0, sender.Length > 0 ? [sender] : []);
            }

            throw new InvalidDataException(IntegrationText.Format("EyfelSoftApiError", body.Length > 120 ? body[..120] : body));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(IntegrationText.Get("IntegrationTimeout"));
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                IntegrationText.Format("EyfelSoftApiError", ex.Message), ex);
        }
    }

    /// <summary>
    /// Tek bir GSM numarasına SMS gönderir ve API'nin döndürdüğü yanıtı döner.
    /// </summary>
    public async Task<string> SendAsync(EyfelSoftSettings settings, string gsm, string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(settings.ApiUrl))
            throw new ArgumentException(IntegrationText.Get("EyfelSoftUrlRequired"), nameof(settings));

        string url = BuildUrl(settings.ApiUrl, gsm, message, settings.Sender);

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using HttpResponseMessage response = await httpClient
                .GetAsync(url, timeout.Token)
                .ConfigureAwait(false);

            return (await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false)).Trim();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(IntegrationText.Get("IntegrationTimeout"));
        }
    }

    private static string BuildUrl(string baseUrl, string gsm, string message, string sender)
    {
        string encodedGsm = HttpUtility.UrlEncode(gsm);
        string encodedMsg = HttpUtility.UrlEncode(message);
        string sep = baseUrl.Contains('?') ? "&" : "?";
        string url = $"{baseUrl}{sep}gsm={encodedGsm}&mesaj={encodedMsg}";
        if (!string.IsNullOrWhiteSpace(sender))
        {
            url += $"&baslik={HttpUtility.UrlEncode(sender)}";
        }
        return url;
    }
}
