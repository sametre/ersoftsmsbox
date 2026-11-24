using System.Security.Cryptography;
using System.Text.Json;

namespace ERSoftSMSBox.Integrations;

/// <summary>
/// Persists one provider's settings encrypted with DPAPI for the current Windows user.
/// Each provider uses its own file under %LocalAppData%\ERSoftSMSBox.
/// </summary>
internal sealed class IntegrationSettingsStore<TSettings> where TSettings : class
{
    private readonly string filePath;
    private readonly Func<TSettings> createDefault;

    internal IntegrationSettingsStore(string fileName, Func<TSettings> createDefault)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(createDefault);
        this.createDefault = createDefault;
        filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ERSoftSMSBox", fileName);
    }

    internal async Task<TSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return createDefault();
        }

        byte[] encrypted = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        byte[] plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
        try
        {
            return JsonSerializer.Deserialize<TSettings>(plain)
                ?? throw new InvalidDataException(IntegrationText.Get("IntegrationStorageInvalid"));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    internal async Task SaveAsync(TSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(settings);
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
        string temporaryPath = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, encrypted, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, filePath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
