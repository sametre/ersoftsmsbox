using ERSoftSMSBox.Messaging;
using NUnit.Framework;

namespace ERSoftSMSBox.Tests;

[TestFixture]
public class MessageCategoryTests
{
    [TestCase(1, "Due5")]
    [TestCase(5, "Due5")]
    [TestCase(6, "Due10")]
    [TestCase(15, "Due15")]
    [TestCase(20, "Due20")]
    [TestCase(30, "Due30")]
    [TestCase(45, "Due45")]
    [TestCase(60, "Due60")]
    [TestCase(61, "Due60Plus")]
    [TestCase(400, "Due60Plus")]
    public void WhenCustomerOwesMoneyThenOverdueDaysMapToOneBucket(int days, string expectedKey)
    {
        Assert.That(MessageCategory.FindDueBucket(days, 100m)?.Key, Is.EqualTo(expectedKey));
    }

    [TestCase(null, 100)]
    [TestCase(0, 100)]
    [TestCase(-3, 100)]
    [TestCase(10, 0)]
    [TestCase(10, -5)]
    public void WhenNothingIsOverdueThenNoBucketApplies(int? days, decimal debt)
    {
        Assert.That(MessageCategory.FindDueBucket(days, debt), Is.Null);
    }

    [Test]
    public void WhenDueBucketsAreListedThenTheyCoverEveryDayWithoutGapsOrOverlaps()
    {
        MessageCategory[] buckets = MessageCategory.All
            .Where(category => category.Kind == MessageCategoryKind.DueRange && category.MaxDays is not null)
            .OrderBy(category => category.MinDays).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(buckets[0].MinDays, Is.EqualTo(1));
            for (int index = 1; index < buckets.Length; index++)
            {
                Assert.That(buckets[index].MinDays, Is.EqualTo(buckets[index - 1].MaxDays + 1), buckets[index].Key);
            }
            Assert.That(MessageCategory.Find("Due60Plus").MinDays, Is.EqualTo(buckets[^1].MaxDays + 1));
        });
    }

    [Test]
    public void WhenCategoryKeysAreListedThenEachHasALocalizedName()
    {
        Assert.That(MessageCategory.All.Select(category => category.Name), Has.None.StartsWith("Category"));
    }

    [Test]
    public void WhenCategoryIsUnknownThenLookupFails()
    {
        Assert.Throws<ArgumentException>(() => MessageCategory.Find("Nope"));
    }

    [Test]
    public void WhenTemplateCategoriesAreListedThenMissingPhoneIsExcluded()
    {
        Assert.That(MessageCategory.WithTemplates.Select(category => category.Kind), Has.None.EqualTo(MessageCategoryKind.MissingPhone));
    }
}
