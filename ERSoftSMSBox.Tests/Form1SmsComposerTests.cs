using System.Threading;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraTab;
using ERSoftSMSBox.Messaging;
using NUnit.Framework;

namespace ERSoftSMSBox.Tests;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
[SetUICulture("tr-TR")]
public class Form1SmsComposerTests
{
    [TestCase("NewSms", "NewSms")]
    [TestCase("BulkSms", "BulkSms")]
    [TestCase("Customers", "Customers")]
    [TestCase("Offices", "Offices")]
    [TestCase("Templates", "Templates")]
    [TestCase("PhoneBook", "PhoneBook")]
    [TestCase("SentSms", "SentSms")]
    [TestCase("SentMessages", "SentSms")]
    public void WhenMenuIsClickedThenOnlyItsScreenIsVisible(string menu, string destination)
    {
        using Form1 form = new();
        form.Show();

        form.Ribbon.Items[menu].PerformClick();

        Assert.That(Find<Control>(form, "officesPanel").Controls.Cast<Control>()
            .Where(control => control.Visible).Select(control => control.Tag), Is.EqualTo(new[] { destination }));
    }

    [Test]
    public void WhenSingleRecipientIsFocusedThenOnlyThatRecipientIsPreviewed()
    {
        using Form1 form = new();
        form.Show();
        form.Ribbon.Items["NewSms"].PerformClick();
        GridControl grid = Find<GridControl>(form, "NewSmsGrid");
        grid.DataSource = new List<SmsRecipient>
        {
            new("C1", "Ali", "O1", "Merkez", "05321234567", 0, null),
            new("C2", "Ayse", "O1", "Merkez", "05329876543", 0, null)
        };
        grid.ForceInitialize();
        ((GridView)grid.MainView).FocusedRowHandle = 1;
        Find<MemoEdit>(form, "NewSmsMessage").Text = "Merhaba {AdSoyad}";

        Find<SimpleButton>(form, "NewSmsPrepare").PerformClick();

        Assert.That(Find<MemoEdit>(form, "NewSmsPreview").Text, Is.EqualTo("905329876543: Merhaba Ayse"));
    }

    [Test]
    public void WhenTwoBulkRecipientsAreSelectedThenBothArePersonalized()
    {
        using Form1 form = new();
        form.Show();
        form.Ribbon.Items["BulkSms"].PerformClick();
        GridControl grid = Find<GridControl>(form, "BulkSmsGrid");
        grid.DataSource = Recipients();
        grid.ForceInitialize();
        GridView view = (GridView)grid.MainView;
        view.ClearSelection();
        view.SelectRow(0);
        view.SelectRow(1);
        Find<MemoEdit>(form, "BulkSmsMessage").Text = "Merhaba {AdSoyad}";

        Find<SimpleButton>(form, "BulkSmsPrepare").PerformClick();

        Assert.That(Find<MemoEdit>(form, "BulkSmsPreview").Text,
            Is.EqualTo("905321234567: Merhaba Ali" + Environment.NewLine + Environment.NewLine + "905329876543: Merhaba Ayse"));
    }

    [TestCase("NewSms", "BulkSms")]
    [TestCase("BulkSms", "NewSms")]
    public void WhenComposerMessageChangesThenOtherDraftIsPreserved(string changed, string other)
    {
        using Form1 form = new();
        Find<MemoEdit>(form, other + "Message").Text = "Korunan taslak";

        Find<MemoEdit>(form, changed + "Message").Text = "Yeni taslak";

        Assert.That(Find<MemoEdit>(form, other + "Message").Text, Is.EqualTo("Korunan taslak"));
    }

    [TestCase("NewSms")]
    [TestCase("BulkSms")]
    public void WhenNoRecipientExistsThenPreviewIsDisabled(string key)
    {
        using Form1 form = new();
        form.Show();
        form.Ribbon.Items[key].PerformClick();

        Find<MemoEdit>(form, key + "Message").Text = "Merhaba";

        Assert.That(Find<SimpleButton>(form, key + "Prepare").Enabled, Is.False);
    }

    [TestCase("NewSmsMessage")]
    [TestCase("NewSmsSearch")]
    public void WhenDraftInputChangesThenOldPreviewIsCleared(string input)
    {
        using Form1 form = new();
        form.Show();
        form.Ribbon.Items["NewSms"].PerformClick();
        GridControl grid = Find<GridControl>(form, "NewSmsGrid");
        grid.DataSource = Recipients();
        grid.ForceInitialize();
        ((GridView)grid.MainView).FocusedRowHandle = 0;
        Find<MemoEdit>(form, "NewSmsMessage").Text = "Merhaba {AdSoyad}";
        Find<SimpleButton>(form, "NewSmsPrepare").PerformClick();

        Find<Control>(form, input).Text = "Degisen deger";

        Assert.That(Find<MemoEdit>(form, "NewSmsPreview").Text, Is.Empty);
    }

    [TestCase("NewSms", false)]
    [TestCase("BulkSms", true)]
    public void WhenComposerIsCreatedThenSelectionModeMatchesPurpose(string key, bool multiple)
    {
        using Form1 form = new();

        GridView view = (GridView)Find<GridControl>(form, key + "Grid").MainView;

        Assert.That(view.OptionsSelection.MultiSelect, Is.EqualTo(multiple));
    }

    [Test]
    public void WhenShellIsCreatedThenNoSharedTabControlExists()
    {
        using Form1 form = new();

        IEnumerable<XtraTabControl> tabControls = Descendants(form).OfType<XtraTabControl>();

        Assert.That(tabControls, Is.Empty);
    }

    [TestCase("NewSms", "BulkSms")]
    [TestCase("BulkSms", "Customers")]
    [TestCase("Customers", "Templates")]
    [TestCase("Templates", "Offices")]
    [TestCase("Offices", "PhoneBook")]
    [TestCase("PhoneBook", "SentSms")]
    [TestCase("SentSms", "NewSms")]
    public void WhenAnotherMenuOpensThenPreviousScreenIsHidden(string previous, string next)
    {
        using Form1 form = new();
        form.Show();
        form.Ribbon.Items[previous].PerformClick();

        form.Ribbon.Items[next].PerformClick();

        Assert.That(Find<Control>(form, "officesPanel").Controls.Cast<Control>()
            .Where(control => control.Visible).Select(control => control.Tag), Is.EqualTo(new[] { next }));
    }

    [TestCase("NewSms", "BulkSms")]
    [TestCase("BulkSms", "Customers")]
    public void WhenReturningThroughMenuThenMessageDraftIsPreserved(string composer, string other)
    {
        using Form1 form = new();
        form.Show();
        form.Ribbon.Items[composer].PerformClick();
        Find<MemoEdit>(form, composer + "Message").Text = "Kisiye ozel taslak";

        form.Ribbon.Items[other].PerformClick();
        form.Ribbon.Items[composer].PerformClick();

        Assert.That(Find<MemoEdit>(form, composer + "Message").Text, Is.EqualTo("Kisiye ozel taslak"));
    }

    [Test]
    public void WhenHomeOpensThenMenuScreensAreHidden()
    {
        using Form1 form = new();
        form.Show();
        form.Ribbon.Items["NewSms"].PerformClick();

        form.Ribbon.Items["HomePage"].PerformClick();

        Assert.That(Find<Control>(form, "officesPanel").Visible, Is.False);
    }

    [TestCase("customerGrid")]
    [TestCase("NewSmsGrid")]
    [TestCase("BulkSmsGrid")]
    public void WhenCustomerScreenIsCreatedThenLegacyDateFieldsAreAvailable(string gridName)
    {
        using Form1 form = new();
        GridView view = (GridView)Find<GridControl>(form, gridName).MainView;
        string[] expected = [nameof(SmsRecipient.LastPaymentDate), nameof(SmsRecipient.DueDate),
            nameof(SmsRecipient.ReferenceDate), nameof(SmsRecipient.RecordedPaymentDate),
            nameof(SmsRecipient.PaymentDateChanged), nameof(SmsRecipient.LastReminderDate),
            nameof(SmsRecipient.ReminderReferenceDate), nameof(SmsRecipient.HasMessageStage),
            nameof(SmsRecipient.NeedsReminderCheck)];

        IEnumerable<string> fields = view.Columns.Cast<DevExpress.XtraGrid.Columns.GridColumn>().Select(column => column.FieldName);

        Assert.That(fields, Is.SupersetOf(expected));
    }

    private IEnumerable<Control> Descendants(Control parent)
        => parent.Controls.Cast<Control>().SelectMany(control => new[] { control }.Concat(Descendants(control)));

    private List<SmsRecipient> Recipients() =>
    [
        new("C1", "Ali", "O1", "Merkez", "05321234567", 0, null),
        new("C2", "Ayse", "O1", "Merkez", "05329876543", 0, null)
    ];

    private T Find<T>(Form1 form, string name) where T : Control
        => form.Controls.Find(name, true).OfType<T>().Single();
}
