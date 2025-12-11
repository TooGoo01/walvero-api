-- ============================================
-- SEQUENCE TABLE-LƏRİNİ SEED ETMƏK ÜÇÜN SADƏ SQL
-- ============================================
-- Bu script-i migration-dan SONRA çalıştırın
-- ============================================

-- ============================================
-- 1. CARD PREFIX SEQUENCE (Mütləq)
-- ============================================

-- PostgreSQL:
INSERT INTO CardPrefixSequences (Id, LastPrefix)
VALUES (1, 789)
ON CONFLICT (Id) DO NOTHING;

-- SQL Server:
/*
IF NOT EXISTS (SELECT 1 FROM CardPrefixSequences WHERE Id = 1)
    INSERT INTO CardPrefixSequences (Id, LastPrefix) VALUES (1, 789);
*/

-- ============================================
-- 2. COUNTRY SEQUENCES (İstifadə olunan ölkələr üçün)
-- ============================================

-- Azərbaycan (994)
INSERT INTO CountrySequences (CountryCode, LastNumber)
SELECT 994, 0
WHERE NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 994);

-- Fransa (336)
INSERT INTO CountrySequences (CountryCode, LastNumber)
SELECT 336, 0
WHERE NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 336);

-- Türkiyə (90)
INSERT INTO CountrySequences (CountryCode, LastNumber)
SELECT 90, 0
WHERE NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 90);

-- Rusiya (7)
INSERT INTO CountrySequences (CountryCode, LastNumber)
SELECT 7, 0
WHERE NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 7);

-- Digər ölkələr üçün eyni şəkildə əlavə edin...

-- ============================================
-- 3. TENANT CUSTOMER SEQUENCES (Bütün tenant-lar üçün)
-- ============================================

-- PostgreSQL:
INSERT INTO TenantCustomerSequences (TenantId, LastNumber)
SELECT DISTINCT TenantId, 0
FROM Customers
WHERE TenantId NOT IN (SELECT TenantId FROM TenantCustomerSequences);

-- SQL Server:
/*
INSERT INTO TenantCustomerSequences (TenantId, LastNumber)
SELECT DISTINCT c.TenantId, 0
FROM Customers c
WHERE NOT EXISTS (
    SELECT 1 FROM TenantCustomerSequences tcs 
    WHERE tcs.TenantId = c.TenantId
);
*/

-- ============================================
-- 4. YOXLAMA
-- ============================================

SELECT 'CountrySequences' AS TableName, COUNT(*) AS Count FROM CountrySequences
UNION ALL
SELECT 'TenantCustomerSequences', COUNT(*) FROM TenantCustomerSequences
UNION ALL
SELECT 'CardPrefixSequences', COUNT(*) FROM CardPrefixSequences;

