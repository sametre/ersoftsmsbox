using System.Net;
using System.Text;
using System.Web;
using ERSoftSMSBox.Integrations;
using NUnit.Framework;

namespace ERSoftSMSBox.Tests;

[TestFixture]
public class EyfelSoftClientTests
{
    private static readonly EyfelSoftSettings Asmx = new(true, "https://sms.example.test/sms.asmx/send_sms",
        "TEST", "test-user", "test-password", "test-vendor");

    [Test]
    public async Task WhenAsmxSmsIsSentThenAllLegacyParametersAndTurkishCharactersAreEncodedOnce()
    {
        using RecordingHandler handler = new("<string xmlns=\"http://tempuri.org/\">ACK-123</string>");
        using HttpClient http = new(handler);
        string message = "ç Ç ğ Ğ ı İ ö Ö ş Ş ü Ü & + % ? # =\r\nİkinci satır";
        string result = await new EyfelSoftClient(http).SendAsync(Asmx, "05321234567", message);
        var query = HttpUtility.ParseQueryString(handler.Uri!.Query);
        Assert.Multiple(() =>
        {
            Assert.That(handler.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(query["message"], Is.EqualTo(message));
            Assert.That(query["gsm"], Is.EqualTo("905321234567"));
            Assert.That(query["username"], Is.EqualTo(Asmx.Username));
            Assert.That(query["password"], Is.EqualTo(Asmx.Password));
            Assert.That(query["vendor"], Is.EqualTo(Asmx.Vendor));
            Assert.That(query["header"], Is.EqualTo("TEST"));
            Assert.That(query["charset"], Is.EqualTo("turkish"));
            Assert.That(query.AllKeys, Does.Contain("startdate").And.Contain("exdate"));
            Assert.That(handler.Uri.AbsoluteUri, Does.Contain("%C3%A7%20").And.Not.Contain("%25C3"));
            Assert.That(query["mesaj"], Is.Null);
            Assert.That(result, Is.EqualTo("ACK-123"));
        });
    }

    [Test]
    public async Task WhenPhpEndpointIsUsedThenLegacyParametersAndStaticTokenArePreserved()
    {
        using RecordingHandler handler = new("\"ACK-456\"");
        using HttpClient http = new(handler);
        EyfelSoftSettings settings = new(true, "https://sms.example.test/account.send.php?token=keep&gsm=old&mesaj=old", "TEST");
        string result = await new EyfelSoftClient(http).SendAsync(settings, "05321234567", "Türkçe & + %");
        var query = HttpUtility.ParseQueryString(handler.Uri!.Query);
        Assert.Multiple(() =>
        {
            Assert.That(query["token"], Is.EqualTo("keep"));
            Assert.That(query.GetValues("gsm"), Has.Length.EqualTo(1));
            Assert.That(query["mesaj"], Is.EqualTo("Türkçe & + %"));
            Assert.That(query["baslik"], Is.EqualTo("TEST"));
            Assert.That(query["message"], Is.Null);
            Assert.That(result, Is.EqualTo("ACK-456"));
        });
    }

    [TestCase("\"Hata : Yetersiz bakiye.\"")]
    [TestCase("Hata : Yanlış parola")]
    [TestCase("<string>ERROR: invalid credentials</string>")]
    [TestCase("<html><body>Login</body></html>")]
    [TestCase("")]
    public void WhenProviderRejectsOrReturnsUnexpectedContentThenItIsNotReportedAsSuccess(string body)
    {
        using RecordingHandler handler = new(body);
        using HttpClient http = new(handler);
        Assert.ThrowsAsync<InvalidDataException>(() => new EyfelSoftClient(http).SendAsync(Asmx, "05321234567", "Test"));
    }

    [Test]
    public void WhenHttpErrorContainsAccountUrlThenExceptionHidesCredentials()
    {
        using RecordingHandler handler = new("test-user test-password") { Status = HttpStatusCode.Unauthorized };
        using HttpClient http = new(handler);
        HttpRequestException? exception = Assert.ThrowsAsync<HttpRequestException>(
            () => new EyfelSoftClient(http).SendAsync(Asmx, "05321234567", "Test"));
        Assert.That(exception!.Message, Does.Not.Contain("test-user").And.Not.Contain("test-password"));
    }

    [Test]
    public async Task WhenConnectionIsCheckedThenNoMessageOrRecipientIsSent()
    {
        using RecordingHandler handler = new("<definitions xmlns=\"http://schemas.xmlsoap.org/wsdl/\"><operation name=\"send_sms\"/></definitions>");
        using HttpClient http = new(handler);
        SmsProviderAccount account = await new EyfelSoftClient(http).CheckAsync(Asmx);
        Assert.Multiple(() =>
        {
            Assert.That(handler.Uri!.AbsolutePath, Is.EqualTo("/sms.asmx"));
            Assert.That(handler.Uri.Query, Is.EqualTo("?WSDL"));
            Assert.That(account.CredentialsVerified, Is.False);
            Assert.That(handler.Uri.AbsoluteUri, Does.Not.Contain("test-password").And.Not.Contain("gsm="));
        });
    }

    [TestCase(false, "05321234567")]
    [TestCase(true, "invalid")]
    public void WhenInactiveOrPhoneIsInvalidThenNetworkIsNotCalled(bool active, string phone)
    {
        using RecordingHandler handler = new("ACK");
        using HttpClient http = new(handler);
        Assert.CatchAsync<Exception>(() => new EyfelSoftClient(http).SendAsync(Asmx with { IsActive = active }, phone, "Test"));
        Assert.That(handler.Uri, Is.Null);
    }

    private sealed class RecordingHandler(string body) : HttpMessageHandler
    {
        internal Uri? Uri { get; private set; }
        internal HttpMethod? Method { get; private set; }
        internal HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            Method = request.Method;
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(body, Encoding.UTF8) });
        }
    }
}
