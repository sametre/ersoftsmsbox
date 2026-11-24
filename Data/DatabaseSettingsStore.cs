using System.Security.Cryptography;
using System.Text.Json;
using ERSoftSMSBox.Integrations;

namespace ERSoftSMSBox.Data;

internal sealed class DatabaseSettingsStore
{
    private readonly string filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ERSoftSMSBox", "database.dat");

    internal async Task<DatabaseSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return new DatabaseSettings();
        }

        byte[] encrypted = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        byte[] plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
        try
        {
            return JsonSerializer.Deserialize<DatabaseSettings>(plain)
                ?? throw new InvalidDataException(IntegrationText.Get("DatabaseStorageError"));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    internal async Task SaveAsync(DatabaseSettings settings, CancellationToken cancellationToken)
    {
        _ = settings.CreateConnectionString();
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(settings.WindowsAuthentication
            ? settings with { UserName = "", Password = "" } : settings);
        byte[] encrypted;
        try
        {
            encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }

        string directory = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException();
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(temporary, encrypted, cancellationToken).ConfigureAwait(false);
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
}
