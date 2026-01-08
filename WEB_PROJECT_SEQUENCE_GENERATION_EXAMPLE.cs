// ============================================
// WEB LAYİHƏSİ ÜÇÜN SEQUENCE GENERATION NÜMUNƏSİ
// ============================================

// ============================================
// 1. Program.cs və ya Startup.cs-də Dependency Injection
// ============================================

using Loyalty.DataAccess;
using Loyalty.Business.Services;

var builder = WebApplication.CreateBuilder(args);

// ... digər konfiqurasiyalar ...

// DataAccess servisləri (SequenceRepository daxil)
builder.Services.AddDataAccessServices();

// Business servisləri (CardNumberGenerator, WalveroIdGenerator daxil)
builder.Services.AddBusinessServices(builder.Configuration);

// ... digər servislər ...

var app = builder.Build();

// ============================================
// 2. Customer Service Nümunəsi
// ============================================

using Loyalty.Business.Services.Abstractions.Sequences;
using Microsoft.Data.SqlClient;

public class CustomerService
{
    private readonly ICardNumberGenerator _cardNumberGenerator;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(
        ICardNumberGenerator cardNumberGenerator,
        ApplicationDbContext dbContext,
        ILogger<CustomerService> logger)
    {
        _cardNumberGenerator = cardNumberGenerator;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Customer> CreateCustomerAsync(
        int tenantId,
        string firstName,
        string lastName,
        string phoneNumber,
        string? email = null,
        CancellationToken ct = default)
    {
        // 1. CardNumber generate et (atomik)
        var cardNumber = await _cardNumberGenerator.GenerateCardNumberAsync(tenantId, ct);

        // 2. Customer yarat
        var customer = new Customer
        {
            TenantId = tenantId,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            Email = email,
            CardNumber = cardNumber, // Generate edilmiş CardNumber
            IsActive = true,
            LastActivityDate = DateTime.UtcNow
        };

        // 3. Save et (retry məntiqini əlavə edin)
        try
        {
            await _dbContext.Customers.AddAsync(customer, ct);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Customer created with CardNumber: {CardNumber}", cardNumber);
            return customer;
        }
        catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
        {
            // Unique constraint violation - retry with new CardNumber
            _logger.LogWarning("Duplicate CardNumber detected, regenerating...");

            // Regenerate CardNumber
            customer.CardNumber = await _cardNumberGenerator.GenerateCardNumberAsync(tenantId, ct);

            // Retry save
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Customer created with regenerated CardNumber: {CardNumber}", customer.CardNumber);
            return customer;
        }
    }
}

// ============================================
// 3. User Service Nümunəsi (WalveroId)
// ============================================

using Loyalty.Business.Services.Abstractions.Sequences;
using Microsoft.AspNetCore.Identity;

public class UserRegistrationService
{
    private readonly IWalveroIdGenerator _walveroIdGenerator;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UserRegistrationService> _logger;

    public UserRegistrationService(
        IWalveroIdGenerator walveroIdGenerator,
        UserManager<ApplicationUser> userManager,
        ILogger<UserRegistrationService> logger)
    {
        _walveroIdGenerator = walveroIdGenerator;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IdentityResult> RegisterUserAsync(
        string phoneNumber,
        string email,
        string password,
        string? name = null,
        string? surname = null,
        CancellationToken ct = default)
    {
        // 1. WalveroId generate et (atomik)
        var countryCode = _walveroIdGenerator.ExtractCountryCode(phoneNumber);
        if (!countryCode.HasValue)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "InvalidPhoneNumber",
                Description = "Phone number format is invalid. Cannot extract country code."
            });
        }

        var walveroId = await _walveroIdGenerator.GenerateWalveroIdAsync(countryCode.Value, ct);

        // 2. User yarat
        var user = new ApplicationUser
        {
            UserName = phoneNumber,
            PhoneNumber = phoneNumber,
            Email = email,
            Name = name ?? string.Empty,
            Surname = surname ?? string.Empty,
            WalveroId = walveroId, // Generate edilmiş WalveroId
            IsPhoneVerified = false,
            Status = UserStatus.PendingOtpConfirmation,
            EmailConfirmed = false,
            PhoneNumberConfirmed = false,
            Position = "Customer"
        };

        // 3. Save et (retry məntiqini əlavə edin)
        var result = await _userManager.CreateAsync(user, password);

        // Retry on unique constraint violation
        if (!result.Succeeded)
        {
            var errors = result.Errors.ToList();
            var hasUniqueError = errors.Any(e => 
                e.Code.Contains("Duplicate") || 
                e.Description.Contains("unique") ||
                e.Description.Contains("WalveroId"));

            if (hasUniqueError)
            {
                _logger.LogWarning("Duplicate WalveroId detected, regenerating...");

                // Regenerate WalveroId
                user.WalveroId = await _walveroIdGenerator.GenerateWalveroIdAsync(countryCode.Value, ct);

                // Retry
                result = await _userManager.CreateAsync(user, password);
            }
        }

        if (result.Succeeded)
        {
            _logger.LogInformation("User created with WalveroId: {WalveroId}", walveroId);
        }

        return result;
    }
}

// ============================================
// 4. Controller Nümunəsi
// ============================================

using Microsoft.AspNetCore.Mvc;
using Loyalty.Business.Services.Abstractions.Sequences;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly CustomerService _customerService;

    public CustomersController(CustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateCustomer(
        [FromBody] CreateCustomerRequest request,
        CancellationToken ct)
    {
        try
        {
            var customer = await _customerService.CreateCustomerAsync(
                request.TenantId,
                request.FirstName,
                request.LastName,
                request.PhoneNumber,
                request.Email,
                ct);

            return Ok(new
            {
                Success = true,
                CustomerId = customer.Id,
                CardNumber = customer.CardNumber
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                Success = false,
                Message = ex.Message
            });
        }
    }
}

public class CreateCustomerRequest
{
    public int TenantId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
}

// ============================================
// 5. Migration Skriptini Çalışdırma
// ============================================

/*
SQL Server Management Studio-da:

1. Verilənlər bazasına qoşulun
2. Yeni Query açın
3. Skripti açın: Loyalty.DataAccess/Migrations/20251219140448_optimize_sequence_generation.sql
4. Execute edin (F5)

Və ya command line:
sqlcmd -S "ServerName" -d "DatabaseName" -U "Username" -P "Password" 
      -i "Loyalty.DataAccess\Migrations\20251219140448_optimize_sequence_generation.sql"
*/

// ============================================
// 6. Yoxlama
// ============================================

/*
Migration skriptini çalışdırdıqdan sonra:

1. Column-ları yoxlayın:
   SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE
   FROM INFORMATION_SCHEMA.COLUMNS
   WHERE TABLE_NAME IN ('TenantCustomerSequences', 'CountrySequences')
   AND COLUMN_NAME IN ('Prefix', 'UpdatedAt');

2. Index-ləri yoxlayın:
   SELECT i.name AS IndexName, t.name AS TableName
   FROM sys.indexes i
   INNER JOIN sys.tables t ON i.object_id = t.object_id
   WHERE i.name IN ('IX_Customers_CardNumber', 'IX_AspNetUsers_WalveroId');

3. Test edin:
   - Yeni customer yaradın → CardNumber generate olunmalıdır
   - Yeni user yaradın → WalveroId generate olunmalıdır
   - Concurrent request-ləri test edin → Duplicate error olmamalıdır
*/

