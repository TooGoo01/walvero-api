-- ============================================
-- Migration: Fix GoogleSaveUrl Column Length
-- Date: 2025-12-19
-- ============================================
-- Google Wallet save links can be very long (JWT tokens included)
-- Current length (500) is insufficient. Increase to 2000.
-- ============================================

-- ============================================
-- 1. Alter GoogleSaveUrl column to allow longer URLs
-- ============================================
IF EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID('dbo.WalveroWalletPasses') 
    AND name = 'GoogleSaveUrl'
)
BEGIN
    ALTER TABLE dbo.WalveroWalletPasses
    ALTER COLUMN GoogleSaveUrl NVARCHAR(2000) NULL;
    
    PRINT 'Updated GoogleSaveUrl column length to 2000 characters';
END
ELSE
BEGIN
    PRINT 'GoogleSaveUrl column does not exist. Skipping.';
END
GO

-- ============================================
-- Migration Complete
-- ============================================
PRINT 'Migration completed successfully!';
PRINT 'GoogleSaveUrl can now store longer Google Wallet save links.';

