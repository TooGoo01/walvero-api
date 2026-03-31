using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Entities.Users;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text.Json;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// Service for generating wallet passes for Walvero shadow tenant.
    /// Creates passes based on user's WalveroId, not loyalty cards.
    /// </summary>
    public class WalveroWalletPassService : IWalveroWalletPassService
    {
        private readonly IUnitOfWork _uow;
        private readonly ITenantRepository _tenantRepository;
        private readonly IWalveroWalletPassRepository _passRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWalletCredentialProvider _credProvider;
        private readonly IApplePassBuilder _applePassBuilder;
        private readonly IApplePassSigner _applePassSigner;
        private readonly IGoogleWalletServiceFactory _googleWalletFactory;
        private readonly IFilePathResolver _filePathResolver;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WalveroWalletPassService> _logger;
        private readonly IAppEnvironment _env;

        public WalveroWalletPassService(
            IUnitOfWork uow,
            ITenantRepository tenantRepository,
            IWalveroWalletPassRepository passRepository,
            ICustomerRepository customerRepository,
            UserManager<ApplicationUser> userManager,
            IWalletCredentialProvider credProvider,
            IApplePassBuilder applePassBuilder,
            IApplePassSigner applePassSigner,
            IGoogleWalletServiceFactory googleWalletFactory,
            IFilePathResolver filePathResolver,
            IConfiguration configuration,
            ILogger<WalveroWalletPassService> logger,
            IAppEnvironment env)
        {
            _uow = uow;
            _tenantRepository = tenantRepository;
            _passRepository = passRepository;
            _customerRepository = customerRepository;
            _userManager = userManager;
            _credProvider = credProvider;
            _applePassBuilder = applePassBuilder;
            _applePassSigner = applePassSigner;
            _googleWalletFactory = googleWalletFactory;
            _filePathResolver = filePathResolver;
            _configuration = configuration;
            _logger = logger;
            _env = env;
        }

        public async Task<ServiceResult<WalveroWalletPassResponseDto>> CreateWalveroPassAsync(
            int userId,
            string platform,
            CancellationToken ct = default)
        {
            // 1. Validate platform
            if (platform?.ToLowerInvariant() != "ios" && platform?.ToLowerInvariant() != "android")
            {
                return ServiceResult<WalveroWalletPassResponseDto>.Fail("Platform must be 'ios' or 'android'");
            }

            // 2. Load user
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return ServiceResult<WalveroWalletPassResponseDto>.Fail("User not found");
            }

            // 3. Check WalveroId
            if (user.WalveroId == null || user.WalveroId == 0)
            {
                return ServiceResult<WalveroWalletPassResponseDto>.Fail("User has no walveroId");
            }

            var walveroIdStr = user.WalveroId.Value.ToString();
            var walveroIdMasked = MaskWalveroId(walveroIdStr);

            // 4. Find Walvero shadow tenant
            var walveroTenant = await FindWalveroTenantAsync(ct);
            if (walveroTenant == null)
            {
                _logger.LogError("Walvero shadow tenant not found. System misconfiguration.");
                return ServiceResult<WalveroWalletPassResponseDto>.Fail("Walvero shadow tenant missing");
            }

            // 5. Get or create pass record
            var passRecord = await _passRepository.GetOrCreateAsync(
                userId,
                walveroTenant.Id,
                walveroIdMasked,
                ct);

            // 6. Get program and card template
            var program = walveroTenant.LoyaltyPrograms?.FirstOrDefault(lp => lp.IsActive);
            if (program == null || program.CardTemplate == null)
            {
                return ServiceResult<WalveroWalletPassResponseDto>.Fail("Walvero tenant has no active program or card template");
            }

            var cardTemplate = program.CardTemplate;
            var baseUrl = _configuration["App:BaseUrl"] ?? "https://appapi.walvero.com";

            // 6.5. Get customer name from Customer entity
            var customerName = $"{user.Name} {user.Surname}".Trim();
            var customers = await _customerRepository.GetByUserId(userId);
            var walveroTenantCustomer = customers.FirstOrDefault(c => c.TenantId == walveroTenant.Id);
            if (walveroTenantCustomer != null && !string.IsNullOrWhiteSpace(walveroTenantCustomer.FirstName))
            {
                customerName = $"{walveroTenantCustomer.FirstName} {walveroTenantCustomer.LastName}".Trim();
            }

            // 7. Generate pass based on platform
            if (platform.ToLowerInvariant() == "ios")
            {
                    var appleResult = await GenerateApplePassAsync(
                    passRecord,
                    walveroTenant,
                    program,
                    cardTemplate,
                    walveroIdStr,
                    customerName,
                    baseUrl,
                    ct);

                if (!appleResult.Success)
                {
                    return ServiceResult<WalveroWalletPassResponseDto>.Fail(appleResult.Message);
                }
            }
            else // android
            {
                var googleResult = await GenerateGooglePassAsync(
                    passRecord,
                    walveroTenant,
                    program,
                    cardTemplate,
                    walveroIdStr,
                    customerName,
                    baseUrl,
                    ct);

                if (!googleResult.Success)
                {
                    return ServiceResult<WalveroWalletPassResponseDto>.Fail(googleResult.Message);
                }
            }

            // 8. Reload pass record to get updated URLs
            passRecord = await _passRepository.GetByUserAndTenantAsync(userId, walveroTenant.Id, ct);
            if (passRecord == null)
            {
                return ServiceResult<WalveroWalletPassResponseDto>.Fail("Failed to retrieve pass record");
            }

            // 9. Return response
            var response = new WalveroWalletPassResponseDto
            {
                TenantId = walveroTenant.Id,
                UserId = userId,
                Platform = platform.ToLowerInvariant(),
                WalveroIdMasked = walveroIdMasked,
                ApplePassUrl = passRecord.ApplePassUrl,
                GoogleSaveUrl = passRecord.GoogleSaveUrl,
                PassId = passRecord.Id
            };

            return ServiceResult<WalveroWalletPassResponseDto>.Ok(response);
        }

        private async Task<Tenant?> FindWalveroTenantAsync(CancellationToken ct)
        {
            return await _tenantRepository.FindWalveroTenantAsync(ct);
        }

        private string MaskWalveroId(string walveroId)
        {
            if (string.IsNullOrEmpty(walveroId) || walveroId.Length < 4)
                return "WLV****";

            return $"WLV****{walveroId.Substring(walveroId.Length - 3)}";
        }

        private async Task<ServiceResult> GenerateApplePassAsync(
            WalveroWalletPass passRecord,
            Tenant tenant,
            LoyaltyProgram program,
            CardTemplate cardTemplate,
            string walveroId,
            string customerName,
            string baseUrl,
            CancellationToken ct)
        {
            try
            {
                // Get Apple credentials
                var creds = await _credProvider.GetAppleAsync(tenant.Id, ct);

                // Create snapshot from template
                // Helper function to convert relative paths to absolute URLs
                string ToAbsoluteUrl(string? dbPath)
                {
                    if (string.IsNullOrWhiteSpace(dbPath)) return string.Empty;
                    if (dbPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                        dbPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return dbPath;
                    
                    var normalizedBaseUrl = baseUrl.TrimEnd('/', '\\');
                    var normalizedPath = dbPath.TrimStart('~').TrimStart('/', '\\');
                    normalizedPath = string.IsNullOrWhiteSpace(normalizedPath) ? "/" : "/" + normalizedPath;
                    return normalizedBaseUrl + normalizedPath;
                }

                var snapshot = CardTemplateSnapshotFactory.From(
                    program,
                    cardTemplate,
                    tenant.Branding,
                    ToAbsoluteUrl);

                // Generate serial number if not exists
                var serial = passRecord.AppleSerialNumber ?? $"WLV-{passRecord.Id:D8}";

                // Create pass directory
                var outDir = Path.Combine(_env.WebRootPath, "passes", "walvero", passRecord.Id.ToString());
                Directory.CreateDirectory(outDir);

                var workDir = Path.Combine(Path.GetTempPath(), $"walvero-pkpass-{passRecord.Id}-{Guid.NewGuid():N}");
                Directory.CreateDirectory(workDir);

                try
                {
                    // Build pass using BuildPassRequest (not ApplePassBuildArgs)
                    var buildRequest = new BuildPassRequest
                    {
                        TeamIdentifier = creds.TeamIdentifier,
                        PassTypeIdentifier = creds.PassTypeIdentifier,
                        SerialNumber = serial,
                        OrganizationName = "Walvero",
                        Description = "Walvero ID Card",
                        LogoText = "Walvero",
                        CustomerName = customerName,
                        Barcode = walveroId,
                        BarcodeMessage = walveroId,
                        BarcodeFormat = "PKBarcodeFormatCode128",
                        BackgroundColor = snapshot.BackgroundColor ?? "#FFFFFF",
                        ForegroundColor = snapshot.ForegroundColor ?? "#000000",
                        LabelColor = snapshot.LabelColor ?? "#999999",
                        LogoUrl = snapshot.GoogleLogoUrl,
                        HeroImageUrl = snapshot.HeroImageUrl,
                        StripImageUrl = snapshot.StripImageUrl,
                        WebServiceURL = creds.WebServiceUrl,
                        AuthenticationToken = creds.AuthToken,
                        // NO balance, points, or transactions - empty fields
                        HeaderFields = new List<PassField>(),
                        SecondaryFields = new List<PassField> 
                        { 
                            new PassField("customer", customerName, "Customer") 
                        },
                        AuxiliaryFields = new List<PassField>(),
                        PrimaryFields = new List<PassField>()
                    };

                    await _applePassBuilder.BuildAsync(buildRequest, workDir, ct);

                    // Sign pass
                    var signRequest = new SignPassRequest
                    {
                        WorkingDirectory = workDir,
                        OutputPkPassPath = Path.Combine(outDir, $"{serial}.pkpass"),
                        AppleCertP12Path = creds.CertP12Path,
                        AppleCertPassword = creds.CertPassword,
                        AppleWwdrCertPath = creds.WwdrCertPath
                    };

                    await _applePassSigner.SignAsync(signRequest, ct);

                    // Update pass record with absolute URL
                    var publicUrl = $"https://appapi.walvero.com/passes/walvero/{passRecord.Id}/{serial}.pkpass";
                    await _passRepository.UpdatePassUrlsAsync(
                        passRecord.Id,
                        applePassUrl: publicUrl,
                        googleSaveUrl: null,
                        appleSerialNumber: serial,
                        googleObjectId: null,
                        hasIosPass: true,
                        hasAndroidPass: passRecord.HasAndroidPass,
                        ct);

                    return ServiceResult.Ok();
                }
                finally
                {
                    // Cleanup temp directory
                    if (Directory.Exists(workDir))
                    {
                        try { Directory.Delete(workDir, true); } catch { }
                    }
                }
            }
            catch (DbUpdateException dbEx)
            {
                // Log inner exception for better debugging
                var innerEx = dbEx.InnerException;
                var errorMessage = dbEx.Message;
                
                if (innerEx != null)
                {
                    errorMessage = $"{dbEx.Message} | Inner: {innerEx.Message}";
                    _logger.LogError(innerEx, 
                        "Database error generating Apple pass for WalveroId: {WalveroId}. Error: {Error}", 
                        walveroId, innerEx.Message);
                    
                    // If it's a SQL exception, log the error number
                    if (innerEx is Microsoft.Data.SqlClient.SqlException sqlEx)
                    {
                        _logger.LogError("SQL Error Number: {ErrorNumber}, State: {State}, LineNumber: {LineNumber}", 
                            sqlEx.Number, sqlEx.State, sqlEx.LineNumber);
                        errorMessage = $"{errorMessage} | SQL Error {sqlEx.Number}";
                    }
                }
                else
                {
                    _logger.LogError(dbEx, "Database error generating Apple pass for WalveroId: {WalveroId}", walveroId);
                }
                
                return ServiceResult.Fail($"Failed to generate Apple pass: {errorMessage}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Apple pass for WalveroId: {WalveroId}", walveroId);
                
                // Include inner exception if available
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage = $"{ex.Message} | Inner: {ex.InnerException.Message}";
                    _logger.LogError(ex.InnerException, "Inner exception details");
                }
                
                return ServiceResult.Fail($"Failed to generate Apple pass: {errorMessage}");
            }
        }

        private async Task<ServiceResult> GenerateGooglePassAsync(
            WalveroWalletPass passRecord,
            Tenant tenant,
            LoyaltyProgram program,
            CardTemplate cardTemplate,
            string walveroId,
            string customerName,
            string baseUrl,
            CancellationToken ct)
        {
            try
            {
                // Get Google Wallet service
                var googleService = await _googleWalletFactory.CreateForTenantAsync(tenant.Id, program.Id, ct);

                // Create snapshot
                // Helper function to convert relative paths to absolute URLs
                string ToAbsoluteUrl(string? dbPath)
                {
                    if (string.IsNullOrWhiteSpace(dbPath)) return string.Empty;
                    if (dbPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                        dbPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return dbPath;
                    
                    var normalizedBaseUrl = baseUrl.TrimEnd('/', '\\');
                    var normalizedPath = dbPath.TrimStart('~').TrimStart('/', '\\');
                    normalizedPath = string.IsNullOrWhiteSpace(normalizedPath) ? "/" : "/" + normalizedPath;
                    return normalizedBaseUrl + normalizedPath;
                }

                var snapshot = CardTemplateSnapshotFactory.From(
                    program,
                    cardTemplate,
                    tenant.Branding,
                    ToAbsoluteUrl);

                // Create object ID for Walvero pass
                // Format: {issuerId}.walvero-{passRecordId}
                var objectId = passRecord.GoogleObjectId;
                if (string.IsNullOrEmpty(objectId))
                {
                    // Get issuer ID from Google Wallet service
                    var tempService = await _googleWalletFactory.CreateForTenantAsync(tenant.Id, program.Id, ct);
                    var issuerId = tempService.GetIssuerId();
                    objectId = $"{issuerId}.walvero-{passRecord.Id}";
                }

                // Create or get Walvero object (NO points/balance)
                var createResult = await googleService.CreateOrGetWalveroObjectAsync(
                    objectId: objectId,
                    tpl: snapshot,
                    accountName: customerName,
                    barcodeValue: walveroId,
                    ct);

                if (!createResult.Success)
                {
                    return ServiceResult.Fail($"Failed to create Google Wallet object: {createResult.Message}");
                }

                // Generate save link for Walvero pass
                var saveLinkResult = await googleService.GenerateWalveroSaveLinkAsync(objectId, ct);
                if (!saveLinkResult.Success)
                {
                    return ServiceResult.Fail($"Failed to generate save link: {saveLinkResult.Message}");
                }

                var saveUrl = saveLinkResult.Data?.ToString() ?? saveLinkResult.Message as string;

                // Update pass record with object ID and save URL
                await _passRepository.UpdatePassUrlsAsync(
                    passRecord.Id,
                    applePassUrl: passRecord.ApplePassUrl,
                    googleSaveUrl: saveUrl,
                    appleSerialNumber: passRecord.AppleSerialNumber,
                    googleObjectId: objectId,
                    hasIosPass: passRecord.HasIosPass,
                    hasAndroidPass: true,
                    ct);

                return ServiceResult.Ok();
            }
            catch (DbUpdateException dbEx)
            {
                // Log inner exception for better debugging
                var innerEx = dbEx.InnerException;
                var errorMessage = dbEx.Message;
                
                if (innerEx != null)
                {
                    errorMessage = $"{dbEx.Message} | Inner: {innerEx.Message}";
                    _logger.LogError(innerEx, 
                        "Database error generating Google pass for WalveroId: {WalveroId}. Error: {Error}", 
                        walveroId, innerEx.Message);
                    
                    // If it's a SQL exception, log the error number
                    if (innerEx is Microsoft.Data.SqlClient.SqlException sqlEx)
                    {
                        _logger.LogError("SQL Error Number: {ErrorNumber}, State: {State}, LineNumber: {LineNumber}", 
                            sqlEx.Number, sqlEx.State, sqlEx.LineNumber);
                        errorMessage = $"{errorMessage} | SQL Error {sqlEx.Number}";
                    }
                }
                else
                {
                    _logger.LogError(dbEx, "Database error generating Google pass for WalveroId: {WalveroId}", walveroId);
                }
                
                return ServiceResult.Fail($"Failed to generate Google pass: {errorMessage}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Google pass for WalveroId: {WalveroId}", walveroId);
                
                // Include inner exception if available
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage = $"{ex.Message} | Inner: {ex.InnerException.Message}";
                    _logger.LogError(ex.InnerException, "Inner exception details");
                }
                
                return ServiceResult.Fail($"Failed to generate Google pass: {errorMessage}");
            }
        }
    }
}

