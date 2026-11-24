using System.Net;
using System.Text;
using System.Text.Json;
using ERSoftSMSBox.Integrations;
using NUnit.Framework;

namespace ERSoftSMSBox.Tests;

[TestFixture]
public class IletiMerkeziClientTests
{
    private const string BalanceResponse = """{"response":{"status":{"code":200},"balance":{"amount":10.584,"sms":67}}}""";
    private const string SendersResponse = """{"response":{"status":{"code":200},"senders":{"sender":["ERSoft"]}}}""";
    private readonly IletiMerkeziSettings activeSettings = new(true, "test-key", "test-hash");

    [Test]
    public void WhenConfigurationIsNewThenIntegrationIsInactive()
    {
        Assert.That(new IletiMerkeziSettings().IsActive, Is.False);
    }

    [Test]
    public void WhenSettingsAreFormattedThenCredentialsAreRedacted()
    {
        Assert.That(activeSettings.ToString(), Does.Not.Contain("test-key").And.Not.Contain("test-hash"));
    }

    [Test]
    public void WhenHttpClientIsNullThenConstructorRejectsIt()
    {
        Assert.Throws<ArgumentNullException>(() => new IletiMerkeziClient(null!));
    }

    [Test]
    public void WhenIntegrationIsInactiveThenNoNetworkRequestIsMade()
    {
        using HttpClient http = new(new UnexpectedRequestHandler());
        IletiMerkeziClient client = new(http);

        Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAccountAsync(activeSettings with { IsActive = false }));
    }

    [TestCase("", "hash")]
    [TestCase("key", "")]
    [TestCase(" ", "hash")]
    [TestCase("key", " ")]
    public void WhenCredentialsAreMissingThenNoNetworkRequestIsMade(string key, string hash)
    {
        using HttpClient http = new(new UnexpectedRequestHandler());
        IletiMerkeziClient client = new(http);

        Assert.ThrowsAsync<ArgumentException>(() => client.GetAccountAsync(new IletiMerkeziSettings(true, key, hash)));
    }

    [Test]
    public void WhenSettingsAreNullThenNoNetworkRequestIsMade()
    {
        using HttpClient http = new(new UnexpectedRequestHandler());
        IletiMerkeziClient client = new(http);

        Assert.ThrowsAsync<ArgumentNullException>(() => client.GetAccountAsync(null!));
    }

    [Test]
    public async Task WhenAccountIsRequestedThenOnlyReadOnlyEndpointsAreCalled()
    {
        using RecordingHandler handler = new(BalanceResponse, SendersResponse);
        using HttpClient http = new(handler);
        IletiMerkeziClient client = new(http);

        await client.GetAccountAsync(activeSettings);

        Assert.That(handler.Requests.Select(request => request.Uri), Is.EqualTo(new[]
        {
            "https://api.iletimerkezi.com/v1/get-balance/json",
            "https://api.iletimerkezi.com/v1/get-sender/json"
        }));
    }

    [Test]
    public async Task WhenAccountIsRequestedThenAuthenticationIsInJsonBody()
    {
        using RecordingHandler handler = new(BalanceResponse, SendersResponse);
        using HttpClient http = new(handler);
        IletiMerkeziClient client = new(http);

        await client.GetAccountAsync(activeSettings);
        using JsonDocument document = JsonDocument.Parse(handler.Requests[0].Body);
        JsonElement authentication = document.RootElement.GetProperty("request").GetProperty("authentication");

        Assert.That(new[] { authentication.GetProperty("key").GetString(), authentication.GetProperty("hash").GetString() },
            Is.EqualTo(new[] { "test-key", "test-hash" }));
    }

    [Test]
    public async Task WhenAccountIsRequestedThenJsonPostIsUsed()
    {
        using RecordingHandler handler = new(BalanceResponse, SendersResponse);
        using HttpClient http = new(handler);
        IletiMerkeziClient client = new(http);

        await client.GetAccountAsync(activeSettings);

        Assert.That(handler.Requests.Select(request => (request.Method, request.ContentType)),
            Is.All.EqualTo(("POST", "application/json")));
    }

    [TestCase(BalanceResponse)]
    [TestCase("""{"response":{"status":{"code":"200"},"balance":{"amount":"10.584","sms":"67"}}}""")]
    public async Task WhenNumericOrLegacyStringBalanceIsReturnedThenValuesAreParsed(string json)
    {
        using RecordingHandler handler = new(json, SendersResponse);
        using HttpClient http = new(handler);
        IletiMerkeziClient client = new(http);

        SmsProviderAccount account = await client.GetAccountAsync(activeSettings);

        Assert.That((account.Balance, account.Sms), Is.EqualTo((10.584m, 67L)));
    }

    [Test]
    public async Task WhenSendersAreReturnedThenApprovedNamesAreAvailable()
    {
        using RecordingHandler handler = new(BalanceResponse, SendersResponse);
        using HttpClient http = new(handler);
        IletiMerkeziClient client = new(http);

        SmsProviderAccount account = await client.GetAccountAsync(activeSettings);

        Assert.That(account.Senders, Is.EqualTo(new[] { "ERSoft" }));
    }

    [Test]
    public async Task WhenNoSendersAreApprovedThenEmptyListIsAccepted()
    {
        using RecordingHandler handler = new(BalanceResponse,
            """{"response":{"status":{"code":200},"senders":{"sender":[]}}}""");
        using HttpClient http = new(handler);
        IletiMerkeziClient client = new(http);

        SmsProviderAccount account = await client.GetAccountAsync(activeSettings);

        Assert.That(account.Senders, Is.Empty);
    }

    [TestCase("not-json")]
    [TestCase("{}")]
    [TestCase("[]")]
    [TestCase("""{"response":{"status":{"code":200}}}""")]
    [TestCase("""{"response":{"status":{"code":200},"balance":{"amount":null,"sms":67}}}""")]
    [TestCase("""{"response":{"status":{"code":200},"balance":{"amount":10.584,"sms":"invalid"}}}""")]
    [TestCase("""{"response":{"status":{"code":999999999999},"balance":{"amount":0,"sms":0}}}""")]
    public void WhenBalanceResponseIsMalformedThenSafeErrorIsReturned(string json)
    {
        using RecordingHandler handler = new(json);
        using HttpClient http = new(handler);
        IletiMerkeziClient client = new(http);

        Assert.ThrowsAsync<InvalidDataException>(() => client.GetAccountAsync(activeSettings));
    }

    [TestCase("""{"response":{"status":{"code":200},"senders":{"sender":null}}}""")]
    [TestCase("""{"response":{"status":{"code":200},"senders":{"sender":[123]}}}""")]
    [TestCase("""{"response":{"status":{"code":200},"senders":{"sender":[""]}}}""")]
    public void WhenSenderResponseIsMalformedThenSafeErrorIsReturned(string json)
    {
        using RecordingHandler handler = new(BalanceResponse, json);
        using HttpClient http = new(handler);
        IletiMerkeziClient client = new(http);

        Assert.ThrowsAsync<InvalidDataException>(() => client.GetAccountAsync(activeSettings));
    }

    [Test]
    public void WhenApiRejectsAuthenticationThenProviderErrorIsReturned()
    {
        using RecordingHandler handler = new("""{"response":{"status":{"code":401,"message":"test-key test-hash"}}}""");
        using HttpClient http = new(handler);
        IletiMerkeziClient client = new(http);

        Assert.That(async () => await client.GetAccountAsync(activeSettings),
            Throws.TypeOf<IletiMerkeziApiException>().With.Property("Code").EqualTo(401)
                .And.Message.Not.Contains("test-key").And.Message.Not.Contains("test-hash"));
    }

    [Test]
    public void WhenHttpStatusIsAnErrorThenHttpErrorIsReturned()
    {
        using RecordingHandler handler = new(BalanceResponse) { StatusCode = HttpStatusCode.ServiceUnavailable };
        using HttpClient http = new(handler);
        IletiMerkeziClient client = new(http);

        Assert.That(async () => await client.GetAccountAsync(activeSettings),
            Throws.TypeOf<HttpRequestException>().With.Property("StatusCode").EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    public void WhenOperationIsCancelledThenNoNetworkRequestIsMade()
    {
        using HttpClient http = new(new UnexpectedRequestHandler());
        IletiMerkeziClient client = new(http);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() => client.GetAccountAsync(activeSettings, cancellation.Token));
    }

    [Test]
    public async Task WhenPendingOperationIsCancelledThenRequestIsCancelled()
    {
        using PendingHandler handler = new();
        using HttpClient http = new(handler);
        IletiMerkeziClient client = new(http);
        using CancellationTokenSource cancellation = new();
        Task<SmsProviderAccount> request = client.GetAccountAsync(activeSettings, cancellation.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () => await request.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    private sealed record CapturedRequest(string Uri, string Method, string ContentType, string Body);

    private sealed class RecordingHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> responses = new(responses);
        internal List<CapturedRequest> Requests { get; } = [];
        internal HttpStatusCode StatusCode { get; init; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(request.RequestUri!.AbsoluteUri, request.Method.Method,
                request.Content.Headers.ContentType!.MediaType!, body));

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
