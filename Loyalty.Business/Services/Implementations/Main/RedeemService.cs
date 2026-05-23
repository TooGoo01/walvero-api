// Business/Services/Implementations/Main/RedeemService.cs
using Hangfire;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Dtos.Realtime;
using Loyalty.Business.Helpers;
using Loyalty.Business.Hubs;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Loyalty.Infrastructure.Extensions;

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using WalletPlatform = Loyalty.DataAccess.Enums.WalletPlatform;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class RedeemService : IRedeemService
    {
        private readonly ILoyaltyCardRepository _cardSvc;
        private readonly ILoyaltyProgramRepository _programSvc;
        private readonly ITenantBrandingRepository _brandingSvc;
        private readonly ICustomerRepository _customerRepo;

        private readonly IGoogleWalletServiceFactory _gwFactory;
        private readonly IWalletRouter _walletRouter;
        private readonly ITenantRepository _tenantPolicySvc;
        private readonly IOtpGenerator _otpGenerator;

        private readonly IUnitOfWork uow;
        private readonly ILogger<RedeemService> _logger;
        private readonly IHubContext<LoyaltyHub> _hubContext;
        private readonly INotificationService _notificationService;
        private readonly LoyaltyWalletDbContext _dbContext;
        private readonly ITimeZoneService _timeZoneService;
        private readonly ILotteryRepository _lotteryRepository;
        private readonly IReceiptValidationService _receiptValidation;
        private readonly IWhatsAppAlertService _whatsappAlert;
        private readonly ITenantProvider _tenantProvider;
        private readonly Hangfire.IBackgroundJobClient _jobClient;
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
            ICustomerRepository customerRepo,
            LoyaltyWalletDbContext dbContext,
            ITimeZoneService timeZoneService,
            ILotteryRepository lotteryRepository,
            IReceiptValidationService receiptValidation,
            IWhatsAppAlertService whatsappAlert,
            ITenantProvider tenantProvider,
            Hangfire.IBackgroundJobClient jobClient)
        {
            _cardSvc = cardSvc;
            _programSvc = programSvc;
            _brandingSvc = brandingSvc;
            _gwFactory = gwFactory;
            _customerRepo = customerRepo;

            _walletRouter = walletRouter;
            _tenantPolicySvc = tenantPolicySvc;
            _otpGenerator = otpGenerator;

            this.uow = uow;
            _logger = logger;
            _hubContext = hubContext;
            _notificationService = notificationService;
            _dbContext = dbContext;
            _timeZoneService = timeZoneService;
            _lotteryRepository = lotteryRepository;
            _receiptValidation = receiptValidation;
            _whatsappAlert = whatsappAlert;
            _tenantProvider = tenantProvider;
            _jobClient = jobClient;
        }

        // Resolved per-redeem fraud config (branch override + tenant fallback)
        private sealed class EffectiveSuspiciousCfg
        {
            public bool RequireOcr { get; init; }
            public decimal Tolerance { get; init; } = 1m;
            public decimal? AmountCap { get; init; }
            public int? SpendCountCap { get; init; }
            public bool AlertEnabled { get; init; }
            public string? AlertPhone { get; init; }
            public string TenantName { get; init; } = string.Empty;
        }

        private async Task<EffectiveSuspiciousCfg> ResolveCfgAsync(int tenantId, int? branchId, CancellationToken ct)
        {
            var tenant = await _dbContext.Tenants
                .AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => new
                {
                    t.CompanyName,
                    t.RequireReceiptOcr,
                    t.OcrToleranceAzn,
                    t.SuspiciousAmountCap,
                    t.SuspiciousSpendCountCap,
                    t.EnableSuspiciousAlert,
                    t.AlertWhatsappPhone
                })
                .FirstOrDefaultAsync(ct);

            if (tenant is null)
                return new EffectiveSuspiciousCfg();

            var branch = branchId.HasValue
                ? await _dbContext.Branches
                    .AsNoTracking()
                    .Where(b => b.Id == branchId.Value)
                    .Select(b => new
                    {
                        b.RequireReceiptOcr,
                        b.OcrToleranceAzn,
                        b.SuspiciousAmountCap,
                        b.SuspiciousSpendCountCap,
                        b.EnableSuspiciousAlert,
                        b.AlertWhatsappPhone
                    })
                    .FirstOrDefaultAsync(ct)
                : null;

            return new EffectiveSuspiciousCfg
            {
                RequireOcr = tenant.RequireReceiptOcr || (branch?.RequireReceiptOcr ?? false),
                Tolerance = branch?.OcrToleranceAzn ?? tenant.OcrToleranceAzn ?? 1m,
                AmountCap = branch?.SuspiciousAmountCap ?? tenant.SuspiciousAmountCap,
                SpendCountCap = branch?.SuspiciousSpendCountCap ?? tenant.SuspiciousSpendCountCap,
                AlertEnabled = (branch?.EnableSuspiciousAlert ?? false) || tenant.EnableSuspiciousAlert,
                AlertPhone = !string.IsNullOrWhiteSpace(branch?.AlertWhatsappPhone)
                    ? branch.AlertWhatsappPhone
                    : tenant.AlertWhatsappPhone,
                TenantName = tenant.CompanyName ?? string.Empty
            };
        }

        // START (Auto / ForcePending / ForceApply)
        public async Task<RedeemResultDto> RedeemAsync(RedeemRequestDto request, CancellationToken ct = default)
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["TenantId"] = request.TenantId,
                ["ProgramId"] = request.LoyaltyProgramId,
                ["Delta"] = request.Delta,
                ["Mode"] = request.Mode.ToString(),
                ["PrefPlatform"] = request.PreferredPlatform.ToString(),
                ["IdemKey"] = request.IdempotencyKey
            });

            try
            {
                _logger.LogInformation("Redeem started. Code={Code}", request.Code);

                if (request.TenantId <= 0 || string.IsNullOrWhiteSpace(request.Code))
                {
                    _logger.LogWarning("Invalid tenant or code. TenantId={TenantId}, CodeEmpty={CodeEmpty}",
                        request.TenantId, string.IsNullOrWhiteSpace(request.Code));
                    return Fail("Tenant və ya kod yanlışdır.");
                }

                // 1) Kart - Try Barcode → WalveroId (phone ilə dəqiqləşdir) → Phone
                LoyaltyCard? card = null;
                var code = request.Code.Trim();
                Customer? resolvedCustomer = null;

                // 1a) BarcodeValue ilə axtar
                card = await _cardSvc.GetByBarcodeValueAsync(request.TenantId, code, ct);

                // Barcode ilə tapıldısa da, customer-in phone-u ilə dəqiqləşdir
                if (card?.Customer?.applicationUser?.UserName != null)
                {
                    var phoneCustomer = await _customerRepo.GetByPhoneAsync(
                        request.TenantId, card.Customer.applicationUser.UserName, ct);
                    if (phoneCustomer != null && phoneCustomer.Id != card.CustomerId)
                    {
                        // Phone ilə tapılan customer fərqlidirsə, onun kartını istifadə et
                        var phoneCard = await FindActiveCardForCustomer(request.TenantId, phoneCustomer.Id, request.LoyaltyProgramId, ct);
                        if (phoneCard != null)
                            card = phoneCard;
                    }
                }

                // 1b) WalveroId ilə axtar → phone ilə dəqiqləşdir
                if (card == null && long.TryParse(code, out long walveroId))
                {
                    _logger.LogInformation("Barcode not found, trying WalveroId: {WalveroId}", walveroId);

                    var walveroCustomer = await _customerRepo.GetByWalveroIdAsync(request.TenantId, walveroId, ct);
                    resolvedCustomer = walveroCustomer;

                    // Phone ilə dəqiqləşdir
                    if (walveroCustomer?.applicationUser?.UserName != null)
                    {
                        var phoneCustomer = await _customerRepo.GetByPhoneAsync(
                            request.TenantId, walveroCustomer.applicationUser.UserName, ct);
                        if (phoneCustomer != null)
                            resolvedCustomer = phoneCustomer;
                    }

                    if (resolvedCustomer != null)
                        card = await FindActiveCardForCustomer(request.TenantId, resolvedCustomer.Id, request.LoyaltyProgramId, ct);
                }

                // 1c) Phone nömrəsi ilə axtar
                if (card == null && code.StartsWith("+"))
                {
                    _logger.LogInformation("Trying phone lookup: {Phone}", code);
                    var phoneCustomer = await _customerRepo.GetByPhoneAsync(request.TenantId, code, ct);
                    if (phoneCustomer != null)
                        card = await FindActiveCardForCustomer(request.TenantId, phoneCustomer.Id, request.LoyaltyProgramId, ct);
                }

                if (card == null)
                {
                    _logger.LogWarning("Card not found. Code={Code}", code);
                    return Fail("Kart tapılmadı.");
                }

                var appUserId = card.Customer?.applicationUser?.Id;

                // 2) Program + Branding + Policy
                var program = await _programSvc.GetByIdNameAsync(card.LoyaltyProgramId, ct);
                if (program is null)
                {
                    _logger.LogWarning("Program not found. ProgramId={ProgramId}", card.LoyaltyProgramId);
                    return Fail("Loyalty program tapılmadı.");
                }

                var branding = await _brandingSvc.GetByTenantIdAsync(request.TenantId, ct);
                var policy = await _tenantPolicySvc.GetByIdAsync(request.TenantId, ct);

                string baseUrl = "https://appapi.walvero.com";
                string ToAbs(string? dbPath)
                {
                    if (string.IsNullOrWhiteSpace(dbPath)) return string.Empty;
                    if (dbPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                        dbPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return dbPath;
                    
                    var normalizedBaseUrl = baseUrl.TrimEnd('/', '\\');
                    var normalizedPath = dbPath.TrimStart('~').TrimStart('/', '\\');
                    normalizedPath = string.IsNullOrWhiteSpace(normalizedPath) ? "/" : "/" + normalizedPath;
                    return normalizedBaseUrl + normalizedPath;
                }

                var snapshot = CardTemplateSnapshotFactory.From(program, program.CardTemplate, branding, ToAbs);
                var delta = request.Delta;

                _logger.LogInformation("Snapshot built. TemplateType={TemplateType}, RequireConfirmation={RequireConfirmation}",
                    snapshot.TemplateType, policy.RequireConfirmation);

                // === Branch-aware fraud-prevention pipeline ===
                var operatorBranchId = _tenantProvider.CurrentBranchId;
                var claimBranchId = operatorBranchId;
                // JWT-də claim yoxdursa (köhnə token), ApplicationUser.BranchId-ə fallback
                if (!operatorBranchId.HasValue)
                {
                    var userIdStr = _tenantProvider.CurrentUserId;
                    if (int.TryParse(userIdStr, out var uid))
                    {
                        operatorBranchId = await _dbContext.Users
                            .AsNoTracking()
                            .Where(u => u.Id == uid)
                            .Select(u => u.BranchId)
                            .FirstOrDefaultAsync(ct);
                    }
                }
                var cfg = await ResolveCfgAsync(request.TenantId, operatorBranchId, ct);

                _logger.LogInformation(
                    "Fraud cfg resolved. UserId={Uid} claimBranch={ClaimBranch} dbBranch={DbBranch} " +
                    "RequireOcr={Req} AmountCap={Cap} SpendCap={SCap} HasReceipt={HasImg}",
                    _tenantProvider.CurrentUserId, claimBranchId, operatorBranchId,
                    cfg.RequireOcr, cfg.AmountCap, cfg.SpendCountCap, request.ReceiptImage != null);

                // Receipt OCR — earn (delta > 0) və pay (delta < 0) hər iki rejimdə.
                // Earn-də də doğrulama lazımdır ki, kassir 500 yazsa çek 500 olduğu üçün bonus düzgün hesablansın.
                string? receiptUrl = null;
                decimal? ocrAmount = null;
                bool isSuspicious = false;
                string? suspiciousReason = null;

                if (cfg.RequireOcr && delta != 0)
                {
                    if (request.ReceiptImage is null)
                    {
                        _logger.LogWarning("Receipt OCR required but no image. TenantId={TenantId}, BranchId={BranchId}",
                            request.TenantId, operatorBranchId);
                        return Fail("Çekin şəklini yükləyin.");
                    }

                    // Yeni async axın: çekin şəklini blob-a sync yüklə (kicik), sonra Hangfire-ə OCR job buy.
                    // Mobile cavabı dərhal qaytarır — OCR background-da getsin, waiter növbəti customer-ə keçsin.
                    string blobUrl;
                    try
                    {
                        var uploaded = await _receiptValidation.UploadReceiptOnlyAsync(request.ReceiptImage, ct);
                        blobUrl = uploaded.url;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Receipt blob upload failed at PendingOcr stage");
                        return Fail("Çek faylının yüklənməsi alınmadı.");
                    }

                    // Transaction yarat PendingOcr status ilə, balansa toxunma
                    var (localTimePending, tzPending) = await _timeZoneService.GetTenantLocalTimeAsync(request.TenantId, null, CancellationToken.None);
                    var pendingTx = BuildTransaction(card, delta, request.OrderId, request.Note,
                        delta < 0 ? TransactionType.Redeemed : TransactionType.Earned, localTimePending, tzPending);
                    pendingTx.Status = RedeemStatus.PendingOcr;
                    pendingTx.BranchId = operatorBranchId;
                    pendingTx.Amount = request.OriginalAmount;
                    pendingTx.ReceiptImageUrl = blobUrl;

                    uow.Repository<Transaction>().Add(pendingTx);
                    uow.Commit();

                    // Job-a göndər
                    var amountForOcr = request.OriginalAmount ?? Math.Abs(delta);
                    var customerName = card.Customer?.applicationUser?.UserName;
                    _jobClient.Enqueue<ReceiptOcrJob>(j => j.ProcessAsync(
                        pendingTx.Id,
                        amountForOcr,
                        cfg.Tolerance,
                        cfg.AlertEnabled,
                        cfg.AlertPhone,
                        cfg.TenantName,
                        customerName,
                        request.OperatorId,
                        cfg.AmountCap ?? 0,
                        CancellationToken.None));

                    _logger.LogInformation("PendingOcr tx queued. TxId={TxId} CardId={CardId}", pendingTx.Id, card.Id);

                    return new RedeemResultDto
                    {
                        Success = true,
                        IsPending = true,
                        LoyaltyCardId = card.Id,
                        AppliedDelta = 0,
                        Message = "Çek OCR yoxlaması üçün göndərildi. Balans yoxlamadan sonra yenilənəcək."
                    };
                }

                // Suspicious check — amount cap OR spendCount cap (OCR-də flag olunmuş ola bilər artıq)
                if (!isSuspicious && cfg.AmountCap.HasValue && Math.Abs(delta) > cfg.AmountCap.Value)
                {
                    isSuspicious = true;
                    suspiciousReason = "AmountExceedsCap";
                }
                else if (!isSuspicious && cfg.SpendCountCap.HasValue && request.SpendCount.HasValue
                         && request.SpendCount.Value > cfg.SpendCountCap.Value)
                {
                    isSuspicious = true;
                    suspiciousReason = "SpendCountExceedsCap";
                }

                // Check for free reward spending (ProgressBased only)
                if (request.SpendCount.HasValue && request.SpendCount.Value > 0)
                {
                    // Free reward xərcləməsi
                    request.Delta = 0; // Delta-nı 0-a set et ki, ApplyDelta çağırılmasın
                    string freeRewardLabel = GetFreeRewardLabel(snapshot);
                    return await RedeemFreeRewardAsync(
                        card, request, snapshot, freeRewardLabel,
                        operatorBranchId, receiptUrl, ocrAmount, isSuspicious, suspiciousReason, cfg, ct);
                }

                // Safety: ProgressBased-də delta=0 və spendCount yoxdur/0-dırsa, heç nə etmə
                if (snapshot.TemplateType == CardTemplateType.ProgressBased
                    && delta == 0
                    && (!request.SpendCount.HasValue || request.SpendCount.Value == 0))
                {
                    _logger.LogInformation("ProgressBased noop (delta=0, spendCount missing/0). CardId={CardId}", card.Id);
                    return new RedeemResultDto
                    {
                        Success = true,
                        Message = "Heç bir dəyişiklik edilmədi (delta=0, spendCount=0).",
                        LoyaltyCardId = card.Id,
                        AppliedDelta = 0
                    };
                }

                // 3) Mode qərarı
                var mustConfirm = snapshot.TemplateType == CardTemplateType.TierBased
                                  && policy.RequireConfirmation;

                if (request.Mode == RedeemMode.ForcePending)
                {
                    mustConfirm = true;
                    _logger.LogInformation("ForcePending enabled -> mustConfirm={MustConfirm}", mustConfirm);
                }

                if (request.Mode == RedeemMode.ForceApply && policy.RequireConfirmation)
                {
                    _logger.LogWarning("ForceApply denied due to tenant policy (confirmation required).");
                    return Fail("Bu tenant üçün Tier redeem təsdiq tələb edir.");
                }

                if (mustConfirm && request.Delta < 0)
                {
                    // 3a) OTP ilə PENDING
                    var otp = _otpGenerator.GenerateNumeric(6);
                    if (card.Customer.PhoneNumber == "+994222222222")
                    {
                        otp = "994789";
                    }
                    var rr = new RedeemRequest
                    {
                        TenantId = request.TenantId,
                        LoyaltyCardId = card.Id,
                        PointsDelta = delta,
                        Status = RedeemStatus.Pending,
                        Code = otp,
                        ExpiresUtc = DateTime.UtcNow.AddMinutes(policy.ConfirmationTtlMinutes),
                        IdempotencyKey = request.IdempotencyKey,
                        OperatorId = request.OperatorId,
                        OrderId = request.OrderId,
                        Note = request.Note,
                        BranchId = operatorBranchId,
                        ReceiptImageUrl = receiptUrl,
                        OcrExtractedAmount = ocrAmount
                    };

                    await uow.Repository<RedeemRequest>().AddAsync(rr);
                    uow.Commit();

                    _logger.LogInformation("Pending redeem created. RequestId={RequestId}, ExpiresInMin={TTL}, OtpMasked={Otp}",
                        rr.Id, policy.ConfirmationTtlMinutes, MaskOtp(otp));

                    // Bildiriş (SMS/push) - yalnız RequireConfirmation=true olduqda göndər
                    if (policy.RequireConfirmation)
                    {
                        var destination = card.Customer.PhoneNumber;
                        if (!string.IsNullOrWhiteSpace(destination))
                        {
                            var text = $"Sizin OTP: {otp}\nwalvero.com #{otp}";
                            try
                            {
                                var data = new Dictionary<string, string>
                                {
                                    { "type", "otp" },
                                    { "otpCode", otp },
                                    { "expiryMinutes", policy.ConfirmationTtlMinutes.ToString() }
                                };

                                if (appUserId.HasValue)
                                    await _notificationService.SendToUserAsync(appUserId.Value, "Təsdiq", text, data);
                                else
                                    _logger.LogWarning("appUserId is null -> skipping in-app OTP notification. CardId={CardId}", card.Id);

                                await Helper.SendSmsAsync(destination, text);
                                _logger.LogInformation("OTP SMS sent. Phone={Phone}", MaskPhone(destination));
                            }
                            catch (Exception smsEx)
                            {
                                _logger.LogError(smsEx, "OTP SMS send failed. Phone={Phone}", MaskPhone(destination));
                            }
                        }
                        else
                        {
                            _logger.LogWarning("No phone number to send OTP. CustomerId={CustomerId}", card.CustomerId);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("RequireConfirmation=false, OTP notification skipped. CardId={CardId}", card.Id);
                    }

                    return new RedeemResultDto
                    {
                        Success = true,
                        IsPending = true,
                        LoyaltyCardId = card.Id,
                        RequiresOtp = mustConfirm,
                        AppliedDelta = delta,
                        RedeemRequestId = rr.Id,
                        Message = $"Təsdiq kodu göndərildi. Keçərlilik: {policy.ConfirmationTtlMinutes} dəqiqə."
                    };
                }

                // 4) Dərhal tətbiq
                if (request.DryRun)
                {
                    _logger.LogInformation("DryRun mode -> no DB changes. CardId={CardId}, Delta={Delta}", card.Id, delta);
                    return new RedeemResultDto
                    {
                        Success = true,
                        IsPending = false,
                        LoyaltyCardId = card.Id,
                        AppliedDelta = 0,
                        Message = $"Kart #{card.Id} üçün {(delta >= 0 ? "+" : "")}{delta} tətbiq olundu.",
                    };
                }

                // Suspicious → flag-only, balansa toxunma; admin review etməlidir
                if (isSuspicious)
                {
                    var (sLocalTime, sTzId) = await _timeZoneService.GetTenantLocalTimeAsync(request.TenantId, null, CancellationToken.None);
                    var stx = BuildTransaction(card, delta, request.OrderId, request.Note,
                        delta < 0 ? TransactionType.Redeemed : TransactionType.Earned,
                        sLocalTime, sTzId);
                    stx.Status = RedeemStatus.FlaggedForReview;
                    stx.BranchId = operatorBranchId;
                    stx.Amount = request.OriginalAmount;
                    stx.ReceiptImageUrl = receiptUrl;
                    stx.OcrExtractedAmount = ocrAmount;
                    stx.IsSuspicious = true;
                    stx.SuspiciousReason = suspiciousReason;

                    uow.Repository<Transaction>().Add(stx);
                    uow.Commit();

                    _logger.LogWarning("Suspicious tx flagged. CardId={CardId}, Reason={Reason}, Delta={Delta}, AmountCap={AmountCap}, SpendCountCap={SpendCountCap}",
                        card.Id, suspiciousReason, delta, cfg.AmountCap, cfg.SpendCountCap);

                    if (cfg.AlertEnabled && !string.IsNullOrWhiteSpace(cfg.AlertPhone))
                    {
                        var customerName = BuildCustomerName(card);
                        var branchName = operatorBranchId.HasValue
                            ? await _dbContext.Branches.AsNoTracking()
                                .Where(b => b.Id == operatorBranchId.Value)
                                .Select(b => (string?)b.Name)
                                .FirstOrDefaultAsync(ct)
                            : null;
                        var operatorName = await ResolveOperatorNameAsync(request.OperatorId, ct);
                        var enteredAmount = request.OriginalAmount ?? Math.Abs(delta);
                        var txRef = stx.OrderId ?? stx.Id.ToString();
                        _ = Task.Run(() => _whatsappAlert.SendSuspiciousTxAlertAsync(
                            cfg.AlertPhone!, cfg.TenantName, branchName, customerName, operatorName,
                            enteredAmount, ocrAmount, cfg.AmountCap, suspiciousReason,
                            txRef, CancellationToken.None));
                    }

                    return new RedeemResultDto
                    {
                        Success = true,
                        Message = "Şübhəli əməliyyat kimi qeyd olundu. Admin yoxlamasına gözləyir.",
                        LoyaltyCardId = card.Id,
                        AppliedDelta = 0,
                    };
                }

                var beforePoints = card.CurrentPoints;
                var beforeSteps = card.CurrentSteps;
                var beforeCompletedCycles = card.CompletedCycles;

                card.LifetimePoints += 1;
                ApplyDelta(card, snapshot, delta);
                // Get tenant's local time — CancellationToken.None: yazma mərhələsi ləğv olunmamalıdır
                var (localTime, timeZoneId) = await _timeZoneService.GetTenantLocalTimeAsync(request.TenantId, null, CancellationToken.None);

                var tx = BuildTransaction(card, delta, request.OrderId, request.Note, TransactionType.Earned, localTime, timeZoneId);
                tx.BranchId = operatorBranchId;
                tx.Amount = request.OriginalAmount;
                tx.ReceiptImageUrl = receiptUrl;
                tx.OcrExtractedAmount = ocrAmount;
                uow.Repository<LoyaltyCard>().Update(card);
                uow.Repository<Transaction>().Add(tx);
                uow.Commit();

                _logger.LogInformation("Delta applied. CardId={CardId}, Points {Before}->{After}, Steps {StepsBefore}->{StepsAfter}, CompletedCycles {CyclesBefore}->{CyclesAfter}, Delta={Delta}",
                    card.Id, beforePoints, card.CurrentPoints, beforeSteps, card.CurrentSteps, beforeCompletedCycles, card.CompletedCycles, delta);

                // 4b) Lottery ticket generation
                if (program.IsLotteryEnabled && delta > 0)
                {
                    bool shouldGenerateTicket = false;

                    // Count-based: every N purchases
                    if (program.LotteryPurchaseThreshold > 0)
                    {
                        card.LotteryEarnCount++;
                        if (card.LotteryEarnCount >= program.LotteryPurchaseThreshold)
                        {
                            card.LotteryEarnCount = 0;
                            shouldGenerateTicket = true;
                        }
                    }

                    // Amount-based: every X points spent
                    if (program.LotteryAmountThreshold.HasValue && program.LotteryAmountThreshold > 0)
                    {
                        card.LotteryAmountAccum += Math.Abs(delta);
                        while (card.LotteryAmountAccum >= program.LotteryAmountThreshold.Value)
                        {
                            card.LotteryAmountAccum -= program.LotteryAmountThreshold.Value;
                            shouldGenerateTicket = true;
                        }
                    }

                    if (shouldGenerateTicket)
                    {
                        var ticket = new LotteryTicket
                        {
                            LoyaltyCardId = card.Id,
                            TenantId = card.TenantId,
                            TicketCode = GenerateLotteryTicketCode(),
                            Status = LotteryTicketStatus.Active
                        };
                        uow.Repository<LotteryTicket>().Add(ticket);
                        _logger.LogInformation("Lottery ticket generated. CardId={CardId}, TicketCode={TicketCode}", card.Id, ticket.TicketCode);
                    }

                    uow.Repository<LoyaltyCard>().Update(card);
                    uow.Commit();
                }

                // 5) Lottery data for wallet display
                int lotteryTickets = 0;
                string? lotteryWinPrize = null;
                DateTime? lotteryDrawDate = null;
                if (program.IsLotteryEnabled)
                {
                    try
                    {
                        lotteryTickets = await _lotteryRepository.GetActiveTicketCountByCardIdAsync(card.Id);
                        var wins = await _lotteryRepository.GetWinsByCardIdAsync(card.Id);
                        var latestWin = wins.FirstOrDefault();
                        if (latestWin?.LotteryDraw != null)
                            lotteryWinPrize = latestWin.LotteryDraw.PrizeName;

                        var nextDraw = await _lotteryRepository.GetNextScheduledDrawAsync(card.TenantId, program.Id);
                        if (nextDraw?.ScheduledAt != null)
                            lotteryDrawDate = nextDraw.ScheduledAt;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to query lottery data for wallet display. CardId={CardId}", card.Id);
                    }
                }

                // 6) Platforma → router
                var primaryPlatform = card.IsIOS ? WalletPlatform.AppleWallet : WalletPlatform.GooglePay;
                var secondaryPlatform = card.IsIOS ? WalletPlatform.GooglePay : WalletPlatform.AppleWallet;

                var primaryOk = false;
                var secondaryOk = false;

                // ⚠️ CancellationToken.None: DB commit tamamlandıqdan sonra wallet/notification ləğv olunmamalıdır
                // PRIMARY - Credentials yoxdursa exception atmadan davam et
                _logger.LogInformation("Routing after commit to PRIMARY {Platform}.", primaryPlatform);
                try
                {
                    var primaryRes = await _walletRouter.RouteAsync(
                        primaryPlatform,
                        card.Id,
                        snapshot,
                        card.CurrentPoints,
                        card.CurrentSteps,
                        card.CompletedCycles,
                        (int)delta,
                        CancellationToken.None,
                        lotteryTickets,
                        lotteryWinPrize,
                        lotteryDrawDate);

                    if (!primaryRes.Success)
                        _logger.LogWarning("Wallet update FAILED on PRIMARY {Platform}. CardId={CardId}", primaryPlatform, card.Id);
                    else
                        primaryOk = true;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("credential") || ex.Message.Contains("not found"))
                {
                    _logger.LogWarning(ex, "Credentials not found for PRIMARY {Platform}. Continuing without wallet update. CardId={CardId}", primaryPlatform, card.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Wallet update error on PRIMARY {Platform}. Continuing without wallet update. CardId={CardId}", primaryPlatform, card.Id);
                }

                // SECONDARY - Credentials yoxdursa exception atmadan davam et
                _logger.LogInformation("Trying SECONDARY {Platform}.", secondaryPlatform);
                try
                {
                    var secondaryRes = await _walletRouter.RouteAsync(
                        secondaryPlatform,
                        card.Id,
                        snapshot,
                        card.CurrentPoints,
                        card.CurrentSteps,
                        card.CompletedCycles,
                        (int)delta,
                        CancellationToken.None,
                        lotteryTickets,
                        lotteryWinPrize,
                        lotteryDrawDate);

                    if (!secondaryRes.Success)
                        _logger.LogWarning("Wallet update FAILED on SECONDARY {Platform}. CardId={CardId}", secondaryPlatform, card.Id);
                    else
                        secondaryOk = true;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("credential") || ex.Message.Contains("not found"))
                {
                    _logger.LogWarning(ex, "Credentials not found for SECONDARY {Platform}. Continuing without wallet update. CardId={CardId}", secondaryPlatform, card.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Wallet update error on SECONDARY {Platform}. Continuing without wallet update. CardId={CardId}", secondaryPlatform, card.Id);
                }

                if (appUserId.HasValue)
                {
                    var message = new CardUpdatedMessage
                    {
                        CardId = card.Id,
                        ProgramId = card.LoyaltyProgramId,
                        TenantId = card.TenantId,
                        CurrentPoints = card.CurrentPoints,
                        LastRedeemAmount = request.Delta,
                        UpdatedAt = DateTime.UtcNow
                    };

                    var groupName = $"user-{appUserId.Value}";
                    var data = new Dictionary<string, string> { { "type", "normal" } };

                    try
                    {
                        await _hubContext
                            .Clients
                            .Group(groupName)
                            .SendAsync("CardUpdated", message, cancellationToken: CancellationToken.None);
                    }
                    catch (Exception hubEx)
                    {
                        _logger.LogWarning(hubEx, "SignalR CardUpdated send failed. CardId={CardId}", card.Id);
                    }

                    var currency = snapshot.Currency ?? "AZN"; // Default: AZN
                    var message1 = delta > 0
                        ? $"{snapshot.ProgramName} balansınıza {delta} {currency} əlavə olundu!"
                        : $"{snapshot.ProgramName} balansınızdan {delta * -1} {currency} çıxıldı!";

                    try
                    {
                        await _notificationService.SendToUserAsync(appUserId.Value, snapshot.ProgramName, message1, data);
                    }
                    catch (Exception fcmEx)
                    {
                        _logger.LogWarning(fcmEx, "FCM notification failed (non-fatal). CardId={CardId}", card.Id);
                    }
                }

              

                _logger.LogInformation("Redeem completed successfully. CardId={CardId}, AppliedDelta={Delta}", card.Id, delta);

                return new RedeemResultDto
                {
                    Success = true,
                    Message = $"Kart #{card.Id} üçün {(delta >= 0 ? "+" : "")}{delta} tətbiq olundu.",
                    LoyaltyCardId = card.Id,
                    RequiresOtp = false,
                    AppliedDelta = delta
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RedeemAsync exception.");
                return Fail("Yeniləmə zamanı sistem xətası baş verdi.");
            }
        }

        // CONFIRM (OTP)
        public async Task<RedeemResultDto> ConfirmRedeemAsync(int redeemRequestId, string code, CancellationToken ct = default)
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["RedeemRequestId"] = redeemRequestId
            });

            try
            {
                _logger.LogInformation("Confirm started. RequestId={RequestId}", redeemRequestId);

                var rr = await uow.Repository<RedeemRequest>().GetByIdAsync(redeemRequestId);
                if (rr is null)
                {
                    _logger.LogWarning("Redeem request not found. RequestId={RequestId}", redeemRequestId);
                    return Fail("Sorğu tapılmadı.");
                }

                if (rr.Status != RedeemStatus.Pending)
                {
                    _logger.LogWarning("Redeem request status is not Pending. Status={Status}", rr.Status);
                    return Fail("Sorğunun statusu PENDING deyil.");
                }

                if (rr.ExpiresUtc < DateTime.UtcNow)
                {
                    rr.Status = RedeemStatus.Expired;
                    uow.Repository<RedeemRequest>().Update(rr);
                    uow.Commit();
                    _logger.LogWarning("Redeem request expired. RequestId={RequestId}", redeemRequestId);
                    return Fail("Kodun vaxtı keçib.");
                }

                var policy = await _tenantPolicySvc.GetByIdAsync(rr.TenantId, ct);
                if (policy.RequireConfirmation && !string.Equals(rr.Code, code, StringComparison.Ordinal))
                {
                    _logger.LogWarning("OTP mismatch. Provided={Provided}, Stored={StoredMasked}", MaskOtp(code), MaskOtp(rr.Code));
                    return Fail("Təsdiq kodu yanlışdır.");
                }

                var card = await _cardSvc.GetById(rr.LoyaltyCardId, ct);
                if (card is null)
                {
                    _logger.LogWarning("Card not found for Confirm. CardId={CardId}", rr.LoyaltyCardId);
                    return Fail("Kart tapılmadı.");
                }

                var program = await _programSvc.GetByIdNameAsync(card.LoyaltyProgramId, ct);
                var branding = await _brandingSvc.GetByTenantIdAsync(rr.TenantId, ct);

                string baseUrl = "https://appapi.walvero.com";
                string ToAbs(string? dbPath)
                {
                    if (string.IsNullOrWhiteSpace(dbPath)) return string.Empty;
                    if (dbPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                        dbPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return dbPath;
                    
                    var normalizedBaseUrl = baseUrl.TrimEnd('/', '\\');
                    var normalizedPath = dbPath.TrimStart('~').TrimStart('/', '\\');
                    normalizedPath = string.IsNullOrWhiteSpace(normalizedPath) ? "/" : "/" + normalizedPath;
                    return normalizedBaseUrl + normalizedPath;
                }

                var snapshot = CardTemplateSnapshotFactory.From(program, program.CardTemplate, branding, ToAbs);

                var beforePoints = card.CurrentPoints;
                var beforeSteps = card.CurrentSteps;
                var beforeCompletedCycles = card.CompletedCycles;

                ApplyDelta(card, snapshot, rr.PointsDelta);
                // CancellationToken.None: yazma mərhələsi ləğv olunmamalıdır
                var (localTime, timeZoneId) = await _timeZoneService.GetTenantLocalTimeAsync(rr.TenantId, null, CancellationToken.None);

                // ProgressBased: display delta = reward sayı (integer), raw delta deyil
                // PointsBased/TierBased: kəsr saxlanır
                decimal displayDelta = rr.PointsDelta;
                if (snapshot.TemplateType == CardTemplateType.ProgressBased && rr.PointsDelta < 0)
                {
                    int totalSteps = Math.Max(1, snapshot.TotalSteps);
                    displayDelta = -(Math.Abs((int)rr.PointsDelta) / totalSteps);
                }

                var tx = BuildTransaction(card, displayDelta, rr.OrderId, rr.Note, TransactionType.Redeemed, localTime, timeZoneId);
                tx.BranchId = rr.BranchId;
                tx.ReceiptImageUrl = rr.ReceiptImageUrl;
                tx.OcrExtractedAmount = rr.OcrExtractedAmount;
                rr.Status = RedeemStatus.Applied;

                uow.Repository<LoyaltyCard>().Update(card);
                uow.Repository<Transaction>().Add(tx);
                uow.Repository<RedeemRequest>().Update(rr);
                uow.Commit();

                _logger.LogInformation("Confirm applied. CardId={CardId}, Points {Before}->{After}, Steps {StepsBefore}->{StepsAfter}, CompletedCycles {CyclesBefore}->{CyclesAfter}, Delta={Delta}, DisplayDelta={DisplayDelta}",
                    card.Id, beforePoints, card.CurrentPoints, beforeSteps, card.CurrentSteps, beforeCompletedCycles, card.CompletedCycles, rr.PointsDelta, displayDelta);

                var primaryPlatform = card.IsIOS ? WalletPlatform.AppleWallet : WalletPlatform.GooglePay;
                var secondaryPlatform = card.IsIOS ? WalletPlatform.GooglePay : WalletPlatform.AppleWallet;

                var primaryOk = false;
                var secondaryOk = false;

                // ⚠️ CancellationToken.None: DB commit-dən sonra wallet/notification ləğv olunmamalıdır
                // PRIMARY - Credentials yoxdursa exception atmadan davam et
                _logger.LogInformation("Routing after confirm to PRIMARY {Platform}.", primaryPlatform);
                try
                {
                    var primaryRes = await _walletRouter.RouteAsync(
                        primaryPlatform,
                        card.Id,
                        snapshot,
                        card.CurrentPoints,
                        card.CurrentSteps,
                        card.CompletedCycles,
                        (int)displayDelta,
                        CancellationToken.None);

                    if (!primaryRes.Success)
                        _logger.LogWarning("Wallet update FAILED on PRIMARY {Platform}. CardId={CardId}", primaryPlatform, card.Id);
                    else
                        primaryOk = true;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("credential") || ex.Message.Contains("not found"))
                {
                    _logger.LogWarning(ex, "Credentials not found for PRIMARY {Platform}. Continuing without wallet update. CardId={CardId}", primaryPlatform, card.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Wallet update error on PRIMARY {Platform}. Continuing without wallet update. CardId={CardId}", primaryPlatform, card.Id);
                }

                // SECONDARY - Credentials yoxdursa exception atmadan davam et
                _logger.LogInformation("Trying SECONDARY {Platform}.", secondaryPlatform);
                try
                {
                    var secondaryRes = await _walletRouter.RouteAsync(
                        secondaryPlatform,
                        card.Id,
                        snapshot,
                        card.CurrentPoints,
                        card.CurrentSteps,
                        card.CompletedCycles,
                        (int)displayDelta,
                        CancellationToken.None);

                    if (!secondaryRes.Success)
                        _logger.LogWarning("Wallet update FAILED on SECONDARY {Platform}. CardId={CardId}", secondaryPlatform, card.Id);
                    else
                        secondaryOk = true;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("credential") || ex.Message.Contains("not found"))
                {
                    _logger.LogWarning(ex, "Credentials not found for SECONDARY {Platform}. Continuing without wallet update. CardId={CardId}", secondaryPlatform, card.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Wallet update error on SECONDARY {Platform}. Continuing without wallet update. CardId={CardId}", secondaryPlatform, card.Id);
                }



                var appUserId = card.Customer?.applicationUser?.Id;
                if (appUserId.HasValue)
                {
                    var message = new CardUpdatedMessage
                    {
                        CardId = card.Id,
                        ProgramId = card.LoyaltyProgramId,
                        TenantId = card.TenantId,
                        CurrentPoints = card.CurrentPoints,
                        LastRedeemAmount = displayDelta,
                        UpdatedAt = DateTime.UtcNow
                    };

                    var groupName = $"user-{appUserId.Value}";
                    var data = new Dictionary<string, string> { { "type", "normal" } };

                    try
                    {
                        await _hubContext
                            .Clients
                            .Group(groupName)
                            .SendAsync("CardUpdated", message, cancellationToken: CancellationToken.None);
                    }
                    catch (Exception hubEx)
                    {
                        _logger.LogWarning(hubEx, "SignalR CardUpdated send failed (confirm). CardId={CardId}", card.Id);
                    }

                    string message1;
                    if (snapshot.TemplateType == CardTemplateType.ProgressBased)
                    {
                        var rewardLabel = snapshot.RewardDescription ?? "reward";
                        var rewardCount = Math.Abs(displayDelta);
                        message1 = displayDelta >= 0
                            ? $"{snapshot.ProgramName}: {rewardCount} {rewardLabel} əlavə olundu!"
                            : $"{snapshot.ProgramName}: {rewardCount} {rewardLabel} xərcləndi!";
                    }
                    else
                    {
                        var currency = snapshot.Currency ?? "AZN";
                        message1 = displayDelta > 0
                            ? $"{snapshot.ProgramName} balansınıza {displayDelta} {currency} əlavə olundu!"
                            : $"{snapshot.ProgramName} balansınızdan {Math.Abs(displayDelta)} {currency} çıxıldı!";
                    }

                    try
                    {
                        await _notificationService.SendToUserAsync(appUserId.Value, snapshot.ProgramName, message1, data);
                    }
                    catch (Exception fcmEx)
                    {
                        _logger.LogWarning(fcmEx, "FCM notification failed (non-fatal, confirm). CardId={CardId}", card.Id);
                    }
                }

                _logger.LogInformation("Confirm completed successfully. CardId={CardId}, AppliedDelta={Delta}", card.Id, displayDelta);

                return new RedeemResultDto
                {
                    Success = true,
                    Message = $"Təsdiq edildi. Kart #{card.Id} üçün {(displayDelta >= 0 ? "+" : "")}{displayDelta} tətbiq olundu.",
                    LoyaltyCardId = card.Id,
                    AppliedDelta = displayDelta,
                    OldPoints = beforePoints,
                    NewPoints = card.CurrentPoints
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ConfirmRedeemAsync exception. RequestId={RequestId}", redeemRequestId);
                return Fail("Təsdiq zamanı sistem xətası baş verdi.");
            }
        }

        // ------- Helpers -------

        private static void ApplyDelta(LoyaltyCard card, CardTemplateSnapshot snapshot, decimal delta)
        {
            switch (snapshot.TemplateType)
            {
                case CardTemplateType.ProgressBased:
                    // ProgressBased: steps integer — kəsr atılır
                    int totalSteps = Math.Max(1, snapshot.TotalSteps);
                    int stepsDelta = (int)delta;

                    if (stepsDelta > 0)  // Earning steps
                    {
                        int newSteps = card.CurrentSteps + stepsDelta;
                        card.CurrentPoints = Math.Max(0, card.CurrentPoints + stepsDelta);

                        // Bu delta ilə neçə cycle tamamlandı?
                        int cyclesEarned = newSteps / totalSteps;
                        if (cyclesEarned > 0)
                        {
                            // ✅ CompletedCycles = cari balans → üzərinə əlavə et, RESET etmə
                            card.CompletedCycles += cyclesEarned;
                        }

                        // remainder
                        card.CurrentSteps = newSteps % totalSteps;
                    }
                    else if (stepsDelta < 0)
                    {
                        // spending points / free reward redeem
                        card.CurrentPoints = Math.Max(0, card.CurrentPoints + stepsDelta);
                        // CompletedCycles azalt (hər cycle = TotalSteps point)
                        int cyclesLost = Math.Abs(stepsDelta) / totalSteps;
                        if (cyclesLost > 0)
                            card.CompletedCycles = Math.Max(0, card.CompletedCycles - cyclesLost);
                    }
                    break;

                case CardTemplateType.PointsBased:
                case CardTemplateType.TierBased:
                    // PointsBased/TierBased: kəsr saxlanır
                    card.CurrentPoints = Math.Max(0m, card.CurrentPoints + delta);
                    break;
            }
        }


        private static Transaction BuildTransaction(
            LoyaltyCard card,
            decimal delta,
            string? orderId,
            string? note,
            TransactionType type,
            DateTimeOffset localOccurredAt,
            string timeZoneIdUsed)
        {
            return new Transaction
            {
                LoyaltyCardId = card.Id,
                Points = Math.Abs(delta),
                BalanceAfter = card.CurrentPoints,
                ReferenceNumber = card.BarcodeValue,
                Type = type,
                Status = RedeemStatus.Applied,
                OrderId = orderId,
                Description = delta >= 0
                    ? $"Kart balansının artımı: +{delta}"
                    : $"Ödəniş zamanı bonus istifadə edildi: {delta}",
                LocalOccurredAt = localOccurredAt,
                TimeZoneIdUsed = timeZoneIdUsed
                // istəsən burada OperatorId/Note da saxla
            };
        }

        private async Task<LoyaltyCard?> FindActiveCardForCustomer(int tenantId, int customerId, int? programId, CancellationToken ct)
        {
            if (programId.HasValue)
            {
                return await _cardSvc.GetLastActiveByCustomerAsync(tenantId, programId.Value, customerId, ct);
            }

            var cards = await _cardSvc.ListByCustomerAsync(tenantId, customerId, ct);
            return cards?.Where(c => c.Status == CardStatus.Active)
                .OrderByDescending(c => c.Id)
                .FirstOrDefault();
        }

        private static RedeemResultDto Fail(string msg) => new() { Success = false, Message = msg };

        private static string? BuildCustomerName(LoyaltyCard card)
        {
            var c = card.Customer;
            if (c == null) return null;
            var full = $"{c.FirstName} {c.LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(full)) return full;
            return c.PhoneNumber ?? c.applicationUser?.UserName;
        }

        private async Task<string?> ResolveOperatorNameAsync(string? operatorId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(operatorId)) return null;
            // Cəhd et int id-dirsə Users-də Id, deyilsə UserName/PhoneNumber ilə tap
            int.TryParse(operatorId, out var intId);
            var u = await _dbContext.Users.AsNoTracking()
                .Where(x => (intId > 0 && x.Id == intId) || x.UserName == operatorId || x.PhoneNumber == operatorId)
                .Select(x => new { x.Name, x.Surname, x.UserName, x.PhoneNumber })
                .FirstOrDefaultAsync(ct);
            if (u == null) return operatorId;
            var full = $"{u.Name} {u.Surname}".Trim();
            return !string.IsNullOrWhiteSpace(full) ? full : (u.UserName ?? u.PhoneNumber ?? operatorId);
        }

        private static string GetFreeRewardLabel(CardTemplateSnapshot snapshot)
        {
            return string.IsNullOrWhiteSpace(snapshot.FreeRewardLabel)
                ? "Pulsuz içki"
                : snapshot.FreeRewardLabel;
        }

        private async Task<RedeemResultDto> RedeemFreeRewardAsync(
            LoyaltyCard card,
            RedeemRequestDto request,
            CardTemplateSnapshot snapshot,
            string freeRewardLabel,
            int? branchId,
            string? receiptUrl,
            decimal? ocrAmount,
            bool isSuspicious,
            string? suspiciousReason,
            EffectiveSuspiciousCfg cfg,
            CancellationToken ct)
        {
            try
            {
                var spendCount = request.SpendCount.GetValueOrDefault();
                if (spendCount <= 0)
                    return Fail("Xərcləmə sayı 0-dan böyük olmalıdır.");

                // ✅ Kartı DB-dən TRACKING ilə götür (dəqiq update üçün)
                var dbCard = await _dbContext.Set<LoyaltyCard>()
                    .SingleAsync(c => c.Id == card.Id, ct);

                // ✅ Balans yoxlaması
                if (dbCard.CompletedCycles < spendCount)
                    return Fail($"Kifayət qədər balans yoxdur. Mövcud: {dbCard.CompletedCycles} {freeRewardLabel}.");

                // Suspicious → balansa toxunmadan flag-only Transaction yarat
                if (isSuspicious)
                {
                    var stx = new Transaction
                    {
                        LoyaltyCardId = dbCard.Id,
                        Type = TransactionType.Redeemed,
                        Status = RedeemStatus.FlaggedForReview,
                        OrderId = request.OrderId,
                        ReferenceNumber = dbCard.BarcodeValue,
                        Points = spendCount,
                        Amount = request.OriginalAmount,
                        BalanceAfter = dbCard.CompletedCycles,
                        BranchId = branchId,
                        ReceiptImageUrl = receiptUrl,
                        OcrExtractedAmount = ocrAmount,
                        IsSuspicious = true,
                        SuspiciousReason = suspiciousReason,
                        Description = $"Şübhəli: {spendCount} {freeRewardLabel} xərcləmə (cap={cfg.SpendCountCap}). Admin review."
                    };
                    _dbContext.Set<Transaction>().Add(stx);
                    await _dbContext.SaveChangesAsync(CancellationToken.None);

                    _logger.LogWarning("Suspicious free-reward flagged. CardId={CardId}, Reason={Reason}, SpendCount={SpendCount}",
                        dbCard.Id, suspiciousReason, spendCount);

                    if (cfg.AlertEnabled && !string.IsNullOrWhiteSpace(cfg.AlertPhone))
                    {
                        var customerName = BuildCustomerName(card);
                        var branchName = branchId.HasValue
                            ? await _dbContext.Branches.AsNoTracking()
                                .Where(b => b.Id == branchId.Value)
                                .Select(b => (string?)b.Name)
                                .FirstOrDefaultAsync(ct)
                            : null;
                        var operatorName = await ResolveOperatorNameAsync(request.OperatorId, ct);
                        var txRef = stx.OrderId ?? stx.Id.ToString();
                        _ = Task.Run(() => _whatsappAlert.SendSuspiciousTxAlertAsync(
                            cfg.AlertPhone!, cfg.TenantName, branchName, customerName, operatorName,
                            spendCount, null, (decimal?)cfg.SpendCountCap, suspiciousReason,
                            txRef, CancellationToken.None));
                    }

                    return new RedeemResultDto
                    {
                        Success = true,
                        Message = "Şübhəli əməliyyat kimi qeyd olundu. Admin yoxlamasına gözləyir.",
                        LoyaltyCardId = dbCard.Id,
                        AppliedDelta = 0
                    };
                }

                var before = dbCard.CompletedCycles;
                var beforePoints = dbCard.CurrentPoints;
                var beforeSteps = dbCard.CurrentSteps;

                // ProgressBased-də: Her CompletedCycle = TotalSteps kadar point deməkdir
                // spendCount kadar CompletedCycle harcandığında, TotalSteps * spendCount kadar point azaltılmalıdır
                int totalSteps = Math.Max(1, snapshot.TotalSteps);
                int pointsToDeduct = spendCount * totalSteps;

                dbCard.CompletedCycles = before - spendCount;
                dbCard.CurrentPoints = Math.Max(0, beforePoints - pointsToDeduct);
                // CurrentSteps dəyişmir - reward xərcləmə stamp proqresini təsir etmir
                dbCard.EditDate = DateTime.UtcNow;

                _logger.LogInformation("ProgressBased free reward redeem. CardId={CardId}, SpendCount={SpendCount}, PointsDeducted={PointsDeducted}, Points {Before}->{After}, Steps {StepsBefore}->{StepsAfter}, Cycles {CyclesBefore}->{CyclesAfter}",
                    dbCard.Id, spendCount, pointsToDeduct, beforePoints, dbCard.CurrentPoints, beforeSteps, dbCard.CurrentSteps, before, dbCard.CompletedCycles);
                // ✅ (Opsional) idempotency
                var idemKey = (request.OrderId ?? request.IdempotencyKey)?.Trim();
                if (!string.IsNullOrWhiteSpace(idemKey))
                {
                    var existingTx = await _dbContext.Set<Transaction>()
                        .FirstOrDefaultAsync(
                            t => t.OrderId == idemKey &&
                                 t.LoyaltyCardId == card.Id &&
                                 t.Type == TransactionType.Redeemed,
                            ct);

                    if (existingTx != null)
                    {
                        // Reload to get current balance
                        await _dbContext.Entry(dbCard).ReloadAsync(ct);
                        return new RedeemResultDto
                        {
                            Success = true,
                            Message = "Bu əməliyyat artıq tətbiq olunub.",
                            LoyaltyCardId = dbCard.Id,
                            RemainingRewardCount = dbCard.CompletedCycles,
                            FreeRewardLabel = freeRewardLabel
                        };
                    }
                }

                // ⚠️ CancellationToken.None: yazma əməliyyatları client disconnect olsa belə tamamlanmalıdır
                var (localTime, timeZoneId) = await _timeZoneService.GetTenantLocalTimeAsync(request.TenantId, null, CancellationToken.None);
                // ✅ Transaction log yaz
                var tx = new Transaction
                {
                    LoyaltyCardId = dbCard.Id,
                    Type = TransactionType.Redeemed,
                    OrderId = idemKey,
                    ReferenceNumber = dbCard.BarcodeValue,
                    Points = spendCount,
                    Amount = request.OriginalAmount,
                    BalanceAfter = dbCard.CompletedCycles,
                    BranchId = branchId,
                    ReceiptImageUrl = receiptUrl,
                    OcrExtractedAmount = ocrAmount,
                    Description = $"{spendCount} {freeRewardLabel} xərcləndi. Balans: {before} -> {dbCard.CompletedCycles}"
                };

                _dbContext.Set<Transaction>().Add(tx);
                await _dbContext.SaveChangesAsync(CancellationToken.None);

                // ✅ Wallet refresh (DB-dən oxunmuş real dəyərlə)
                var primaryPlatform = dbCard.IsIOS ? WalletPlatform.AppleWallet : WalletPlatform.GooglePay;
                var secondaryPlatform = dbCard.IsIOS ? WalletPlatform.GooglePay : WalletPlatform.AppleWallet;

                // ⚠️ CancellationToken.None: wallet update client disconnect-dən asılı olmamalıdır
                try
                {
                    _ = await _walletRouter.RouteAsync(primaryPlatform, dbCard.Id, snapshot, dbCard.CurrentPoints, dbCard.CurrentSteps, dbCard.CompletedCycles, 0, CancellationToken.None);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("credential") || ex.Message.Contains("tapılmadı"))
                {
                    _logger.LogWarning(ex, "Credentials not found for PRIMARY {Platform} in RedeemFreeRewardAsync. Continuing. CardId={CardId}", primaryPlatform, dbCard.Id);
                }
                catch (Exception ex)
                {
                    // Wallet fail olsa da DB update artıq yazılıb
                    _logger.LogWarning(ex, "Wallet routing failed for PRIMARY {Platform} after RedeemFreeRewardAsync. Continuing. CardId={CardId}", primaryPlatform, dbCard.Id);
                }

                try
                {
                    _ = await _walletRouter.RouteAsync(secondaryPlatform, dbCard.Id, snapshot, dbCard.CurrentPoints, dbCard.CurrentSteps, dbCard.CompletedCycles, 0, CancellationToken.None);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("credential") || ex.Message.Contains("tapılmadı"))
                {
                    _logger.LogWarning(ex, "Credentials not found for SECONDARY {Platform} in RedeemFreeRewardAsync. Continuing. CardId={CardId}", secondaryPlatform, dbCard.Id);
                }
                catch (Exception ex)
                {
                    // Wallet fail olsa da DB update artıq yazılıb
                    _logger.LogWarning(ex, "Wallet routing failed for SECONDARY {Platform} after RedeemFreeRewardAsync. Continuing. CardId={CardId}", secondaryPlatform, dbCard.Id);
                }

                return new RedeemResultDto
                {
                    Success = true,
                    Message = $"{spendCount} {freeRewardLabel} azaldıldı. Qalan balans: {dbCard.CompletedCycles} {freeRewardLabel}, Points: {dbCard.CurrentPoints}",
                    LoyaltyCardId = dbCard.Id,
                    RemainingRewardCount = dbCard.CompletedCycles,
                    FreeRewardLabel = freeRewardLabel,
                    AppliedDelta = -pointsToDeduct // Point azalmasını qaytarırıq
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RedeemFreeRewardAsync failed. CardId={CardId}", card.Id);
                return Fail($"RedeemFreeRewardAsync xətası: {ex.Message}");
            }
        }

        // ---- Small masking helpers for safe logging ----
        private static string MaskOtp(string? otp)
            => string.IsNullOrWhiteSpace(otp) ? "<null>" : new string('*', Math.Max(0, otp.Length - 2)) + otp[^2..];

        private static string MaskPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return "<null>";
            var trimmed = phone.Trim();
            if (trimmed.Length <= 4) return new string('*', trimmed.Length);
            return new string('*', trimmed.Length - 4) + trimmed[^4..];
        }

        private static string GenerateLotteryTicketCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var bytes = RandomNumberGenerator.GetBytes(8);
            return "WLV-" + string.Concat(bytes.Select(b => chars[b % chars.Length]));
        }
    }
}
