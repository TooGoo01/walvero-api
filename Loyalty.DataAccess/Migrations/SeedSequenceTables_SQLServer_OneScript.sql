-- ============================================
-- SEQUENCE TABLE-LƏRİNİ SEED ETMƏK ÜÇÜN SQL SERVER SCRIPT (BİR SCRIPT)
-- ============================================
-- Migration-dan SONRA bu script-i çalıştırın
-- Database adını dəyişdirməyə ehtiyac yoxdur
-- ============================================

-- ============================================
-- 1. CARD PREFIX SEQUENCE
-- ============================================
IF NOT EXISTS (SELECT 1 FROM CardPrefixSequences WHERE Id = 1)
    INSERT INTO CardPrefixSequences (Id, LastPrefix) VALUES (1, 789);

-- ============================================
-- 2. COUNTRY SEQUENCES
-- ============================================
-- Azərbaycan (994)
IF NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 994)
    INSERT INTO CountrySequences (CountryCode, LastNumber) VALUES (994, 0);

-- Kuwait (965)
IF NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 965)
    INSERT INTO CountrySequences (CountryCode, LastNumber) VALUES (965, 0);

-- Saudi Arabia (966)
IF NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 966)
    INSERT INTO CountrySequences (CountryCode, LastNumber) VALUES (966, 0);

-- Uzbekistan (998)
IF NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 998)
    INSERT INTO CountrySequences (CountryCode, LastNumber) VALUES (998, 0);

-- UAE (971)
IF NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 971)
    INSERT INTO CountrySequences (CountryCode, LastNumber) VALUES (971, 0);

-- Qatar (974)
IF NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 974)
    INSERT INTO CountrySequences (CountryCode, LastNumber) VALUES (974, 0);

-- Oman (968)
IF NOT EXISTS (SELECT 1 FROM CountrySequences WHERE CountryCode = 968)
    INSERT INTO CountrySequences (CountryCode, LastNumber) VALUES (968, 0);

-- ============================================
-- 3. TENANT CUSTOMER SEQUENCES
-- ============================================
INSERT INTO TenantCustomerSequences (TenantId, LastNumber)
SELECT DISTINCT c.TenantId, 0
FROM Customers c
WHERE NOT EXISTS (
    SELECT 1 FROM TenantCustomerSequences tcs 
    WHERE tcs.TenantId = c.TenantId
);

-- ============================================
-- 4. YOXLAMA
-- ============================================
SELECT 'CountrySequences' AS TableName, COUNT(*) AS Count FROM CountrySequences
UNION ALL
SELECT 'TenantCustomerSequences', COUNT(*) FROM TenantCustomerSequences
UNION ALL
SELECT 'CardPrefixSequences', COUNT(*) FROM CardPrefixSequences;

