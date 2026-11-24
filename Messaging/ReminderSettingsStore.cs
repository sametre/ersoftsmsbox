using System.Text.Json;
using ERSoftSMSBox.Integrations;

namespace ERSoftSMSBox.Messaging;

/// <summary>
/// Stores the user's preferred reminder interval and same-day local-time window.
/// These preferences alone do not enable or transmit SMS messages.
/// </summary>
public sealed record ReminderSettings
{
    /// <summary>Gets the minimum number of days between reminders.</summary>
    public int RepeatDays { get; init; } = 3;

    /// <summary>Gets the start of the preferred local-time window.</summary>
    public TimeOnly StartTime { get; init; } = new(10, 0);

    /// <summary>Gets the end of the preferred local-time window.</summary>
    public TimeOnly EndTime { get; init; } = new(18, 0);
}

/// <summary>
/// Persists validated reminder preferences atomically for the current Windows user.
/// </summary>
public sealed class ReminderSettingsStore
{
    private readonly string filePath;

    /// <summary>Creates a store in the current user's local application data folder.</summary>
    public ReminderSettingsStore()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ERSoftSMSBox", "reminder-settings.json"))
    {
    }

    /// <summary>Creates a store at the specified file path.</summary>
    public ReminderSettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = Path.GetFullPath(filePath);
    }

    /// <summary>Loads validated preferences, or defaults when the file does not exist.</summary>
    public async Task<ReminderSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                4096, FileOptions.Asynchronous);
            ReminderSettings settings = await JsonSerializer.DeserializeAsync<ReminderSettings>(stream,
                cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException(IntegrationText.Get("ReminderSettingsInvalid"));
            Validate(settings);

            return settings;
        }
        catch (FileNotFoundException)
        {
            return new ReminderSettings();
        }
        catch (DirectoryNotFoundException)
        {
            return new ReminderSettings();
        }
    }

    /// <summary>Validates preferences before replacing the saved file atomically.</summary>
    public async Task SaveAsync(ReminderSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Validate(settings);
        cancellationToken.ThrowIfCancellationRequested();
        string directory = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException();
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            await using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, settings, new JsonSerializerOptions { WriteIndented = true },
                    cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, filePath, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static void Validate(ReminderSettings settings)
    {
        if (settings.RepeatDays is < 1 or > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), IntegrationText.Get("ReminderDaysInvalid"));
        }
        if (settings.StartTime >= settings.EndTime
            || settings.StartTime.Ticks % TimeSpan.TicksPerMinute != 0
            || settings.EndTime.Ticks % TimeSpan.TicksPerMinute != 0)
        {
            throw new ArgumentException(IntegrationText.Get("ReminderHoursInvalid"), nameof(settings));
        }
    }
}
