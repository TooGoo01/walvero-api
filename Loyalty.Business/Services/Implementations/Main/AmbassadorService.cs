using Hangfire;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Sequences;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Entities.Users;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Loyalty.Infrastructure.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class AmbassadorService : IAmbassadorService
    {
        private readonly ICustomerRepository _customerRepo;
        private readonly ILoyaltyProgramRepository _programRepo;
        private readonly ILoyaltyCardRepository _cardRepo;
        private readonly ICardNumberGenerator _cardNumberGenerator;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUnitOfWork _uow;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly LoyaltyWalletDbContext _db;
        private readonly IPointsExpiryService _pointsExpiry;
        private readonly ITimeZoneService _timeZoneService;
        private readonly ILogger<AmbassadorService> _logger;

        public AmbassadorService(
            ICustomerRepository customerRepo,
            ILoyaltyProgramRepository programRepo,
            ILoyaltyCardRepository cardRepo,
            ICardNumberGenerator cardNumberGenerator,
            UserManager<ApplicationUser> userManager,
            IUnitOfWork uow,
            IServiceScopeFactory scopeFactory,
            LoyaltyWalletDbContext db,
            IPointsExpiryService pointsExpiry,
            ITimeZoneService timeZoneService,
            ILogger<AmbassadorService> logger)
        {
            _timeZoneService = timeZoneService;
            _customerRepo = customerRepo;
            _programRepo = programRepo;
            _cardRepo = cardRepo;
            _cardNumberGenerator = cardNumberGenerator;
            _userManager = userManager;
            _uow = uow;
            _scopeFactory = scopeFactory;
            _db = db;
            _pointsExpiry = pointsExpiry;
            _logger = logger;
        }

        public async Task<ServiceResult> CreateAmbassadorAsync(int tenantId, string userId, CreateAmbassadorDto dto, CancellationToken ct = default)
        {
            if (tenantId <= 0)
                return new ServiceResult(false, "Tenant not resolved.");
            if (dto == null || string.IsNullOrWhiteSpace(dto.PhoneNumber))
                return new ServiceResult(false, "Phone number is required.");
            if (dto.ExpiresAt.HasValue && dto.ExpiresAt.Value.ToUniversalTime() <= DateTime.UtcNow)
                return new ServiceResult(false, "Son istifadə tarixi gələcəkdə olmalıdır.");

            // 1) Tenant-də ambassador aktivdirmi?
            var tenant = await _db.Set<Tenant>().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
            if (tenant == null)
                return new ServiceResult(false, "Tenant not found.");
            if (!tenant.EnableAmbassadors)
                return new ServiceResult(false, "Ambassador proqramı bu tenant üçün aktiv deyil.");

            // 2) Proqram: tenant-da Ambassador proqramı varsa həmişə o (ayrı dizayn/mexanika, bir aktiv kart);
            //    yoxdursa köhnə rejim — kart göndərilən proqramda, keçid yoxdur.
            var ambassadorProgram = await _programRepo.GetAmbassadorProgramAsync(tenantId, ct);
            var program = await _programRepo.GetByIdNameAsync(ambassadorProgram?.Id ?? dto.LoyaltyProgramId, ct);
            if (program is null || program.TenantId != tenantId)
                return new ServiceResult(false, "Program not found for this tenant.");
            var segmentMode = ambassadorProgram != null;

            var phone = dto.PhoneNumber.Trim();
            var regUserId = int.TryParse(userId, out var uid) ? uid : 1;

            // Telefonla mövcud app user-i tap (kart avtomatik app-da görünsün deyə)
            var phoneDigits = new string(phone.Where(char.IsDigit).ToArray());
            var existingAppUser = await _userManager.Users.FirstOrDefaultAsync(u =>
                u.PhoneNumber == phone || u.PhoneNumber == phoneDigits || u.PhoneNumber == "+" + phoneDigits, ct);

            // 3) Customer tap / yarat
            var customer = await _customerRepo.GetByPhoneAsync(tenantId, phone, ct);
            if (customer is null)
            {
                var cardNumber = !string.IsNullOrWhiteSpace(dto.CardNumber)
                    ? dto.CardNumber!.Trim()
                    : await _cardNumberGenerator.GenerateCardNumberAsync(tenantId, ct);

                customer = new Customer
                {
                    TenantId = tenantId,
                    FirstName = dto.FirstName?.Trim() ?? "",
                    LastName = dto.LastName?.Trim() ?? "",
                    PhoneNumber = phone,
                    Email = dto.Email?.Trim(),
                    IsActive = true,
                    IsAmbassador = true,
                    LastActivityDate = DateTime.UtcNow,
                    CardNumber = cardNumber,
                    applicationUserId = existingAppUser?.Id,
                    RegUserId = regUserId,
                    RegDate = DateTime.UtcNow
                };

                await _uow.Repository<Customer>().AddAsync(customer);
                try
                {
                    await _uow.CommitAsync(ct);
                }
                catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
                {
                    // CardNumber toqquşması — yalnız avtomatik generasiyada təkrar cəhd
                    if (string.IsNullOrWhiteSpace(dto.CardNumber))
                    {
                        customer.CardNumber = await _cardNumberGenerator.GenerateCardNumberAsync(tenantId, ct);
                        await _uow.CommitAsync(ct);
                    }
                    else
                    {
                        return new ServiceResult(false, "Bu kart nömrəsi artıq istifadə olunur.");
                    }
                }
            }
            else
            {
                // Mövcud müştəri — ambassador kimi işarələ + məlumatları yenilə
                customer.IsAmbassador = true;
                if (!string.IsNullOrWhiteSpace(dto.FirstName)) customer.FirstName = dto.FirstName.Trim();
                if (!string.IsNullOrWhiteSpace(dto.LastName)) customer.LastName = dto.LastName!.Trim();
                if (!string.IsNullOrWhiteSpace(dto.Email)) customer.Email = dto.Email!.Trim();
                if (!customer.IsActive) customer.IsActive = true;
                if (!customer.applicationUserId.HasValue && existingAppUser != null)
                    customer.applicationUserId = existingAppUser.Id;
                if (!string.IsNullOrWhiteSpace(dto.CardNumber) && string.IsNullOrWhiteSpace(customer.CardNumber))
                    customer.CardNumber = dto.CardNumber!.Trim();
                customer.LastActivityDate = DateTime.UtcNow;
                _uow.Repository<Customer>().Update(customer);
                await _uow.CommitAsync(ct);
            }

            // 4) Kart: seqment rejimində adi kart bağlanır, ambassador kartı açılır (bir aktiv kart);
            //    köhnə rejimdə kart tap / yarat (BarcodeValue boş → SaveChanges Customer.CardNumber-dən doldurur)
            LoyaltyCard? card;
            if (segmentMode)
            {
                var switched = await SwitchActiveCardAsync(customer, program, dto.CarryOverBalance, regUserId, "ambassador edildi", ct);
                if (!switched.Success) return new ServiceResult(false, switched.Error);
                card = switched.Card!;
            }
            else
                card = await _cardRepo.GetLastActiveByCustomerAsync(tenantId, program.Id, customer.Id, ct);
            if (card is null)
            {
                var deactive = await _cardRepo.GetLastInactiveByCustomerAsync(tenantId, program.Id, customer.Id, ct);
                if (deactive != null)
                {
                    deactive.IsActive = true;
                    deactive.Status = CardStatus.Active;
                    deactive.ActivatedAt = DateTime.UtcNow;
                    deactive.EditDate = DateTime.UtcNow;
                    deactive.EditUserId = regUserId;
                    _uow.Repository<LoyaltyCard>().Update(deactive);
                    await _uow.CommitAsync(ct);
                    card = deactive;
                }
                else
                {
                    card = new LoyaltyCard
                    {
                        TenantId = tenantId,
                        CustomerId = customer.Id,
                        LoyaltyProgramId = program.Id,
                        BarcodeType = BarcodeType.Code128,
                        Status = CardStatus.Active,
                        ActivatedAt = DateTime.UtcNow,
                        RegUserId = regUserId,
                        RegDate = DateTime.UtcNow,
                        IsActive = true
                    };
                    await _uow.Repository<LoyaltyCard>().AddAsync(card);
                    await _uow.CommitAsync(ct);
                }
            }

            // 5) POS-a müştəri sync (iiko customer create + program enroll) — isolated scope, best-effort
            await RunPosCustomerSyncSafeAsync(tenantId, customer.Id, ct);
            // iiko ambassador qonaq kateqoriyası (inteqrasiyada təyin edilibsə)
            await RunAmbassadorCategorySyncSafeAsync(tenantId, customer.Id, true, ct);

            // 6) İlkin xal (varsa) + bitmə tarixi
            if (dto.InitialPoints.HasValue && dto.InitialPoints.Value != 0)
                await CreditPointsInternalAsync(card, program, customer.Id, dto.InitialPoints.Value, "Ambassador ilkin xal", dto.ExpiresAt, ct);
            else if (dto.ExpiresAt.HasValue)
            {
                card.ExpiresAt = PointsExpiry.ResolveOnCredit(card.ExpiresAt, dto.ExpiresAt, DateTime.UtcNow);
                card.EditDate = DateTime.UtcNow;
                _uow.Repository<LoyaltyCard>().Update(card);
                await _uow.CommitAsync(ct);
            }

            // 7) Ambassador-a məlumat SMS-i (best-effort)
            TrySendAmbassadorSms(phone, tenant.CompanyName);

            return new ServiceResult(true, new { customerId = customer.Id, cardId = card.Id });
        }

        public async Task<ServiceResult> PromoteExistingAsync(int tenantId, string userId, PromoteAmbassadorDto dto, CancellationToken ct = default)
        {
            if (tenantId <= 0)
                return new ServiceResult(false, "Tenant not resolved.");
            if (dto == null || dto.CustomerId <= 0)
                return new ServiceResult(false, "Customer is required.");

            var customer = await _db.Set<Customer>()
                .FirstOrDefaultAsync(c => c.Id == dto.CustomerId && c.TenantId == tenantId, ct);
            if (customer is null)
                return new ServiceResult(false, "Müştəri tapılmadı.");

            // Proqram resolve yalnız köhnə rejimdə (Ambassador proqramı varsa Create onu özü seçir):
            // verilən → müştərinin aktiv kart proqramı → tenant-in ilk aktiv proqramı
            int programId = 0;
            if (await _programRepo.GetAmbassadorProgramAsync(tenantId, ct) != null)
            {
                // seqment rejimi — programId ignor edilir
            }
            else if (dto.LoyaltyProgramId.HasValue && dto.LoyaltyProgramId.Value > 0)
            {
                var p = await _programRepo.GetByIdNameAsync(dto.LoyaltyProgramId.Value, ct);
                if (p is null || p.TenantId != tenantId)
                    return new ServiceResult(false, "Program not found for this tenant.");
                programId = p.Id;
            }
            else
            {
                var cardProgramId = await _db.Set<LoyaltyCard>()
                    .Where(c => c.CustomerId == customer.Id && c.TenantId == tenantId && c.IsActive)
                    .OrderByDescending(c => c.Id)
                    .Select(c => (int?)c.LoyaltyProgramId)
                    .FirstOrDefaultAsync(ct);

                if (cardProgramId.HasValue)
                    programId = cardProgramId.Value;
                else
                {
                    var first = (await _programRepo.GetActiveByTenantAsync(tenantId, ProgramAudience.Public, ct)).FirstOrDefault();
                    if (first is null)
                        return new ServiceResult(false, "Tenant-də aktiv proqram yoxdur.");
                    programId = first.Id;
                }
            }

            // Mövcud Create axını promote + kart + xal + POS + SMS məntiqini onsuz da edir (telefonla tapır)
            var createDto = new CreateAmbassadorDto
            {
                LoyaltyProgramId = programId,
                FirstName = customer.FirstName ?? "",
                LastName = customer.LastName,
                PhoneNumber = customer.PhoneNumber,
                Email = customer.Email,
                CardNumber = customer.CardNumber,
                InitialPoints = dto.InitialPoints,
                ExpiresAt = dto.ExpiresAt,
                CarryOverBalance = dto.CarryOverBalance
            };

            return await CreateAmbassadorAsync(tenantId, userId, createDto, ct);
        }

        public async Task<ServiceResult> DemoteAsync(int tenantId, string userId, DemoteAmbassadorDto dto, CancellationToken ct = default)
        {
            if (tenantId <= 0)
                return new ServiceResult(false, "Tenant not resolved.");
            if (dto == null || dto.CustomerId <= 0)
                return new ServiceResult(false, "Customer is required.");

            var customer = await _db.Set<Customer>()
                .FirstOrDefaultAsync(c => c.Id == dto.CustomerId && c.TenantId == tenantId, ct);
            if (customer is null)
                return new ServiceResult(false, "Müştəri tapılmadı.");
            if (!customer.IsAmbassador)
                return new ServiceResult(false, "Müştəri ambassador deyil.");

            var regUserId = int.TryParse(userId, out var uid) ? uid : 1;

            // Aktiv kart ambassador proqramındadırsa → adi proqrama keçid; köhnə rejimdə yalnız işarə götürülür
            var active = await _cardRepo.GetActiveByCustomerAsync(tenantId, customer.Id, ct);
            var activeProgram = active == null ? null : await _programRepo.GetByIdNameAsync(active.LoyaltyProgramId, ct);
            LoyaltyCard? resultCard = active;
            if (activeProgram?.Audience == ProgramAudience.Ambassador)
            {
                // Hədəf: müştərinin son bağlanmış Public kartının proqramı → tenant-ın default Public proqramı
                var publicPrograms = await _programRepo.GetActiveByTenantAsync(tenantId, ProgramAudience.Public, ct);
                var publicIds = publicPrograms.Select(p => p.Id).ToList();
                var lastPublicProgramId = await _db.Set<LoyaltyCard>()
                    .Where(c => c.TenantId == tenantId && c.CustomerId == customer.Id && !c.IsActive && publicIds.Contains(c.LoyaltyProgramId))
                    .OrderByDescending(c => c.Id)
                    .Select(c => (int?)c.LoyaltyProgramId)
                    .FirstOrDefaultAsync(ct);
                var targetId = lastPublicProgramId ?? publicPrograms.FirstOrDefault()?.Id;
                if (targetId == null)
                    return new ServiceResult(false, "Tenant-da aktiv adi proqram yoxdur — ambassador kartı açıq qaldı.");
                var target = await _programRepo.GetByIdNameAsync(targetId.Value, ct);

                var switched = await SwitchActiveCardAsync(customer, target!, dto.CarryOverBalance, regUserId, "ambassadorluqdan çıxarıldı", ct);
                if (!switched.Success) return new ServiceResult(false, switched.Error);
                resultCard = switched.Card;
            }

            customer.IsAmbassador = false;
            customer.EditDate = DateTime.UtcNow;
            _uow.Repository<Customer>().Update(customer);
            await _uow.CommitAsync(ct);

            await RunAmbassadorCategorySyncSafeAsync(tenantId, customer.Id, false, ct);

            return new ServiceResult(true, new { customerId = customer.Id, cardId = resultCard?.Id });
        }

        private sealed record SwitchResult(bool Success, LoyaltyCard? Card, string? Error);

        /// <summary>
        /// Bir müştəri = bir aktiv kart: müştərinin tenant-dakı digər aktiv kartlarını bağlayır (Cancelled, balans dondurulur)
        /// və hədəf proqramda kartı açır (aktiv varsa onu, bağlı varsa reaktiv, yoxdursa yeni — eyni barkodla).
        /// carryOver: bağlanan kartların pul balansı hədəf karta köçürülür (yalnız hər iki tərəf xal/cashback kartıdırsa).
        /// </summary>
        private async Task<SwitchResult> SwitchActiveCardAsync(
            Customer customer, LoyaltyProgram target, bool carryOver, int regUserId, string reason, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var tenantId = customer.TenantId;
            var activeCards = await _db.Set<LoyaltyCard>()
                .Where(c => c.TenantId == tenantId && c.CustomerId == customer.Id && c.IsActive)
                .OrderByDescending(c => c.Id)
                .ToListAsync(ct);

            var targetCard = activeCards.FirstOrDefault(c => c.LoyaltyProgramId == target.Id);
            var toClose = activeCards.Where(c => c.Id != targetCard?.Id).ToList();

            // Köçürmə yoxlaması — heç nə dəyişməzdən əvvəl
            var targetType = target.CardTemplate?.EffectiveType ?? CardTemplateType.PointsBased;
            var carryCards = new List<LoyaltyCard>();
            if (carryOver)
            {
                var closeProgramIds = toClose.Select(c => c.LoyaltyProgramId).Distinct().ToList();
                var closePrograms = await _db.Set<LoyaltyProgram>().AsNoTracking()
                    .Include(p => p.CardTemplate)
                    .Where(p => closeProgramIds.Contains(p.Id))
                    .ToListAsync(ct);
                foreach (var c in toClose.Where(c => c.CurrentPoints > 0))
                {
                    var fromType = closePrograms.FirstOrDefault(p => p.Id == c.LoyaltyProgramId)?.CardTemplate?.EffectiveType ?? CardTemplateType.PointsBased;
                    if (!AmbassadorCardSwitch.CanCarryOver(fromType, targetType))
                        return new SwitchResult(false, null, "Balans köçürülməsi hədiyyə (möhür) kartı ilə mümkün deyil — köçürmə seçimini söndürün.");
                    carryCards.Add(c);
                }
            }

            // 1) Köhnə aktiv kartları bağla (balans kartda qalır — geri açılanda qayıdır)
            foreach (var old in toClose)
            {
                old.IsActive = false;
                old.Status = CardStatus.Cancelled;
                old.EditDate = now;
                old.EditUserId = regUserId;
                _uow.Repository<LoyaltyCard>().Update(old);
                _uow.Repository<Transaction>().Add(NewMarkerTx(old.Id, 0, old.CurrentPoints, $"Kart bağlandı: {reason}", now));
            }

            // 2) Hədəf kart: aktiv → bağlı (reaktiv) → yeni (eyni barkod, unikal CardNumber)
            var reactivated = false;
            if (targetCard == null)
            {
                targetCard = await _db.Set<LoyaltyCard>()
                    .Where(c => c.TenantId == tenantId && c.CustomerId == customer.Id && c.LoyaltyProgramId == target.Id && !c.IsActive)
                    .OrderByDescending(c => c.Id)
                    .FirstOrDefaultAsync(ct);
                if (targetCard != null)
                {
                    targetCard.IsActive = true;
                    targetCard.Status = CardStatus.Active;
                    targetCard.ActivatedAt = now;
                    targetCard.EditDate = now;
                    targetCard.EditUserId = regUserId;
                    _uow.Repository<LoyaltyCard>().Update(targetCard);
                    _uow.Repository<Transaction>().Add(NewMarkerTx(targetCard.Id, 0, targetCard.CurrentPoints, $"Kart yenidən açıldı: {reason}", now));
                    reactivated = true;
                }
                else
                {
                    var barcode = toClose.Select(c => c.BarcodeValue).FirstOrDefault(b => !string.IsNullOrWhiteSpace(b))
                                  ?? customer.CardNumber;
                    var baseNumber = !string.IsNullOrWhiteSpace(customer.CardNumber) ? customer.CardNumber : barcode;
                    string? cardNumber = null;
                    if (!string.IsNullOrWhiteSpace(baseNumber))
                    {
                        var suffix = target.Audience == ProgramAudience.Ambassador ? "A" : "P";
                        for (var i = 0; i < 50 && cardNumber == null; i++)
                        {
                            var candidate = AmbassadorCardSwitch.CardNumberCandidate(baseNumber, suffix, i);
                            if (!await _db.Set<LoyaltyCard>().AnyAsync(c => c.CardNumber == candidate, ct))
                                cardNumber = candidate;
                        }
                    }
                    targetCard = new LoyaltyCard
                    {
                        TenantId = tenantId,
                        CustomerId = customer.Id,
                        LoyaltyProgramId = target.Id,
                        // CardNumber qlobal unique — açıq təyin (SaveChanges toqquşmanı yalnız tracked kartlarda yoxlayır);
                        // boş qalarsa SaveChanges generasiya edir. Barkod köhnə kartla eyni (çap olunmuş kart, POS cardTrack).
                        CardNumber = cardNumber ?? string.Empty,
                        BarcodeValue = barcode,
                        BarcodeType = BarcodeType.Code128,
                        Status = CardStatus.Active,
                        ActivatedAt = now,
                        RegUserId = regUserId,
                        RegDate = now,
                        IsActive = true
                    };
                    await _uow.Repository<LoyaltyCard>().AddAsync(targetCard);
                }
            }
            await _uow.CommitAsync(ct);

            // 3) Bağlanan kartların pass-ları yenilənir (inaktiv kart → Apple voided / Google INACTIVE) — background
            foreach (var old in toClose)
            {
                var oldId = old.Id;
                var oldProgramId = old.LoyaltyProgramId;
                try { BackgroundJob.Enqueue<IWalletPushJob>(j => j.RunAsync(oldId, oldProgramId, 0)); }
                catch (Exception ex) { _logger.LogWarning(ex, "Closed card wallet push enqueue failed. CardId={CardId}", oldId); }
            }
            if (reactivated)
            {
                var reId = targetCard.Id;
                var reProgramId = targetCard.LoyaltyProgramId;
                try { BackgroundJob.Enqueue<IWalletPushJob>(j => j.ReactivateAsync(reId, reProgramId)); }
                catch (Exception ex) { _logger.LogWarning(ex, "Reactivated card wallet push enqueue failed. CardId={CardId}", reId); }
            }

            // 4) Balans köçürməsi: köhnədən çıx (Adjusted), yeniyə kredit (wallet push + POS sync CreditPointsInternalAsync-də)
            foreach (var from in carryCards)
            {
                var amount = from.CurrentPoints;
                from.CurrentPoints = 0;
                from.EditDate = now;
                _uow.Repository<LoyaltyCard>().Update(from);
                _uow.Repository<Transaction>().Add(NewMarkerTx(from.Id, -amount, 0, $"Balans köçürüldü → kart #{targetCard.Id}", now));
                await _uow.CommitAsync(ct);
                await CreditPointsInternalAsync(targetCard, target, customer.Id, amount, $"Balans köçürüldü ← kart #{from.Id}", null, ct);
            }

            _logger.LogInformation(
                "Ambassador card switch: CustomerId={CustomerId}, Closed=[{Closed}], Target={TargetCardId} (Program {ProgramId}), CarriedCards={Carry}",
                customer.Id, string.Join(",", toClose.Select(c => c.Id)), targetCard.Id, target.Id, carryCards.Count);
            return new SwitchResult(true, targetCard, null);
        }

        private static Transaction NewMarkerTx(int cardId, decimal points, decimal balanceAfter, string description, DateTime now) => new()
        {
            LoyaltyCardId = cardId,
            Type = TransactionType.Adjusted,
            Status = RedeemStatus.Applied,
            Points = points,
            BalanceAfter = balanceAfter,
            Description = description,
            RegDate = now,
            IsActive = true,
            LocalOccurredAt = DateTimeOffset.UtcNow,
            TimeZoneIdUsed = "UTC"
        };

        public async Task<ServiceResult> AddPointsAsync(int tenantId, CreditPointsDto dto, CancellationToken ct = default)
        {
            if (dto == null || dto.LoyaltyCardId <= 0)
                return new ServiceResult(false, "Card is required.");

            var card = await _cardRepo.GetById(dto.LoyaltyCardId, ct);
            if (card is null)
                return new ServiceResult(false, "Card not found.");
            if (card.TenantId != tenantId)
                return new ServiceResult(false, "Card does not belong to your tenant.");
            if (!card.IsActive)
                return new ServiceResult(false, "Kart aktiv deyil — müştərinin aktiv kartına xal verin.");

            var program = await _programRepo.GetByIdNameAsync(card.LoyaltyProgramId, ct);
            if (program is null)
                return new ServiceResult(false, "Program missing.");
            if (dto.ExpiresAt.HasValue && dto.ExpiresAt.Value.ToUniversalTime() <= DateTime.UtcNow)
                return new ServiceResult(false, "Son istifadə tarixi gələcəkdə olmalıdır.");

            var description = string.IsNullOrWhiteSpace(dto.Description) ? "Manual xal" : dto.Description!.Trim();
            await CreditPointsInternalAsync(card, program, card.CustomerId, dto.Points, description, dto.ExpiresAt, ct);

            return new ServiceResult(true, new { cardId = card.Id, balance = card.CurrentPoints, expiresAt = PointsExpiry.AsUtc(card.ExpiresAt) });
        }

        public async Task<ServiceResult> SetPointsExpiryAsync(int tenantId, SetPointsExpiryDto dto, CancellationToken ct = default)
        {
            if (dto == null || dto.LoyaltyCardId <= 0)
                return new ServiceResult(false, "Card is required.");

            var card = await _cardRepo.GetById(dto.LoyaltyCardId, ct);
            if (card is null)
                return new ServiceResult(false, "Card not found.");
            if (card.TenantId != tenantId)
                return new ServiceResult(false, "Card does not belong to your tenant.");
            if (!card.IsActive)
                return new ServiceResult(false, "Kart aktiv deyil — müştərinin aktiv kartında dəyişin.");

            var now = DateTime.UtcNow;
            card.ExpiresAt = dto.ExpiresAt.HasValue
                ? DateTime.SpecifyKind(dto.ExpiresAt.Value.ToUniversalTime(), DateTimeKind.Utc)
                : null;
            card.EditDate = now;
            _uow.Repository<LoyaltyCard>().Update(card);
            await _uow.CommitAsync(ct);

            decimal expired = 0m;
            if (card.IsPointsExpired(now))
                expired = await _pointsExpiry.ExpireCardAsync(card.Id, ct); // keçmiş tarix → dərhal sıfırla + iiko

            _logger.LogInformation("Ambassador points expiry set. CardId={CardId}, ExpiresAt={ExpiresAt}, ExpiredNow={Expired}", card.Id, card.ExpiresAt, expired);
            return new ServiceResult(true, new { cardId = card.Id, expiresAt = PointsExpiry.AsUtc(card.ExpiresAt), balance = card.CurrentPoints, expiredPoints = expired });
        }

        /// <summary>
        /// Karta xal kredit et → Transaction + wallet pass push (background) + POS wallet forward (isolated).
        /// expiresAt: verilibsə kartın bitmə tarixini əvəz edir; verilməyibsə keçmiş tarix təmizlənir (bax PointsExpiry.ResolveOnCredit).
        /// </summary>
        private async Task CreditPointsInternalAsync(
            LoyaltyCard card, LoyaltyProgram program, int customerId, decimal points, string description, DateTime? expiresAt, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            card.ExpiresAt = PointsExpiry.ResolveOnCredit(card.ExpiresAt, expiresAt, now);
            if (card.ExpiresAt.HasValue)
            {
                // Təsvirdə tenant-ın yerli vaxtı (məs. Bakı UTC+4)
                var (local, _) = await _timeZoneService.GetTenantLocalTimeAsync(card.TenantId, card.ExpiresAt.Value, ct);
                description = $"{description} (bitmə {local:dd.MM.yyyy HH:mm})";
            }

            card.CurrentPoints += points;
            if (points > 0) card.LifetimePoints += points;
            card.EditDate = now;
            _uow.Repository<LoyaltyCard>().Update(card);

            var tx = new Transaction
            {
                LoyaltyCardId = card.Id,
                Type = points >= 0 ? TransactionType.Bonus : TransactionType.Redeemed,
                Status = RedeemStatus.Applied,
                Points = points,
                BalanceAfter = card.CurrentPoints,
                Description = description,
                RegDate = DateTime.UtcNow,
                IsActive = true,
                LocalOccurredAt = DateTimeOffset.UtcNow,
                TimeZoneIdUsed = "UTC"
            };
            _uow.Repository<Transaction>().Add(tx);
            await _uow.CommitAsync(ct);

            // Wallet pass push (Apple + Google) — background (Hangfire)
            try
            {
                BackgroundJob.Enqueue<IWalletPushJob>(j => j.RunAsync(card.Id, program.Id, points));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ambassador wallet push enqueue failed. CardId={CardId}", card.Id);
            }

            // POS forward — iiko wallet balansını yeni CurrentPoints-ə uyğunlaşdır (isolated, best-effort)
            await RunPosBalanceSyncSafeAsync(card.TenantId, customerId, card.CurrentPoints, ct);
        }

        private async Task RunPosCustomerSyncSafeAsync(int tenantId, int customerId, CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<IPosSyncService>();
                var db = scope.ServiceProvider.GetRequiredService<LoyaltyWalletDbContext>();
                var customer = await db.Set<Customer>().FirstOrDefaultAsync(x => x.Id == customerId, ct);
                if (customer == null) return;
                await sync.SyncCustomerToPosAsync(tenantId, customer, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ambassador POS customer sync failed. CustomerId={CustomerId}, TenantId={TenantId}", customerId, tenantId);
            }
        }

        private async Task RunAmbassadorCategorySyncSafeAsync(int tenantId, int customerId, bool isAmbassador, CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<IPosSyncService>();
                await sync.SyncAmbassadorCategoryAsync(tenantId, customerId, isAmbassador, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ambassador iiko category sync failed. CustomerId={CustomerId}, TenantId={TenantId}", customerId, tenantId);
            }
        }

        private async Task RunPosBalanceSyncSafeAsync(int tenantId, int customerId, decimal currentPoints, CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<IPosSyncService>();
                await sync.SyncBalanceToPosAsync(tenantId, customerId, currentPoints, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ambassador POS balance sync failed. CustomerId={CustomerId}, TenantId={TenantId}", customerId, tenantId);
            }
        }

        private void TrySendAmbassadorSms(string phone, string? companyName)
        {
            try
            {
                var name = string.IsNullOrWhiteSpace(companyName) ? "Walvero" : companyName!.Trim();
                var text = $"{name} sizi ambassador olaraq əlavə etdi. Kartınızı Walvero tətbiqində görə bilərsiniz: https://walvero.com";
                _ = Helper.SendSmsAsync(phone, text);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ambassador SMS failed. Phone={Phone}", phone);
            }
        }
    }
}
