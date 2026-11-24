using System.Text.Json;
using ERSoftSMSBox.Integrations;
using ERSoftSMSBox.Messaging;

namespace ERSoftSMSBox;

internal sealed partial class ReminderSettingsDialog : Form
{
    private readonly ReminderSettingsStore store = new();
    private readonly CancellationTokenSource lifetime = new();
    private bool resourcesDisposed;

    internal ReminderSettingsDialog()
    {
        InitializeComponent();
        Text = AccessibleName = IntegrationText.Get("ReminderSettingsTitle");
        description.Text = AccessibleDescription = IntegrationText.Get("ReminderSettingsDescription");
        repeatLabel.Text = repeatDays.AccessibleName = IntegrationText.Get("ReminderRepeatDays");
        startLabel.Text = startTime.AccessibleName = IntegrationText.Get("ReminderStartTime");
        endLabel.Text = endTime.AccessibleName = IntegrationText.Get("ReminderEndTime");
        repeatDays.AccessibleDescription = IntegrationText.Get("ReminderDaysHelp");
        startTime.AccessibleDescription = endTime.AccessibleDescription = IntegrationText.Get("ReminderHoursHelp");
        notice.Text = IntegrationText.Get("ReminderSettingsNotice");
        status.Text = IntegrationText.Get("ReminderSettingsLoading");
        save.Text = save.AccessibleName = IntegrationText.Get("ReminderSettingsSave");
        save.AccessibleDescription = IntegrationText.Get("ReminderSettingsNotice");
        cancel.Text = cancel.AccessibleName = IntegrationText.Get("ReminderSettingsCancel");
        cancel.AccessibleDescription = cancel.Text.Replace("&", "");
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            ReminderSettings settings = await store.LoadAsync(lifetime.Token);
            if (lifetime.IsCancellationRequested)
            {
                return;
            }
            repeatDays.Value = settings.RepeatDays;
            DateOnly date = new(2000, 1, 1);
            startTime.Value = date.ToDateTime(settings.StartTime);
            endTime.Value = date.ToDateTime(settings.EndTime);
            fields.Enabled = save.Enabled = true;
            status.Text = "";
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            // Closing the dialog cancels pending file access.
        }
        catch (IOException) { SetStatus("ReminderSettingsLoadError"); }
        catch (UnauthorizedAccessException) { SetStatus("ReminderSettingsLoadError"); }
        catch (JsonException) { SetStatus("ReminderSettingsLoadError"); }
        catch (ArgumentException) { SetStatus("ReminderSettingsLoadError"); }
    }

    private async void SaveSettingsAsync(object? sender, EventArgs e)
    {
        fields.Enabled = save.Enabled = false;
        SetStatus("ReminderSettingsSaving");
        try
        {
            ReminderSettings settings = new()
            {
                RepeatDays = decimal.ToInt32(repeatDays.Value),
                StartTime = new TimeOnly(startTime.Value.Hour, startTime.Value.Minute),
                EndTime = new TimeOnly(endTime.Value.Hour, endTime.Value.Minute)
            };
            await store.SaveAsync(settings, lifetime.Token);
            if (!lifetime.IsCancellationRequested)
            {
                DialogResult = DialogResult.OK;
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            // Cancel leaves the dialog closed without touching disposed controls.
        }
        catch (ArgumentException exception)
        {
            if (!lifetime.IsCancellationRequested)
            {
                status.Text = exception.Message;
            }
        }
        catch (IOException) { SetStatus("ReminderSettingsSaveError"); }
        catch (UnauthorizedAccessException) { SetStatus("ReminderSettingsSaveError"); }
        finally
        {
            if (!lifetime.IsCancellationRequested)
            {
                fields.Enabled = save.Enabled = true;
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (!e.Cancel)
        {
            lifetime.Cancel();
        }
    }

    private void SetStatus(string key)
    {
        if (!lifetime.IsCancellationRequested)
        {
            status.Text = IntegrationText.Get(key);
        }
    }
}
