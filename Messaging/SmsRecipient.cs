namespace ERSoftSMSBox.Messaging;

/// <summary>
/// Holds a read-only customer snapshot used to prepare an SMS preview, not proof of messaging consent.
/// <paramref name="DaysOverdue"/> counts days since the BBSoft reference date; <paramref name="BirthDate"/> comes from Nebim when available.
/// </summary>
public sealed record SmsRecipient(string CustomerCode, string FullName, string OfficeCode,
    string OfficeDescription, string Phone, decimal Debt, DateTime? ReferenceDate,
    int? DaysOverdue = null, int PhoneCount = 0, string? Stage = null, DateTime? LastSentDate = null,
    int SentCount = 0, bool LegalFollowUp = false, DateTime? BirthDate = null, int? DaysToBirthday = null,
    DateTime? LastPaymentDate = null, DateTime? DueDate = null, DateTime? RecordedPaymentDate = null,
    DateTime? LastReminderDate = null)
{
    /// <summary>
    /// Preserves the legacy IS NOT NULL check, including an empty but non-null SSMMesaj value.
    /// This is a workflow flag, not messaging consent.
    /// </summary>
    public bool HasMessageStage => Stage is not null;

    /// <summary>
    /// Matches the legacy Kontrol expression: review a nonzero balance or an existing message stage.
    /// </summary>
    public bool NeedsReminderCheck => Debt != 0 || HasMessageStage;

    /// <summary>
    /// Indicates a changed reference day only when both the current and previously recorded dates exist.
    /// </summary>
    public bool PaymentDateChanged => ReferenceDate.HasValue && RecordedPaymentDate.HasValue
        && ReferenceDate.Value.Date != RecordedPaymentDate.Value.Date;

    /// <summary>
    /// Gets the legacy reminder reference without presenting a fallback date as an actual SMS send.
    /// </summary>
    public DateTime? ReminderReferenceDate => LastReminderDate ?? ReferenceDate;

    /// <summary>
    /// Gets the localized due bucket name for grid display, or an empty string when the customer owes nothing.
    /// </summary>
    public string DueBucket => MessageCategory.FindDueBucket(DaysOverdue, Debt)?.Name ?? "";
}
