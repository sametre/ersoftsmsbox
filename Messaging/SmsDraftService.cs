using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ERSoftSMSBox.Integrations;

namespace ERSoftSMSBox.Messaging;

/// <summary>
/// Represents a preview only; creating this value never sends a message.
/// </summary>
public sealed record SmsDraft(string CustomerCode, string Phone, string Text);

/// <summary>
/// Reports prepared messages and the input rows excluded for invalid or shared phone numbers.
/// </summary>
public sealed record SmsDraftBatch(IReadOnlyList<SmsDraft> Messages, int InvalidPhoneCount, int ConflictingPhoneCount);

/// <summary>
/// Prepares bounded, personalized SMS previews without network or storage access.
/// </summary>
public static class SmsDraftService
{
    private static readonly Regex Placeholder = new(@"\{[^{}]*\}", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>
    /// Gets the placeholders a template may contain.
    /// </summary>
    public static IReadOnlyList<string> Placeholders { get; } = ["{AdSoyad}", "{CariKodu}", "{Ofis}", "{Borc}", "{Gun}", "{DogumGunu}"];

    /// <summary>
    /// Normalizes a Turkish mobile number to 90 plus ten digits; returns null for unsupported input.
    /// This validates formatting only, not ownership, consent, or deliverability.
    /// </summary>
    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length > 40)
        {
            return null;
        }
        string input = phone.Trim();
        StringBuilder digits = new(14);
        for (int index = 0; index < input.Length; index++)
        {
            char character = input[index];
            if (char.IsAsciiDigit(character))
            {
                digits.Append(character);
            }
            else if (!(char.IsWhiteSpace(character) || character is '(' or ')' or '-' || character == '+' && index == 0))
            {
                return null;
            }
        }
        string number = digits.ToString();
        if (input[0] == '+' && (number.Length != 12 || !number.StartsWith("90", StringComparison.Ordinal)))
        {
            return null;
        }
        if (number.StartsWith("0090", StringComparison.Ordinal)) number = number[4..];
        else if (number.Length == 12 && number.StartsWith("90", StringComparison.Ordinal)) number = number[2..];
        else if (number.Length == 11 && number.StartsWith('0')) number = number[1..];

        return number.Length == 10 && number[0] == '5' ? "90" + number : null;
    }

    /// <summary>
    /// Personalizes up to 500 selected rows. Every row sharing a normalized phone number is excluded
    /// to avoid disclosing one customer's balance to another. No messages are transmitted.
    /// </summary>
    public static SmsDraftBatch Prepare(IReadOnlyList<SmsRecipient> recipients, string template,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        if (recipients.Count is < 1 or > 500)
        {
            throw new ArgumentException(IntegrationText.Get("SmsSelectionRequired"), nameof(recipients));
        }
        ValidateTemplate(template);
        cancellationToken.ThrowIfCancellationRequested();
        List<(SmsRecipient Recipient, string Phone)> valid = [];
        int invalid = 0;
        foreach (SmsRecipient recipient in recipients)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(recipient);
            string? phone = NormalizePhone(recipient.Phone);
            if (phone is null)
            {
                invalid++;
                continue;
            }
            valid.Add((recipient, phone));
        }

        List<SmsDraft> messages = [];
        int conflicts = 0;
        foreach (IGrouping<string, (SmsRecipient Recipient, string Phone)> group in valid.GroupBy(item => item.Phone, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (group.Count() > 1)
            {
                conflicts += group.Count();
                continue;
            }
            SmsRecipient recipient = group.Single().Recipient;
            string message = Placeholder.Replace(template, match => match.Value switch
            {
                "{AdSoyad}" => recipient.FullName,
                "{CariKodu}" => recipient.CustomerCode,
                "{Ofis}" => recipient.OfficeDescription,
                "{Borc}" => recipient.Debt.ToString("N2", TurkishCulture),
                "{Gun}" => (recipient.DaysOverdue ?? 0).ToString(TurkishCulture),
                "{DogumGunu}" => recipient.BirthDate?.ToString("dd MMMM", TurkishCulture) ?? "",
                _ => throw new ArgumentException(IntegrationText.Get("SmsTemplateInvalid"), nameof(template))
            });
            if (message.Length > 1000)
            {
                throw new ArgumentException(IntegrationText.Get("SmsDraftTooLong"), nameof(template));
            }
            messages.Add(new SmsDraft(recipient.CustomerCode, group.Key, message));
        }

        return new SmsDraftBatch(messages.AsReadOnly(), invalid, conflicts);
    }

    /// <summary>
    /// Rejects empty, oversized or unknown-placeholder templates.
    /// </summary>
    public static void ValidateTemplate(string template)
    {
        if (string.IsNullOrWhiteSpace(template) || template.Length > 1000)
        {
            throw new ArgumentException(IntegrationText.Get("SmsTemplateInvalid"), nameof(template));
        }
        foreach (Match match in Placeholder.Matches(template))
        {
            if (!Placeholders.Contains(match.Value, StringComparer.Ordinal))
            {
                throw new ArgumentException(IntegrationText.Get("SmsTemplateInvalid"), nameof(template));
            }
        }
        string literal = Placeholder.Replace(template, "");
        if (literal.Contains('{') || literal.Contains('}'))
        {
            throw new ArgumentException(IntegrationText.Get("SmsTemplateInvalid"), nameof(template));
        }
    }
}
