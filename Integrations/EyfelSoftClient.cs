using System.Web;
using System.Xml;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ERSoftSMSBox.Messaging;

namespace ERSoftSMSBox.Integrations;

/// <summary>Supports PHP SMS and AjansWebSMS send_sms without logging authenticated URLs.</summary>
public sealed class EyfelSoftClient(HttpClient httpClient)
{
    static EyfelSoftClient() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    private readonly HttpClient httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<SmsProviderAccount> CheckAsync(EyfelSoftSettings settings,
        CancellationToken cancellationToken = default)
    {
        ValidateActive(settings);
        Uri uri = BuildCheckUri(settings);
        string body = await RequestAsync(uri, cancellationToken).ConfigureAwait(false);
        if (settings.IsAsmx)
        {
            XDocument metadata = ParseXml(body);
            if (metadata.Root?.Name.LocalName != "definitions"
                || !metadata.Descendants().Any(node => node.Name.LocalName == "operation"
                    && (string?)node.Attribute("name") == "send_sms"))
                throw new InvalidDataException(IntegrationText.Get("EyfelSoftInvalidResponse"));
        }
        else if (!body.Contains("GSM", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(IntegrationText.Get("EyfelSoftInvalidResponse"));
        }
        // Reachability does not prove authentication, balance or delivery.
        return new SmsProviderAccount(0, 0, settings.Sender.Length > 0 ? [settings.Sender] : [], false);
    }

    public async Task<string> SendAsync(EyfelSoftSettings settings, string gsm, string message,
        CancellationToken cancellationToken = default)
    {
        Uri uri = BuildSendUri(settings, gsm, message);
        string body = await RequestAsync(uri, cancellationToken).ConfigureAwait(false);
        string result = body.TrimStart().StartsWith('<') ? ReadXmlResult(body) : body.Trim();
        if (result.StartsWith('"'))
        {
            try { result = JsonSerializer.Deserialize<string>(result) ?? ""; }
            catch (JsonException) { throw new InvalidDataException(IntegrationText.Get("EyfelSoftInvalidResponse")); }
        }
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidDataException(IntegrationText.Get("EyfelSoftInvalidResponse"));
        if (result.StartsWith("Hata", StringComparison.OrdinalIgnoreCase)
            || result.StartsWith("Error", StringComparison.OrdinalIgnoreCase)
            || result.StartsWith("ERR", StringComparison.OrdinalIgnoreCase)
            || result.Contains("Hata :", StringComparison.OrdinalIgnoreCase)
            || result.Contains("Hata:", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(IntegrationText.Format("EyfelSoftApiError", Redact(result, settings)));
        // The raw acknowledgement is not a delivery receipt.
        return Redact(result, settings);
    }

    internal static Uri BuildSendUri(EyfelSoftSettings settings, string gsm, string message)
    {
        ValidateActive(settings);
        string phone = SmsDraftService.NormalizePhone(gsm)
            ?? throw new ArgumentException(IntegrationText.Get("EyfelSoftInvalidPhone"));
        if (string.IsNullOrWhiteSpace(message) || message.Length > 1000)
            throw new ArgumentException(IntegrationText.Get("SmsTemplateInvalid"));

        UriBuilder builder = Endpoint(settings);
        var query = HttpUtility.ParseQueryString(builder.Query);
        RemoveSendFields(query);
        query["gsm"] = phone;
        if (settings.IsAsmx)
        {
            query["username"] = settings.Username;
            query["password"] = settings.Password;
            query["vendor"] = settings.Vendor;
            query["header"] = settings.Sender;
            query["message"] = message.Trim();
            query["charset"] = "turkish";
            query["startdate"] = "";
            query["exdate"] = "";
        }
        else
        {
            query["mesaj"] = message.Trim();
            if (!string.IsNullOrWhiteSpace(settings.Sender)) query["baslik"] = settings.Sender;
        }
        builder.Query = EncodeQuery(query);
        return builder.Uri;
    }

    internal static Uri BuildCheckUri(EyfelSoftSettings settings)
    {
        UriBuilder builder = Endpoint(settings);
        if (settings.IsAsmx)
        {
            builder.Path = builder.Path[..^"/send_sms".Length];
            builder.Query = "WSDL";
        }
        else
        {
            var query = HttpUtility.ParseQueryString(builder.Query);
            RemoveSendFields(query);
            builder.Query = EncodeQuery(query);
        }
        return builder.Uri;
    }

    private static UriBuilder Endpoint(EyfelSoftSettings settings)
    {
        settings.Validate();
        UriBuilder builder = new(settings.ApiUrl);
        if (settings.IsAsmx && builder.Path.EndsWith(".asmx", StringComparison.OrdinalIgnoreCase))
            builder.Path += "/send_sms";
        return builder;
    }

    private static void RemoveSendFields(System.Collections.Specialized.NameValueCollection query)
    {
        foreach (string field in new[] { "gsm", "mesaj", "baslik", "message", "header",
            "charset", "startdate", "exdate" }) query.Remove(field);
    }

    private static string EncodeQuery(System.Collections.Specialized.NameValueCollection query)
        => string.Join("&", query.AllKeys.Where(key => key is not null)
            .Select(key => Uri.EscapeDataString(key!) + "=" + Uri.EscapeDataString(query[key!] ?? "")));

    private static void ValidateActive(EyfelSoftSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsActive) throw new InvalidOperationException(IntegrationText.Get("IntegrationDisabledError"));
        settings.Validate();
    }

    private async Task<string> RequestAsync(Uri uri, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(uri, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(IntegrationText.Format("EyfelSoftHttpError", (int)response.StatusCode),
                    null, response.StatusCode);
            return await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new TimeoutException(IntegrationText.Get("IntegrationTimeout")); }
        catch (HttpRequestException exception) when (exception.StatusCode is null)
        {
            throw new HttpRequestException(IntegrationText.Format("EyfelSoftHostError", uri.Host));
        }
    }

    private static XDocument ParseXml(string body)
    {
        try
        {
            using XmlReader reader = XmlReader.Create(new StringReader(body),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            return XDocument.Load(reader);
        }
        catch (XmlException) { throw new InvalidDataException(IntegrationText.Get("EyfelSoftInvalidResponse")); }
    }

    private static string ReadXmlResult(string body)
    {
        XDocument document = ParseXml(body);
        XElement? result = document.Root?.Name.LocalName == "string" ? document.Root
            : document.Descendants().FirstOrDefault(node => node.Name.LocalName == "send_smsResult");
        return result?.Value.Trim() ?? throw new InvalidDataException(IntegrationText.Get("EyfelSoftInvalidResponse"));
    }

    private static string Redact(string text, EyfelSoftSettings settings)
    {
        foreach (string value in new[] { settings.Password, settings.Username })
        {
            if (value.Length > 0)
                text = text.Replace(value, "***", StringComparison.Ordinal)
                    .Replace(Uri.EscapeDataString(value), "***", StringComparison.OrdinalIgnoreCase);
        }
        return text.Length > 500 ? text[..500] : text;
    }
}
