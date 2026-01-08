-- ============================================
-- Migration: Optimize CardNumber/WalveroId Generation
-- Date: 2025-12-19
-- ============================================
-- This migration optimizes sequence generation by:
-- 1. Adding Prefix column to TenantCustomerSequences
-- 2. Ensuring unique indexes exist for CardNumber and WalveroId
-- 3. Removing dependency on scanning Customers table
-- ============================================

-- ============================================
-- 1. Add Prefix column to TenantCustomerSequences (if not exists)
-- ============================================
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID('dbo.TenantCustomerSequences') 
    AND name = 'Prefix'
)
BEGIN
    ALTER TABLE dbo.TenantCustomerSequences
    ADD Prefix NVARCHAR(5) NOT NULL DEFAULT '789';
    
    PRINT 'Added Prefix column to TenantCustomerSequences';
END
GO

-- ============================================
-- 2. Add UpdatedAt column to TenantCustomerSequences (if not exists)
-- ============================================
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID('dbo.TenantCustomerSequences') 
    AND name = 'UpdatedAt'
)
BEGIN
    ALTER TABLE dbo.TenantCustomerSequences
    ADD UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
    
    PRINT 'Added UpdatedAt column to TenantCustomerSequences';
END
GO

-- ============================================
-- 3. Add UpdatedAt column to CountrySequences (if not exists)
-- ============================================
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID('dbo.CountrySequences') 
    AND name = 'UpdatedAt'
)
BEGIN
    ALTER TABLE dbo.CountrySequences
    ADD UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
    
    PRINT 'Added UpdatedAt column to CountrySequences';
END
GO

-- ============================================
-- 4. Ensure unique index on Customers.CardNumber (filtered for non-null)
-- ============================================
IF NOT EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE object_id = OBJECT_ID('dbo.Customers') 
    AND name = 'IX_Customers_CardNumber'
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_Customers_CardNumber
    ON dbo.Customers(CardNumber)
    WHERE CardNumber IS NOT NULL;
    
    PRINT 'Created unique index IX_Customers_CardNumber';
END
GO

-- ============================================
-- 5. Ensure unique index on AspNetUsers.WalveroId (filtered for non-null)
-- ============================================
IF NOT EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE object_id = OBJECT_ID('dbo.AspNetUsers') 
    AND name = 'IX_AspNetUsers_WalveroId'
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_AspNetUsers_WalveroId
    ON dbo.AspNetUsers(WalveroId)
    WHERE WalveroId IS NOT NULL;
    
    PRINT 'Created unique index IX_AspNetUsers_WalveroId';
END
GO

-- ============================================
-- 6. Initialize TenantCustomerSequences with Prefix for existing records
-- ============================================
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TenantCustomerSequences' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    UPDATE dbo.TenantCustomerSequences
    SET Prefix = '789'
    WHERE Prefix IS NULL OR Prefix = '';

    PRINT 'Initialized Prefix values in TenantCustomerSequences';
END
ELSE
BEGIN
    PRINT 'TenantCustomerSequences table does not exist yet. Skipping initialization.';
END
GO

-- ============================================
-- Migration Complete
-- ============================================
PRINT 'Migration completed successfully!';
PRINT 'Sequence generation is now optimized with atomic SQL Server UPDATE...OUTPUT';

