using System.Security.Cryptography;
using System.Text;
using ERSoftSMSBox.Integrations;
using NUnit.Framework;

namespace ERSoftSMSBox.Tests;

[TestFixture]
public class EyfelSoftSettingsTests
{
    [Test]
    public async Task WhenProviderIsRecreatedThenEnabledStatusAndAllCredentialsRemainEncrypted()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ERSoftSmsSettingsTests", Guid.NewGuid().ToString("N"));
        EyfelSoftProvider provider = new(directory);
        SmsProviderDraft expected = new(true, "https://sms.example.test/sms.asmx", "private-password", "TEST",
            "private-user", "vendor-code");
        await provider.SaveAsync(expected, CancellationToken.None);

        SmsProviderDraft actual = await new EyfelSoftProvider(directory).LoadAsync(CancellationToken.None);
        byte[] encrypted = await File.ReadAllBytesAsync(Path.Combine(directory, "eyfelsoft.dat"));

        Assert.Multiple(() =>
        {
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(Encoding.UTF8.GetString(encrypted), Does.Not.Contain("private-password").And.Not.Contain("private-user"));
            Assert.That(actual.ToString(), Does.Not.Contain("private-password"));
        });
    }

    [Test]
    public async Task WhenOldPhpSettingsAreLoadedThenNoPasswordIsRequiredAndActivityIsPreserved()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ERSoftSmsSettingsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        byte[] plain = Encoding.UTF8.GetBytes("""{"IsActive":true,"ApiUrl":"https://sms.example.test/account.send.php","Sender":"TEST"}""");
        await File.WriteAllBytesAsync(Path.Combine(directory, "eyfelsoft.dat"),
            ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser));
        EyfelSoftProvider provider = new(directory);

        SmsProviderDraft loaded = await provider.LoadAsync(CancellationToken.None);
        await provider.SaveAsync(loaded, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(provider.CanCheckAccount(loaded), Is.True);
            Assert.That(loaded.IsActive, Is.True);
            Assert.That(loaded.Secret, Is.Empty);
            Assert.That(loaded.Sender, Is.EqualTo("TEST"));
        });
    }
}
