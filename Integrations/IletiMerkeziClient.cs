using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace ERSoftSMSBox.Integrations;

/// <summary>
/// Identifies an application-level error returned by İleti Merkezi.
/// </summary>
public sealed class IletiMerkeziApiException : InvalidOperationException
{
    /// <summary>
    /// Creates an error without retaining a response body or account credentials.
    /// </summary>
    public IletiMerkeziApiException(int code)
        : base(code == 401
            ? IntegrationText.Get("IletiMerkeziAuthError")
            : IntegrationText.Format("IntegrationApiError", IntegrationText.Get("IletiMerkezi"), code))
    {
        Code = code;
    }

    /// <summary>
    /// Gets the provider's status code.
    /// </summary>
    public int Code { get; }
}

/// <summary>
/// Reads account information through the documented İleti Merkezi JSON API.
/// The caller owns the supplied HTTP client.
/// </summary>
public sealed class IletiMerkeziClient
{
    private static readonly Uri BaseAddress = new("https://api.iletimerkezi.com/v1/");
    private readonly HttpClient httpClient;

    /// <summary>
    /// Creates a client using the supplied HTTP transport.
    /// </summary>
    public IletiMerkeziClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        this.httpClient = httpClient;
    }

    /// <summary>
    /// Reads balance and approved sender IDs without sending SMS or consuming credits.
    /// Disabled configurations are rejected before any HTTP request is sent.
    /// </summary>
    public async Task<SmsProviderAccount> GetAccountAsync(IletiMerkeziSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsActive)
        {
            throw new InvalidOperationException(IntegrationText.Get("IntegrationDisabledError"));
        }
        if (string.IsNullOrWhiteSpace(settings.Key) || string.IsNullOrWhiteSpace(settings.Hash))
        {
            throw new ArgumentException(IntegrationText.Format("IntegrationCredentialsRequired",
                IntegrationText.Get("IletiMerkeziIdentifier"), IntegrationText.Get("IletiMerkeziSecret")), nameof(settings));
        }
        cancellationToken.ThrowIfCancellationRequested();

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using JsonDocument balanceDocument = await PostAsync("get-balance/json", settings, timeout.Token).ConfigureAwait(false);
            JsonElement balance = GetProperty(GetProperty(balanceDocument.RootElement, "response"), "balance");
            decimal amount = ReadDecimal(GetProperty(balance, "amount"));
            long sms = ReadLong(GetProperty(balance, "sms"));

            using JsonDocument senderDocument = await PostAsync("get-sender/json", settings, timeout.Token).ConfigureAwait(false);
            JsonElement senders = GetProperty(GetProperty(GetProperty(senderDocument.RootElement, "response"), "senders"), "sender");
            if (senders.ValueKind != JsonValueKind.Array)
            {
                throw InvalidResponse();
            }
            List<string> names = [];
            foreach (JsonElement sender in senders.EnumerateArray())
            {
                timeout.Token.ThrowIfCancellationRequested();
                if (sender.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(sender.GetString()))
                {
                    throw InvalidResponse();
                }
                names.Add(sender.GetString() ?? throw InvalidResponse());
            }

            return new SmsProviderAccount(amount, sms, names.AsReadOnly());
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

    private async Task<JsonDocument> PostAsync(string endpoint, IletiMerkeziSettings settings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(BaseAddress, endpoint))
        {
            Content = JsonContent.Create(new
            {
                request = new { authentication = new { key = settings.Key, hash = settings.Hash } }
            })
        };
        using HttpResponseMessage response = await httpClient.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(IntegrationText.Format("IntegrationHttpError", (int)response.StatusCode),
                null, response.StatusCode);
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        JsonDocument document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions { MaxDepth = 32 },
            cancellationToken).ConfigureAwait(false);
        try
        {
            long code = ReadLong(GetProperty(GetProperty(GetProperty(document.RootElement, "response"), "status"), "code"));
            if (code != 200)
            {
                throw new IletiMerkeziApiException(checked((int)code));
            }

            return document;
        }
        catch (InvalidDataException)
        {
            document.Dispose();
            throw;
        }
        catch (IletiMerkeziApiException)
        {
            document.Dispose();
            throw;
        }
        catch (OverflowException)
        {
            document.Dispose();
            throw InvalidResponse();
        }
    }

    private static JsonElement GetProperty(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value)
            ? value : throw InvalidResponse();

    private static decimal ReadDecimal(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out decimal number))
        {
            return number;
        }
        if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(),
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number))
        {
            return number;
        }

        throw InvalidResponse();
    }

    private static long ReadLong(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number))
        {
            return number;
        }
        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(),
            NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
        {
            return number;
        }

        throw InvalidResponse();
    }

    private static InvalidDataException InvalidResponse()
        => new(IntegrationText.Format("IntegrationInvalidResponse", IntegrationText.Get("IletiMerkezi")));
}
