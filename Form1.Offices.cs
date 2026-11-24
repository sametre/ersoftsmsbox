using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevExpress.Data;
using DevExpress.Data.Filtering;
using DevExpress.Utils;
using DevExpress.XtraBars;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid;
using ERSoftSMSBox.Data;
using ERSoftSMSBox.Integrations;
using ERSoftSMSBox.Messaging;
using Microsoft.Data.SqlClient;

namespace ERSoftSMSBox;

public partial class Form1
{
    private readonly PanelControl officesPanel = new();
    private readonly Dictionary<string, Control> officePages = new(StringComparer.Ordinal);
    private string? activeOfficePageKey;
    private readonly Dictionary<string, LabelControl> tabStatus = [];
    private readonly Dictionary<string, bool> tabLoaded = [];
    private readonly MessageTemplateStore templateStore = new();
    private readonly Dictionary<string, string> templates = [];
    private CancellationTokenSource? officeOperation;
    private Task officeOperationCompletion = Task.CompletedTask;
    private Task? templateLoading;
    private bool officeBusy;
    private bool templatesLoaded;

    private readonly GridControl officeGrid = new();
    private readonly GridView officeView = new();
    private readonly SimpleButton officeRefresh = new();
    private readonly SimpleButton officeShowCustomers = new();

    private readonly ComboBoxEdit customerOffice = new();
    private readonly ComboBoxEdit customerCategory = new();
    private readonly TextEdit customerSearch = new();
    private readonly CheckEdit customerRequirePhone = new();
    private readonly SimpleButton customerList = new();
    private readonly SimpleButton customerPrepare = new();
    private readonly GridControl customerGrid = new();
    private readonly GridView customerView = new();
    private readonly MemoEdit customerTemplate = new();
    private readonly MemoEdit customerPreview = new();
    private bool customerTemplateEdited;

    private readonly ComboBoxEdit phoneOffice = new();
    private readonly TextEdit phoneSearch = new();
    private readonly SimpleButton phoneList = new();
    private readonly SimpleButton phoneExport = new();
    private readonly GridControl phoneGrid = new();
    private readonly GridView phoneView = new();

    private readonly ComboBoxEdit sentOffice = new();
    private readonly DateEdit sentFrom = new();
    private readonly DateEdit sentTo = new();
    private readonly TextEdit sentSearch = new();
    private readonly SimpleButton sentList = new();
    private readonly SimpleButton sentClearFilter = new();
    private readonly GridControl sentSummaryGrid = new();
    private readonly GridView sentSummaryView = new();
    private readonly GridControl sentDetailGrid = new();
    private readonly GridView sentDetailView = new();

    private readonly ListBoxControl templateList = new();
    private readonly MemoEdit templateEditor = new();
    private readonly SimpleButton templateSave = new();
    private readonly SimpleButton templateReset = new();
    private bool templatePopulating;

    private PanelControl CreateOfficesPanel()
    {
        officesPanel.Name = "officesPanel";
        officesPanel.Dock = DockStyle.Fill;
        officesPanel.BorderStyle = BorderStyles.NoBorder;
        officesPanel.Visible = false;
        officesPanel.AccessibleName = GetText("OfficesPage");
        Disposed += (_, _) => officeOperation?.Cancel();

        AddOfficePage("Offices", CreateOfficesTab());
        AddOfficePage("Customers", CreateCustomersTab());
        AddOfficePage("PhoneBook", CreatePhoneBookTab());
        AddOfficePage("SentSms", CreateSentSmsTab());
        AddOfficePage("Templates", CreateTemplatesTab());
        AddOfficePage("NewSms", CreateSmsComposer(singleSms));
        AddOfficePage("BulkSms", CreateSmsComposer(bulkSms));
        UpdateOfficeControls();

        return officesPanel;
    }

    private void AddOfficePage(string key, Control content)
    {
        content.Name = "page" + key;
        content.Tag = key;
        content.Dock = DockStyle.Fill;
        content.Visible = false;
        content.AccessibleName = GetText(key);
        content.AccessibleDescription = GetText(key + "Description");
        content.VisibleChanged += OfficePageVisibleChangedAsync;
        officePages.Add(key, content);
        tabLoaded[key] = false;
        officesPanel.Controls.Add(content);
    }

    private TableLayoutPanel CreateTabLayout(string key, params Control[] rows)
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = rows.Length + 1, Padding = new Padding(8) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int index = 0; index < rows.Length; index++)
        {
            layout.RowStyles.Add(new RowStyle(rows[index].Dock == DockStyle.Fill ? SizeType.Percent : SizeType.AutoSize,
                rows[index].Dock == DockStyle.Fill ? 100 : 0));
            layout.Controls.Add(rows[index], 0, index);
        }
        LabelControl status = new()
        {
            Name = "status" + key, Text = GetText("OfficeConnectFirst"), AutoSizeMode = LabelAutoSizeMode.Vertical,
            Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(3, 6, 3, 3)
        };
        tabStatus[key] = status;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(status, 0, rows.Length);

        return layout;
    }

    private FlowLayoutPanel CreateToolbar(params Control[] controls)
    {
        FlowLayoutPanel bar = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Anchor = AnchorStyles.Left | AnchorStyles.Right, WrapContents = true };
        bar.Controls.AddRange(controls);

        return bar;
    }

    private Control Labeled(string key, Control editor, int width)
    {
        FlowLayoutPanel pair = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0, 0, 12, 0) };
        LabelControl label = new() { Text = GetText(key), Margin = new Padding(3, 9, 6, 3) };
        editor.Name = "edit" + key;
        editor.Width = width;
        editor.Margin = new Padding(3, 5, 3, 3);
        editor.AccessibleName = GetText(key);
        pair.Controls.Add(label);
        pair.Controls.Add(editor);

        return pair;
    }

    private void ConfigureToolbarButton(SimpleButton button, string name, string textKey, string iconKey)
    {
        ConfigureIntegrationButton(button, name, textKey, iconKey, 0);
        button.MinimumSize = new Size(120, 30);
        button.Margin = new Padding(3, 4, 9, 3);
    }

    private static GridControl ConfigureGrid(GridControl grid, GridView view, string name, bool checkBoxSelect = false, bool allowGroup = true)
    {
        grid.Name = name;
        grid.Dock = DockStyle.Fill;
        grid.MainView = view;
        grid.MinimumSize = new Size(0, 160);
        view.GridControl = grid;
        view.OptionsBehavior.Editable = false;
        view.OptionsBehavior.ReadOnly = true;
        view.OptionsView.ShowAutoFilterRow = true;
        view.OptionsView.ShowFooter = true;
        view.OptionsView.ShowGroupPanel = allowGroup;
        view.OptionsView.ColumnAutoWidth = false;
        view.OptionsCustomization.AllowGroup = allowGroup;
        view.OptionsFind.AlwaysVisible = true;
        view.OptionsFind.FindNullPrompt = IntegrationText.Get("OfficeFindPrompt");
        view.OptionsSelection.MultiSelect = checkBoxSelect;
        if (checkBoxSelect)
        {
            view.OptionsSelection.MultiSelectMode = GridMultiSelectMode.CheckBoxRowSelect;
            view.OptionsSelection.ShowCheckBoxSelectorInColumnHeader = DefaultBoolean.True;
        }

        return grid;
    }

    private GridColumn AddColumn(GridView view, string field, string captionKey, string? format = null, SummaryItemType summary = SummaryItemType.None)
    {
        GridColumn column = view.Columns.AddVisible(field, GetText(captionKey));
        column.OptionsColumn.AllowEdit = false;
        column.MinWidth = 60;
        column.Width = format is "n2" ? 110 : 120;
        if (format is not null)
        {
            column.DisplayFormat.FormatType = format == "n2" || format == "n0" ? FormatType.Numeric : FormatType.DateTime;
            column.DisplayFormat.FormatString = format;
        }
        if (summary != SummaryItemType.None)
        {
            column.Summary.Add(summary, field, summary == SummaryItemType.Sum ? "{0:n2}" : "{0:n0}");
        }

        return column;
    }

    private Control CreateOfficesTab()
    {
        ConfigureToolbarButton(officeRefresh, "officeRefresh", "OfficeRefresh", "Ready");
        ConfigureToolbarButton(officeShowCustomers, "officeShowCustomers", "OfficeShowCustomers", "Customers");
        officeRefresh.Click += async (_, _) => await RefreshOfficesAsync();
        officeShowCustomers.Click += ShowCustomersForSelectedOffice;
        ConfigureGrid(officeGrid, officeView, "officeGrid", allowGroup: false);
        AddColumn(officeView, nameof(OfficeSummary.OfficeCode), "DatabaseOfficeCode");
        AddColumn(officeView, nameof(OfficeSummary.OfficeDescription), "DatabaseOffice").Width = 220;
        AddColumn(officeView, nameof(OfficeSummary.CustomerCount), "OfficeCustomerCount", "n0", SummaryItemType.Sum).Summary[0].DisplayFormat = "{0:n0}";
        AddColumn(officeView, nameof(OfficeSummary.WithPhoneCount), "OfficeWithPhoneCount", "n0", SummaryItemType.Sum).Summary[0].DisplayFormat = "{0:n0}";
        AddColumn(officeView, nameof(OfficeSummary.DebtorCount), "OfficeDebtorCount", "n0", SummaryItemType.Sum).Summary[0].DisplayFormat = "{0:n0}";
        AddColumn(officeView, nameof(OfficeSummary.TotalDebt), "OfficeTotalDebt", "n2", SummaryItemType.Sum);
        AddColumn(officeView, nameof(OfficeSummary.LegalCount), "OfficeLegalCount", "n0", SummaryItemType.Sum).Summary[0].DisplayFormat = "{0:n0}";
        AddColumn(officeView, nameof(OfficeSummary.SentCount), "OfficeSentCount", "n0", SummaryItemType.Sum).Summary[0].DisplayFormat = "{0:n0}";
        AddColumn(officeView, nameof(OfficeSummary.LastSentDate), "OfficeLastSentDate", "dd.MM.yyyy");
        officeView.DoubleClick += ShowCustomersForSelectedOffice;
        officeView.FocusedRowChanged += (_, _) => UpdateOfficeControls();

        return CreateTabLayout("Offices", CreateToolbar(officeRefresh, officeShowCustomers), officeGrid);
    }

    private void AddCustomerDateColumns(GridView view)
    {
        AddColumn(view, nameof(SmsRecipient.LastPaymentDate), "CustomerLastPaymentDate", "dd.MM.yyyy");
        AddColumn(view, nameof(SmsRecipient.DueDate), "CustomerDueDate", "dd.MM.yyyy");
        AddColumn(view, nameof(SmsRecipient.ReferenceDate), "CustomerControlDate", "dd.MM.yyyy")
            .ToolTip = GetText("CustomerDateRule");
        AddColumn(view, nameof(SmsRecipient.RecordedPaymentDate), "CustomerRecordedPaymentDate", "dd.MM.yyyy");
        AddColumn(view, nameof(SmsRecipient.PaymentDateChanged), "CustomerPaymentChanged");
        AddColumn(view, nameof(SmsRecipient.LastReminderDate), "CustomerLastReminderDate", "dd.MM.yyyy");
        AddColumn(view, nameof(SmsRecipient.ReminderReferenceDate), "CustomerReminderReferenceDate", "dd.MM.yyyy");
        AddColumn(view, nameof(SmsRecipient.HasMessageStage), "CustomerMessageCheck").ToolTip = GetText("CustomerDateRule");
        AddColumn(view, nameof(SmsRecipient.NeedsReminderCheck), "CustomerReminderCheck").ToolTip = GetText("CustomerDateRule");
    }

    private Control CreateCustomersTab()
    {
        customerOffice.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
        customerCategory.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
        customerCategory.Properties.Items.AddRange(MessageCategory.All.ToArray());
        customerCategory.SelectedIndex = 0;
        customerCategory.SelectedIndexChanged += CustomerCategoryChanged;
        customerSearch.Properties.MaxLength = 100;
        customerSearch.Properties.NullValuePrompt = GetText("DatabaseSearch");
        customerRequirePhone.Text = GetText("CustomerRequirePhone");
        customerRequirePhone.Checked = true;
        customerRequirePhone.Margin = new Padding(3, 7, 12, 3);
        ConfigureToolbarButton(customerList, "customerList", "DatabaseRead", "Contacts");
        ConfigureToolbarButton(customerPrepare, "customerPrepare", "DatabasePrepare", "NewSms");
        customerList.Click += async (_, _) => await LoadCustomersAsync();
        customerPrepare.Click += PrepareCustomerDraft;

        ConfigureGrid(customerGrid, customerView, "customerGrid", checkBoxSelect: true);
        AddColumn(customerView, nameof(SmsRecipient.CustomerCode), "DatabaseCustomerCode", summary: SummaryItemType.Count);
        AddColumn(customerView, nameof(SmsRecipient.FullName), "DatabaseFullName").Width = 200;
        AddColumn(customerView, nameof(SmsRecipient.OfficeDescription), "DatabaseOffice").Width = 160;
        AddColumn(customerView, nameof(SmsRecipient.Phone), "DatabasePhone");
        AddColumn(customerView, nameof(SmsRecipient.PhoneCount), "CustomerPhoneCount", "n0").Width = 70;
        AddColumn(customerView, nameof(SmsRecipient.Debt), "DatabaseDebt", "n2", SummaryItemType.Sum);
        AddCustomerDateColumns(customerView);
        AddColumn(customerView, nameof(SmsRecipient.DaysOverdue), "CustomerDaysOverdue", "n0").Width = 80;
        AddColumn(customerView, nameof(SmsRecipient.DueBucket), "CustomerDueBucket").Width = 140;
        AddColumn(customerView, nameof(SmsRecipient.Stage), "CustomerStage").Width = 70;
        AddColumn(customerView, nameof(SmsRecipient.LastSentDate), "OfficeLastSentDate", "dd.MM.yyyy");
        AddColumn(customerView, nameof(SmsRecipient.SentCount), "OfficeSentCount", "n0").Width = 80;
        AddColumn(customerView, nameof(SmsRecipient.LegalFollowUp), "CustomerLegal").Width = 80;
        AddColumn(customerView, nameof(SmsRecipient.BirthDate), "CustomerBirthDate", "dd.MM.yyyy");
        AddColumn(customerView, nameof(SmsRecipient.DaysToBirthday), "CustomerDaysToBirthday", "n0").Width = 90;
        AddColumn(customerView, nameof(SmsRecipient.OfficeCode), "DatabaseOfficeCode").Visible = false;
        customerView.RowStyle += CustomerRowStyle;
        customerView.SelectionChanged += (_, _) => { customerPreview.Text = ""; UpdateOfficeControls(); };

        SplitContainerControl split = new() { Dock = DockStyle.Fill, Horizontal = false, SplitterPosition = 320, MinimumSize = new Size(0, 420) };
        split.Panel1.Controls.Add(customerGrid);
        TableLayoutPanel draft = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        draft.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        draft.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        draft.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        draft.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        LabelControl templateLabel = new() { Text = GetText("DatabaseTemplate"), Margin = new Padding(3, 6, 3, 3) };
        templateLabel.ToolTip = GetText("DatabaseTemplateHelp");
        LabelControl previewLabel = new() { Text = GetText("DatabasePreview"), Margin = new Padding(3, 6, 3, 3) };
        customerTemplate.Name = "customerTemplate";
        customerTemplate.Dock = DockStyle.Fill;
        customerTemplate.Properties.MaxLength = 1000;
        customerTemplate.ToolTip = GetText("DatabaseTemplateHelp");
        customerTemplate.AccessibleName = GetText("DatabaseTemplate");
        customerTemplate.EditValueChanged += (_, _) => { if (!templatePopulating) { customerTemplateEdited = true; customerPreview.Text = ""; } };
        customerPreview.Name = "customerPreview";
        customerPreview.Dock = DockStyle.Fill;
        customerPreview.Properties.ReadOnly = true;
        customerPreview.AccessibleName = GetText("DatabasePreview");
        draft.Controls.Add(templateLabel, 0, 0);
        draft.Controls.Add(previewLabel, 1, 0);
        draft.Controls.Add(customerTemplate, 0, 1);
        draft.Controls.Add(customerPreview, 1, 1);
        split.Panel2.Controls.Add(draft);

        return CreateTabLayout("Customers",
            CreateToolbar(Labeled("DatabaseOffice", customerOffice, 240), Labeled("CustomerCategory", customerCategory, 220),
                Labeled("DatabaseSearch", customerSearch, 180), customerRequirePhone, customerList, customerPrepare),
            split);
    }

    private Control CreatePhoneBookTab()
    {
        phoneOffice.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
        phoneSearch.Properties.MaxLength = 100;
        ConfigureToolbarButton(phoneList, "phoneList", "DatabaseRead", "Contacts");
        ConfigureToolbarButton(phoneExport, "phoneExport", "PhoneExport", "ImportContacts");
        phoneList.Click += async (_, _) => await LoadPhonesAsync();
        phoneExport.Click += ExportPhones;
        ConfigureGrid(phoneGrid, phoneView, "phoneGrid");
        AddColumn(phoneView, nameof(PhoneEntry.OfficeDescription), "DatabaseOffice").GroupIndex = 0;
        AddColumn(phoneView, nameof(PhoneEntry.OfficeCode), "DatabaseOfficeCode").Width = 80;
        AddColumn(phoneView, nameof(PhoneEntry.CustomerCode), "DatabaseCustomerCode");
        AddColumn(phoneView, nameof(PhoneEntry.FullName), "DatabaseFullName").Width = 220;
        AddColumn(phoneView, nameof(PhoneEntry.Description), "PhoneDescription").Width = 140;
        AddColumn(phoneView, nameof(PhoneEntry.Phone), "DatabasePhone", summary: SummaryItemType.Count);
        phoneView.GroupSummary.Add(SummaryItemType.Count, nameof(PhoneEntry.Phone), null, GetText("PhoneGroupCount"));
        phoneView.OptionsBehavior.AutoExpandAllGroups = true;

        return CreateTabLayout("PhoneBook",
            CreateToolbar(Labeled("DatabaseOffice", phoneOffice, 240), Labeled("DatabaseSearch", phoneSearch, 180), phoneList, phoneExport),
            phoneGrid);
    }

    private Control CreateSentSmsTab()
    {
        sentOffice.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
        sentFrom.Properties.AllowNullInput = sentTo.Properties.AllowNullInput = DefaultBoolean.True;
        sentFrom.EditValue = DateTime.Today.AddDays(-30);
        sentSearch.Properties.MaxLength = 100;
        ConfigureToolbarButton(sentList, "sentList", "DatabaseRead", "SentMessages");
        ConfigureToolbarButton(sentClearFilter, "sentClearFilter", "SentClearFilter", "Ready");
        sentList.Click += async (_, _) => await LoadSentMessagesAsync();
        sentClearFilter.Click += (_, _) => { sentDetailView.ActiveFilterCriteria = null; tabStatus["SentSms"].Text = IntegrationText.Format("SentDetailCount", sentDetailView.RowCount); };

        ConfigureGrid(sentSummaryGrid, sentSummaryView, "sentSummaryGrid", allowGroup: false);
        sentSummaryView.OptionsView.ShowAutoFilterRow = false;
        sentSummaryView.OptionsFind.AlwaysVisible = false;
        AddColumn(sentSummaryView, nameof(SentMessageSummary.SentDate), "SentDate", "dd.MM.yyyy");
        AddColumn(sentSummaryView, nameof(SentMessageSummary.OfficeCode), "DatabaseOfficeCode").Width = 80;
        AddColumn(sentSummaryView, nameof(SentMessageSummary.OfficeDescription), "DatabaseOffice").Width = 220;
        AddColumn(sentSummaryView, nameof(SentMessageSummary.Count), "SentCount", "n0", SummaryItemType.Sum).Summary[0].DisplayFormat = "{0:n0}";
        sentSummaryView.FocusedRowChanged += SentSummaryRowChanged;

        ConfigureGrid(sentDetailGrid, sentDetailView, "sentDetailGrid");
        AddColumn(sentDetailView, nameof(SentMessage.SentDate), "SentDate", "dd.MM.yyyy");
        AddColumn(sentDetailView, nameof(SentMessage.SentTime), "SentTime").Width = 80;
        AddColumn(sentDetailView, nameof(SentMessage.OfficeDescription), "DatabaseOffice").Width = 160;
        AddColumn(sentDetailView, nameof(SentMessage.CustomerCode), "DatabaseCustomerCode", summary: SummaryItemType.Count);
        AddColumn(sentDetailView, nameof(SentMessage.FullName), "DatabaseFullName").Width = 200;
        AddColumn(sentDetailView, nameof(SentMessage.Phone), "DatabasePhone");
        AddColumn(sentDetailView, nameof(SentMessage.Debt), "DatabaseDebt", "n2", SummaryItemType.Sum);
        AddColumn(sentDetailView, nameof(SentMessage.ReferenceDate), "DatabaseReferenceDate", "dd.MM.yyyy");
        AddColumn(sentDetailView, nameof(SentMessage.DaysOverdue), "CustomerDaysOverdue", "n0").Width = 80;
        AddColumn(sentDetailView, nameof(SentMessage.Category), "CustomerCategory").Width = 150;
        AddColumn(sentDetailView, nameof(SentMessage.SentId), "SentId").Width = 140;
        AddColumn(sentDetailView, nameof(SentMessage.OfficeCode), "DatabaseOfficeCode").Visible = false;

        SplitContainerControl split = new() { Dock = DockStyle.Fill, Horizontal = false, SplitterPosition = 180, MinimumSize = new Size(0, 420) };
        split.Panel1.Controls.Add(sentSummaryGrid);
        split.Panel2.Controls.Add(sentDetailGrid);

        return CreateTabLayout("SentSms",
            CreateToolbar(Labeled("DatabaseOffice", sentOffice, 240), Labeled("SentFrom", sentFrom, 120), Labeled("SentTo", sentTo, 120),
                Labeled("DatabaseSearch", sentSearch, 180), sentList, sentClearFilter),
            split);
    }

    private Control CreateTemplatesTab()
    {
        templateList.Name = "templateList";
        templateList.Dock = DockStyle.Fill;
        templateList.Items.AddRange(MessageCategory.WithTemplates.ToArray());
        templateList.SelectedIndexChanged += TemplateSelectionChanged;
        templateEditor.Name = "templateEditor";
        templateEditor.Dock = DockStyle.Fill;
        templateEditor.Properties.MaxLength = 1000;
        templateEditor.AccessibleName = GetText("DatabaseTemplate");
        templateEditor.EditValueChanged += TemplateTextChanged;
        ConfigureToolbarButton(templateSave, "templateSave", "TemplateSave", "Ready");
        ConfigureToolbarButton(templateReset, "templateReset", "TemplateReset", "Templates");
        templateSave.Click += async (_, _) => await SaveTemplatesAsync();
        templateReset.Click += ResetTemplate;
        LabelControl help = new()
        {
            Text = IntegrationText.Format("TemplateHelp", string.Join(", ", SmsDraftService.Placeholders)),
            AutoSizeMode = LabelAutoSizeMode.Vertical, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(3, 6, 3, 6)
        };

        SplitContainerControl split = new() { Dock = DockStyle.Fill, Horizontal = true, SplitterPosition = 260, MinimumSize = new Size(0, 300) };
        split.Panel1.Controls.Add(templateList);
        split.Panel2.Controls.Add(templateEditor);

        Control layout = CreateTabLayout("Templates", help, CreateToolbar(templateSave, templateReset), split);
        tabStatus["Templates"].Text = GetText("TemplateReady");

        return layout;
    }

    private void OfficesButtonClick(object sender, ItemClickEventArgs e)
    {
        if (e.Item.Tag is string key)
        {
            ShowModule(key);
        }
    }

    private void ShowOffices(string pageKey)
    {
        Control page = officePages[pageKey];
        HideIntegrations();
        HideDatabase();
        quickActions.Visible = false;
        officesPanel.SuspendLayout();
        try
        {
            foreach (Control otherPage in officePages.Values)
            {
                if (otherPage != page) otherPage.Visible = false;
            }
            activeOfficePageKey = pageKey;
            ApplyOfficePageTexts(pageKey);
            page.Visible = true;
            page.BringToFront();
            officesPanel.Visible = true;
            officesPanel.BringToFront();
        }
        finally
        {
            officesPanel.ResumeLayout(true);
        }
    }

    private void HideOffices()
    {
        officeOperation?.Cancel();
        officesPanel.Visible = false;
    }

    private void ApplyOfficePageTexts(string key)
    {
        sectionTitle.Text = GetText(key);
        sectionDescription.Text = GetText(key + "Description");
        statusText.Caption = connectedDatabase is null ? GetText("DatabaseDisconnected") : GetText("DatabaseConnectedStatus");
    }

    private async void OfficePageVisibleChangedAsync(object? sender, EventArgs e)
    {
        if (sender is not Control { Visible: true, Tag: string key } page || IsDisposed)
        {
            return;
        }
        try
        {
            await EnsureTemplatesLoadedAsync();
            await officeOperationCompletion;
            if (!IsDisposed && page.Visible && activeOfficePageKey == key)
            {
                await AutoLoadCurrentPageAsync();
            }
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed && page.Visible)
            {
                tabStatus[key].Text = GetText("DatabaseCancelled");
            }
        }
    }

    // Loads only the menu's current screen, preserving data when navigating back.
    private async Task AutoLoadCurrentPageAsync()
    {
        if (!officesPanel.Visible || connectedDatabase is null || activeOfficePageKey is not string key || tabLoaded[key])
        {
            return;
        }
        switch (key)
        {
            case "Offices": await RefreshOfficesAsync(); break;
            case "NewSms": await LoadComposerCustomersAsync(singleSms); break;
            case "BulkSms": await LoadComposerCustomersAsync(bulkSms); break;
            case "Customers": await LoadCustomersAsync(); break;
            case "PhoneBook": await LoadPhonesAsync(); break;
            case "SentSms": await LoadSentMessagesAsync(); break;
        }
    }

    private void OnDatabaseConnected()
    {
        foreach (string tab in officePages.Keys.Where(key => key != "Templates"))
        {
            tabLoaded[tab] = false;
            tabStatus[tab].Text = GetText(connectedDatabase is null ? "OfficeConnectFirst" : "OfficeReady");
        }
        OfficeOption[] options = [new OfficeOption("", GetText("DatabaseAllOffices")),
            .. officeSummaries.Select(office => new OfficeOption(office.OfficeCode, office.OfficeDescription))];
        foreach (ComboBoxEdit combo in new[] { customerOffice, phoneOffice, sentOffice, singleSms.Office, bulkSms.Office })
        {
            combo.Properties.Items.Clear();
            combo.Properties.Items.AddRange(options);
            combo.SelectedIndex = 0;
        }
        string defaultOffice = connectedDatabase?.OfficeCode ?? "";
        if (defaultOffice.Length > 0)
        {
            int index = Array.FindIndex(options, option => option.Code == defaultOffice);
            if (index > 0)
            {
                customerOffice.SelectedIndex = phoneOffice.SelectedIndex = sentOffice.SelectedIndex
                    = singleSms.Office.SelectedIndex = bulkSms.Office.SelectedIndex = index;
            }
        }
        officeGrid.DataSource = officeSummaries.ToList();
        tabLoaded["Offices"] = connectedDatabase is not null;
        ClearComposerCustomers(singleSms);
        ClearComposerCustomers(bulkSms);
        customerGrid.DataSource = null;
        phoneGrid.DataSource = null;
        sentSummaryGrid.DataSource = null;
        sentDetailGrid.DataSource = null;
        customerPreview.Text = "";
        if (connectedDatabase is not null)
        {
            tabStatus["Offices"].Text = IntegrationText.Format("OfficeCount", officeSummaries.Count, officeSummaries.Sum(office => office.CustomerCount));
        }
        if (officesPanel.Visible && activeOfficePageKey is string key)
        {
            ApplyOfficePageTexts(key);
        }
        UpdateOfficeControls();
    }

    private Task EnsureTemplatesLoadedAsync() => templateLoading ??= LoadTemplatesAsync();

    private async Task LoadTemplatesAsync()
    {
        if (templatesLoaded)
        {
            return;
        }
        try
        {
            IReadOnlyDictionary<string, string> loaded = await templateStore.LoadAsync();
            if (IsDisposed) return;
            foreach ((string key, string template) in loaded)
            {
                templates[key] = template;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            if (IsDisposed) return;
            foreach (MessageCategory category in MessageCategory.WithTemplates)
            {
                templates[category.Key] = MessageTemplateStore.DefaultFor(category);
            }
            tabStatus["Templates"].Text = GetText("TemplateLoadError");
        }
        templatesLoaded = true;
        if (templateList.SelectedIndex < 0 && templateList.ItemCount > 0)
        {
            templateList.SelectedIndex = 0;
        }
        else
        {
            TemplateSelectionChanged(null, EventArgs.Empty);
        }
        CustomerCategoryChanged(null, EventArgs.Empty);
        UpdateComposerControls(singleSms);
        UpdateComposerControls(bulkSms);
    }

    private async Task<bool> RunOfficeOperationAsync(string tabKey, Func<CancellationToken, Task> work)
    {
        if (officeBusy || IsDisposed)
        {
            return false;
        }
        if (connectedDatabase is null)
        {
            tabStatus[tabKey].Text = GetText("OfficeConnectFirst");
            return false;
        }
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(90));
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        officeOperationCompletion = completion.Task;
        officeOperation = cancellation;
        officeBusy = true;
        UpdateOfficeControls();
        tabStatus[tabKey].Text = GetText("IntegrationBusy");
        try
        {
            await work(cancellation.Token);
            return true;
        }
        catch (OperationCanceledException) { tabStatus[tabKey].Text = GetText("DatabaseCancelled"); }
        catch (SqlException exception) { tabStatus[tabKey].Text = IntegrationText.Format("DatabaseSqlError", exception.Number, FirstLine(exception.Message)); }
        catch (InvalidCastException) { tabStatus[tabKey].Text = GetText("DatabaseSchemaError"); }
        catch (FormatException) { tabStatus[tabKey].Text = GetText("DatabaseSchemaError"); }
        catch (OverflowException) { tabStatus[tabKey].Text = GetText("DatabaseSchemaError"); }
        catch (InvalidDataException exception) { tabStatus[tabKey].Text = exception.Message; }
        catch (ArgumentException exception) { tabStatus[tabKey].Text = exception.Message; }
        catch (InvalidOperationException) { tabStatus[tabKey].Text = GetText("DatabaseSchemaError"); }
        catch (IOException) { tabStatus[tabKey].Text = GetText("DatabaseStorageError"); }
        catch (UnauthorizedAccessException) { tabStatus[tabKey].Text = GetText("DatabaseStorageError"); }
        catch (CryptographicException) { tabStatus[tabKey].Text = GetText("IntegrationDecryptError"); }
        finally
        {
            if (officeOperation == cancellation) officeOperation = null;
            officeBusy = false;
            try
            {
                if (!IsDisposed) UpdateOfficeControls();
            }
            finally
            {
                completion.TrySetResult();
            }
        }

        return false;
    }

    private static string SelectedOfficeCode(ComboBoxEdit combo) => (combo.SelectedItem as OfficeOption)?.Code ?? "";

    private async Task RefreshOfficesAsync()
    {
        DatabaseSettings? settings = connectedDatabase;
        await RunOfficeOperationAsync("Offices", async token =>
        {
            IReadOnlyList<OfficeSummary> offices = await repository.ReadOfficesAsync(settings!, token);
            if (IsDisposed || connectedDatabase != settings) return;
            officeSummaries = offices;
            OnDatabaseConnected();
        });
    }

    private void ShowCustomersForSelectedOffice(object? sender, EventArgs e)
    {
        if (officeView.GetFocusedRow() is not OfficeSummary office)
        {
            return;
        }
        int index = customerOffice.Properties.Items.Cast<OfficeOption>().ToList().FindIndex(option => option.Code == office.OfficeCode);
        customerOffice.SelectedIndex = Math.Max(index, 0);
        tabLoaded["Customers"] = false;
        ShowOffices("Customers");
    }

    private void CustomerCategoryChanged(object? sender, EventArgs e)
    {
        if (customerCategory.SelectedItem is not MessageCategory category)
        {
            return;
        }
        string key = templates.ContainsKey(category.Key) ? category.Key : "AllCustomers";
        if (!customerTemplateEdited || string.IsNullOrWhiteSpace(customerTemplate.Text))
        {
            templatePopulating = true;
            try
            {
                customerTemplate.Text = templates.TryGetValue(key, out string? template) ? template : MessageTemplateStore.DefaultFor(category);
                customerTemplateEdited = false;
            }
            finally
            {
                templatePopulating = false;
            }
        }
        customerPreview.Text = "";
    }

    private async Task LoadCustomersAsync()
    {
        DatabaseSettings? settings = connectedDatabase;
        await RunOfficeOperationAsync("Customers", async token =>
        {
            MessageCategory category = customerCategory.SelectedItem as MessageCategory ?? MessageCategory.All[0];
            CustomerFilter filter = new(SelectedOfficeCode(customerOffice), customerSearch.Text, category, customerRequirePhone.Checked,
                screenSettings?.NebimDatabase ?? "");
            customerGrid.DataSource = null;
            customerPreview.Text = "";
            Page<SmsRecipient> page = await repository.ReadCustomersAsync(settings!, filter, token);
            if (IsDisposed || connectedDatabase != settings) return;
            customerGrid.DataSource = page.Items.ToList();
            customerView.Columns[nameof(SmsRecipient.BirthDate)].Visible = category.Kind == MessageCategoryKind.Birthday;
            customerView.Columns[nameof(SmsRecipient.DaysToBirthday)].Visible = category.Kind == MessageCategoryKind.Birthday;
            customerView.BestFitColumns();
            tabLoaded["Customers"] = true;
            tabStatus["Customers"].Text = IntegrationText.Format(page.HasMore ? "DatabaseResultsLimited" : "DatabaseResults", page.Items.Count,
                category.Name, page.Items.Sum(customer => customer.Debt));
        });
    }

    private void CustomerRowStyle(object? sender, RowStyleEventArgs e)
    {
        if (customerView.GetRow(e.RowHandle) is not SmsRecipient customer || customer.Debt <= 0)
        {
            return;
        }
        if (customer.LegalFollowUp)
        {
            e.Appearance.ForeColor = Color.Gray;
        }
        else if (customer.DaysOverdue > 60)
        {
            e.Appearance.BackColor = Color.FromArgb(255, 228, 225);
        }
        else if (customer.DaysOverdue > 20)
        {
            e.Appearance.BackColor = Color.FromArgb(255, 244, 214);
        }
    }

    private void PrepareCustomerDraft(object? sender, EventArgs e)
    {
        try
        {
            SmsRecipient[] selected = customerView.GetSelectedRows().Where(handle => handle >= 0)
                .Select(handle => customerView.GetRow(handle)).OfType<SmsRecipient>().ToArray();
            SmsDraftBatch batch = SmsDraftService.Prepare(selected, customerTemplate.Text);
            customerPreview.Text = string.Join(Environment.NewLine + Environment.NewLine,
                batch.Messages.Select(message => $"{message.Phone}: {message.Text}"));
            tabStatus["Customers"].Text = IntegrationText.Format("DatabaseDraftSummary", batch.Messages.Count,
                batch.InvalidPhoneCount, batch.ConflictingPhoneCount);
        }
        catch (ArgumentException exception) { tabStatus["Customers"].Text = exception.Message; }
    }

    private async Task LoadPhonesAsync()
    {
        DatabaseSettings? settings = connectedDatabase;
        await RunOfficeOperationAsync("PhoneBook", async token =>
        {
            phoneGrid.DataSource = null;
            Page<PhoneEntry> page = await repository.ReadPhonesAsync(settings!, SelectedOfficeCode(phoneOffice), phoneSearch.Text, token);
            if (IsDisposed || connectedDatabase != settings) return;
            phoneGrid.DataSource = page.Items.ToList();
            phoneView.BestFitColumns();
            tabLoaded["PhoneBook"] = true;
            tabStatus["PhoneBook"].Text = IntegrationText.Format(page.HasMore ? "PhoneResultsLimited" : "PhoneResults", page.Items.Count,
                page.Items.Select(phone => phone.CustomerCode).Distinct(StringComparer.Ordinal).Count());
        });
    }

    private void ExportPhones(object? sender, EventArgs e)
    {
        if (phoneGrid.DataSource is not List<PhoneEntry> phones || phones.Count == 0)
        {
            tabStatus["PhoneBook"].Text = GetText("PhoneExportEmpty");
            return;
        }
        using SaveFileDialog dialog = new()
        {
            Title = GetText("PhoneExport"), Filter = GetText("PhoneExportFilter"),
            FileName = $"telefonlar_{SelectedOfficeCode(phoneOffice) switch { "" => "tum", string code => code }}_{DateTime.Today:yyyyMMdd}.csv"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        try
        {
            StringBuilder csv = new();
            csv.AppendLine(string.Join(';', GetText("DatabaseOfficeCode"), GetText("DatabaseOffice"), GetText("DatabaseCustomerCode"),
                GetText("DatabaseFullName"), GetText("PhoneDescription"), GetText("DatabasePhone")));
            foreach (PhoneEntry phone in phones)
            {
                csv.AppendLine(string.Join(';', Csv(phone.OfficeCode), Csv(phone.OfficeDescription), Csv(phone.CustomerCode),
                    Csv(phone.FullName), Csv(phone.Description), Csv(phone.Phone)));
            }
            File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
            tabStatus["PhoneBook"].Text = IntegrationText.Format("PhoneExported", phones.Count, Path.GetFileName(dialog.FileName));
        }
        catch (IOException) { tabStatus["PhoneBook"].Text = GetText("DatabaseStorageError"); }
        catch (UnauthorizedAccessException) { tabStatus["PhoneBook"].Text = GetText("DatabaseStorageError"); }
    }

    // Prefix formula-like values so spreadsheet software treats exported cells as text.
    private static string Csv(string value)
    {
        string text = value.Replace("\r", " ").Replace("\n", " ");
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@')
        {
            text = "'" + text;
        }

        return text.Contains(';') || text.Contains('"') ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }

    private async Task LoadSentMessagesAsync()
    {
        DatabaseSettings? settings = connectedDatabase;
        await RunOfficeOperationAsync("SentSms", async token =>
        {
            SentMessageFilter filter = new(SelectedOfficeCode(sentOffice), sentSearch.Text,
                sentFrom.EditValue is DateTime from ? from : null, sentTo.EditValue is DateTime to ? to : null);
            sentSummaryGrid.DataSource = null;
            sentDetailGrid.DataSource = null;
            sentDetailView.ActiveFilterCriteria = null;
            IReadOnlyList<SentMessageSummary> summary = await repository.ReadSentSummaryAsync(settings!, filter, token);
            Page<SentMessage> page = await repository.ReadSentMessagesAsync(settings!, filter, token);
            if (IsDisposed || connectedDatabase != settings) return;
            sentSummaryGrid.DataSource = summary.ToList();
            sentDetailGrid.DataSource = page.Items.ToList();
            sentSummaryView.BestFitColumns();
            sentDetailView.BestFitColumns();
            tabLoaded["SentSms"] = true;
            tabStatus["SentSms"].Text = IntegrationText.Format(page.HasMore ? "SentResultsLimited" : "SentResults", page.Items.Count,
                summary.Sum(row => row.Count), summary.Count);
        });
    }

    private void SentSummaryRowChanged(object? sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e)
    {
        if (sentSummaryView.GetFocusedRow() is not SentMessageSummary row || sentDetailGrid.DataSource is null)
        {
            return;
        }
        sentDetailView.ActiveFilterCriteria = row.SentDate is null
            ? new BinaryOperator(nameof(SentMessage.OfficeCode), row.OfficeCode)
            : CriteriaOperator.And(new BinaryOperator(nameof(SentMessage.OfficeCode), row.OfficeCode),
                new BinaryOperator(nameof(SentMessage.SentDate), row.SentDate.Value.Date));
        tabStatus["SentSms"].Text = IntegrationText.Format("SentDetailCount", sentDetailView.RowCount);
    }

    private void TemplateSelectionChanged(object? sender, EventArgs e)
    {
        if (templateList.SelectedItem is not MessageCategory category)
        {
            return;
        }
        templatePopulating = true;
        try
        {
            templateEditor.Text = templates.TryGetValue(category.Key, out string? template) ? template : MessageTemplateStore.DefaultFor(category);
        }
        finally
        {
            templatePopulating = false;
        }
    }

    private void TemplateTextChanged(object? sender, EventArgs e)
    {
        if (templatePopulating || templateList.SelectedItem is not MessageCategory category)
        {
            return;
        }
        templates[category.Key] = templateEditor.Text;
        tabStatus["Templates"].Text = GetText("IntegrationUnsaved");
        if (customerCategory.SelectedItem is MessageCategory current && current.Key == category.Key && !customerTemplateEdited)
        {
            templatePopulating = true;
            try { customerTemplate.Text = templateEditor.Text; }
            finally { templatePopulating = false; }
        }
    }

    private void ResetTemplate(object? sender, EventArgs e)
    {
        if (templateList.SelectedItem is MessageCategory category)
        {
            templateEditor.Text = MessageTemplateStore.DefaultFor(category);
        }
    }

    private async Task SaveTemplatesAsync()
    {
        try
        {
            await templateStore.SaveAsync(templates);
            tabStatus["Templates"].Text = GetText("TemplateSaved");
            customerTemplateEdited = false;
            CustomerCategoryChanged(null, EventArgs.Empty);
        }
        catch (ArgumentException exception) { tabStatus["Templates"].Text = exception.Message; }
        catch (IOException) { tabStatus["Templates"].Text = GetText("DatabaseStorageError"); }
        catch (UnauthorizedAccessException) { tabStatus["Templates"].Text = GetText("DatabaseStorageError"); }
    }

    private void UpdateOfficeControls()
    {
        bool ready = !officeBusy && connectedDatabase is not null;
        officeRefresh.Enabled = customerList.Enabled = phoneList.Enabled = sentList.Enabled = ready;
        officeShowCustomers.Enabled = ready && officeView.GetFocusedRow() is OfficeSummary;
        customerPrepare.Enabled = !officeBusy && customerView.SelectedRowsCount > 0;
        phoneExport.Enabled = !officeBusy && phoneGrid.DataSource is List<PhoneEntry> { Count: > 0 };
        sentClearFilter.Enabled = !officeBusy && sentDetailGrid.DataSource is not null;
        templateSave.Enabled = templateReset.Enabled = !officeBusy;
        foreach (Control control in new Control[] { customerOffice, customerCategory, customerSearch, customerRequirePhone, phoneOffice, phoneSearch, sentOffice, sentFrom, sentTo, sentSearch })
        {
            control.Enabled = !officeBusy;
        }
        UpdateComposerControls(singleSms);
        UpdateComposerControls(bulkSms);
    }
}
