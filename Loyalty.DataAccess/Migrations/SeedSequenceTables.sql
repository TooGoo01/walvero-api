-- ============================================
-- SEQUENCE TABLE-LƏRİNİ SEED ETMƏK ÜÇÜN SQL QUERY-LƏRİ
-- ============================================
-- Migration-dan SONRA bu script-i çalıştırın
-- ============================================

-- ============================================
-- 1. COUNTRY SEQUENCES - Mövcud User-lərdən
-- ============================================

-- Variant A: Mövcud WalveroId-lərdən sequence hesablamaq (əgər artıq generate olunubsa)
-- QEYD: Bu variant yalnız mövcud WalveroId-lər varsa işləyir

-- Hər ölkə kodu üçün son sequence nömrəsini tap və insert et
INSERT INTO CountrySequences (CountryCode, LastNumber)
SELECT 
    CAST(SUBSTRING(CAST(WalveroId AS VARCHAR), 1, 3) AS INT) AS CountryCode,
    CAST(SUBSTRING(CAST(WalveroId AS VARCHAR), 5, 5) AS INT) AS LastNumber
FROM (
    SELECT 
        WalveroId,
        ROW_NUMBER() OVER (PARTITION BY CAST(SUBSTRING(CAST(WalveroId AS VARCHAR), 1, 3) AS INT) ORDER BY WalveroId DESC) AS rn
    FROM AspNetUsers
    WHERE WalveroId IS NOT NULL
) AS ranked
WHERE rn = 1
ON CONFLICT (CountryCode) DO NOTHING; -- PostgreSQL üçün

-- SQL Server üçün:
/*
INSERT INTO CountrySequences (CountryCode, LastNumber)
SELECT 
    CAST(SUBSTRING(CAST(WalveroId AS VARCHAR(20)), 1, 3) AS INT) AS CountryCode,
    CAST(SUBSTRING(CAST(WalveroId AS VARCHAR(20)), 5, 5) AS INT) AS LastNumber
FROM (
    SELECT 
        WalveroId,
        ROW_NUMBER() OVER (
            PARTITION BY CAST(SUBSTRING(CAST(WalveroId AS VARCHAR(20)), 1, 3) AS INT) 
            ORDER BY WalveroId DESC
        ) AS rn
    FROM AspNetUsers
    WHERE WalveroId IS NOT NULL
) AS ranked
WHERE rn = 1
AND NOT EXISTS (
    SELECT 1 FROM CountrySequences cs 
    WHERE cs.CountryCode = CAST(SUBSTRING(CAST(ranked.WalveroId AS VARCHAR(20)), 1, 3) AS INT)
);
*/

-- Variant B: Telefon nömrələrindən ölkə kodlarını tap və sıfırdan başlat (tövsiyə olunur)
-- QEYD: Bu variant daha təhlükəsizdir, çünki mövcud WalveroId-lərdən asılı deyil

-- Azərbaycan (994) üçün
INSERT INTO CountrySequences (CountryCode, LastNumber)
SELECT 994, 0
WHERE NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 994);

-- Fransa (336) üçün
INSERT INTO CountrySequences (CountryCode, LastNumber)
SELECT 336, 0
WHERE NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 336);

-- Digər ölkələr üçün (telefon nömrələrindən avtomatik tapmaq)
-- QEYD: Bu query telefon nömrələrindən ölkə kodunu çıxarır (ilk 3 rəqəm)
-- Amma daha dəqiq olması üçün manual əlavə etmək daha yaxşıdır

-- Məsələn, Türkiyə (90) üçün:
INSERT INTO CountrySequences (CountryCode, LastNumber)
SELECT 90, 0
WHERE NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 90);

-- Rusiya (7) üçün:
INSERT INTO CountrySequences (CountryCode, LastNumber)
SELECT 7, 0
WHERE NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 7);

-- ============================================
-- 2. TENANT CUSTOMER SEQUENCES - Mövcud Customer-lərdən
-- ============================================

-- Variant A: Mövcud CardNumber-lərdən sequence hesablamaq (əgər artıq generate olunubsa)
-- QEYD: Bu variant yalnız mövcud CardNumber-lər varsa işləyir
-- Format: {prefix}{tenantId}{prefix}{seq:0000}

-- PostgreSQL üçün:
INSERT INTO TenantCustomerSequences (TenantId, LastNumber)
SELECT 
    TenantId,
    CAST(SUBSTRING(CardNumber, LENGTH(CardNumber) - 3) AS INT) AS LastNumber
FROM (
    SELECT 
        TenantId,
        CardNumber,
        ROW_NUMBER() OVER (PARTITION BY TenantId ORDER BY CardNumber DESC) AS rn
    FROM Customers
    WHERE CardNumber IS NOT NULL 
      AND LENGTH(CardNumber) >= 4
      AND CardNumber ~ '^[0-9]+$' -- Yalnız rəqəmlərdən ibarət
) AS ranked
WHERE rn = 1
ON CONFLICT (TenantId) DO NOTHING;

-- SQL Server üçün:
/*
INSERT INTO TenantCustomerSequences (TenantId, LastNumber)
SELECT 
    TenantId,
    CAST(RIGHT(CardNumber, 4) AS INT) AS LastNumber
FROM (
    SELECT 
        TenantId,
        CardNumber,
        ROW_NUMBER() OVER (PARTITION BY TenantId ORDER BY CardNumber DESC) AS rn
    FROM Customers
    WHERE CardNumber IS NOT NULL 
      AND LEN(CardNumber) >= 4
      AND CardNumber NOT LIKE '%[^0-9]%' -- Yalnız rəqəmlərdən ibarət
) AS ranked
WHERE rn = 1
AND NOT EXISTS (
    SELECT 1 FROM TenantCustomerSequences tcs 
    WHERE tcs.TenantId = ranked.TenantId
);
*/

-- Variant B: Bütün tenant-lar üçün sıfırdan başlat (tövsiyə olunur)
-- QEYD: Bu variant daha təhlükəsizdir

-- PostgreSQL üçün:
INSERT INTO TenantCustomerSequences (TenantId, LastNumber)
SELECT DISTINCT TenantId, 0
FROM Customers
WHERE TenantId NOT IN (SELECT TenantId FROM TenantCustomerSequences);

-- SQL Server üçün:
/*
INSERT INTO TenantCustomerSequences (TenantId, LastNumber)
SELECT DISTINCT TenantId, 0
FROM Customers c
WHERE NOT EXISTS (
    SELECT 1 FROM TenantCustomerSequences tcs 
    WHERE tcs.TenantId = c.TenantId
);
*/

-- ============================================
-- 3. CARD PREFIX SEQUENCE - İlkin dəyər
-- ============================================

-- PostgreSQL üçün:
INSERT INTO CardPrefixSequences (Id, LastPrefix)
VALUES (1, 789)
ON CONFLICT (Id) DO NOTHING;

-- SQL Server üçün:
/*
IF NOT EXISTS (SELECT 1 FROM CardPrefixSequences WHERE Id = 1)
    INSERT INTO CardPrefixSequences (Id, LastPrefix) 
    VALUES (1, 789);
*/

-- ============================================
-- 4. YOXLAMA QUERY-LƏRİ
-- ============================================

-- CountrySequences yoxlamaq:
SELECT * FROM CountrySequences ORDER BY CountryCode;

-- TenantCustomerSequences yoxlamaq:
SELECT * FROM TenantCustomerSequences ORDER BY TenantId;

-- CardPrefixSequences yoxlamaq:
SELECT * FROM CardPrefixSequences;

-- ============================================
-- 5. MÖVCUD DATA ÜÇÜN SEQUENCE-LƏRİ YENİLƏMƏK
-- ============================================
-- Əgər mövcud WalveroId və CardNumber-lər varsa və 
-- sequence-ləri onlara uyğunlaşdırmaq istəyirsinizsə:

-- CountrySequences yeniləmək (mövcud WalveroId-lərdən):
UPDATE CountrySequences cs
SET LastNumber = (
    SELECT MAX(CAST(SUBSTRING(CAST(WalveroId AS VARCHAR(20)), 5, 5) AS INT))
    FROM AspNetUsers
    WHERE WalveroId IS NOT NULL
      AND CAST(SUBSTRING(CAST(WalveroId AS VARCHAR(20)), 1, 3) AS INT) = cs.CountryCode
)
WHERE EXISTS (
    SELECT 1 FROM AspNetUsers
    WHERE WalveroId IS NOT NULL
      AND CAST(SUBSTRING(CAST(WalveroId AS VARCHAR(20)), 1, 3) AS INT) = cs.CountryCode
);

-- TenantCustomerSequences yeniləmək (mövcud CardNumber-lərdən):
-- QEYD: Bu query CardNumber formatından asılıdır
-- Format: {prefix}{tenantId}{prefix}{seq:0000}
-- Son 4 rəqəm sequence-dir

UPDATE TenantCustomerSequences tcs
SET LastNumber = (
    SELECT MAX(CAST(RIGHT(CardNumber, 4) AS INT))
    FROM Customers
    WHERE TenantId = tcs.TenantId
      AND CardNumber IS NOT NULL
      AND LEN(CardNumber) >= 4
      AND CardNumber NOT LIKE '%[^0-9]%'
)
WHERE EXISTS (
    SELECT 1 FROM Customers
    WHERE TenantId = tcs.TenantId
      AND CardNumber IS NOT NULL
);

