using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Loyalty.Infrastructure.Utilities.Security.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class PosSyncService : IPosSyncService
    {
        private readonly ITenantIntegrationRepository _integrationRepo;
        private readonly ITenantIntegrationVenueRepository _venueRepo;
        private readonly IPosCustomerMappingRepository _mappingRepo;
        private readonly ILoyaltyCardRepository _cardRepo;
        private readonly ISensitiveDataProtector _protector;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IIikoPosApiClient _iikoClient;
        private readonly IIikoLegacyBalanceService _legacy;
        private readonly IUnitOfWork _uow;
        private readonly LoyaltyWalletDbContext _db;
        private readonly ILogger<PosSyncService> _logger;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public PosSyncService(
            ITenantIntegrationRepository integrationRepo,
            ITenantIntegrationVenueRepository venueRepo,
            IPosCustomerMappingRepository mappingRepo,
            ILoyaltyCardRepository cardRepo,
            ISensitiveDataProtector protector,
            IHttpClientFactory httpClientFactory,
            IIikoPosApiClient iikoClient,
            IIikoLegacyBalanceService legacy,
            IUnitOfWork uow,
            LoyaltyWalletDbContext db,
            ILogger<PosSyncService> logger)
        {
            _integrationRepo = integrationRepo;
            _venueRepo = venueRepo;
            _mappingRepo = mappingRepo;
            _cardRepo = cardRepo;
            _protector = protector;
            _httpClientFactory = httpClientFactory;
            _iikoClient = iikoClient;
            _legacy = legacy;
            _uow = uow;
            _db = db;
            _logger = logger;
        }

        public async Task SyncCustomerToPosAsync(int tenantId, Customer customer, CancellationToken ct = default)
        {
            var integrations = await _integrationRepo.GetAllActiveByTenantAsync(tenantId, ct);
            if (integrations == null || !integrations.Any())
                return;

            // Telefonla axtarış rejimində yeni bağlanma oldu → sonda köhnə balansı Walvero-ya əlavə et + iiko-nu Walvero-ya bərabərləşdir
            var phoneMatchFlow = false;
            foreach (var integration in integrations)
            {
                // iiko üçün ApiBaseUrl məcburi deyil (client-də hardcoded)
                var isIiko = integration.IntegrationType?.Source == "iiko_pos";
                if (!isIiko && string.IsNullOrWhiteSpace(integration.ApiBaseUrl))
                    continue;

                // Per-branch iiko: venue varsa hər org-a ayrıca sync (mapping per-venue). Venue yoxdursa — legacy tək-org (aşağı).
                if (isIiko)
                {
                    var venues = await _venueRepo.GetActiveByIntegrationAsync(integration.Id, ct);
                    if (venues.Count > 0)
                    {
                        try { phoneMatchFlow |= await SyncIikoAllVenuesAsync(integration, customer, venues, ct); }
                        catch (Exception ex) { _logger.LogWarning(ex, "iiko multi-venue sync FAILED: IntegrationId={IntegrationId}, CustomerId={CustomerId}", integration.Id, customer.Id); }
                        continue;
                    }
                }

                try
                {
                    var existing = await _mappingRepo.GetByTenantIntegrationAndCustomerAsync(integration.Id, customer.Id, ct);

                    if (existing != null)
                    {
                        // Mapping var. Yalnız iiko-da enrollment flag false-dursa retry et.
                        if (isIiko && !existing.IsEnrolledInProgram && !string.IsNullOrEmpty(integration.ExternalProgramId))
                        {
                            var retried = await RetryIikoProgramEnrollmentAsync(integration, existing, customer.Id, ct);
                            if (retried)
                            {
                                existing.IsEnrolledInProgram = true;
                                _mappingRepo.Update(existing);
                                await _uow.CommitAsync(ct);
                            }
                        }
                        else
                        {
                            _logger.LogDebug("POS sync skipped (already mapped): IntegrationId={IntegrationId}, CustomerId={CustomerId}", integration.Id, customer.Id);
                        }
                        continue;
                    }

                    string? externalId;
                    bool enrolledInProgram;
                    IikoPhoneMatch? phoneMatch = null;
                    if (isIiko)
                        (externalId, enrolledInProgram, phoneMatch) = await SyncCustomerToIikoAsync(integration, customer, ct);
                    else
                        (externalId, enrolledInProgram) = await SyncToSinglePosAsync(integration, customer, ct);

                    // POS external ID qaytarmasa, öz customer ID-ni istifadə et
                    externalId ??= customer.Id.ToString();

                    if (!string.IsNullOrEmpty(externalId))
                    {
                        // Save mapping
                        var mapping = new PosCustomerMapping
                        {
                            TenantIntegrationId = integration.Id,
                            ExternalCustomerId = externalId,
                            CustomerId = customer.Id,
                            IsEnrolledInProgram = enrolledInProgram,
                            RegUserId = 1,
                            RegDate = DateTime.UtcNow,
                            IsActive = true
                        };
                        ApplyPhoneMatch(mapping, phoneMatch, externalId);
                        await _mappingRepo.AddAsync(mapping);
                        await _uow.CommitAsync(ct);
                        if (isIiko && integration.MatchExistingByPhone) phoneMatchFlow = true;

                        _logger.LogInformation(
                            "POS sync OK: IntegrationId={IntegrationId}, CustomerId={CustomerId}, ExternalId={ExternalId}, EnrolledInProgram={Enrolled}",
                            integration.Id, customer.Id, externalId, enrolledInProgram);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "POS sync FAILED: IntegrationId={IntegrationId}, CustomerId={CustomerId}",
                        integration.Id, customer.Id);
                }
            }

            // Əvvəlki sync-də bağlanıb, kart o vaxt yox idi (enrollment: müştəri → sync → kart) → import indi
            if (!phoneMatchFlow)
                phoneMatchFlow = await _db.Set<PosCustomerMapping>().AsNoTracking()
                    .AnyAsync(m => m.CustomerId == customer.Id && m.IsActive && m.LegacyBalance > 0 && m.LegacyImportedAt == null, ct);
            if (phoneMatchFlow)
                await FinalizePhoneMatchAsync(tenantId, customer.Id, ct);
        }

        /// <summary>
        /// Telefonla bağlanmadan sonra: gözləyən köhnə iiko balansını Walvero kartına əlavə et (X+Y), sonra iiko cüzdanını
        /// Walvero balansına bərabərləşdir (diff — cüzdan org-lar arası ortaqdırsa 2-ci org fərq görmür). Kart yoxdursa import gözləyir.
        /// </summary>
        public async Task FinalizePhoneMatchAsync(int tenantId, int customerId, CancellationToken ct = default)
        {
            try
            {
                await _legacy.ImportPendingAsync(tenantId, customerId, ct);
                var points = await _db.Set<LoyaltyCard>().AsNoTracking()
                    .Where(c => c.TenantId == tenantId && c.CustomerId == customerId && c.IsActive)
                    .OrderByDescending(c => c.Id)
                    .Select(c => (decimal?)c.CurrentPoints)
                    .FirstOrDefaultAsync(ct);
                if (points.HasValue)
                    await SyncBalanceToPosAsync(tenantId, customerId, points.Value, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "iiko phone match finalize FAILED: CustomerId={CustomerId}", customerId);
            }
        }

        /// <summary>Mapping-ə telefonla tapılan qonağın köhnə balansını yaz (yalnız mapping həmin iiko id-yə bağlanıbsa).</summary>
        private static void ApplyPhoneMatch(PosCustomerMapping mapping, IikoPhoneMatch? match, string? externalId)
        {
            if (match == null || match.IsConflict) return;
            if (!string.Equals(match.IikoCustomerId.ToString(), externalId, StringComparison.OrdinalIgnoreCase)) return;
            mapping.LegacyBalance = match.Balance;
            mapping.LegacyWalletId = match.WalletId?.ToString();
        }

        private async Task<(string? ExternalId, bool EnrolledInProgram)> SyncToSinglePosAsync(TenantIntegration integration, Customer customer, CancellationToken ct)
        {
            // iiko POS üçün ayrı işləmə — iiko Cloud API endpoint-lərinə çağırış
            if (integration.IntegrationType?.Source == "iiko_pos")
            {
                var (iikoId, iikoEnrolled, _) = await SyncCustomerToIikoAsync(integration, customer, ct);
                return (iikoId, iikoEnrolled);
            }

            var rawBaseUrl = integration.ApiBaseUrl!.TrimEnd('/');
            var hasUrlCredentials = rawBaseUrl.Contains("@");

            HttpClient client;
            string url;

            if (hasUrlCredentials)
            {
                // URL has credentials (e.g. http://1C:987654321@host/path)
                // Use handler that does NOT auto-send credentials as Auth header
                var handler = new HttpClientHandler
                {
                    Credentials = null,
                    PreAuthenticate = false,
                    UseDefaultCredentials = false
                };
                client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
                url = $"{rawBaseUrl}/customers";
                // No Authorization header - credentials stay in URL only
            }
            else
            {
                client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(15);
                url = $"{rawBaseUrl}/customers";

                // ApiUsername + ApiToken → Basic Auth
                if (!string.IsNullOrEmpty(integration.ApiUsername) && !string.IsNullOrEmpty(integration.ApiTokenEncrypted))
                {
                    var password = _protector.Decrypt(integration.ApiTokenEncrypted);
                    var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{integration.ApiUsername}:{password}"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                }
                // ApiToken only → Bearer
                else if (!string.IsNullOrEmpty(integration.ApiTokenEncrypted))
                {
                    var token = _protector.Decrypt(integration.ApiTokenEncrypted);
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }

            var name = $"{customer.FirstName} {customer.LastName}".Trim();
            if (string.IsNullOrEmpty(name)) name = customer.PhoneNumber;

            // BarcodeValue-nu LoyaltyCard-dan al
            var cards = await _cardRepo.ListByCustomerAsync(integration.TenantId, customer.Id, ct);
            var barcodeValue = cards?
                .Where(c => c.IsActive && !string.IsNullOrEmpty(c.BarcodeValue))
                .OrderByDescending(c => c.Id)
                .Select(c => c.BarcodeValue)
                .FirstOrDefault();

            // WalveroId-ni ApplicationUser-dən al
            long? walveroId = null;
            if (customer.applicationUserId.HasValue)
            {
                walveroId = await _db.Users
                    .Where(u => u.Id == customer.applicationUserId.Value)
                    .Select(u => u.WalveroId)
                    .FirstOrDefaultAsync(ct);
            }

            // Barcode: "walveroId,barcodeValue" formatında
            string barcode;
            if (walveroId.HasValue && !string.IsNullOrEmpty(barcodeValue))
                barcode = $"{walveroId.Value},{barcodeValue}";
            else if (walveroId.HasValue)
                barcode = walveroId.Value.ToString();
            else if (!string.IsNullOrEmpty(barcodeValue))
                barcode = barcodeValue;
            else
                barcode = customer.Id.ToString();

            var barcode1 = walveroId?.ToString() ?? "";
            var barcode2 = barcodeValue ?? "";

            var body = new[]
            {
                new
                {
                    venue_id = integration.ExternalVenueId ?? "1",
                    type = "add",
                    data = new[]
                    {
                        new
                        {
                            name = name,
                            customer_code = customer.Id.ToString(),
                            barcode = barcode,
                            barcode1 = barcode1,
                            barcode2 = barcode2
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogDebug("POS sync request: Url={Url}, Body={Body}", url, json);

            var response = await client.PostAsync(url, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            _logger.LogDebug("POS sync response: Status={StatusCode}, Body={Body}", response.StatusCode, responseBody);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("POS sync HTTP error: Status={StatusCode}, Body={Body}, Url={Url}",
                    response.StatusCode, responseBody, url);
                return (null, true);
            }

            // Try to extract external customer ID from response
            return (ExtractExternalId(responseBody), true);
        }

        private async Task<(string? ExternalId, bool EnrolledInProgram, IikoPhoneMatch? Match)> SyncCustomerToIikoAsync(TenantIntegration integration, Customer customer, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(integration.ApiTokenEncrypted) || string.IsNullOrEmpty(integration.ExternalVenueId))
            {
                _logger.LogWarning("iiko sync: ApiToken or ExternalVenueId is missing. IntegrationId={Id}", integration.Id);
                return (null, false, null);
            }

            var apiLogin = _protector.Decrypt(integration.ApiTokenEncrypted);
            if (string.IsNullOrEmpty(apiLogin))
                return (null, false, null);

            // iiko access token (55 dəq cache) — v2 app-auth (appId/clientSecret optional)
            var clientSecret = string.IsNullOrEmpty(integration.ApiSecretEncrypted) ? null : _protector.Decrypt(integration.ApiSecretEncrypted);
            var accessToken = await _iikoClient.GetAccessTokenAsync(apiLogin, integration.ApiAppId, clientSecret, ct: ct);
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("iiko sync: access_token alınmadı. IntegrationId={Id}", integration.Id);
                return (null, false, null);
            }

            if (!Guid.TryParse(integration.ExternalVenueId, out var orgId))
            {
                _logger.LogWarning("iiko sync: ExternalVenueId GUID deyil: {VenueId}", integration.ExternalVenueId);
                return (null, false, null);
            }

            var (cardsInfo, walveroId, primaryCard, allTracks) = await GatherIikoCardInfoAsync(integration, customer, ct);
            var match = integration.MatchExistingByPhone
                ? await _legacy.FindByPhoneAsync(integration, accessToken, orgId, customer, ct)
                : null;
            var (externalId, enrolled) = await CreateIikoCustomerInOrgAsync(integration, customer, accessToken, orgId, integration.ExternalProgramId,
                cardsInfo, walveroId, primaryCard, allTracks, match, ct);
            return (externalId, enrolled, match);
        }

        /// <summary>
        /// Per-branch: hər venue org-una ayrıca müştəri yarat + per-venue mapping. Token bir dəfə alınır.
        /// Qaytarır: telefonla axtarış rejimində yeni mapping yarandı (köhnə balans finalize lazımdır).
        /// </summary>
        private async Task<bool> SyncIikoAllVenuesAsync(TenantIntegration integration, Customer customer, List<TenantIntegrationVenue> venues, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(integration.ApiTokenEncrypted)) return false;
            var accessToken = await GetIikoTokenAsync(integration, ct);
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("iiko multi-venue sync: access_token alınmadı. IntegrationId={Id}", integration.Id);
                return false;
            }

            var (cardsInfo, walveroId, primaryCard, allTracks) = await GatherIikoCardInfoAsync(integration, customer, ct);
            var ensuredTracksFor = new HashSet<Guid>();
            // iiko qonağı + cüzdan org-lar arası ortaqdır → telefonla axtarış bir dəfə (ilk yaradılacaq venue-da)
            IikoPhoneMatch? match = null;
            var matchSearched = false;
            var newlyLinked = false;

            foreach (var venue in venues)
            {
                if (!Guid.TryParse(venue.ExternalVenueId, out var orgId))
                {
                    _logger.LogWarning("iiko sync: venue ExternalVenueId GUID deyil: {VenueId} (venueRowId={RowId})", venue.ExternalVenueId, venue.Id);
                    continue;
                }
                var programId = string.IsNullOrEmpty(venue.ExternalProgramId) ? integration.ExternalProgramId : venue.ExternalProgramId;
                var existing = await _mappingRepo.GetByVenueAndCustomerAsync(venue.Id, customer.Id, ct);

                if (existing != null)
                {
                    if (!existing.IsEnrolledInProgram && !string.IsNullOrEmpty(programId) && Guid.TryParse(existing.ExternalCustomerId, out var exCustId))
                    {
                        var retried = await TryEnrollIikoCustomerInProgramAsync(programId, accessToken, orgId, exCustId, customer.Id, ct);
                        if (retried)
                        {
                            existing.IsEnrolledInProgram = true;
                            _mappingRepo.Update(existing);
                            await _uow.CommitAsync(ct);
                        }
                    }
                    // Mapping əvvəldən var (admin import / push ilə yaranıb) — müştəri sonradan app-a qeydiyyatdan keçibsə
                    // WalveroId və kart barkodu iiko-da olmaya bilər → çatışmayanları əlavə et (iiko müştərisi org-lar arası ortaqdır, 1 dəfə).
                    if (Guid.TryParse(existing.ExternalCustomerId, out var mappedId) && ensuredTracksFor.Add(mappedId))
                        await EnsureIikoCardTracksAsync(accessToken, orgId, mappedId, customer.Id, allTracks, ct);
                    continue;
                }

                if (integration.MatchExistingByPhone && !matchSearched)
                {
                    match = await _legacy.FindByPhoneAsync(integration, accessToken, orgId, customer, ct);
                    matchSearched = true;
                }
                var (externalId, enrolled) = await CreateIikoCustomerInOrgAsync(integration, customer, accessToken, orgId, programId,
                    cardsInfo, walveroId, primaryCard, allTracks, match, ct);
                externalId ??= customer.Id.ToString();
                var mapping = new PosCustomerMapping
                {
                    TenantIntegrationId = integration.Id,
                    TenantIntegrationVenueId = venue.Id,
                    ExternalCustomerId = externalId,
                    CustomerId = customer.Id,
                    IsEnrolledInProgram = enrolled,
                    RegUserId = 1,
                    RegDate = DateTime.UtcNow,
                    IsActive = true
                };
                ApplyPhoneMatch(mapping, match, externalId);
                await _mappingRepo.AddAsync(mapping);
                await _uow.CommitAsync(ct);
                if (integration.MatchExistingByPhone) newlyLinked = true;
                _logger.LogInformation("iiko venue sync OK: IntegrationId={Id}, VenueRowId={VenueId}, CustomerId={CustomerId}, iikoId={IikoId}", integration.Id, venue.Id, customer.Id, externalId);
            }
            return newlyLinked;
        }

        /// <summary>iiko müştərisində çatışmayan kart-track-ləri əlavə et (best-effort; mövcud olanlar keçilir).</summary>
        private async Task EnsureIikoCardTracksAsync(string accessToken, Guid orgId, Guid iikoCustomerId, int customerId, IEnumerable<string?> tracks, CancellationToken ct)
        {
            var wanted = tracks.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (wanted.Count == 0) return;
            try
            {
                var info = await _iikoClient.FindCustomerAsync(accessToken, orgId, IikoCustomerLookup.Id, iikoCustomerId.ToString(), ct);
                var have = info.Data?.Cards?.Select(c => c.Track ?? c.Number ?? string.Empty).Where(s => s.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase)
                           ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var track in wanted.Where(t => !have.Contains(t)))
                {
                    var ok = await _iikoClient.AddCustomerCardAsync(accessToken, orgId, iikoCustomerId, track, track, ct);
                    _logger.LogInformation("iiko sync: card track add (mapped customer). CustomerId={CustomerId}, iikoId={IikoId}, Track={Track}, Success={Success}",
                        customerId, iikoCustomerId, track, ok);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "iiko sync: card track ensure failed. CustomerId={CustomerId}, iikoId={IikoId}", customerId, iikoCustomerId);
            }
        }

        private async Task<string?> GetIikoTokenAsync(TenantIntegration integration, CancellationToken ct)
        {
            var apiLogin = _protector.Decrypt(integration.ApiTokenEncrypted!);
            if (string.IsNullOrEmpty(apiLogin)) return null;
            var clientSecret = string.IsNullOrEmpty(integration.ApiSecretEncrypted) ? null : _protector.Decrypt(integration.ApiSecretEncrypted);
            return await _iikoClient.GetAccessTokenAsync(apiLogin, integration.ApiAppId, clientSecret, ct: ct);
        }

        /// <summary>
        /// Kartlar + WalveroId + iiko-ya gedən track-lər. PrimaryCard = inteqrasiyanın CardTrackMode-una görə əsas track
        /// (tam barkod / WalveroId / son N); AllTracks = əsas + tam barkod (həmişə) + WalveroId. Barkod yoxdursa Customer.Id.
        /// </summary>
        private async Task<(List<LoyaltyCard>? Cards, long? WalveroId, string PrimaryCard, List<string> AllTracks)> GatherIikoCardInfoAsync(TenantIntegration integration, Customer customer, CancellationToken ct)
        {
            var tenantId = integration.TenantId;
            var cards = await _cardRepo.ListByCustomerAsync(tenantId, customer.Id, ct);
            var barcodeValue = cards?
                .Where(c => c.IsActive && !string.IsNullOrEmpty(c.BarcodeValue))
                .OrderByDescending(c => c.Id)
                .Select(c => c.BarcodeValue)
                .FirstOrDefault();

            long? walveroId = null;
            if (customer.applicationUserId.HasValue)
            {
                walveroId = await _db.Users
                    .Where(u => u.Id == customer.applicationUserId.Value)
                    .Select(u => u.WalveroId)
                    .FirstOrDefaultAsync(ct);
            }

            var allTracks = PosCardTrack.AllTracks(integration.CardTrackMode, integration.CardTrackDigits, barcodeValue, walveroId?.ToString());
            var primaryCard = allTracks.FirstOrDefault() ?? customer.Id.ToString();
            return (cards?.ToList(), walveroId, primaryCard, allTracks);
        }

        /// <summary>
        /// Bir iiko org-da müştəri yarat/yenilə + program enroll + walveroId kartı + balans topup. programId — per-org (venue) və ya parent.
        /// match — telefonla tapılan mövcud qonaq (Id ilə yenilənir, yeni qonaq yaranmır); konfliktdə telefon göndərilmir.
        /// </summary>
        private async Task<(string? ExternalId, bool EnrolledInProgram)> CreateIikoCustomerInOrgAsync(
            TenantIntegration integration, Customer customer, string accessToken, Guid orgId, string? programId,
            List<LoyaltyCard>? cards, long? walveroId, string primaryCard, List<string> allTracks, IikoPhoneMatch? match, CancellationToken ct)
        {
            var ambassadorCategoryId = integration.AmbassadorIikoCategoryId;
            var conflict = match?.IsConflict == true;
            var iikoCustomer = new IikoCustomerDto
            {
                Id = match != null && !conflict ? match.IikoCustomerId : null,
                Name = customer.FirstName?.Trim() ?? "",
                Surname = customer.LastName?.Trim(),
                // null JSON-a yazılmır → iiko-dakı telefon dəyişmir. Konflikt: başqa Walvero müştərisinin qonağına birləşməsin
                Phone = integration.SendPhoneToPos && !conflict ? Helpers.PhoneNormalizer.ToE164(customer.PhoneNumber) : null,
                Email = string.IsNullOrWhiteSpace(customer.Email) ? null : customer.Email.Trim(),
                CardTrack = primaryCard,
                CardNumber = primaryCard,
                // iiko 1900-01-01–bu gün aralığından kənar tarixi rədd edir → sahə göndərilmir
                Birthday = Helpers.BirthDateRules.IsWithinPosRange(customer.DateOfBirth, DateTime.UtcNow)
                    ? customer.DateOfBirth!.Value.ToString("yyyy-MM-dd HH:mm:ss.fff")
                    : null
            };

            var result = await _iikoClient.CreateOrUpdateCustomerAsync(accessToken, orgId, iikoCustomer, ct);
            if (result == null)
            {
                _logger.LogWarning("iiko sync: CreateOrUpdate failed. CustomerId={CustomerId} orgId={OrgId}", customer.Id, orgId);
                return (null, false);
            }

            var enrolledInProgram = await TryEnrollIikoCustomerInProgramAsync(programId, accessToken, orgId, result.Id, customer.Id, ct);

            // Kartın proqramının öz iiko proqramı/cüzdanı (məs. ambassador) — müştəri ona da yazılır ki, cüzdan yaransın
            var target = await GetWalletTargetAsync(customer.TenantId, customer.Id, ct);
            if (!string.IsNullOrWhiteSpace(target.IikoProgramId)
                && !string.Equals(target.IikoProgramId, programId, StringComparison.OrdinalIgnoreCase))
                await TryEnrollIikoCustomerInProgramAsync(target.IikoProgramId, accessToken, orgId, result.Id, customer.Id, ct);

            // Ambassador qonaq kateqoriyası
            if (customer.IsAmbassador && Guid.TryParse(ambassadorCategoryId, out var ambCategory))
            {
                var catOk = await _iikoClient.AddCustomerCategoryAsync(accessToken, orgId, result.Id, ambCategory, ct);
                _logger.LogInformation("iiko ambassador category add (create): CustomerId={CustomerId}, orgId={OrgId}, Category={Category}, Success={Success}",
                    customer.Id, orgId, ambCategory, catOk);
            }

            // Əlavə kartlar: tam barkod (skan üçün həmişə) + WalveroId — əsas track create_or_update ilə yazılıb
            foreach (var track in allTracks.Where(t => !string.Equals(t, primaryCard, StringComparison.OrdinalIgnoreCase)))
            {
                var addCardOk = await _iikoClient.AddCustomerCardAsync(accessToken, orgId, result.Id, track, track, ct);
                if (!addCardOk)
                    _logger.LogInformation("iiko sync: əlavə kart track yazılmadı (ola bilsin artıq var). CustomerId={CustomerId}, Track={Track}", customer.Id, track);
            }

            // Telefonla axtarış rejimində balans sonda FinalizePhoneMatchAsync ilə gedir (köhnə balans əlavəsindən sonra, diff)
            if (!integration.MatchExistingByPhone && cards != null && cards.Any())
            {
                var activeCard = cards.Where(c => c.IsActive).OrderByDescending(c => c.Id).FirstOrDefault();
                if (activeCard != null && activeCard.CurrentPoints > 0)
                {
                    var customerInfo = await _iikoClient.GetCustomerInfoAsync(accessToken, orgId, cardTrack: primaryCard, ct: ct);
                    var wallets = (customerInfo?.WalletBalances ?? new List<IikoWalletBalance>()).Select(w => (w.Id, w.Balance)).ToList();
                    // Proqramın cüzdanı (PosWalletId) — başqa cüzdana yazılmır; təyin edilməyibsə birinci (bugünkü)
                    var pick = Helpers.IikoWalletSelection.Pick(wallets, target.WalletId);
                    if (pick.Outcome == Helpers.IikoWalletSelection.Outcome.Found)
                    {
                        var topUpOk = await _iikoClient.TopUpWalletAsync(accessToken, orgId, result.Id, pick.WalletId!.Value, activeCard.CurrentPoints, ct);
                        _logger.LogInformation("iiko sync balance: CustomerId={CustomerId}, WalletId={WalletId}, Amount={Amount}, Success={Success}", customer.Id, pick.WalletId, activeCard.CurrentPoints, topUpOk);
                    }
                    else
                        _logger.LogWarning("iiko sync balance skipped: wallet {Outcome}. CustomerId={CustomerId}, PreferredWallet={Wallet}", pick.Outcome, customer.Id, target.WalletId);
                }
            }

            _logger.LogInformation("iiko sync OK: CustomerId={CustomerId}, orgId={OrgId}, iikoId={IikoId}", customer.Id, orgId, result.Id);
            return (result.Id.ToString(), enrolledInProgram);
        }

        /// <summary>
        /// iiko customer_program/add chağırışı. programId yoxdursa true qayıdır (nothing to do).
        /// Bad GUID — false + warning. HTTP success/fail nəticəsi log-da result={True|False} kimi yazılır.
        /// </summary>
        private async Task<bool> TryEnrollIikoCustomerInProgramAsync(
            string? programIdStr, string accessToken, Guid orgId, Guid iikoCustomerId,
            int customerId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(programIdStr))
                return true;

            if (!Guid.TryParse(programIdStr, out var programId))
            {
                _logger.LogWarning(
                    "iiko ExternalProgramId is not a valid GUID; skipping customer_program/add. value={Value} customerId={CustomerId}",
                    programIdStr, customerId);
                return false;
            }

            var added = await _iikoClient.AddCustomerToProgramAsync(accessToken, orgId, iikoCustomerId, programId, ct);
            _logger.LogInformation(
                "iiko customer_program/add result={Result} customerId={CustomerId} iikoCustomerId={IikoCustomerId} programId={ProgramId} orgId={OrgId}",
                added, customerId, iikoCustomerId, programId, orgId);
            return added;
        }

        /// <summary>
        /// Legacy (tək-org) mapping üçün proqrama enrollment-i təkrar et. Token + GUID-lər valid deyilsə false.
        /// </summary>
        private async Task<bool> RetryIikoProgramEnrollmentAsync(
            TenantIntegration integration, PosCustomerMapping mapping, int customerId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(integration.ApiTokenEncrypted)) return false;
            var accessToken = await GetIikoTokenAsync(integration, ct);
            if (string.IsNullOrEmpty(accessToken)) return false;

            if (!Guid.TryParse(integration.ExternalVenueId, out var orgId)) return false;
            if (!Guid.TryParse(mapping.ExternalCustomerId, out var iikoCustomerId)) return false;

            return await TryEnrollIikoCustomerInProgramAsync(integration.ExternalProgramId, accessToken, orgId, iikoCustomerId, customerId, ct);
        }

        public async Task<bool> SyncBalanceToPosAsync(int tenantId, int customerId, decimal currentPoints, CancellationToken ct = default)
        {
            var integrations = await _integrationRepo.GetAllActiveByTenantAsync(tenantId, ct);
            if (integrations == null || !integrations.Any())
                return true;

            var allOk = true;
            foreach (var integration in integrations)
            {
                var isIiko = integration.IntegrationType?.Source == "iiko_pos";
                if (!isIiko) continue;

                try
                {
                    var accessToken = await GetIikoTokenAsync(integration, ct);
                    if (string.IsNullOrEmpty(accessToken)) { allOk = false; continue; }

                    var target = await GetWalletTargetAsync(tenantId, customerId, ct);
                    var cards = await _cardRepo.ListByCustomerAsync(tenantId, customerId, ct);
                    var cardTrack = cards?
                        .Where(c => c.IsActive && !string.IsNullOrEmpty(c.BarcodeValue))
                        .OrderByDescending(c => c.Id)
                        .Select(c => c.BarcodeValue)
                        .FirstOrDefault() ?? customerId.ToString();

                    // Per-branch: venue varsa hər org-da balans sync. Venue yoxdursa — legacy parent org.
                    var venues = await _venueRepo.GetActiveByIntegrationAsync(integration.Id, ct);
                    if (venues.Count > 0)
                    {
                        foreach (var venue in venues)
                        {
                            if (!Guid.TryParse(venue.ExternalVenueId, out var vOrgId)) continue;
                            var vMapping = await _mappingRepo.GetByVenueAndCustomerAsync(venue.Id, customerId, ct);
                            if (vMapping == null) continue;
                            if (!await TopUpIikoBalanceAsync(accessToken, vOrgId, vMapping, cardTrack, currentPoints, customerId, target, ct))
                                allOk = false;
                        }
                    }
                    else
                    {
                        var mapping = await _mappingRepo.GetByTenantIntegrationAndCustomerAsync(integration.Id, customerId, ct);
                        if (mapping == null)
                        {
                            _logger.LogDebug("Balance sync skipped (no mapping): IntegrationId={IntegrationId}, CustomerId={CustomerId}", integration.Id, customerId);
                            continue;
                        }
                        if (!Guid.TryParse(integration.ExternalVenueId, out var orgId)) continue;
                        if (!await TopUpIikoBalanceAsync(accessToken, orgId, mapping, cardTrack, currentPoints, customerId, target, ct))
                            allOk = false;
                    }
                }
                catch (Exception ex)
                {
                    allOk = false;
                    _logger.LogWarning(ex, "iiko balance sync FAILED: IntegrationId={IntegrationId}, CustomerId={CustomerId}", integration.Id, customerId);
                }
            }
            return allOk;
        }

        /// <summary>Müştərinin aktiv kartının proqramına bağlı iiko cüzdanı + iiko proqramı (null = bugünkü davranış).</summary>
        private async Task<(string? WalletId, string? IikoProgramId)> GetWalletTargetAsync(int tenantId, int customerId, CancellationToken ct)
        {
            var programId = await _db.Set<LoyaltyCard>().AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.CustomerId == customerId && c.IsActive)
                .OrderByDescending(c => c.Id)
                .Select(c => (int?)c.LoyaltyProgramId)
                .FirstOrDefaultAsync(ct);
            if (programId == null) return (null, null);
            var p = await _db.Set<LoyaltyProgram>().AsNoTracking()
                .Where(x => x.Id == programId.Value)
                .Select(x => new { x.PosWalletId, x.PosExternalProgramId })
                .FirstOrDefaultAsync(ct);
            return (p?.PosWalletId, p?.PosExternalProgramId);
        }

        public async Task<bool> SyncAmbassadorCategoryAsync(int tenantId, int customerId, bool isAmbassador, CancellationToken ct = default)
        {
            var integrations = await _integrationRepo.GetAllActiveByTenantAsync(tenantId, ct);
            var allOk = true;
            foreach (var integration in integrations ?? Enumerable.Empty<TenantIntegration>())
            {
                if (integration.IntegrationType?.Source != "iiko_pos") continue;
                if (!Guid.TryParse(integration.AmbassadorIikoCategoryId, out var categoryId)) continue;
                try
                {
                    var accessToken = await GetIikoTokenAsync(integration, ct);
                    if (string.IsNullOrEmpty(accessToken)) { allOk = false; continue; }

                    // org → iiko müştəri id (venue mapping-ləri; yoxdursa legacy tək-org mapping)
                    var targets = new List<(Guid OrgId, string? ExternalCustomerId)>();
                    var venues = await _venueRepo.GetActiveByIntegrationAsync(integration.Id, ct);
                    if (venues.Count > 0)
                    {
                        foreach (var v in venues)
                        {
                            if (!Guid.TryParse(v.ExternalVenueId, out var vOrg)) continue;
                            var m = await _mappingRepo.GetByVenueAndCustomerAsync(v.Id, customerId, ct);
                            targets.Add((vOrg, m?.ExternalCustomerId));
                        }
                    }
                    else if (Guid.TryParse(integration.ExternalVenueId, out var org))
                    {
                        var m = await _mappingRepo.GetByTenantIntegrationAndCustomerAsync(integration.Id, customerId, ct);
                        targets.Add((org, m?.ExternalCustomerId));
                    }

                    foreach (var (orgId, extId) in targets)
                    {
                        if (!Guid.TryParse(extId, out var iikoCustomerId))
                        {
                            _logger.LogInformation("iiko ambassador category skipped (no mapping): CustomerId={CustomerId}, orgId={OrgId}", customerId, orgId);
                            continue;
                        }
                        var ok = isAmbassador
                            ? await _iikoClient.AddCustomerCategoryAsync(accessToken, orgId, iikoCustomerId, categoryId, ct)
                            : await _iikoClient.RemoveCustomerCategoryAsync(accessToken, orgId, iikoCustomerId, categoryId, ct);
                        _logger.LogInformation("iiko ambassador category {Op}: CustomerId={CustomerId}, orgId={OrgId}, Category={Category}, Success={Success}",
                            isAmbassador ? "add" : "remove", customerId, orgId, categoryId, ok);
                        if (!ok) allOk = false;
                    }
                }
                catch (Exception ex)
                {
                    allOk = false;
                    _logger.LogWarning(ex, "iiko ambassador category sync FAILED: IntegrationId={IntegrationId}, CustomerId={CustomerId}", integration.Id, customerId);
                }
            }
            return allOk;
        }

        /// <summary>Bir iiko org-da müştərinin wallet balansını currentPoints-ə uyğunlaşdır (diff topup). Qaytarır: uğur (fərq yoxdursa true; wallet tapılmasa false).</summary>
        private async Task<bool> TopUpIikoBalanceAsync(string accessToken, Guid orgId, PosCustomerMapping mapping, string cardTrack, decimal currentPoints, int customerId,
            (string? WalletId, string? IikoProgramId) target, CancellationToken ct)
        {
            // Telefonla bağlanmış qonağın köhnə balansı hələ Walvero-ya yazılmayıb — push etsək iiko-dakı X chargeoff olunar
            if (IikoLegacyBalance.IsPending(mapping.LegacyBalance, mapping.LegacyImportedAt))
            {
                _logger.LogInformation("iiko balance sync skipped (legacy import pending): CustomerId={CustomerId}, orgId={OrgId}, Legacy={Legacy}",
                    customerId, orgId, mapping.LegacyBalance);
                return true;
            }

            // Mapping-dəki iiko id ilə axtar (kart track dəyişmiş ola bilər), tapılmasa cardTrack ilə.
            IikoCustomerInfoResponse? customerInfo = null;
            var hasMappedId = Guid.TryParse(mapping.ExternalCustomerId, out var mappedId);
            if (hasMappedId)
                customerInfo = (await _iikoClient.FindCustomerAsync(accessToken, orgId, IikoCustomerLookup.Id, mappedId.ToString(), ct)).Data;
            customerInfo ??= await _iikoClient.GetCustomerInfoAsync(accessToken, orgId, cardTrack: cardTrack, ct: ct);

            var iikoCustomerId = hasMappedId ? mappedId : (customerInfo?.Id ?? Guid.Empty);

            // Proqramın cüzdanı (PosWalletId; pims: Cashback vs Ambassador) — təyin edilməyibsə birinci (bugünkü davranış)
            var wallets = (customerInfo?.WalletBalances ?? new List<IikoWalletBalance>()).Select(w => (w.Id, w.Balance)).ToList();
            var pick = Helpers.IikoWalletSelection.Pick(wallets, target.WalletId);
            if (pick.Outcome == Helpers.IikoWalletSelection.Outcome.PreferredMissing
                && iikoCustomerId != Guid.Empty && !string.IsNullOrWhiteSpace(target.IikoProgramId))
            {
                // Müştəri həmin iiko proqramında deyil → yaz, cüzdan yaranır, yenidən oxu
                await TryEnrollIikoCustomerInProgramAsync(target.IikoProgramId, accessToken, orgId, iikoCustomerId, customerId, ct);
                var again = (await _iikoClient.FindCustomerAsync(accessToken, orgId, IikoCustomerLookup.Id, iikoCustomerId.ToString(), ct)).Data;
                wallets = (again?.WalletBalances ?? new List<IikoWalletBalance>()).Select(w => (w.Id, w.Balance)).ToList();
                pick = Helpers.IikoWalletSelection.Pick(wallets, target.WalletId);
            }

            if (pick.Outcome == Helpers.IikoWalletSelection.Outcome.Found)
            {
                var walletId = pick.WalletId!.Value;
                var diff = Math.Round(currentPoints - pick.Balance, 2);
                if (diff == 0) return true;

                // iiko-da mənfi topup yoxdur — azalma chargeoff ilə gedir
                var ok = diff > 0
                    ? await _iikoClient.TopUpWalletAsync(accessToken, orgId, iikoCustomerId, walletId, diff, ct)
                    : await _iikoClient.ChargeOffWalletAsync(accessToken, orgId, iikoCustomerId, walletId, -diff, ct);
                _logger.LogInformation(
                    "iiko balance sync: CustomerId={CustomerId}, orgId={OrgId}, WalletId={WalletId}, Diff={Diff}, NewBalance={NewBalance}, Success={Success}",
                    customerId, orgId, walletId, diff, currentPoints, ok);
                return ok;
            }

            _logger.LogWarning("iiko balance sync: wallet {Outcome}. CustomerId={CustomerId}, orgId={OrgId}, PreferredWallet={Wallet}",
                pick.Outcome, customerId, orgId, target.WalletId);
            return false;
        }

        /// <summary>
        /// POS response-dan external customer ID-ni extract edir.
        /// Ferqli POS formatlarini destekleyir:
        /// - {"id": "123"} veya {"id": 123}
        /// - {"customerId": "123"}
        /// - {"data": {"id": "123"}}
        /// - {"customers": [{"id": "123"}]}
        /// - [{"id": "123"}]
        /// </summary>
        private string? ExtractExternalId(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                // Array response: [{"id": "123"}]
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    return TryGetId(root[0]);
                }

                // Object response
                if (root.ValueKind == JsonValueKind.Object)
                {
                    // Direct: {"id": "123"} or {"customerId": "123"}
                    var id = TryGetId(root);
                    if (id != null) return id;

                    // Nested: {"data": {"id": "123"}}
                    if (root.TryGetProperty("data", out var data))
                    {
                        if (data.ValueKind == JsonValueKind.Object)
                            return TryGetId(data);
                        if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
                            return TryGetId(data[0]);
                    }

                    // Array field: {"customers": [{"id": "123"}]}
                    if (root.TryGetProperty("customers", out var customers) &&
                        customers.ValueKind == JsonValueKind.Array && customers.GetArrayLength() > 0)
                    {
                        return TryGetId(customers[0]);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse POS response for external ID");
            }

            return null;
        }

        private static string? TryGetId(JsonElement el)
        {
            foreach (var prop in new[] { "id", "Id", "ID", "customerId", "CustomerId", "customer_id" })
            {
                if (el.TryGetProperty(prop, out var val))
                {
                    return val.ValueKind switch
                    {
                        JsonValueKind.String => val.GetString(),
                        JsonValueKind.Number => val.GetRawText(),
                        _ => null
                    };
                }
            }
            return null;
        }
    }
}
