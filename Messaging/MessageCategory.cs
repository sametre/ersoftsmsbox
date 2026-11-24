namespace ERSoftSMSBox.Messaging;

/// <summary>
/// Distinguishes how a category selects customers.
/// </summary>
public enum MessageCategoryKind
{
    /// <summary>Every customer in the office, including legal follow-up accounts.</summary>
    All,
    /// <summary>Customers with a positive balance whose reference date lies in the configured day range.</summary>
    DueRange,
    /// <summary>Customers whose birthday falls within the next N days (requires the Nebim database).</summary>
    Birthday,
    /// <summary>Customers without a mobile phone number.</summary>
    MissingPhone
}

/// <summary>
/// A message category shown on the customer screen. Due-day ranges follow the BBSoft practice of counting
/// days since the last payment / first due date (<c>QuitPaymentDate</c>).
/// </summary>
public sealed record MessageCategory(string Key, MessageCategoryKind Kind, int? MinDays = null, int? MaxDays = null,
    int? BirthdayWindowDays = null)
{
    /// <summary>
    /// Gets the categories in display order.
    /// </summary>
    public static IReadOnlyList<MessageCategory> All { get; } =
    [
        new("AllCustomers", MessageCategoryKind.All),
        new("Overdue", MessageCategoryKind.DueRange, 1, null),
        new("Due5", MessageCategoryKind.DueRange, 1, 5),
        new("Due10", MessageCategoryKind.DueRange, 6, 10),
        new("Due15", MessageCategoryKind.DueRange, 11, 15),
        new("Due20", MessageCategoryKind.DueRange, 16, 20),
        new("Due30", MessageCategoryKind.DueRange, 21, 30),
        new("Due45", MessageCategoryKind.DueRange, 31, 45),
        new("Due60", MessageCategoryKind.DueRange, 46, 60),
        new("Due60Plus", MessageCategoryKind.DueRange, 61, null),
        new("BirthdayToday", MessageCategoryKind.Birthday, BirthdayWindowDays: 0),
        new("BirthdayWeek", MessageCategoryKind.Birthday, BirthdayWindowDays: 7),
        new("BirthdayMonth", MessageCategoryKind.Birthday, BirthdayWindowDays: 30),
        new("MissingPhone", MessageCategoryKind.MissingPhone)
    ];

    /// <summary>
    /// Gets the categories that carry an SMS template.
    /// </summary>
    public static IEnumerable<MessageCategory> WithTemplates
        => All.Where(category => category.Kind is MessageCategoryKind.DueRange or MessageCategoryKind.Birthday or MessageCategoryKind.All);

    /// <summary>
    /// Finds a category by key.
    /// </summary>
    public static MessageCategory Find(string key)
        => All.FirstOrDefault(category => string.Equals(category.Key, key, StringComparison.Ordinal))
            ?? throw new ArgumentException($"Unknown message category '{key}'.", nameof(key));

    /// <summary>
    /// Gets the localized display name.
    /// </summary>
    public string Name => Integrations.IntegrationText.Get("Category" + Key);

    /// <summary>
    /// Returns the due-range category that contains the given number of overdue days, or null when none applies.
    /// </summary>
    public static MessageCategory? FindDueBucket(int? daysOverdue, decimal debt)
    {
        if (daysOverdue is null or < 1 || debt <= 0)
        {
            return null;
        }

        return All.FirstOrDefault(category => category.Kind == MessageCategoryKind.DueRange && category.MaxDays is not null
            && category.MinDays <= daysOverdue && daysOverdue <= category.MaxDays)
            ?? All.First(category => category.Key == "Due60Plus");
    }

    /// <inheritdoc />
    public override string ToString() => Name;
}
