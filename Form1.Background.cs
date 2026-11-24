namespace ERSoftSMSBox;

public partial class Form1
{
    private NotifyIcon? trayIcon;
    private bool exitRequested;
    private bool backgroundNoticeShown;
    private FormWindowState restoredWindowState = FormWindowState.Normal;

    private void ConfigureBackgroundMode()
    {
        components ??= new System.ComponentModel.Container();
        ContextMenuStrip menu = new(components);
        ToolStripMenuItem open = new(GetText("TrayOpen"))
        {
            AccessibleName = GetText("TrayOpen"),
            AccessibleDescription = GetText("TrayOpenDescription")
        };
        ToolStripMenuItem exit = new(GetText("TrayExit"))
        {
            AccessibleName = GetText("TrayExit"),
            AccessibleDescription = GetText("TrayExitDescription")
        };
        open.Click += RestoreFromTray;
        exit.Click += ExitFromTray;
        menu.Items.Add(open);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);

        trayIcon = new NotifyIcon(components)
        {
            Text = GetText("AppTitle"),
            Icon = Icon ?? SystemIcons.Application,
            ContextMenuStrip = menu
        };
        trayIcon.DoubleClick += RestoreFromTray;
        trayIcon.BalloonTipClicked += RestoreFromTray;
    }

   
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (trayIcon is not null)
        {
            trayIcon.Visible = true;
        }
        _ = CheckUpdateSilentlyAsync();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (trayIcon is null || Disposing || IsDisposed)
        {
            return;
        }
        if (WindowState == FormWindowState.Minimized)
        {
            HideToTray();
        }
        else
        {
            restoredWindowState = WindowState;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        // Session shutdown and application exit must still be allowed.
        if (!exitRequested && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
        }
    }

    private void HideToTray()
    {
        if (trayIcon is null)
        {
            return;
        }
        trayIcon.Visible = true;
        Hide();
        if (!backgroundNoticeShown)
        {
            backgroundNoticeShown = true;
            trayIcon.ShowBalloonTip(2500, GetText("AppTitle"), GetText("TrayBackground"), ToolTipIcon.Info);
        }
    }

    private void RestoreFromTray(object? sender, EventArgs e)
    {
        Show();
        WindowState = restoredWindowState;
        Activate();
    }

    private void ExitFromTray(object? sender, EventArgs e)
    {
        RestoreFromTray(sender, e);
        if (MessageBox.Show(this, GetText("TrayExitConfirm"), GetText("AppTitle"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        exitRequested = true;
        Close();
        if (!IsDisposed)
        {
            exitRequested = false;
        }
    }
}
