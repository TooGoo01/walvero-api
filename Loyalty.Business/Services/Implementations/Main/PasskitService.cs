using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using File = System.IO.File;

namespace Loyalty.Business.Services.Implementations.Main
{
    // ===========================
    // Service implementation
    // ===========================
    public sealed class PasskitService : IPasskitService
    {
        private readonly IUnitOfWork _uow;
        private readonly IAppleWalletService _apple;
        private readonly IAppEnvironment _env;
        private readonly ILogger<PasskitService> _log;
        private readonly IHttpClientFactory _http;

        public PasskitService(
            IUnitOfWork uow,
            IAppleWalletService apple,
            IAppEnvironment env,
            ILogger<PasskitService> log,
            IHttpClientFactory http)
        {
            _uow = uow;
            _apple = apple;
            _env = env;
            _log = log;
            _http = http;
        }

        public async Task<ServiceResult> RegisterAsync(
            string deviceLibId,
            string passTypeId,
            string serial,
            string authHeader,
            string pushToken,
            string? userAgent,
            CancellationToken ct)
        {
            // 1) Auth
            if (!await AuthorizeAsync(serial, authHeader, ct))
                return new ServiceResult(false, "Unauthorized");

            // 2) Card
            var card = _uow.Repository<LoyaltyCard>().Get(c => c.AppleSerial == serial);
            if (card is null)
                return new ServiceResult(false, "Card not found");

            // 3) Upsert (idempotent)
            var repo = _uow.Repository<PassDeviceRegistration>();
            var existing = repo.Get(r =>
                r.DeviceLibraryIdentifier == deviceLibId &&
                r.PassTypeIdentifier == passTypeId &&
                r.SerialNumber == serial && r.IsActive);

            if (existing is null)
            {
                repo.Add(new PassDeviceRegistration
                {
                    DeviceLibraryIdentifier = deviceLibId,
                    PassTypeIdentifier = passTypeId,
                    SerialNumber = serial,
                    PushToken = pushToken,
                    LoyaltyCardId = card.Id,
                    CustomerId = card.CustomerId,
                    TenantId = card.TenantId,
                    UserAgent = userAgent,
                    LastUpdatedUtc = DateTime.UtcNow
                });
                _uow.Commit();
                // İlk qeydiyyat -> 201 (Controller bu flag-ə baxıb 201 verəcək)
                return new ServiceResult(true, alreadyExists: false);
            }
            else
            {
                existing.PushToken = pushToken;
                existing.LoyaltyCardId = card.Id;
                existing.CustomerId = card.CustomerId;
                existing.TenantId = card.TenantId;
                existing.UserAgent = userAgent;
                existing.LastUpdatedUtc = DateTime.UtcNow;
                repo.Update(existing);
                _uow.Commit();
                // Artıq qeydiyyatlı idi -> 200
                return new ServiceResult(true, alreadyExists: true);
            }
        }

        public sealed class PassMeta
        {
            public bool Exists { get; set; }
            public string? ETag { get; set; }
            public DateTimeOffset LastModified { get; set; }
            public string? Error { get; set; }
        }

        // DTO: ListUpdated üçün cavab modeli
        public sealed class UpdatedListDto
        {
            public string LastUpdatedTag { get; init; } = "";
            public IReadOnlyList<string> Serials { get; init; } = Array.Empty<string>();
        }

        public async Task<ServiceResult> UnregisterAsync(
            string deviceLibId,
            string passTypeId,
            string serial,
            string authHeader,
            CancellationToken ct)
        {
            // 1) DB-dən registrasiyanı tap
            var regRepo = _uow.Repository<PassDeviceRegistration>();
            var reg = regRepo.Get(r =>
                r.DeviceLibraryIdentifier == deviceLibId &&
                r.PassTypeIdentifier == passTypeId &&
                r.SerialNumber == serial);

            // 2) LoyaltyCard-ı tap (ola bilər ki, artıq silinib)
            var cardRepo = _uow.Repository<LoyaltyCard>();
            var card = cardRepo.Get(c => c.AppleSerial == serial);

            // 3) Auth check – amma Apple-un “köhnə serialı silirəm” halında bizi yıxmasın
            var authorized = false;

            if (card != null)
            {
                // kart hələ var → normal auth
                authorized = await AuthorizeAsync(serial, authHeader, ct);
            }
            else if (reg != null)
            {
                // kart artıq yoxdur, amma bu konkret CİHAZ özünü təmizləmək istəyir
                // → bu halda çox da sərt yoxlama, sadəcə header var mı, kifayətdir
                var parts = authHeader?.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var token = parts?.Length == 2 ? parts[1].Trim() : null;
                authorized = !string.IsNullOrEmpty(token);
            }
            else
            {
                // həm kart yoxdur, həm də registration yoxdur → bu, Apple-ın “artıq yoxdur, təmizləyim” halıdır
                // BURADA DA 200 demək lazımdır
                return new ServiceResult(true);
            }

            if (!authorized)
            {
                // Apple-a 401/403 vermək də olar, amma çox vaxt bu da sonra /v1/log gətirir
                return new ServiceResult(false, "Unauthorized");
            }

            // 4) Registrasiyanı deaktiv et (tam silmək əvəzinə)
            if (reg != null)
            {
                reg.IsActive = false;
                reg.EditDate = DateTime.UtcNow;
                regRepo.Update(reg);
                _uow.Commit();
            }

            // 5) Həmişə OK qaytar ki, Apple bu serialı yenidən soruşmasın
            return new ServiceResult(true);
        }

        /// <summary>
        /// Pass faylının local path-ni qaytarır.
        /// Əgər localda tapmırsa, https://appapi.walvero.com + ApplePassUrl
        /// üzərindən HttpClient ilə yükləyib lokala yazır, sonra yenə local path qaytarır.
        /// </summary>
        public async Task<ServiceResult> GetPassPathAsync(
            string passTypeId,
            string serial,
            CancellationToken ct)
        {
            var card = _uow.Repository<LoyaltyCard>()
                           .Get(c => c.AppleSerial == serial && c.IsActive);

            if (card is null)
                return new ServiceResult(false, "Card Not found");

            var localPath = await EnsureLocalPkpassAsync(card, ct);
            if (localPath is null)
                return new ServiceResult(false, "Pass file not found");

            return new ServiceResult(true, data: localPath);
        }

        public async Task<ServiceResult> GetPassMetaAsync(
            string passTypeId,
            string serial,
            CancellationToken ct)
        {
            var card = _uow.Repository<LoyaltyCard>()
                           .Get(c => c.AppleSerial == serial && c.IsActive);

            if (card is null)
            {
                var notFound = new PassMeta { Exists = false, Error = "Card not found" };
                return new ServiceResult(false, data: notFound, message: "Card not found");
            }

            var passPath = await EnsureLocalPkpassAsync(card, ct);
            if (passPath is null || !File.Exists(passPath))
            {
                var notFound = new PassMeta { Exists = false, Error = "Pass file not found" };
                return new ServiceResult(false, data: notFound, message: "Pass file not found");
            }

            // Fayl haqqında məlumatlar
            var fi = new FileInfo(passPath);
            var lastModified = fi.LastWriteTimeUtc;
            var etag = $"W/\"{lastModified.Ticks}-{fi.Length}\"";

            var meta = new PassMeta
            {
                Exists = true,
                ETag = etag,
                LastModified = new DateTimeOffset(lastModified, TimeSpan.Zero)
            };

            return new ServiceResult(true, data: meta);
        }

        public Task<ServiceResult> ListUpdatedAsync(
            string deviceLibId,
            string passTypeId,
            string? sinceTag,
            CancellationToken ct)
        {
            bool sinceParsed = false;

            DateTimeOffset sinceDto = string.IsNullOrWhiteSpace(sinceTag)
                ? DateTimeOffset.UnixEpoch               // 1970-01-01T00:00:00+00:00
                : DateTimeOffset.ParseExact(
                    sinceTag,
                    "yyyy-MM-dd'T'HH:mm:ss.fffffff",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal
                  );

            DateTime sinceUtc = sinceDto.UtcDateTime;
            sinceParsed = true;

            var repo = _uow.Repository<PassDeviceRegistration>();
            var baseQuery = repo.GetAll()
                                .Where(r => r.DeviceLibraryIdentifier == deviceLibId &&
                                            r.PassTypeIdentifier == passTypeId &&
                                            r.IsActive);

            var totalCount = baseQuery.Count();
            _log.LogInformation("Base query count (active regs for device+passType) = {Count}", totalCount);

            var q = baseQuery;
            if (sinceParsed && sinceUtc > DateTime.MinValue)
            {
                // Normalize DB values as UTC before comparison
                q = q.Where(r => DateTime.SpecifyKind(r.LastUpdatedUtc.Value, DateTimeKind.Utc) > sinceUtc);
                _log.LogInformation("Applied time filter: LastUpdatedUtc > {Since:o}", sinceUtc);
            }

            var list = q.Select(r => new { r.SerialNumber, r.LastUpdatedUtc, r.EditDate, r.LoyaltyCardId })
                        .ToList();

            var filteredCount = list.Count;
            _log.LogInformation("Filtered result count = {Count}", filteredCount);

            var nullCount = list.Count(x => x.LastUpdatedUtc == null);
            if (nullCount > 0)
                _log.LogWarning("There are {NullCount} rows with NULL LastUpdatedUtc", nullCount);

            var kinds = list.Where(x => x.LastUpdatedUtc != null)
                            .GroupBy(x => x.LastUpdatedUtc!.Value.Kind)
                            .Select(g => new { Kind = g.Key, Cnt = g.Count() })
                            .ToList();
            foreach (var k in kinds)
                _log.LogInformation("LastUpdatedUtc.Kind={Kind} count={Cnt}", k.Kind, k.Cnt);

            var minUtc = list.Where(x => x.LastUpdatedUtc != null).Select(x => x.LastUpdatedUtc!.Value).DefaultIfEmpty().Min();
            var maxUtc = list.Where(x => x.LastUpdatedUtc != null).Select(x => x.LastUpdatedUtc!.Value).DefaultIfEmpty().Max();
            if (minUtc != default || maxUtc != default)
                _log.LogInformation("Result LastUpdatedUtc range: min={Min:o}, max={Max:o}", minUtc, maxUtc);

            foreach (var row in list.OrderByDescending(x => x.LastUpdatedUtc).Take(5))
                _log.LogInformation("Sample → serial={Serial} lastUpdatedUtc={Utc:o}", row.SerialNumber, row.LastUpdatedUtc);

            var serials = list.Select(x => x.SerialNumber).Distinct().ToList();
            _log.LogInformation("Distinct serials={Cnt}", serials.Count);

            // safer null handling for max
            var lastUpdatedUtc = list.Where(x => x.LastUpdatedUtc.HasValue)
                                     .Select(x => x.LastUpdatedUtc.Value)
                                     .DefaultIfEmpty(DateTime.UtcNow)
                                     .Max();

            var resultDto = new UpdatedListDto
            {
                LastUpdatedTag = lastUpdatedUtc.ToString("o"),  // ISO 8601 format
                Serials = serials
            };

            return Task.FromResult(new ServiceResult(true, data: resultDto));
        }

        // ===========================
        // Helpers
        // ===========================
        /// <summary>
        /// Local pkpass faylının mövcudluğunu təmin edir.
        /// Əvvəlcə WebRoot + ApplePassUrl yoxlanır, tapılmazsa:
        /// https://appapi.walvero.com/{ApplePassUrl} ünvanından HttpClient ilə download edib lokala yazır.
        /// Uğurlu olarsa local path qaytarır, əks halda null.
        /// </summary>
        private async Task<string?> EnsureLocalPkpassAsync(LoyaltyCard card, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(card.ApplePassUrl))
            {
                _log.LogWarning("Card {CardId} has empty ApplePassUrl", card.Id);
                return null;
            }

            var localPath = Path.Combine(
                _env.WebRootPath,
                card.ApplePassUrl.TrimStart('/')
            );

            // 1) Localda varsa → elə onu qaytar
            if (File.Exists(localPath))
            {
                _log.LogInformation("Local pkpass found at {Path}", localPath);
                return localPath;
            }

            _log.LogWarning("Local pkpass not found at {Path}", localPath);

            // 2) Remote URL: https://walvero.com/{relativePath}
            const string remoteBase = "https://appapi.walvero.com";
            var relativePath = card.ApplePassUrl.TrimStart('/');
            var backupUrl = $"{remoteBase.TrimEnd('/')}/{relativePath}";

            try
            {
                _log.LogInformation("Downloading pkpass from remote url {Url}", backupUrl);

                var client = _http.CreateClient();
                using var resp = await client.GetAsync(backupUrl, ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _log.LogWarning("Remote pkpass download failed. Status={StatusCode}", resp.StatusCode);
                    return null;
                }

                var bytes = await resp.Content.ReadAsByteArrayAsync(ct);

                // directory yoxdursa – yarat
                var dir = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                await File.WriteAllBytesAsync(localPath, bytes, ct);

                _log.LogInformation("Remote pkpass saved to {Path}", localPath);

                return localPath;
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "Error while downloading pkpass from remote url {Url} for CardId={CardId}",
                    backupUrl,
                    card.Id);

                return null;
            }
        }

        private Task<bool> AuthorizeAsync(
            string serial,
            string authHeader,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(authHeader)) return Task.FromResult(false);

            var parts = authHeader.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !parts[0].Equals("ApplePass", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(false);

            var token = parts[1]?.Trim();
            if (string.IsNullOrEmpty(token))
                return Task.FromResult(false);

            var card = _uow.Repository<LoyaltyCard>().Get(c => c.AppleSerial == serial);
            if (card is null)
                return Task.FromResult(false);

            // WebServiceURL istifadə etmirsinizsə, token boş ola bilər — bu halda icazə verə bilərsiniz.
            if (string.IsNullOrWhiteSpace(card.AppleAuthToken))
                return Task.FromResult(true);

            var match = FixedTimeEquals(token, card.AppleAuthToken);
            if (!match)
                _log.LogWarning("[AUTH] Token mismatch for serial={Serial}. Incoming={InToken}, DB={DbToken}", serial, token, card.AppleAuthToken);
            return Task.FromResult(match);
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;
            var result = 0;
            for (int i = 0; i < a.Length; i++)
                result |= a[i] ^ b[i];
            return result == 0;
        }
    }
}
