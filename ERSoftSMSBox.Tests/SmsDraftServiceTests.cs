using ERSoftSMSBox.Messaging;
using NUnit.Framework;

namespace ERSoftSMSBox.Tests;

[TestFixture]
public class SmsDraftServiceTests
{
    private readonly SmsRecipient recipient = new("C001", "Test User", "OF1", "Test Office", "05550102030", 1234.5m, null);

    [Test]
    public void WhenLocalMobileNumberIsProvidedThenCountryCodeIsAdded()
    {
        string? number = SmsDraftService.NormalizePhone("0555 010 20 30");

        Assert.That(number, Is.EqualTo("905550102030"));
    }

    [TestCase("5550102030")]
    [TestCase("05550102030")]
    [TestCase("905550102030")]
    [TestCase("+90 (555) 010-20-30")]
    [TestCase("0090 555 010 20 30")]
    public void WhenSupportedMobileFormatIsUsedThenNumberIsNormalized(string phone)
    {
        Assert.That(SmsDraftService.NormalizePhone(phone), Is.EqualTo("905550102030"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("02120102030")]
    [TestCase("555010203")]
    [TestCase("55501020300")]
    [TestCase("+49 555 010 20 30")]
    [TestCase("+5550102030")]
    [TestCase("+05550102030")]
    [TestCase("5550102030x")]
    [TestCase("5550102030;5550102031")]
    [TestCase("5+550102030")]
    public void WhenMobileFormatIsInvalidThenNumberIsRejected(string? phone)
    {
        Assert.That(SmsDraftService.NormalizePhone(phone), Is.Null);
    }

    [Test]
    public void WhenTemplateIsPersonalizedThenAllSupportedFieldsAreRendered()
    {
        SmsDraftBatch result = SmsDraftService.Prepare([recipient], "{AdSoyad}|{CariKodu}|{Ofis}|{Borc}");

        Assert.That(result.Messages.Single().Text, Is.EqualTo("Test User|C001|Test Office|1.234,50"));
    }

    [Test]
    public void WhenOverdueDaysAndBirthdayAreKnownThenTheyAreRendered()
    {
        SmsRecipient detailed = recipient with { DaysOverdue = 17, BirthDate = new DateTime(1990, 3, 5) };

        SmsDraftBatch result = SmsDraftService.Prepare([detailed], "{Gun} gün|{DogumGunu}");

        Assert.That(result.Messages.Single().Text, Is.EqualTo("17 gün|05 Mart"));
    }

    [Test]
    public void WhenOverdueDaysAreUnknownThenZeroIsRendered()
    {
        SmsDraftBatch result = SmsDraftService.Prepare([recipient], "{Gun}|{DogumGunu}");

        Assert.That(result.Messages.Single().Text, Is.EqualTo("0|"));
    }

    [Test]
    public void WhenCustomerDataContainsTokenThenItIsNotEvaluatedAgain()
    {
        SmsDraftBatch result = SmsDraftService.Prepare([recipient with { FullName = "{Borc}" }], "{AdSoyad} {Borc}");

        Assert.That(result.Messages.Single().Text, Is.EqualTo("{Borc} 1.234,50"));
    }

    [Test]
    public void WhenDraftIsPreparedThenPhoneIsNormalized()
    {
        SmsDraftBatch result = SmsDraftService.Prepare([recipient], "Test");

        Assert.That(result.Messages.Single().Phone, Is.EqualTo("905550102030"));
    }

    [Test]
    public void WhenPhoneIsInvalidThenInvalidCountIsReported()
    {
        SmsDraftBatch result = SmsDraftService.Prepare([recipient with { Phone = "invalid" }], "Test");

        Assert.That(result.InvalidPhoneCount, Is.EqualTo(1));
    }

    [Test]
    public void WhenPhoneIsInvalidThenNoMessageIsPrepared()
    {
        SmsDraftBatch result = SmsDraftService.Prepare([recipient with { Phone = "invalid" }], "Test");

        Assert.That(result.Messages, Is.Empty);
    }

    [Test]
    public void WhenTwoCustomersShareNormalizedPhoneThenBothAreExcluded()
    {
        SmsDraftBatch result = SmsDraftService.Prepare(
            [recipient, recipient with { CustomerCode = "C002", Phone = "+90 555 010 20 30" }], "{Borc}");

        Assert.That(result.Messages, Is.Empty);
    }

    [Test]
    public void WhenTwoCustomersShareNormalizedPhoneThenBothConflictsAreCounted()
    {
        SmsDraftBatch result = SmsDraftService.Prepare(
            [recipient, recipient with { CustomerCode = "C002", Phone = "+90 555 010 20 30" }], "{Borc}");

        Assert.That(result.ConflictingPhoneCount, Is.EqualTo(2));
    }

    [Test]
    public void WhenPhonesDifferThenBothMessagesArePrepared()
    {
        SmsDraftBatch result = SmsDraftService.Prepare(
            [recipient, recipient with { CustomerCode = "C002", Phone = "05550102031" }], "{CariKodu}");

        Assert.That(result.Messages.Select(message => message.CustomerCode), Is.EqualTo(new[] { "C001", "C002" }));
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("{Unknown}")]
    [TestCase("{AdSoyad")]
    [TestCase("AdSoyad}")]
    [TestCase("{{AdSoyad}}")]
    public void WhenTemplateIsInvalidThenPreparationIsRejected(string template)
    {
        Assert.Throws<ArgumentException>(() => SmsDraftService.Prepare([recipient], template));
    }

    [Test]
    public void WhenTemplateExceedsLimitThenPreparationIsRejected()
    {
        Assert.Throws<ArgumentException>(() => SmsDraftService.Prepare([recipient], new string('a', 1001)));
    }

    [Test]
    public void WhenPersonalizedTextExceedsLimitThenPreparationIsRejected()
    {
        SmsRecipient longName = recipient with { FullName = new string('a', 1001) };

        Assert.Throws<ArgumentException>(() => SmsDraftService.Prepare([longName], "{AdSoyad}"));
    }

    [Test]
    public void WhenSelectionIsEmptyThenPreparationIsRejected()
    {
        Assert.Throws<ArgumentException>(() => SmsDraftService.Prepare([], "Test"));
    }

    [Test]
    public void WhenSelectionExceedsLimitThenPreparationIsRejected()
    {
        SmsRecipient[] recipients = Enumerable.Repeat(recipient, 501).ToArray();

        Assert.Throws<ArgumentException>(() => SmsDraftService.Prepare(recipients, "Test"));
    }

    [Test]
    public void WhenSelectionIsAtLimitThenPreparationIsAllowed()
    {
        SmsRecipient[] recipients = Enumerable.Repeat(recipient, 500).ToArray();

        Assert.DoesNotThrow(() => SmsDraftService.Prepare(recipients, "Test"));
    }

    [Test]
    public void WhenCancelledThenPreparationStops()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => SmsDraftService.Prepare([recipient], "Test", cancellation.Token));
    }
}
