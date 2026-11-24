using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ERSoftSMSBox.Integrations;

/// <summary>
/// Identifies an application-level error returned by the NetGSM v3 API.
/// </summary>
public sealed class NetGsmApiException : InvalidOperationException
{
    /// <summary>
    /// Creates an error from the HTTP status and the machine-readable <c>code</c> field, without retaining the body.
    /// </summary>
    public NetGsmApiException(HttpStatusCode statusCode, string code)
        : base(statusCode == HttpStatusCode.Unauthorized
            ? IntegrationText.Get("NetGsmAuthError")
            : IntegrationText.Format("NetGsmApiError", (int)statusCode, code))
    {
        StatusCode = statusCode;
        Code = code;
    }

    /// <summary>
    /// Gets the HTTP status returned by NetGSM.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Gets the provider's error code such as <c>invalid_credentials</c> or <c>UNAUTHENTICATED</c>.
    /// </summary>
    public string Code { get; }
}

/// <summary>
/// Reads account information through the documented NetGSM v3 REST API
/// (token from the Authorization API, then balance and sender IDs from the SMS API).
/// The caller owns the supplied HTTP client.
/// </summary>
public sealed class NetGsmClient
{
    private const string CreditBalanceName = "Kredi Bakiye";
    private const string SmsBalanceName = "Adet SMS";
    private static readonly Uri TokenAddress = new("https://ext-api.netgsm.com.tr/auth/v3/token");
    private static readonly Uri SmsBaseAddress = new("https://ext-api.netgsm.com.tr/sms/v3/");
    private static readonly JsonDocumentOptions DocumentOptions = new() { MaxDepth = 32 };
    private readonly HttpClient httpClient;

    /// <summary>
    /// Creates a client using the supplied HTTP transport.
    /// </summary>
    public NetGsmClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        this.httpClient = httpClient;
    }

    /// <summary>
    /// Reads credit balance, remaining SMS count and usable sender IDs without sending SMS.
    /// Disabled configurations are rejected before any HTTP request is sent.
    /// </summary>
    public async Task<SmsProviderAccount> GetAccountAsync(NetGsmSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsActive)
        {
            throw new InvalidOperationException(IntegrationText.Get("IntegrationDisabledError"));
        }
        if (string.IsNullOrWhiteSpace(settings.UserName) || string.IsNullOrWhiteSpace(settings.Password))
        {
            throw new ArgumentException(IntegrationText.Format("IntegrationCredentialsRequired",
                IntegrationText.Get("NetGsmIdentifier"), IntegrationText.Get("NetGsmSecret")), nameof(settings));
        }
        cancellationToken.ThrowIfCancellationRequested();

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            string token = await GetAccessTokenAsync(settings, timeout.Token).ConfigureAwait(false);

            using JsonDocument balanceDocument = await GetAsync("balance?stip=3", token, timeout.Token).ConfigureAwait(false);
            (decimal credit, long sms) = ReadBalance(balanceDocument.RootElement, timeout.Token);

            using JsonDocument senderDocument = await GetAsync("senders", token, timeout.Token).ConfigureAwait(false);
            IReadOnlyList<string> senders = ReadSenders(senderDocument.RootElement, timeout.Token);

            return new SmsProviderAccount(credit, sms, senders);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(IntegrationText.Get("IntegrationTimeout"));
        }
        catch (JsonException)
        {
            throw InvalidResponse();
        }
    }

    private async Task<string> GetAccessTokenAsync(NetGsmSettings settings, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, TokenAddress)
        {
            Content = JsonContent.Create(new { username = settings.UserName, password = settings.Password, scope = "sms" })
        };
        using JsonDocument document = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        JsonElement token = GetProperty(document.RootElement, "access_token");
        if (token.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(token.GetString()))
        {
            throw InvalidResponse();
        }

        return token.GetString()!;
    }

    private async Task<JsonDocument> GetAsync(string endpoint, string token, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(SmsBaseAddress, endpoint));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using HttpResponseMessage response = await httpClient.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateErrorAsync(response, cancellationToken).ConfigureAwait(false);
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        return await JsonDocument.ParseAsync(stream, DocumentOptions, cancellationToken).ConfigureAwait(false);
    }

    // NetGSM reports application errors as JSON with a machine-readable "code"; fall back to the HTTP status otherwise.
    private static async Task<Exception> CreateErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string code = "";
        try
        {
            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, DocumentOptions, cancellationToken).ConfigureAwait(false);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("code", out JsonElement value)
                && value.ValueKind == JsonValueKind.String)
            {
                code = value.GetString() ?? "";
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }

        if (code.Length > 0 || response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new NetGsmApiException(response.StatusCode, code);
        }

        return new HttpRequestException(IntegrationText.Format("IntegrationHttpError", (int)response.StatusCode),
            null, response.StatusCode);
    }

    private static (decimal Credit, long Sms) ReadBalance(JsonElement root, CancellationToken cancellationToken)
    {
        JsonElement entries = GetProperty(root, "balance");
        if (entries.ValueKind != JsonValueKind.Array)
        {
            throw InvalidResponse();
        }

        decimal credit = 0;
        long sms = 0;
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonElement name = GetProperty(entry, "balance_name");
            if (name.ValueKind != JsonValueKind.String)
            {
                throw InvalidResponse();
            }
            switch (name.GetString())
            {
                case CreditBalanceName:
                    credit = ReadAmount(GetProperty(entry, "amount"));
                    break;
                case SmsBalanceName:
                    sms = (long)Math.Truncate(ReadAmount(GetProperty(entry, "amount")));
                    break;
            }
        }

        return (credit, sms);
    }

    private static IReadOnlyList<string> ReadSenders(JsonElement root, CancellationToken cancellationToken)
    {
        JsonElement entries = GetProperty(root, "senders");
        if (entries.ValueKind != JsonValueKind.Array)
        {
            throw InvalidResponse();
        }

        List<string> names = [];
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonElement name = GetProperty(entry, "name");
            JsonElement usable = GetProperty(entry, "usable");
            if (usable.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || name.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
            {
                throw InvalidResponse();
            }
            if (usable.ValueKind == JsonValueKind.True && !string.IsNullOrWhiteSpace(name.GetString()))
            {
                names.Add(name.GetString()!);
            }
        }

        return names.AsReadOnly();
    }

    // Amounts arrive as numbers ("277"), plain strings ("767") or Turkish-formatted credit ("92,150" = 92.15 TL).
    private static decimal ReadAmount(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out decimal number))
        {
            return number;
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            string text = (value.GetString() ?? "").Trim();
            if (text.Contains(',') && !text.Contains('.'))
            {
                text = text.Replace(',', '.');
            }
            if (decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out number))
            {
                return number;
            }
        }

        throw InvalidResponse();
    }

    private static JsonElement GetProperty(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value)
            ? value : throw InvalidResponse();

    private static InvalidDataException InvalidResponse()
        => new(IntegrationText.Format("IntegrationInvalidResponse", IntegrationText.Get("NetGsm")));
}
