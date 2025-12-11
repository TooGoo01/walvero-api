-- ============================================
-- SEQUENCE TABLE-LƏRİNİ SEED ETMƏK ÜÇÜN SQL SERVER SCRIPT
-- ============================================
-- Migration-dan SONRA bu script-i çalıştırın
-- ============================================

USE [YourDatabaseName]; -- Database adını dəyişdirin
GO

-- ============================================
-- 1. CARD PREFIX SEQUENCE (Mütləq)
-- ============================================

IF NOT EXISTS (SELECT 1 FROM CardPrefixSequences WHERE Id = 1)
BEGIN
    INSERT INTO CardPrefixSequences (Id, LastPrefix) 
    VALUES (1, 789);
    PRINT 'CardPrefixSequence yaradıldı: Id=1, LastPrefix=789';
END
ELSE
BEGIN
    PRINT 'CardPrefixSequence artıq mövcuddur';
END
GO

-- ============================================
-- 2. COUNTRY SEQUENCES (İstifadə olunan ölkələr)
-- ============================================

-- Azərbaycan (994)
IF NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 994)
BEGIN
    INSERT INTO CountrySequences (CountryCode, LastNumber) 
    VALUES (994, 0);
    PRINT 'CountrySequence yaradıldı: Azərbaycan (994)';
END
GO

-- Kuwait (965)
IF NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 965)
BEGIN
    INSERT INTO CountrySequences (CountryCode, LastNumber) 
    VALUES (965, 0);
    PRINT 'CountrySequence yaradıldı: Kuwait (965)';
END
GO

-- Saudi Arabia (966)
IF NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 966)
BEGIN
    INSERT INTO CountrySequences (CountryCode, LastNumber) 
    VALUES (966, 0);
    PRINT 'CountrySequence yaradıldı: Saudi Arabia (966)';
END
GO

-- Uzbekistan (998)
IF NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 998)
BEGIN
    INSERT INTO CountrySequences (CountryCode, LastNumber) 
    VALUES (998, 0);
    PRINT 'CountrySequence yaradıldı: Uzbekistan (998)';
END
GO

-- UAE (971)
IF NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 971)
BEGIN
    INSERT INTO CountrySequences (CountryCode, LastNumber) 
    VALUES (971, 0);
    PRINT 'CountrySequence yaradıldı: UAE (971)';
END
GO

-- Qatar (974)
IF NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 974)
BEGIN
    INSERT INTO CountrySequences (CountryCode, LastNumber) 
    VALUES (974, 0);
    PRINT 'CountrySequence yaradıldı: Qatar (974)';
END
GO

-- Oman (968)
IF NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 968)
BEGIN
    INSERT INTO CountrySequences (CountryCode, LastNumber) 
    VALUES (968, 0);
    PRINT 'CountrySequence yaradıldı: Oman (968)';
END
GO

-- ============================================
-- 3. TENANT CUSTOMER SEQUENCES (Bütün tenant-lar üçün)
-- ============================================

INSERT INTO TenantCustomerSequences (TenantId, LastNumber)
SELECT DISTINCT c.TenantId, 0
FROM Customers c
WHERE NOT EXISTS (
    SELECT 1 FROM TenantCustomerSequences tcs 
    WHERE tcs.TenantId = c.TenantId
);

DECLARE @TenantCount INT;
SELECT @TenantCount = COUNT(*) FROM TenantCustomerSequences;
PRINT 'TenantCustomerSequences yaradıldı: ' + CAST(@TenantCount AS VARCHAR(10)) + ' tenant';
GO

-- ============================================
-- 4. YOXLAMA QUERY-LƏRİ
-- ============================================

PRINT '';
PRINT '========== YOXLAMA NƏTICƏLƏRİ ==========';
PRINT '';

-- CountrySequences yoxlamaq
SELECT 
    'CountrySequences' AS TableName,
    CountryCode,
    CASE CountryCode
        WHEN 994 THEN 'Azərbaycan'
        WHEN 965 THEN 'Kuwait'
        WHEN 966 THEN 'Saudi Arabia'
        WHEN 998 THEN 'Uzbekistan'
        WHEN 971 THEN 'UAE'
        WHEN 974 THEN 'Qatar'
        WHEN 968 THEN 'Oman'
        ELSE 'Unknown'
    END AS CountryName,
    LastNumber
FROM CountrySequences
ORDER BY CountryCode;

-- TenantCustomerSequences yoxlamaq
SELECT 
    'TenantCustomerSequences' AS TableName,
    TenantId,
    LastNumber
FROM TenantCustomerSequences
ORDER BY TenantId;

-- CardPrefixSequences yoxlamaq
SELECT 
    'CardPrefixSequences' AS TableName,
    Id,
    LastPrefix
FROM CardPrefixSequences;

-- Ümumi sayğac
SELECT 
    'CountrySequences' AS TableName, 
    COUNT(*) AS Count 
FROM CountrySequences
UNION ALL
SELECT 
    'TenantCustomerSequences', 
    COUNT(*) 
FROM TenantCustomerSequences
UNION ALL
SELECT 
    'CardPrefixSequences', 
    COUNT(*) 
FROM CardPrefixSequences;

PRINT '';
PRINT '========== SEED TAMAMLANDI ==========';
GO

