using System.Net;
using System.Text;
using System.Text.Json;
using ERSoftSMSBox.Integrations;
using NUnit.Framework;

namespace ERSoftSMSBox.Tests;

[TestFixture]
public class NetGsmClientTests
{
    private const string TokenResponse = """{"access_token":"jwt-token","token_type":"Bearer","expires_in":86400,"refresh_token":"r","scope":"sms","user_id":1,"sub_user_id":0}""";
    private const string BalanceResponse = """{"balance":[{"amount":277,"balance_name":"Adet SMS"},{"amount":"767","balance_name":"Adet OTP SMS"},{"amount":"92,150","balance_name":"Kredi Bakiye"}]}""";
    private const string SendersResponse = """{"count":2,"senders":[{"name":"ERSOFT","type":"ALPHANUMERIC","brandCode":1,"usable":true,"reason":null},{"name":"5XXXXXXXXX","type":"SUBSCRIBER_NUMBER","brandCode":0,"usable":false,"reason":"SUBSCRIBER_NUMBER_RESTRICTED"}]}""";
    private readonly NetGsmSettings activeSettings = new(true, "test-user", "test-password");

    [Test]
    public void WhenConfigurationIsNewThenIntegrationIsInactive()
    {
        Assert.That(new NetGsmSettings().IsActive, Is.False);
    }

    [Test]
    public void WhenSettingsAreFormattedThenCredentialsAreRedacted()
    {
        Assert.That(activeSettings.ToString(), Does.Not.Contain("test-user").And.Not.Contain("test-password"));
    }

    [Test]
    public void WhenIntegrationIsInactiveThenNoNetworkRequestIsMade()
    {
        using HttpClient http = new(new UnexpectedRequestHandler());
        NetGsmClient client = new(http);

        Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAccountAsync(activeSettings with { IsActive = false }));
    }

    [TestCase("", "password")]
    [TestCase("user", "")]
    [TestCase(" ", "password")]
    public void WhenCredentialsAreMissingThenNoNetworkRequestIsMade(string userName, string password)
    {
        using HttpClient http = new(new UnexpectedRequestHandler());
        NetGsmClient client = new(http);

        Assert.ThrowsAsync<ArgumentException>(() => client.GetAccountAsync(new NetGsmSettings(true, userName, password)));
    }

    [Test]
    public async Task WhenAccountIsRequestedThenTokenBalanceAndSendersAreReadInOrder()
    {
        using RecordingHandler handler = new(TokenResponse, BalanceResponse, SendersResponse);
        using HttpClient http = new(handler);
        NetGsmClient client = new(http);

        await client.GetAccountAsync(activeSettings);

        Assert.That(handler.Requests.Select(request => (request.Method, request.Uri)), Is.EqualTo(new[]
        {
            ("POST", "https://ext-api.netgsm.com.tr/auth/v3/token"),
            ("GET", "https://ext-api.netgsm.com.tr/sms/v3/balance?stip=3"),
            ("GET", "https://ext-api.netgsm.com.tr/sms/v3/senders")
        }));
    }

    [Test]
    public async Task WhenTokenIsRequestedThenCredentialsAndSmsScopeAreInJsonBody()
    {
        using RecordingHandler handler = new(TokenResponse, BalanceResponse, SendersResponse);
        using HttpClient http = new(handler);
        NetGsmClient client = new(http);

        await client.GetAccountAsync(activeSettings);
        using JsonDocument document = JsonDocument.Parse(handler.Requests[0].Body);
        JsonElement root = document.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests[0].ContentType, Is.EqualTo("application/json"));
            Assert.That(root.GetProperty("username").GetString(), Is.EqualTo("test-user"));
            Assert.That(root.GetProperty("password").GetString(), Is.EqualTo("test-password"));
            Assert.That(root.GetProperty("scope").GetString(), Is.EqualTo("sms"));
        });
    }

    [Test]
    public async Task WhenSmsEndpointsAreCalledThenBearerTokenIsSent()
    {
        using RecordingHandler handler = new(TokenResponse, BalanceResponse, SendersResponse);
        using HttpClient http = new(handler);
        NetGsmClient client = new(http);

        await client.GetAccountAsync(activeSettings);

        Assert.That(handler.Requests.Skip(1).Select(request => request.Authorization), Is.All.EqualTo("Bearer jwt-token"));
    }

    [Test]
    public async Task WhenBalanceIsReturnedThenCreditAndSmsCountAreParsed()
    {
        using RecordingHandler handler = new(TokenResponse, BalanceResponse, SendersResponse);
        using HttpClient http = new(handler);
        NetGsmClient client = new(http);

        SmsProviderAccount account = await client.GetAccountAsync(activeSettings);

        Assert.That((account.Balance, account.Sms), Is.EqualTo((92.150m, 277L)));
    }

    [TestCase("""{"balance":[]}""", 0, 0)]
    [TestCase("""{"balance":[{"amount":"242.5","balance_name":"Adet SMS"}]}""", 0, 242)]
    [TestCase("""{"balance":[{"amount":12.5,"balance_name":"Kredi Bakiye"}]}""", 12.5, 0)]
    public async Task WhenBalanceEntriesVaryThenMissingValuesDefaultToZero(string json, decimal credit, long sms)
    {
        using RecordingHandler handler = new(TokenResponse, json, SendersResponse);
        using HttpClient http = new(handler);
        NetGsmClient client = new(http);

        SmsProviderAccount account = await client.GetAccountAsync(activeSettings);

        Assert.That((account.Balance, account.Sms), Is.EqualTo((credit, sms)));
    }

    [Test]
    public async Task WhenSendersAreReturnedThenOnlyUsableNamesAreAvailable()
    {
        using RecordingHandler handler = new(TokenResponse, BalanceResponse, SendersResponse);
        using HttpClient http = new(handler);
        NetGsmClient client = new(http);

        SmsProviderAccount account = await client.GetAccountAsync(activeSettings);

        Assert.That(account.Senders, Is.EqualTo(new[] { "ERSOFT" }));
    }

    [Test]
    public async Task WhenNoSendersAreRegisteredThenEmptyListIsAccepted()
    {
        using RecordingHandler handler = new(TokenResponse, BalanceResponse, """{"count":0,"senders":[]}""");
        using HttpClient http = new(handler);
        NetGsmClient client = new(http);

        SmsProviderAccount account = await client.GetAccountAsync(activeSettings);

        Assert.That(account.Senders, Is.Empty);
    }

    [TestCase("not-json")]
    [TestCase("{}")]
    [TestCase("""{"access_token":""}""")]
    [TestCase("""{"access_token":123}""")]
    public void WhenTokenResponseIsMalformedThenSafeErrorIsReturned(string json)
    {
        using RecordingHandler handler = new(json);
        using HttpClient http = new(handler);
        NetGsmClient client = new(http);

        Assert.ThrowsAsync<InvalidDataException>(() => client.GetAccountAsync(activeSettings));
    }

    [TestCase("{}")]
    [TestCase("""{"balance":"92,150"}""")]
    [TestCase("""{"balance":[{"amount":"abc","balance_name":"Kredi Bakiye"}]}""")]
    [TestCase("""{"balance":[{"amount":1}]}""")]
    public void WhenBalanceResponseIsMalformedThenSafeErrorIsReturned(string json)
    {
        using RecordingHandler handler = new(TokenResponse, json);
        using HttpClient http = new(handler);
        NetGsmClient client = new(http);

        Assert.ThrowsAsync<InvalidDataException>(() => client.GetAccountAsync(activeSettings));
    }

    [TestCase("""{"count":1,"senders":null}""")]
    [TestCase("""{"count":1,"senders":[{"name":"ERSOFT"}]}""")]
    [TestCase("""{"count":1,"senders":[{"name":123,"usable":true}]}""")]
    public void WhenSenderResponseIsMalformedThenSafeErrorIsReturned(string json)
    {
        using RecordingHandler handler = new(TokenResponse, BalanceResponse, json);
        using HttpClient http = new(handler);
        NetGsmClient client = new(http);

        Assert.ThrowsAsync<InvalidDataException>(() => client.GetAccountAsync(activeSettings));
    }

    [Test]
    public void WhenCredentialsAreRejectedThenProviderErrorHidesSecrets()
    {
        using RecordingHandler handler = new("""{"code":"invalid_credentials","detail":"test-user test-password"}""")
        {
            StatusCode = HttpStatusCode.Unauthorized
        };
        using HttpClient http = new(handler);
        NetGsmClient client = new(http);

        Assert.That(async () => await client.GetAccountAsync(activeSettings),
            Throws.TypeOf<NetGsmApiException>().With.Property("Code").EqualTo("invalid_credentials")
                .And.Property("StatusCode").EqualTo(HttpStatusCode.Unauthorized)
                .And.Message.Not.Contains("test-user").And.Message.Not.Contains("test-password"));
    }

    [Test]
    public void WhenApiAccessIsDeniedThenErrorCodeIsReported()
    {
        using RecordingHandler handler = new("""{"code":"ip_not_allowed"}""") { StatusCode = HttpStatusCode.Forbidden };
        using HttpClient http = new(handler);
        NetGsmClient client = new(http);

        Assert.That(async () => await client.GetAccountAsync(activeSettings),
            Throws.TypeOf<NetGsmApiException>().With.Property("Code").EqualTo("ip_not_allowed")
                .And.Message.Contains("ip_not_allowed"));
    }

    [Test]
    public void WhenHttpStatusIsAnErrorWithoutCodeThenHttpErrorIsReturned()
    {
        using RecordingHandler handler = new("<html>down</html>") { StatusCode = HttpStatusCode.ServiceUnavailable };
        using HttpClient http = new(handler);
        NetGsmClient client = new(http);

        Assert.That(async () => await client.GetAccountAsync(activeSettings),
            Throws.TypeOf<HttpRequestException>().With.Property("StatusCode").EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    public void WhenOperationIsCancelledThenNoNetworkRequestIsMade()
    {
        using HttpClient http = new(new UnexpectedRequestHandler());
        NetGsmClient client = new(http);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() => client.GetAccountAsync(activeSettings, cancellation.Token));
    }

    [Test]
    public async Task WhenPendingOperationIsCancelledThenRequestIsCancelled()
    {
        using PendingHandler handler = new();
        using HttpClient http = new(handler);
        NetGsmClient client = new(http);
        using CancellationTokenSource cancellation = new();
        Task<SmsProviderAccount> request = client.GetAccountAsync(activeSettings, cancellation.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () => await request.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    private sealed record CapturedRequest(string Uri, string Method, string? ContentType, string Body, string? Authorization);

    private sealed class RecordingHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> responses = new(responses);
        internal List<CapturedRequest> Requests { get; } = [];
        internal HttpStatusCode StatusCode { get; init; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(request.RequestUri!.AbsoluteUri, request.Method.Method,
                request.Content?.Headers.ContentType?.MediaType, body, request.Headers.Authorization?.ToString()));

            return new HttpResponseMessage(StatusCode)
            {
                Content = new StringContent(responses.Dequeue(), Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class UnexpectedRequestHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new AssertionException("An HTTP request must not be sent for this configuration.");
    }

    private sealed class PendingHandler : HttpMessageHandler
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            throw new AssertionException("The request should have been cancelled.");
        }
    }
}
