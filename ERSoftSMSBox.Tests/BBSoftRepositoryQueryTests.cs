using ERSoftSMSBox.Data;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using NUnit.Framework;

namespace ERSoftSMSBox.Tests;

[TestFixture]
public class BBSoftRepositoryQueryTests
{
    private static IEnumerable<TestCaseData> Queries()
        => BBSoftRepository.GetQueriesForValidation().Select(query => new TestCaseData(query.Sql).SetName("Query_" + query.Name));

    [TestCaseSource(nameof(Queries))]
    public void WhenQueryIsParsedThenItIsValidTransactSql(string sql)
    {
        TSql120Parser parser = new(initialQuotedIdentifiers: true);

        parser.Parse(new StringReader(sql), out IList<ParseError> errors);

        Assert.That(errors, Is.Empty, string.Join(Environment.NewLine, errors.Select(error => $"{error.Line}:{error.Column} {error.Message}")));
    }

    [TestCaseSource(nameof(Queries))]
    public void WhenQueryIsInspectedThenItOnlyReads(string sql)
    {
        TSql120Parser parser = new(initialQuotedIdentifiers: true);
        TSqlFragment fragment = parser.Parse(new StringReader(sql), out _);
        WriteDetector detector = new();

        fragment.Accept(detector);

        Assert.That(detector.Writes, Is.Empty);
    }

    [TestCaseSource(nameof(Queries))]
    public void WhenQueryIsBuiltThenNoStringParsingIsAppliedToDates(string sql)
    {
        Assert.That(sql, Does.Not.Contain("SUBSTRING").IgnoreCase.And.Not.Contain("ISDATE").IgnoreCase);
    }

    [TestCase("NebimV3", true)]
    [TestCase("Nebim_2024", true)]
    [TestCase("Nebim V3", false)]
    [TestCase("Nebim]; DROP TABLE x; --", false)]
    [TestCase("1Nebim", false)]
    [TestCase("", false)]
    public void WhenNebimDatabaseNameIsCheckedThenOnlyPlainIdentifiersAreAccepted(string name, bool expected)
    {
        Assert.That(BBSoftRepository.IsSafeIdentifier(name), Is.EqualTo(expected));
    }

    [TestCase("Customers")]
    [TestCase("CustomersWithBirthday")]
    public void WhenCustomerDatesAreReadThenLegacySentinelIsConvertedToNull(string name)
    {
        string sql = BBSoftRepository.GetQueriesForValidation().Single(query => query.Name == name).Sql;

        Assert.That(sql, Does.Contain("CONVERT(date, pd.SonOdemeTarihi)")
            .And.Contain("CONVERT(date, pd.VadeTarihi)")
            .And.Contain("CONVERT(date, pd.KontrolTarihi)")
            .And.Contain("CONVERT(date, '19650620', 112)"));
    }

    [TestCase("Customers")]
    [TestCase("CustomersWithBirthday")]
    public void WhenCustomerDatesAreReadThenLegacyPrecomputedTableIsUsed(string name)
    {
        string sql = BBSoftRepository.GetQueriesForValidation().Single(query => query.Name == name).Sql;

        Assert.That(sql, Does.Contain("MAX(QuitPaymentDate) AS KontrolTarihi")
            .And.Contain("FROM dbo.MusteriSonOdemeTarihi GROUP BY RetailCustomerCode")
            .And.Not.Contain("dbo.MusteriSonOdeme "));
    }

    [TestCase("Customers")]
    [TestCase("CustomersWithBirthday")]
    public void WhenRecordedPaymentDateIsReadThenLegacyCustomerPaymentDateIsUsed(string name)
    {
        string sql = BBSoftRepository.GetQueriesForValidation().Single(query => query.Name == name).Sql;

        Assert.That(sql, Does.Contain("CONVERT(date, a.PaymentDate)"));
    }

    [TestCase("Customers")]
    [TestCase("CustomersWithBirthday")]
    public void WhenDueCustomersAreFilteredThenRealDueDateIsRequired(string name)
    {
        string sql = BBSoftRepository.GetQueriesForValidation().Single(query => query.Name == name).Sql;

        Assert.That(sql, Does.Contain("@DueOnly = 0 OR (Debt > 0 AND DueDate IS NOT NULL AND DaysOverdue IS NOT NULL)"));
    }

    [TestCase("Customers")]
    [TestCase("CustomersWithBirthday")]
    public void WhenMessageStageIsSelectedThenNullIsNotReplaced(string name)
    {
        string sql = BBSoftRepository.GetQueriesForValidation().Single(query => query.Name == name).Sql;

        Assert.That(sql, Does.Contain("a.SSMMesaj AS Stage"));
    }

    [TestCase("Customers")]
    [TestCase("CustomersWithBirthday")]
    public void WhenReminderDateIsSelectedThenLegacyReminderSummaryIsRead(string name)
    {
        string sql = BBSoftRepository.GetQueriesForValidation().Single(query => query.Name == name).Sql;

        Assert.That(sql, Does.Contain("CONVERT(date, MaxSentDate)")
            .And.Contain("FROM dbo.GonderilenMusteriler GROUP BY CurrAccCode"));
    }

    private sealed class WriteDetector : TSqlFragmentVisitor
    {
        internal List<string> Writes { get; } = [];

        public override void Visit(InsertStatement node) => Writes.Add("INSERT");
        public override void Visit(UpdateStatement node) => Writes.Add("UPDATE");
        public override void Visit(DeleteStatement node) => Writes.Add("DELETE");
        public override void Visit(MergeStatement node) => Writes.Add("MERGE");
        public override void Visit(ExecuteStatement node) => Writes.Add("EXECUTE");
        public override void Visit(AlterTableStatement node) => Writes.Add("ALTER");
        public override void Visit(DropObjectsStatement node) => Writes.Add("DROP");
        public override void Visit(TruncateTableStatement node) => Writes.Add("TRUNCATE");
    }
}
