using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using ERSoftSMSBox.Integrations;
using ERSoftSMSBox.Messaging;

namespace ERSoftSMSBox.Data;

/// <summary>
/// Values of <c>BBSoft.dbo.EkranTanimlama</c>: BBSoft's message stage texts, stage day intervals,
/// the Nebim source database and the guarantor flag.
/// </summary>
internal sealed record BBSoftScreenSettings(IReadOnlyList<string> StageMessages, IReadOnlyList<int> StageDays,
    string NebimDatabase, bool SendToGuarantor)
{
    public override string ToString() => $"BBSoftScreenSettings {{ NebimDatabase = {NebimDatabase} }}";
}

/// <summary>
/// Per-office counters shown on the office overview.
/// </summary>
internal sealed record OfficeSummary(string OfficeCode, string OfficeDescription, int CustomerCount, int WithPhoneCount,
    int DebtorCount, decimal TotalDebt, int LegalCount, int SentCount, DateTime? LastSentDate);

internal sealed record CustomerFilter(string OfficeCode, string Search, MessageCategory Category, bool RequirePhone,
    string NebimDatabase)
{
    internal void Validate()
    {
        if (OfficeCode.Length > 50 || Search.Length > 100 || NebimDatabase.Length > 128
            || (NebimDatabase.Length > 0 && !BBSoftRepository.IsSafeIdentifier(NebimDatabase)))
        {
            throw new ArgumentException(IntegrationText.Get("DatabaseFilterInvalid"));
        }
    }
}

internal sealed record PhoneEntry(string OfficeCode, string OfficeDescription, string CustomerCode, string FullName,
    string Description, string Phone);

internal sealed record SentMessageFilter(string OfficeCode, string Search, DateTime? FromDate, DateTime? ToDate)
{
    internal void Validate()
    {
        if (OfficeCode.Length > 50 || Search.Length > 100 || FromDate > ToDate)
        {
            throw new ArgumentException(IntegrationText.Get("DatabaseFilterInvalid"));
        }
    }
}

/// <summary>
/// One row of <c>GonderilenSSMler</c>; the legacy table stores no message body, so <see cref="Category"/> is derived.
/// </summary>
internal sealed record SentMessage(string CustomerCode, string FullName, string OfficeCode, string OfficeDescription,
    string Phone, string PhoneDescription, decimal Debt, DateTime? ReferenceDate, DateTime? SentDate, string SentTime,
    string SentId)
{
    public int? DaysOverdue => SentDate is null || ReferenceDate is null ? null : (SentDate.Value.Date - ReferenceDate.Value.Date).Days;

    public string Category => MessageCategory.FindDueBucket(DaysOverdue, Debt)?.Name
        ?? IntegrationText.Get(Debt > 0 ? "CategoryOverdue" : "CategoryAllCustomers");
}

internal sealed record SentMessageSummary(string OfficeCode, string OfficeDescription, DateTime? SentDate, int Count);

/// <summary>
/// One row of <c>GonderilenDTler</c> — includes the message body and delivery status.
/// </summary>
internal sealed record SentDtMessage(string CustomerCode, string FullName, string OfficeCode, string OfficeDescription,
    string Phone, string PhoneDescription, decimal Debt, DateTime? ReferenceDate, DateTime? SentDate, string SentTime,
    string SentId, string Message, string Delivery);

/// <summary>
/// Summary row from <c>GonderilenMusteriler</c> tracking first/last SMS contact per customer.
/// </summary>
internal sealed record SentCustomerRecord(string CustomerCode, DateTime? MinSentDate, DateTime? MaxSentDate);

/// <summary>
/// One row from <c>Kampanyalar</c>.
/// </summary>
internal sealed record Campaign(DateTime? CampaignDate, string Message);

/// <summary>
/// One row of <c>KampanyaSSMler</c> — an SMS sent as part of a campaign.
/// </summary>
internal sealed record CampaignSms(string CustomerCode, string FullName, string OfficeCode, string OfficeDescription,
    string Phone, string PhoneDescription, DateTime? SentDate, string SentTime, string SentId, string Message,
    string ContactType, string GuarantorName, string GuarantorPhone, int CampaignCount);

/// <summary>
/// One row from <c>Kullanici</c>. Password is intentionally not exposed.
/// </summary>
internal sealed record UserAccount(string UserName, string FullName, string Department, string Role,
    string ComputerName, bool IsAdmin);

/// <summary>
/// One row from <c>Modul</c>.
/// </summary>
internal sealed record ModuleEntry(string Module, double SectionCode, string SectionDescription);

/// <summary>
/// One row from <c>MusteriDogumTarihi</c> — BBSoft-native birthday table.
/// </summary>
internal sealed record CustomerBirthDate(string CustomerCode, DateOnly? BirthDate);

/// <summary>
/// One row from <c>MusteriKefilKarti</c> — guarantor contact for a customer.
/// </summary>
internal sealed record GuarantorCard(string CustomerCode, string ContactType, string GuarantorName, string GuarantorPhone);

/// <summary>
/// One row from <c>MusteriSonOdeme</c> — newer payment/due date record.
/// </summary>
internal sealed record CustomerLastPayment(string CustomerCode, DateTime? ExpiryDate, DateTime? QuitPaymentDate);

/// <summary>
/// One row from <c>Sirket</c>.
/// </summary>
internal sealed record CompanyInfo(string CompanyCode, string CompanyName);

/// <summary>
/// One row from <c>Yetki</c> — user permission for one module section.
/// </summary>
internal sealed record PermissionEntry(string Module, double SectionCode, string SectionDescription, string UserName,
    double Level, bool Show, bool New, bool Edit, bool Delete, bool Print, bool Email, bool Transfer,
    bool DefineField, bool DefineScreen);

internal sealed record Page<T>(IReadOnlyList<T> Items, bool HasMore);

/// <summary>
/// Read-only queries against all BBSoft intermediary tables. Nothing is written.
/// </summary>
internal sealed class BBSoftRepository
{
    internal const int PageSize = 2000;
    private static readonly Regex Identifier = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

    internal static bool IsSafeIdentifier(string name) => Identifier.IsMatch(name);

    // Every BBSoft date column is datetime/date (verified against the live schema), so no text parsing is needed.
    internal static string AsDate(string col) => $"CONVERT(date, {col})";

    internal async Task<DateTime> ReadServerTimeAsync(DatabaseSettings settings, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await OpenAsync(settings, cancellationToken).ConfigureAwait(false);
        using SqlCommand command = CreateCommand(connection, "SELECT CURRENT_TIMESTAMP AS Zaman");
        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        return result is DateTime dt ? dt : DateTime.Now;
    }

    internal async Task<BBSoftScreenSettings> ReadScreenSettingsAsync(DatabaseSettings settings, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await OpenAsync(settings, cancellationToken).ConfigureAwait(false);
        using SqlCommand command = CreateCommand(connection, "SELECT TOP (1) * FROM dbo.EkranTanimlama");
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new BBSoftScreenSettings([], [], "", false);
        }
        List<string> messages = [];
        List<int> days = [];
        // Live BBSoft uses MesajVadeGunu1..8 / MesajNot1..8 / KefilGonder1..8; older builds used Mesaj1VadeGunu / Mesaj1 / KefilGonder.
        bool guarantor = ReadOptionalBool(reader, "KefilGonder");
        for (int stage = 1; stage <= 8; stage++)
        {
            string dayColumn = HasColumn(reader, $"MesajVadeGunu{stage}") ? $"MesajVadeGunu{stage}" : $"Mesaj{stage}VadeGunu";
            if (!HasColumn(reader, dayColumn)) break;
            days.Add(ReadOptionalInt(reader, dayColumn));
            messages.Add(HasColumn(reader, $"MesajNot{stage}") ? ReadOptionalText(reader, $"MesajNot{stage}") : ReadOptionalText(reader, "Mesaj" + stage));
            guarantor |= ReadOptionalBool(reader, $"KefilGonder{stage}");
        }
        string nebim = ReadOptionalText(reader, "VeriAlinacakDatabases");

        return new BBSoftScreenSettings(messages.AsReadOnly(), days.AsReadOnly(), IsSafeIdentifier(nebim) ? nebim : "", guarantor);
    }

    internal async Task<IReadOnlyList<OfficeSummary>> ReadOfficesAsync(DatabaseSettings settings, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await OpenAsync(settings, cancellationToken).ConfigureAwait(false);
        using SqlCommand command = CreateCommand(connection, OfficeQuery);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<OfficeSummary> offices = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            offices.Add(new OfficeSummary(ReadText(reader, 0), ReadText(reader, 1), ReadInt(reader, 2), ReadInt(reader, 3),
                ReadInt(reader, 4), ReadDecimal(reader, 5), ReadInt(reader, 6), ReadInt(reader, 7), ReadDate(reader, 8)));
        }
        if (offices.Count > 1000)
        {
            throw new InvalidDataException(IntegrationText.Get("DatabaseOfficeLimit"));
        }

        return offices.AsReadOnly();
    }

    internal async Task<Page<SmsRecipient>> ReadCustomersAsync(DatabaseSettings settings, CustomerFilter filter,
        CancellationToken cancellationToken)
    {
        filter.Validate();
        bool birthday = filter.Category.Kind == MessageCategoryKind.Birthday;
        bool useNebimBirthday = birthday && filter.NebimDatabase.Length > 0;
        bool useBBSoftBirthday = birthday && !useNebimBirthday;
        await using SqlConnection connection = await OpenAsync(settings, cancellationToken).ConfigureAwait(false);
        using SqlCommand command = CreateCommand(connection,
            BuildCustomerQuery(useNebimBirthday ? filter.NebimDatabase : "", useBBSoftBirthday));
        command.Parameters.Add("@Take", SqlDbType.Int).Value = PageSize + 1;
        command.Parameters.Add("@Office", SqlDbType.NVarChar, 50).Value = filter.OfficeCode;
        command.Parameters.Add("@Search", SqlDbType.NVarChar, 100).Value = filter.Search.Trim();
        command.Parameters.Add("@RequirePhone", SqlDbType.Bit).Value = filter.RequirePhone || birthday
            || filter.Category.Kind == MessageCategoryKind.DueRange;
        command.Parameters.Add("@MissingPhone", SqlDbType.Bit).Value = filter.Category.Kind == MessageCategoryKind.MissingPhone;
        command.Parameters.Add("@IncludeLegal", SqlDbType.Bit).Value = filter.Category.Kind is MessageCategoryKind.All or MessageCategoryKind.MissingPhone;
        command.Parameters.Add("@DueOnly", SqlDbType.Bit).Value = filter.Category.Kind == MessageCategoryKind.DueRange;
        command.Parameters.Add("@MinDays", SqlDbType.Int).Value = (object?)filter.Category.MinDays ?? DBNull.Value;
        command.Parameters.Add("@MaxDays", SqlDbType.Int).Value = (object?)filter.Category.MaxDays ?? DBNull.Value;
        command.Parameters.Add("@BirthdayDays", SqlDbType.Int).Value = (object?)filter.Category.BirthdayWindowDays ?? DBNull.Value;
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<SmsRecipient> customers = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            customers.Add(new SmsRecipient(ReadText(reader, 0), ReadText(reader, 1), ReadText(reader, 2), ReadText(reader, 3),
                ReadText(reader, 4), ReadDecimal(reader, 6), ReadDate(reader, 7), ReadNullableInt(reader, 8), ReadInt(reader, 5),
                reader.IsDBNull(9) ? null : ReadText(reader, 9), ReadDate(reader, 10), ReadInt(reader, 11), ReadInt(reader, 12) == 1,
                birthday ? ReadDate(reader, reader.GetOrdinal("BirthDate")) : null,
                birthday ? ReadNullableInt(reader, reader.GetOrdinal("DaysToBirthday")) : null,
                ReadDate(reader, reader.GetOrdinal("LastPaymentDate")), ReadDate(reader, reader.GetOrdinal("DueDate")),
                ReadDate(reader, reader.GetOrdinal("RecordedPaymentDate")), ReadDate(reader, reader.GetOrdinal("LastReminderDate"))));
        }

        return ToPage(customers);
    }

    internal async Task<Page<PhoneEntry>> ReadPhonesAsync(DatabaseSettings settings, string officeCode, string search,
        CancellationToken cancellationToken)
    {
        if (officeCode.Length > 50 || search.Length > 100)
        {
            throw new ArgumentException(IntegrationText.Get("DatabaseFilterInvalid"));
        }
        await using SqlConnection connection = await OpenAsync(settings, cancellationToken).ConfigureAwait(false);
        using SqlCommand command = CreateCommand(connection, PhoneQuery);
        command.Parameters.Add("@Take", SqlDbType.Int).Value = PageSize + 1;
        command.Parameters.Add("@Office", SqlDbType.NVarChar, 50).Value = officeCode;
        command.Parameters.Add("@Search", SqlDbType.NVarChar, 100).Value = search.Trim();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<PhoneEntry> phones = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            phones.Add(new PhoneEntry(ReadText(reader, 0), ReadText(reader, 1), ReadText(reader, 2), ReadText(reader, 3),
                ReadText(reader, 4), ReadText(reader, 5)));
        }

        return ToPage(phones);
    }

    internal async Task<IReadOnlyList<SentMessageSummary>> ReadSentSummaryAsync(DatabaseSettings settings, SentMessageFilter filter,
        CancellationToken cancellationToken)
    {
        filter.Validate();
        await using SqlConnection connection = await OpenAsync(settings, cancellationToken).ConfigureAwait(false);
        using SqlCommand command = CreateCommand(connection, SentSummaryQuery);
        AddSentParameters(command, filter);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<SentMessageSummary> rows = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new SentMessageSummary(ReadText(reader, 0), ReadText(reader, 1), ReadDate(reader, 2), ReadInt(reader, 3)));
        }

        return rows.AsReadOnly();
    }

    internal async Task<Page<SentMessage>> ReadSentMessagesAsync(DatabaseSettings settings, SentMessageFilter filter,
        CancellationToken cancellationToken)
    {
        filter.Validate();
        await using SqlConnection connection = await OpenAsync(settings, cancellationToken).ConfigureAwait(false);
        using SqlCommand command = CreateCommand(connection, SentDetailQuery);
        command.Parameters.Add("@Take", SqlDbType.Int).Value = PageSize + 1;
        AddSentParameters(command, filter);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<SentMessage> rows = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new SentMessage(ReadText(reader, 0), ReadText(reader, 1), ReadText(reader, 2), ReadText(reader, 3),
                ReadText(reader, 4), ReadText(reader, 5), ReadDecimal(reader, 6), ReadDate(reader, 7), ReadDate(reader, 8),
                ReadText(reader, 9), ReadText(reader, 10)));
        }

        return ToPage(rows);
    }

    internal async Task<Page<SentDtMessage>> ReadSentDtMessagesAsync(DatabaseSettings settings, SentMessageFilter filter,
        CancellationToken cancellationToken)
    {
        filter.Validate();
        await using SqlConnection connection = await OpenAsync(settings, cancellationToken).ConfigureAwait(false);
        using SqlCommand command = CreateCommand(connection, SentDtDetailQuery);
        command.Parameters.Add("@Take", SqlDbType.Int).Value = PageSize + 1;
        AddSentParameters(command, filter);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<SentDtMessage> rows = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new SentDtMessage(ReadText(reader, 0), ReadText(reader, 1), ReadText(reader, 2), ReadText(reader, 3),
                ReadText(reader, 4), ReadText(reader, 5), ReadDecimal(reader, 6), ReadDate(reader, 7), ReadDate(reader, 8),
                ReadText(reader, 9), ReadText(reader, 10), ReadText(reader, 11), ReadText(reader, 12)));
        }

        return ToPage(rows);
    }

    internal async Task<IReadOnlyList<SentMessageSummary>> ReadSentDtSummaryAsync(DatabaseSettings settings, SentMessageFilter filter,
        CancellationToken cancellationToken)
    {
        filter.Validate();
        await using SqlConnection connection = await OpenAsync(settings, cancellationToken).ConfigureAwait(false);
        using SqlCommand command = CreateCommand(connection, SentDtSummaryQuery);
        AddSentParameters(command, filter);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<SentMessageSummary> rows = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new SentMessageSummary(ReadText(reader, 0), ReadText(reader, 1), ReadDate(reader, 2), ReadInt(reader, 3)));
        }

        return rows.AsReadOnly();
    }

    internal async Task<IReadOnlyList<SentCustomerRecord>> ReadSentCustomersAsync(DatabaseSettings settings,
        string officeCode, CancellationToken cancellationToken)
    {
        if (officeCode.Length > 50) throw new ArgumentException(IntegrationText.Get("DatabaseFilterInvalid"));
        await using SqlConnection connection = await OpenAsync(settings, cancellationToken).ConfigureAwait(false);
        using SqlCommand command = CreateCommand(connection, SentCustomersQuery);
        command.Parameters.Add("@Office", SqlDbType.NVarChar, 50).Value = officeCode;
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<SentCustomerRecord> rows = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new SentCustomerRecord(ReadText(reader, 0), ReadDate(reader, 1), ReadDate(reader, 2)));
        }

        return rows.AsReadOnly();
    }

    internal async Task<IReadOnlyList<Campaign>> ReadCampaignsAsync(DatabaseSettings settings, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await OpenAsync(settings, cancellationToken).ConfigureAwait(false);
        using SqlCommand command = CreateCommand(connection, CampaignsQuery);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<Campaign> rows = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new Campaign(ReadDate(reader, 0), ReadText(reader, 1)));
        }

        return rows.AsReadOnly();
    }

    internal async Task<Page<CampaignSms>> ReadCampaignSmsAsync(DatabaseSettings settings, SentMessageFilter filter,
        CancellationToken cancellationToken)
    {
        filter.Validate();
        await using SqlConnection connection = await OpenAsync(settings, cancellationToken).ConfigureAwait(false);
        using SqlCommand command = CreateCommand(connection, CampaignSmsQuery);
        command.Parameters.Add("@Take", SqlDbType.Int).Value = PageSize + 1;
        AddSentParameters(command, filter);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<CampaignSms> rows = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CampaignSms(ReadText(reader, 0), ReadText(reader, 1), ReadText(reader, 2), ReadText(reader, 3),
                ReadText(reader, 4), ReadText(reader, 5), ReadDate(reader, 6), ReadText(reader, 7), ReadText(reader, 8),
                ReadText(reader, 9), ReadText(reader, 10), ReadText(reader, 11), ReadText(reader, 12), ReadInt(reader, 13)));
        }

        return ToPage(rows);
    }

    internal async Task<IReadOnlyList<UserAccount>> ReadUsersAsync(DatabaseSettings settings, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await OpenAsync(settings, cancellationToken).ConfigureAwait(false);
        using SqlCommand command = CreateCommand(connection, UsersQuery);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<UserAccount> rows = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new UserAccount(ReadText(reader, 0), ReadText(reader, 1), ReadText(reader, 2), ReadText(reader, 3),
                ReadText(reader, 4), string.Equals(ReadText(reader, 5), "E", StringComparison.OrdinalIgnoreCase)));
        }

        return rows.AsReadOnly();
    }

    internal async Task<IReadOnlyList<ModuleEntry>> ReadModulesAsync(DatabaseSettings settings, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await OpenAsync(settings, cancellationToken).ConfigureAwait(false);
        using SqlCommand command = CreateCommand(connection, ModulesQuery);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<ModuleEntry> rows = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ModuleEntry(ReadText(reader, 0), ReadDouble(reader, 1), ReadText(reader, 2)));
        }

        return rows.AsReadOnly();
    }

    internal async Task<IReadOnlyList<CustomerBirthDate>> ReadCustomerBirthDatesAsync(DatabaseSettings settings,
        string officeCode, CancellationToken cancellationToken)
    {
        if (officeCode.Length > 50) throw new ArgumentException(IntegrationText.Get("DatabaseFilterInvalid"));
        await using SqlConnection connection = await OpenAsync(settings, cancellationToken).ConfigureAwait(false);
        using SqlCommand command = CreateCommand(connection, BirthDatesQuery);
        command.Parameters.Add("@Office", SqlDbType.NVarChar, 50).Value = officeCode;
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<CustomerBirthDate> rows = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CustomerBirthDate(ReadText(reader, 0), ReadDateOnly(reader, 1)));
        }

        return rows.AsReadOnly();
    }

    internal async Task<IReadOnlyList<GuarantorCard>> ReadGuarantorCardsAsync(DatabaseSettings settings,
        string customerCode, CancellationToken cancellationToken)
    {
        if (customerCode.Length > 60) throw new ArgumentException(IntegrationText.Get("DatabaseFilterInvalid"));
        await using SqlConnection connection = await OpenAsync(settings, cancellationToken).ConfigureAwait(false);
        using SqlCommand command = CreateCommand(connection, GuarantorCardsQuery);
        command.Parameters.Add("@Customer", SqlDbType.NVarChar, 60).Value = customerCode;
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<GuarantorCard> rows = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new GuarantorCard(ReadText(reader, 0), ReadText(reader, 1), ReadText(reader, 2), ReadText(reader, 3)));
        }

        return rows.AsReadOnly();
    }

    internal async Task<IReadOnlyList<CustomerLastPayment>> ReadLastPaymentsAsync(DatabaseSettings settings,
        string customerCode, CancellationToken cancellationToken)
    {
        if (customerCode.Length > 60) throw new ArgumentException(IntegrationText.Get("DatabaseFilterInvalid"));
        await using SqlConnection connection = await OpenAsync(settings, cancellationToken).ConfigureAwait(false);
        using SqlCommand command = CreateCommand(connection, LastPaymentsQuery);
        command.Parameters.Add("@Customer", SqlDbType.NVarChar, 60).Value = customerCode;
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<CustomerLastPayment> rows = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CustomerLastPayment(ReadText(reader, 0), ReadDate(reader, 1), ReadDate(reader, 2)));
        }

        return rows.AsReadOnly();
    }

    internal async Task<IReadOnlyList<CompanyInfo>> ReadCompanyInfoAsync(DatabaseSettings settings, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await OpenAsync(settings, cancellationToken).ConfigureAwait(false);
        using SqlCommand command = CreateCommand(connection, CompanyQuery);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<CompanyInfo> rows = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CompanyInfo(ReadText(reader, 0), ReadText(reader, 1)));
        }

        return rows.AsReadOnly();
    }

    internal async Task<IReadOnlyList<PermissionEntry>> ReadPermissionsAsync(DatabaseSettings settings,
        string userName, CancellationToken cancellationToken)
    {
        if (userName.Length > 60) throw new ArgumentException(IntegrationText.Get("DatabaseFilterInvalid"));
        await using SqlConnection connection = await OpenAsync(settings, cancellationToken).ConfigureAwait(false);
        using SqlCommand command = CreateCommand(connection, PermissionsQuery);
        command.Parameters.Add("@User", SqlDbType.NVarChar, 60).Value = userName;
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<PermissionEntry> rows = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new PermissionEntry(ReadText(reader, 0), ReadDouble(reader, 1), ReadText(reader, 2), ReadText(reader, 3),
                ReadDouble(reader, 4), ReadBool(reader, 5), ReadBool(reader, 6), ReadBool(reader, 7), ReadBool(reader, 8),
                ReadBool(reader, 9), ReadBool(reader, 10), ReadBool(reader, 11), ReadBool(reader, 12), ReadBool(reader, 13)));
        }

        return rows.AsReadOnly();
    }

    private static void AddSentParameters(SqlCommand command, SentMessageFilter filter)
    {
        command.Parameters.Add("@Office", SqlDbType.NVarChar, 50).Value = filter.OfficeCode;
        command.Parameters.Add("@Search", SqlDbType.NVarChar, 100).Value = filter.Search.Trim();
        command.Parameters.Add("@From", SqlDbType.DateTime).Value = (object?)filter.FromDate?.Date ?? DBNull.Value;
        command.Parameters.Add("@To", SqlDbType.DateTime).Value = (object?)filter.ToDate?.Date.AddDays(1) ?? DBNull.Value;
    }

    private static async Task<SqlConnection> OpenAsync(DatabaseSettings settings, CancellationToken cancellationToken)
    {
        SqlConnection connection = new(settings.CreateConnectionString());
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static SqlCommand CreateCommand(SqlConnection connection, string sql)
    {
        SqlCommand command = connection.CreateCommand();
        command.CommandTimeout = 60;
        command.CommandText = sql;

        return command;
    }

    private static Page<T> ToPage<T>(List<T> items)
    {
        bool hasMore = items.Count > PageSize;
        if (hasMore)
        {
            items.RemoveAt(PageSize);
        }

        return new Page<T>(items.AsReadOnly(), hasMore);
    }

    private static bool HasColumn(SqlDataReader reader, string name)
    {
        for (int index = 0; index < reader.FieldCount; index++)
        {
            if (string.Equals(reader.GetName(index), name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string ReadOptionalText(SqlDataReader reader, string name)
        => HasColumn(reader, name) ? ReadText(reader, reader.GetOrdinal(name)) : "";

    private static bool ReadOptionalBool(SqlDataReader reader, string name)
        => HasColumn(reader, name) && ReadBool(reader, reader.GetOrdinal(name));

    private static int ReadOptionalInt(SqlDataReader reader, string name)
    {
        if (!HasColumn(reader, name) || reader.IsDBNull(reader.GetOrdinal(name)))
        {
            return 0;
        }
        object value = reader.GetValue(reader.GetOrdinal(name));

        return value is string text
            ? int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : 0
            : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static string ReadText(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? "" : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.Trim() ?? "";

    private static int ReadInt(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static int? ReadNullableInt(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static double ReadDouble(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0 : Convert.ToDouble(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static decimal ReadDecimal(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0 : Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static bool ReadBool(SqlDataReader reader, int ordinal)
        => !reader.IsDBNull(ordinal) && Convert.ToBoolean(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    // Legacy BBSoft wrote dates through BDE string parameters, so the column type may be datetime or text.
    private static DateTime? ReadDate(SqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }
        object value = reader.GetValue(ordinal);
        if (value is DateTime date)
        {
            return date;
        }
        string text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? "";

        return DateTime.TryParse(text, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, out date)
            || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ? date : null;
    }

    private static DateOnly? ReadDateOnly(SqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return null;
        object value = reader.GetValue(ordinal);
        if (value is DateOnly d) return d;
        if (value is DateTime dt) return DateOnly.FromDateTime(dt);
        string text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? "";

        return DateOnly.TryParse(text, CultureInfo.GetCultureInfo("tr-TR"), out DateOnly parsed)
            || DateOnly.TryParse(text, CultureInfo.InvariantCulture, out parsed) ? parsed : null;
    }

    /// <summary>
    /// Exposes every query text so tests can validate the T-SQL syntax without a database.
    /// </summary>
    internal static IEnumerable<(string Name, string Sql)> GetQueriesForValidation()
    {
        yield return ("Offices", OfficeQuery);
        yield return ("Customers", BuildCustomerQuery(""));
        yield return ("CustomersWithBirthday", BuildCustomerQuery("NebimV3"));
        yield return ("CustomersWithBBSoftBirthday", BuildCustomerQuery("", true));
        yield return ("Phones", PhoneQuery);
        yield return ("SentSummary", SentSummaryQuery);
        yield return ("SentDetail", SentDetailQuery);
        yield return ("SentDtSummary", SentDtSummaryQuery);
        yield return ("SentDtDetail", SentDtDetailQuery);
        yield return ("SentCustomers", SentCustomersQuery);
        yield return ("Campaigns", CampaignsQuery);
        yield return ("CampaignSms", CampaignSmsQuery);
        yield return ("Users", UsersQuery);
        yield return ("Modules", ModulesQuery);
        yield return ("BirthDates", BirthDatesQuery);
        yield return ("GuarantorCards", GuarantorCardsQuery);
        yield return ("LastPayments", LastPaymentsQuery);
        yield return ("Company", CompanyQuery);
        yield return ("Permissions", PermissionsQuery);
    }

    private const string OfficeQuery = """
        SELECT a.OfficeCode, MAX(COALESCE(a.OfficeDescription, N'')) AS OfficeDescription, COUNT(*) AS CustomerCount,
               SUM(CASE WHEN p.CurrAccCode IS NOT NULL THEN 1 ELSE 0 END) AS WithPhoneCount,
               SUM(CASE WHEN COALESCE(b.Debit, 0) > 0 THEN 1 ELSE 0 END) AS DebtorCount,
               SUM(COALESCE(b.Debit, 0)) AS TotalDebt,
               SUM(CASE WHEN l.CurrAccCode IS NOT NULL THEN 1 ELSE 0 END) AS LegalCount,
               MAX(COALESCE(s.SentCount, 0)) AS SentCount, MAX(s.LastSentDate) AS LastSentDate
        FROM dbo.MusteriAdSoyad AS a
        LEFT JOIN (SELECT DISTINCT CurrAccCode FROM dbo.MusteriTelefonNo) AS p ON p.CurrAccCode = a.CurrAccCode
        LEFT JOIN (SELECT CurrAccCode, SUM(Debit) AS Debit FROM dbo.MusteriKalanBorc GROUP BY CurrAccCode) AS b ON b.CurrAccCode = a.CurrAccCode
        LEFT JOIN (SELECT DISTINCT CurrAccCode FROM dbo.AvukatlikKarti) AS l ON l.CurrAccCode = a.CurrAccCode
        LEFT JOIN (SELECT OfficeCode, COUNT(*) AS SentCount, MAX(SentDate) AS LastSentDate FROM dbo.GonderilenSSMler GROUP BY OfficeCode) AS s
               ON s.OfficeCode = a.OfficeCode
        WHERE a.OfficeCode IS NOT NULL
        GROUP BY a.OfficeCode
        ORDER BY a.OfficeCode;
        """;

    private static string BuildCustomerQuery(string nebimDatabase, bool bbsoftBirthday = false)
    {
        bool hasNebim = nebimDatabase.Length > 0;
        bool hasBirthday = hasNebim || bbsoftBirthday;

        string birthdaySelectColumns = !hasBirthday ? "" : hasNebim
            ? """
              , bd.BirthDate,
                 DATEDIFF(day, CAST(GETDATE() AS date),
                          CASE WHEN DATEADD(year, DATEDIFF(year, bd.BirthDate, GETDATE()), bd.BirthDate) < CAST(GETDATE() AS date)
                               THEN DATEADD(year, DATEDIFF(year, bd.BirthDate, GETDATE()) + 1, bd.BirthDate)
                               ELSE DATEADD(year, DATEDIFF(year, bd.BirthDate, GETDATE()), bd.BirthDate) END) AS DaysToBirthday
              """
            : """
              , bbd.BirthDate,
                 DATEDIFF(day, CAST(GETDATE() AS date),
                          CASE WHEN DATEADD(year, DATEDIFF(year, bbd.BirthDate, GETDATE()), bbd.BirthDate) < CAST(GETDATE() AS date)
                               THEN DATEADD(year, DATEDIFF(year, bbd.BirthDate, GETDATE()) + 1, bbd.BirthDate)
                               ELSE DATEADD(year, DATEDIFF(year, bbd.BirthDate, GETDATE()), bbd.BirthDate) END) AS DaysToBirthday
              """;

        string birthdayJoin = hasNebim
            ? $"""
               LEFT JOIN (SELECT CurrAccCode, MIN(BirthDate) AS BirthDate FROM [{nebimDatabase}].dbo.prCurrAccPersonalInfo
                          WHERE CurrAccTypeCode = 4 AND BirthDate IS NOT NULL GROUP BY CurrAccCode) AS bd ON bd.CurrAccCode = a.CurrAccCode
               """
            : bbsoftBirthday
                ? "LEFT JOIN dbo.MusteriDogumTarihi AS bbd ON bbd.CurrAccCode = a.CurrAccCode"
                : "";

        string birthdayFilter = !hasBirthday ? "" : "AND (@BirthdayDays IS NULL OR DaysToBirthday <= @BirthdayDays)";
        string finalBirthdayColumns = hasBirthday ? ", BirthDate, DaysToBirthday" : "";

        return $"""
            WITH Phones AS (
                SELECT CurrAccCode, MIN(TelefonNo) AS Phone, COUNT(*) AS PhoneCount
                FROM dbo.MusteriTelefonNo WHERE TelefonNo IS NOT NULL GROUP BY CurrAccCode
            ),
            PayDates AS (
                SELECT RetailCustomerCode AS CurrAccCode,
                       MAX(SonOdemeTarihi) AS SonOdemeTarihi,
                       MAX(VadeTarihi) AS VadeTarihi,
                       MAX(QuitPaymentDate) AS KontrolTarihi
                FROM dbo.MusteriSonOdemeTarihi GROUP BY RetailCustomerCode
            ),
            ReminderDates AS (
                SELECT CurrAccCode, {AsDate("MaxSentDate")} AS LastReminderDate
                FROM dbo.GonderilenMusteriler GROUP BY CurrAccCode, MaxSentDate
            ),
            BaseData AS (
                SELECT a.CurrAccCode AS CustomerCode,
                       COALESCE(a.FirstLastName, N'') AS FullName,
                       COALESCE(a.OfficeCode, N'') AS OfficeCode,
                       COALESCE(a.OfficeDescription, N'') AS OfficeDescription,
                       COALESCE(p.Phone, N'') AS Phone,
                       COALESCE(p.PhoneCount, 0) AS PhoneCount,
                       COALESCE(b.Debit, 0) AS Debt,
                       NULLIF({AsDate("pd.SonOdemeTarihi")}, CONVERT(date, '19650620', 112)) AS LastPaymentDate,
                       NULLIF({AsDate("pd.VadeTarihi")}, CONVERT(date, '19650620', 112)) AS DueDate,
                       NULLIF({AsDate("pd.KontrolTarihi")}, CONVERT(date, '19650620', 112)) AS ReferenceDate,
                       a.SSMMesaj AS Stage,
                       g.LastReminderDate,
                       COALESCE(gs.SentCount, 0) AS SentCount,
                       {AsDate("a.PaymentDate")} AS RecordedPaymentDate,
                       CASE WHEN l.CurrAccCode IS NULL THEN 0 ELSE 1 END AS LegalFollowUp
                       {birthdaySelectColumns}
                FROM dbo.MusteriAdSoyad AS a
                LEFT JOIN Phones AS p ON p.CurrAccCode = a.CurrAccCode
                LEFT JOIN (SELECT CurrAccCode, SUM(Debit) AS Debit FROM dbo.MusteriKalanBorc GROUP BY CurrAccCode) AS b
                       ON b.CurrAccCode = a.CurrAccCode
                LEFT JOIN PayDates AS pd ON pd.CurrAccCode = a.CurrAccCode
                LEFT JOIN (SELECT CurrAccCode, MAX(LastReminderDate) AS LastReminderDate
                           FROM ReminderDates GROUP BY CurrAccCode) AS g ON g.CurrAccCode = a.CurrAccCode
                LEFT JOIN (SELECT CurrAccCode, COUNT(*) AS SentCount
                           FROM dbo.GonderilenSSMler GROUP BY CurrAccCode) AS gs ON gs.CurrAccCode = a.CurrAccCode
                LEFT JOIN (SELECT DISTINCT CurrAccCode FROM dbo.AvukatlikKarti) AS l ON l.CurrAccCode = a.CurrAccCode
                {birthdayJoin}
            ),
            FinalData AS (
                SELECT *,
                       CASE WHEN ReferenceDate IS NULL THEN NULL
                            ELSE DATEDIFF(day, ReferenceDate, CAST(GETDATE() AS date)) END AS DaysOverdue
                FROM BaseData
            )
            SELECT TOP (@Take) CustomerCode, FullName, OfficeCode, OfficeDescription, Phone, PhoneCount, Debt,
                   ReferenceDate, DaysOverdue, Stage, LastReminderDate, SentCount, LegalFollowUp,
                   LastPaymentDate, DueDate, RecordedPaymentDate{finalBirthdayColumns}
            FROM FinalData
            WHERE (@Office = N'' OR OfficeCode = @Office)
              AND (@Search = N'' OR CHARINDEX(@Search, CustomerCode) > 0 OR CHARINDEX(@Search, FullName) > 0 OR CHARINDEX(@Search, Phone) > 0)
              AND (@RequirePhone = 0 OR Phone <> N'')
              AND (@MissingPhone = 0 OR Phone = N'')
              AND (@IncludeLegal = 1 OR LegalFollowUp = 0)
              AND (@DueOnly = 0 OR (Debt > 0 AND DueDate IS NOT NULL AND DaysOverdue IS NOT NULL))
              AND (@MinDays IS NULL OR DaysOverdue >= @MinDays)
              AND (@MaxDays IS NULL OR DaysOverdue <= @MaxDays)
              {birthdayFilter}
            ORDER BY OfficeCode, FullName, CustomerCode
            OPTION (RECOMPILE);
            """;
    }

    private const string PhoneQuery = """
        SELECT TOP (@Take) COALESCE(a.OfficeCode, N''), COALESCE(a.OfficeDescription, N''), a.CurrAccCode,
               COALESCE(a.FirstLastName, N''), COALESCE(t.Description, N''), t.TelefonNo
        FROM dbo.MusteriTelefonNo AS t
        INNER JOIN dbo.MusteriAdSoyad AS a ON a.CurrAccCode = t.CurrAccCode
        WHERE t.TelefonNo IS NOT NULL
          AND (@Office = N'' OR a.OfficeCode = @Office)
          AND (@Search = N'' OR CHARINDEX(@Search, a.CurrAccCode) > 0 OR CHARINDEX(@Search, a.FirstLastName) > 0 OR CHARINDEX(@Search, t.TelefonNo) > 0)
        ORDER BY a.OfficeCode, a.FirstLastName, a.CurrAccCode, t.TelefonNo
        OPTION (RECOMPILE);
        """;

    private const string SentSummaryQuery = """
        SELECT OfficeCode, MAX(COALESCE(OfficeDescription, N'')) AS OfficeDescription, SentDate, COUNT(*) AS SentCount
        FROM dbo.GonderilenSSMler
        WHERE (@Office = N'' OR OfficeCode = @Office)
          AND (@From IS NULL OR SentDate >= @From) AND (@To IS NULL OR SentDate < @To)
          AND (@Search = N'' OR CHARINDEX(@Search, CurrAccCode) > 0 OR CHARINDEX(@Search, FirstLastName) > 0 OR CHARINDEX(@Search, TelefonNo) > 0)
        GROUP BY OfficeCode, SentDate
        ORDER BY SentDate DESC, OfficeCode;
        """;

    private const string SentDetailQuery = """
        SELECT TOP (@Take) CurrAccCode, COALESCE(FirstLastName, N''), COALESCE(OfficeCode, N''), COALESCE(OfficeDescription, N''),
               COALESCE(TelefonNo, N''), COALESCE(Description, N''), COALESCE(Debit, 0), QuitPaymentDate, SentDate,
               COALESCE(CAST(SentTime AS nvarchar(30)), N''), COALESCE(CAST(SentID AS nvarchar(100)), N'')
        FROM dbo.GonderilenSSMler
        WHERE (@Office = N'' OR OfficeCode = @Office)
          AND (@From IS NULL OR SentDate >= @From) AND (@To IS NULL OR SentDate < @To)
          AND (@Search = N'' OR CHARINDEX(@Search, CurrAccCode) > 0 OR CHARINDEX(@Search, FirstLastName) > 0 OR CHARINDEX(@Search, TelefonNo) > 0)
        ORDER BY SentDate DESC, OfficeCode, FirstLastName
        OPTION (RECOMPILE);
        """;

    private const string SentDtSummaryQuery = """
        SELECT OfficeCode, MAX(COALESCE(OfficeDescription, N'')) AS OfficeDescription, SentDate, COUNT(*) AS SentCount
        FROM dbo.GonderilenDTler
        WHERE (@Office = N'' OR OfficeCode = @Office)
          AND (@From IS NULL OR SentDate >= @From) AND (@To IS NULL OR SentDate < @To)
          AND (@Search = N'' OR CHARINDEX(@Search, CurrAccCode) > 0 OR CHARINDEX(@Search, FirstLastName) > 0 OR CHARINDEX(@Search, TelefonNo) > 0)
        GROUP BY OfficeCode, SentDate
        ORDER BY SentDate DESC, OfficeCode;
        """;

    private const string SentDtDetailQuery = """
        SELECT TOP (@Take) CurrAccCode, COALESCE(FirstLastName, N''), COALESCE(OfficeCode, N''), COALESCE(OfficeDescription, N''),
               COALESCE(TelefonNo, N''), COALESCE(Description, N''), COALESCE(Debit, 0), QuitPaymentDate, SentDate,
               COALESCE(CAST(SentTime AS nvarchar(30)), N''), COALESCE(CAST(SentID AS nvarchar(100)), N''),
               COALESCE(CAST(strMsg AS nvarchar(500)), N''), COALESCE(Gonderim, N'')
        FROM dbo.GonderilenDTler
        WHERE (@Office = N'' OR OfficeCode = @Office)
          AND (@From IS NULL OR SentDate >= @From) AND (@To IS NULL OR SentDate < @To)
          AND (@Search = N'' OR CHARINDEX(@Search, CurrAccCode) > 0 OR CHARINDEX(@Search, FirstLastName) > 0 OR CHARINDEX(@Search, TelefonNo) > 0)
        ORDER BY SentDate DESC, OfficeCode, FirstLastName
        OPTION (RECOMPILE);
        """;

    private static readonly string SentCustomersQuery = $"""
        SELECT m.CurrAccCode,
               {AsDate("m.MinSentDate")} AS MinSentDate,
               {AsDate("m.MaxSentDate")} AS MaxSentDate
        FROM dbo.GonderilenMusteriler AS m
        INNER JOIN dbo.MusteriAdSoyad AS a ON a.CurrAccCode = m.CurrAccCode
        WHERE (@Office = N'' OR a.OfficeCode = @Office)
        ORDER BY m.MaxSentDate DESC, m.CurrAccCode;
        """;

    private const string CampaignsQuery = """
        SELECT KampanyaTarih, CAST(GonderilenMesaj AS nvarchar(max)) AS GonderilenMesaj
        FROM dbo.Kampanyalar
        ORDER BY KampanyaTarih DESC;
        """;

    private const string CampaignSmsQuery = """
        SELECT TOP (@Take) CurrAccCode, COALESCE(FirstLastName, N''), COALESCE(OfficeCode, N''), COALESCE(OfficeDescription, N''),
               COALESCE(TelefonNo, N''), COALESCE(Description, N''), SentDate,
               COALESCE(CAST(SentTime AS nvarchar(30)), N''), COALESCE(CAST(SentID AS nvarchar(100)), N''),
               COALESCE(CAST(strMsg AS nvarchar(500)), N''), COALESCE(TypeContact, N''),
               COALESCE(KefilFirsLastName, N''), COALESCE(KefilTelefonNo, N''), COALESCE(KampanyaAdedi, 0)
        FROM dbo.KampanyaSSMler
        WHERE (@Office = N'' OR OfficeCode = @Office)
          AND (@From IS NULL OR SentDate >= @From) AND (@To IS NULL OR SentDate < @To)
          AND (@Search = N'' OR CHARINDEX(@Search, CurrAccCode) > 0 OR CHARINDEX(@Search, FirstLastName) > 0 OR CHARINDEX(@Search, TelefonNo) > 0)
        ORDER BY SentDate DESC, OfficeCode, FirstLastName
        OPTION (RECOMPILE);
        """;

    private const string UsersQuery = """
        SELECT KullaniciAdi, COALESCE(AdiSoyadi, N''), COALESCE(Bolum, N''), COALESCE(Gorev, N''),
               COALESCE(BilgisayarAdi, N''), COALESCE(Yonetici, N'')
        FROM dbo.Kullanici
        ORDER BY KullaniciAdi;
        """;

    private const string ModulesQuery = """
        SELECT COALESCE(Modul, N''), COALESCE(BolumKodu, 0), COALESCE(BolumAciklama, N'')
        FROM dbo.Modul
        ORDER BY Modul, BolumKodu;
        """;

    private const string BirthDatesQuery = """
        SELECT d.CurrAccCode, d.BirthDate
        FROM dbo.MusteriDogumTarihi AS d
        INNER JOIN dbo.MusteriAdSoyad AS a ON a.CurrAccCode = d.CurrAccCode
        WHERE d.BirthDate IS NOT NULL
          AND (@Office = N'' OR a.OfficeCode = @Office)
        ORDER BY d.CurrAccCode;
        """;

    private const string GuarantorCardsQuery = """
        SELECT ContackCurrAccCode, COALESCE(TypeContact, N''), COALESCE(KefilFirsLastName, N''), COALESCE(KefilTelefonNo, N'')
        FROM dbo.MusteriKefilKarti
        WHERE (@Customer = N'' OR ContackCurrAccCode = @Customer)
        ORDER BY ContackCurrAccCode;
        """;

    private const string LastPaymentsQuery = """
        SELECT RetailCustomerCode, ExpiryDate, QuitPaymentDate
        FROM dbo.MusteriSonOdeme
        WHERE (@Customer = N'' OR RetailCustomerCode = @Customer)
        ORDER BY RetailCustomerCode;
        """;

    private const string CompanyQuery = """
        SELECT COALESCE(SirketKodu, N''), COALESCE(SirketAdi, N'')
        FROM dbo.Sirket;
        """;

    private const string PermissionsQuery = """
        SELECT COALESCE(Modul, N''), COALESCE(BolumKodu, 0), COALESCE(BolumAciklama, N''),
               COALESCE(KullaniciAdi, N''), COALESCE(Seviye, 0),
               COALESCE(Goster, 0), COALESCE(Yeni, 0), COALESCE(Degistir, 0), COALESCE(Sil, 0),
               COALESCE(Yazdir, 0), COALESCE(EPosta, 0), COALESCE(Transfer, 0),
               COALESCE(AlanTanimla, 0), COALESCE(EkranTanimla, 0)
        FROM dbo.Yetki
        WHERE (@User = N'' OR KullaniciAdi = @User)
        ORDER BY Modul, BolumKodu, KullaniciAdi;
        """;
}
