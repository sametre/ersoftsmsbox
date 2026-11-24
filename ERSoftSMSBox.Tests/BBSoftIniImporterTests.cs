using ERSoftSMSBox.Data;
using NUnit.Framework;

namespace ERSoftSMSBox.Tests;

[TestFixture]
public class BBSoftIniImporterTests
{
    private const string Ini = """
        [Dizin]
        SabitlerDizini=C:\Source\BBSoft
        OfficeCode=001
        xmltirnak="
        KullaniciAdi=admin

        [SQL]
        ServerName=SERVER
        GenelDatabaseName=BBSoft
        SQLUserName=sa
        SQLPassword=secret-value

        [PageSetup]
        Top=60
        """;

    [Test]
    public void WhenBBSoftIniIsParsedThenSqlAndOfficeValuesAreMapped()
    {
        DatabaseSettings settings = BBSoftIniImporter.Parse(Ini, new DatabaseSettings());

        Assert.Multiple(() =>
        {
            Assert.That(settings.Server, Is.EqualTo("SERVER"));
            Assert.That(settings.Database, Is.EqualTo("BBSoft"));
            Assert.That(settings.UserName, Is.EqualTo("sa"));
            Assert.That(settings.Password, Is.EqualTo("secret-value"));
            Assert.That(settings.OfficeCode, Is.EqualTo("001"));
            Assert.That(settings.WindowsAuthentication, Is.False);
        });
    }

    [Test]
    public void WhenIniOmitsKeysThenCurrentValuesAreKept()
    {
        DatabaseSettings current = new("OLD", "EVDUNYASI", "user", "pw", OfficeCode: "007", TrustServerCertificate: true);

        DatabaseSettings settings = BBSoftIniImporter.Parse("[SQL]\r\nServerName=NEW\r\n", current);

        Assert.That(settings, Is.EqualTo(current with { Server = "NEW" }));
    }

    [Test]
    public void WhenIniOmitsSqlUserThenBBSoftDefaultSaIsUsed()
    {
        DatabaseSettings settings = BBSoftIniImporter.Parse("[SQL]\nServerName=SRV\nSQLPassword=pw\n", new DatabaseSettings());

        Assert.That(settings.UserName, Is.EqualTo("sa"));
    }

    [Test]
    public void WhenProgramIniPointsToSabitlerDiziniThenSqlSectionIsReadFromThere()
    {
        string root = Path.Combine(Path.GetTempPath(), "ERSoftSMSBoxTests", Guid.NewGuid().ToString("N"));
        string constants = Path.Combine(root, "Sabitler");
        Directory.CreateDirectory(constants);
        try
        {
            string programIni = Path.Combine(root, "Ayarlar.INI");
            File.WriteAllText(programIni, $"[Dizin]\r\nSabitlerDizini={constants}\r\nOfficeCode=003\r\n\r\n[SQL]\r\nServerName=LOCAL\r\n");
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            File.WriteAllText(Path.Combine(constants, "Ayarlar.INI"),
                "[SQL]\r\nServerName=DATASRV\r\nGenelDatabaseName=EVDUNYASI\r\nSQLUserName=sa\r\nSQLPassword=pw\r\n",
                System.Text.Encoding.GetEncoding(1254));

            DatabaseSettings settings = BBSoftIniImporter.ReadFile(programIni, new DatabaseSettings());

            Assert.That((settings.Server, settings.Database, settings.UserName, settings.OfficeCode),
                Is.EqualTo(("DATASRV", "EVDUNYASI", "sa", "003")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public void WhenSabitlerDiziniHasNoIniThenSelectedFileIsUsed()
    {
        string root = Path.Combine(Path.GetTempPath(), "ERSoftSMSBoxTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string programIni = Path.Combine(root, "Ayarlar.INI");
            File.WriteAllText(programIni, $"[Dizin]\r\nSabitlerDizini={Path.Combine(root, "Missing")}\r\n[SQL]\r\nServerName=LOCAL\r\nSQLPassword=x\r\n");

            DatabaseSettings settings = BBSoftIniImporter.ReadFile(programIni, new DatabaseSettings());

            Assert.That(settings.Server, Is.EqualTo("LOCAL"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public void WhenKeysUseDifferentCasingThenTheyAreStillRecognized()
    {
        DatabaseSettings settings = BBSoftIniImporter.Parse("[sql]\nservername=X\ngeneldatabasename=Y\n", new DatabaseSettings());

        Assert.That((settings.Server, settings.Database), Is.EqualTo(("X", "Y")));
    }

    [TestCase("")]
    [TestCase("[Dizin]\nOfficeCode=001")]
    [TestCase("not an ini")]
    public void WhenSqlSectionIsMissingThenFileIsRejected(string text)
    {
        Assert.Throws<InvalidDataException>(() => BBSoftIniImporter.Parse(text, new DatabaseSettings()));
    }

    [Test]
    public void WhenSettingsAreFormattedThenPasswordIsNotExposed()
    {
        DatabaseSettings settings = BBSoftIniImporter.Parse(Ini, new DatabaseSettings());

        Assert.That(settings.ToString(), Does.Not.Contain("secret-value"));
    }

    [Test]
    public void WhenConnectionStringIsBuiltThenBBSoftBdeParametersAreMirrored()
    {
        DatabaseSettings settings = BBSoftIniImporter.Parse(Ini, new DatabaseSettings());

        string connectionString = settings.CreateConnectionString();

        Assert.That(connectionString, Does.Contain("Data Source=SERVER").And.Contain("Initial Catalog=BBSoft")
            .And.Contain("User ID=sa").And.Contain("Packet Size=4096").And.Contain("Current Language=Turkish")
            .And.Contain("Encrypt=False").And.Contain("Trust Server Certificate=False")
            .And.Not.Contain("Application Intent"));
    }

    [Test]
    public void WhenQueryTimeoutIsReadThenItMatchesBBSoftMaxQueryTime()
    {
        Assert.That(DatabaseSettings.QueryTimeoutSeconds, Is.EqualTo(300));
    }
}
