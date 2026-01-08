# WalveroId ilə Redeem Lookup - Implementation Prompt

## Məqsəd
Redeem controller və service-də Code parametrinin həm WalveroId (long rəqəm), həm də Barcode (string) kimi işlənməsini təmin etmək. Əgər Code WalveroId-dirsə, həmin WalveroId ilə tenant ID-yə görə customer tapılsın, balansı artırılsın və customer məlumatları response-da göstərilsin.

---

## 1. DataAccess Layer - Repository Dəyişiklikləri

### 1.1. ICustomerRepository Interface-ə Metod Əlavə Et

**Fayl:** `Loyalty.DataAccess/Repositories/Abstractions/Main/ICustomerRepository.cs`

```csharp
public interface ICustomerRepository : IGenericRepository<Customer>
{
    Task<Customer?> GetByEmailAsync(int tenantId, string email, CancellationToken ct = default);
    Task<Customer?> GetByPhoneAsync(int tenantId, string phone, CancellationToken ct = default);
    Task<Customer?> GetByWalveroIdAsync(int tenantId, long walveroId, CancellationToken ct = default); // YENİ
    Task<List<Customer>> SearchAsync(int tenantId, string? q, int skip, int take, CancellationToken ct = default);
    Task<List<Customer>> GetByTenant(int tenantId, int? userId);
    Task<List<Customer>> GetByUserId(int userId);
}
```

### 1.2. CustomerRepository-də Metod Implement Et

**Fayl:** `Loyalty.DataAccess/Repositories/Implementations/Main/CustomerRepository.cs`

```csharp
public async Task<Customer?> GetByWalveroIdAsync(int tenantId, long walveroId, CancellationToken ct = default)
{
    return await GetAsQueryable()
        .Include(x => x.LoyaltyCards)
        .Include(x => x.applicationUser)
        .Where(x => x.TenantId == tenantId && x.IsActive && x.applicationUser != null && x.applicationUser.WalveroId == walveroId)
        .FirstOrDefaultAsync(ct);
}
```

**Qeyd:** `applicationUser` entity-sində `WalveroId` property-si olmalıdır (long? tipində).

---

## 2. Business Layer - DTO Dəyişiklikləri

### 2.1. RedeemResultDto-ya Customer Məlumatları Əlavə Et

**Fayl:** `Loyalty.Business/Dtos/Get/Main/RedeemResultDto.cs`

```csharp
public sealed class RedeemResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public bool? RequiresOtp { get; set; }

    public int? LoyaltyCardId { get; set; }
    public int? AppliedDelta { get; set; }
    public int? OldPoints { get; set; }
    public int? NewPoints { get; set; }
    public bool? IsPending { get; set; }
    public int? RedeemRequestId { get; set; }
    
    // Customer information for display - YENİ
    public GetCustomerDto? Customer { get; set; }
}
```

---

## 3. Business Layer - Service Dəyişiklikləri

### 3.1. RedeemService Constructor-a ICustomerRepository Əlavə Et

**Fayl:** `Loyalty.Business/Services/Implementations/Main/RedeemService.cs`

**Constructor-da:**
```csharp
private readonly ICustomerRepository _customerRepo;

public RedeemService(
    ILoyaltyCardRepository cardSvc,
    ILoyaltyProgramRepository programSvc,
    ITenantBrandingRepository brandingSvc,
    IGoogleWalletServiceFactory gwFactory,
    IWalletRouter walletRouter,
    ITenantRepository tenantPolicySvc,
    IOtpGenerator otpGenerator,
    IUnitOfWork uow,
    ILogger<RedeemService> logger,
    IHubContext<LoyaltyHub> hubContext,
    INotificationService notificationService,
    ICustomerRepository customerRepo) // YENİ
{
    _cardSvc = cardSvc;
    _programSvc = programSvc;
    _brandingSvc = brandingSvc;
    _gwFactory = gwFactory;
    _customerRepo = customerRepo; // YENİ
    // ... digər field-lər
}
```

### 3.2. RedeemService.RedeemAsync Metodunu Dəyişdir

**Fayl:** `Loyalty.Business/Services/Implementations/Main/RedeemService.cs`

**RedeemAsync metodunun başlanğıcında (kart tapmaq hissəsində):**

**ƏVVƏL (köhnə kod):**
```csharp
// 1) Kart
var card = await _cardSvc.GetByBarcodeValueAsync(request.TenantId, request.Code.Trim(), ct);
if (card is null)
{
    _logger.LogWarning("Card not found by barcode. Code={Code}", request.Code);
    return Fail("Kart tapılmadı (barcode uyğun gəlmədi).");
}
```

**SONRA (yeni kod):**
```csharp
// 1) Kart - Check if Code is WalveroId or Barcode
LoyaltyCard? card = null;
var code = request.Code.Trim();

if (long.TryParse(code, out long walveroId))
{
    // Code is WalveroId - find customer by WalveroId, then get their card
    _logger.LogInformation("Code detected as WalveroId: {WalveroId}", walveroId);
    
    var customer = await _customerRepo.GetByWalveroIdAsync(request.TenantId, walveroId, ct);
    if (customer == null)
    {
        _logger.LogWarning("Customer not found by WalveroId: {WalveroId} for TenantId: {TenantId}", walveroId, request.TenantId);
        return Fail($"WalveroId {walveroId} ilə customer tapılmadı.");
    }

    // Find the active card for this customer and program
    if (request.LoyaltyProgramId.HasValue)
    {
        card = await _cardSvc.GetLastActiveByCustomerAsync(
            request.TenantId, 
            request.LoyaltyProgramId.Value, 
            customer.Id, 
            ct);
    }
    else
    {
        // If no program specified, get the most recent active card
        var cards = await _cardSvc.ListByCustomerAsync(request.TenantId, customer.Id, ct);
        card = cards?.Where(c => c.Status == CardStatus.Active)
            .OrderByDescending(c => c.Id)
            .FirstOrDefault();
    }

    if (card == null)
    {
        _logger.LogWarning("Active card not found for CustomerId: {CustomerId}, ProgramId: {ProgramId}", 
            customer.Id, request.LoyaltyProgramId);
        return Fail("Aktiv kart tapılmadı.");
    }
}
else
{
    // Code is Barcode - find card by barcode
    card = await _cardSvc.GetByBarcodeValueAsync(request.TenantId, code, ct);
    if (card is null)
    {
        _logger.LogWarning("Card not found by barcode. Code={Code}", code);
        return Fail("Kart tapılmadı (barcode uyğun gəlmədi).");
    }
}

var appUserName = card.Customer?.applicationUser?.Id;
```

**Qeyd:** `CardStatus` enum-u və `ILoyaltyCardRepository.GetLastActiveByCustomerAsync` metodunun mövcud olduğunu yoxlayın.

---

## 4. WebAPI Layer - Controller Dəyişiklikləri

### 4.1. RedeemController Constructor-a ICustomerRepository Əlavə Et

**Fayl:** `Loyalty.WebAPI/Controllers/RedeemController.cs`

**Using directive-lərə əlavə et:**
```csharp
using Loyalty.DataAccess.Repositories.Abstractions.Main;
```

**Field və Constructor-da:**
```csharp
private readonly ICustomerRepository _customerRepository;

public RedeemController(
    ITenantProvider tenantProvider,
    IRedeemService redeemService,
    ILoyaltyProgramService programSvc,
    ICustomerEnrollmentService customerSvc,
    ILoyaltyCardService cardSvc,
    ILogger<RedeemController> logger,
    IPasskitPushService passkitPushService,
    ICustomerRepository customerRepository) // YENİ
{
    _tenantProvider = tenantProvider;
    _redeemService = redeemService;
    _programSvc = programSvc;
    _customerSvc = customerSvc;
    _cardSvc = cardSvc;
    _logger = logger;
    _passkitPushService = passkitPushService;
    _customerRepository = customerRepository; // YENİ
}
```

### 4.2. RedeemController.Start Metodunu Dəyişdir

**Fayl:** `Loyalty.WebAPI/Controllers/RedeemController.cs`

**Start metodunun başlanğıcında:**

**ƏVVƏL (köhnə kod):**
```csharp
var cardModel = await _cardSvc.GetByBarcodeValueAsync(tenantId.Value, (model.Code ?? "").Trim());
var card = cardModel.Data as GetLoyaltyCardDto;

var customerModel = await _customerSvc.GetByIdName(card.CustomerId, "N/A");
var customer = customerModel.Data as GetCustomerDto;
```

**SONRA (yeni kod):**
```csharp
var code = (model.Code ?? "").Trim();
GetLoyaltyCardDto? card = null;
GetCustomerDto? customer = null;

// Check if Code is a WalveroId (long number)
if (long.TryParse(code, out long walveroId))
{
    _logger.LogInformation("Code detected as WalveroId: {WalveroId}", walveroId);
    
    // Find customer by WalveroId
    var customerEntity = await _customerRepository.GetByWalveroIdAsync(tenantId.Value, walveroId, ct);
    
    if (customerEntity == null)
    {
        _logger.LogWarning("Customer not found by WalveroId: {WalveroId} for TenantId: {TenantId}", walveroId, tenantId.Value);
        return BadRequest(new { success = false, message = $"WalveroId {walveroId} ilə customer tapılmadı." });
    }

    // Get customer DTO
    var customerModel = await _customerSvc.GetByIdName(customerEntity.Id, "N/A");
    customer = customerModel.Data as GetCustomerDto;

    if (customer == null)
    {
        _logger.LogWarning("Customer DTO not found for CustomerId: {CustomerId}", customerEntity.Id);
        return BadRequest(new { success = false, message = "Customer məlumatları tapılmadı." });
    }

    // Find the active card for this customer and program
    if (programId.HasValue && customer.LoyaltyCards != null && customer.LoyaltyCards.Any())
    {
        card = customer.LoyaltyCards
            .Where(c => c.LoyaltyProgramId == programId.Value)
            .OrderByDescending(c => c.Id)
            .FirstOrDefault();
    }

    if (card == null)
    {
        _logger.LogWarning("Active card not found for CustomerId: {CustomerId}, ProgramId: {ProgramId}", customer.Id, programId);
        return BadRequest(new { success = false, message = "Aktiv kart tapılmadı." });
    }
}
else
{
    // Original logic: find card by barcode
    var cardModel = await _cardSvc.GetByBarcodeValueAsync(tenantId.Value, code);
    card = cardModel.Data as GetLoyaltyCardDto;

    if (card == null)
    {
        _logger.LogWarning("Card not found by barcode: {Code}", code);
        return BadRequest(new { success = false, message = "Kart tapılmadı." });
    }

    var customerModel = await _customerSvc.GetByIdName(card.CustomerId, "N/A");
    customer = customerModel.Data as GetCustomerDto;
}
```

### 4.3. RedeemController.Start Metodunun Sonunda Customer Məlumatlarını Response-a Əlavə Et

**Start metodunun sonunda (return-dən əvvəl):**
```csharp
var res = await _redeemService.RedeemAsync(dto, ct);

// Add customer information to response
if (res.Success && customer != null)
{
    res.Customer = customer;
}

return Ok(res);
```

---

## 5. Dependency Injection - Service Registration

**Fayl:** `Loyalty.DataAccess/ServiceRegistration.cs` (və ya uyğun registration faylı)

`ICustomerRepository` artıq register edilibsə, heç nə etməyə ehtiyac yoxdur. Əgər yoxdursa:

```csharp
services.AddScoped<ICustomerRepository, CustomerRepository>();
```

---

## 6. Test Senaryoları

### 6.1. WalveroId ilə Test
```http
POST /api/Redeem/Start
Content-Type: application/json

{
  "Code": "994100001",
  "Delta": 10,
  "OrderId": "ORDER123",
  "mode": "auto",
  "paymentMethod": "card"
}
```

**Gözlənilən nəticə:**
- Customer WalveroId ilə tapılır
- Customer-in aktiv kartı tapılır
- Balans artırılır
- Response-da `Customer` məlumatları göstərilir

### 6.2. Barcode ilə Test (köhnə funksionallıq)
```http
POST /api/Redeem/Start
Content-Type: application/json

{
  "Code": "78917890001",
  "Delta": 10,
  "OrderId": "ORDER123",
  "mode": "auto",
  "paymentMethod": "card"
}
```

**Gözlənilən nəticə:**
- Kart barcode ilə tapılır
- Köhnə məntiq işləyir
- Balans artırılır

---

## 7. Tələb Olunan Entity və Property-lər

### 7.1. ApplicationUser Entity
```csharp
public class ApplicationUser : IdentityUser<int>
{
    // ...
    public long? WalveroId { get; set; } // MÖVCUD OLMALIDIR
    // ...
}
```

### 7.2. Customer Entity
```csharp
public class Customer : EntityBase, IEntity
{
    // ...
    public int? applicationUserId { get; set; }
    public ApplicationUser? applicationUser { get; set; }
    public ICollection<LoyaltyCard> LoyaltyCards { get; set; }
    // ...
}
```

### 7.3. LoyaltyCard Entity
```csharp
public class LoyaltyCard : EntityBase, IEntity
{
    // ...
    public CardStatus Status { get; set; } // MÖVCUD OLMALIDIR
    // ...
}
```

---

## 8. Xəta Halları və Validasiya

1. **WalveroId tapılmadı:**
   - Response: `400 BadRequest` - "WalveroId {walveroId} ilə customer tapılmadı."

2. **Customer-in aktiv kartı yoxdur:**
   - Response: `400 BadRequest` - "Aktiv kart tapılmadı."

3. **Code nə WalveroId, nə də Barcode kimi tanınır:**
   - Barcode kimi işlənir və barcode ilə axtarış edilir.

---

## 9. Qeydlər

- `long.TryParse` istifadə edilir ki, Code-un WalveroId olub olmadığını yoxlasın.
- WalveroId formatı: `994100001`, `994100002` və s. (long rəqəm).
- Barcode formatı: `78917890001` və s. (string, rəqəmlərdən ibarət ola bilər, amma long-a çevrilə bilməz).
- Həm Controller, həm də Service-də lookup məntiqini təmin etmək vacibdir (defensive programming).

---

## 10. Checklist

- [ ] `ICustomerRepository.GetByWalveroIdAsync` metodunu əlavə et
- [ ] `CustomerRepository.GetByWalveroIdAsync` metodunu implement et
- [ ] `RedeemResultDto.Customer` property-sini əlavə et
- [ ] `RedeemService` constructor-a `ICustomerRepository` inject et
- [ ] `RedeemService.RedeemAsync` metodunda WalveroId lookup əlavə et
- [ ] `RedeemController` constructor-a `ICustomerRepository` inject et
- [ ] `RedeemController.Start` metodunda WalveroId lookup əlavə et
- [ ] `RedeemController.Start` metodunda customer məlumatlarını response-a əlavə et
- [ ] Test et: WalveroId ilə redeem
- [ ] Test et: Barcode ilə redeem (köhnə funksionallıq)

---

## 11. Fayl Siyahısı

1. `Loyalty.DataAccess/Repositories/Abstractions/Main/ICustomerRepository.cs`
2. `Loyalty.DataAccess/Repositories/Implementations/Main/CustomerRepository.cs`
3. `Loyalty.Business/Dtos/Get/Main/RedeemResultDto.cs`
4. `Loyalty.Business/Services/Implementations/Main/RedeemService.cs`
5. `Loyalty.WebAPI/Controllers/RedeemController.cs`

---

**Son yeniləmə:** 2025-12-20

