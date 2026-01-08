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
        public GoogleWalletServiceFactory(IAppEnvironment env, ILoyaltyProgramRepository programSvc, IWalletCredentialRepository walletSvc, HttpClient httpClient, ILoyaltyCardRepository? cardRepo)
        {
            _env = env;
            _programSvc = programSvc;
            _walletSvc = walletSvc;
            _httpClient = httpClient;
            _cardRepo = cardRepo;
        }



        public async Task<IGoogleWalletService> CreateForTenantAsync(
     int tenantId,
     int loyaltyProgramId,
     CancellationToken ct = default)
        {
            // 1) Program tap
            var program = await _programSvc.GetByIdNameAsync(loyaltyProgramId, ct)
                          ?? throw new InvalidOperationException("Loyalty program not found.");

            var classSuffix = string.IsNullOrWhiteSpace(program.Code)
                ? $"lp-{program.Id}"
                : program.Code;

            // 2) Google cred tap
            var cred = await _walletSvc.GetActiveAsync(tenantId, WalletPlatform.GooglePay, ct)
                       ?? throw new InvalidOperationException("Google Wallet credentials not found for tenant.");

            // 3) SA JSON path / URL
            var saJsonPathOrUrl = "https://walvero.com/" + cred.GooglePrivateKeyUrl;
            if (string.IsNullOrWhiteSpace(saJsonPathOrUrl))
                throw new InvalidOperationException("Service Account JSON path is empty.");

            // 🔹 Nəticədə istifadə edəcəyimiz FİZİKİ fayl yolu
            string serviceAccountFilePath;

            // 3.1. Əgər HTTP/HTTPS URL-dirsə → linkdən JSON yüklə
            if (Uri.TryCreate(saJsonPathOrUrl, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                // Məs: cred.GooglePrivateKeyUrl = "https://walvero.com/sa/google/serviceAccount.json"
                // Buradan JSON mətnini götürürük
                var json = await _httpClient.GetStringAsync(uri, ct);

                // Müvəqqəti fayl yaradıb ora yazırıq
                var tempFile = Path.GetTempFileName();
                await File.WriteAllTextAsync(tempFile, json, ct);

                serviceAccountFilePath = tempFile;
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
                Origins = new[] { "https://walvero.com", "https://localhost:7199" }
            };

            // 5) Servisi yaradıb qaytar
            return new GoogleWalletService(cfg,null, _cardRepo);
        }

    }
}
