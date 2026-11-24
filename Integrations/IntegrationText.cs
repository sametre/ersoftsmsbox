using System.Globalization;
using System.Resources;

namespace ERSoftSMSBox.Integrations;

internal static class IntegrationText
{
    private static readonly ResourceManager Resources = new("ERSoftSMSBox.Form1", typeof(IntegrationText).Assembly);

    internal static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    internal static string Format(string key, params object[] arguments)
        => string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
}
