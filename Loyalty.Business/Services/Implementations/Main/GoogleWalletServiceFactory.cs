using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class GoogleWalletServiceFactory : IGoogleWalletServiceFactory
    {
        private readonly IWalletCredentialRepository _walletSvc;
        private readonly ILoyaltyProgramRepository _programSvc;
        private readonly IAppEnvironment _env;
        private readonly HttpClient _httpClient;
        private readonly ILoyaltyCardRepository? _cardRepo;
        private readonly Microsoft.Extensions.Logging.ILoggerFactory? _loggerFactory;
        private readonly ITenantSuspensionGuard? _suspensionGuard;
        private readonly ITimeZoneService? _timeZoneService;
        private readonly IWalletSecretFetcher? _secrets;
        public GoogleWalletServiceFactory(IAppEnvironment env, ILoyaltyProgramRepository programSvc, IWalletCredentialRepository walletSvc, HttpClient httpClient, ILoyaltyCardRepository? cardRepo, Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory = null,
            ITenantSuspensionGuard? suspensionGuard = null, ITimeZoneService? timeZoneService = null, IWalletSecretFetcher? secrets = null)
        {
            _secrets = secrets;
            _env = env;
            _programSvc = programSvc;
            _walletSvc = walletSvc;
            _httpClient = httpClient;
            _cardRepo = cardRepo;
            _loggerFactory = loggerFactory;
            _suspensionGuard = suspensionGuard;
            _timeZoneService = timeZoneService;
        }



        public async Task<IGoogleWalletService> CreateForTenantAsync(
     int tenantId,
     int loyaltyProgramId,
     CancellationToken ct = default)
        {
            // 1) Program tap
            var program = await _programSvc.GetByIdNameAsync(loyaltyProgramId, ct);
            if (program == null)
            {
                throw new InvalidOperationException($"Loyalty program not found: ProgramId={loyaltyProgramId}");
            }

            var classSuffix = string.IsNullOrWhiteSpace(program.Code)
                ? $"lp-{program.Id}"
                : program.Code;

            // 2) Google cred tap - EXCEPTİON ATMADAN log et
            var cred = await _walletSvc.GetActiveAsync(tenantId, WalletPlatform.GooglePay, ct);
            if (cred == null)
            {
                throw new InvalidOperationException($"Google Wallet credentials not found: TenantId={tenantId}");
            }

            // 3) SA JSON path / URL
            var saJsonPathOrUrl = cred.GooglePrivateKeyUrl?.StartsWith("http", StringComparison.OrdinalIgnoreCase) == true
                ? cred.GooglePrivateKeyUrl
                : "https://appapi.walvero.com/" + cred.GooglePrivateKeyUrl;
            if (string.IsNullOrWhiteSpace(saJsonPathOrUrl))
                throw new InvalidOperationException("Service Account JSON path is empty.");

            // 🔹 Nəticədə istifadə edəcəyimiz FİZİKİ fayl yolu
            string serviceAccountFilePath;

            // 3.1. Əgər HTTP/HTTPS URL-dirsə → öz storage-imizdən authenticated oxu, public olmayan temp-ə keşlə
            if (Uri.TryCreate(saJsonPathOrUrl, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                serviceAccountFilePath = _secrets != null
                    ? await _secrets.MaterializeAsync(saJsonPathOrUrl, ".json", ct)
                    : await DownloadToTempLegacyAsync(uri, ct);
            }
            else
            {
                // 3.2. Əgər URL deyil, sadə path-dirsə:
                // Məs: "sa/google/serviceAccount.json" və ya "/sa/google/serviceAccount.json"

                var saPath = saJsonPathOrUrl.TrimStart('/', '\\');

                // Əgər rootlu (C:\ və s.) deyilsə, WebRootPath ilə birləşdir
              

                serviceAccountFilePath = saPath;
            }

            // 4) Runtime config yığ
            var cfg = new GoogleWalletRuntimeConfig
            {
                IssuerId = cred.IssuerId ?? throw new InvalidOperationException("IssuerId is required."),
                ClassSuffix = classSuffix,
                ApplicationName = "Loyalty Wallet",
                ServiceAccountJsonPath = serviceAccountFilePath,   // 👈 ARTIQ HƏM URL, HƏM PATH DƏSTƏKLƏNİR
                Origins = new[] { "https://appapi.walvero.com", "https://walvero.com", "https://localhost:7199" }
            };

            // 5) Servisi yaradıb qaytar
            var gwLogger = _loggerFactory != null
                ? Microsoft.Extensions.Logging.LoggerFactoryExtensions.CreateLogger<GoogleWalletService>(_loggerFactory)
                : null;
            return new GoogleWalletService(cfg, gwLogger, _cardRepo, null, _suspensionGuard, _timeZoneService);
        }

        // Fetcher verilməyən (test) konstruksiyalar üçün köhnə davranış
        private async Task<string> DownloadToTempLegacyAsync(Uri uri, CancellationToken ct)
        {
            var json = await _httpClient.GetStringAsync(uri, ct);
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, json, ct);
            return tempFile;
        }

    }
}
