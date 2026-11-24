using System.Globalization;
using ERSoftSMSBox.Messaging;
using NUnit.Framework;

namespace ERSoftSMSBox.Tests;

[TestFixture]
public class SmsRecipientTests
{
    [TestCase(null, false)]
    [TestCase("", true)]
    [TestCase("1", true)]
    [TestCase("N", true)]
    public void WhenMessageStageIsCheckedThenLegacyNullSemanticsArePreserved(string? stage, bool expected)
    {
        SmsRecipient recipient = CreateRecipient() with { Stage = stage };

        bool actual = recipient.HasMessageStage;

        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(0, null, false)]
    [TestCase(10, null, true)]
    [TestCase(-10, null, true)]
    [TestCase(0, "", true)]
    [TestCase(0, "1", true)]
    public void WhenReminderIsCheckedThenBalanceOrExistingStageRequiresReview(int debt, string? stage, bool expected)
    {
        SmsRecipient recipient = CreateRecipient() with { Debt = debt, Stage = stage };

        bool actual = recipient.NeedsReminderCheck;

        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(null, null, false)]
    [TestCase("2026-01-10", null, false)]
    [TestCase(null, "2026-01-10", false)]
    [TestCase("2026-01-10", "2026-01-10", false)]
    [TestCase("2026-01-11", "2026-01-10", true)]
    [TestCase("2026-01-09", "2026-01-10", true)]
    public void WhenPaymentDateIsCheckedThenOnlyDifferentKnownDatesAreFlagged(string? current, string? recorded, bool expected)
    {
        SmsRecipient recipient = CreateRecipient() with { ReferenceDate = Date(current), RecordedPaymentDate = Date(recorded) };

        bool actual = recipient.PaymentDateChanged;

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void WhenPaymentTimesDifferOnSameDayThenNoDateChangeIsFlagged()
    {
        SmsRecipient recipient = CreateRecipient() with
        {
            ReferenceDate = new DateTime(2026, 1, 10, 12, 0, 0),
            RecordedPaymentDate = new DateTime(2026, 1, 10, 8, 0, 0)
        };

        bool actual = recipient.PaymentDateChanged;

        Assert.That(actual, Is.False);
    }

    [TestCase("2026-01-10", "2026-01-20", "2026-01-20")]
    [TestCase("2026-01-10", null, "2026-01-10")]
    [TestCase(null, "2026-01-20", "2026-01-20")]
    [TestCase(null, null, null)]
    public void WhenReminderReferenceIsReadThenReminderDatePrecedesFallback(string? reference, string? reminder, string? expected)
    {
        SmsRecipient recipient = CreateRecipient() with { ReferenceDate = Date(reference), LastReminderDate = Date(reminder) };

        DateTime? actual = recipient.ReminderReferenceDate;

        Assert.That(actual, Is.EqualTo(Date(expected)));
    }

    [Test]
    public void WhenReminderFallsBackThenActualSentDateRemainsUnknown()
    {
        SmsRecipient recipient = CreateRecipient() with { ReferenceDate = new DateTime(2026, 1, 10) };

        DateTime? actual = recipient.LastSentDate;

        Assert.That(actual, Is.Null);
    }

    private DateTime? Date(string? value)
        => value is null ? null : DateTime.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private SmsRecipient CreateRecipient() => new("C1", "Ali", "O1", "Merkez", "05321234567", 0, null);
}
