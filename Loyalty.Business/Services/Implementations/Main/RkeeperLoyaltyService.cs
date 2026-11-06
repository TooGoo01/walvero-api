using Hangfire;
using Loyalty.Business.Helpers;
using Loyalty.Business.Models.TenantIntegration;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// r_keeper FarCards HTTP — Walvero kart serveridir (balans master bizdə, kassa cüzdanı yoxdur).
    /// Qazanc Kind=3 (qonağın öz pulu) üzrə Walvero qaydaları ilə, xalla ödəniş Kind=0 (mənfi) ilə.
    /// Çek mövcud POS sifariş emalına (ProcessIikoOrderEventAsync, SourcePrefix="rkeeper") map olunur —
    /// tier / happy hours / xoş gəldin bonusu / möhür / ləğv eyni yoldan keçir.
    /// </summary>
    public sealed class RkeeperLoyaltyService : IRkeeperLoyaltyService
    {
        public const string Source = "rkeeper";

        // TRRESPONSE error_code — kassir ekranında err_text ilə görünür (ANSI kassa → yalnız ASCII mətn)
        private const int ErrInvalidXml = 1;
        private const int ErrUnsupportedKind = 2;
        private const int ErrCardNotFound = 3;
        private const int ErrInsufficientBalance = 4;
        private const int ErrProcessing = 5;
        private const int ErrPaymentNotAllowed = 6;

        private readonly ICustomerRepository _customerRepo;
        private readonly ILoyaltyCardRepository _loyaltyCardRepo;
        private readonly ILoyaltyProgramRepository _loyaltyProgramRepo;
        private readonly ITenantIntegrationResolver _resolver;
        private readonly IWalletWebhookService _webhookService;
        private readonly IUnitOfWork _uow;
        private readonly Hangfire.IBackgroundJobClient _jobClient;
        private readonly ILogger<RkeeperLoyaltyService> _logger;

        public RkeeperLoyaltyService(
            ICustomerRepository customerRepo,
            ILoyaltyCardRepository loyaltyCardRepo,
            ILoyaltyProgramRepository loyaltyProgramRepo,
            ITenantIntegrationResolver resolver,
            IWalletWebhookService webhookService,
            IUnitOfWork uow,
            Hangfire.IBackgroundJobClient jobClient,
            ILogger<RkeeperLoyaltyService> logger)
        {
            _customerRepo = customerRepo;
            _loyaltyCardRepo = loyaltyCardRepo;
            _loyaltyProgramRepo = loyaltyProgramRepo;
            _resolver = resolver;
            _webhookService = webhookService;
            _uow = uow;
            _jobClient = jobClient;
            _logger = logger;
        }

        public async Task<string> GetCardInfoAsync(WalletWebhookResolvedContext context, string requestXml, CancellationToken ct = default)
        {
            var query = FarCardsXml.ParseCardQuery(requestXml);
            if (query == null)
            {
                _logger.LogWarning("rkeeper GetCardInfoEx: parse olunmadı. TenantId={TenantId}", context.TenantId);
                return FarCardsXml.BuildCardNotFound();
            }

            var card = await FindActiveCardAsync(context, query.Card, account: 0, ct);
            if (card == null)
            {
                _logger.LogInformation("rkeeper GetCardInfoEx NOT_FOUND. TenantId={TenantId} Card={Card} Restaurant={Restaurant} Unit={Unit}",
                    context.TenantId, query.Card, query.Restaurant, query.UnitNo);
                return FarCardsXml.BuildCardNotFound();
            }

            var customer = await _customerRepo.GetByIdAsync(card.CustomerId, ct);
            var program = await _loyaltyProgramRepo.GetByIdNameAsync(card.LoyaltyProgramId, ct);
            var templateType = program?.CardTemplate?.EffectiveType ?? CardTemplateType.PointsBased;

            var holder = $"{customer?.FirstName} {customer?.LastName}".Trim();
            if (holder.Length == 0) holder = customer?.PhoneNumber ?? card.CardNumber;

            bool locked = !card.IsActive || card.Status != CardStatus.Active;
            bool expired = card.IsPointsExpired();

            // Möhür kartında pul balansı yoxdur — kassa xalla ödəniş təklif etməsin (Summa=0)
            decimal balance = templateType == CardTemplateType.ProgressBased || expired ? 0m : card.CurrentPoints;
            string screen = templateType == CardTemplateType.ProgressBased
                ? $"Walvero: mohur {card.CurrentSteps}/{program?.CardTemplate?.TotalSteps ?? 10}, hediyye {card.CompletedCycles}"
                : $"Walvero balans: {balance:0.00} AZN{(expired ? " (muddeti bitib)" : "")}";

            _logger.LogInformation(
                "rkeeper GetCardInfoEx OK. TenantId={TenantId} Card={Card} CardId={CardId} Balance={Balance} Locked={Locked} Expired={Expired} Restaurant={Restaurant}",
                context.TenantId, query.Card, card.Id, balance, locked, expired, query.Restaurant);

            return FarCardsXml.BuildCardInfo(new FarCardsCardInfo(
                CardCode: query.Card,
                Account: card.Id,
                PersonId: card.CustomerId,
                Holder: holder,
                BalanceMinor: FarCardsXml.ToMinor(balance),
                Locked: locked,
                WhyLock: locked ? "Kart aktiv deyil" : null,
                ScreenMessage: screen,
                PrintMessage: screen));
        }

        public async Task<string> ProcessTransactionsAsync(WalletWebhookResolvedContext context, string requestXml, CancellationToken ct = default)
        {
            var transactions = FarCardsXml.ParseTransactions(requestXml);
            if (transactions == null)
            {
                _logger.LogWarning("rkeeper TransactionsEx: XML parse olunmadı. TenantId={TenantId}", context.TenantId);
                return FarCardsXml.BuildTransactionsError(ErrInvalidXml, "Walvero: invalid XML");
            }
            var inpBuf = FarCardsXml.ParseInpBufCheck(requestXml);
            List<FarCardsCheckSummary> checks;
            bool fromInpBuf = false;   // çek TransactionsEx sətirlərindən yox, INPBUF sənədindən oxunub
            bool validateOnly = false; // chmode 13: yalnız yoxla (balans), heç nə yazma

            if (transactions.Count == 0)
            {
                // r_keeper "Pass All Receipts XML data": <Transactions/> boş, çek INPBUF-dadır
                var fromBuf = MapInpBufCheck(context, inpBuf, out validateOnly);
                if (fromBuf == null)
                    return FarCardsXml.BuildTransactionsOk();
                checks = new List<FarCardsCheckSummary> { fromBuf };
                fromInpBuf = true;
            }
            else
            {
                checks = FarCardsCheck.Summarize(transactions);
                checks = SupplementOwnPaidFromInpBuf(context, checks, inpBuf);
            }

            // FarCards qismən icranı qəbul etmir (Result=0 hamısı / 1 heç biri) → əvvəl hamısını yoxla, sonra emal et
            var plan = new List<(FarCardsCheckSummary Check, LoyaltyCard Card, IikoOrderEventRequest Request, WalletWebhookResolvedContext Ctx)>();
            foreach (var check in checks)
            {
                if (check.HasUnknownKind)
                {
                    _logger.LogWarning("rkeeper TransactionsEx UNKNOWN_KIND. TenantId={TenantId} Check={Check} Kinds={Kinds}",
                        context.TenantId, check.CheckKey, string.Join(",", transactions.Select(t => t.Kind).Distinct()));
                    return FarCardsXml.BuildTransactionsError(ErrUnsupportedKind, "Walvero: unsupported transaction kind");
                }

                if (check.IgnoredMinor != 0)
                {
                    // Kind 1/2 — GetCardInfoEx Discount=0/Bonus=0 qaytarır, normalda gəlməməlidir (r_keeper tərəfli bonus konfiqi?)
                    _logger.LogWarning("rkeeper TransactionsEx: Kind 1/2 ignored. TenantId={TenantId} Check={Check} SumMinor={Sum}",
                        context.TenantId, check.CheckKey, check.IgnoredMinor);
                }

                if (check.BonusPaid == 0m && check.OwnPaid == 0m)
                    continue;

                var card = await FindActiveCardAsync(context, check.Card, check.Account, ct);
                if (card == null)
                {
                    _logger.LogWarning("rkeeper TransactionsEx CARD_NOT_FOUND. TenantId={TenantId} Card={Card} Account={Account} Check={Check} FromInpBuf={FromInpBuf}",
                        context.TenantId, check.Card, check.Account, check.CheckKey, fromInpBuf);
                    // "Bütün çeklər" rejimində yad kartlı çek də gələ bilər — balansdan ödəniş yoxdursa kassanı saxlama
                    if (fromInpBuf && (check.IsReturn || check.BonusPaid == 0m))
                        continue;
                    return FarCardsXml.BuildTransactionsError(ErrCardNotFound, "Walvero: card not found");
                }

                string orderId = $"{context.TenantIntegrationId}:{check.CheckKey}";
                // WalletWebhookService idempotency açarı ilə eyni format: {SourcePrefix}:{OrderId}:{EventType}
                string markerKey = $"{Source}:{orderId}:{(check.IsReturn ? "OrderReturned" : "OrderClosed")}";
                // Marker PosOrderMarkers-dadır (yeni); köhnə tarixçədə Transaction.OrderId kimi
                bool alreadyProcessed =
                    (await _uow.Repository<PosOrderMarker>().GetAllAsync(m => m.OrderKey == markerKey)).Any()
                    || (await _uow.Repository<Transaction>().GetAllAsync(t => t.OrderId == markerKey)).Any();

                // Xalla ödənişin avtorizasiyası (retry-də balans artıq çıxılıb → yoxlama atlanır, emal duplicate qaytarır)
                if (!check.IsReturn && check.BonusPaid > 0m && !alreadyProcessed)
                {
                    var program = await _loyaltyProgramRepo.GetByIdNameAsync(card.LoyaltyProgramId, ct);
                    var templateType = program?.CardTemplate?.EffectiveType ?? CardTemplateType.PointsBased;
                    if (templateType == CardTemplateType.ProgressBased || card.IsPointsExpired())
                        return FarCardsXml.BuildTransactionsError(ErrPaymentNotAllowed, "Walvero: bonus payment not allowed for this card");
                    if (card.CurrentPoints < check.BonusPaid)
                    {
                        _logger.LogWarning("rkeeper TransactionsEx INSUFFICIENT. TenantId={TenantId} CardId={CardId} Balance={Balance} Pay={Pay} Check={Check}",
                            context.TenantId, card.Id, card.CurrentPoints, check.BonusPaid, check.CheckKey);
                        return FarCardsXml.BuildTransactionsError(ErrInsufficientBalance,
                            $"Walvero: balans kifayet etmir ({card.CurrentPoints:0.00} AZN)");
                    }
                }

                var checkCtx = await WithBranchAsync(context, check.Restaurant, ct);
                plan.Add((check, card, MapToOrderEvent(check, card, orderId), checkCtx));
            }

            if (validateOnly)
                return FarCardsXml.BuildTransactionsOk();

            foreach (var (check, card, request, checkCtx) in plan)
            {
                // Balansdan ödəniş olan satış sinxron qalır: çıxılma dərhal yazılmalıdır (kassanın növbəti skanı / slipi
                // yeni balansı görsün, ikiqat xərcləmə pəncərəsi olmasın). Qalan hər şey (yalnız keşbek, qaytarma) —
                // ack-first: Hangfire "pos" növbəsi (öz serveri var, çek OCR worker-lərini tutmur).
                bool mustBeSync = !check.IsReturn && check.BonusPaid > 0m;
                if (!mustBeSync && TryEnqueue(context, check, card, request, checkCtx))
                    continue;

                var result = await _webhookService.ProcessIikoOrderEventAsync(checkCtx, request, ct);
                _logger.LogInformation(
                    "rkeeper TransactionsEx {Event}. TenantId={TenantId} CardId={CardId} Check={Check} Own={Own} BonusPaid={BonusPaid} BranchId={BranchId} Success={Success} Message={Message}",
                    request.EventType, context.TenantId, card.Id, check.CheckKey, check.OwnPaid, check.BonusPaid, checkCtx.BranchId, result.Success, result.Message);

                if (!result.Success)
                    return FarCardsXml.BuildTransactionsError(ErrProcessing, "Walvero: " + (result.Message ?? "processing failed"));
            }

            return FarCardsXml.BuildTransactionsOk();
        }

        /// <summary>Çeki Hangfire "pos" növbəsinə qoy. Növbəyə yazıla bilmədisə false → çağıran sinxron emal edir (çek itməsin).</summary>
        private bool TryEnqueue(WalletWebhookResolvedContext context, FarCardsCheckSummary check, LoyaltyCard card,
            IikoOrderEventRequest request, WalletWebhookResolvedContext checkCtx)
        {
            try
            {
                var jobId = _jobClient.Enqueue<RkeeperCheckJob>(j => j.ProcessAsync(checkCtx, request, CancellationToken.None));
                _logger.LogInformation(
                    "rkeeper TransactionsEx ENQUEUED. TenantId={TenantId} CardId={CardId} Event={Event} Check={Check} Own={Own} BonusPaid={BonusPaid} BranchId={BranchId} JobId={JobId}",
                    context.TenantId, card.Id, request.EventType, check.CheckKey, check.OwnPaid, check.BonusPaid, checkCtx.BranchId, jobId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "rkeeper TransactionsEx enqueue alınmadı — sinxron emal. TenantId={TenantId} Check={Check}",
                    context.TenantId, check.CheckKey);
                return false;
            }
        }

        /// <summary>
        /// INPBUF sənədi → çek xülasəsi. null = ediləcək iş yoxdur (çek deyil / kart yoxdur / məbləğ sıfır / kimlik yoxdur).
        /// chmode: 1 satış, 2 satışın geri alınması və 3 qaytarma → ləğv, 4 qaytarmanın geri alınması → satış yenidən
        /// (ayrıca açarla), 13 çekdən əvvəl → yalnız yoxlama.
        /// </summary>
        private FarCardsCheckSummary? MapInpBufCheck(WalletWebhookResolvedContext context, FarCardsInpBufCheck? inp, out bool validateOnly)
        {
            validateOnly = false;
            if (inp == null) return null;

            bool isReturn;
            string key = inp.CheckKey;
            switch (inp.ChMode)
            {
                case FarCardsChMode.Sale:
                    isReturn = false;
                    break;
                case FarCardsChMode.SaleRollback:
                case FarCardsChMode.Return:
                    isReturn = true;
                    break;
                case FarCardsChMode.ReturnRollback:
                    // Qaytarma ləğv olundu → çek yenidən qüvvədədir; ilkin OrderClosed markeri artıq var, ona görə ayrıca açar
                    isReturn = false;
                    if (key.Length > 0) key += "-rr";
                    break;
                case FarCardsChMode.BeforeReceipt:
                    isReturn = false;
                    validateOnly = true;
                    break;
                default:
                    return null; // sifariş redaktəsi, preçek, məlumat çapı və s.
            }

            if (inp.Card.Length == 0 || (inp.OwnPaid == 0m && inp.BonusPaid == 0m))
                return null;

            if (key.Length == 0)
            {
                if (validateOnly)
                    key = "precheck"; // yazılmır, yalnız balans yoxlanır
                else
                {
                    _logger.LogWarning("rkeeper TransactionsEx INPBUF: çek kimliyi yoxdur (checkguid/checknum), atlandı. TenantId={TenantId} Card={Card} ChMode={ChMode}",
                        context.TenantId, inp.Card, inp.ChMode);
                    return null;
                }
            }

            _logger.LogInformation(
                "rkeeper TransactionsEx INPBUF. TenantId={TenantId} ChMode={ChMode} Card={Card} Own={Own} BonusPaid={BonusPaid} Restaurant={Restaurant} CheckNum={CheckNum} Check={Check}",
                context.TenantId, inp.ChMode, inp.Card, inp.OwnPaid, inp.BonusPaid, inp.Restaurant, inp.CheckNum, key);

            return new FarCardsCheckSummary
            {
                CheckKey = key,
                Card = inp.Card,
                Account = 0,
                Restaurant = inp.Restaurant,
                RkCheck = inp.CheckNum,
                BonusPaid = inp.BonusPaid,
                OwnPaid = inp.OwnPaid,
                IsReturn = isReturn
            };
        }

        /// <summary>
        /// Sətirlərdə yalnız balansdan ödəniş (Kind 0) gəlib, qonağın öz pulu (Kind 3) yoxdursa — keşbek bazasını
        /// INPBUF-dakı nağd/bank ödənişlərindən götür. Yalnız tək çek və eyni kart üçün (səhv uyğunlaşdırma olmasın).
        /// </summary>
        private List<FarCardsCheckSummary> SupplementOwnPaidFromInpBuf(
            WalletWebhookResolvedContext context, List<FarCardsCheckSummary> checks, FarCardsInpBufCheck? inp)
        {
            if (inp == null || inp.ChMode != FarCardsChMode.Sale || inp.OwnPaid <= 0m || checks.Count != 1)
                return checks;
            var c = checks[0];
            if (c.IsReturn || c.OwnPaid != 0m || c.HasUnknownKind) return checks;
            if (inp.Card.Length > 0 && c.Card.Length > 0 && !string.Equals(inp.Card, c.Card, StringComparison.Ordinal)) return checks;

            _logger.LogInformation("rkeeper TransactionsEx: OwnPaid INPBUF-dan tamamlandı. TenantId={TenantId} Check={Check} Own={Own}",
                context.TenantId, c.CheckKey, inp.OwnPaid);
            return new List<FarCardsCheckSummary>
            {
                new FarCardsCheckSummary
                {
                    CheckKey = c.CheckKey,
                    Card = c.Card,
                    Account = c.Account,
                    Restaurant = c.Restaurant,
                    RkCheck = c.RkCheck,
                    BonusPaid = c.BonusPaid,
                    OwnPaid = inp.OwnPaid,
                    IsReturn = c.IsReturn,
                    IgnoredMinor = c.IgnoredMinor,
                    HasUnknownKind = c.HasUnknownKind
                }
            };
        }

        /// <summary>
        /// FarCards çeki → POS sifariş event-i. TotalSum = öz pulu + xalla ödəniş; xalla ödəniş Credential-lı
        /// payment kimi gedir → earnBase = öz pulu, balansdan BonusPaid çıxılır. Məhsul sətirləri Faza 2-də (soOrderData).
        /// </summary>
        public static IikoOrderEventRequest MapToOrderEvent(FarCardsCheckSummary check, LoyaltyCard card, string orderId)
        {
            var request = new IikoOrderEventRequest
            {
                SourcePrefix = Source,
                EventType = check.IsReturn ? "OrderReturned" : "OrderClosed",
                OrderId = orderId,
                OrderNumber = int.TryParse(check.RkCheck, out var n) ? n : null,
                TotalSum = check.OwnPaid + check.BonusPaid,
                FullSum = check.OwnPaid + check.BonusPaid,
                // CardNumber qlobal unique — WalletWebhookService axtarış zəncirində ilk dəqiq uyğunluq
                CustomerCardNumber = card.CardNumber
            };
            if (!check.IsReturn && check.BonusPaid > 0m)
            {
                request.Payments.Add(new IikoOrderPayment
                {
                    PaymentTypeName = "Walvero",
                    Sum = check.BonusPaid,
                    Credential = check.Card
                });
            }
            return request;
        }

        /// <summary>
        /// Kart axtarışı: Account (bizim qaytardığımız LoyaltyCard.Id) → CardNumber/Barkod → WalveroId → telefon → son N rəqəm.
        /// FarCards kartı Int64 kimi ötürür (öndəki sıfırlar itir) → rəqəmli identifikator lazımdır, WalveroId tövsiyə olunur.
        /// </summary>
        private async Task<LoyaltyCard?> FindActiveCardAsync(WalletWebhookResolvedContext context, string code, long account, CancellationToken ct)
        {
            code = (code ?? "").Trim();

            if (account is > 0 and <= int.MaxValue)
            {
                var byAccount = await _loyaltyCardRepo.GetById((int)account, ct);
                if (byAccount != null && byAccount.TenantId == context.TenantId)
                    return await ActiveOfAsync(context.TenantId, byAccount, ct);
            }

            if (code.Length == 0) return null;

            var byCode = await _loyaltyCardRepo.GetByCardNumberAsync(context.TenantId, code, ct)
                         ?? await _loyaltyCardRepo.GetByBarcodeValueAsync(context.TenantId, code, ct);
            if (byCode != null)
                return await ActiveOfAsync(context.TenantId, byCode, ct);

            if (long.TryParse(code, out var walveroId))
            {
                var byWalvero = await _customerRepo.GetByWalveroIdAsync(context.TenantId, walveroId, ct);
                if (byWalvero != null)
                    return await _loyaltyCardRepo.GetActiveByCustomerAsync(context.TenantId, byWalvero.Id, ct);
            }

            // Telefon yalnız tam formada (994XXXXXXXXX / 0XXXXXXXXX) — 9 rəqəmli WalveroId ilə qarışmasın
            if ((code.StartsWith("994") && code.Length == 12) || (code.StartsWith("0") && code.Length == 10))
            {
                var e164 = PhoneNormalizer.ToE164(code);
                if (e164 != null)
                {
                    foreach (var variant in PhoneNormalizer.Variants(e164))
                    {
                        var byPhone = await _customerRepo.GetByPhoneAsync(context.TenantId, variant, ct);
                        if (byPhone != null)
                            return await _loyaltyCardRepo.GetActiveByCustomerAsync(context.TenantId, byPhone.Id, ct);
                    }
                }
            }

            if (context.CardTrackMode == PosCardTrackMode.LastDigits && PosCardTrack.IsShortCode(code, context.CardTrackDigits))
            {
                var (byShort, matches) = await _loyaltyCardRepo.GetByBarcodeSuffixAsync(context.TenantId, code, ct);
                if (matches == 1 && byShort != null)
                    return await ActiveOfAsync(context.TenantId, byShort, ct);
            }

            return null;
        }

        // Köhnə (deaktiv) kart skan olunubsa müştərinin aktiv kartına keç; aktiv yoxdursa tapılanı qaytar (Locked göstərilir)
        private async Task<LoyaltyCard?> ActiveOfAsync(int tenantId, LoyaltyCard found, CancellationToken ct)
        {
            if (found.IsActive && found.Status == CardStatus.Active) return found;
            return await _loyaltyCardRepo.GetActiveByCustomerAsync(tenantId, found.CustomerId, ct) ?? found;
        }

        /// <summary>r_keeper restoran kodu → TenantIntegrationVenue (ExternalVenueId) → BranchId (eyni inteqrasiyadırsa).</summary>
        private async Task<WalletWebhookResolvedContext> WithBranchAsync(WalletWebhookResolvedContext context, string? restaurant, CancellationToken ct)
        {
            if (context.BranchId.HasValue || string.IsNullOrWhiteSpace(restaurant))
                return context;

            var venueCtx = await _resolver.ResolveByVenueAndSourceAsync(restaurant.Trim(), Source, ct);
            if (venueCtx == null || venueCtx.TenantIntegrationId != context.TenantIntegrationId || !venueCtx.BranchId.HasValue)
                return context;

            return new WalletWebhookResolvedContext
            {
                TenantId = context.TenantId,
                LoyaltyProgramId = context.LoyaltyProgramId,
                Source = context.Source,
                TenantIntegrationId = context.TenantIntegrationId,
                ExternalVenueId = venueCtx.ExternalVenueId,
                BranchId = venueCtx.BranchId,
                TenantIntegrationVenueId = venueCtx.TenantIntegrationVenueId,
                CardTrackMode = context.CardTrackMode,
                CardTrackDigits = context.CardTrackDigits
            };
        }
    }
}
