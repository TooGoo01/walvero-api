using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Loyalty.Business.Dtos.Telegram;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class TelegramBotService : ITelegramBotService
    {
        private const string LinkCodeCachePrefix = "tg-link:";
        private const string RateCachePrefix = "tg-rate:";
        private const int LinkCodeTtlMinutes = 10;
        private const int MaxLookupsPerMinute = 20;
        private const int MaxMatches = 5; // 6-cı nəticə "çox nəticə" deməkdir
        private const int RecentRedeemCount = 10;

        private static readonly Regex DigitsRegex = new(@"^\d{5,20}$", RegexOptions.Compiled);

        private readonly IMemoryCache _cache;
        private readonly ITelegramAdminLinkRepository _linkRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ITelegramSender _sender;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITelegramSettingsProvider _settingsProvider;
        private readonly ILogger<TelegramBotService> _logger;

        public TelegramBotService(
            IMemoryCache cache,
            ITelegramAdminLinkRepository linkRepository,
            ICustomerRepository customerRepository,
            ITransactionRepository transactionRepository,
            ITelegramSender sender,
            IUnitOfWork unitOfWork,
            ITelegramSettingsProvider settingsProvider,
            ILogger<TelegramBotService> logger)
        {
            _cache = cache;
            _linkRepository = linkRepository;
            _customerRepository = customerRepository;
            _transactionRepository = transactionRepository;
            _sender = sender;
            _unitOfWork = unitOfWork;
            _settingsProvider = settingsProvider;
            _logger = logger;
        }

        // ------------------- Link kodu (admin panel tərəfi) -------------------

        public async Task<TelegramLinkCodeResult> GenerateLinkCodeAsync(int applicationUserId, int tenantId, CancellationToken ct = default)
        {
            var code = GenerateCode(8);
            _cache.Set(LinkCodeCachePrefix + code, (applicationUserId, tenantId),
                TimeSpan.FromMinutes(LinkCodeTtlMinutes));

            var settings = await _settingsProvider.GetAsync(ct);
            return new TelegramLinkCodeResult
            {
                Code = code,
                DeepLink = $"https://t.me/{settings.BotUsername}?start={code}",
                ExpiresInSeconds = LinkCodeTtlMinutes * 60
            };
        }

        public async Task<bool> HasActiveLinkAsync(int applicationUserId, CancellationToken ct = default)
            => await _linkRepository.GetActiveByUserAsync(applicationUserId, ct) != null;

        public Task UnlinkUserAsync(int applicationUserId, CancellationToken ct = default)
            => _linkRepository.DeactivateByUserAsync(applicationUserId, ct);

        // ------------------- Webhook update routing -------------------

        public async Task HandleUpdateAsync(TelegramUpdateDto update, CancellationToken ct = default)
        {
            if (update.CallbackQuery != null)
            {
                await HandleCallbackAsync(update.CallbackQuery, ct);
                return;
            }

            var message = update.Message;
            var chatId = message?.Chat?.Id;
            var text = message?.Text?.Trim();
            if (chatId == null || string.IsNullOrEmpty(text))
                return;

            if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
            {
                var code = text.Length > 6 ? text[6..].Trim() : string.Empty;
                await HandleStartAsync(chatId.Value, code, message!.From?.Username, ct);
                return;
            }

            if (text.Equals("/stop", StringComparison.OrdinalIgnoreCase))
            {
                await _linkRepository.DeactivateByChatIdAsync(chatId.Value, ct);
                await _sender.SendMessageAsync(chatId.Value,
                    "Telegram hesabınız ayrıldı. Yenidən bağlamaq üçün admin paneldən kod alın.", ct: ct);
                return;
            }

            if (DigitsRegex.IsMatch(text))
            {
                await HandleLookupAsync(chatId.Value, text, ct);
                return;
            }

            await _sender.SendMessageAsync(chatId.Value,
                "Müştəri axtarışı üçün kartın və ya Walvero ID-nin <b>son 5 rəqəmini</b> göndərin.\n" +
                "Hesabı ayırmaq üçün: /stop", ct: ct);
        }

        // ------------------- /start CODE -------------------

        private async Task HandleStartAsync(long chatId, string code, string? telegramUsername, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(code))
            {
                await _sender.SendMessageAsync(chatId,
                    "Salam! Bu bot tenant adminlər üçündür.\n" +
                    "Bağlanmaq üçün admin paneldən \"Telegrama bağla\" bölməsindən kod alın və " +
                    "<code>/start KOD</code> göndərin.", ct: ct);
                return;
            }

            var cacheKey = LinkCodeCachePrefix + code.ToUpperInvariant();
            if (!_cache.TryGetValue<(int userId, int tenantId)>(cacheKey, out var linkInfo))
            {
                await _sender.SendMessageAsync(chatId, "Kod yanlış və ya vaxtı bitib. Admin paneldən yeni kod alın.", ct: ct);
                return;
            }

            _cache.Remove(cacheKey); // single-use

            // Köhnə linkləri əvəz et: həm bu chat-ın, həm bu user-in
            await _linkRepository.DeactivateByChatIdAsync(chatId, ct);
            await _linkRepository.DeactivateByUserAsync(linkInfo.userId, ct);

            await _linkRepository.AddAsync(new TelegramAdminLink
            {
                ApplicationUserId = linkInfo.userId,
                TenantId = linkInfo.tenantId,
                ChatId = chatId,
                TelegramUsername = telegramUsername,
                LinkedAt = DateTime.UtcNow,
                RegDate = DateTime.UtcNow,
                RegUserId = linkInfo.userId,
                IsActive = true
            });
            await _unitOfWork.CommitAsync(ct);

            await _sender.SendMessageAsync(chatId,
                "Hesabınız uğurla bağlandı ✅\n" +
                "Müştəri axtarışı üçün kartın və ya Walvero ID-nin <b>son 5 rəqəmini</b> göndərin.", ct: ct);
        }

        // ------------------- Axtarış -------------------

        private async Task HandleLookupAsync(long chatId, string digits, CancellationToken ct)
        {
            var link = await _linkRepository.GetActiveByChatIdAsync(chatId, ct);
            if (link == null)
            {
                await _sender.SendMessageAsync(chatId,
                    "Əvvəlcə admin paneldən Telegram hesabınızı bağlayın (\"Telegrama bağla\" → kod → /start KOD).", ct: ct);
                return;
            }

            if (IsRateLimited(chatId))
            {
                await _sender.SendMessageAsync(chatId, "Çox sorğu göndərildi, bir dəqiqə gözləyin.", ct: ct);
                return;
            }

            var customers = await _customerRepository.SearchByLastDigitsAsync(link.TenantId, digits, MaxMatches + 1, ct);

            if (customers.Count == 0)
            {
                await _sender.SendMessageAsync(chatId, "Müştəri tapılmadı.", ct: ct);
                return;
            }

            if (customers.Count == 1)
            {
                await SendCustomerDetailAsync(chatId, customers[0], ct);
                return;
            }

            if (customers.Count > MaxMatches)
            {
                await _sender.SendMessageAsync(chatId,
                    "Çox nəticə tapıldı. Daha dəqiq axtarış üçün kart nömrəsinin daha çox rəqəmini göndərin.", ct: ct);
                return;
            }

            // 2-5 nəticə → inline keyboard ilə seçim
            var buttons = customers.Select(c => new[]
            {
                new
                {
                    text = $"{MaskName(c)} • …{LastDigits(c.CardNumber ?? c.applicationUser?.WalveroId?.ToString(), 5)}",
                    callback_data = $"cust:{c.Id}"
                }
            }).ToArray();

            await _sender.SendMessageAsync(chatId,
                $"{customers.Count} müştəri tapıldı, birini seçin:",
                new { inline_keyboard = buttons }, ct);
        }

        // ------------------- Callback (müştəri seçimi) -------------------

        private async Task HandleCallbackAsync(TelegramCallbackQueryDto callback, CancellationToken ct)
        {
            await _sender.AnswerCallbackQueryAsync(callback.Id, ct);

            var chatId = callback.Message?.Chat?.Id;
            var data = callback.Data;
            if (chatId == null || data == null || !data.StartsWith("cust:") ||
                !int.TryParse(data[5..], out var customerId))
                return;

            // Link chat.id-dən təzə resolve olunur — TenantId yalnız buradan
            var link = await _linkRepository.GetActiveByChatIdAsync(chatId.Value, ct);
            if (link == null)
                return;

            var customer = await _customerRepository.GetByIdAsync(customerId, ct);
            if (customer == null || customer.TenantId != link.TenantId || !customer.IsActive)
            {
                await _sender.SendMessageAsync(chatId.Value, "Müştəri tapılmadı.", ct: ct);
                return;
            }

            await SendCustomerDetailAsync(chatId.Value, customer, ct);
        }

        // ------------------- Detal mesajı -------------------

        private async Task SendCustomerDetailAsync(long chatId, Customer customer, CancellationToken ct)
        {
            var transactions = await _transactionRepository
                .GetRecentRedeemedByCustomerAsync(customer.Id, RecentRedeemCount, ct);

            var sb = new StringBuilder();
            sb.Append("👤 <b>").Append(Escape(MaskName(customer))).Append("</b>\n");

            if (!string.IsNullOrEmpty(customer.CardNumber))
                sb.Append("💳 Kart: <code>…").Append(LastDigits(customer.CardNumber, 5)).Append("</code>\n");

            var walveroId = customer.applicationUser?.WalveroId;
            if (walveroId != null)
                sb.Append("🆔 Walvero ID: <code>…").Append(LastDigits(walveroId.ToString(), 5)).Append("</code>\n");

            var activeCards = customer.LoyaltyCards.Where(c => c.IsActive).ToList();
            if (activeCards.Count == 1)
            {
                sb.Append("💰 Balans: <b>").Append(activeCards[0].CurrentPoints.ToString("0.##")).Append(" xal</b>\n");
            }
            else if (activeCards.Count > 1)
            {
                sb.Append("💰 Balanslar:\n");
                foreach (var card in activeCards)
                    sb.Append("   • …").Append(LastDigits(card.CardNumber, 5))
                      .Append(": <b>").Append(card.CurrentPoints.ToString("0.##")).Append(" xal</b>\n");
            }
            else
            {
                sb.Append("💰 Aktiv kart yoxdur\n");
            }

            sb.Append('\n');

            if (transactions.Count == 0)
            {
                sb.Append("Balansdan xərcləmə yoxdur.");
            }
            else
            {
                sb.Append("🧾 <b>Son xərcləmələr:</b>\n");
                foreach (var tx in transactions)
                {
                    sb.Append(tx.LocalOccurredAt.ToString("dd.MM.yyyy HH:mm"))
                      .Append("  -").Append(tx.Points.ToString("0.##")).Append(" xal");
                    if (tx.Amount != null)
                        sb.Append("  (").Append(tx.Amount.Value.ToString("0.##")).Append(" ₼)");
                    if (!string.IsNullOrEmpty(tx.Description))
                        sb.Append("  ").Append(Escape(Truncate(tx.Description, 40)));
                    sb.Append('\n');
                }
            }

            await _sender.SendMessageAsync(chatId, sb.ToString(), ct: ct);
        }

        // ------------------- Helpers -------------------

        private bool IsRateLimited(long chatId)
        {
            var key = RateCachePrefix + chatId;
            var count = _cache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return 0;
            });
            if (count >= MaxLookupsPerMinute)
                return true;

            _cache.Set(key, count + 1, TimeSpan.FromMinutes(1));
            return false;
        }

        private static string GenerateCode(int length)
        {
            // Oxşar simvollar (0/O, 1/I) çıxarılıb — admin kodu əl ilə də yaza bilər
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var bytes = RandomNumberGenerator.GetBytes(length);
            var chars = new char[length];
            for (var i = 0; i < length; i++)
                chars[i] = alphabet[bytes[i] % alphabet.Length];
            return new string(chars);
        }

        private static string MaskName(Customer c)
        {
            var first = c.FirstName?.Trim() ?? string.Empty;
            var last = c.LastName?.Trim() ?? string.Empty;
            var lastInitial = last.Length > 0 ? last[0] + "." : string.Empty;
            var masked = $"{first} {lastInitial}".Trim();
            return masked.Length > 0 ? masked : "Müştəri";
        }

        private static string LastDigits(string? value, int count)
            => string.IsNullOrEmpty(value) ? "?" : value.Length <= count ? value : value[^count..];

        private static string Truncate(string value, int max)
            => value.Length <= max ? value : value[..max] + "…";

        private static string Escape(string value)
            => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
