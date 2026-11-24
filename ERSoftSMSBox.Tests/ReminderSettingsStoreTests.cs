using ERSoftSMSBox.Messaging;
using NUnit.Framework;

namespace ERSoftSMSBox.Tests;

[TestFixture]
public class ReminderSettingsStoreTests
{
    private string filePath = "";

    [SetUp]
    public void CreateTestPath()
    {
        filePath = Path.Combine(Path.GetTempPath(), "ERSoftSMSBoxTests", Guid.NewGuid().ToString("N"), "reminder-settings.json");
        TestContext.Progress.WriteLine(filePath);
    }

    [Test]
    public async Task WhenFileIsMissingThenDefaultsAreLoaded()
    {
        ReminderSettingsStore store = new(filePath);

        ReminderSettings result = await store.LoadAsync();

        Assert.That(result, Is.EqualTo(new ReminderSettings
        {
            RepeatDays = 3, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(18, 0)
        }));
    }

    [TestCase(1)]
    [TestCase(7)]
    [TestCase(365)]
    public async Task WhenPreferencesAreSavedThenAnotherStoreLoadsThem(int days)
    {
        ReminderSettingsStore store = new(filePath);
        ReminderSettings expected = new()
        {
            RepeatDays = days, StartTime = new TimeOnly(9, 30), EndTime = new TimeOnly(17, 45)
        };

        await store.SaveAsync(expected);
        ReminderSettings actual = await new ReminderSettingsStore(filePath).LoadAsync();

        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(366)]
    public void WhenRepeatDaysAreOutsideLimitsThenSaveIsRejected(int days)
    {
        ReminderSettingsStore store = new(filePath);
        ReminderSettings settings = new() { RepeatDays = days };

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.SaveAsync(settings));
    }

    [TestCase(10, 10)]
    [TestCase(18, 10)]
    public void WhenTimeWindowIsNotIncreasingThenSaveIsRejected(int start, int end)
    {
        ReminderSettingsStore store = new(filePath);
        ReminderSettings settings = new() { StartTime = new TimeOnly(start, 0), EndTime = new TimeOnly(end, 0) };

        Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(settings));
    }

    [Test]
    public void WhenTimeContainsSecondsThenSaveIsRejected()
    {
        ReminderSettingsStore store = new(filePath);
        ReminderSettings settings = new() { StartTime = new TimeOnly(10, 0, 1) };

        Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(settings));
    }

    [Test]
    public async Task WhenSaveIsInvalidThenPreviousSettingsRemainUnchanged()
    {
        ReminderSettingsStore store = new(filePath);
        ReminderSettings original = new() { RepeatDays = 5 };
        await store.SaveAsync(original);

        try
        {
            await store.SaveAsync(new ReminderSettings { RepeatDays = 0 });
        }
        catch (ArgumentOutOfRangeException)
        {
            // The outcome under test is preservation of the previous file.
        }

        Assert.That(await store.LoadAsync(), Is.EqualTo(original));
    }

    [Test]
    public void WhenLoadIsCancelledThenDefaultsAreNotReturned()
    {
        ReminderSettingsStore store = new(filePath);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() => store.LoadAsync(cancellation.Token));
    }

    [Test]
    public void WhenSaveIsCancelledThenOperationIsRejected()
    {
        ReminderSettingsStore store = new(filePath);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() => store.SaveAsync(new ReminderSettings(), cancellation.Token));
    }

    [Test]
    public async Task WhenFileContainsInvalidDaysThenLoadIsRejected()
    {
        ReminderSettingsStore store = new(filePath);
        await store.SaveAsync(new ReminderSettings());
        await File.WriteAllTextAsync(filePath, "{\"RepeatDays\":0}");

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.LoadAsync());
    }

    [Test]
    public async Task WhenFileContainsNullThenLoadIsRejected()
    {
        ReminderSettingsStore store = new(filePath);
        await store.SaveAsync(new ReminderSettings());
        await File.WriteAllTextAsync(filePath, "null");

        Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
    }

    [Test]
    public async Task WhenFileIsMalformedThenLoadIsRejected()
    {
        ReminderSettingsStore store = new(filePath);
        await store.SaveAsync(new ReminderSettings());
        await File.WriteAllTextAsync(filePath, "not json");

        Assert.ThrowsAsync<System.Text.Json.JsonException>(() => store.LoadAsync());
    }
}
