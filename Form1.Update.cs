using DevExpress.XtraBars;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using ERSoftSMSBox.Updates;

namespace ERSoftSMSBox;

public partial class Form1
{
    private readonly PanelControl updatePanel = new();
    private readonly LabelControl updateCurrentVersion = new();
    private readonly LabelControl updateStatus = new();
    private readonly MemoEdit updateReleaseNotes = new();
    private readonly SimpleButton updateCheck = new();
    private readonly SimpleButton updateInstall = new();
    private readonly SimpleButton updateCancel = new();
    private readonly DevExpress.XtraEditors.ProgressBarControl updateProgress = new();
    private readonly UpdateService updateService = new();
    private BarButtonItem? updateBadge;
    private CancellationTokenSource? updateOperation;
    private VersionInfo? availableUpdate;
    private bool updateBusy;

    private PanelControl CreateUpdatePanel()
    {
        updatePanel.Name = "updatePanel";
        updatePanel.Dock = DockStyle.Fill;
        updatePanel.BorderStyle = BorderStyles.NoBorder;
        updatePanel.AutoScroll = true;
        updatePanel.Visible = false;
        updatePanel.AccessibleName = GetText("AppUpdate");
        updatePanel.VisibleChanged += UpdatePanelVisibleChanged;
        Disposed += UpdateResourcesDisposed;

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 7
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < root.RowCount; i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        updateCurrentVersion.Name = "updateCurrentVersion";
        updateCurrentVersion.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture,
            GetText("UpdateCurrentVersion"), AppVersion.Current);
        updateCurrentVersion.AutoSizeMode = LabelAutoSizeMode.Vertical;
        updateCurrentVersion.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        updateCurrentVersion.Margin = new Padding(3, 8, 3, 8);
        root.Controls.Add(updateCurrentVersion, 0, 0);

        FlowLayoutPanel buttons = new()
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Left, WrapContents = false, Margin = new Padding(0, 4, 0, 4)
        };
        ConfigureIntegrationButton(updateCheck, "updateCheck", "UpdateCheck", "ConnectionSettings", 0);
        ConfigureIntegrationButton(updateInstall, "updateInstall", "UpdateInstall", "Ready", 1);
        ConfigureIntegrationButton(updateCancel, "updateCancel", "IntegrationCancel", "Exit", 2);
        updateCheck.Click += CheckUpdateAsync;
        updateInstall.Click += InstallUpdateAsync;
        updateCancel.Click += CancelUpdateOperation;
        buttons.Controls.AddRange([updateCheck, updateInstall, updateCancel]);
        root.Controls.Add(buttons, 0, 1);

        updateProgress.Name = "updateProgress";
        updateProgress.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        updateProgress.Height = 22;
        updateProgress.Margin = new Padding(3, 4, 3, 4);
        updateProgress.Properties.Maximum = 100;
        updateProgress.Visible = false;
        root.Controls.Add(updateProgress, 0, 2);

        updateStatus.Name = "updateStatus";
        updateStatus.Text = GetText("UpdateStatusIdle");
        updateStatus.AutoSizeMode = LabelAutoSizeMode.Vertical;
        updateStatus.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        updateStatus.Margin = new Padding(3, 8, 3, 4);
        root.Controls.Add(updateStatus, 0, 3);

        LabelControl notesTitle = new()
        {
            Text = GetText("UpdateReleaseNotes"), Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(3, 12, 3, 4), AutoSizeMode = LabelAutoSizeMode.Vertical
        };
        notesTitle.Appearance.FontStyleDelta = FontStyle.Bold;
        root.Controls.Add(notesTitle, 0, 4);

        updateReleaseNotes.Name = "updateReleaseNotes";
        updateReleaseNotes.Properties.ReadOnly = true;
        updateReleaseNotes.Height = 150;
        updateReleaseNotes.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        updateReleaseNotes.Text = GetText("UpdateNoNotes");
        root.Controls.Add(updateReleaseNotes, 0, 5);

        updatePanel.Controls.Add(root);
        UpdateUpdateControls();
        return updatePanel;
    }

    private void ShowUpdate()
    {
        SelectRibbonPage("SettingsPage");
        ActivateWorkspace(updatePanel);
        sectionTitle.Text = GetText("AppUpdate");
        sectionDescription.Text = GetText("AppUpdateHint");
        statusText.Caption = GetText("AppUpdate");
        quickActions.Visible = false;
        updatePanel.Visible = true;
        updatePanel.BringToFront();
    }

    private void HideUpdate()
    {
        updateOperation?.Cancel();
        updatePanel.Visible = false;
    }

    private void UpdatePanelVisibleChanged(object? sender, EventArgs e)
    {
        if (!updatePanel.Visible) return;
        availableUpdate = null;
        updateInstall.Enabled = false;
        updateReleaseNotes.Text = GetText("UpdateNoNotes");
        updateProgress.Visible = false;
        UpdateUpdateControls();
    }

    private async void CheckUpdateAsync(object? sender, EventArgs e)
    {
        if (updateBusy || IsDisposed) return;
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        updateOperation = cts;
        updateBusy = true;
        availableUpdate = null;
        UpdateUpdateControls();
        SetUpdateMessage(GetText("UpdateChecking"));
        try
        {
            VersionInfo? info = await updateService.CheckForUpdateAsync(cts.Token);
            if (IsDisposed) return;
            if (info is null)
            {
                SetUpdateMessage(GetText("UpdateNoRelease"));
                return;
            }
            bool isNewer = IsNewerVersion(info.Version);
            if (!isNewer)
            {
                SetUpdateMessage(string.Format(System.Globalization.CultureInfo.CurrentCulture,
                    GetText("UpdateUpToDate"), info.Version));
                return;
            }
            availableUpdate = info;
            updateReleaseNotes.Text = string.IsNullOrWhiteSpace(info.ReleaseNotes)
                ? GetText("UpdateNoNotes") : info.ReleaseNotes;
            SetUpdateMessage(string.Format(System.Globalization.CultureInfo.CurrentCulture,
                GetText("UpdateAvailable"), info.Version, info.ReleaseDate));
            ShowUpdateBadge(info.Version);
        }
        catch (OperationCanceledException) { SetUpdateMessage(GetText("UpdateCancelled")); }
        catch (HttpRequestException ex) { SetUpdateMessage(string.Format(System.Globalization.CultureInfo.CurrentCulture, GetText("UpdateNetworkError"), ex.Message)); }
        catch (System.Text.Json.JsonException) { SetUpdateMessage(GetText("UpdateParseError")); }
        finally
        {
            updateOperation = null;
            updateBusy = false;
            if (!IsDisposed) UpdateUpdateControls();
        }
    }

    private async void InstallUpdateAsync(object? sender, EventArgs e)
    {
        if (updateBusy || IsDisposed || availableUpdate is null) return;
        DialogResult confirm = XtraMessageBox.Show(this,
            string.Format(System.Globalization.CultureInfo.CurrentCulture,
                GetText("UpdateConfirm"), availableUpdate.Version),
            GetText("AppUpdate"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        using CancellationTokenSource cts = new(TimeSpan.FromMinutes(5));
        updateOperation = cts;
        updateBusy = true;
        updateProgress.Visible = true;
        updateProgress.EditValue = 0;
        UpdateUpdateControls();
        SetUpdateMessage(GetText("UpdateDownloading"));

        string zipPath = Path.Combine(Path.GetTempPath(), $"ERSoftUpdate_{availableUpdate.Version}.zip");
        try
        {
            Progress<int> progress = new(pct =>
            {
                if (!IsDisposed) updateProgress.EditValue = pct;
            });
            await updateService.DownloadUpdateAsync(zipPath, progress, cts.Token);
            if (IsDisposed) return;
            updateProgress.EditValue = 100;
            SetUpdateMessage(GetText("UpdateApplying"));
            ApplyUpdate(zipPath, availableUpdate.Version);
        }
        catch (OperationCanceledException)
        {
            SetUpdateMessage(GetText("UpdateCancelled"));
            updateProgress.Visible = false;
        }
        catch (HttpRequestException ex)
        {
            SetUpdateMessage(string.Format(System.Globalization.CultureInfo.CurrentCulture,
                GetText("UpdateNetworkError"), ex.Message));
            updateProgress.Visible = false;
        }
        catch (IOException ex)
        {
            SetUpdateMessage(string.Format(System.Globalization.CultureInfo.CurrentCulture,
                GetText("UpdateIOError"), ex.Message));
            updateProgress.Visible = false;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            SetUpdateMessage(ex.NativeErrorCode == 1223 ? GetText("UpdateCancelled") : ex.Message);
            updateProgress.Visible = false;
        }
        finally
        {
            updateOperation = null;
            updateBusy = false;
            if (!IsDisposed) UpdateUpdateControls();
        }
    }

    private static void ApplyUpdate(string zipPath, string version)
    {
        string installDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
        string operationId = Guid.NewGuid().ToString("N");
        string scriptPath = Path.Combine(Path.GetTempPath(), $"ERSoftApplyUpdate_{operationId}.ps1");
        string exePath = Path.Combine(installDir, "ERSoftSMSBox.exe");
        string extractDir = Path.Combine(Path.GetTempPath(), $"ERSoftExtract_{operationId}");
        int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
        bool requiresElevation = !CanWriteUpdateTo(installDir);

        string script = CreateUpdateScript(zipPath, installDir, exePath, extractDir, pid);
        File.WriteAllText(scriptPath, script, System.Text.Encoding.UTF8);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = true,
            Verb = requiresElevation ? "runas" : "",
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
        });
        Application.Exit();
    }

    private static bool CanWriteUpdateTo(string directory)
    {
        string probe = Path.Combine(directory, $".ersoft-update-{Guid.NewGuid():N}.tmp");
        try
        {
            using FileStream stream = new(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                1, FileOptions.DeleteOnClose);
            return true;
        }
        catch (UnauthorizedAccessException) { return false; }
        catch (IOException) { return false; }
    }

    internal static string CreateUpdateScript(string zipPath, string installDir, string exePath, string extractDir, int pid)
    {
        static string Quote(string value) => value.Replace("'", "''");
        zipPath = Quote(zipPath);
        installDir = Quote(installDir);
        exePath = Quote(exePath);
        extractDir = Quote(extractDir);
        return $@"
Add-Type -AssemblyName System.Windows.Forms
$ErrorActionPreference = 'Stop'
try {{
    $proc = Get-Process -Id {pid} -ErrorAction SilentlyContinue
    if ($proc) {{
        Wait-Process -Id {pid} -Timeout 30 -ErrorAction Stop
    }}
    Start-Sleep -Milliseconds 500
    Expand-Archive -LiteralPath '{zipPath}' -DestinationPath '{extractDir}' -Force
    foreach ($required in @('ERSoftSMSBox.exe', 'ERSoftSMSBox.dll', 'ERSoftSMSBox.runtimeconfig.json', 'ERSoftSMSBox.deps.json')) {{
        if (-not (Test-Path -LiteralPath (Join-Path '{extractDir}' $required))) {{
            throw ""Guncelleme paketi eksik: $required""
        }}
    }}
    Get-ChildItem -LiteralPath '{extractDir}' | Copy-Item -Destination '{installDir}' -Recurse -Force -ErrorAction Stop
    Remove-Item -LiteralPath '{extractDir}' -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath '{zipPath}' -Force -ErrorAction SilentlyContinue
    Start-Process -FilePath '{exePath}'
}} catch {{
    $msg = ""Guncelleme uygulanamadi:`n$($_.Exception.Message)`n`nUygulama yeniden baslatiliyor.""
    [System.Windows.Forms.MessageBox]::Show($msg, ""ERSoftSMSBox Guncelleme Hatasi"", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
    if (Test-Path '{exePath}') {{ Start-Process -FilePath '{exePath}' }}
}}
";
    }

    private BarButtonItem CreateUpdateBadge()
    {
        updateBadge = new BarButtonItem
        {
            Name = "updateBadge",
            Caption = "",
            Alignment = BarItemLinkAlignment.Right,
            Visibility = BarItemVisibility.Never
        };
        updateBadge.ImageOptions.SvgImage = menuImages["Ready"];
        updateBadge.ImageOptions.SvgImageSize = new Size(16, 16);
        updateBadge.ItemClick += (_, _) => ShowUpdate();
        return updateBadge;
    }

    private async Task CheckUpdateSilentlyAsync()
    {
        await Task.Delay(3000);
        if (IsDisposed) return;
        try
        {
            using System.Threading.CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));
            Updates.VersionInfo? info = await updateService.CheckForUpdateAsync(cts.Token);
            if (IsDisposed || info is null) return;
            if (IsNewerVersion(info.Version)) ShowUpdateBadge(info.Version);
        }
        catch { /* arka plan kontrolü sessizce başarısız olabilir */ }
    }

    internal static bool IsNewerVersion(string remote) =>
        Version.TryParse(remote, out Version? r) && Version.TryParse(AppVersion.Current, out Version? c) && r > c;

    private void ShowUpdateBadge(string version)
    {
        if (IsDisposed || updateBadge is null) return;
        updateBadge.Caption = string.Format(System.Globalization.CultureInfo.CurrentCulture,
            GetText("UpdateBadge"), version);
        updateBadge.Visibility = BarItemVisibility.Always;
    }

    private void CancelUpdateOperation(object? sender, EventArgs e) => updateOperation?.Cancel();

    private void UpdateResourcesDisposed(object? sender, EventArgs e) => updateOperation?.Cancel();

    private void UpdateUpdateControls()
    {
        updateCheck.Enabled = !updateBusy;
        updateInstall.Enabled = !updateBusy && availableUpdate is not null;
        updateCancel.Enabled = updateBusy;
    }

    private void SetUpdateMessage(string text)
    {
        if (!IsDisposed) updateStatus.Text = text;
    }
}
