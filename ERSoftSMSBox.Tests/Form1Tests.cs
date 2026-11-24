using System.Threading;
using System.Windows.Forms;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraEditors;
using ERSoftSMSBox.Data;
using ERSoftSMSBox.Integrations;
using NUnit.Framework;

namespace ERSoftSMSBox.Tests;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
[SetUICulture("tr-TR")]
public class Form1Tests
{
    private static readonly (string Menu, string? Panel)[] Destinations =
    [
        ("HomePage", "dashboardPanel"), ("NewSms", "officesPanel"), ("BulkSms", "officesPanel"),
        ("Templates", "officesPanel"), ("Offices", "officesPanel"), ("Customers", "officesPanel"),
        ("PhoneBook", "officesPanel"), ("SentSms", "officesPanel"), ("SentMessages", "officesPanel"),
        ("DatabaseSettings", "databasePanel"), ("ConnectionSettings", "apiSettingsPanel"),
        ("IletiMerkezi", "integrationsPanel"), ("NetGsm", "integrationsPanel"), ("EyfelSoft", "integrationsPanel"),
        ("AppUpdate", "updatePanel"), ("DeliveryReports", null)
    ];

    private static IEnumerable<TestCaseData> MenuOrigins()
        => Destinations.Select(destination => new TestCaseData(destination.Menu));

    [TestCaseSource(nameof(MenuOrigins))]
    public void WhenNavigatingBetweenModulesThenOnlyDestinationPanelIsVisible(string origin)
    {
        using Form1 form = new();
        form.Show();
        foreach ((string menu, string? panel) in Destinations)
        {
            form.Ribbon.Items[origin].PerformClick();
            UiTestPump.DoEvents();
            form.Ribbon.Items[menu].PerformClick();
            UiTestPump.DoEvents();

            AssertWorkspace(form, panel, $"{origin} -> {menu}");
            if (panel == "officesPanel")
                Assert.That(form.Controls.Find(panel, true).Single().Controls.Cast<Control>()
                    .Where(control => control.Visible).Select(control => control.Tag),
                    Is.EqualTo(new[] { menu == "SentMessages" ? "SentSms" : menu }), $"{origin} -> {menu}");
            if (panel == "integrationsPanel")
                Assert.That(form.Controls.Find("sectionTitle", true).Single().Text,
                    Is.EqualTo(SmsProvider.Find(menu).Name), $"{origin} -> {menu}");
        }
    }

    [TestCase("quickNewSms", "SmsPage", "officesPanel")]
    [TestCase("quickBulkSms", "SmsPage", "officesPanel")]
    [TestCase("quickCustomers", "OfficesPage", "officesPanel")]
    [TestCase("quickDatabaseSettings", "OfficesPage", "databasePanel")]
    public void WhenQuickActionOpensThenRibbonAndWorkspaceNavigateTogether(string button, string tab, string panel)
    {
        using Form1 form = new();
        form.Show();
        ((SimpleButton)form.Controls.Find(button, true).Single()).PerformClick();
        UiTestPump.DoEvents();
        Assert.That(form.Ribbon.SelectedPage.Name, Is.EqualTo(tab));
        AssertWorkspace(form, panel, button);
    }

    [Test]
    public void WhenQuickHomeIsUsedThenSettingsTabCanOpenItsOwnScreenAgain()
    {
        using Form1 form = new();
        form.Show();
        form.Ribbon.Items["ConnectionSettings"].PerformClick();
        form.Ribbon.Items["HomePage"].PerformClick();
        UiTestPump.DoEvents();
        Assert.That(form.Ribbon.SelectedPage.Name, Is.EqualTo("HomePage"));

        form.Ribbon.SelectedPage = form.Ribbon.Pages.Single(page => page.Name == "SettingsPage");
        UiTestPump.DoEvents();

        AssertWorkspace(form, "apiSettingsPanel", "Home -> Settings");
    }

    [Test]
    public void WhenApplicationSettingsDialogClosesThenPreviousModuleDoesNotRemainOnSettingsPage()
    {
        using Form1 form = new();
        form.Show();
        form.Ribbon.Items["Customers"].PerformClick();
        using System.Windows.Forms.Timer closeDialog = new() { Interval = 50 };
        closeDialog.Tick += (_, _) =>
        {
            ReminderSettingsDialog? dialog = Application.OpenForms.OfType<ReminderSettingsDialog>().FirstOrDefault();
            if (dialog is null) return;
            closeDialog.Stop();
            dialog.Close();
        };
        closeDialog.Start();

        form.Ribbon.Items["ApplicationSettings"].PerformClick();
        UiTestPump.DoEvents();

        Assert.That(form.Ribbon.SelectedPage.Name, Is.EqualTo("SettingsPage"));
        AssertWorkspace(form, null, "ApplicationSettings");
    }

    [TestCase("HomePage", "dashboardPanel", null)]
    [TestCase("SmsPage", "officesPanel", "NewSms")]
    [TestCase("OfficesPage", "officesPanel", "Offices")]
    [TestCase("ReportsPage", "officesPanel", "SentSms")]
    [TestCase("SettingsPage", "apiSettingsPanel", null)]
    [TestCase("IntegrationsPage", "integrationsPanel", null)]
    [TestCase("AppearancePage", null, null)]
    public void WhenRibbonTabChangesThenItsOwnScreenOpens(string tab, string? panel, string? child)
    {
        using Form1 form = new();
        form.Show();
        UiTestPump.DoEvents();
        form.Ribbon.SelectedPage = form.Ribbon.Pages.Single(page => page.Name == (tab == "IntegrationsPage" ? "SmsPage" : "IntegrationsPage"));
        UiTestPump.DoEvents();

        form.Ribbon.SelectedPage = form.Ribbon.Pages.Single(page => page.Name == tab);
        UiTestPump.DoEvents();

        Assert.That(form.Ribbon.SelectedPage.Name, Is.EqualTo(tab));
        AssertWorkspace(form, panel, tab);
        if (child is not null)
        {
            Control offices = form.Controls.Find("officesPanel", true).Single();
            Assert.That(offices.Controls.Cast<Control>().Where(c => c.Visible).Select(c => c.Tag),
                Is.EqualTo(new[] { child }));
        }
    }

    [Test]
    public void WhenDashboardIsReboundThenOfficeNamesAndCountsAreDisplayed()
    {
        using Form1 form = new();
        form.Show();
        typeof(Form1).GetField("officeSummaries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(form, new List<OfficeSummary> { new("01", "Merkez mağaza", 12, 10, 3, 100, 0, 7, null) });
        var rebuild = typeof(Form1).GetMethod("BuildTopOfficesGrid", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        GridControl grid = (GridControl)form.Controls.Find("topOfficesGrid", true).Single();
        grid.ForceInitialize();
        for (int i = 0; i < 2; i++)
        {
            rebuild.Invoke(form, null);
            GridView view = (GridView)grid.MainView;
            Assert.That(view.GetRowCellValue(0, "OfficeName"), Is.EqualTo("Merkez mağaza"));
            Assert.That(view.GetRowCellValue(0, "SentCount"), Is.EqualTo(7));
        }
    }

    private static void AssertWorkspace(Form1 form, string? panel, string context)
    {
        string[] panels = ["dashboardPanel", "officesPanel", "integrationsPanel", "databasePanel", "apiSettingsPanel", "updatePanel"];
        string[] visible = panels.Where(name => form.Controls.Find(name, true).Single().Visible).ToArray();
        Assert.That(visible, Is.EqualTo(panel is null ? Array.Empty<string>() : new[] { panel }), context);
    }

    [Test]
    public void WhenMainFormIsCreatedThenShellTitleIsSet()
    {
        using Form1 form = new();

        Assert.That(form.Text, Is.EqualTo("ERSoft SMS Box"));
    }

    [Test]
    public void WhenCloseIsRequestedThenWindowIsHidden()
    {
        using Form1 form = new();
        form.Show();

        form.Close();

        Assert.That(form.Visible, Is.False);
    }

    [Test]
    public void WhenCloseIsRequestedThenFormRemainsAlive()
    {
        using Form1 form = new();
        form.Show();

        form.Close();

        Assert.That(form.IsDisposed, Is.False);
    }

    [Test]
    public void WhenWindowIsMinimizedThenWindowIsHidden()
    {
        using Form1 form = new();
        form.Show();

        form.WindowState = FormWindowState.Minimized;

        Assert.That(form.Visible, Is.False);
    }

    [Test]
    public void WhenHiddenWindowIsShownAgainThenWindowIsVisible()
    {
        using Form1 form = new();
        form.Show();
        form.Close();

        form.Show();

        Assert.That(form.Visible, Is.True);
    }
}
