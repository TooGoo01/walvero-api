# Custom ID and Card Number Generation - Implementation Summary

## ✅ Implementation Complete

All three goals have been implemented using EF Core without database triggers or raw SQL.

---

## 📋 Goal 1: WalveroId per User (per Country)

### ✅ What Was Added:

1. **ApplicationUser Entity:**
   - Added `WalveroId` property (long?, nullable)
   - Unique index on `WalveroId` (filtered for non-null values)

2. **CountrySequence Entity:**
   - `CountryCode` (int, PK) - e.g., 994, 336
   - `LastNumber` (int) - Last sequence number used

3. **Generation Logic:**
   - Pattern: `{countryCode}{1}{seq:00000}`
   - Examples: `994100001`, `994100002`, `336100001`
   - Country code extracted from `PhoneNumber` field
   - Never reused (sequence continues even if users deleted)

### 🔧 How It Works:

- When a new `ApplicationUser` is added with a `PhoneNumber`:
  1. Extract country code from phone (supports +994, 994, 0 prefixes)
  2. Find or create `CountrySequence` for that country
  3. Increment `LastNumber`
  4. Generate: `{countryCode}1{LastNumber:D5}`
  5. Assign to user's `WalveroId`

---

## 📋 Goal 2: Tenant-Specific Card Numbers with Dynamic Prefix

### ✅ What Was Added:

1. **Customer Entity:**
   - Added `CardNumber` property (string?, nullable)
   - Unique index on `CardNumber` (filtered for non-null values)

2. **Sequence Entities:**
   - `TenantCustomerSequence` - Per-tenant sequence (TenantId, LastNumber)
   - `CardPrefixSequence` - Global prefix sequence (Id=1, LastPrefix)

3. **Generation Logic:**
   - Pattern: `{prefix}{tenantId}{prefix}{seq:0000}`
   - Examples:
     - Tenant 1: `78917890001`, `78917890002`, ...
     - Tenant 2: `78927890001`, `78927890002`, ...
   - Prefix increments globally (789 → 790 → 791...)
   - Sequence resets per tenant when prefix changes

### 🔧 How It Works:

- When a new `Customer` is added with empty `CardNumber`:
  1. Get or create `TenantCustomerSequence` for the tenant
  2. Get or create `CardPrefixSequence` (Id=1)
  3. Increment tenant's `LastNumber`
  4. If sequence > 9999, increment prefix and reset sequence to 1
  5. Generate: `{prefix}{tenantId}{prefix}{seq:D4}`
  6. Assign to customer's `CardNumber`

---

## 📋 Goal 3: Cascade Deletes

### ✅ What Was Configured:

1. **ApplicationUser → Customer:**
   - Already configured: `OnDelete(DeleteBehavior.Cascade)`
   - ✅ When user deleted → all customers deleted

2. **Customer → LoyaltyCard:**
   - Updated: `OnDelete(DeleteBehavior.Cascade)` (was Restrict)
   - ✅ When customer deleted → all loyalty cards deleted

---

## 📁 Files Created/Modified

### New Files:
- `Loyalty.DataAccess/Entities/Sequences/CountrySequence.cs`
- `Loyalty.DataAccess/Entities/Sequences/TenantCustomerSequence.cs`
- `Loyalty.DataAccess/Entities/Sequences/CardPrefixSequence.cs`

### Modified Files:
- `Loyalty.DataAccess/Entities/Users/ApplicationUser.cs` - Added `WalveroId`
- `Loyalty.DataAccess/Entities/Main/Customer.cs` - Added `CardNumber`
- `Loyalty.DataAccess/Context/LoyaltyWalletDbContext.cs`:
  - Added DbSets for sequence entities
  - Added entity configurations
  - Overrode `SaveChangesAsync` with generation logic
  - Updated cascade delete for Customer → LoyaltyCard

---

## 🔄 SaveChangesAsync Flow

```csharp
1. Process EntityBase timestamps (RegDate, EditDate)
2. For new ApplicationUsers without WalveroId:
   - Extract country code from PhoneNumber
   - Generate WalveroId using CountrySequence
3. For new Customers without CardNumber:
   - Generate CardNumber using TenantCustomerSequence + CardPrefixSequence
4. Save all changes (transactional)
```

---

## ⚠️ Important Notes

### Country Code Extraction:
- Currently extracts from `PhoneNumber` field
- Supports formats: `+994501234567`, `994501234567`, `0501234567`
- For local format (starts with 0), defaults to 994 (Azerbaijan)
- **Recommendation:** Consider adding explicit `CountryCode` field to `ApplicationUser` for better accuracy

### Card Number Prefix Logic:
- Prefix increments when tenant sequence exceeds 9999
- You can adjust this logic in `GenerateCardNumberAsync` method
- Starting prefix is 789 (configurable in `CardPrefixSequence`)

### Concurrency:
- All sequence updates happen within the same EF transaction
- EF Core handles concurrency automatically via optimistic concurrency
- For high-concurrency scenarios, consider using database-level row locking

---

## 🚀 Next Steps

1. **Create Migration:**
   ```powershell
   dotnet ef migrations add AddCustomIdGeneration --project Loyalty.DataAccess --startup-project Loyalty.WebAPI
   ```

2. **Review Migration:**
   - Check that sequence tables are created
   - Verify indexes on `WalveroId` and `CardNumber`
   - Confirm cascade delete settings

3. **Seed Initial Data (Optional):**
   ```sql
   -- Seed CardPrefixSequence
   INSERT INTO CardPrefixSequences (Id, LastPrefix) VALUES (1, 789);
   ```

4. **Test:**
   - Create new users → verify WalveroId generation
   - Create new customers → verify CardNumber generation
   - Delete user → verify customers are cascade deleted
   - Delete customer → verify loyalty cards are cascade deleted

---

## 📝 Example Usage

### Creating a User (WalveroId auto-generated):
```csharp
var user = new ApplicationUser
{
    UserName = "+994501234567",
    PhoneNumber = "+994501234567",
    // ... other properties
};
await _userManager.CreateAsync(user, password);
// WalveroId will be auto-generated: 994100001, 994100002, etc.
```

### Creating a Customer (CardNumber auto-generated):
```csharp
var customer = new Customer
{
    TenantId = 1,
    FirstName = "John",
    LastName = "Doe",
    PhoneNumber = "+994501234567",
    // CardNumber will be auto-generated: 78917890001, 78917890002, etc.
};
await _dbContext.Customers.AddAsync(customer);
await _dbContext.SaveChangesAsync();
```

---

## ✅ All Requirements Met

- ✅ No database triggers
- ✅ No raw SQL
- ✅ Pure C# / EF Core implementation
- ✅ Transactional (all in SaveChangesAsync)
- ✅ Cascade deletes configured
- ✅ Unique constraints with indexes
- ✅ Never reuses deleted IDs
- ✅ Per-country sequences for WalveroId
- ✅ Per-tenant sequences for CardNumber
- ✅ Global incrementing prefix for CardNumber

