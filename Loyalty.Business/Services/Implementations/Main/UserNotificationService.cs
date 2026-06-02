using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class UserNotificationService : IUserNotificationService
    {
        private readonly LoyaltyWalletDbContext _db;
        private readonly INotificationService _fcm;
        private readonly ILogger<UserNotificationService> _logger;

        public UserNotificationService(
            LoyaltyWalletDbContext db,
            INotificationService fcm,
            ILogger<UserNotificationService> logger)
        {
            _db = db;
            _fcm = fcm;
            _logger = logger;
        }

        public async Task NotifyReceiptIssueAsync(int customerId, string tenantName, string reason, CancellationToken ct = default)
        {
            var customer = await _db.Customers
                .AsNoTracking()
                .Where(c => c.Id == customerId)
                .Select(c => new { c.applicationUserId, c.FirstName })
                .FirstOrDefaultAsync(ct);

            if (customer == null || !customer.applicationUserId.HasValue)
            {
                _logger.LogInformation("NotifyReceiptIssue: customer {Id} has no applicationUserId — skip", customerId);
                return;
            }

            var (title, body) = MessageFor(reason, tenantName);

            // Save Notification entity for in-app inbox
            try
            {
                _db.Set<Notification>().Add(new Notification
                {
                    UserId = customer.applicationUserId.Value,
                    Title = title,
                    Body = body,
                    CreatedAt = DateTime.UtcNow,
                });
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification entity save failed cust={Cust}", customerId);
            }

            try
            {
                await _fcm.SendToUserAsync(customer.applicationUserId.Value, title, body, new { reason, tenant = tenantName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FCM send failed cust={Cust}", customerId);
            }
        }

        private static (string title, string body) MessageFor(string reason, string tenantName) => reason switch
        {
            "ReceiptPendingReview" => ("Əməliyyat qeydə alındı",
                $"{tenantName}: çekiniz yoxlanış üçün göndərildi. Yoxlanışdan sonra balansınız avtomatik artacaq."),
            "QrMissing" => ("Çekdə QR kod yoxdur",
                $"{tenantName}: əməliyyat üçün QR kodlu çek tələb olunur. Növbəti dəfə QR kodu olan çek təqdim edin."),
            "DuplicateReceiptId" => ("Çek artıq istifadə olunub",
                $"{tenantName}: təqdim etdiyiniz çek əvvəlcədən sistemdə qeydiyyatdadır. Eyni çek təkrar istifadə oluna bilməz."),
            "ObjectCodeMismatch" => ("Obyekt kodu uyğun deyil",
                $"{tenantName}: çekdəki obyekt kodu seçilmiş filiala uyğun deyil. Çekin doğru filialdan olduğundan əmin olun."),
            "TaxNumberMismatch" => ("VÖEN uyğun deyil",
                $"{tenantName}: çekdəki VÖEN gözlənilən şirkətə uyğun deyil. Yalnız bu şirkətin çekləri qəbul olunur."),
            "ReceiptDateMismatch" => ("Çekin tarixi uyğun deyil",
                $"{tenantName}: yalnız bu günkü tarixli çeklər qəbul olunur."),
            "OcrMismatch" => ("Çekin məbləği uyğun deyil",
                $"{tenantName}: çekdəki məbləğ daxil edilən məbləğlə uyğun gəlmədi."),
            "OcrUnreadable" => ("Çek oxunmadı",
                $"{tenantName}: çekin şəkli aydın oxunmadı. Növbəti dəfə daha aydın şəkil təqdim edin."),
            _ => ("Çek yoxlaması",
                $"{tenantName}: əməliyyat manual baxış üçün gözləməyə qoyuldu.")
        };
    }
}
