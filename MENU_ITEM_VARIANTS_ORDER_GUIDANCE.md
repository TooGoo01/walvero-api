# Order/Cart Integration for Menu Item Variants

No order or cart entities exist in this codebase. When you implement orders/cart, apply these changes.

## Order item entity (when added)

Include these fields on your order line item:

- `MenuItemVariantId` (int, FK to MenuItemVariants) — required; identifies which variant was ordered.
- `UnitPrice` (double) — snapshot of variant price at order time.
- `DiscountPrice` (double) — snapshot of variant discount price at order time.
- `Currency` (string) — from MenuItem at order time.
- `CurrencySign` (string) — from MenuItem at order time.

## Cart item entity (when added)

Same pattern as order item:

- `MenuItemVariantId` — which variant is in the cart.
- `UnitPrice` / `DiscountPrice` — snapshots when added to cart (or resolve at checkout).
- `Currency` / `CurrencySign` — from MenuItem.

## API changes

- Add-to-cart / create-order requests should accept `menuItemVariantId` instead of (or in addition to) `menuItemId`.
- Ensure validation: the variant belongs to the menu item and is active.

## Data flow

1. Client gets menu items with `Variants` and `DefaultVariantId`.
2. For single-variant items, client uses `DefaultVariantId` and hides the variant selector.
3. On add-to-cart or place-order, client sends `menuItemVariantId`.
4. Server snapshots `UnitPrice`, `DiscountPrice`, `Currency`, `CurrencySign` from the variant and menu item.
