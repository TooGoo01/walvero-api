using Hangfire;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Loyalty.Infrastructure.Utilities.Security.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class PosCustomerIikoService : IPosCustomerIikoService
    {
        private const string IikoSource = "iiko_pos";

        private readonly ITenantIntegrationRepository _integrationRepo;
        private readonly ITenantIntegrationVenueRepository _venueRepo;
        private readonly IPosCustomerMappingRepository _mappingRepo;
        private readonly ILoyaltyCardRepository _cardRepo;
        private readonly ILoyaltyProgramRepository _programRepo;
        private readonly ISensitiveDataProtector _protector;
        private readonly IIikoPosApiClient _iiko;
        private readonly IIikoLegacyBalanceService _legacy;
        private readonly IPosSyncService _posSync;
        private readonly IUnitOfWork _uow;
        private readonly LoyaltyWalletDbContext _db;
        private readonly ILogger<PosCustomerIikoService> _logger;

        public PosCustomerIikoService(
            ITenantIntegrationRepository integrationRepo,
            ITenantIntegrationVenueRepository venueRepo,
            IPosCustomerMappingRepository mappingRepo,
            ILoyaltyCardRepository cardRepo,
            ILoyaltyProgramRepository programRepo,
            ISensitiveDataProtector protector,
            IIikoPosApiClient iiko,
            IIikoLegacyBalanceService legacy,
            IPosSyncService posSync,
            IUnitOfWork uow,
            LoyaltyWalletDbContext db,
            ILogger<PosCustomerIikoService> logger)
        {
            _integrationRepo = integrationRepo;
            _venueRepo = venueRepo;
            _mappingRepo = mappingRepo;
            _cardRepo = cardRepo;
            _programRepo = programRepo;
            _protector = protector;
            _iiko = iiko;
            _legacy = legacy;
            _posSync = posSync;
            _uow = uow;
            _db = db;
            _logger = logger;
        }

        private sealed class Ctx
        {
            public Customer Customer = null!;
            public List<LoyaltyCard> Cards = new();
            public LoyaltyCard? DefaultCard;
            public long? WalveroId;
            public string? Phone;
            public string? LookupCardTrack;
            public List<TenantIntegration> Integrations = new();
            public Dictionary<int, string?> Tokens = new();
            public Dictionary<int, CardTemplateType> TemplateTypes = new();
            public Dictionary<Guid, List<IikoLoyaltyProgram>?> ProgramsByOrg = new();
            /// <summary>Kartın proqramına bağlı iiko cüzdanı (LoyaltyProgram.PosWalletId) — varsa seçimdə üstündür.</summary>
            public string? PreferredWalletId;
        }

        public async Task<ServiceResult<PosCustomerIikoStatusDto>> GetStatusAsync(int tenantId, int customerId, int? cardId, string? lookupCardTrack = null, CancellationToken ct = default)
        {
            var (ctx, error) = await LoadAsync(tenantId, customerId, cardId, lookupCardTrack, ct);
            if (ctx == null) return ServiceResult<PosCustomerIikoStatusDto>.Fail(error!);
            return ServiceResult<PosCustomerIikoStatusDto>.Ok(await BuildStatusAsync(ctx, ct));
        }

        public async Task<ServiceResult<PosCustomerIikoStatusDto>> PushBalanceAsync(int tenantId, int customerId, IikoPushBalanceDto dto, CancellationToken ct = default)
        {
            var (ctx, error) = await LoadAsync(tenantId, customerId, dto.CardId, dto.LookupCardTrack, ct);
            if (ctx == null) return ServiceResult<PosCustomerIikoStatusDto>.Fail(error!);
            if (ctx.DefaultCard == null) return ServiceResult<PosCustomerIikoStatusDto>.Fail("Aktiv kart yoxdur.");

            var status = await BuildStatusAsync(ctx, ct);
            var isProgress = TemplateOf(ctx, ctx.DefaultCard) == CardTemplateType.ProgressBased;
            var errors = new Dictionary<(int, int?), string>();
            var pushed = 0;
            // iiko wallet org-lar arası paylaşıla bilər (eyni müştəri id + eyni wallet) — bir dəfə düzəlt
            var touchedWallets = new HashSet<(Guid, Guid)>();
            var cardAddedTo = new HashSet<Guid>();

            foreach (var v in status.Venues)
            {
                if (dto.IntegrationId.HasValue && v.IntegrationId != dto.IntegrationId.Value) continue;
                if (dto.VenueId.HasValue && v.VenueId != dto.VenueId.Value) continue;
                if (!v.Found || v.Error != null || v.IikoCustomerId == null) continue;

                var token = ctx.Tokens.GetValueOrDefault(v.IntegrationId);
                if (string.IsNullOrEmpty(token)) continue;
                var orgId = Guid.Parse(v.OrganizationId);

                if (!v.MappingExists)
                {
                    var mapErr = await EnsureMappingAsync(v.IntegrationId, v.VenueId, v.IikoCustomerId.Value, customerId, ct);
                    if (mapErr != null) { errors[(v.IntegrationId, v.VenueId)] = mapErr; continue; }
                }

                // iiko müştərisində çatışmayan kart-track-ləri əlavə et — kassada skan işləsin:
                //   bizim kart barkodu (789…), app WalveroId (varsa), köhnə sistemin fiziki kartı (lookupCardTrack).
                // iiko müştərisi org-lar arası paylaşılır → hər iiko id üçün bir dəfə; uğursuzluq (artıq var / başqasında) yalnız log.
                if (cardAddedTo.Add(v.IikoCustomerId.Value))
                {
                    // Əsas track inteqrasiyanın CardTrackMode-una görə (son N rəqəm ola bilər); tam barkod + WalveroId həmişə
                    var integ = ctx.Integrations.FirstOrDefault(i => i.Id == v.IntegrationId);
                    var wanted = PosCardTrack.AllTracks(
                        integ?.CardTrackMode ?? PosCardTrackMode.FullBarcode, integ?.CardTrackDigits,
                        ctx.DefaultCard?.BarcodeValue, ctx.WalveroId?.ToString());
                    if (!string.IsNullOrWhiteSpace(ctx.LookupCardTrack)) wanted.Add(ctx.LookupCardTrack.Trim());
                    var tracks = wanted
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Where(t => !v.IikoCardTracks.Contains(t, StringComparer.OrdinalIgnoreCase));
                    foreach (var track in tracks)
                    {
                        var addOk = await _iiko.AddCustomerCardAsync(token, orgId, v.IikoCustomerId.Value, track, track, ct);
                        _logger.LogInformation(
                            "iiko admin push: card track add. CustomerId={CustomerId}, iikoId={IikoId}, Track={Track}, Success={Success}",
                            customerId, v.IikoCustomerId, track, addOk);
                    }
                }

                if (v.SelectedWalletId == null) { errors[(v.IntegrationId, v.VenueId)] = "iiko wallet tapılmadı."; continue; }
                if (!touchedWallets.Add((v.IikoCustomerId.Value, v.SelectedWalletId.Value))) continue;

                var diff = v.Diff ?? 0m;
                if (isProgress) diff = Math.Truncate(diff);
                if (diff == 0) continue;

                var ok = diff > 0
                    ? await _iiko.TopUpWalletAsync(token, orgId, v.IikoCustomerId.Value, v.SelectedWalletId.Value, diff, ct)
                    : await _iiko.ChargeOffWalletAsync(token, orgId, v.IikoCustomerId.Value, v.SelectedWalletId.Value, -diff, ct);

                _logger.LogInformation(
                    "iiko admin push: CustomerId={CustomerId}, orgId={OrgId}, WalletId={WalletId}, Diff={Diff}, Success={Success}",
                    customerId, orgId, v.SelectedWalletId, diff, ok);

                if (ok) pushed++;
                else errors[(v.IntegrationId, v.VenueId)] = "iiko balans yeniləmə alınmadı (topup/chargeoff).";
            }

            var fresh = await BuildStatusAsync(ctx, ct);
            foreach (var v in fresh.Venues)
                if (errors.TryGetValue((v.IntegrationId, v.VenueId), out var e)) v.Error = e;

            var msg = errors.Count > 0 && pushed == 0 ? "iiko-ya göndərilə bilmədi." : $"{pushed} org yeniləndi.";
            return new ServiceResult<PosCustomerIikoStatusDto>(errors.Count == 0 || pushed > 0, fresh, msg);
        }

        public async Task<ServiceResult<PosCustomerIikoStatusDto>> PullBalanceAsync(int tenantId, int customerId, IikoPullBalanceDto dto, CancellationToken ct = default)
        {
            var (ctx, error) = await LoadAsync(tenantId, customerId, dto.CardId, null, ct);
            if (ctx == null) return ServiceResult<PosCustomerIikoStatusDto>.Fail(error!);
            if (ctx.DefaultCard == null) return ServiceResult<PosCustomerIikoStatusDto>.Fail("Aktiv kart yoxdur.");

            var status = await BuildStatusAsync(ctx, ct);
            var v = status.Venues.FirstOrDefault(x => x.IntegrationId == dto.IntegrationId && x.VenueId == dto.VenueId);
            if (v == null) return ServiceResult<PosCustomerIikoStatusDto>.Fail("Venue tapılmadı.");
            if (v.Error != null) return ServiceResult<PosCustomerIikoStatusDto>.Fail(v.Error);
            if (!v.Found) return ServiceResult<PosCustomerIikoStatusDto>.Fail("Müştəri iiko-da tapılmadı.");
            if (v.IikoBalance == null) return ServiceResult<PosCustomerIikoStatusDto>.Fail("iiko wallet tapılmadı.");

            var card = await _uow.Repository<LoyaltyCard>().GetByIdAsync(ctx.DefaultCard.Id, ct);
            if (card == null) return ServiceResult<PosCustomerIikoStatusDto>.Fail("Kart tapılmadı.");

            var program = await _programRepo.GetByIdNameAsync(card.LoyaltyProgramId, ct);
            var templateType = program?.CardTemplate?.EffectiveType ?? CardTemplateType.PointsBased;
            var totalSteps = program?.CardTemplate?.TotalSteps ?? 10;

            var delta = Math.Round(v.IikoBalance.Value - card.CurrentPoints, 2);
            if (templateType == CardTemplateType.ProgressBased) delta = Math.Truncate(delta);
            if (delta == 0)
                return ServiceResult<PosCustomerIikoStatusDto>.Ok(status, "Balans artıq eynidir.");

            BalanceMath.ApplyDelta(card, new CardTemplateSnapshot { TemplateType = templateType, TotalSteps = totalSteps }, delta);
            if (delta > 0) card.LifetimePoints += delta;
            card.EditDate = DateTime.UtcNow;
            _uow.Repository<LoyaltyCard>().Update(card);

            await _uow.Repository<Transaction>().AddAsync(new Transaction
            {
                LoyaltyCardId = card.Id,
                Type = TransactionType.Adjusted,
                Status = RedeemStatus.Applied,
                Points = delta,
                BalanceAfter = card.CurrentPoints,
                Description = $"iiko balans sinxronu ({v.BranchName})",
                BranchId = v.BranchId,
                RegDate = DateTime.UtcNow,
                IsActive = true,
                LocalOccurredAt = DateTimeOffset.UtcNow,
                TimeZoneIdUsed = "UTC"
            });
            await _uow.CommitAsync(ct);

            try
            {
                BackgroundJob.Enqueue<IWalletPushJob>(j => j.RunAsync(card.Id, card.LoyaltyProgramId, delta));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "iiko admin pull: wallet push enqueue failed. CardId={CardId}", card.Id);
            }

            _logger.LogInformation(
                "iiko admin pull: CustomerId={CustomerId}, CardId={CardId}, orgId={OrgId}, Delta={Delta}, NewBalance={Balance}",
                customerId, card.Id, v.OrganizationId, delta, card.CurrentPoints);

            var (ctx2, _) = await LoadAsync(tenantId, customerId, dto.CardId, null, ct);
            var fresh = ctx2 != null ? await BuildStatusAsync(ctx2, ct) : status;
            return ServiceResult<PosCustomerIikoStatusDto>.Ok(fresh, $"Kart balansı {delta:+0.##;-0.##} dəyişdi.");
        }

        public async Task<ServiceResult<PosCustomerIikoStatusDto>> MergeByPhoneAsync(int tenantId, int customerId, IikoMergeByPhoneDto dto, CancellationToken ct = default)
        {
            var (ctx, error) = await LoadAsync(tenantId, customerId, dto.CardId, null, ct);
            if (ctx == null) return ServiceResult<PosCustomerIikoStatusDto>.Fail(error!);
            if (ctx.DefaultCard == null) return ServiceResult<PosCustomerIikoStatusDto>.Fail("Aktiv kart yoxdur.");
            if (TemplateOf(ctx, ctx.DefaultCard) == CardTemplateType.ProgressBased)
                return ServiceResult<PosCustomerIikoStatusDto>.Fail("Möhür kartına kassa balansı köçürülmür.");

            var status = await BuildStatusAsync(ctx, ct);
            var venues = status.Venues.Where(v => !dto.IntegrationId.HasValue || v.IntegrationId == dto.IntegrationId.Value).ToList();
            var candidates = venues.Where(v => v.Found && v.Error == null && v.IikoCustomerId != null && !v.MappingExists).ToList();
            var hasPending = venues.Any(v => v.LegacyPendingBalance > 0);
            if (candidates.Count == 0 && !hasPending)
            {
                return venues.Any(v => v.MappingExists)
                    ? ServiceResult<PosCustomerIikoStatusDto>.Fail("Müştəri artıq kassa qonağına bağlıdır — balans üçün Yüklə/Göndər istifadə edin.")
                    : ServiceResult<PosCustomerIikoStatusDto>.Fail("Müştəri kassada tapılmadı.");
            }

            // Bütün mapping-lər əvvəlcə yaradılır (köhnə balans tapılma anındakı dəyərlə) — import + push sonra
            var errors = new List<string>();
            foreach (var v in candidates)
            {
                var mapErr = await EnsureMappingAsync(v.IntegrationId, v.VenueId, v.IikoCustomerId!.Value, customerId, ct,
                    IikoLegacyBalance.Normalize(v.IikoBalance), v.SelectedWalletId?.ToString());
                if (mapErr != null) errors.Add($"{v.BranchName}: {mapErr}");
            }
            if (errors.Count > 0 && errors.Count == candidates.Count && !hasPending)
                return ServiceResult<PosCustomerIikoStatusDto>.Fail(string.Join(" ", errors));

            var added = await _legacy.ImportPendingAsync(tenantId, customerId, ct);
            _logger.LogInformation("iiko admin merge-by-phone: CustomerId={CustomerId}, Linked={Linked}, Added={Added}", customerId, candidates.Count - errors.Count, added);

            // Track-lər + iiko cüzdanı = Walvero (X+Y)
            var push = await PushBalanceAsync(tenantId, customerId, new IikoPushBalanceDto { IntegrationId = dto.IntegrationId, CardId = dto.CardId }, ct);
            var msg = $"Kassa balansı {added:0.##} Walvero-ya əlavə olundu.";
            if (errors.Count > 0) msg += " " + string.Join(" ", errors);
            if (!push.Success) msg += " Kassaya göndərmə: " + push.Message;
            return new ServiceResult<PosCustomerIikoStatusDto>(true, push.Data, msg);
        }

        public async Task<ServiceResult<IikoPhoneMatchPreviewDto>> PreviewPhoneMatchAsync(int tenantId, int integrationId, int skip, int take, CancellationToken ct = default)
        {
            var integration = (await _integrationRepo.GetAllActiveByTenantAsync(tenantId, ct) ?? new List<TenantIntegration>())
                .FirstOrDefault(i => i.Id == integrationId && i.IntegrationType?.Source == IikoSource);
            if (integration == null) return ServiceResult<IikoPhoneMatchPreviewDto>.Fail("Kassa inteqrasiyası tapılmadı.");
            var token = await GetTokenAsync(integration, ct);
            if (string.IsNullOrEmpty(token)) return ServiceResult<IikoPhoneMatchPreviewDto>.Fail("Kassa access token alınmadı.");
            var orgId = await FirstOrgAsync(integration, ct);
            if (orgId == null) return ServiceResult<IikoPhoneMatchPreviewDto>.Fail("Kassa org (venue) təyin edilməyib.");

            take = Math.Clamp(take, 1, 100);
            skip = Math.Max(0, skip);
            var query = UnlinkedCustomers(tenantId, integrationId);
            var dto = new IikoPhoneMatchPreviewDto
            {
                IntegrationId = integrationId,
                MatchExistingByPhone = integration.MatchExistingByPhone,
                Total = await query.CountAsync(ct),
                Skip = skip,
                Take = take
            };
            var customers = await query.OrderBy(c => c.Id).Skip(skip).Take(take).ToListAsync(ct);
            var ids = customers.Select(c => c.Id).ToList();
            var cards = await _db.Set<LoyaltyCard>().AsNoTracking()
                .Where(c => c.TenantId == tenantId && ids.Contains(c.CustomerId) && c.IsActive)
                .Select(c => new { c.Id, c.CustomerId, c.CurrentPoints })
                .ToListAsync(ct);
            var balances = cards.GroupBy(c => c.CustomerId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.Id).First().CurrentPoints);

            foreach (var c in customers)
            {
                var match = await _legacy.FindByPhoneAsync(integration, token, orgId.Value, c, ct);
                dto.Rows.Add(new IikoPhoneMatchRowDto
                {
                    CustomerId = c.Id,
                    Name = $"{c.FirstName} {c.LastName}".Trim(),
                    Phone = PhoneNormalizer.ToE164(c.PhoneNumber),
                    Found = match != null,
                    IikoCustomerId = match?.IikoCustomerId,
                    IikoBalance = match is { IsConflict: false } ? match.Balance : null,
                    OurBalance = balances.GetValueOrDefault(c.Id),
                    ConflictCustomerId = match?.ConflictCustomerId
                });
            }
            return ServiceResult<IikoPhoneMatchPreviewDto>.Ok(dto);
        }

        public async Task<int> RunPhoneMatchBulkAsync(int tenantId, int integrationId, CancellationToken ct = default)
        {
            var ids = await UnlinkedCustomers(tenantId, integrationId).OrderBy(c => c.Id).Select(c => c.Id).ToListAsync(ct);
            _logger.LogInformation("iiko phone match bulk START: TenantId={TenantId}, IntegrationId={IntegrationId}, Customers={Count}", tenantId, integrationId, ids.Count);
            var linked = 0;
            foreach (var id in ids)
            {
                try
                {
                    _db.ChangeTracker.Clear();
                    var customer = await _db.Set<Customer>().FirstOrDefaultAsync(c => c.Id == id, ct);
                    if (customer == null) continue;
                    await _posSync.SyncCustomerToPosAsync(tenantId, customer, ct);
                    var legacy = await _db.Set<PosCustomerMapping>().AsNoTracking()
                        .AnyAsync(m => m.TenantIntegrationId == integrationId && m.CustomerId == id && m.LegacyBalance != null, ct);
                    if (legacy) linked++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "iiko phone match bulk: customer failed. CustomerId={CustomerId}", id);
                }
            }
            _logger.LogInformation("iiko phone match bulk DONE: TenantId={TenantId}, IntegrationId={IntegrationId}, Customers={Count}, LinkedByPhone={Linked}",
                tenantId, integrationId, ids.Count, linked);
            return linked;
        }

        /// <summary>Sinxron HTTP sorğusunda emal olunan maksimum kart (hər kart üçün kassada axtarış edilir).</summary>
        private const int ProgramPullMaxCards = 300;

        public async Task<ServiceResult<IikoProgramPullResultDto>> PullProgramBalancesAsync(int tenantId, IikoProgramPullDto dto, CancellationToken ct = default)
        {
            if (!Guid.TryParse(dto.WalletId, out var walletId))
                return ServiceResult<IikoProgramPullResultDto>.Fail("Kassa cüzdanını seçin.");
            var program = await _db.Set<LoyaltyProgram>().AsNoTracking().Include(p => p.CardTemplate)
                .FirstOrDefaultAsync(p => p.Id == dto.ProgramId && p.TenantId == tenantId, ct);
            if (program == null) return ServiceResult<IikoProgramPullResultDto>.Fail("Proqram tapılmadı.");
            var templateType = program.CardTemplate?.EffectiveType ?? CardTemplateType.PointsBased;
            var totalSteps = Math.Max(1, program.CardTemplate?.TotalSteps ?? 10);

            var cards = await _db.Set<LoyaltyCard>().AsNoTracking()
                .Where(c => c.LoyaltyProgramId == program.Id && c.IsActive)
                .OrderBy(c => c.Id)
                .Select(c => new { c.Id, c.CustomerId, c.CardNumber })
                .ToListAsync(ct);
            if (cards.Count > ProgramPullMaxCards)
                return ServiceResult<IikoProgramPullResultDto>.Fail($"Proqramda {cards.Count} kart var — toplu yeniləmə {ProgramPullMaxCards} karta qədərdir.");

            var result = new IikoProgramPullResultDto { ProgramId = program.Id, WalletId = walletId.ToString(), Applied = dto.Apply, Total = cards.Count };
            foreach (var card in cards)
            {
                var row = new IikoProgramPullRowDto { CustomerId = card.CustomerId, CardId = card.Id, CardNumber = card.CardNumber };
                result.Rows.Add(row);
                try
                {
                    _db.ChangeTracker.Clear();
                    var (ctx, error) = await LoadAsync(tenantId, card.CustomerId, card.Id, null, ct);
                    if (ctx == null) { row.Note = error; continue; }
                    row.Name = $"{ctx.Customer.FirstName} {ctx.Customer.LastName}".Trim();
                    ctx.PreferredWalletId = walletId.ToString();
                    row.OurBalance = ctx.DefaultCard?.CurrentPoints ?? 0m;

                    // iiko qonağı org-lar arası ortaqdır — cüzdanı tapılan ilk venue kifayətdir
                    var status = await BuildStatusAsync(ctx, ct);
                    var v = status.Venues.FirstOrDefault(x => x.Found && x.SelectedWalletId == walletId && x.IikoBalance != null);
                    if (v == null)
                    {
                        result.NotFound++;
                        row.Note = status.Venues.Select(x => x.Error ?? x.LookupNote).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                                   ?? "Kassada tapılmadı.";
                        continue;
                    }

                    var target = Math.Max(0m, v.IikoBalance!.Value);
                    if (templateType == CardTemplateType.ProgressBased) target = Math.Truncate(target);
                    row.IikoBalance = target;
                    row.Delta = Math.Round(target - row.OurBalance, 2);
                    if (row.Delta == 0) continue;
                    result.Changed++;
                    if (dto.Apply)
                        await SetCardBalanceAsync(card.Id, templateType, totalSteps, target, $"Kassa balansından yeniləndi ({v.BranchName})", v.BranchId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "iiko program pull: card failed. ProgramId={ProgramId}, CardId={CardId}", program.Id, card.Id);
                    row.Note = "Xəta: " + ex.Message;
                }
            }

            _logger.LogInformation("iiko program pull: TenantId={TenantId}, ProgramId={ProgramId}, Wallet={Wallet}, Apply={Apply}, Total={Total}, Changed={Changed}, NotFound={NotFound}",
                tenantId, program.Id, walletId, dto.Apply, result.Total, result.Changed, result.NotFound);
            return ServiceResult<IikoProgramPullResultDto>.Ok(result);
        }

        /// <summary>
        /// Kart balansını mütləq dəyərə qoyur (Adjusted tx + pass push). Möhür kartında invariant saxlanır:
        /// CurrentPoints = CompletedCycles × addım + CurrentSteps.
        /// </summary>
        private async Task SetCardBalanceAsync(int cardId, CardTemplateType templateType, int totalSteps, decimal target,
            string description, int? branchId, CancellationToken ct)
        {
            var card = await _uow.Repository<LoyaltyCard>().GetByIdAsync(cardId, ct);
            if (card == null) return;
            var delta = Math.Round(target - card.CurrentPoints, 2);
            if (delta == 0) return;

            card.CurrentPoints = target;
            if (templateType == CardTemplateType.ProgressBased)
            {
                var abs = (int)target;
                card.CompletedCycles = abs / totalSteps;
                card.CurrentSteps = abs % totalSteps;
            }
            if (delta > 0) card.LifetimePoints += delta;
            card.EditDate = DateTime.UtcNow;
            _uow.Repository<LoyaltyCard>().Update(card);

            await _uow.Repository<Transaction>().AddAsync(new Transaction
            {
                LoyaltyCardId = card.Id,
                Type = TransactionType.Adjusted,
                Status = RedeemStatus.Applied,
                Points = delta,
                BalanceAfter = card.CurrentPoints,
                Description = description,
                BranchId = branchId,
                RegDate = DateTime.UtcNow,
                IsActive = true,
                LocalOccurredAt = DateTimeOffset.UtcNow,
                TimeZoneIdUsed = "UTC"
            });
            await _uow.CommitAsync(ct);

            try
            {
                BackgroundJob.Enqueue<IWalletPushJob>(j => j.RunAsync(card.Id, card.LoyaltyProgramId, delta));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "iiko program pull: wallet push enqueue failed. CardId={CardId}", card.Id);
            }
        }

        /// <summary>Bu inteqrasiyada mapping-i olmayan, telefonu olan aktiv müştərilər.</summary>
        private IQueryable<Customer> UnlinkedCustomers(int tenantId, int integrationId)
            => _db.Set<Customer>().AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.IsActive && c.PhoneNumber != null && c.PhoneNumber != "")
                .Where(c => !_db.Set<PosCustomerMapping>().Any(m => m.TenantIntegrationId == integrationId && m.CustomerId == c.Id));

        /// <summary>Axtarış üçün org: ilk aktiv venue, yoxdursa inteqrasiyanın öz ExternalVenueId-si (iiko qonağı org-lar arası ortaqdır).</summary>
        private async Task<Guid?> FirstOrgAsync(TenantIntegration integration, CancellationToken ct)
        {
            var venues = await _venueRepo.GetActiveByIntegrationAsync(integration.Id, ct);
            foreach (var v in venues)
                if (Guid.TryParse(v.ExternalVenueId, out var vOrg)) return vOrg;
            return Guid.TryParse(integration.ExternalVenueId, out var org) ? org : null;
        }

        // ── internals ──

        private async Task<(Ctx? Ctx, string? Error)> LoadAsync(int tenantId, int customerId, int? cardId, string? lookupCardTrack, CancellationToken ct)
        {
            var customer = await _uow.Repository<Customer>().GetByIdAsync(customerId, ct);
            if (customer == null || customer.TenantId != tenantId)
                return (null, "Müştəri tapılmadı.");

            var ctx = new Ctx
            {
                Customer = customer,
                Phone = PhoneNormalizer.ToE164(customer.PhoneNumber),
                LookupCardTrack = string.IsNullOrWhiteSpace(lookupCardTrack) ? null : lookupCardTrack.Trim()
            };
            ctx.Cards = (await _cardRepo.ListByCustomerAsync(tenantId, customerId, ct))?.ToList() ?? new List<LoyaltyCard>();

            var active = ctx.Cards.Where(c => c.IsActive).OrderByDescending(c => c.Id).ToList();
            if (cardId.HasValue) ctx.DefaultCard = ctx.Cards.FirstOrDefault(c => c.Id == cardId.Value);
            ctx.DefaultCard ??= active.FirstOrDefault(c => !string.IsNullOrEmpty(c.BarcodeValue)) ?? active.FirstOrDefault();
            if (ctx.DefaultCard != null)
                ctx.PreferredWalletId = await _db.Set<LoyaltyProgram>().AsNoTracking()
                    .Where(p => p.Id == ctx.DefaultCard.LoyaltyProgramId)
                    .Select(p => p.PosWalletId)
                    .FirstOrDefaultAsync(ct);

            if (customer.applicationUserId.HasValue)
            {
                ctx.WalveroId = await _db.Users
                    .Where(u => u.Id == customer.applicationUserId.Value)
                    .Select(u => (long?)u.WalveroId)
                    .FirstOrDefaultAsync(ct);
            }

            foreach (var programId in ctx.Cards.Select(c => c.LoyaltyProgramId).Distinct())
            {
                var program = await _programRepo.GetByIdNameAsync(programId, ct);
                ctx.TemplateTypes[programId] = program?.CardTemplate?.EffectiveType ?? CardTemplateType.PointsBased;
            }

            var integrations = await _integrationRepo.GetAllActiveByTenantAsync(tenantId, ct);
            ctx.Integrations = (integrations ?? new List<TenantIntegration>())
                .Where(i => i.IntegrationType?.Source == IikoSource)
                .ToList();
            foreach (var integration in ctx.Integrations)
                ctx.Tokens[integration.Id] = await GetTokenAsync(integration, ct);

            return (ctx, null);
        }

        private async Task<PosCustomerIikoStatusDto> BuildStatusAsync(Ctx ctx, CancellationToken ct)
        {
            var dto = new PosCustomerIikoStatusDto
            {
                CustomerId = ctx.Customer.Id,
                HasIikoIntegration = ctx.Integrations.Count > 0,
                OurCards = ctx.Cards.Where(c => c.IsActive).OrderByDescending(c => c.Id).Select(c => new IikoOurCardDto
                {
                    CardId = c.Id,
                    CardNumber = c.CardNumber,
                    Barcode = c.BarcodeValue,
                    CurrentPoints = c.CurrentPoints,
                    ProgramId = c.LoyaltyProgramId,
                    TemplateType = TemplateOf(ctx, c).ToString(),
                    IsDefault = c.Id == ctx.DefaultCard?.Id
                }).ToList()
            };

            foreach (var integration in ctx.Integrations)
            {
                var token = ctx.Tokens.GetValueOrDefault(integration.Id);
                var venues = await _venueRepo.GetActiveByIntegrationAsync(integration.Id, ct);
                if (venues.Count > 0)
                {
                    foreach (var venue in venues)
                        dto.Venues.Add(await ResolveVenueAsync(ctx, integration, venue, token, ct));
                }
                else if (!string.IsNullOrEmpty(integration.ExternalVenueId))
                {
                    dto.Venues.Add(await ResolveVenueAsync(ctx, integration, null, token, ct));
                }
            }

            return dto;
        }

        private async Task<IikoVenueStatusDto> ResolveVenueAsync(Ctx ctx, TenantIntegration integration, TenantIntegrationVenue? venue, string? token, CancellationToken ct)
        {
            var orgRaw = venue?.ExternalVenueId ?? integration.ExternalVenueId ?? string.Empty;
            var status = new IikoVenueStatusDto
            {
                IntegrationId = integration.Id,
                VenueId = venue?.Id,
                BranchId = venue?.BranchId,
                BranchName = venue?.Branch?.Name ?? venue?.DisplayName ?? integration.DisplayName ?? "iiko",
                OrganizationId = orgRaw,
                OurBalance = ctx.DefaultCard?.CurrentPoints ?? 0m
            };

            var mapping = venue != null
                ? await _mappingRepo.GetByVenueAndCustomerAsync(venue.Id, ctx.Customer.Id, ct)
                : null;
            // Legacy (venue-siz) mapping — iiko müştəri id-si org-lar arası paylaşıldığı üçün id ilə axtarışa yarayır
            mapping ??= await _mappingRepo.GetByTenantIntegrationAndCustomerAsync(integration.Id, ctx.Customer.Id, ct);
            status.MappingExists = mapping != null;
            status.ExternalCustomerId = mapping?.ExternalCustomerId;
            if (mapping != null && IikoLegacyBalance.IsPending(mapping.LegacyBalance, mapping.LegacyImportedAt))
                status.LegacyPendingBalance = mapping.LegacyBalance;

            if (!Guid.TryParse(orgRaw, out var orgId)) { status.Error = "ExternalVenueId GUID deyil."; return status; }
            if (string.IsNullOrEmpty(token)) { status.Error = "iiko access token alınmadı."; return status; }

            var lookups = new List<(string By, IikoCustomerLookup Type, string Value)>();
            if (mapping != null && Guid.TryParse(mapping.ExternalCustomerId, out var mappedId))
                lookups.Add(("id", IikoCustomerLookup.Id, mappedId.ToString()));
            if (!string.IsNullOrEmpty(ctx.DefaultCard?.BarcodeValue))
                lookups.Add(("barcode", IikoCustomerLookup.CardTrack, ctx.DefaultCard!.BarcodeValue!));
            if (ctx.WalveroId.HasValue)
                lookups.Add(("walveroId", IikoCustomerLookup.CardTrack, ctx.WalveroId.Value.ToString()));
            if (!string.IsNullOrEmpty(ctx.LookupCardTrack))
                lookups.Add(("cardTrack", IikoCustomerLookup.CardTrack, ctx.LookupCardTrack!));
            if (!string.IsNullOrEmpty(ctx.Phone))
                lookups.Add(("phone", IikoCustomerLookup.Phone, ctx.Phone!));

            string? lastNotFound = null;
            foreach (var (by, type, value) in lookups)
            {
                var r = await _iiko.FindCustomerAsync(token, orgId, type, value, ct);
                if (r.Success && r.Data != null)
                {
                    Fill(status, r.Data, by);
                    break;
                }
                if (r.IsNotFound) { lastNotFound = r.ErrorDescription; continue; }
                status.Error = r.ErrorDescription ?? $"iiko HTTP {r.StatusCode}";
                break;
            }
            if (!status.Found && status.Error == null)
                status.LookupNote = lastNotFound;

            if (status.Found)
                await SelectWalletAsync(ctx, integration, venue, orgId, token, status, ct);

            return status;
        }

        private static void Fill(IikoVenueStatusDto status, IikoCustomerInfoResponse d, string foundBy)
        {
            status.Found = true;
            status.FoundBy = foundBy;
            status.IikoCustomerId = d.Id;
            status.IikoName = $"{d.Name} {d.Surname}".Trim();
            status.IikoPhone = d.Phone;
            status.IikoCardTracks = d.Cards?
                .Select(c => c.Track ?? c.Number ?? string.Empty)
                .Where(s => s.Length > 0)
                .ToList() ?? new List<string>();
            status.Wallets = d.WalletBalances?
                .Select(w => new IikoWalletDto { Id = w.Id, Name = w.Name, Type = w.Type, Balance = w.Balance })
                .ToList() ?? new List<IikoWalletDto>();
            status.IikoCategories = d.Categories?
                .Where(c => c.IsActive != false)
                .Select(c => string.IsNullOrWhiteSpace(c.Name) ? c.Id.ToString() : c.Name!)
                .ToList() ?? new List<string>();
        }

        private async Task SelectWalletAsync(Ctx ctx, TenantIntegration integration, TenantIntegrationVenue? venue, Guid orgId, string token, IikoVenueStatusDto status, CancellationToken ct)
        {
            if (status.Wallets.Count == 0) return;

            IikoWalletDto? selected = null;
            if (Guid.TryParse(ctx.PreferredWalletId, out var preferredWallet))
            {
                // Proqramın öz cüzdanı (pims: Ambassador) — tapılmasa başqa cüzdana düşmə, admin görsün
                selected = status.Wallets.FirstOrDefault(w => w.Id == preferredWallet);
                if (selected == null)
                {
                    var note = $"Proqramın iiko cüzdanı ({preferredWallet}) bu müştəridə yoxdur — müştəri həmin iiko proqramına yazılmayıb";
                    status.LookupNote = string.IsNullOrEmpty(status.LookupNote) ? note : status.LookupNote + " | " + note;
                    return;
                }
            }
            var programIdStr = venue?.ExternalProgramId ?? integration.ExternalProgramId;
            if (selected == null && Guid.TryParse(programIdStr, out var programId))
            {
                if (!ctx.ProgramsByOrg.TryGetValue(orgId, out var programs))
                {
                    programs = await _iiko.GetLoyaltyProgramsAsync(token, orgId, ct);
                    ctx.ProgramsByOrg[orgId] = programs;
                }
                if (programs == null)
                {
                    // iiko "Transport_WrongCrmId / Organization not found": org iikoCard (loyalty) ilə bağlı deyil.
                    // Wallet seçimi ilk wallet-ə düşür — admin bunu görsün.
                    var note = $"iiko loyalty proqramları alınmadı (org {orgId} iikoCard-a bağlı deyil?) — ilk wallet seçildi";
                    status.LookupNote = string.IsNullOrEmpty(status.LookupNote) ? note : status.LookupNote + " | " + note;
                }
                var walletId = programs?.FirstOrDefault(p => p.Id == programId)?.WalletId;
                if (walletId.HasValue)
                    selected = status.Wallets.FirstOrDefault(w => w.Id == walletId.Value);
            }
            selected ??= status.Wallets.First();

            status.SelectedWalletId = selected.Id;
            status.IikoBalance = selected.Balance;
            status.Diff = Math.Round(status.OurBalance - selected.Balance, 2);
        }

        private async Task<string?> EnsureMappingAsync(int integrationId, int? venueId, Guid iikoCustomerId, int customerId, CancellationToken ct,
            decimal? legacyBalance = null, string? legacyWalletId = null)
        {
            var mapping = new PosCustomerMapping
            {
                TenantIntegrationId = integrationId,
                TenantIntegrationVenueId = venueId,
                ExternalCustomerId = iikoCustomerId.ToString(),
                CustomerId = customerId,
                IsEnrolledInProgram = false,
                LegacyBalance = legacyBalance,
                LegacyWalletId = legacyWalletId,
                RegUserId = 1,
                RegDate = DateTime.UtcNow,
                IsActive = true
            };
            try
            {
                await _mappingRepo.AddAsync(mapping);
                await _uow.CommitAsync(ct);
                return null;
            }
            catch (DbUpdateException ex)
            {
                // Eyni iiko id başqa müştəriyə bağlıdır (unique index) — push etmirik
                _db.Entry(mapping).State = EntityState.Detached;
                _logger.LogWarning(ex, "iiko admin sync: mapping yaradıla bilmədi. CustomerId={CustomerId}, iikoId={IikoId}", customerId, iikoCustomerId);
                return "Bu iiko müştərisi başqa Walvero müştərisinə bağlıdır.";
            }
        }

        private async Task<string?> GetTokenAsync(TenantIntegration integration, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(integration.ApiTokenEncrypted)) return null;
            var apiLogin = _protector.Decrypt(integration.ApiTokenEncrypted);
            if (string.IsNullOrEmpty(apiLogin)) return null;
            var clientSecret = string.IsNullOrEmpty(integration.ApiSecretEncrypted) ? null : _protector.Decrypt(integration.ApiSecretEncrypted);
            return await _iiko.GetAccessTokenAsync(apiLogin, integration.ApiAppId, clientSecret, ct: ct);
        }

        private static CardTemplateType TemplateOf(Ctx ctx, LoyaltyCard card)
            => ctx.TemplateTypes.TryGetValue(card.LoyaltyProgramId, out var t) ? t : CardTemplateType.PointsBased;
    }
}
