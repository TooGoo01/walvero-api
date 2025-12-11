# Dəyişikliklərdən Sonra Problemsiz İşləməsi Üçün Təlimatlar

## 📋 ÜMUMİ BAXIŞ

Bu dəyişikliklərlə:
- ✅ `ApplicationUser`-ə `WalveroId` əlavə edildi (avtomatik generate olunur)
- ✅ `Customer`-ə `CardNumber` əlavə edildi (avtomatik generate olunur)
- ✅ `LoyaltyCard.BarcodeValue` avtomatik olaraq `Customer.CardNumber` ilə eyniləşdirilir
- ✅ 3 yeni sequence table yaradıldı
- ✅ Cascade delete konfiqurasiyası yeniləndi

---

## 🔧 ADDIM 1: Migration Yaradın və Tətbiq Edin

### 1.1 Migration Yaradın

```powershell
dotnet ef migrations add AddCustomIdGeneration --project Loyalty.DataAccess --startup-project Loyalty.WebAPI
```

### 1.2 Migration-ı Yoxlayın

Yaradılan migration faylını yoxlayın və aşağıdakıları təsdiq edin:
- `CountrySequences` table yaradılır
- `TenantCustomerSequences` table yaradılır
- `CardPrefixSequences` table yaradılır
- `AspNetUsers` table-ına `WalveroId` column əlavə olunur
- `Customers` table-ına `CardNumber` column əlavə olunur
- Index-lər yaradılır

### 1.3 Migration-ı Tətbiq Edin

```powershell
dotnet ef database update --project Loyalty.DataAccess --startup-project Loyalty.WebAPI
```

### 1.4 İlkin Data Seed Edin (Opsional)

Migration-dan sonra `CardPrefixSequences`-ə ilkin data əlavə edin:

```sql
-- CardPrefixSequence-ə ilkin data
INSERT INTO CardPrefixSequences (Id, LastPrefix) 
VALUES (1, 789)
ON CONFLICT (Id) DO NOTHING; -- PostgreSQL üçün
-- və ya
IF NOT EXISTS (SELECT 1 FROM CardPrefixSequences WHERE Id = 1)
    INSERT INTO CardPrefixSequences (Id, LastPrefix) VALUES (1, 789); -- SQL Server üçün
```

---

## 🔧 ADDIM 2: Kod Dəyişiklikləri

### 2.1 CustomerEnrollmentService.cs - Düzəliş Lazımdır

**Fayl:** `Loyalty.Business/Services/Implementations/Main/CustomerEnrollmentService.cs`

**Problem:** `LoyaltyCard` yaradılarkən `BarcodeValue` manual set edilir və `CardNumber` da manual generate olunur.

**Dəyişiklik:**

```csharp
// ❌ KÖHNƏ KOD (Line 189-197):
var barcode = await _cardSvc.GenerateUniqueWalletCodeAsync(tenantId, program.Id, int.Parse(userId), ct);

card = new LoyaltyCard
{
    TenantId = tenantId,
    CustomerId = customer.Id,
    LoyaltyProgramId = program.Id,
    CardNumber = $"LC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}".Substring(0, 20),
    BarcodeValue = barcode, // ❌ Manual set
    BarcodeType = BarcodeType.Code128,
    Status = CardStatus.Active,
    ActivatedAt = DateTime.UtcNow,
    RegUserId = int.Parse(userId),
    RegDate = DateTime.UtcNow,
    IsActive = true
};

// ✅ YENİ KOD:
card = new LoyaltyCard
{
    TenantId = tenantId,
    CustomerId = customer.Id,
    LoyaltyProgramId = program.Id,
    // CardNumber və BarcodeValue artıq avtomatik set olunacaq SaveChangesAsync-də
    // BarcodeValue boş buraxın - avtomatik Customer.CardNumber ilə eyniləşdiriləcək
    BarcodeType = BarcodeType.Code128,
    Status = CardStatus.Active,
    ActivatedAt = DateTime.UtcNow,
    RegUserId = int.Parse(userId),
    RegDate = DateTime.UtcNow,
    IsActive = true
};
```

**Qeyd:** `GenerateUniqueWalletCodeAsync` çağırışını silin, çünki artıq lazım deyil.

---

### 2.2 LoyaltyCardService.cs - GenerateUniqueWalletCodeAsync Metodu

**Fayl:** `Loyalty.Business/Services/Implementations/Main/LoyaltyCardService.cs`

**Qeyd:** `GenerateUniqueWalletCodeAsync` metodu artıq istifadə olunmur, amma silməyin - digər yerlərdə istifadə oluna bilər. Yalnız `CustomerEnrollmentService`-dən çağırışı silin.

---

### 2.3 Customer Yaradılması - Heç Bir Dəyişiklik Lazım Deyil

**Fayl:** `Loyalty.Business/Services/Implementations/Main/CustomerEnrollmentService.cs` (Line 133-148)

**Status:** ✅ Hazır - `CardNumber` boş buraxılır, avtomatik generate olunacaq.

```csharp
customer = new Customer
{
    TenantId = tenantId,
    FirstName = user.Name?.Trim() ?? "",
    LastName = user.Surname?.Trim() ?? "",
    PhoneNumber = phone ?? "",
    Email = email,
    Gender = user.Gender,
    DateOfBirth = user.DateOfBirth,
    IsActive = true,
    LastActivityDate = DateTime.UtcNow,
    applicationUserId = int.Parse(userId)
    // CardNumber boş buraxılır - avtomatik generate olunacaq
};
```

---

## 🔧 ADDIM 3: Mövcud Data Üçün SQL Script-ləri (Opsional)

### 3.1 Mövcud Customer-lərə CardNumber Generate Etmək

Əgər mövcud `Customer`-ləriniz varsa və onlara `CardNumber` vermək istəyirsinizsə:

```sql
-- Mövcud Customer-lərə CardNumber generate etmək
-- QEYD: Bu script-i migration-dan SONRA çalıştırın

-- Hər tenant üçün sequence yaradın (əgər yoxdursa)
INSERT INTO TenantCustomerSequences (TenantId, LastNumber)
SELECT DISTINCT TenantId, 0
FROM Customers
WHERE TenantId NOT IN (SELECT TenantId FROM TenantCustomerSequences);

-- CardPrefixSequence yaradın (əgər yoxdursa)
IF NOT EXISTS (SELECT 1 FROM CardPrefixSequences WHERE Id = 1)
    INSERT INTO CardPrefixSequences (Id, LastPrefix) VALUES (1, 789);

-- Mövcud Customer-lərə CardNumber təyin edin
-- QEYD: Bu script-i C# kodunda yazmaq daha yaxşıdır, çünki sequence-ləri düzgün increment etmək lazımdır
```

**Tövsiyə:** Mövcud customer-lərə `CardNumber` vermək üçün ayrı bir C# service metodu yazın:

```csharp
public async Task UpdateExistingCustomersWithCardNumbers()
{
    var customersWithoutCardNumber = await _dbContext.Customers
        .Where(c => string.IsNullOrEmpty(c.CardNumber))
        .ToListAsync();

    foreach (var customer in customersWithoutCardNumber)
    {
        customer.CardNumber = await GenerateCardNumberAsync(customer.TenantId, CancellationToken.None);
    }

    await _dbContext.SaveChangesAsync();
}
```

---

### 3.2 Mövcud ApplicationUser-lərə WalveroId Generate Etmək

```sql
-- Mövcud user-lərə WalveroId generate etmək
-- QEYD: Bu script-i C# kodunda yazmaq daha yaxşıdır
```

**Tövsiyə:** C# service metodu:

```csharp
public async Task UpdateExistingUsersWithWalveroId()
{
    var usersWithoutWalveroId = await _dbContext.Users
        .Where(u => u.WalveroId == null && !string.IsNullOrEmpty(u.PhoneNumber))
        .ToListAsync();

    foreach (var user in usersWithoutWalveroId)
    {
        var countryCode = ExtractCountryCode(user.PhoneNumber);
        if (countryCode.HasValue)
        {
            user.WalveroId = await GenerateWalveroIdAsync(countryCode.Value, CancellationToken.None);
        }
    }

    await _dbContext.SaveChangesAsync();
}
```

---

## 🔧 ADDIM 4: Test Etmək

### 4.1 Yeni Customer Yaradın

```csharp
var customer = new Customer
{
    TenantId = 1,
    FirstName = "Test",
    LastName = "User",
    PhoneNumber = "+994501234567",
    // CardNumber boş buraxın
};

await _dbContext.Customers.AddAsync(customer);
await _dbContext.SaveChangesAsync();

// Yoxlayın:
Assert.IsNotNull(customer.CardNumber); // "78917890001" formatında olmalıdır
```

### 4.2 Yeni ApplicationUser Yaradın

```csharp
var user = new ApplicationUser
{
    UserName = "+994501234567",
    PhoneNumber = "+994501234567",
    // WalveroId boş buraxın
};

await _userManager.CreateAsync(user, "Password123");
await _dbContext.SaveChangesAsync();

// Yoxlayın:
Assert.IsNotNull(user.WalveroId); // "994100001" formatında olmalıdır
```

### 4.3 Yeni LoyaltyCard Yaradın

```csharp
var card = new LoyaltyCard
{
    CustomerId = customer.Id,
    TenantId = 1,
    LoyaltyProgramId = 1,
    // BarcodeValue boş buraxın
};

await _dbContext.LoyaltyCards.AddAsync(card);
await _dbContext.SaveChangesAsync();

// Yoxlayın:
Assert.AreEqual(customer.CardNumber, card.BarcodeValue); // Eyni olmalıdır
```

### 4.4 Cascade Delete Test Edin

```csharp
// ApplicationUser silin
await _userManager.DeleteAsync(user);

// Yoxlayın: Customer-lər də silinməlidir
var customers = await _dbContext.Customers
    .Where(c => c.applicationUserId == user.Id)
    .ToListAsync();
Assert.AreEqual(0, customers.Count);

// Customer silin
await _dbContext.Customers.Remove(customer);
await _dbContext.SaveChangesAsync();

// Yoxlayın: LoyaltyCard-lər də silinməlidir
var cards = await _dbContext.LoyaltyCards
    .Where(c => c.CustomerId == customer.Id)
    .ToListAsync();
Assert.AreEqual(0, cards.Count);
```

---

## ⚠️ DİQQƏT EDİLMƏLİ MƏSƏLƏLƏR

### 1. UnitOfWork Commit vs SaveChangesAsync

Əgər `UnitOfWork.Commit()` istifadə edirsinizsə, `SaveChangesAsync` çağırılmalıdır. Yoxlayın:

```csharp
// CustomerEnrollmentService.cs-də:
await _uow.Repository<Customer>().AddAsync(customer);
_uow.Commit(); // Bu SaveChangesAsync çağırmalıdır
```

### 2. Concurrent Inserts

Yüksək concurrency-də sequence-lər düzgün işləməlidir. EF Core avtomatik olaraq transaction istifadə edir, amma test edin.

### 3. Country Code Extraction

`ExtractCountryCode` metodu telefon nömrəsindən ölkə kodunu çıxarır. Əgər düzgün işləmirsə, `ApplicationUser`-ə `CountryCode` field əlavə edin.

---

## 📝 XÜLASƏ

### ✅ Dəyişiklik Lazım Olan Fayllar:

1. **CustomerEnrollmentService.cs** (Line 189-197)
   - `GenerateUniqueWalletCodeAsync` çağırışını silin
   - `BarcodeValue` və `CardNumber` manual set etməyin

### ✅ Dəyişiklik Lazım Olmayan Fayllar:

- `Customer` yaradılması - artıq düzgündür
- `ApplicationUser` yaradılması - artıq düzgündür

### ✅ Migration Addımları:

1. Migration yaradın
2. Migration-ı tətbiq edin
3. İlkin data seed edin (CardPrefixSequence)
4. Mövcud data üçün update script-ləri çalıştırın (opsional)

---

## 🚀 Növbəti Addımlar

1. ✅ Migration yaradın və tətbiq edin
2. ✅ `CustomerEnrollmentService.cs`-də düzəliş edin
3. ✅ Test edin
4. ✅ Production-a deploy edin

