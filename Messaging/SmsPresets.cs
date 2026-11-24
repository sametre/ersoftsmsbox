using ERSoftSMSBox.Integrations;

namespace ERSoftSMSBox.Messaging;

/// <summary>
/// One ready-made template shown in the preset picker of the SMS composer.
/// </summary>
public sealed class SmsPreset
{
    internal SmsPreset(string name, string text)
    {
        Name = name;
        Text = text;
    }

    public string Name { get; }
    public string Text { get; }
    public override string ToString() => Name;
}

/// <summary>
/// Built-in preset templates for common debt-collection and customer-service scenarios.
/// </summary>
public static class SmsPresets
{
    /// <summary>Gets the full list of built-in presets in menu order.</summary>
    public static IReadOnlyList<SmsPreset> All { get; } = BuildAll();

    private static IReadOnlyList<SmsPreset> BuildAll() =>
    [
        Make("PresetGenel",     "PresetGenelAd"),
        Make("PresetGecikme",   "PresetGecikmeAd"),
        Make("PresetVade",      "PresetVadeAd"),
        Make("PresetAcil",      "PresetAcilAd"),
        Make("PresetSonUyari",  "PresetSonUyariAd"),
        Make("PresetOdeme",     "PresetOdemeAd"),
        Make("PresetDogumGunu", "PresetDogumGunuAd"),
        Make("PresetIletisim",  "PresetIletisimAd"),
    ];

    private static SmsPreset Make(string textKey, string nameKey) =>
        new(IntegrationText.Get(nameKey), IntegrationText.Get(textKey));
}
