# Digər Layihə Üçün Dəyişikliklər Siyahısı

Bu sənəddə bu layihədə edilən bütün dəyişikliklər siyahıya alınmışdır. Digər layihənizdə eyni dəyişiklikləri edin.

---

## 📁 1. LOYALTY.DATAACCESS LAYİHƏSİ

### ✅ 1.1. Yeni Fayllar

#### `Repositories/Abstractions/Sequences/ISequenceRepository.cs`
**Yoxlayın:** Bu fayl mövcuddurmu? Əgər yoxdursa, yaradın:
```csharp
namespace Loyalty.DataAccess.Repositories.Abstractions.Sequences
{
    /// <summary>
    /// Repository for atomic sequence generation using SQL Server UPDATE...OUTPUT
    /// </summary>
    public interface ISequenceRepository
    {
        /// <summary>
        /// Atomically increments and returns the next sequence number for a tenant.
        /// Handles prefix rollover when LastNumber >= 9999.
        /// </summary>
        /// <param name="tenantId">Tenant ID</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Tuple of (LastNumber, Prefix)</returns>
        Task<(int LastNumber, string Prefix)> GetNextTenantSequenceAsync(int tenantId, CancellationToken ct = default);

        /// <summary>
        /// Atomically increments and returns the next sequence number for a country.
        /// </summary>
        /// <param name="countryCode">Country code (e.g., 994, 336)</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>LastNumber</returns>
        Task<int> GetNextCountrySequenceAsync(int countryCode, CancellationToken ct = default);
    }
}
```

#### `Repositories/Implementations/Sequences/SequenceRepository.cs`
**Yoxlayın:** Bu fayl mövcuddurmu? Əgər yoxdursa, yaradın:

**Tam kod:**
```csharp
using Dapper;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Repositories.Abstractions.Sequences;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Loyalty.DataAccess.Repositories.Implementations.Sequences
{
    /// <summary>
    /// Repository for atomic sequence generation using SQL Server UPDATE...OUTPUT
    /// Uses UPDLOCK + HOLDLOCK to prevent race conditions
    /// </summary>
    public class SequenceRepository : ISequenceRepository
    {
        private readonly LoyaltyWalletDbContext _dbContext;
        private readonly ILogger<SequenceRepository>? _logger;

        public SequenceRepository(LoyaltyWalletDbContext dbContext, ILogger<SequenceRepository>? logger = null)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger;
        }

        private IDbConnection GetConnection()
        {
            return _dbContext.Database.GetDbConnection();
        }

        public async Task<(int LastNumber, string Prefix)> GetNextTenantSequenceAsync(int tenantId, CancellationToken ct = default)
        {
            const string sql = @"
                DECLARE @OutputTable TABLE (LastNumber INT, Prefix NVARCHAR(5));

                -- Ensure row exists (idempotent insert)
                IF NOT EXISTS (SELECT 1 FROM TenantCustomerSequences WITH (UPDLOCK, HOLDLOCK) WHERE TenantId = @TenantId)
                BEGIN
                    INSERT INTO TenantCustomerSequences (TenantId, LastNumber, Prefix, UpdatedAt)
                    VALUES (@TenantId, 0, '789', GETUTCDATE());
                END

                -- Atomically increment and return
                -- Prefix rollover: if LastNumber >= 9999, increment prefix (789 -> 790 -> 791...)
                UPDATE TenantCustomerSequences WITH (UPDLOCK, HOLDLOCK)
                SET 
                    LastNumber = CASE 
                        WHEN LastNumber >= 9999 THEN 1 
                        ELSE LastNumber + 1 
                    END,
                    Prefix = CASE 
                        WHEN LastNumber >= 9999 THEN CAST(CAST(Prefix AS INT) + 1 AS NVARCHAR(5))
                        ELSE Prefix
                    END,
                    UpdatedAt = GETUTCDATE()
                OUTPUT INSERTED.LastNumber, INSERTED.Prefix
                INTO @OutputTable (LastNumber, Prefix)
                WHERE TenantId = @TenantId;

                SELECT LastNumber, Prefix FROM @OutputTable;
            ";

            try
            {
                var connection = GetConnection();
                if (connection.State != ConnectionState.Open)
                    connection.Open();

                var result = await connection.QueryFirstOrDefaultAsync<(int LastNumber, string Prefix)>(
                    sql,
                    new { TenantId = tenantId },
                    commandTimeout: 30
                );

                if (result.LastNumber == 0 && string.IsNullOrEmpty(result.Prefix))
                {
                    _logger?.LogWarning("Sequence generation returned default values for TenantId: {TenantId}", tenantId);
                    result = (1, "789");
                }

                _logger?.LogDebug("Generated sequence for TenantId {TenantId}: LastNumber={LastNumber}, Prefix={Prefix}",
                    tenantId, result.LastNumber, result.Prefix);

                return result;
            }
            catch (SqlException ex) when (ex.Number == 1205) // Deadlock
            {
                _logger?.LogWarning(ex, "Deadlock detected for TenantId {TenantId}, retrying...", tenantId);
                await Task.Delay(100, ct);
                return await GetNextTenantSequenceAsync(tenantId, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error generating sequence for TenantId {TenantId}", tenantId);
                throw;
            }
        }

        public async Task<int> GetNextCountrySequenceAsync(int countryCode, CancellationToken ct = default)
        {
            const string sql = @"
                DECLARE @OutputTable TABLE (LastNumber INT);

                -- Ensure row exists (idempotent insert)
                IF NOT EXISTS (SELECT 1 FROM CountrySequences WITH (UPDLOCK, HOLDLOCK) WHERE CountryCode = @CountryCode)
                BEGIN
                    INSERT INTO CountrySequences (CountryCode, LastNumber, UpdatedAt)
                    VALUES (@CountryCode, 0, GETUTCDATE());
                END

                -- Atomically increment and return
                UPDATE CountrySequences WITH (UPDLOCK, HOLDLOCK)
                SET 
                    LastNumber = LastNumber + 1,
                    UpdatedAt = GETUTCDATE()
                OUTPUT INSERTED.LastNumber
                INTO @OutputTable (LastNumber)
                WHERE CountryCode = @CountryCode;

                SELECT LastNumber FROM @OutputTable;
            ";

            try
            {
                var connection = GetConnection();
                if (connection.State != ConnectionState.Open)
                    connection.Open();

                var result = await connection.QueryFirstOrDefaultAsync<int>(
                    sql,
                    new { CountryCode = countryCode },
                    commandTimeout: 30
                );

                if (result == 0)
                {
                    _logger?.LogWarning("Sequence generation returned 0 for CountryCode: {CountryCode}", countryCode);
                    result = 1;
                }

                _logger?.LogDebug("Generated sequence for CountryCode {CountryCode}: LastNumber={LastNumber}",
                    countryCode, result);

                return result;
            }
            catch (SqlException ex) when (ex.Number == 1205) // Deadlock
            {
                _logger?.LogWarning(ex, "Deadlock detected for CountryCode {CountryCode}, retrying...", countryCode);
                await Task.Delay(100, ct);
                return await GetNextCountrySequenceAsync(countryCode, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error generating sequence for CountryCode {CountryCode}", countryCode);
                throw;
            }
        }
    }
}
```

---

### ✅ 1.2. Entity Dəyişiklikləri

#### `Entities/Sequences/TenantCustomerSequence.cs`
**Yoxlayın:** Bu entity-də aşağıdakı property-lər varmı?
```csharp
public class TenantCustomerSequence : EntityBase, IEntity
{
    public int TenantId { get; set; }
    public int LastNumber { get; set; } = 0;
    public string Prefix { get; set; } = "789"; // ✅ BU PROPERTY ƏLAVƏ EDİN
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow; // ✅ BU PROPERTY ƏLAVƏ EDİN
    public byte[]? RowVersion { get; set; }
}
```

#### `Entities/Sequences/CountrySequence.cs`
**Yoxlayın:** Bu entity-də `UpdatedAt` property-si varmı?
```csharp
public class CountrySequence : EntityBase, IEntity
{
    public int CountryCode { get; set; }
    public int LastNumber { get; set; } = 0;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow; // ✅ BU PROPERTY ƏLAVƏ EDİN
    public byte[]? RowVersion { get; set; }
}
```

---

### ✅ 1.3. DbContext Dəyişiklikləri

#### `Context/LoyaltyWalletDbContext.cs`

**1. Entity Konfiqurasiyası - CountrySequence:**
```csharp
// ==== COUNTRY SEQUENCE ====
modelBuilder.Entity<CountrySequence>(entity =>
{
    entity.ToTable("CountrySequences");
    entity.HasKey(cs => cs.CountryCode);
    entity.Property(cs => cs.CountryCode).ValueGeneratedNever();
    entity.Property(cs => cs.LastNumber).IsRequired();
    entity.Property(cs => cs.UpdatedAt).IsRequired(); // ✅ BU SƏTRİ ƏLAVƏ EDİN
    // ... digər Ignore-lar
});
```

**2. Entity Konfiqurasiyası - TenantCustomerSequence:**
```csharp
// ==== TENANT CUSTOMER SEQUENCE ====
modelBuilder.Entity<TenantCustomerSequence>(entity =>
{
    entity.ToTable("TenantCustomerSequences");
    entity.HasKey(tcs => tcs.TenantId);
    entity.Property(tcs => tcs.TenantId).ValueGeneratedNever();
    entity.Property(tcs => tcs.LastNumber).IsRequired();
    entity.Property(tcs => tcs.Prefix).IsRequired().HasMaxLength(5).HasDefaultValue("789"); // ✅ BU SƏTRİ ƏLAVƏ EDİN
    entity.Property(tcs => tcs.UpdatedAt).IsRequired(); // ✅ BU SƏTRİ ƏLAVƏ EDİN
    // ... digər Ignore-lar
});
```

**3. SaveChangesAsync - Generation Məntiqini Silin:**
```csharp
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    // Process EntityBase timestamps first
    var entries = ChangeTracker.Entries<EntityBase>();
    foreach (var entry in entries)
    {
        if (entry.State == EntityState.Added)
        {
            entry.Entity.RegDate = DateTime.UtcNow;
        }
        if (entry.State == EntityState.Modified)
        {
            entry.Entity.EditDate = DateTime.UtcNow;
        }
    }

    // ❌ BUNLARI SİLİN:
    // - GenerateWalveroIdAsync çağırışları
    // - GenerateCardNumberAsync çağırışları
    // - ExtractCountryCode çağırışları

    // ✅ BUNU ƏLAVƏ EDİN:
    // NOTE: WalveroId and CardNumber generation has been moved to services (ICardNumberGenerator, IWalveroIdGenerator)
    // They are generated before SaveChanges is called to avoid N+1 queries and improve performance

    // Generate CardNumber and BarcodeValue for new LoyaltyCards
    // (Bu hissəni saxlayın - LoyaltyCard üçün lazımdır)
    var newLoyaltyCards = ChangeTracker.Entries<LoyaltyCard>()
        .Where(e => e.State == EntityState.Added)
        .Select(e => e.Entity)
        .ToList();

    // ... LoyaltyCard məntiqini saxlayın ...

    return await base.SaveChangesAsync(cancellationToken);
}
```

**4. SaveChanges (Synchronous) - Eyni Dəyişikliklər:**
```csharp
public override int SaveChanges()
{
    // Eyni dəyişikliklər - generation məntiqini silin
    // ...
}
```

**5. Köhnə Metodları Silin:**
```csharp
// ❌ BUNLARI SİLİN:
// - private long GenerateWalveroIdSync(int countryCode)
// - private string GenerateCardNumberSync(int tenantId)
// - private async Task<long> GenerateWalveroIdAsync(int countryCode, CancellationToken cancellationToken)
// - private async Task<string> GenerateCardNumberAsync(int tenantId, CancellationToken cancellationToken)
// - private int? ExtractCountryCode(string phoneNumber)

// ✅ ExtractNumericPartFromBarcode metodunu saxlayın (LoyaltyCard üçün lazımdır)
```

---

### ✅ 1.4. ServiceRegistration Dəyişiklikləri

#### `ServiceRegistration.cs`
**Yoxlayın:** `ISequenceRepository` register edilibmi?
```csharp
public static void AddDataAccessServices(this IServiceCollection services)
{
    // ... digər servislər ...
    services.AddScoped<ISequenceRepository, SequenceRepository>(); // ✅ BU SƏTRİ ƏLAVƏ EDİN
}
```

---

### ✅ 1.5. Project File Dəyişiklikləri

#### `Loyalty.DataAccess.csproj`
**Yoxlayın:** Aşağıdakı paketlər varmı?
```xml
<ItemGroup>
  <!-- ... digər paketlər ... -->
  <PackageReference Include="Dapper" Version="2.1.35" /> <!-- ✅ BU PAKETİ ƏLAVƏ EDİN -->
  <PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.2" /> <!-- ✅ BU PAKETİ ƏLAVƏ EDİN -->
</ItemGroup>
```

---

### ✅ 1.6. Migration Skripti

#### `Migrations/20251219140448_optimize_sequence_generation.sql`
**Yoxlayın:** Bu migration skripti varmı? Əgər yoxdursa, yaradın və SQL Server-də çalışdırın.

**Faylın tam kodunu:** `Loyalty.DataAccess/Migrations/20251219140448_optimize_sequence_generation.sql` faylından kopyalayın.

---

## 📁 2. LOYALTY.BUSINESS LAYİHƏSİ

### ✅ 2.1. Yeni Fayllar

#### `Services/Abstractions/Sequences/ICardNumberGenerator.cs`
**Yoxlayın:** Bu fayl mövcuddurmu? Əgər yoxdursa, yaradın:
```csharp
namespace Loyalty.Business.Services.Abstractions.Sequences
{
    /// <summary>
    /// Service for generating unique CardNumbers for customers
    /// Format: {prefix}{tenantId:D2}{prefix}{seq:D4}
    /// Example: 789017890001, 789017890002, ...
    /// </summary>
    public interface ICardNumberGenerator
    {
        /// <summary>
        /// Generates a unique CardNumber for a tenant
        /// </summary>
        /// <param name="tenantId">Tenant ID</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Generated CardNumber</returns>
        Task<string> GenerateCardNumberAsync(int tenantId, CancellationToken ct = default);
    }
}
```

#### `Services/Abstractions/Sequences/IWalveroIdGenerator.cs`
**Yoxlayın:** Bu fayl mövcuddurmu? Əgər yoxdursa, yaradın:
```csharp
namespace Loyalty.Business.Services.Abstractions.Sequences
{
    /// <summary>
    /// Service for generating unique WalveroId for users
    /// Format: {countryCode}1{seq:D5}
    /// Example: 994100001, 994100002, 336100001, ...
    /// </summary>
    public interface IWalveroIdGenerator
    {
        /// <summary>
        /// Generates a unique WalveroId for a country code
        /// </summary>
        /// <param name="countryCode">Country code (e.g., 994, 336)</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Generated WalveroId</returns>
        Task<long> GenerateWalveroIdAsync(int countryCode, CancellationToken ct = default);

        /// <summary>
        /// Extracts country code from phone number
        /// Supports formats: +994, 994, 0 (with country code)
        /// </summary>
        /// <param name="phoneNumber">Phone number</param>
        /// <returns>Country code or null if not found</returns>
        int? ExtractCountryCode(string? phoneNumber);
    }
}
```

#### `Services/Implementations/Sequences/CardNumberGenerator.cs`
**Yoxlayın:** Bu fayl mövcuddurmu? Əgər yoxdursa, yaradın:

**Tam kod:**
```csharp
using Loyalty.Business.Services.Abstractions.Sequences;
using Loyalty.DataAccess.Repositories.Abstractions.Sequences;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Sequences
{
    /// <summary>
    /// Service for generating unique CardNumbers using atomic sequence generation
    /// Format: {prefix}{tenantId:D2}{prefix}{seq:D4}
    /// </summary>
    public class CardNumberGenerator : ICardNumberGenerator
    {
        private readonly ISequenceRepository _sequenceRepository;
        private readonly ILogger<CardNumberGenerator>? _logger;

        public CardNumberGenerator(
            ISequenceRepository sequenceRepository,
            ILogger<CardNumberGenerator>? logger = null)
        {
            _sequenceRepository = sequenceRepository ?? throw new ArgumentNullException(nameof(sequenceRepository));
            _logger = logger;
        }

        public async Task<string> GenerateCardNumberAsync(int tenantId, CancellationToken ct = default)
        {
            if (tenantId <= 0)
                throw new ArgumentException("TenantId must be greater than 0", nameof(tenantId));

            try
            {
                // Get next sequence atomically
                var (lastNumber, prefix) = await _sequenceRepository.GetNextTenantSequenceAsync(tenantId, ct);

                // Format: {prefix}{tenantId:D2}{prefix}{seq:D4}
                // Example: 789017890001, 789017890002, ...
                var cardNumber = $"{prefix}{tenantId:D2}{prefix}{lastNumber:D4}";

                _logger?.LogDebug("Generated CardNumber for TenantId {TenantId}: {CardNumber}",
                    tenantId, cardNumber);

                return cardNumber;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error generating CardNumber for TenantId {TenantId}", tenantId);
                throw;
            }
        }
    }
}
```

#### `Services/Implementations/Sequences/WalveroIdGenerator.cs`
**Yoxlayın:** Bu fayl mövcuddurmu? Əgər yoxdursa, yaradın:

**Tam kod:**
```csharp
using Loyalty.Business.Services.Abstractions.Sequences;
using Loyalty.DataAccess.Repositories.Abstractions.Sequences;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Loyalty.Business.Services.Implementations.Sequences
{
    /// <summary>
    /// Service for generating unique WalveroId using atomic sequence generation
    /// Format: {countryCode}1{seq:D5}
    /// </summary>
    public class WalveroIdGenerator : IWalveroIdGenerator
    {
        private readonly ISequenceRepository _sequenceRepository;
        private readonly ILogger<WalveroIdGenerator>? _logger;

        public WalveroIdGenerator(
            ISequenceRepository sequenceRepository,
            ILogger<WalveroIdGenerator>? logger = null)
        {
            _sequenceRepository = sequenceRepository ?? throw new ArgumentNullException(nameof(sequenceRepository));
            _logger = logger;
        }

        public async Task<long> GenerateWalveroIdAsync(int countryCode, CancellationToken ct = default)
        {
            if (countryCode <= 0)
                throw new ArgumentException("CountryCode must be greater than 0", nameof(countryCode));

            try
            {
                // Get next sequence atomically
                var lastNumber = await _sequenceRepository.GetNextCountrySequenceAsync(countryCode, ct);

                // Format: {countryCode}1{seq:D5}
                // Example: 994100001, 994100002, 336100001, ...
                var walveroId = long.Parse($"{countryCode}1{lastNumber:D5}");

                _logger?.LogDebug("Generated WalveroId for CountryCode {CountryCode}: {WalveroId}",
                    countryCode, walveroId);

                return walveroId;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error generating WalveroId for CountryCode {CountryCode}", countryCode);
                throw;
            }
        }

        public int? ExtractCountryCode(string? phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return null;

            // Remove all non-digit characters except +
            var cleaned = phoneNumber.Trim();

            // Patterns:
            // +994501234567 -> 994
            // 994501234567 -> 994
            // 0501234567 (Azerbaijan) -> 994
            // 336123456789 (France) -> 336

            // Try +countryCode pattern first
            if (cleaned.StartsWith("+"))
            {
                // Extract first 1-3 digits after +
                var match = Regex.Match(cleaned.Substring(1), @"^(\d{1,3})");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var code))
                {
                    return code;
                }
            }

            // Try countryCode pattern (2-3 digits at start)
            var countryMatch = Regex.Match(cleaned, @"^(\d{2,3})");
            if (countryMatch.Success && int.TryParse(countryMatch.Groups[1].Value, out var countryCode))
            {
                // Common country codes
                if (countryCode >= 1 && countryCode <= 999)
                {
                    // Special case: Azerbaijan local numbers starting with 0
                    if (cleaned.StartsWith("0") && cleaned.Length >= 9)
                    {
                        return 994; // Azerbaijan country code
                    }

                    return countryCode;
                }
            }

            _logger?.LogWarning("Could not extract country code from phone number: {Phone}", 
                phoneNumber?.Substring(0, Math.Min(10, phoneNumber.Length)));

            return null;
        }
    }
}
```

#### `Services/Implementations/Sequences/SequenceGenerationHelper.cs` (Opsional)
**Yoxlayın:** Bu helper fayl yaradılıb, amma istifadə olunmayıb. İstəsəniz əlavə edə bilərsiniz.

---

### ✅ 2.2. ServiceRegistration Dəyişiklikləri

#### `Services/ServiceRegistration.cs`
**Yoxlayın:** Aşağıdakı servislər register edilibmi?
```csharp
public static IServiceCollection AddBusinessServices(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // ... digər servislər ...
    
    // ✅ BU SƏTRLƏRİ ƏLAVƏ EDİN:
    services.AddScoped<ICardNumberGenerator, CardNumberGenerator>();
    services.AddScoped<IWalveroIdGenerator, WalveroIdGenerator>();
    
    return services;
}
```

---

### ✅ 2.3. CustomerEnrollmentService Dəyişiklikləri

#### `Services/Implementations/Main/CustomerEnrollmentService.cs`

**1. Using əlavə edin:**
```csharp
using Loyalty.Business.Services.Abstractions.Sequences; // ✅ BU USING-İ ƏLAVƏ EDİN
```

**2. Constructor-a inject edin:**
```csharp
private readonly ICardNumberGenerator _cardNumberGenerator; // ✅ BU FIELD-İ ƏLAVƏ EDİN

public CustomerEnrollmentService(
    // ... digər parametrlər ...
    ICardNumberGenerator cardNumberGenerator) // ✅ BU PARAMETRİ ƏLAVƏ EDİN
{
    // ... digər assignment-lar ...
    _cardNumberGenerator = cardNumberGenerator; // ✅ BU SƏTRİ ƏLAVƏ EDİN
}
```

**3. Customer yaradarkən CardNumber generate edin:**
```csharp
if (customer is null)
{
    // ✅ BU HİSSƏNİ ƏLAVƏ EDİN - CardNumber generate et
    var cardNumber = await _cardNumberGenerator.GenerateCardNumberAsync(tenantId, ct);

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
        applicationUserId = int.Parse(userId),
        CardNumber = cardNumber // ✅ BU SƏTRİ ƏLAVƏ EDİN
    };

    await _uow.Repository<Customer>().AddAsync(customer);
    
    // ✅ BU RETRY MƏNTİQİNİ ƏLAVƏ EDİN:
    try
    {
        _uow.Commit();
    }
    catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
    {
        // Regenerate and retry once
        customer.CardNumber = await _cardNumberGenerator.GenerateCardNumberAsync(tenantId, ct);
        _uow.Commit();
    }
}
```

---

### ✅ 2.4. UserService Dəyişiklikləri

#### `Services/Implementations/Users/UserService.cs`

**1. Using əlavə edin:**
```csharp
using Loyalty.Business.Services.Abstractions.Sequences; // ✅ BU USING-İ ƏLAVƏ EDİN
```

**2. Constructor-a inject edin:**
```csharp
private readonly IWalveroIdGenerator _walveroIdGenerator; // ✅ BU FIELD-İ ƏLAVƏ EDİN

public UserService(
    // ... digər parametrlər ...
    IWalveroIdGenerator walveroIdGenerator = null) // ✅ BU PARAMETRİ ƏLAVƏ EDİN
{
    // ... digər assignment-lar ...
    _walveroIdGenerator = walveroIdGenerator; // ✅ BU SƏTRİ ƏLAVƏ EDİN
}
```

**3. RegisterUserWithPendingStatusAsync metodunda:**
```csharp
public async Task<ServiceResult> RegisterUserWithPendingStatusAsync(
    RegisterWithPhoneDto registerDto, 
    CancellationToken ct = default) // ✅ CancellationToken parametrini əlavə edin
{
    // ... mövcud kod ...

    // Create new user in pending state
    var user = new ApplicationUser
    {
        UserName = registerDto.PhoneNumber,
        PhoneNumber = registerDto.PhoneNumber,
        Email = registerDto.Email,
        Name = registerDto.Name ?? string.Empty,
        Surname = registerDto.Surname ?? string.Empty,
        IsPhoneVerified = false,
        Status = UserStatus.PendingOtpConfirmation,
        EmailConfirmed = false,
        PhoneNumberConfirmed = false,
        Position = "Customer"
    };

    // ✅ BU HİSSƏNİ ƏLAVƏ EDİN - WalveroId generate et
    if (_walveroIdGenerator != null && !string.IsNullOrEmpty(user.PhoneNumber))
    {
        var countryCode = _walveroIdGenerator.ExtractCountryCode(user.PhoneNumber);
        if (countryCode.HasValue)
        {
            try
            {
                user.WalveroId = await _walveroIdGenerator.GenerateWalveroIdAsync(countryCode.Value, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to generate WalveroId for user, will be generated in SaveChanges");
            }
        }
    }

    var result = await _userManager.CreateAsync(user, registerDto.Password);
    
    // ✅ BU RETRY MƏNTİQİNİ ƏLAVƏ EDİN:
    if (!result.Succeeded && _walveroIdGenerator != null && !string.IsNullOrEmpty(user.PhoneNumber))
    {
        var errors = result.Errors.ToList();
        var hasUniqueError = errors.Any(e => e.Code.Contains("Duplicate") || e.Description.Contains("unique"));
        
        if (hasUniqueError)
        {
            var countryCode = _walveroIdGenerator.ExtractCountryCode(user.PhoneNumber);
            if (countryCode.HasValue)
            {
                user.WalveroId = await _walveroIdGenerator.GenerateWalveroIdAsync(countryCode.Value, ct);
                result = await _userManager.CreateAsync(user, registerDto.Password);
            }
        }
    }

    // ... qalan kod ...
}
```

**4. RegisterAsync metodunda (əgər varsa):**
Eyni məntiq - WalveroId generate et və retry məntiqini əlavə et.

---

### ✅ 2.5. Interface Dəyişiklikləri

#### `Services/Abstractions/Users/IUserService.cs`
**Yoxlayın:** `RegisterUserWithPendingStatusAsync` metodunda `CancellationToken` parametri varmı?
```csharp
// ❌ Köhnə:
Task<ServiceResult> RegisterUserWithPendingStatusAsync(RegisterWithPhoneDto registerDto);

// ✅ Yeni:
Task<ServiceResult> RegisterUserWithPendingStatusAsync(RegisterWithPhoneDto registerDto, CancellationToken ct = default);
```

---

## 📋 3. YOXLAMA SİYAHISI

Digər layihədə dəyişiklikləri etdikdən sonra:

- [ ] `Loyalty.DataAccess.csproj`-də Dapper və SqlClient paketləri var
- [ ] `ISequenceRepository` və `SequenceRepository` mövcuddur
- [ ] `TenantCustomerSequence` entity-də `Prefix` və `UpdatedAt` property-ləri var
- [ ] `CountrySequence` entity-də `UpdatedAt` property-si var
- [ ] `LoyaltyWalletDbContext`-də entity konfiqurasiyaları update edilib
- [ ] `SaveChanges` və `SaveChangesAsync`-dən generation məntiqini silinib
- [ ] Köhnə generation metodları silinib
- [ ] `ServiceRegistration`-da `ISequenceRepository` register edilib
- [ ] `ICardNumberGenerator` və `CardNumberGenerator` mövcuddur
- [ ] `IWalveroIdGenerator` və `WalveroIdGenerator` mövcuddur
- [ ] `ServiceRegistration`-da generator servisləri register edilib
- [ ] `CustomerEnrollmentService`-də `ICardNumberGenerator` inject edilib və istifadə olunur
- [ ] `UserService`-də `IWalveroIdGenerator` inject edilib və istifadə olunur
- [ ] `IUserService` interface-də metod signature update edilib
- [ ] Migration skripti yaradılıb və SQL Server-də çalışdırılıb

---

## 🚀 4. MIGRATION SKRİPTİNİ ÇALIŞDIRMA

1. Migration skriptini digər layihəyə kopyalayın
2. SQL Server Management Studio-da çalışdırın
3. Yoxlayın ki, column-lar və index-lər yaradılıb

---

## ⚠️ 5. ƏHƏMİYYƏTLİ QEYDLƏR

1. **Köhnə metodları silməyi unutmayın** - `GenerateCardNumberAsync`, `GenerateWalveroIdAsync`, `ExtractCountryCode`
2. **Retry məntiqini əlavə edin** - Unique constraint violation üçün
3. **Migration skriptini çalışdırın** - Database schema dəyişiklikləri üçün
4. **Test edin** - Yeni customer/user yaradarkən CardNumber/WalveroId generate olunmalıdır

---

## 📞 6. PROBLEM HƏLLƏRİ

Əgər problem yaşayırsınızsa:
1. Build error-ları yoxlayın - paketlər və reference-lər düzgündürmü?
2. Migration skriptini yoxlayın - çalışdırılıbmı?
3. Dependency injection-ı yoxlayın - servislər register edilibmi?
4. Log-ları yoxlayın - generation zamanı error varmı?

