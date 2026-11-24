using System.Security.Cryptography;
using System.Text.Json;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using ERSoftSMSBox.Integrations;

namespace ERSoftSMSBox;

public partial class Form1
{
    private readonly PanelControl apiSettingsPanel = new();
    private readonly TextEdit apiUrl = new();
    private readonly TextEdit apiUsername = new();
    private readonly TextEdit apiPassword = new();
    private readonly TextEdit apiVendor = new();
    private readonly TextEdit apiSender = new();
    private readonly CheckEdit apiEnabled = new();
    private readonly SimpleButton apiSave = new();
    private readonly SimpleButton apiSaveConnect = new();
    private readonly SimpleButton apiCancel = new();
    private readonly LabelControl apiResult = new();
    private readonly EyfelSoftProvider apiSettingsProvider;
    private readonly HttpClient apiSettingsHttpClient;
    private TableLayoutPanel? apiFields;
    private CancellationTokenSource? apiSettingsOperation;
    private bool apiSettingsBusy;
    private bool apiSettingsLoaded;
    private bool apiSettingsDirty;
    private bool apiSettingsPopulating;
    private bool apiReloadOnReturn;

    internal Task ApiSettingsCompletion { get; private set; } = Task.CompletedTask;

    private enum ApiSettingsAction { Load, Save, SaveConnect }

    private Control CreateApiSettingsPanel()
    {
        apiSettingsPanel.Name = "apiSettingsPanel";
        apiSettingsPanel.Dock = DockStyle.Fill;
        apiSettingsPanel.BorderStyle = BorderStyles.NoBorder;
        apiSettingsPanel.AutoScroll = true;
        apiSettingsPanel.Visible = false;
        apiSettingsPanel.AccessibleName = GetText("ConnectionSettings");
        Disposed += (_, _) =>
        {
            apiSettingsOperation?.Cancel();
            apiSettingsHttpClient.Dispose();
        };

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, RowCount = 4, Padding = new Padding(8)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < root.RowCount; row++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        apiEnabled.Text = GetText("IntegrationEnabled");
        apiPassword.Properties.UseSystemPasswordChar = true;
        apiUrl.Properties.MaxLength = 2000;
        apiUrl.Properties.NullValuePrompt = GetText("ApiSettingsUrlPrompt");
        apiUsername.Properties.MaxLength = apiVendor.Properties.MaxLength = apiSender.Properties.MaxLength = 100;
        apiFields = CreateDatabaseFields(
            ("EyfelSoftApiUrl", apiUrl), ("ApiSettingsState", apiEnabled),
            ("EyfelSoftUsername", apiUsername), ("EyfelSoftSecret", apiPassword),
            ("EyfelSoftVendor", apiVendor), ("EyfelSoftSender", apiSender));
        apiUrl.Name = "apiUrl";
        apiEnabled.Name = "apiEnabled";
        apiUsername.Name = "apiUsername";
        apiPassword.Name = "apiPassword";
        apiVendor.Name = "apiVendor";
        apiSender.Name = "apiSender";
        root.Controls.Add(apiFields, 0, 0);

        LabelControl help = new()
        {
            Text = GetText("ApiSettingsHelp"), AutoSizeMode = LabelAutoSizeMode.Vertical,
            Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(3, 10, 3, 10)
        };
        root.Controls.Add(help, 0, 1);
        ConfigureIntegrationButton(apiSave, "apiSave", "ApiSettingsSave", "Ready", 0);
        ConfigureIntegrationButton(apiSaveConnect, "apiSaveConnect", "ApiSettingsSaveConnect", "ConnectionSettings", 1);
        ConfigureIntegrationButton(apiCancel, "apiCancel", "IntegrationCancel", "Exit", 2);
        apiSave.Click += (_, _) => ApiSettingsCompletion = RunApiSettingsOperationAsync(ApiSettingsAction.Save);
        apiSaveConnect.Click += (_, _) => ApiSettingsCompletion = RunApiSettingsOperationAsync(ApiSettingsAction.SaveConnect);
        apiCancel.Click += (_, _) => apiSettingsOperation?.Cancel();
        root.Controls.Add(CreateToolbar(apiSave, apiSaveConnect, apiCancel), 0, 2);

        apiResult.Name = "apiResult";
        apiResult.AutoSizeMode = LabelAutoSizeMode.Vertical;
        apiResult.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        apiResult.Margin = new Padding(3, 10, 3, 10);
        apiResult.Text = GetText("ApiSettingsIdle");
        root.Controls.Add(apiResult, 0, 3);
        apiSettingsPanel.Controls.Add(root);
        foreach (BaseEdit editor in new BaseEdit[] { apiUrl, apiEnabled, apiUsername, apiPassword, apiVendor, apiSender })
        {
            editor.EditValueChanged += (_, _) =>
            {
                if (apiSettingsPopulating) return;
                apiSettingsDirty = true;
                apiResult.Text = GetText("IntegrationUnsaved");
                UpdateApiSettingsControls();
            };
        }
        UpdateApiSettingsControls();
        return apiSettingsPanel;
    }

    private void ShowApiSettings()
    {
        SelectRibbonPage("SettingsPage");
        sectionTitle.Text = GetText("ConnectionSettings");
        sectionDescription.Text = GetText("ConnectionSettingsHint");
        ActivateWorkspace(apiSettingsPanel);
        statusText.Caption = GetText("ConnectionSettings");
        if (apiSettingsBusy && apiSettingsOperation?.IsCancellationRequested == true)
            apiReloadOnReturn = true;
        if (!apiSettingsLoaded && !apiSettingsDirty && !apiSettingsBusy)
            ApiSettingsCompletion = RunApiSettingsOperationAsync(ApiSettingsAction.Load);
    }

    private void HideApiSettings()
    {
        apiSettingsOperation?.Cancel();
        apiReloadOnReturn = false;
        apiSettingsPanel.Visible = false;
    }

    private void InvalidateApiSettings() => apiSettingsLoaded = false;

    private SmsProviderDraft ReadApiSettings() => new(apiEnabled.Checked, apiUrl.Text.Trim(), apiPassword.Text,
        apiSender.Text.Trim(), apiUsername.Text.Trim(), apiVendor.Text.Trim());

    private void PopulateApiSettings(SmsProviderDraft settings)
    {
        apiSettingsPopulating = true;
        try
        {
            apiUrl.Text = settings.Identifier;
            apiUsername.Text = settings.Username;
            apiPassword.Text = settings.Secret;
            apiVendor.Text = settings.Vendor;
            apiSender.Text = settings.Sender;
            apiEnabled.Checked = settings.IsActive;
            apiSettingsLoaded = true;
            apiSettingsDirty = false;
        }
        finally { apiSettingsPopulating = false; }
    }

    private async Task RunApiSettingsOperationAsync(ApiSettingsAction action)
    {
        if (apiSettingsBusy || IsDisposed || Disposing) return;
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(40));
        apiSettingsOperation = cancellation;
        apiSettingsBusy = true;
        bool saved = false;
        UpdateApiSettingsControls();
        apiResult.Text = GetText(action == ApiSettingsAction.Load ? "ApiSettingsLoading" : "IntegrationBusy");
        try
        {
            if (action == ApiSettingsAction.Load)
            {
                SmsProviderDraft loaded = await apiSettingsProvider.LoadAsync(cancellation.Token);
                if (IsDisposed || Disposing || cancellation.IsCancellationRequested) return;
                PopulateApiSettings(loaded);
                apiResult.Text = GetText("ApiSettingsIdle");
                return;
            }

            SmsProviderDraft settings = ReadApiSettings();
            // Persist before checking; a failed connection must not discard valid account settings.
            await apiSettingsProvider.SaveAsync(settings, cancellation.Token);
            saved = true;
            if (IsDisposed || Disposing) return;
            apiSettingsLoaded = true;
            apiSettingsDirty = false;
            if (activeProvider is EyfelSoftProvider) integrationLoaded = false;
            apiResult.Text = GetText("ApiSettingsSaved");
            if (action == ApiSettingsAction.SaveConnect)
            {
                SmsProviderDraft stored = await apiSettingsProvider.LoadAsync(cancellation.Token);
                await apiSettingsProvider.GetAccountAsync(apiSettingsHttpClient, stored, cancellation.Token);
                if (!IsDisposed && !Disposing && !cancellation.IsCancellationRequested)
                    apiResult.Text = GetText("ApiSettingsSaved") + Environment.NewLine + GetText("EyfelSoftConnectionAvailable");
            }
        }
        catch (OperationCanceledException) { SetApiSettingsError(GetText("IntegrationCancelled"), saved); }
        catch (TimeoutException) { SetApiSettingsError(GetText("IntegrationTimeout"), saved); }
        catch (HttpRequestException exception) { SetApiSettingsError(exception.Message, saved); }
        catch (CryptographicException) { SetApiSettingsError(GetText("IntegrationDecryptError"), saved); }
        catch (UnauthorizedAccessException) { SetApiSettingsError(GetText("IntegrationStorageError"), saved); }
        catch (InvalidDataException exception) { SetApiSettingsError(exception.Message, saved); }
        catch (IOException) { SetApiSettingsError(GetText("IntegrationStorageError"), saved); }
        catch (JsonException) { SetApiSettingsError(GetText("IntegrationStorageInvalid"), saved); }
        catch (ArgumentException exception) { SetApiSettingsError(exception.Message, saved); }
        catch (InvalidOperationException exception) { SetApiSettingsError(exception.Message, saved); }
        finally
        {
            apiSettingsOperation = null;
            apiSettingsBusy = false;
            if (!IsDisposed && !Disposing)
            {
                UpdateApiSettingsControls();
                // A quick leave-and-return may occur while a cancelled load is still completing.
                if (apiReloadOnReturn && apiSettingsPanel.Visible && !apiSettingsLoaded && !apiSettingsDirty)
                {
                    apiReloadOnReturn = false;
                    ApiSettingsCompletion = RunApiSettingsOperationAsync(ApiSettingsAction.Load);
                }
            }
        }
    }

    private void SetApiSettingsError(string error, bool saved)
    {
        if (!IsDisposed && !Disposing)
            apiResult.Text = (saved ? GetText("ApiSettingsSaved") + Environment.NewLine : "") + error;
    }

    private void UpdateApiSettingsControls()
    {
        if (apiFields is not null) apiFields.Enabled = !apiSettingsBusy;
        apiSave.Enabled = !apiSettingsBusy;
        apiSaveConnect.Enabled = !apiSettingsBusy && apiSettingsProvider.CanCheckAccount(ReadApiSettings());
        apiCancel.Enabled = apiSettingsBusy;
    }
}
