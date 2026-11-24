using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using DevExpress.XtraBars;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using ERSoftSMSBox.Data;
using ERSoftSMSBox.Integrations;
using Microsoft.Data.SqlClient;

namespace ERSoftSMSBox;

public partial class Form1
{
    private readonly PanelControl databasePanel = new();
    private readonly TextEdit databaseServer = new();
    private readonly TextEdit databaseName = new();
    private readonly TextEdit databaseUser = new();
    private readonly TextEdit databasePassword = new();
    private readonly TextEdit databaseOfficeCode = new();
    private readonly CheckEdit databaseWindows = new();
    private readonly CheckEdit databaseTrustCertificate = new();
    private readonly SimpleButton databaseImport = new();
    private readonly SimpleButton databaseConnect = new();
    private readonly SimpleButton databaseSave = new();
    private readonly SimpleButton databaseCancel = new();
    private readonly LabelControl databaseResult = new();
    private readonly MemoEdit databaseScreenInfo = new();
    private readonly DatabaseSettingsStore databaseStore = new();
    private readonly BBSoftRepository repository = new();
    private TableLayoutPanel? databaseFields;
    private DatabaseSettings? connectedDatabase;
    private BBSoftScreenSettings? screenSettings;
    private IReadOnlyList<OfficeSummary> officeSummaries = [];
    private CancellationTokenSource? databaseOperation;
    private bool databaseLoaded;
    private bool databasePopulating;
    private bool databaseBusy;

    private PanelControl CreateDatabasePanel()
    {
        databasePanel.Name = "databasePanel";
        databasePanel.Dock = DockStyle.Fill;
        databasePanel.BorderStyle = BorderStyles.NoBorder;
        databasePanel.AutoScroll = true;
        databasePanel.Visible = false;
        databasePanel.AccessibleName = GetText("DatabaseSettings");
        databasePanel.AccessibleDescription = GetText("DatabaseDescription");
        databasePanel.VisibleChanged += DatabaseActionAsync;
        Disposed += DatabaseResourcesDisposed;

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 5
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < root.RowCount; row++)
        {
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        databaseName.Text = "BBSoft";
        databasePassword.Properties.UseSystemPasswordChar = true;
        databaseOfficeCode.Properties.MaxLength = 50;
        databaseWindows.Text = GetText("DatabaseWindows");
        databaseTrustCertificate.Text = GetText("DatabaseTrustCertificate");
        databaseTrustCertificate.ToolTip = GetText("DatabaseTrustCertificateHelp");
        databaseFields = CreateDatabaseFields(
            ("DatabaseServer", databaseServer), ("DatabaseName", databaseName),
            ("DatabaseUser", databaseUser), ("DatabasePassword", databasePassword),
            ("DatabaseOfficeCode", databaseOfficeCode), ("DatabaseAuthentication", databaseWindows),
            ("DatabaseSecurity", databaseTrustCertificate));
        root.Controls.Add(databaseFields, 0, 0);

        FlowLayoutPanel buttons = new()
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Left | AnchorStyles.Right, WrapContents = true, TabIndex = 1
        };
        ConfigureIntegrationButton(databaseImport, "databaseImport", "DatabaseImport", "ImportContacts", 0);
        ConfigureIntegrationButton(databaseConnect, "databaseConnect", "DatabaseConnect", "ConnectionSettings", 1);
        ConfigureIntegrationButton(databaseSave, "databaseSave", "DatabaseSave", "Ready", 2);
        ConfigureIntegrationButton(databaseCancel, "databaseCancel", "IntegrationCancel", "Exit", 3);
        databaseImport.Click += ImportDatabaseSettings;
        databaseConnect.Click += DatabaseActionAsync;
        databaseSave.Click += DatabaseActionAsync;
        databaseCancel.Click += CancelDatabaseOperation;
        buttons.Controls.AddRange([databaseImport, databaseConnect, databaseSave, databaseCancel]);
        root.Controls.Add(buttons, 0, 1);

        databaseResult.Name = "databaseResult";
        databaseResult.Text = GetText("DatabaseSafetyNote");
        databaseResult.AutoSizeMode = LabelAutoSizeMode.Vertical;
        databaseResult.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        databaseResult.Margin = new Padding(3, 8, 3, 8);
        root.Controls.Add(databaseResult, 0, 2);

        LabelControl screenTitle = new()
        {
            Text = GetText("DatabaseScreenSettings"), Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(3, 12, 3, 4)
        };
        screenTitle.Appearance.FontStyleDelta = FontStyle.Bold;
        root.Controls.Add(screenTitle, 0, 3);
        databaseScreenInfo.Name = "databaseScreenInfo";
        databaseScreenInfo.Properties.ReadOnly = true;
        databaseScreenInfo.Height = 170;
        databaseScreenInfo.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        databaseScreenInfo.Text = GetText("DatabaseScreenSettingsHelp");
        databaseScreenInfo.AccessibleName = GetText("DatabaseScreenSettings");
        root.Controls.Add(databaseScreenInfo, 0, 4);

        databasePanel.Controls.Add(root);

        foreach (BaseEdit editor in new BaseEdit[] { databaseServer, databaseName, databaseUser, databasePassword, databaseOfficeCode, databaseWindows, databaseTrustCertificate })
        {
            editor.EditValueChanged += DatabaseProfileChanged;
        }
        UpdateDatabaseControls();

        return databasePanel;
    }

    private TableLayoutPanel CreateDatabaseFields(params (string Key, Control Editor)[] fields)
    {
        TableLayoutPanel layout = new()
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Left | AnchorStyles.Right, ColumnCount = 4,
            RowCount = (fields.Length + 1) / 2, Margin = new Padding(3, 6, 3, 6)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int index = 0; index < fields.Length; index++)
        {
            (string key, Control editor) = fields[index];
            if (index % 2 == 0)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            LabelControl label = new() { Text = GetText(key), Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(3, 6, 9, 6) };
            editor.Name = key;
            editor.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            editor.Margin = new Padding(3, 6, 9, 6);
            editor.TabIndex = index;
            editor.AccessibleName = GetText(key);
            editor.AccessibleDescription = GetText(key);
            int column = index % 2 * 2;
            layout.Controls.Add(label, column, index / 2);
            layout.Controls.Add(editor, column + 1, index / 2);
        }
        if (fields.Length == 1)
        {
            layout.SetColumnSpan(fields[0].Editor, 3);
        }

        return layout;
    }

    private void DatabaseButtonClick(object sender, ItemClickEventArgs e) => ShowDatabase();

    private void ShowDatabase()
    {
        SelectRibbonPage("OfficesPage");
        ActivateWorkspace(databasePanel);
        sectionTitle.Text = GetText("DatabaseSettings");
        sectionDescription.Text = GetText("DatabaseDescription");
        statusText.Caption = connectedDatabase is null ? GetText("DatabaseDisconnected") : GetText("DatabaseConnectedStatus");
        quickActions.Visible = false;
        databasePanel.Visible = true;
        databasePanel.BringToFront();
    }

    private void HideDatabase()
    {
        databaseOperation?.Cancel();
        databasePanel.Visible = false;
    }

    private DatabaseSettings ReadDatabaseSettings() => new(databaseServer.Text.Trim(), databaseName.Text.Trim(),
        databaseUser.Text.Trim(), databasePassword.Text, databaseWindows.Checked, databaseTrustCertificate.Checked,
        databaseOfficeCode.Text.Trim());

    private void PopulateDatabaseSettings(DatabaseSettings settings)
    {
        databasePopulating = true;
        try
        {
            databaseServer.Text = settings.Server;
            databaseName.Text = settings.Database;
            databaseUser.Text = settings.UserName;
            databasePassword.Text = settings.Password;
            databaseOfficeCode.Text = settings.OfficeCode;
            databaseWindows.Checked = settings.WindowsAuthentication;
            databaseTrustCertificate.Checked = settings.TrustServerCertificate;
        }
        finally
        {
            databasePopulating = false;
        }
    }

    private void ImportDatabaseSettings(object? sender, EventArgs e)
    {
        using OpenFileDialog dialog = new()
        {
            Title = GetText("DatabaseImport"), Filter = GetText("DatabaseImportFilter"), FileName = "Ayarlar.INI", CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        try
        {
            PopulateDatabaseSettings(BBSoftIniImporter.ReadFile(dialog.FileName, ReadDatabaseSettings()));
            DatabaseProfileChanged(sender, e);
            SetDatabaseMessage(IntegrationText.Format("DatabaseImported", Path.GetFileName(dialog.FileName)));
        }
        catch (InvalidDataException) { SetDatabaseMessage(GetText("DatabaseImportInvalid")); }
        catch (IOException) { SetDatabaseMessage(GetText("DatabaseImportInvalid")); }
        catch (UnauthorizedAccessException) { SetDatabaseMessage(GetText("DatabaseImportInvalid")); }
    }

    private async void DatabaseActionAsync(object? sender, EventArgs e)
    {
        if (databaseBusy || IsDisposed || sender == databasePanel && (!databasePanel.Visible || databaseLoaded))
        {
            return;
        }
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(60));
        databaseOperation = cancellation;
        databaseBusy = true;
        UpdateDatabaseControls();
        try
        {
            if (sender == databasePanel)
            {
                DatabaseSettings settings = await databaseStore.LoadAsync(cancellation.Token);
                if (IsDisposed) return;
                PopulateDatabaseSettings(settings);
                databaseLoaded = true;
                databaseResult.Text = GetText("DatabaseSafetyNote");
            }
            else if (sender == databaseSave)
            {
                await databaseStore.SaveAsync(ReadDatabaseSettings(), cancellation.Token);
                SetDatabaseMessage(GetText("DatabaseSaved"));
            }
            else if (sender == databaseConnect)
            {
                SetDatabaseMessage(GetText("IntegrationBusy"));
                DatabaseSettings settings = ReadDatabaseSettings();
                DateTime serverTime = await repository.ReadServerTimeAsync(settings, cancellation.Token);
                BBSoftScreenSettings screen = await repository.ReadScreenSettingsAsync(settings, cancellation.Token);
                IReadOnlyList<OfficeSummary> offices = await repository.ReadOfficesAsync(settings, cancellation.Token);
                if (IsDisposed) return;
                connectedDatabase = settings;
                screenSettings = screen;
                officeSummaries = offices;
                InvalidateDashboard();
                databaseScreenInfo.Text = DescribeScreenSettings(screen);
                statusText.Caption = GetText("DatabaseConnectedStatus");
                OnDatabaseConnected();
                SetDatabaseMessage(IntegrationText.Format("DatabaseConnected", offices.Count, offices.Sum(office => office.CustomerCount),
                    serverTime.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture)));
            }
        }
        catch (OperationCanceledException) { SetDatabaseMessage(GetText("DatabaseCancelled")); }
        catch (SqlException exception) { SetDatabaseMessage(IntegrationText.Format("DatabaseSqlError", exception.Number, FirstLine(exception.Message))); }
        catch (CryptographicException) { SetDatabaseMessage(GetText("IntegrationDecryptError")); }
        catch (UnauthorizedAccessException) { SetDatabaseMessage(GetText("DatabaseStorageError")); }
        catch (IOException) { SetDatabaseMessage(GetText("DatabaseStorageError")); }
        catch (JsonException) { SetDatabaseMessage(GetText("DatabaseStorageError")); }
        catch (InvalidDataException exception) { SetDatabaseMessage(exception.Message); }
        catch (InvalidCastException) { SetDatabaseMessage(GetText("DatabaseSchemaError")); }
        catch (FormatException) { SetDatabaseMessage(GetText("DatabaseSchemaError")); }
        catch (OverflowException) { SetDatabaseMessage(GetText("DatabaseSchemaError")); }
        catch (ArgumentException exception) { SetDatabaseMessage(exception.Message); }
        catch (InvalidOperationException) { SetDatabaseMessage(GetText("DatabaseSchemaError")); }
        finally
        {
            databaseOperation = null;
            databaseBusy = false;
            if (!IsDisposed) UpdateDatabaseControls();
        }
    }

    private string DescribeScreenSettings(BBSoftScreenSettings screen)
    {
        List<string> lines = [];
        for (int stage = 0; stage < screen.StageDays.Count; stage++)
        {
            lines.Add(IntegrationText.Format("DatabaseStageLine", stage + 1, screen.StageDays[stage],
                string.IsNullOrWhiteSpace(screen.StageMessages[stage]) ? GetText("DatabaseStageNoText") : screen.StageMessages[stage]));
        }
        lines.Add(IntegrationText.Format("DatabaseNebimLine", screen.NebimDatabase.Length == 0 ? GetText("DatabaseNebimMissing") : screen.NebimDatabase));
        lines.Add(GetText(screen.SendToGuarantor ? "DatabaseGuarantorOn" : "DatabaseGuarantorOff"));

        return string.Join(Environment.NewLine, lines);
    }

    private static string FirstLine(string message)
    {
        int newLine = message.IndexOfAny(['\r', '\n']);

        return newLine < 0 ? message : message[..newLine];
    }

    private void DatabaseProfileChanged(object? sender, EventArgs e)
    {
        if (databasePopulating) return;
        connectedDatabase = null;
        screenSettings = null;
        officeSummaries = [];
        databaseScreenInfo.Text = GetText("DatabaseScreenSettingsHelp");
        statusText.Caption = GetText("DatabaseDisconnected");
        InvalidateDashboard();
        OnDatabaseConnected();
        SetDatabaseMessage(GetText("DatabaseFiltersChanged"));
        UpdateDatabaseControls();
    }

    private void UpdateDatabaseControls()
    {
        if (databaseFields is not null) databaseFields.Enabled = !databaseBusy;
        databaseUser.Enabled = databasePassword.Enabled = !databaseBusy && !databaseWindows.Checked;
        databaseImport.Enabled = databaseConnect.Enabled = databaseSave.Enabled = !databaseBusy;
        databaseCancel.Enabled = databaseBusy;
    }

    private void SetDatabaseMessage(string text)
    {
        if (!IsDisposed) databaseResult.Text = text;
    }

    private void CancelDatabaseOperation(object? sender, EventArgs e) => databaseOperation?.Cancel();

    private void DatabaseResourcesDisposed(object? sender, EventArgs e) => databaseOperation?.Cancel();
}
