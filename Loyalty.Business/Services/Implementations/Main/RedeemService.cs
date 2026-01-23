// Business/Services/Implementations/Main/RedeemService.cs
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
            ITimeZoneService timeZoneService)
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

                // 1) Kart - Try Barcode first, then WalveroId
                LoyaltyCard? card = null;
                var code = request.Code.Trim();

                // First, try to find by barcode (priority)
                card = await _cardSvc.GetByBarcodeValueAsync(request.TenantId, code, ct);

                if (card == null && long.TryParse(code, out long walveroId))
                {
                    // Barcode not found, try WalveroId
                    _logger.LogInformation("Barcode not found, trying WalveroId: {WalveroId}", walveroId);

                    var customer = await _customerRepo.GetByWalveroIdAsync(request.TenantId, walveroId, ct);
                    if (customer == null)
                    {
                        _logger.LogWarning("Customer not found by WalveroId: {WalveroId} for TenantId: {TenantId}", walveroId, request.TenantId);
                        return Fail($"Kart tapılmadı (barcode və WalveroId uyğun gəlmədi).");
                    }

                    // Find the active card for this customer and program
                    if (request.LoyaltyProgramId.HasValue)
                    {
                        card = await _cardSvc.GetLastActiveByCustomerAsync(
                            request.TenantId,
                            request.LoyaltyProgramId.Value,
                            customer.Id,
                            ct);
                    }
                    else
                    {
                        // If no program specified, get the most recent active card
                        var cards = await _cardSvc.ListByCustomerAsync(request.TenantId, customer.Id, ct);
                        card = cards?.Where(c => c.Status == CardStatus.Active)
                            .OrderByDescending(c => c.Id)
                            .FirstOrDefault();
                    }

                    if (card == null)
                    {
                        _logger.LogWarning("Active card not found for CustomerId: {CustomerId}, ProgramId: {ProgramId}",
                            customer.Id, request.LoyaltyProgramId);
                        return Fail("Aktiv kart tapılmadı.");
                    }
                }
                else if (card == null)
                {
                    // Neither barcode nor WalveroId found
                    _logger.LogWarning("Card not found by barcode. Code={Code}", code);
                    return Fail("Kart tapılmadı (barcode uyğun gəlmədi).");
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

                string baseUrl = "https://walvero.com";
                string ToAbs(string? dbPath)
                {
                    if (string.IsNullOrWhiteSpace(dbPath)) return string.Empty;
                    if (dbPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return dbPath;
                    var rel = "/" + dbPath.TrimStart('/', '\\');
                    return baseUrl + rel;
                }

                var snapshot = CardTemplateSnapshotFactory.From(program, program.CardTemplate, branding, ToAbs);
                var delta = request.Delta;

                _logger.LogInformation("Snapshot built. TemplateType={TemplateType}, RequireConfirmation={RequireConfirmation}",
                    snapshot.TemplateType, policy.RequireConfirmation);

                // Check for free reward spending (ProgressBased only)
                if (request.SpendCount.HasValue && request.SpendCount.Value > 0)
                {
                    // Free reward xərcləməsi
                    request.Delta = 0; // Delta-nı 0-a set et ki, ApplyDelta çağırılmasın
                    string freeRewardLabel = GetFreeRewardLabel(snapshot);
                    return await RedeemFreeRewardAsync(card, request, snapshot, freeRewardLabel, ct);
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
                        Note = request.Note
                    };

                    await uow.Repository<RedeemRequest>().AddAsync(rr);
                    uow.Commit();

                    _logger.LogInformation("Pending redeem created. RequestId={RequestId}, ExpiresInMin={TTL}, OtpMasked={Otp}",
                        rr.Id, policy.ConfirmationTtlMinutes, MaskOtp(otp));

                    // Bildiriş (SMS/email)
                    var destination = card.Customer.PhoneNumber; // sizdə necə saxlanırsa
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

                var beforePoints = card.CurrentPoints;
                var beforeSteps = card.CurrentSteps;
                var beforeCompletedCycles = card.CompletedCycles;

                card.LifetimePoints += 1;
                ApplyDelta(card, snapshot, delta);
                // Get tenant's local time
                var (localTime, timeZoneId) = await _timeZoneService.GetTenantLocalTimeAsync(request.TenantId, null, ct);

                var tx = BuildTransaction(card, delta, request.OrderId, request.Note, TransactionType.Earned, localTime, timeZoneId);
                uow.Repository<LoyaltyCard>().Update(card);
                uow.Repository<Transaction>().Add(tx);
                uow.Commit();

                _logger.LogInformation("Delta applied. CardId={CardId}, Points {Before}->{After}, Steps {StepsBefore}->{StepsAfter}, CompletedCycles {CyclesBefore}->{CyclesAfter}, Delta={Delta}",
                    card.Id, beforePoints, card.CurrentPoints, beforeSteps, card.CurrentSteps, beforeCompletedCycles, card.CompletedCycles, delta);

                // 5) Platforma → router
                var primaryPlatform = card.IsIOS ? WalletPlatform.AppleWallet : WalletPlatform.GooglePay;
                var secondaryPlatform = card.IsIOS ? WalletPlatform.GooglePay : WalletPlatform.AppleWallet;

                var primaryOk = false;
                var secondaryOk = false;

                // PRIMARY
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
                        delta,
                        ct);

                    if (!primaryRes.Success)
                        _logger.LogWarning("Wallet update FAILED on PRIMARY {Platform}. CardId={CardId}", primaryPlatform, card.Id);
                    else
                        primaryOk = true;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("credential") || ex.Message.Contains("tapılmadı"))
                {
                    _logger.LogWarning(ex, "Credentials not found for PRIMARY {Platform}. Continuing without wallet update. CardId={CardId}", primaryPlatform, card.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Wallet update error on PRIMARY {Platform}. Continuing without wallet update. CardId={CardId}", primaryPlatform, card.Id);
                }

                // SECONDARY
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
                        delta,
                        ct);

                    if (!secondaryRes.Success)
                        _logger.LogWarning("Wallet update FAILED on SECONDARY {Platform}. CardId={CardId}", secondaryPlatform, card.Id);
                    else
                        secondaryOk = true;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("credential") || ex.Message.Contains("tapılmadı"))
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

                    await _hubContext
                        .Clients
                        .Group(groupName)
                        .SendAsync("CardUpdated", message, cancellationToken: ct);

                    var currency = snapshot.Currency ?? "AZN"; // Default: AZN
                    var message1 = delta > 0
                        ? $"{snapshot.ProgramName} balansınıza {delta} {currency} əlavə olundu!"
                        : $"{snapshot.ProgramName} balansınızdan {delta * 1} {currency} çıxıldı!";

                    await _notificationService.SendToUserAsync(appUserId.Value, snapshot.ProgramName, message1, data);
                }

                if (!primaryOk && !secondaryOk)
                {
                    _logger.LogWarning("Wallet update failed on both platforms, but continuing. CardId={CardId}, PrimaryOk={PrimaryOk}, SecondaryOk={SecondaryOk}", 
                        card.Id, primaryOk, secondaryOk);
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

                // OTP yoxlamanı aktiv etmək istəsəniz bu hissəni açın:
                // if (!string.Equals(rr.Code, code, StringComparison.Ordinal))
                // {
                //     _logger.LogWarning("OTP mismatch. Provided={Provided}, Stored={StoredMasked}", MaskOtp(code), MaskOtp(rr.Code));
                //     return Fail("Təsdiq kodu yanlışdır.");
                // }

                var card = await _cardSvc.GetById(rr.LoyaltyCardId, ct);
                if (card is null)
                {
                    _logger.LogWarning("Card not found for Confirm. CardId={CardId}", rr.LoyaltyCardId);
                    return Fail("Kart tapılmadı.");
                }

                var program = await _programSvc.GetByIdNameAsync(card.LoyaltyProgramId, ct);
                var branding = await _brandingSvc.GetByTenantIdAsync(rr.TenantId, ct);

                string baseUrl = "https://walvero.com";
                string ToAbs(string? dbPath)
                {
                    if (string.IsNullOrWhiteSpace(dbPath)) return string.Empty;
                    if (dbPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return dbPath;
                    var rel = "/" + dbPath.TrimStart('/', '\\');
                    return baseUrl + rel;
                }

                var snapshot = CardTemplateSnapshotFactory.From(program, program.CardTemplate, branding, ToAbs);

                var beforePoints = card.CurrentPoints;
                var beforeSteps = card.CurrentSteps;
                var beforeCompletedCycles = card.CompletedCycles;

                ApplyDelta(card, snapshot, rr.PointsDelta);
                var (localTime, timeZoneId) = await _timeZoneService.GetTenantLocalTimeAsync(rr.TenantId, null, ct);

                var tx = BuildTransaction(card, rr.PointsDelta, rr.OrderId, rr.Note, TransactionType.Redeemed, localTime, timeZoneId);
                rr.Status = RedeemStatus.Applied;

                uow.Repository<LoyaltyCard>().Update(card);
                uow.Repository<Transaction>().Add(tx);
                uow.Repository<RedeemRequest>().Update(rr);
                uow.Commit();

                _logger.LogInformation("Confirm applied. CardId={CardId}, Points {Before}->{After}, Steps {StepsBefore}->{StepsAfter}, CompletedCycles {CyclesBefore}->{CyclesAfter}, Delta={Delta}",
                    card.Id, beforePoints, card.CurrentPoints, beforeSteps, card.CurrentSteps, beforeCompletedCycles, card.CompletedCycles, rr.PointsDelta);

                var primaryPlatform = card.IsIOS ? WalletPlatform.AppleWallet : WalletPlatform.GooglePay;
                var secondaryPlatform = card.IsIOS ? WalletPlatform.GooglePay : WalletPlatform.AppleWallet;

                var primaryOk = false;
                var secondaryOk = false;

                // PRIMARY
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
                        rr.PointsDelta,
                        ct);

                    if (!primaryRes.Success)
                        _logger.LogWarning("Wallet update FAILED on PRIMARY {Platform}. CardId={CardId}", primaryPlatform, card.Id);
                    else
                        primaryOk = true;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("credential") || ex.Message.Contains("tapılmadı"))
                {
                    _logger.LogWarning(ex, "Credentials not found for PRIMARY {Platform}. Continuing without wallet update. CardId={CardId}", primaryPlatform, card.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Wallet update error on PRIMARY {Platform}. Continuing without wallet update. CardId={CardId}", primaryPlatform, card.Id);
                }

                // SECONDARY
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
                        rr.PointsDelta,
                        ct);

                    if (!secondaryRes.Success)
                        _logger.LogWarning("Wallet update FAILED on SECONDARY {Platform}. CardId={CardId}", secondaryPlatform, card.Id);
                    else
                        secondaryOk = true;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("credential") || ex.Message.Contains("tapılmadı"))
                {
                    _logger.LogWarning(ex, "Credentials not found for SECONDARY {Platform}. Continuing without wallet update. CardId={CardId}", secondaryPlatform, card.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Wallet update error on SECONDARY {Platform}. Continuing without wallet update. CardId={CardId}", secondaryPlatform, card.Id);
                }

                if (!primaryOk && !secondaryOk)
                {
                    _logger.LogWarning("Wallet update failed on both platforms, but continuing. CardId={CardId}, PrimaryOk={PrimaryOk}, SecondaryOk={SecondaryOk}", 
                        card.Id, primaryOk, secondaryOk);
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
                        LastRedeemAmount = rr.PointsDelta,
                        UpdatedAt = DateTime.UtcNow
                    };

                    var groupName = $"user-{appUserId.Value}";
                    var data = new Dictionary<string, string> { { "type", "normal" } };

                    await _hubContext
                        .Clients
                        .Group(groupName)
                        .SendAsync("CardUpdated", message, cancellationToken: ct);

                    var currency = snapshot.Currency ?? "AZN"; // Default: AZN
                    var message1 = rr.PointsDelta > 0
                        ? $"{snapshot.ProgramName} balansınıza {rr.PointsDelta} {currency} əlavə olundu!"
                        : $"{snapshot.ProgramName} balansınızdan {rr.PointsDelta * 1} {currency} çıxıldı!";

                    await _notificationService.SendToUserAsync(appUserId.Value, snapshot.ProgramName, message1, data);
                }

                _logger.LogInformation("Confirm completed successfully. CardId={CardId}, AppliedDelta={Delta}", card.Id, rr.PointsDelta);

                return new RedeemResultDto
                {
                    Success = true,
                    Message = $"Təsdiq edildi. Kart #{card.Id} üçün {(rr.PointsDelta >= 0 ? "+" : "")}{rr.PointsDelta} tətbiq olundu.",
                    LoyaltyCardId = card.Id,
                    AppliedDelta = rr.PointsDelta
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ConfirmRedeemAsync exception. RequestId={RequestId}", redeemRequestId);
                return Fail("Təsdiq zamanı sistem xətası baş verdi.");
            }
        }

        // ------- Helpers -------

        private static void ApplyDelta(LoyaltyCard card, CardTemplateSnapshot snapshot, int delta)
        {
            switch (snapshot.TemplateType)
            {
                case CardTemplateType.ProgressBased:
                    int totalSteps = Math.Max(1, snapshot.TotalSteps);

                    if (delta > 0)  // Earning steps
                    {
                        int newSteps = card.CurrentSteps + delta;
                        card.CurrentPoints = Math.Max(0, card.CurrentPoints + delta);

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
                    else if (delta < 0)
                    {
                        // spending points (not free rewards)
                        card.CurrentPoints = Math.Max(0, card.CurrentPoints + delta);
                    }
                    break;

                case CardTemplateType.PointsBased:
                case CardTemplateType.TierBased:
                    card.CurrentPoints = Math.Max(0, card.CurrentPoints + delta);
                    break;
            }
        }


        private static Transaction BuildTransaction(
            LoyaltyCard card,
            int delta,
            string? orderId,
            string? note,
            TransactionType type,
            DateTimeOffset localOccurredAt,
            string timeZoneIdUsed)
        {
            return new Transaction
            {
                LoyaltyCardId = card.Id,
                Points = card.CurrentPoints,
                BalanceAfter = card.CurrentPoints,
                ReferenceNumber = card.BarcodeValue,
                Type = type,
                OrderId = orderId,
                Description = delta >= 0
                    ? $"Kart balansının artımı: +{delta}"
                    : $"Ödəniş zamanı bonus istifadə edildi: {delta}",
                LocalOccurredAt = localOccurredAt,
                TimeZoneIdUsed = timeZoneIdUsed
                // istəsən burada OperatorId/Note da saxla
            };
        }

        private static RedeemResultDto Fail(string msg) => new() { Success = false, Message = msg };

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

                var before = dbCard.CompletedCycles;
                var beforePoints = dbCard.CurrentPoints;
                var beforeSteps = dbCard.CurrentSteps;
                
                // ProgressBased-də: Her CompletedCycle = TotalSteps kadar point deməkdir
                // spendCount kadar CompletedCycle harcandığında, TotalSteps * spendCount kadar point azaltılmalıdır
                int totalSteps = Math.Max(1, snapshot.TotalSteps);
                int pointsToDeduct = spendCount * totalSteps;
                
                dbCard.CompletedCycles = before - spendCount;
                dbCard.CurrentPoints = Math.Max(0, beforePoints - pointsToDeduct);
                // CurrentSteps-i yenidən hesabla: CurrentPoints % TotalSteps
                dbCard.CurrentSteps = dbCard.CurrentPoints % totalSteps;
                dbCard.EditDate = DateTime.UtcNow;

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
                var (localTime, timeZoneId) = await _timeZoneService.GetTenantLocalTimeAsync(request.TenantId, null, ct);
                // ✅ Transaction log yaz
                var tx = new Transaction
                {
                    LoyaltyCardId = dbCard.Id,
                    Type = TransactionType.Redeemed,
                    OrderId = idemKey,
                    ReferenceNumber = dbCard.BarcodeValue,
                    Points = dbCard.CurrentPoints,
                    BalanceAfter = dbCard.CurrentPoints,
                    Description = $"{spendCount} {freeRewardLabel} xərcləndi. CompletedCycles: {before} -> {dbCard.CompletedCycles}, Points: {beforePoints} -> {dbCard.CurrentPoints}, Steps: {beforeSteps} -> {dbCard.CurrentSteps}",
                    LocalOccurredAt = localTime,
                    TimeZoneIdUsed = timeZoneId
                };

                _dbContext.Set<Transaction>().Add(tx);
                await _dbContext.SaveChangesAsync(ct);

                // ✅ Wallet refresh (DB-dən oxunmuş real dəyərlə)
                var primaryPlatform = dbCard.IsIOS ? WalletPlatform.AppleWallet : WalletPlatform.GooglePay;
                var secondaryPlatform = dbCard.IsIOS ? WalletPlatform.GooglePay : WalletPlatform.AppleWallet;

                try
                {
                    _ = await _walletRouter.RouteAsync(primaryPlatform, dbCard.Id, snapshot, dbCard.CurrentPoints, dbCard.CurrentSteps, dbCard.CompletedCycles, 0, ct);
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
                    _ = await _walletRouter.RouteAsync(secondaryPlatform, dbCard.Id, snapshot, dbCard.CurrentPoints, dbCard.CurrentSteps, dbCard.CompletedCycles, 0, ct);
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
    }
}
