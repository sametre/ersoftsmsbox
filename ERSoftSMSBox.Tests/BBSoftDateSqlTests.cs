using ERSoftSMSBox.Data;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace ERSoftSMSBox.Tests;

[TestFixture]
public class BBSoftDateSqlTests
{
    [TestCase("datetime")]
    [TestCase("date")]
    public async Task WhenFullCustomerQueryRunsThenNativeDatesReturnCustomerRows(string dateType)
    {
        string? connectionString = Environment.GetEnvironmentVariable("ERSOFT_TEST_SQL_CONNECTION");
        if (string.IsNullOrEmpty(connectionString)) Assert.Ignore("Set ERSOFT_TEST_SQL_CONNECTION to run SQL integration tests.");
        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        using SqlCommand setup = connection.CreateCommand();
        setup.CommandText = $"""
            CREATE TABLE #MusteriAdSoyad(CurrAccCode nvarchar(50),FirstLastName nvarchar(100),OfficeCode nvarchar(50),OfficeDescription nvarchar(100),SSMMesaj nvarchar(20),PaymentDate datetime);
            CREATE TABLE #MusteriTelefonNo(CurrAccCode nvarchar(50),TelefonNo nvarchar(50));
            CREATE TABLE #MusteriKalanBorc(CurrAccCode nvarchar(50),Debit decimal(18,2));
            CREATE TABLE #MusteriSonOdeme(RetailCustomerCode nvarchar(50),ExpiryDate datetime,QuitPaymentDate datetime);
            CREATE TABLE #MusteriSonOdemeTarihi(RetailCustomerCode nvarchar(50),QuitPaymentDate datetime,VadeTarihi {dateType},SonOdemeTarihi {dateType});
            CREATE TABLE #GonderilenMusteriler(CurrAccCode nvarchar(50),MaxSentDate {dateType});
            CREATE TABLE #GonderilenSSMler(CurrAccCode nvarchar(50),QuitPaymentDate datetime);
            CREATE TABLE #AvukatlikKarti(CurrAccCode nvarchar(50));
            INSERT INTO #MusteriAdSoyad VALUES(N'C1',N'Test müşterisi',N'O1',N'Test mağaza',NULL,NULL);
            INSERT INTO #MusteriTelefonNo VALUES(N'C1',N'05321234567');
            INSERT INTO #MusteriKalanBorc VALUES(N'C1',100);
            INSERT INTO #MusteriSonOdemeTarihi VALUES(N'C1',CONVERT(datetime,'20261101',112),@DueValue,@DueValue);
            INSERT INTO #GonderilenMusteriler VALUES(N'C1',@ReminderValue);
            """;
        setup.Parameters.AddWithValue("@DueValue", new DateTime(2026, 11, 20));
        setup.Parameters.AddWithValue("@ReminderValue", new DateTime(2026, 11, 10));
        string sql = BBSoftRepository.GetQueriesForValidation().Single(query => query.Name == "Customers").Sql;
        foreach (string table in new[] { "MusteriAdSoyad", "MusteriTelefonNo", "MusteriKalanBorc", "MusteriSonOdemeTarihi",
            "MusteriSonOdeme", "GonderilenMusteriler", "GonderilenSSMler", "AvukatlikKarti" }.OrderByDescending(name => name.Length))
            sql = sql.Replace("dbo." + table, "#" + table, StringComparison.Ordinal);
        using SqlCommand queryCommand = connection.CreateCommand();
        // Keep temporary table creation and reads in the same parameterized SQL scope.
        queryCommand.CommandText = setup.CommandText + Environment.NewLine + sql;
        foreach (SqlParameter parameter in setup.Parameters)
            queryCommand.Parameters.AddWithValue(parameter.ParameterName, parameter.Value);
        queryCommand.Parameters.AddWithValue("@Take", 2000);
        queryCommand.Parameters.AddWithValue("@Office", "");
        queryCommand.Parameters.AddWithValue("@Search", "");
        foreach (string flag in new[] { "RequirePhone", "IncludeLegal", "MissingPhone", "DueOnly" })
            queryCommand.Parameters.AddWithValue("@" + flag, flag is "RequirePhone" or "IncludeLegal" ? 1 : 0);
        queryCommand.Parameters.Add("@MinDays", System.Data.SqlDbType.Int).Value = DBNull.Value;
        queryCommand.Parameters.Add("@MaxDays", System.Data.SqlDbType.Int).Value = DBNull.Value;

        await using SqlDataReader reader = await queryCommand.ExecuteReaderAsync();

        Assert.That(await reader.ReadAsync(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(reader["FullName"], Is.EqualTo("Test müşterisi"));
            Assert.That(reader["DueDate"], Is.EqualTo(new DateTime(2026, 11, 20)));
            Assert.That(reader["LastReminderDate"], Is.EqualTo(new DateTime(2026, 11, 10)));
        });
    }

    [TestCase("datetime", "2026-11-24T13:45:00", "mdy", "2026-11-24")]
    [TestCase("datetime", "2026-11-24T13:45:00", "dmy", "2026-11-24")]
    [TestCase("datetime2", "2026-11-24T13:45:00", "dmy", "2026-11-24")]
    [TestCase("date", "2026-11-24", "dmy", "2026-11-24")]
    [TestCase("datetime", "1965-06-20T00:00:00", "dmy", "1965-06-20")]
    [TestCase("datetime", null, "dmy", null)]
    public async Task WhenNativeDateIsReadThenItIsConvertedRegardlessOfDateFormat(string sqlType, string? value,
        string dateFormat, string? expected)
    {
        string? connectionString = Environment.GetEnvironmentVariable("ERSOFT_TEST_SQL_CONNECTION");
        if (string.IsNullOrEmpty(connectionString)) Assert.Ignore("Set ERSOFT_TEST_SQL_CONNECTION to run SQL integration tests.");
        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        using SqlCommand command = connection.CreateCommand();
        command.CommandText = $"SET DATEFORMAT {dateFormat}; SELECT {BBSoftRepository.AsDate("value")} " +
            $"FROM (SELECT CONVERT({sqlType}, @value, 126) AS value) AS dates";
        command.Parameters.AddWithValue("@value", (object?)value ?? DBNull.Value);

        object? actual = await command.ExecuteScalarAsync();

        Assert.That(actual is DateTime date ? date.ToString("yyyy-MM-dd") : null, Is.EqualTo(expected));
    }
}
