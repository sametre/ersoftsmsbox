-- ERSoftSMSBox - BBSoft veritabani yapi cikarma betigi
-- Sadece YAPIYI okur: tablo, kolon, tip, anahtar, indeks, view/prosedur adlari ve satir SAYISI.
-- Hicbir musteri verisi (satir icerigi) okunmaz, hicbir sey degistirilmez.
-- Kullanim: SSMS'te BBSoft veritabanini secin, F5 ile calistirin,
--           "Messages" sekmesinde Ctrl+A, Ctrl+C yapip ciktiyi yapistirin.
-- SQL Server 2008 ve sonrasi ile uyumludur.

SET NOCOUNT ON;

DECLARE @line nvarchar(4000);

PRINT '===== SUNUCU / VERITABANI =====';
PRINT 'Surum      : ' + REPLACE(REPLACE(CAST(@@VERSION AS nvarchar(300)), CHAR(10), ' '), CHAR(13), ' ');
PRINT 'Veritabani : ' + DB_NAME();
SELECT @line = 'Uyumluluk  : ' + CAST(compatibility_level AS varchar(10)) + '   Collation: ' + ISNULL(collation_name, '')
FROM sys.databases WHERE name = DB_NAME();
PRINT @line;
SELECT @line = 'Dil        : ' + @@LANGUAGE + '   DateFormat: ' + ISNULL((SELECT dateformat FROM sys.syslanguages WHERE name = @@LANGUAGE), '?');
PRINT @line;
PRINT '';

PRINT '===== SUNUCUDAKI VERITABANLARI =====';
DECLARE db_cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT name + '  (uyumluluk ' + CAST(compatibility_level AS varchar(10)) + ')' FROM sys.databases ORDER BY name;
OPEN db_cur; FETCH NEXT FROM db_cur INTO @line;
WHILE @@FETCH_STATUS = 0 BEGIN PRINT '  ' + @line; FETCH NEXT FROM db_cur INTO @line; END
CLOSE db_cur; DEALLOCATE db_cur;
PRINT '';

PRINT '===== TABLOLAR VE KOLONLAR =====';
DECLARE @tableId int, @tableName nvarchar(300), @rows bigint;
DECLARE tbl_cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT t.object_id, s.name + '.' + t.name,
           ISNULL((SELECT SUM(p.rows) FROM sys.partitions p WHERE p.object_id = t.object_id AND p.index_id IN (0, 1)), 0)
    FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE t.is_ms_shipped = 0
    ORDER BY s.name, t.name;
OPEN tbl_cur; FETCH NEXT FROM tbl_cur INTO @tableId, @tableName, @rows;
WHILE @@FETCH_STATUS = 0
BEGIN
    PRINT '[' + @tableName + ']  satir sayisi: ' + CAST(@rows AS varchar(20));

    DECLARE col_cur CURSOR LOCAL FAST_FORWARD FOR
        SELECT '    ' + c.name + '  ' +
               CASE
                   WHEN ty.name IN ('varchar', 'char', 'varbinary', 'binary')
                       THEN ty.name + '(' + CASE WHEN c.max_length = -1 THEN 'max' ELSE CAST(c.max_length AS varchar(10)) END + ')'
                   WHEN ty.name IN ('nvarchar', 'nchar')
                       THEN ty.name + '(' + CASE WHEN c.max_length = -1 THEN 'max' ELSE CAST(c.max_length / 2 AS varchar(10)) END + ')'
                   WHEN ty.name IN ('decimal', 'numeric')
                       THEN ty.name + '(' + CAST(c.precision AS varchar(5)) + ',' + CAST(c.scale AS varchar(5)) + ')'
                   ELSE ty.name
               END +
               CASE WHEN c.is_nullable = 1 THEN ' NULL' ELSE ' NOT NULL' END +
               CASE WHEN c.is_identity = 1 THEN ' IDENTITY' ELSE '' END +
               CASE WHEN c.is_computed = 1 THEN ' COMPUTED' ELSE '' END +
               ISNULL(' DEFAULT ' + dc.definition, '')
        FROM sys.columns c
        JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE c.object_id = @tableId
        ORDER BY c.column_id;
    OPEN col_cur; FETCH NEXT FROM col_cur INTO @line;
    WHILE @@FETCH_STATUS = 0 BEGIN PRINT @line; FETCH NEXT FROM col_cur INTO @line; END
    CLOSE col_cur; DEALLOCATE col_cur;

    DECLARE idx_cur CURSOR LOCAL FAST_FORWARD FOR
        SELECT '    -- ' +
               CASE WHEN i.is_primary_key = 1 THEN 'PK ' WHEN i.is_unique = 1 THEN 'UNIQUE ' ELSE 'INDEX ' END +
               i.name + ' (' +
               STUFF((SELECT ', ' + c.name
                      FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                      WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
                      ORDER BY ic.key_ordinal FOR XML PATH('')), 1, 2, '') + ')'
        FROM sys.indexes i
        WHERE i.object_id = @tableId AND i.index_id > 0 AND i.is_hypothetical = 0
        ORDER BY i.index_id;
    OPEN idx_cur; FETCH NEXT FROM idx_cur INTO @line;
    WHILE @@FETCH_STATUS = 0 BEGIN PRINT @line; FETCH NEXT FROM idx_cur INTO @line; END
    CLOSE idx_cur; DEALLOCATE idx_cur;

    PRINT '';
    FETCH NEXT FROM tbl_cur INTO @tableId, @tableName, @rows;
END
CLOSE tbl_cur; DEALLOCATE tbl_cur;

PRINT '===== TARIH ICEREN KOLONLAR (hizli bakis) =====';
DECLARE date_cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT '  ' + t.name + '.' + c.name + '  ' + ty.name
    FROM sys.columns c
    JOIN sys.tables t ON t.object_id = c.object_id
    JOIN sys.types ty ON ty.user_type_id = c.user_type_id
    WHERE t.is_ms_shipped = 0
      AND (ty.name IN ('date', 'datetime', 'datetime2', 'smalldatetime', 'datetimeoffset', 'time')
           OR c.name LIKE '%Date%' OR c.name LIKE '%Tarih%' OR c.name LIKE '%Time%')
    ORDER BY t.name, c.column_id;
OPEN date_cur; FETCH NEXT FROM date_cur INTO @line;
WHILE @@FETCH_STATUS = 0 BEGIN PRINT @line; FETCH NEXT FROM date_cur INTO @line; END
CLOSE date_cur; DEALLOCATE date_cur;
PRINT '';

PRINT '===== VIEW / PROSEDUR / FONKSIYON / TRIGGER =====';
DECLARE obj_cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT '  ' + o.type_desc COLLATE DATABASE_DEFAULT + '  ' + s.name + '.' + o.name +
           CASE WHEN m.definition LIKE '%SUBSTRING%' THEN '   [SUBSTRING kullaniyor]' ELSE '' END +
           CASE WHEN o.type = 'TR' THEN '   (tablo: ' + OBJECT_NAME(o.parent_object_id) + ')' ELSE '' END
    FROM sys.objects o
    JOIN sys.schemas s ON s.schema_id = o.schema_id
    LEFT JOIN sys.sql_modules m ON m.object_id = o.object_id
    WHERE o.is_ms_shipped = 0 AND o.type IN ('V', 'P', 'FN', 'IF', 'TF', 'TR')
    ORDER BY o.type_desc, o.name;
OPEN obj_cur; FETCH NEXT FROM obj_cur INTO @line;
IF @@FETCH_STATUS <> 0 PRINT '  (yok)';
WHILE @@FETCH_STATUS = 0 BEGIN PRINT @line; FETCH NEXT FROM obj_cur INTO @line; END
CLOSE obj_cur; DEALLOCATE obj_cur;
PRINT '';

PRINT '===== SUBSTRING KULLANAN VIEW TANIMLARI (varsa) =====';
DECLARE @def nvarchar(max), @name nvarchar(300), @pos int;
DECLARE def_cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT s.name + '.' + o.name, m.definition
    FROM sys.objects o
    JOIN sys.schemas s ON s.schema_id = o.schema_id
    JOIN sys.sql_modules m ON m.object_id = o.object_id
    WHERE o.is_ms_shipped = 0 AND m.definition LIKE '%SUBSTRING%';
OPEN def_cur; FETCH NEXT FROM def_cur INTO @name, @def;
IF @@FETCH_STATUS <> 0 PRINT '  (yok)';
WHILE @@FETCH_STATUS = 0
BEGIN
    PRINT '--- ' + @name;
    SET @pos = 1;
    WHILE @pos <= LEN(@def) BEGIN PRINT SUBSTRING(@def, @pos, 3500); SET @pos = @pos + 3500; END
    FETCH NEXT FROM def_cur INTO @name, @def;
END
CLOSE def_cur; DEALLOCATE def_cur;

PRINT '';
PRINT '===== BITTI =====';
