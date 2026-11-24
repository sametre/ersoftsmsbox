using ERSoftSMSBox.Messaging;
using NUnit.Framework;

namespace ERSoftSMSBox.Tests;

[TestFixture]
public class MessageTemplateStoreTests
{
    private string directory = "";

    [SetUp]
    public void CreateDirectory()
    {
        directory = Path.Combine(Path.GetTempPath(), "ERSoftSMSBoxTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
    }

    [TearDown]
    public void RemoveDirectory()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task WhenNothingIsSavedThenEveryTemplateCategoryHasADefault()
    {
        MessageTemplateStore store = new(Path.Combine(directory, "templates.json"));

        IReadOnlyDictionary<string, string> templates = await store.LoadAsync();

        Assert.That(templates.Keys, Is.EquivalentTo(MessageCategory.WithTemplates.Select(category => category.Key)));
    }

    [Test]
    public void WhenDefaultsAreRenderedThenDueTemplateUsesDaysAndBalance()
    {
        string template = MessageTemplateStore.DefaultFor(MessageCategory.Find("Due15"));

        Assert.That(template, Does.Contain("{Gun}").And.Contain("{Borc}").And.Contain("{AdSoyad}"));
    }

    [Test]
    public async Task WhenTemplatesAreSavedThenTheyAreLoadedBack()
    {
        MessageTemplateStore store = new(Path.Combine(directory, "templates.json"));
        Dictionary<string, string> templates = new((await store.LoadAsync()).ToDictionary(pair => pair.Key, pair => pair.Value))
        {
            ["BirthdayToday"] = "Nice {AdSoyad} {DogumGunu}"
        };

        await store.SaveAsync(templates);
        IReadOnlyDictionary<string, string> loaded = await store.LoadAsync();

        Assert.That(loaded["BirthdayToday"], Is.EqualTo("Nice {AdSoyad} {DogumGunu}"));
    }

    [Test]
    public void WhenTemplateHasUnknownPlaceholderThenSaveIsRejected()
    {
        MessageTemplateStore store = new(Path.Combine(directory, "templates.json"));

        Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(new Dictionary<string, string> { ["Due5"] = "{Unknown}" }));
    }

    [Test]
    public void WhenCategoryKeyIsUnknownThenSaveIsRejected()
    {
        MessageTemplateStore store = new(Path.Combine(directory, "templates.json"));

        Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(new Dictionary<string, string> { ["Nope"] = "Text" }));
    }

    [Test]
    public async Task WhenSavedFileContainsInvalidEntriesThenDefaultsAreUsedForThem()
    {
        string path = Path.Combine(directory, "templates.json");
        await File.WriteAllTextAsync(path, """{"Due5":"","Unknown":"x","Due10":"Custom {Gun}"}""");
        MessageTemplateStore store = new(path);

        IReadOnlyDictionary<string, string> templates = await store.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(templates["Due5"], Is.EqualTo(MessageTemplateStore.DefaultFor(MessageCategory.Find("Due5"))));
            Assert.That(templates["Due10"], Is.EqualTo("Custom {Gun}"));
            Assert.That(templates.ContainsKey("Unknown"), Is.False);
        });
    }
}
