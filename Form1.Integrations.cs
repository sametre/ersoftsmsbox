using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using ERSoftSMSBox.Integrations;

namespace ERSoftSMSBox;

public partial class Form1
{
    private readonly PanelControl integrationsPanel = new();
    private readonly ComboBoxEdit integrationProvider = new();
    private readonly CheckEdit integrationEnabled = new();
    private readonly LabelControl integrationIdentifierLabel = new();
    private readonly LabelControl integrationSecretLabel = new();
    private readonly TextEdit integrationIdentifier = new();
    private readonly TextEdit integrationSecret = new();
    private readonly ComboBoxEdit integrationSender = new();
    private readonly SimpleButton integrationSave = new();
    private readonly SimpleButton integrationTest = new();
    private readonly SimpleButton integrationCancel = new();
    private readonly LabelControl integrationResult = new();
    private readonly HttpClient integrationHttpClient = new(new HttpClientHandler { AllowAutoRedirect = false });
    private SmsProvider activeProvider = SmsProvider.Default;
    private CancellationTokenSource? integrationOperation;
    private IReadOnlyList<string>? verifiedSenders;
    private bool integrationLoaded;
    private bool integrationDirty;
    private bool integrationPopulating;
    private bool integrationBusy;

    private PanelControl CreateIntegrationsPanel()
    {
        integrationsPanel.Name = "integrationsPanel";
        integrationsPanel.Dock = DockStyle.Fill;
        integrationsPanel.BorderStyle = BorderStyles.NoBorder;
        integrationsPanel.AutoScroll = true;
        integrationsPanel.Visible = false;
        integrationsPanel.AccessibleName = GetText("IntegrationsPage");
        integrationsPanel.AccessibleDescription = GetText("IntegrationDescription");
        integrationsPanel.VisibleChanged += IntegrationActionClickAsync;
        Disposed += IntegrationResourcesDisposed;

        TableLayoutPanel fields = new()
        {
            Name = "integrationFields",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 8
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < fields.RowCount; row++)
        {
            fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        integrationProvider.Name = "integrationProvider";
        integrationProvider.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
        integrationProvider.Properties.Items.AddRange(SmsProvider.All.Select(provider => provider.Name).ToArray());
        integrationProvider.SelectedIndex = 0;
        integrationProvider.SelectedIndexChanged += IntegrationProviderChanged;
        AddIntegrationField(fields, "IntegrationProvider", integrationProvider, 0);

        integrationEnabled.Name = "integrationEnabled";
        integrationEnabled.Text = GetText("IntegrationEnabled");
        integrationEnabled.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        integrationEnabled.AccessibleName = GetText("IntegrationEnabled");
        integrationEnabled.AccessibleDescription = GetText("IntegrationSecurityNote");
        integrationEnabled.TabIndex = 1;
        integrationEnabled.CheckedChanged += IntegrationDraftChanged;
        fields.Controls.Add(integrationEnabled, 0, 1);
        fields.SetColumnSpan(integrationEnabled, 2);

        integrationIdentifier.Name = "integrationIdentifier";
        integrationIdentifier.Properties.UseSystemPasswordChar = true;
        integrationIdentifier.EditValueChanged += IntegrationCredentialsChanged;
        integrationSecret.Name = "integrationSecret";
        integrationSecret.Properties.UseSystemPasswordChar = true;
        integrationSecret.EditValueChanged += IntegrationCredentialsChanged;
        integrationSender.Name = "integrationSender";
        integrationSender.EditValueChanged += IntegrationDraftChanged;
        AddIntegrationField(fields, activeProvider.TextKey("Identifier"), integrationIdentifier, 2, integrationIdentifierLabel);
        AddIntegrationField(fields, activeProvider.TextKey("Secret"), integrationSecret, 3, integrationSecretLabel);
        AddIntegrationField(fields, "IntegrationSender", integrationSender, 4);

        LabelControl securityNote = new()
        {
            Text = GetText("IntegrationSecurityNote"),
            AutoSizeMode = LabelAutoSizeMode.Vertical,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(3, 12, 3, 12)
        };
        fields.Controls.Add(securityNote, 0, 5);
        fields.SetColumnSpan(securityNote, 2);

        FlowLayoutPanel buttons = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            WrapContents = true,
            TabIndex = 5
        };
        ConfigureIntegrationButton(integrationSave, "integrationSave", "IntegrationSave", "Ready", 0);
        ConfigureIntegrationButton(integrationTest, "integrationTest", "IntegrationTest", "ConnectionSettings", 1);
        ConfigureIntegrationButton(integrationCancel, "integrationCancel", "IntegrationCancel", "Exit", 2);
        integrationSave.Click += IntegrationActionClickAsync;
        integrationTest.Click += IntegrationActionClickAsync;
        integrationCancel.Click += CancelIntegrationOperation;
        buttons.Controls.Add(integrationSave);
        buttons.Controls.Add(integrationTest);
        buttons.Controls.Add(integrationCancel);
        fields.Controls.Add(buttons, 0, 6);
        fields.SetColumnSpan(buttons, 2);

        integrationResult.Name = "integrationResult";
        integrationResult.AutoSizeMode = LabelAutoSizeMode.Vertical;
        integrationResult.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        integrationResult.Margin = new Padding(3, 12, 3, 16);
        integrationResult.Text = GetText("IntegrationInactive");
        integrationResult.AccessibleName = GetText("IntegrationsPage");
        fields.Controls.Add(integrationResult, 0, 7);
        fields.SetColumnSpan(integrationResult, 2);
        integrationsPanel.Controls.Add(fields);
        ApplyProviderTexts();
        UpdateIntegrationControls();

        return integrationsPanel;
    }

    private void AddIntegrationField(TableLayoutPanel layout, string key, Control editor, int row, LabelControl? label = null)
    {
        label ??= new LabelControl();
        label.Text = GetText(key);
        label.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        label.Margin = new Padding(3, 8, 16, 8);
        editor.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        editor.Margin = new Padding(3, 8, 3, 8);
        editor.TabIndex = row;
        editor.AccessibleName = GetText(key);
        editor.AccessibleDescription = GetText("IntegrationSecurityNote");
        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(editor, 1, row);
    }

    private void ConfigureIntegrationButton(SimpleButton button, string name, string textKey, string iconKey, int tabIndex)
    {
        button.Name = name;
        button.Text = GetText(textKey);
        button.AutoSize = true;
        button.MinimumSize = new Size(140, 36);
        button.Margin = new Padding(3, 6, 9, 6);
        button.TabIndex = tabIndex;
        button.AccessibleName = GetText(textKey);
        button.AccessibleDescription = GetText(textKey);
        button.ImageOptions.SvgImage = menuImages[iconKey];
        button.ImageOptions.SvgImageSize = new Size(20, 20);
    }

    private void IntegrationsButtonClick(object sender, ItemClickEventArgs e)
        => ShowIntegrations(e.Item.Tag is string key ? SmsProvider.Find(key) : null);

    private void RibbonPageChanged(object? sender, EventArgs e)
    {
        if (sender is RibbonControl { SelectedPage.Name: "IntegrationsPage" })
        {
            ShowIntegrations();
        }
        else if (integrationsPanel.Visible)
        {
            ShowHome();
        }
    }

    private void ShowIntegrations(SmsProvider? provider = null)
    {
        if (provider is not null && !TrySwitchProvider(provider))
        {
            return;
        }
        sectionTitle.Text = activeProvider.Name;
        HideDatabase();
        HideOffices();
        sectionDescription.Text = GetText(activeProvider.TextKey("Description"));
        statusText.Caption = GetText("IntegrationsPage") + " — " + activeProvider.Name;
        quickActions.Visible = false;
        integrationsPanel.Visible = true;
    }

    private void HideIntegrations()
    {
        integrationOperation?.Cancel();
        integrationsPanel.Visible = false;
    }

    private void IntegrationProviderChanged(object? sender, EventArgs e)
    {
        if (integrationPopulating || integrationProvider.SelectedIndex < 0)
        {
            return;
        }
        SmsProvider provider = SmsProvider.All[integrationProvider.SelectedIndex];
        if (provider == activeProvider)
        {
            return;
        }
        if (TrySwitchProvider(provider))
        {
            ShowIntegrations();
        }
        else
        {
            SelectProviderInList(activeProvider);
        }
    }

    // Switches the screen to another provider; unsaved edits require confirmation because each provider stores its own file.
    private bool TrySwitchProvider(SmsProvider provider)
    {
        if (provider == activeProvider)
        {
            return true;
        }
        if (integrationDirty && XtraMessageBox.Show(this,
                IntegrationText.Format("IntegrationSwitchUnsaved", provider.Name), GetText("IntegrationsPage"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return false;
        }

        integrationOperation?.Cancel();
        activeProvider = provider;
        verifiedSenders = null;
        integrationLoaded = false;
        integrationDirty = false;
        integrationPopulating = true;
        try
        {
            SelectProviderInList(provider);
            integrationEnabled.Checked = false;
            integrationIdentifier.Text = "";
            integrationSecret.Text = "";
            integrationSender.Properties.Items.Clear();
            integrationSender.Text = "";
        }
        finally
        {
            integrationPopulating = false;
        }
        ApplyProviderTexts();
        integrationResult.Text = GetText("IntegrationInactive");
        UpdateIntegrationControls();
        if (integrationsPanel.Visible && !integrationBusy)
        {
            IntegrationActionClickAsync(integrationsPanel, EventArgs.Empty);
        }

        return true;
    }

    private void SelectProviderInList(SmsProvider provider)
    {
        bool populating = integrationPopulating;
        integrationPopulating = true;
        try
        {
            integrationProvider.SelectedIndex = SmsProvider.All.ToList().IndexOf(provider);
        }
        finally
        {
            integrationPopulating = populating;
        }
    }

    private void ApplyProviderTexts()
    {
        integrationIdentifierLabel.Text = integrationIdentifier.AccessibleName = GetText(activeProvider.TextKey("Identifier"));
        integrationSecretLabel.Text = integrationSecret.AccessibleName = GetText(activeProvider.TextKey("Secret"));
        integrationResult.AccessibleName = activeProvider.Name;
    }

    private SmsProviderDraft GetIntegrationDraft()
        => new(integrationEnabled.Checked, integrationIdentifier.Text.Trim(), integrationSecret.Text.Trim(),
            integrationSender.Text.Trim());

    private async void IntegrationActionClickAsync(object? sender, EventArgs e)
    {
        if (integrationBusy || IsDisposed || sender == integrationsPanel && (!integrationsPanel.Visible || integrationLoaded))
        {
            return;
        }

        SmsProvider provider = activeProvider;
        using CancellationTokenSource cancellation = new();
        integrationOperation = cancellation;
        integrationBusy = true;
        UpdateIntegrationControls();
        integrationResult.Text = GetText("IntegrationBusy");
        try
        {
            if (sender == integrationsPanel)
            {
                SmsProviderDraft settings = await provider.LoadAsync(cancellation.Token);
                if (IsDisposed || provider != activeProvider)
                {
                    return;
                }
                integrationPopulating = true;
                try
                {
                    integrationIdentifier.Text = settings.Identifier;
                    integrationSecret.Text = settings.Secret;
                    integrationSender.Text = settings.Sender;
                    integrationEnabled.Checked = settings.IsActive;
                    integrationLoaded = true;
                    integrationDirty = false;
                }
                finally
                {
                    integrationPopulating = false;
                }
                integrationResult.Text = GetText(settings.IsActive ? "IntegrationActive" : "IntegrationInactive");
            }
            else if (sender == integrationSave)
            {
                SmsProviderDraft settings = GetIntegrationDraft();
                if (settings.IsActive && (string.IsNullOrWhiteSpace(settings.Identifier) || string.IsNullOrWhiteSpace(settings.Secret)))
                {
                    throw new ArgumentException(IntegrationText.Format("IntegrationCredentialsRequired",
                        GetText(provider.TextKey("Identifier")), GetText(provider.TextKey("Secret"))));
                }
                if (settings.IsActive && settings.Sender.Length > 0 && verifiedSenders is not null
                    && !verifiedSenders.Contains(settings.Sender, StringComparer.Ordinal))
                {
                    throw new InvalidOperationException(GetText("IntegrationSenderInvalid"));
                }
                await provider.SaveAsync(settings, cancellation.Token);
                if (!IsDisposed && provider == activeProvider)
                {
                    integrationLoaded = true;
                    integrationDirty = false;
                    integrationResult.Text = IntegrationText.Format("IntegrationSaved",
                        GetText(settings.IsActive ? "IntegrationActive" : "IntegrationInactive"));
                }
            }
            else if (sender == integrationTest)
            {
                SmsProviderAccount account = await provider.GetAccountAsync(integrationHttpClient, GetIntegrationDraft(),
                    cancellation.Token);
                if (IsDisposed || provider != activeProvider)
                {
                    return;
                }
                verifiedSenders = account.Senders;
                integrationPopulating = true;
                try
                {
                    integrationSender.Properties.Items.Clear();
                    integrationSender.Properties.Items.AddRange(account.Senders.ToArray());
                }
                finally
                {
                    integrationPopulating = false;
                }
                integrationResult.Text = IntegrationText.Format("IntegrationSuccess", account.Balance, account.Sms,
                    account.Senders.Count);
                if (account.Senders.Count == 0)
                {
                    integrationResult.Text += Environment.NewLine + GetText(provider.TextKey("NoSenders"));
                }
            }
        }
        catch (OperationCanceledException)
        {
            ShowIntegrationError("IntegrationCancelled");
        }
        catch (TimeoutException)
        {
            ShowIntegrationError("IntegrationTimeout");
        }
        catch (HttpRequestException exception)
        {
            ShowIntegrationError(exception.StatusCode == HttpStatusCode.Unauthorized
                ? provider.TextKey("AuthError") : "IntegrationNetworkError");
        }
        catch (CryptographicException)
        {
            ShowIntegrationError("IntegrationDecryptError");
        }
        catch (UnauthorizedAccessException)
        {
            ShowIntegrationError("IntegrationStorageError");
        }
        catch (InvalidDataException exception)
        {
            ShowIntegrationMessage(exception.Message);
        }
        catch (IOException)
        {
            ShowIntegrationError("IntegrationStorageError");
        }
        catch (JsonException)
        {
            ShowIntegrationError("IntegrationStorageInvalid");
        }
        catch (ArgumentException exception)
        {
            ShowIntegrationMessage(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ShowIntegrationMessage(exception.Message);
        }
        finally
        {
            if (integrationOperation == cancellation)
            {
                integrationOperation = null;
            }
            integrationBusy = false;
            if (!IsDisposed)
            {
                UpdateIntegrationControls();
                // The provider was switched while this operation ran; load the new provider's saved settings now.
                if (provider != activeProvider && integrationsPanel.Visible && !integrationLoaded)
                {
                    IntegrationActionClickAsync(integrationsPanel, EventArgs.Empty);
                }
            }
        }
    }

    private void IntegrationCredentialsChanged(object? sender, EventArgs e)
    {
        if (!integrationPopulating)
        {
            verifiedSenders = null;
            integrationSender.Properties.Items.Clear();
        }
        IntegrationDraftChanged(sender, e);
    }

    private void IntegrationDraftChanged(object? sender, EventArgs e)
    {
        if (integrationPopulating)
        {
            return;
        }
        if (!integrationEnabled.Checked)
        {
            integrationOperation?.Cancel();
        }
        integrationDirty = true;
        integrationResult.Text = GetText(integrationEnabled.Checked ? "IntegrationActive" : "IntegrationInactive")
            + Environment.NewLine + GetText("IntegrationUnsaved");
        UpdateIntegrationControls();
    }

    private void UpdateIntegrationControls()
    {
        integrationProvider.Enabled = !integrationBusy;
        integrationEnabled.Enabled = !integrationBusy;
        integrationIdentifier.Enabled = integrationSecret.Enabled = integrationSender.Enabled
            = !integrationBusy && integrationEnabled.Checked;
        integrationSave.Enabled = !integrationBusy;
        integrationTest.Enabled = !integrationBusy && integrationEnabled.Checked
            && !string.IsNullOrWhiteSpace(integrationIdentifier.Text) && !string.IsNullOrWhiteSpace(integrationSecret.Text);
        integrationCancel.Enabled = integrationBusy;
    }

    private void CancelIntegrationOperation(object? sender, EventArgs e) => integrationOperation?.Cancel();

    private void ShowIntegrationError(string key) => ShowIntegrationMessage(GetText(key));

    private void ShowIntegrationMessage(string message)
    {
        if (!IsDisposed)
        {
            integrationResult.Text = message;
        }
    }

    private void IntegrationResourcesDisposed(object? sender, EventArgs e)
    {
        integrationOperation?.Cancel();
        integrationHttpClient.Dispose();
    }
}
