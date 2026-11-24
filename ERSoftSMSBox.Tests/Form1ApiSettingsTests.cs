using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using ERSoftSMSBox.Integrations;
using NUnit.Framework;

namespace ERSoftSMSBox.Tests;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
[SetUICulture("tr-TR")]
public class Form1ApiSettingsTests
{
    private static readonly SmsProviderDraft Settings = new(true, "https://sms.example.test/sms.asmx/send_sms",
        "test-password", "TEST", "test-user", "test-vendor");

    [Test]
    public void WhenApiSettingsAreSavedThenAnotherFormLoadsEveryFieldWithoutSqlConnection()
    {
        EyfelSoftProvider provider = CreateProvider();
        using (Form1 form = Open(provider, new RecordingHandler()))
        {
            Populate(form, Settings);
            Find<SimpleButton>(form, "apiSave").PerformClick();
            WaitForApi(form);
            Assert.That(provider.LoadAsync(CancellationToken.None).GetAwaiter().GetResult(), Is.EqualTo(Settings));
        }

        using Form1 reloaded = Open(provider, new RecordingHandler());
        Assert.Multiple(() =>
        {
            Assert.That(Find<TextEdit>(reloaded, "apiUrl").Text, Is.EqualTo(Settings.Identifier));
            Assert.That(Find<TextEdit>(reloaded, "apiPassword").Text, Is.EqualTo(Settings.Secret));
            Assert.That(Find<TextEdit>(reloaded, "apiPassword").Properties.UseSystemPasswordChar, Is.True);
            Assert.That(Find<TextEdit>(reloaded, "apiUsername").Text, Is.EqualTo(Settings.Username));
            Assert.That(Find<TextEdit>(reloaded, "apiVendor").Text, Is.EqualTo(Settings.Vendor));
            Assert.That(Find<TextEdit>(reloaded, "apiSender").Text, Is.EqualTo(Settings.Sender));
            Assert.That(Find<CheckEdit>(reloaded, "apiEnabled").Checked, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WhenSaveAndConnectIsClickedThenSavedConfigurationIsCheckedWithoutSendingSms(bool php)
    {
        EyfelSoftProvider provider = CreateProvider();
        SmsProviderDraft expected = php ? new(true, "https://sms.example.test/account.send.php") : Settings;
        using RecordingHandler handler = new()
        {
            Body = php ? "\"Hata : GSM no eksik.\"" : RecordingHandler.Wsdl,
            BeforeRequest = () => Assert.That(provider.LoadAsync(CancellationToken.None).GetAwaiter().GetResult(), Is.EqualTo(expected))
        };
        using Form1 form = Open(provider, handler);
        Populate(form, expected);

        Find<SimpleButton>(form, "apiSaveConnect").PerformClick();
        WaitForApi(form);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests, Has.Count.EqualTo(1));
            Assert.That(handler.Requests[0].Query, php ? Is.Empty : Is.EqualTo("?WSDL"));
            Assert.That(handler.Requests[0].AbsolutePath, Does.Not.EndWith("/send_sms"));
            Assert.That(Find<Control>(form, "apiResult").Text,
                Does.Contain("ayarları kaydedildi").And.Contain("Servis erişilebilir"));
            Assert.That(form.Controls.Find("databasePanel", true).Single().Visible, Is.False);
        });
    }

    [Test]
    public void WhenConnectionFailsThenSettingsRemainSavedAndFailureIsVisible()
    {
        EyfelSoftProvider provider = CreateProvider();
        using RecordingHandler handler = new() { Status = HttpStatusCode.ServiceUnavailable };
        using Form1 form = Open(provider, handler);
        Populate(form, Settings);

        Find<SimpleButton>(form, "apiSaveConnect").PerformClick();
        WaitForApi(form);

        Assert.That(provider.LoadAsync(CancellationToken.None).GetAwaiter().GetResult(), Is.EqualTo(Settings));
        Assert.That(Find<Control>(form, "apiResult").Text,
            Does.Contain("ayarları kaydedildi").And.Contain("503").And.Not.Contain(Settings.Secret));
    }

    [Test]
    public void WhenAnotherMenuOpensDuringConnectionCheckThenCompletionCannotReopenApiScreen()
    {
        EyfelSoftProvider provider = CreateProvider();
        using PendingHandler handler = new();
        using Form1 form = Open(provider, handler);
        Populate(form, Settings);
        Find<SimpleButton>(form, "apiSaveConnect").PerformClick();
        PumpUntil(() => handler.Started, "Connection check did not start");

        form.Ribbon.Items["Customers"].PerformClick();
        WaitForApi(form);
        UiTestPump.DoEvents();

        Assert.Multiple(() =>
        {
            Assert.That(handler.Cancelled, Is.True);
            Assert.That(Find<Control>(form, "apiSettingsPanel").Visible, Is.False);
            Assert.That(Find<Control>(form, "officesPanel").Visible, Is.True);
            Assert.That(Find<Control>(form, "sectionTitle").Text, Is.EqualTo("Müşteriler"));
        });
    }

    [Test]
    public void WhenLeavingAndReturningThenUnsavedApiDraftIsPreservedOnlyOnApiScreen()
    {
        using RecordingHandler handler = new();
        using Form1 form = Open(CreateProvider(), handler);
        Populate(form, Settings);
        form.Ribbon.Items["DatabaseSettings"].PerformClick();
        UiTestPump.DoEvents();
        Assert.That(Find<Control>(form, "apiSettingsPanel").Visible, Is.False);
        Assert.That(Find<Control>(form, "databasePanel").Controls.Find("apiUrl", true), Is.Empty);

        form.Ribbon.Items["ConnectionSettings"].PerformClick();
        WaitForApi(form);

        Assert.That(Find<TextEdit>(form, "apiPassword").Text, Is.EqualTo(Settings.Secret));
        Assert.That(handler.Requests, Is.Empty);
    }

    [Test]
    public void WhenApiAddressIsInvalidThenNoConnectionIsAttemptedAndNoSettingsAreOverwritten()
    {
        EyfelSoftProvider provider = CreateProvider();
        provider.SaveAsync(Settings, CancellationToken.None).GetAwaiter().GetResult();
        using RecordingHandler handler = new();
        using Form1 form = Open(provider, handler);
        Find<TextEdit>(form, "apiUrl").Text = "not a URL";

        Find<SimpleButton>(form, "apiSave").PerformClick();
        WaitForApi(form);

        Assert.That(Find<SimpleButton>(form, "apiSaveConnect").Enabled, Is.False);
        Assert.That(handler.Requests, Is.Empty);
        Assert.That(provider.LoadAsync(CancellationToken.None).GetAwaiter().GetResult(), Is.EqualTo(Settings));
    }

    private static EyfelSoftProvider CreateProvider() => new(Path.Combine(Path.GetTempPath(),
        "ERSoftApiUiTests", Guid.NewGuid().ToString("N")));

    private static Form1 Open(EyfelSoftProvider provider, HttpMessageHandler handler)
    {
        Form1 form = new(provider, new HttpClient(handler));
        form.Show();
        form.Ribbon.Items["ConnectionSettings"].PerformClick();
        WaitForApi(form);
        return form;
    }

    private static void Populate(Form1 form, SmsProviderDraft settings)
    {
        Find<TextEdit>(form, "apiUrl").Text = settings.Identifier;
        Find<TextEdit>(form, "apiUsername").Text = settings.Username;
        Find<TextEdit>(form, "apiPassword").Text = settings.Secret;
        Find<TextEdit>(form, "apiVendor").Text = settings.Vendor;
        Find<TextEdit>(form, "apiSender").Text = settings.Sender;
        Find<CheckEdit>(form, "apiEnabled").Checked = settings.IsActive;
    }

    private static void WaitForApi(Form1 form)
    {
        Task current;
        do
        {
            current = form.ApiSettingsCompletion;
            PumpUntil(() => current.IsCompleted, "API operation timed out");
            current.GetAwaiter().GetResult();
        } while (!ReferenceEquals(current, form.ApiSettingsCompletion));
    }

    private static void PumpUntil(Func<bool> complete, string failure)
    {
        Stopwatch timer = Stopwatch.StartNew();
        while (!complete() && timer.Elapsed < TimeSpan.FromSeconds(10))
        {
            UiTestPump.DoEvents();
            Thread.Sleep(5);
        }
        Assert.That(complete(), Is.True, failure);
    }

    private static T Find<T>(Form1 form, string name) where T : Control
        => form.Controls.Find(name, true).OfType<T>().Single();

    private sealed class RecordingHandler : HttpMessageHandler
    {
        internal const string Wsdl = "<definitions xmlns=\"http://schemas.xmlsoap.org/wsdl/\"><operation name=\"send_sms\"/></definitions>";
        internal string Body { get; init; } = Wsdl;
        internal HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        internal Action? BeforeRequest { get; init; }
        internal List<Uri> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            BeforeRequest?.Invoke();
            Requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Body) });
        }
    }

    private sealed class PendingHandler : HttpMessageHandler
    {
        internal bool Started { get; private set; }
        internal bool Cancelled { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Started = true;
            try { await Task.Delay(Timeout.Infinite, cancellationToken); }
            catch (OperationCanceledException) { Cancelled = true; throw; }
            throw new InvalidOperationException("The pending request must be cancelled.");
        }
    }
}
