# Web Layihəsi üçün Sequence Generation Setup

## 📋 Ümumi Baxış

Web layihəniz eyni verilənlər bazasını istifadə edirsə, eyni sequence generation məntiqini istifadə edə bilərsiniz.

---

## 🎯 Yanaşma 1: Shared Library İstifadə Etmək (Tövsiyə Olunur)

Əgər web layihəniz **aynı solution-da** və ya **shared library** istifadə edirsə:

### Addım 1: Project Reference-ləri Yoxlayın

Web layihənizin `.csproj` faylında aşağıdakı reference-lər olmalıdır:

```xml
<ItemGroup>
  <ProjectReference Include="..\Loyalty.DataAccess\Loyalty.DataAccess.csproj" />
  <ProjectReference Include="..\Loyalty.Business\Loyalty.Business.csproj" />
</ItemGroup>
```

### Addım 2: Dependency Injection Konfiqurasiyası

Web layihənizin `Program.cs` və ya `Startup.cs` faylında:

```csharp
using Loyalty.DataAccess;
using Loyalty.Business.Services;

// DataAccess servisləri (SequenceRepository daxil)
builder.Services.AddDataAccessServices();

// Business servisləri (CardNumberGenerator, WalveroIdGenerator daxil)
builder.Services.AddBusinessServices(builder.Configuration);
```

### Addım 3: Migration Skriptini Çalışdırın

**Eyni migration skripti** işləyəcək, çünki eyni verilənlər bazasını istifadə edirsiniz:

```sql
-- Loyalty.DataAccess/Migrations/20251219140448_optimize_sequence_generation.sql
-- Bu skripti SQL Server-də çalışdırın
```

### Addım 4: Servisləri İstifadə Edin

Web layihənizdə Customer və ya ApplicationUser yaradarkən:

```csharp
using Loyalty.Business.Services.Abstractions.Sequences;

public class YourService
{
    private readonly ICardNumberGenerator _cardNumberGenerator;
    private readonly IWalveroIdGenerator _walveroIdGenerator;

    public YourService(
        ICardNumberGenerator cardNumberGenerator,
        IWalveroIdGenerator walveroIdGenerator)
    {
        _cardNumberGenerator = cardNumberGenerator;
        _walveroIdGenerator = walveroIdGenerator;
    }

    public async Task CreateCustomerAsync(int tenantId, CancellationToken ct = default)
    {
        // CardNumber generate et
        var cardNumber = await _cardNumberGenerator.GenerateCardNumberAsync(tenantId, ct);

        var customer = new Customer
        {
            TenantId = tenantId,
            CardNumber = cardNumber, // Generate edilmiş CardNumber
            // ... digər property-lər
        };

        // Save et
        await _dbContext.Customers.AddAsync(customer, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task CreateUserAsync(string phoneNumber, CancellationToken ct = default)
    {
        // WalveroId generate et
        var countryCode = _walveroIdGenerator.ExtractCountryCode(phoneNumber);
        if (countryCode.HasValue)
        {
            var walveroId = await _walveroIdGenerator.GenerateWalveroIdAsync(countryCode.Value, ct);

            var user = new ApplicationUser
            {
                PhoneNumber = phoneNumber,
                WalveroId = walveroId, // Generate edilmiş WalveroId
                // ... digər property-lər
            };

            // Save et
            await _userManager.CreateAsync(user, password);
        }
    }
}
```

---

## 🎯 Yanaşma 2: Ayrı Web Layihəsi (Kod Kopyalama)

Əgər web layihəniz **tam ayrıdırsa** və shared library istifadə edə bilmirsə:

### Addım 1: Sequence Entity-ləri Kopyalayın

Web layihənizdə eyni entity-ləri yaradın:

```csharp
// Entities/Sequences/TenantCustomerSequence.cs
public class TenantCustomerSequence
{
    public int TenantId { get; set; }
    public int LastNumber { get; set; } = 0;
    public string Prefix { get; set; } = "789";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// Entities/Sequences/CountrySequence.cs
public class CountrySequence
{
    public int CountryCode { get; set; }
    public int LastNumber { get; set; } = 0;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

### Addım 2: SequenceRepository Kopyalayın

`Loyalty.DataAccess/Repositories/Implementations/Sequences/SequenceRepository.cs` faylını web layihənizə kopyalayın.

**Qeyd:** Dapper paketi lazımdır:
```xml
<PackageReference Include="Dapper" Version="2.1.35" />
<PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.2" />
```

### Addım 3: Generator Servisləri Kopyalayın

- `ICardNumberGenerator` / `CardNumberGenerator`
- `IWalveroIdGenerator` / `WalveroIdGenerator`

Bu faylları web layihənizə kopyalayın:
- `Loyalty.Business/Services/Abstractions/Sequences/ICardNumberGenerator.cs`
- `Loyalty.Business/Services/Abstractions/Sequences/IWalveroIdGenerator.cs`
- `Loyalty.Business/Services/Implementations/Sequences/CardNumberGenerator.cs`
- `Loyalty.Business/Services/Implementations/Sequences/WalveroIdGenerator.cs`

### Addım 4: Dependency Injection

```csharp
// SequenceRepository
services.AddScoped<ISequenceRepository, SequenceRepository>();

// Generators
services.AddScoped<ICardNumberGenerator, CardNumberGenerator>();
services.AddScoped<IWalveroIdGenerator, WalveroIdGenerator>();
```

### Addım 5: Migration Skripti

**Eyni migration skripti** işləyəcək (eyni DB istifadə edirsiniz).

---

## ✅ Yoxlama

### 1. Migration Skriptini Çalışdırın

```sql
-- SQL Server Management Studio-da
-- Loyalty.DataAccess/Migrations/20251219140448_optimize_sequence_generation.sql
```

### 2. Test Edin

```csharp
// Web layihənizdə test
var cardNumber = await _cardNumberGenerator.GenerateCardNumberAsync(tenantId);
var walveroId = await _walveroIdGenerator.GenerateWalveroIdAsync(994);

// Hər biri unikal olmalıdır
```

### 3. Concurrent Test

Eyni anda bir neçə request göndərin - duplicate error olmamalıdır.

---

## 🔍 Troubleshooting

### Problem 1: "ISequenceRepository not found"
**Həll:** `AddDataAccessServices()` çağırıldığından əmin olun.

### Problem 2: "Dapper not found"
**Həll:** Web layihəsinə Dapper paketi əlavə edin:
```xml
<PackageReference Include="Dapper" Version="2.1.35" />
```

### Problem 3: "Table does not exist"
**Həll:** Migration skriptini çalışdırın.

### Problem 4: "Duplicate key error"
**Həll:** Retry məntiqini yoxlayın - generator servislərdə retry olmalıdır.

---

## 📝 Nümunə: Web Layihəsində Customer Yaradılması

```csharp
public class CustomerService
{
    private readonly ICardNumberGenerator _cardNumberGenerator;
    private readonly ApplicationDbContext _dbContext;

    public CustomerService(
        ICardNumberGenerator cardNumberGenerator,
        ApplicationDbContext dbContext)
    {
        _cardNumberGenerator = cardNumberGenerator;
        _dbContext = dbContext;
    }

    public async Task<Customer> CreateCustomerAsync(
        int tenantId, 
        string firstName, 
        string lastName, 
        string phoneNumber,
        CancellationToken ct = default)
    {
        // 1. CardNumber generate et
        var cardNumber = await _cardNumberGenerator.GenerateCardNumberAsync(tenantId, ct);

        // 2. Customer yarat
        var customer = new Customer
        {
            TenantId = tenantId,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            CardNumber = cardNumber, // Generate edilmiş
            IsActive = true,
            LastActivityDate = DateTime.UtcNow
        };

        // 3. Save et (retry məntiqini əlavə edin)
        try
        {
            await _dbContext.Customers.AddAsync(customer, ct);
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
        {
            // Retry: yeni CardNumber generate et
            customer.CardNumber = await _cardNumberGenerator.GenerateCardNumberAsync(tenantId, ct);
            await _dbContext.SaveChangesAsync(ct);
        }

        return customer;
    }
}
```

---

## 🎯 Tövsiyə

**Yanaşma 1 (Shared Library)** istifadə edin, çünki:
- ✅ Kod təkrarlanmır
- ✅ Maintainability yaxşıdır
- ✅ Eyni məntiq hər iki layihədə işləyir
- ✅ Migration skripti eynidir

---

## 📞 Dəstək

Əgər problem yaşayırsınızsa:
1. Web layihənizin `.csproj` faylını yoxlayın (reference-lər)
2. `Program.cs` / `Startup.cs`-də DI konfiqurasiyasını yoxlayın
3. Migration skriptinin çalışdığını yoxlayın
4. Verilənlər bazası connection string-ini yoxlayın

