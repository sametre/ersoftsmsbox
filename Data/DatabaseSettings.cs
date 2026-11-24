using Microsoft.Data.SqlClient;
using ERSoftSMSBox.Integrations;

namespace ERSoftSMSBox.Data;

/// <summary>
/// Mirrors the BBSoft <c>Ayarlar.INI</c> layout: <c>[SQL] ServerName / GenelDatabaseName / SQLUserName / SQLPassword</c>
/// and <c>[Dizin] OfficeCode</c>. The connection reproduces the BDE parameters of BBSoft's <c>Tdm::DatabaseAc</c>
/// (<c>MAX QUERY TIME=300</c>, <c>TDS PACKET SIZE=4096</c>, <c>NATIONAL LANG NAME=Turkish</c>, no forced encryption).
/// No schema changes are made.
/// </summary>
internal sealed record DatabaseSettings(
    string Server = "", string Database = "BBSoft", string UserName = "", string Password = "",
    bool WindowsAuthentication = false, bool TrustServerCertificate = false, string OfficeCode = "")
{
    /// <summary>BBSoft's <c>MAX QUERY TIME</c>, in seconds.</summary>
    internal const int QueryTimeoutSeconds = 300;

    internal string CreateConnectionString()
    {
        if (string.IsNullOrWhiteSpace(Server) || string.IsNullOrWhiteSpace(Database)
            || (!WindowsAuthentication && (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password))))
        {
            throw new ArgumentException(IntegrationText.Get("DatabaseSettingsRequired"));
        }

        SqlConnectionStringBuilder builder = new()
        {
            DataSource = Server.Trim(), InitialCatalog = Database.Trim(),
            IntegratedSecurity = WindowsAuthentication, Encrypt = SqlConnectionEncryptOption.Optional,
            TrustServerCertificate = TrustServerCertificate, PersistSecurityInfo = false,
            ApplicationName = "ERSoftSMSBox", PacketSize = 4096, CurrentLanguage = "Turkish",
            ConnectTimeout = 15
        };
        if (!WindowsAuthentication)
        {
            builder.UserID = UserName.Trim();
            builder.Password = Password;
        }

        return builder.ConnectionString;
    }

    public override string ToString() => nameof(DatabaseSettings);
}

/// <summary>
/// Office entry for filters; an empty code means "all offices".
/// </summary>
internal sealed record OfficeOption(string Code, string Description)
{
    public override string ToString() => string.IsNullOrEmpty(Code) ? Description : $"{Code} — {Description}";
}
