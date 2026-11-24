namespace ERSoftSMSBox.Integrations;

/// <summary>
/// Eyfelsoft SMS API bağlantı ayarları: PHP endpoint URL'si ve gönderen başlığı.
/// </summary>
public sealed record EyfelSoftSettings(bool IsActive = false, string ApiUrl = "", string Sender = "")
{
    public override string ToString() => $"EyfelSoftSettings {{ IsActive = {IsActive} }}";
}
