using System.Text;

namespace ERSoftSMSBox.Data;

/// <summary>
/// Reads connection settings the way BBSoft's <c>Tdm</c> data module does: the program folder's <c>Ayarlar.INI</c>
/// supplies <c>[Dizin] SabitlerDizini</c>, and the <c>[SQL]</c> values are read from <c>&lt;SabitlerDizini&gt;\Ayarlar.INI</c>
/// (falling back to the selected file). The BBSoft default SQL user is <c>sa</c>.
/// </summary>
internal static class BBSoftIniImporter
{
    internal const string DefaultSqlUser = "sa";
    private static readonly Encoding TurkishAnsi = GetTurkishEncoding();

    internal static DatabaseSettings ReadFile(string path, DatabaseSettings current)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(current);
        IniValues program = Read(path);
        IniValues sql = program;
        string constantsDirectory = program.Get("Dizin/SabitlerDizini", "");
        if (constantsDirectory.Length > 0)
        {
            string constantsPath = Path.Combine(constantsDirectory, "Ayarlar.INI");
            if (File.Exists(constantsPath) && !string.Equals(Path.GetFullPath(constantsPath), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
            {
                IniValues constants = Read(constantsPath);
                if (constants.HasSection("SQL"))
                {
                    sql = constants;
                }
            }
        }

        return Apply(program, sql, current);
    }

    /// <summary>
    /// Applies the INI values found in <paramref name="text"/> on top of <paramref name="current"/>.
    /// Keys are matched case-insensitively; missing keys keep their current values.
    /// </summary>
    internal static DatabaseSettings Parse(string text, DatabaseSettings current)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(current);
        IniValues values = IniValues.Parse(text);

        return Apply(values, values, current);
    }

    private static DatabaseSettings Apply(IniValues program, IniValues sql, DatabaseSettings current)
    {
        if (!sql.HasSection("SQL"))
        {
            throw new InvalidDataException("INI");
        }
        string userName = sql.Get("SQL/SQLUserName", current.UserName.Length > 0 ? current.UserName : DefaultSqlUser);
        string password = sql.Get("SQL/SQLPassword", current.Password);

        return current with
        {
            Server = sql.Get("SQL/ServerName", current.Server),
            Database = sql.Get("SQL/GenelDatabaseName", current.Database),
            UserName = userName,
            Password = password,
            WindowsAuthentication = false,
            OfficeCode = program.Get("Dizin/OfficeCode", sql.Get("Dizin/OfficeCode", current.OfficeCode))
        };
    }

    private static IniValues Read(string path)
    {
        FileInfo file = new(path);
        if (file.Length > 1024 * 1024)
        {
            throw new InvalidDataException("INI");
        }
        byte[] bytes = File.ReadAllBytes(path);
        string text = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF
            ? Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3)
            : TurkishAnsi.GetString(bytes);

        return IniValues.Parse(text);
    }

    private static Encoding GetTurkishEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        return Encoding.GetEncoding(1254);
    }

    private sealed class IniValues
    {
        private readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        internal static IniValues Parse(string text)
        {
            IniValues result = new();
            string section = "";
            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] is ';' or '#')
                {
                    continue;
                }
                if (line[0] == '[' && line[^1] == ']')
                {
                    section = line[1..^1].Trim();
                    continue;
                }
                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }
                result.values[section + "/" + line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }

            return result;
        }

        internal bool HasSection(string section)
            => values.Keys.Any(key => key.StartsWith(section + "/", StringComparison.OrdinalIgnoreCase));

        internal string Get(string key, string fallback)
            => values.TryGetValue(key, out string? value) && value.Length > 0 ? value : fallback;
    }
}
