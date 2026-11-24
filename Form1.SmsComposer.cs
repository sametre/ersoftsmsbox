using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using ERSoftSMSBox.Data;
using ERSoftSMSBox.Integrations;
using ERSoftSMSBox.Messaging;

namespace ERSoftSMSBox;

public partial class Form1
{
    private readonly SmsComposer singleSms = new("NewSms", false);
    private readonly SmsComposer bulkSms = new("BulkSms", true);

    private sealed class SmsComposer(string key, bool multiple)
    {
        internal string Key { get; } = key;
        internal bool Multiple { get; } = multiple;
        internal ComboBoxEdit Office { get; } = new();
        internal ComboBoxEdit Category { get; } = new();
        internal ComboBoxEdit Template { get; } = new();
        internal ComboBoxEdit PresetTemplate { get; } = new();
        internal TextEdit Search { get; } = new();
        internal GridControl Grid { get; } = new();
        internal GridView View { get; } = new();
        internal MemoEdit Message { get; } = new();
        internal MemoEdit Preview { get; } = new();
        internal LabelControl Recipient { get; } = new();
        internal LabelControl Counter { get; } = new();
        internal SimpleButton List { get; } = new();
        internal SimpleButton Prepare { get; } = new();
        internal SimpleButton ApplyTemplate { get; } = new();
        internal SimpleButton SelectAll { get; } = new();
        internal SimpleButton DeselectAll { get; } = new();
        internal System.Windows.Forms.Timer SearchTimer { get; } = new() { Interval = 400 };
    }

    private Control CreateSmsComposer(SmsComposer composer)
    {
        string prefix = composer.Key;

        // Filter bar
        composer.Office.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
        composer.Category.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
        composer.Category.Properties.Items.AddRange(MessageCategory.All.ToArray());
        composer.Category.SelectedIndex = 0;
        composer.Search.Properties.MaxLength = 100;
        ConfigureToolbarButton(composer.List, prefix + "List", "DatabaseRead", "Contacts");
        composer.List.Click += async (_, _) => await LoadComposerCustomersAsync(composer);

        Control office = Labeled("DatabaseOffice", composer.Office, 220);
        Control search = Labeled("DatabaseSearch", composer.Search, 180);
        Control category = Labeled("CustomerCategory", composer.Category, 200);
        composer.Office.Name = prefix + "Office";
        composer.Search.Name = prefix + "Search";
        composer.Category.Name = prefix + "Category";
        category.Visible = composer.Multiple;

        FlowLayoutPanel filters;
        if (composer.Multiple)
        {
            ConfigureToolbarButton(composer.SelectAll, prefix + "SelectAll", "BulkSelectAll", "Contacts");
            ConfigureToolbarButton(composer.DeselectAll, prefix + "DeselectAll", "BulkDeselectAll", "Ready");
            composer.SelectAll.Click += (_, _) => { composer.View.SelectAll(); InvalidateComposerPreview(composer); };
            composer.DeselectAll.Click += (_, _) => { composer.View.ClearSelection(); InvalidateComposerPreview(composer); };
            filters = CreateToolbar(office, category, search, composer.List, composer.SelectAll, composer.DeselectAll);
        }
        else
        {
            filters = CreateToolbar(office, search, composer.List);
        }

        // Grid
        ConfigureGrid(composer.Grid, composer.View, prefix + "Grid", checkBoxSelect: composer.Multiple, allowGroup: false);
        composer.Grid.AccessibleName = GetText("Customers");
        composer.Grid.AccessibleDescription = GetText(prefix + "Description");
        AddColumn(composer.View, nameof(SmsRecipient.FullName), "DatabaseFullName").Width = 180;
        AddColumn(composer.View, nameof(SmsRecipient.CustomerCode), "DatabaseCustomerCode");
        AddColumn(composer.View, nameof(SmsRecipient.Phone), "DatabasePhone");
        AddColumn(composer.View, nameof(SmsRecipient.OfficeDescription), "DatabaseOffice");
        AddColumn(composer.View, nameof(SmsRecipient.Debt), "DatabaseDebt", "n2");
        AddColumn(composer.View, nameof(SmsRecipient.DaysOverdue), "CustomerDaysOverdue", "n0");
        AddColumn(composer.View, nameof(SmsRecipient.Stage), "CustomerStage");
        AddCustomerDateColumns(composer.View);
        composer.View.FocusedRowChanged += (_, _) => InvalidateComposerPreview(composer);
        composer.View.SelectionChanged += (_, _) => InvalidateComposerPreview(composer);

        // Template row: category templates + apply + preset picker
        composer.Template.Name = prefix + "Template";
        composer.Template.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
        composer.Template.Properties.Items.AddRange(MessageCategory.WithTemplates.ToArray());
        composer.Template.SelectedIndex = 0;
        composer.Template.AccessibleName = GetText("Templates");
        composer.Template.AccessibleDescription = GetText("SmsComposerTemplate");
        composer.Template.Width = 200;
        ConfigureToolbarButton(composer.ApplyTemplate, prefix + "ApplyTemplate", "SmsComposerTemplate", "Templates");
        composer.ApplyTemplate.Click += (_, _) => ApplyComposerTemplate(composer);

        composer.PresetTemplate.Name = prefix + "PresetTemplate";
        composer.PresetTemplate.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
        composer.PresetTemplate.Properties.Items.Add(GetText("SmsComposerPresetsPlaceholder"));
        foreach (SmsPreset preset in SmsPresets.All)
        {
            composer.PresetTemplate.Properties.Items.Add(preset);
        }
        composer.PresetTemplate.SelectedIndex = 0;
        composer.PresetTemplate.AccessibleName = GetText("SmsComposerPresets");
        composer.PresetTemplate.Width = 220;

        LabelControl presetLabel = new() { Text = GetText("SmsComposerPresets"), Margin = new Padding(20, 9, 6, 3) };
        FlowLayoutPanel templateRow = CreateToolbar(composer.Template, composer.ApplyTemplate, presetLabel, composer.PresetTemplate);

        // Quick placeholder insert buttons (one per placeholder)
        SimpleButton btnName = new();
        SimpleButton btnCode = new();
        SimpleButton btnDebt = new();
        SimpleButton btnDays = new();
        SimpleButton btnOffice = new();
        SimpleButton btnBday = new();
        ConfigureToolbarButton(btnName,   prefix + "InsertName",   "SmsComposerInsertName",      "Contacts");
        ConfigureToolbarButton(btnCode,   prefix + "InsertCode",   "SmsComposerInsertCariKodu",  "Contacts");
        ConfigureToolbarButton(btnDebt,   prefix + "InsertDebt",   "SmsComposerInsertBorc",      "Contacts");
        ConfigureToolbarButton(btnDays,   prefix + "InsertDays",   "SmsComposerInsertGun",       "Contacts");
        ConfigureToolbarButton(btnOffice, prefix + "InsertOffice", "SmsComposerInsertOfis",      "Contacts");
        ConfigureToolbarButton(btnBday,   prefix + "InsertBday",   "SmsComposerInsertDogumGunu", "Contacts");
        btnName.Click   += (_, _) => composer.Message.SelectedText = "{AdSoyad}";
        btnCode.Click   += (_, _) => composer.Message.SelectedText = "{CariKodu}";
        btnDebt.Click   += (_, _) => composer.Message.SelectedText = "{Borc}";
        btnDays.Click   += (_, _) => composer.Message.SelectedText = "{Gun}";
        btnOffice.Click += (_, _) => composer.Message.SelectedText = "{Ofis}";
        btnBday.Click   += (_, _) => composer.Message.SelectedText = "{DogumGunu}";
        LabelControl insertLabel = new() { Text = GetText("SmsComposerInsert"), Margin = new Padding(3, 9, 8, 3) };
        FlowLayoutPanel insertRow = CreateToolbar(insertLabel, btnName, btnCode, btnDebt, btnDays, btnOffice, btnBday);

        // Message area
        SimpleButton clear = new();
        ConfigureToolbarButton(clear, prefix + "Clear", "SmsComposerClear", "Ready");
        clear.Click += (_, _) => ClearComposerMessage(composer);
        ConfigureToolbarButton(composer.Prepare, prefix + "Prepare", "SmsComposerPreview", "NewSms");
        composer.Prepare.Click += (_, _) => PrepareComposerDraft(composer);

        composer.Message.Name = prefix + "Message";
        composer.Message.Properties.MaxLength = 1000;
        composer.Message.AccessibleName = GetText("SmsComposerMessage");
        composer.Message.AccessibleDescription = GetText("DatabaseTemplateHelp");
        composer.Message.EditValueChanged += (_, _) => InvalidateComposerPreview(composer);
        composer.Preview.Name = prefix + "Preview";
        composer.Preview.Properties.ReadOnly = true;
        composer.Preview.AccessibleName = GetText("DatabasePreview");
        composer.Preview.AccessibleDescription = GetText("SmsComposerUnavailable");
        composer.Recipient.Name = prefix + "Recipient";
        composer.Recipient.AutoSizeMode = LabelAutoSizeMode.Vertical;
        composer.Counter.Name = prefix + "Counter";
        composer.Counter.AutoSizeMode = LabelAutoSizeMode.Vertical;
        LabelControl notice = new() { Text = GetText("SmsComposerUnavailable"), AutoSizeMode = LabelAutoSizeMode.Vertical };

        // Editor layout: 10 rows
        TableLayoutPanel editor = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 10, Padding = new Padding(8) };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Control[] rows =
        [
            composer.Recipient,                                              // 0 – alıcı bilgisi
            templateRow,                                                     // 1 – şablon + hazır şablon
            insertRow,                                                       // 2 – hızlı ekle butonları
            new LabelControl { Text = GetText("SmsComposerMessage") },      // 3 – "Mesaj metni" başlığı
            composer.Message,                                                // 4 – mesaj alanı (stretch)
            composer.Counter,                                                // 5 – karakter / SMS sayacı
            CreateToolbar(clear, composer.Prepare),                         // 6 – Temizle / Önizle
            new LabelControl { Text = GetText("DatabasePreview") },         // 7 – "Önizleme" başlığı
            composer.Preview,                                                // 8 – önizleme alanı (stretch)
            notice                                                           // 9 – bilgi notu
        ];
        for (int index = 0; index < rows.Length; index++)
        {
            bool stretch = index is 4 or 8;
            editor.RowStyles.Add(new RowStyle(stretch ? SizeType.Percent : SizeType.AutoSize, stretch ? 50 : 0));
            rows[index].Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            rows[index].Margin = new Padding(3, 4, 3, 4);
            rows[index].TabIndex = index;
            editor.Controls.Add(rows[index], 0, index);
        }

        SplitContainerControl split = new() { Dock = DockStyle.Fill, Horizontal = true, SplitterPosition = 480, MinimumSize = new Size(0, 360) };
        split.Panel1.Controls.Add(composer.Grid);
        split.Panel2.Controls.Add(editor);
        composer.Grid.TabIndex = 0;
        editor.TabIndex = 1;
        filters.TabIndex = 0;
        split.TabIndex = 1;
        Control layout = CreateTabLayout(prefix, filters, split);

        // Events
        composer.Office.EditValueChanged += (_, _) =>
        {
            composer.SearchTimer.Stop();
            ClearComposerCustomers(composer);
            if (connectedDatabase is not null) composer.SearchTimer.Start();
        };
        composer.SearchTimer.Tick += async (_, _) =>
        {
            composer.SearchTimer.Stop();
            await LoadComposerCustomersAsync(composer);
        };
        composer.Search.EditValueChanged += (_, _) =>
        {
            composer.SearchTimer.Stop();
            ClearComposerCustomers(composer);
            if (connectedDatabase is not null) composer.SearchTimer.Start();
        };
        if (composer.Multiple)
        {
            composer.Category.SelectedIndexChanged += (_, _) =>
            {
                composer.SearchTimer.Stop();
                ClearComposerCustomers(composer);
                if (connectedDatabase is not null) composer.SearchTimer.Start();
            };
        }
        composer.Template.SelectedIndexChanged += (_, _) => ApplyComposerTemplate(composer, confirm: false);
        composer.PresetTemplate.SelectedIndexChanged += (_, _) => ApplyPresetTemplate(composer);
        InvalidateComposerPreview(composer);

        return layout;
    }

    private void ClearComposerCustomers(SmsComposer composer)
    {
        composer.Grid.DataSource = null;
        tabLoaded[composer.Key] = false;
        InvalidateComposerPreview(composer);
    }

    private SmsRecipient[] GetComposerRecipients(SmsComposer composer)
    {
        if (!composer.Multiple)
        {
            return composer.View.GetFocusedRow() is SmsRecipient recipient ? [recipient] : [];
        }

        return composer.View.GetSelectedRows().Where(handle => handle >= 0)
            .Select(handle => composer.View.GetRow(handle)).OfType<SmsRecipient>().ToArray();
    }

    private void InvalidateComposerPreview(SmsComposer composer)
    {
        composer.Preview.Text = "";
        SmsRecipient[] recipients = GetComposerRecipients(composer);
        int chars = composer.Message.Text?.Length ?? 0;
        int parts = chars == 0 ? 0 : chars <= 160 ? 1 : (chars + 152) / 153;
        string smsInfo = chars > 0 ? $" · {parts} SMS parçası" : "";
        composer.Counter.Text = $"{chars}/1000 karakter{smsInfo} · {recipients.Length} alıcı seçili";
        composer.Recipient.Text = !composer.Multiple && recipients.Length == 1
            ? IntegrationText.Format("SmsComposerRecipient", recipients[0].FullName, recipients[0].CustomerCode, recipients[0].Phone)
            : recipients.Length > 0 ? IntegrationText.Format("SmsComposerSelection", recipients.Length, chars) : GetText("SmsComposerNoRecipient");
        composer.Prepare.Enabled = !officeBusy && recipients.Length is > 0 and <= 500
            && !string.IsNullOrWhiteSpace(composer.Message.Text)
            && recipients.Any(recipient => SmsDraftService.NormalizePhone(recipient.Phone) is not null);
    }

    private async Task LoadComposerCustomersAsync(SmsComposer composer)
    {
        DatabaseSettings? settings = connectedDatabase;
        await RunOfficeOperationAsync(composer.Key, async token =>
        {
            if (settings is null) return;
            MessageCategory category = composer.Multiple && composer.Category.SelectedItem is MessageCategory selected
                ? selected : MessageCategory.All[0];
            CustomerFilter filter = new(SelectedOfficeCode(composer.Office), composer.Search.Text, category, true,
                screenSettings?.NebimDatabase ?? "");
            ClearComposerCustomers(composer);
            Page<SmsRecipient> page = await repository.ReadCustomersAsync(settings, filter, token);
            if (IsDisposed || connectedDatabase != settings || token.IsCancellationRequested) return;
            composer.Grid.DataSource = page.Items.ToList();
            composer.View.ClearSelection();
            tabLoaded[composer.Key] = true;
            tabStatus[composer.Key].Text = IntegrationText.Format(page.HasMore ? "SmsComposerResultsLimited" : "SmsComposerResults", page.Items.Count);
            InvalidateComposerPreview(composer);
        });
    }

    private void ApplyComposerTemplate(SmsComposer composer, bool confirm = true)
    {
        if (composer.Template.SelectedItem is not MessageCategory category) return;
        string text = templates.TryGetValue(category.Key, out string? template) ? template : MessageTemplateStore.DefaultFor(category);
        if (confirm && !string.IsNullOrWhiteSpace(composer.Message.Text) && composer.Message.Text != text
            && XtraMessageBox.Show(this, GetText("SmsComposerReplace"), GetText(composer.Key),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        composer.Message.Text = text;
    }

    private void ApplyPresetTemplate(SmsComposer composer)
    {
        if (composer.PresetTemplate.SelectedItem is not SmsPreset preset) return;
        if (!string.IsNullOrWhiteSpace(composer.Message.Text) && composer.Message.Text != preset.Text
            && XtraMessageBox.Show(this, GetText("SmsComposerReplace"), GetText(composer.Key),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        composer.Message.Text = preset.Text;
    }

    private void ClearComposerMessage(SmsComposer composer)
    {
        if (composer.Message.Text.Length == 0) return;
        if (XtraMessageBox.Show(this, GetText("SmsComposerClearConfirm"), GetText(composer.Key),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
        {
            composer.Message.Text = "";
        }
    }

    private void PrepareComposerDraft(SmsComposer composer)
    {
        composer.Preview.Text = "";
        try
        {
            SmsDraftBatch batch = SmsDraftService.Prepare(GetComposerRecipients(composer), composer.Message.Text);
            composer.Preview.Text = string.Join(Environment.NewLine + Environment.NewLine,
                batch.Messages.Select(message => $"{message.Phone}: {message.Text}"));
            tabStatus[composer.Key].Text = IntegrationText.Format("DatabaseDraftSummary", batch.Messages.Count,
                batch.InvalidPhoneCount, batch.ConflictingPhoneCount);
        }
        catch (ArgumentException exception)
        {
            tabStatus[composer.Key].Text = exception.Message;
        }
    }

    private void UpdateComposerControls(SmsComposer composer)
    {
        composer.List.Enabled = !officeBusy && connectedDatabase is not null;
        composer.Office.Enabled = composer.Search.Enabled = composer.Grid.Enabled = !officeBusy;
        if (composer.Multiple)
        {
            composer.Category.Enabled = !officeBusy;
            composer.SelectAll.Enabled = !officeBusy;
            composer.DeselectAll.Enabled = !officeBusy;
        }
        composer.ApplyTemplate.Enabled = templatesLoaded;
        SmsRecipient[] recipients = GetComposerRecipients(composer);
        composer.Prepare.Enabled = !officeBusy && recipients.Length is > 0 and <= 500
            && !string.IsNullOrWhiteSpace(composer.Message.Text)
            && recipients.Any(recipient => SmsDraftService.NormalizePhone(recipient.Phone) is not null);
    }
}
