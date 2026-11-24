using System.Text.Json;
using ERSoftSMSBox.Integrations;

namespace ERSoftSMSBox.Messaging;

/// <summary>
/// Stores one SMS template per message category in %LocalAppData%\ERSoftSMSBox\templates.json.
/// Defaults follow the wording of the legacy BBSoft reminder.
/// </summary>
public sealed class MessageTemplateStore
{
    private readonly string filePath;

    /// <summary>
    /// Creates a store at the default location.
    /// </summary>
    public MessageTemplateStore()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ERSoftSMSBox", "templates.json"))
    {
    }

    /// <summary>
    /// Creates a store at a custom path (used by tests).
    /// </summary>
    public MessageTemplateStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = filePath;
    }

    /// <summary>
    /// Gets the built-in template for a category.
    /// </summary>
    public static string DefaultFor(MessageCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);

        return category.Kind switch
        {
            MessageCategoryKind.Birthday => IntegrationText.Get("TemplateDefaultBirthday"),
            MessageCategoryKind.DueRange => IntegrationText.Get("TemplateDefaultDue"),
            _ => IntegrationText.Get("TemplateDefaultGeneral")
        };
    }

    /// <summary>
    /// Loads saved templates merged over the defaults for every category that carries a template.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> LoadAsync(CancellationToken cancellationToken = default)
    {
        Dictionary<string, string> templates = MessageCategory.WithTemplates.ToDictionary(category => category.Key, DefaultFor, StringComparer.Ordinal);
        if (!File.Exists(filePath))
        {
            return templates;
        }
        await using FileStream stream = File.OpenRead(filePath);
        Dictionary<string, string>? saved = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach ((string key, string template) in saved ?? [])
        {
            if (templates.ContainsKey(key) && !string.IsNullOrWhiteSpace(template) && template.Length <= 1000)
            {
                templates[key] = template;
            }
        }

        return templates;
    }

    /// <summary>
    /// Validates and saves the templates atomically.
    /// </summary>
    public async Task SaveAsync(IReadOnlyDictionary<string, string> templates, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(templates);
        Dictionary<string, string> valid = [];
        foreach ((string key, string template) in templates)
        {
            MessageCategory category = MessageCategory.Find(key);
            SmsDraftService.ValidateTemplate(template);
            valid[category.Key] = template;
        }
        string directory = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException();
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            await using (FileStream stream = File.Create(temporary))
            {
                await JsonSerializer.SerializeAsync(stream, valid, new JsonSerializerOptions { WriteIndented = true },
                    cancellationToken).ConfigureAwait(false);
            }
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
}
