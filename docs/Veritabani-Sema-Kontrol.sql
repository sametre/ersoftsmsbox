-- Yalniz test/inceleme amacli metadata okur; veri degistirmez, musteri verisi secmez.
-- SSMS'te dogru veritabanini secin. Nebim icin @Kaynak degerini N'Nebim' yapin.
DECLARE @Kaynak nvarchar(10) = N'BBSoft';

;WITH Beklenen(Kaynak, Nesne, Alan) AS
(
    SELECT * FROM (VALUES
        (N'BBSoft', N'MusteriAdSoyad', N'CurrAccCode'),
        (N'BBSoft', N'MusteriAdSoyad', N'FirstLastName'),
        (N'BBSoft', N'MusteriAdSoyad', N'OfficeCode'),
        (N'BBSoft', N'MusteriAdSoyad', N'OfficeDescription'),
        (N'BBSoft', N'MusteriTelefonNo', N'CurrAccCode'),
        (N'BBSoft', N'MusteriTelefonNo', N'TelefonNo'),
        (N'BBSoft', N'MusteriKalanBorc', N'CurrAccCode'),
        (N'BBSoft', N'MusteriKalanBorc', N'Debit'),
        (N'BBSoft', N'MusteriSonOdemeTarihi', N'RetailCustomerCode'),
        (N'BBSoft', N'MusteriSonOdemeTarihi', N'QuitPaymentDate'),
        (N'BBSoft', N'AvukatlikKarti', N'CurrAccCode'),
        (N'Nebim', N'cdCurrAcc', N'CurrAccTypeCode'),
        (N'Nebim', N'cdCurrAcc', N'CurrAccCode'),
        (N'Nebim', N'cdCurrAcc', N'FirstLastName'),
        (N'Nebim', N'cdCurrAcc', N'OfficeCode'),
        (N'Nebim', N'cdOfficeDesc', N'OfficeCode'),
        (N'Nebim', N'cdOfficeDesc', N'OfficeDescription'),
        (N'Nebim', N'cdOfficeDesc', N'LangCode'),
        (N'Nebim', N'CurrAccCommunication', N'CurrAccCode'),
        (N'Nebim', N'CurrAccCommunication', N'CurrAccTypeCode'),
        (N'Nebim', N'CurrAccCommunication', N'ContactID'),
        (N'Nebim', N'CurrAccCommunication', N'CommAddress'),
        (N'Nebim', N'RetailCustomerTransactions', N'RetailCustomerCode'),
        (N'Nebim', N'RetailCustomerTransactions', N'Debit'),
        (N'Nebim', N'RetailCustomerTransactions', N'Payment'),
        (N'Nebim', N'RetailCustomerTransactions', N'OperationDate'),
        (N'Nebim', N'RetailInstallments', N'CurrAccCode'),
        (N'Nebim', N'RetailInstallments', N'CurrAccTypeCode'),
        (N'Nebim', N'RetailInstallments', N'RemainingAmount'),
        (N'Nebim', N'RetailInstallments', N'DueDate'),
        (N'Nebim', N'trBadDebtTransLine', N'CurrAccCode')
    ) AS Liste(Kaynak, Nesne, Alan)
)
SELECT DB_NAME() AS Veritabani, b.Kaynak, b.Nesne, b.Alan,
       CASE WHEN o.object_id IS NULL THEN N'Nesne yok veya metadata izni yok'
            WHEN c.column_id IS NULL THEN N'Alan yok veya metadata izni yok'
            ELSE N'Alan bulundu; turu ve is anlamini ayrica dogrulayin' END AS Durum,
       o.type_desc AS NesneTuru, t.name AS VeriTuru,
       c.max_length AS Uzunluk, c.precision AS Hassasiyet, c.scale AS OndalikBasamak,
       c.is_nullable AS BosOlabilir,
       HAS_PERMS_BY_NAME(N'dbo.' + QUOTENAME(b.Nesne), N'OBJECT', N'SELECT') AS SecmeIzni
FROM Beklenen AS b
LEFT JOIN sys.schemas AS s ON s.name = N'dbo'
LEFT JOIN sys.objects AS o ON o.schema_id = s.schema_id AND o.name = b.Nesne
LEFT JOIN sys.columns AS c ON c.object_id = o.object_id AND c.name = b.Alan
LEFT JOIN sys.types AS t ON t.user_type_id = c.user_type_id
WHERE b.Kaynak = @Kaynak
ORDER BY b.Nesne, b.Alan;

-- Fonksiyon parametre sozlesmesini ayrica gosterir (Nebim kaynaginda).
SELECT o.name AS Fonksiyon, p.name AS Parametre, t.name AS VeriTuru, p.max_length AS Uzunluk
FROM sys.objects AS o
JOIN sys.schemas AS s ON s.schema_id = o.schema_id
JOIN sys.parameters AS p ON p.object_id = o.object_id
JOIN sys.types AS t ON t.user_type_id = p.user_type_id
WHERE @Kaynak = N'Nebim' AND s.name = N'dbo' AND o.name = N'CurrAccCommunication'
ORDER BY p.parameter_id;
