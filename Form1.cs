using System.ComponentModel;
using System.Globalization;
using DevExpress.Utils;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraEditors;
using ERSoftSMSBox.Integrations;

namespace ERSoftSMSBox;

public partial class Form1 : RibbonForm
{
    private readonly ComponentResourceManager resources = new(typeof(Form1));
    private readonly LabelControl sectionTitle = new();
    private readonly LabelControl sectionDescription = new();
    private readonly BarStaticItem statusText = new();
    private readonly SvgImageCollection menuImages;
    private readonly GroupControl quickActions = new();

    /// <summary>
    /// Creates the SMS management shell.
    /// </summary>
    public Form1()
    {
        InitializeComponent();
        menuImages = CreateMenuImages(components ??= new Container());
        InitializeShell();
        ConfigureBackgroundMode();
    }

    private void InitializeShell()
    {
        SuspendLayout();
        Text = GetText("AppTitle");
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1000, 650);
        ClientSize = new Size(1280, 800);
        IconOptions.SvgImage = menuImages["NewSms"];

        RibbonControl ribbon = new()
        {
            Name = "mainRibbon",
            Dock = DockStyle.Top,
            RibbonStyle = RibbonControlStyle.Office2019,
            ShowApplicationButton = DevExpress.Utils.DefaultBoolean.False,
            ShowToolbarCustomizeItem = false,
            ToolbarLocation = RibbonQuickAccessToolbarLocation.Above,
            AccessibleName = GetText("AppTitle"),
            AccessibleDescription = GetText("MenuAccessibleDescription"),
            TabIndex = 0
        };

        RibbonPageGroup homeGroup = AddPage(ribbon, "HomePage", "NavigationGroup");
        BarButtonItem homeButton = AddButton(ribbon, homeGroup, "HomePage", HomeButtonClick);
        AddButton(ribbon, homeGroup, "Exit", ExitButtonClick);

        RibbonPageGroup smsGroup = AddPage(ribbon, "SmsPage", "SendGroup");
        BarButtonItem newSmsButton = AddButton(ribbon, smsGroup, "NewSms", ModuleButtonClick);
        AddButton(ribbon, smsGroup, "BulkSms", ModuleButtonClick);
        AddButton(ribbon, smsGroup, "Templates", OfficesButtonClick);

        RibbonPageGroup officesGroup = AddPage(ribbon, "OfficesPage", "OfficesGroup");
        AddButton(ribbon, officesGroup, "Offices", OfficesButtonClick);
        BarButtonItem contactsButton = AddButton(ribbon, officesGroup, "Customers", OfficesButtonClick);
        AddButton(ribbon, officesGroup, "PhoneBook", OfficesButtonClick);
        AddButton(ribbon, officesGroup, "SentSms", OfficesButtonClick);
        AddButton(ribbon, officesGroup, "DatabaseSettings", DatabaseButtonClick);

        RibbonPageGroup reportsGroup = AddPage(ribbon, "ReportsPage", "ReportsGroup");
        AddButton(ribbon, reportsGroup, "SentMessages", OfficesButtonClick);
        AddButton(ribbon, reportsGroup, "DeliveryReports", ModuleButtonClick);

        RibbonPageGroup settingsGroup = AddPage(ribbon, "SettingsPage", "SettingsGroup");
        AddButton(ribbon, settingsGroup, "ConnectionSettings", ModuleButtonClick);
        AddButton(ribbon, settingsGroup, "ApplicationSettings", ModuleButtonClick);
        AddButton(ribbon, settingsGroup, "AppUpdate", ModuleButtonClick);

        RibbonPageGroup integrationsGroup = AddPage(ribbon, "IntegrationsPage", "IntegrationsGroup");
        foreach (SmsProvider provider in SmsProvider.All)
        {
            AddButton(ribbon, integrationsGroup, provider.Key, IntegrationsButtonClick);
        }

        homeButton.ItemShortcut = new BarShortcut(Keys.Control | Keys.Home);
        newSmsButton.ItemShortcut = new BarShortcut(Keys.Control | Keys.N);
        contactsButton.ItemShortcut = new BarShortcut(Keys.Control | Keys.R);
        ribbon.Toolbar.ItemLinks.Add(homeButton);
        ribbon.Toolbar.ItemLinks.Add(newSmsButton);
        ribbon.Toolbar.ItemLinks.Add(contactsButton);

        RibbonPageGroup themeGroup = AddPage(ribbon, "AppearancePage", "ThemeGroup");
        SkinRibbonGalleryBarItem themeGallery = new()
        {
            Name = "themeGallery",
            Caption = GetText("SelectTheme"),
            Hint = GetText("SelectTheme")
        };
        themeGallery.ImageOptions.SvgImage = menuImages["SelectTheme"];
        themeGallery.SuperTip = CreateSuperTip("SelectTheme");
        ribbon.Items.Add(themeGallery);
        themeGroup.ItemLinks.Add(themeGallery);

        RibbonStatusBar statusBar = new()
        {
            Name = "mainStatusBar",
            Ribbon = ribbon,
            Dock = DockStyle.Bottom
        };
        statusText.Name = "statusText";
        statusText.ImageOptions.SvgImage = menuImages["Ready"];
        statusText.ImageOptions.SvgImageSize = new Size(16, 16);
        BarStaticItem designStatus = new()
        {
            Name = "designStatus",
            Caption = GetText("DesignStatus"),
            Alignment = BarItemLinkAlignment.Right
        };
        BarButtonItem updateBadgeItem = CreateUpdateBadge();
        BarStaticItem versionItem = new()
        {
            Name = "versionStatus",
            Caption = $"v{AppVersion.Current}",
            Alignment = BarItemLinkAlignment.Right
        };
        ribbon.Items.Add(statusText);
        ribbon.Items.Add(designStatus);
        ribbon.Items.Add(updateBadgeItem);
        ribbon.Items.Add(versionItem);
        statusBar.ItemLinks.Add(statusText);
        statusBar.ItemLinks.Add(designStatus);
        statusBar.ItemLinks.Add(updateBadgeItem);
        statusBar.ItemLinks.Add(versionItem);

        PanelControl workspace = new()
        {
            Name = "workspace",
            Dock = DockStyle.Fill,
            BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.NoBorder,
            Padding = new Padding(32),
            AccessibleName = GetText("WorkspaceAccessibleName"),
            AccessibleDescription = GetText("WorkspaceAccessibleDescription"),
            TabIndex = 1
        };
        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ColumnCount = 1,
            RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        sectionTitle.Name = "sectionTitle";
        sectionTitle.AutoSizeMode = LabelAutoSizeMode.Vertical;
        sectionTitle.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        sectionTitle.Appearance.FontSizeDelta = 12;
        sectionTitle.Appearance.FontStyleDelta = FontStyle.Bold;
        sectionTitle.Margin = new Padding(0, 8, 0, 20);
        sectionDescription.Name = "sectionDescription";
        sectionDescription.AutoSizeMode = LabelAutoSizeMode.Vertical;
        sectionDescription.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        sectionDescription.Appearance.FontSizeDelta = 2;
        sectionDescription.Margin = new Padding(0, 0, 0, 16);
        layout.Controls.Add(sectionTitle, 0, 0);
        layout.Controls.Add(sectionDescription, 0, 1);
        layout.Controls.Add(CreateQuickActions(), 0, 2);
        Panel contentHost = new() { Dock = DockStyle.Fill };
        contentHost.Controls.Add(CreateIntegrationsPanel());
        contentHost.Controls.Add(CreateDatabasePanel());
        contentHost.Controls.Add(CreateOfficesPanel());
        contentHost.Controls.Add(CreateUpdatePanel());
        contentHost.Controls.Add(CreateDashboardPanel());
        layout.Controls.Add(contentHost, 0, 3);
        workspace.Controls.Add(layout);

        Controls.Add(workspace);
        Controls.Add(statusBar);
        Controls.Add(ribbon);
        Ribbon = ribbon;
        StatusBar = statusBar;
        ribbon.SelectedPageChanged += RibbonPageChanged;
        ShowHome();
        ResumeLayout(true);
    }

    private RibbonPageGroup AddPage(RibbonControl ribbon, string pageKey, string groupKey)
    {
        RibbonPage page = new(GetText(pageKey)) { Name = pageKey };
        RibbonPageGroup group = new(GetText(groupKey))
        {
            Name = groupKey,
            ShowCaptionButton = false
        };
        page.Groups.Add(group);
        ribbon.Pages.Add(page);

        return group;
    }

    private BarButtonItem AddButton(RibbonControl ribbon, RibbonPageGroup group, string key,
        ItemClickEventHandler handler)
    {
        BarButtonItem button = new()
        {
            Name = key,
            Caption = GetText(key),
            Hint = GetText(key + "Hint"),
            RibbonStyle = RibbonItemStyles.Large,
            Tag = key
        };
        button.ImageOptions.SvgImage = menuImages[key];
        button.ImageOptions.SvgImageSize = new Size(32, 32);
        button.SuperTip = CreateSuperTip(key);
        button.ItemClick += handler;
        ribbon.Items.Add(button);
        group.ItemLinks.Add(button);

        return button;
    }

    private SuperToolTip CreateSuperTip(string key)
    {
        SuperToolTip toolTip = new();
        toolTip.Items.AddTitle(GetText(key));
        toolTip.Items.Add(GetText(key + "Hint"));

        return toolTip;
    }

    private GroupControl CreateQuickActions()
    {
        quickActions.Name = "quickActions";
        quickActions.Text = GetText("QuickActions");
        quickActions.AutoSize = true;
        quickActions.AutoSizeMode = AutoSizeMode.GrowOnly;
        quickActions.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        quickActions.Margin = new Padding(0, 12, 0, 0);
        quickActions.Padding = new Padding(12);
        quickActions.TabIndex = 0;
        quickActions.AccessibleName = GetText("QuickActions");
        quickActions.AccessibleDescription = GetText("QuickActionsDescription");

        FlowLayoutPanel actionsLayout = new()
        {
            Name = "quickActionsLayout",
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            WrapContents = true,
            TabIndex = 0
        };
        string[] actions = ["NewSms", "BulkSms", "Customers", "DatabaseSettings"];
        foreach (string key in actions)
        {
            SimpleButton button = new()
            {
                Name = "quick" + key,
                Text = GetText(key),
                Tag = key,
                AutoSize = true,
                MinimumSize = new Size(200, 64),
                Margin = new Padding(6),
                AccessibleName = GetText(key),
                AccessibleDescription = GetText(key + "Hint"),
                TabIndex = actionsLayout.Controls.Count,
                SuperTip = CreateSuperTip(key)
            };
            button.ImageOptions.SvgImage = menuImages[key];
            button.ImageOptions.SvgImageSize = new Size(32, 32);
            button.Click += QuickActionClick;
            actionsLayout.Controls.Add(button);
        }
        quickActions.Controls.Add(actionsLayout);

        return quickActions;
    }

    private void ShowHome()
    {
        HideIntegrations();
        HideDatabase();
        HideOffices();
        HideUpdate();
        sectionTitle.Text = GetText("HomeTitle");
        sectionDescription.Text = GetText("HomeDescription");
        statusText.Caption = GetText("Ready");
        quickActions.Visible = true;
        ShowDashboard();
    }

    private void HomeButtonClick(object sender, ItemClickEventArgs e) => ShowHome();

    private void ExitButtonClick(object sender, ItemClickEventArgs e) => Close();

    private void ModuleButtonClick(object sender, ItemClickEventArgs e)
    {
        if (e.Item.Tag is string key)
        {
            ShowModule(key);
        }
    }

    private void QuickActionClick(object? sender, EventArgs e)
    {
        if (sender is SimpleButton { Tag: string key })
        {
            ShowModule(key);
        }
    }

    private void ShowModule(string key)
    {
        switch (key)
        {
            case "Offices" or "Customers" or "PhoneBook" or "SentSms" or "Templates" or "NewSms" or "BulkSms":
                ShowOffices(key);
                return;
            case "SentMessages":
                ShowOffices("SentSms");
                return;
            case "DatabaseSettings":
                ShowDatabase();
                return;
            case "ConnectionSettings":
                ShowIntegrations();
                return;
            case "ApplicationSettings":
                using (ReminderSettingsDialog dialog = new())
                {
                    dialog.ShowDialog(this);
                }
                return;
            case "AppUpdate":
                ShowUpdate();
                return;
        }
        HideIntegrations();
        HideDatabase();
        HideOffices();
        sectionTitle.Text = GetText(key);
        sectionDescription.Text = string.Format(CultureInfo.CurrentCulture,
            GetText("ModuleDescription"), sectionTitle.Text);
        statusText.Caption = sectionTitle.Text;
        quickActions.Visible = false;
    }

    private string GetText(string key) => resources.GetString(key) ?? key;
}
