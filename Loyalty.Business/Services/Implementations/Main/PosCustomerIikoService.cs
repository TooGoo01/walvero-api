using Hangfire;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
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
            public List<TenantIntegration> Integrations = new();
            public Dictionary<int, string?> Tokens = new();
            public Dictionary<int, CardTemplateType> TemplateTypes = new();
            public Dictionary<Guid, List<IikoLoyaltyProgram>?> ProgramsByOrg = new();
        }

        public async Task<ServiceResult<PosCustomerIikoStatusDto>> GetStatusAsync(int tenantId, int customerId, int? cardId, CancellationToken ct = default)
        {
            var (ctx, error) = await LoadAsync(tenantId, customerId, cardId, ct);
            if (ctx == null) return ServiceResult<PosCustomerIikoStatusDto>.Fail(error!);
            return ServiceResult<PosCustomerIikoStatusDto>.Ok(await BuildStatusAsync(ctx, ct));
        }

        public async Task<ServiceResult<PosCustomerIikoStatusDto>> PushBalanceAsync(int tenantId, int customerId, IikoPushBalanceDto dto, CancellationToken ct = default)
        {
            var (ctx, error) = await LoadAsync(tenantId, customerId, dto.CardId, ct);
            if (ctx == null) return ServiceResult<PosCustomerIikoStatusDto>.Fail(error!);
            if (ctx.DefaultCard == null) return ServiceResult<PosCustomerIikoStatusDto>.Fail("Aktiv kart yoxdur.");

            var status = await BuildStatusAsync(ctx, ct);
            var isProgress = TemplateOf(ctx, ctx.DefaultCard) == CardTemplateType.ProgressBased;
            var errors = new Dictionary<(int, int?), string>();
            var pushed = 0;
            // iiko wallet org-lar arası paylaşıla bilər (eyni müştəri id + eyni wallet) — bir dəfə düzəlt
            var touchedWallets = new HashSet<(Guid, Guid)>();

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
            var (ctx, error) = await LoadAsync(tenantId, customerId, dto.CardId, ct);
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

            var (ctx2, _) = await LoadAsync(tenantId, customerId, dto.CardId, ct);
            var fresh = ctx2 != null ? await BuildStatusAsync(ctx2, ct) : status;
            return ServiceResult<PosCustomerIikoStatusDto>.Ok(fresh, $"Kart balansı {delta:+0.##;-0.##} dəyişdi.");
        }

        // ── internals ──

        private async Task<(Ctx? Ctx, string? Error)> LoadAsync(int tenantId, int customerId, int? cardId, CancellationToken ct)
        {
            var customer = await _uow.Repository<Customer>().GetByIdAsync(customerId, ct);
            if (customer == null || customer.TenantId != tenantId)
                return (null, "Müştəri tapılmadı.");

            var ctx = new Ctx { Customer = customer, Phone = NormalizePhone(customer.PhoneNumber) };
            ctx.Cards = (await _cardRepo.ListByCustomerAsync(tenantId, customerId, ct))?.ToList() ?? new List<LoyaltyCard>();

            var active = ctx.Cards.Where(c => c.IsActive).OrderByDescending(c => c.Id).ToList();
            if (cardId.HasValue) ctx.DefaultCard = ctx.Cards.FirstOrDefault(c => c.Id == cardId.Value);
            ctx.DefaultCard ??= active.FirstOrDefault(c => !string.IsNullOrEmpty(c.BarcodeValue)) ?? active.FirstOrDefault();

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

            if (!Guid.TryParse(orgRaw, out var orgId)) { status.Error = "ExternalVenueId GUID deyil."; return status; }
            if (string.IsNullOrEmpty(token)) { status.Error = "iiko access token alınmadı."; return status; }

            var lookups = new List<(string By, IikoCustomerLookup Type, string Value)>();
            if (mapping != null && Guid.TryParse(mapping.ExternalCustomerId, out var mappedId))
                lookups.Add(("id", IikoCustomerLookup.Id, mappedId.ToString()));
            if (!string.IsNullOrEmpty(ctx.DefaultCard?.BarcodeValue))
                lookups.Add(("barcode", IikoCustomerLookup.CardTrack, ctx.DefaultCard!.BarcodeValue!));
            if (ctx.WalveroId.HasValue)
                lookups.Add(("walveroId", IikoCustomerLookup.CardTrack, ctx.WalveroId.Value.ToString()));
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
        }

        private async Task SelectWalletAsync(Ctx ctx, TenantIntegration integration, TenantIntegrationVenue? venue, Guid orgId, string token, IikoVenueStatusDto status, CancellationToken ct)
        {
            if (status.Wallets.Count == 0) return;

            IikoWalletDto? selected = null;
            var programIdStr = venue?.ExternalProgramId ?? integration.ExternalProgramId;
            if (Guid.TryParse(programIdStr, out var programId))
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

        private async Task<string?> EnsureMappingAsync(int integrationId, int? venueId, Guid iikoCustomerId, int customerId, CancellationToken ct)
        {
            var mapping = new PosCustomerMapping
            {
                TenantIntegrationId = integrationId,
                TenantIntegrationVenueId = venueId,
                ExternalCustomerId = iikoCustomerId.ToString(),
                CustomerId = customerId,
                IsEnrolledInProgram = false,
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

        /// <summary>iiko telefonu E.164 gözləyir: "0501234567" / "994501234567" / "501234567" → "+994501234567".</summary>
        private static string? NormalizePhone(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var digits = new string(raw.Where(char.IsDigit).ToArray());
            if (digits.Length == 0) return null;
            if (digits.StartsWith("994") && digits.Length == 12) return "+" + digits;
            if (digits.StartsWith("0") && digits.Length == 10) return "+994" + digits[1..];
            if (digits.Length == 9) return "+994" + digits;
            return "+" + digits;
        }
    }
}
