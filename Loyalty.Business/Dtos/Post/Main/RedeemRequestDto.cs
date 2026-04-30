using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
   
    public enum RedeemMode
    {
        Auto = 0,            // default: Tier+Policy tələb edirsə PENDING, yoxsa dərhal tətbiq
        ForcePending = 1,    // məcburi iki-mərhələli (PENDING yaratsın)
        ForceApply = 2     // məcburi dərhal tətbiq etsin (policy OFF olmalıdır, yoxsa 409)
    }

    /// <summary>
    /// 1-ci mərhələ (start) üçün istək DTO-su.
    /// </summary>
    public sealed class RedeemRequestDto
    {
        public int TenantId { get; set; }

        public int? LoyaltyProgramId { get; set; }   // istəyə görə filtr
        public string Code { get; set; } = string.Empty; // kartın barcode/QR dəyəri
        public decimal Delta { get; set; } = 1;            // +1, +5.5, -1 və s.

        // Yeni sahələr
        public WalletPlatform PreferredPlatform { get; set; } = WalletPlatform.Unknown; // istəyə görə override
        public RedeemMode Mode { get; set; } = RedeemMode.Auto;                          // iki-mərhələ/tek-mərhələ idarəsi
        public string? IdempotencyKey { get; set; }                                      // duplikasiya qarşısı üçün
        public bool DryRun { get; set; } = false;                                        // yalnız yoxla, tətbiq etmə
        public string? OperatorId { get; set; }                                          // kassir/operator ID (audit)
        public string? OrderId { get; set; }                                          // kassir/operator ID (audit)
        public string? Note { get; set; }                                                // səbəb/şərh (audit)
        public int? SpendCount { get; set; }                                             // Free reward xərcləmə sayı (kassirin daxil etdiyi)
    }

    /// <summary>
    /// 2-ci mərhələ (confirm) üçün ayrıca, sadə DTO.
    /// </summary>
    public sealed class RedeemConfirmDto
    {
        public int RedeemRequestId { get; set; }   // PENDING sorğunun ID-si
        public string Code { get; set; } = string.Empty; // OTP / təsdiq kodu
        public string? OperatorId { get; set; }
        public string? IdempotencyKey { get; set; }
    }

}
