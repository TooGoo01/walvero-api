-- Optional one-time backfill script for MenuItemVariants.
-- Run this only if you did NOT include the backfill in the migration,
-- or if you need to re-run backfill (idempotent).
-- Creates one default "Regular" variant for each MenuItem that has no variants.

INSERT INTO MenuItemVariants (MenuItemId, VariantType, Code, AzName, EnName, RuName, SortOrder, IsDefault, Price, DiscountPrice, RegUserId, IsActive, RegDate)
SELECT m.Id, 1, 'Regular', N'Standart', 'Regular', N'Стандарт', 1, 1, m.Price, m.DiscountPrice, 1, 1, GETUTCDATE()
FROM MenuItems m
WHERE NOT EXISTS (SELECT 1 FROM MenuItemVariants v WHERE v.MenuItemId = m.Id);
