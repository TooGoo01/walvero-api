using Loyalty.Business.Dtos.Post.Main;
using Loyalty.DataAccess.Entities.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Helpers
{
    public static class CardTemplateSnapshotFactory
    {
        /// <summary>
        /// db yolunu (uploads/...) absolute URL-ə çevirən funksiya.
        /// Məs: path => $"{scheme}://{host}/{path}"
        /// </summary>
        public delegate string UrlResolver(string? dbPath);

        public static CardTemplateSnapshot From(
            LoyaltyProgram lp,
            CardTemplate? ct,
            TenantBranding? branding,
            UrlResolver toAbsoluteUrl)
        {
            if (lp is null) throw new ArgumentNullException(nameof(lp));
            ct ??= new CardTemplate(); // null gələrsə default

            var snap = new CardTemplateSnapshot
            {
                // Program meta
                LoyaltyProgramId = lp.Id,
                TenantId = lp.TenantId,
                ProgramName = string.IsNullOrWhiteSpace(lp.ProgramName) ? "Loyalty" : lp.ProgramName,
                ProgramDescription = lp.Description,
                ProgramCode = lp.Code,

                // Template meta
                TemplateName = ct.TemplateName ?? string.Empty,
                TemplateType = ct.TemplateType,
                IconLayout = ct.IconLayout,

                // Progress based
                TotalSteps = ct.TotalSteps > 0 ? ct.TotalSteps : 10,
                StepLabel = string.IsNullOrWhiteSpace(ct.StepLabel) ? "Visit" : ct.StepLabel!,
                RewardDescription = string.IsNullOrWhiteSpace(ct.RewardDescription) ? "Get 1 FREE" : ct.RewardDescription!,

                // Points based
                StartPoints = 0,
                PointsPerCurrency = lp.PointsPerCurrency,
                CurrencyPerPoint = lp.CurrencyPerPoint,
                MinPointsToRedeem = lp.MinPointsToRedeem,
                PointsExpiryDays = lp.PointsExpiryDays,

                // Tier based thresholds/icons
                BronzeIconUrl = toAbsoluteUrl(ct.BronzeIconUrl),
                BronzeThreshold = ct.BronzeThreshold,
                SilverIconUrl = toAbsoluteUrl(ct.SilverIconUrl),
                SilverThreshold = ct.SilverThreshold,
                GoldIconUrl = toAbsoluteUrl(ct.GoldIconUrl),
                GoldThreshold = ct.GoldThreshold,
                PlatinumIconUrl = toAbsoluteUrl(ct.PlatinumIconUrl),
                PlatinumThreshold = ct.PlatinumThreshold,
                PlatinumPercent = ct.PlatinumPercent ?? 0,
                BronzePercent = ct.BronzePercent ?? 0,
                SilverPercent = ct.SilverPercent ?? 0,
                GoldPercent = ct.GoldPercent ?? 0,
                PlatinumPercentCash = ct.PlatinumPercentCash ?? 0,
                BronzePercentCash = ct.BronzePercentCash ?? 0,
                SilverPercentCash = ct.SilverPercentCash ?? 0,
                GoldPercentCash = ct.GoldPercentCash ?? 0,
                OneRedeem = ct.OneRedeem,
                PayMentType = ct.PayMentType,
                // Colors (CardTemplate üstünlük, boşdursa Branding → default)
                BackgroundColor = FirstNonEmpty(ct.BackgroundColor, branding?.SecondaryColor, "#FFFFFF")!,
                ForegroundColor = FirstNonEmpty(ct.ForegroundColor, "#000000")!,
                LabelColor = FirstNonEmpty(ct.LabelColor, "#999999")!,
                ProgressBarColor = FirstNonEmpty(ct.ProgressBarColor, "#4CAF50")!,

                // Images (CardTemplate üstünlük, boşdursa Branding)
                LogoUrl = toAbsoluteUrl(FirstNonEmpty(ct.LogoUrl)),
                GoogleLogoUrl = toAbsoluteUrl(FirstNonEmpty(ct.GoogleLogoUrl)),
                BackgroundImageUrl = toAbsoluteUrl(FirstNonEmpty(ct.BackgroundImageUrl, branding?.BackgroundImageUrl)),
                StripImageUrl = toAbsoluteUrl(ct.StripImageUrl),
                HeroImageUrl = toAbsoluteUrl(ct.HeroImageUrl),
            };

            // Icon list (CardTemplate.Icons kolleksiyasından)
            if (ct.Icons != null && ct.Icons.Count > 0)
            {
                foreach (var icon in ct.Icons)
                {
                    if (!string.IsNullOrWhiteSpace(icon.Path))
                        snap.IconUrls.Add(toAbsoluteUrl(icon.Path));
                    else if (!string.IsNullOrWhiteSpace(icon.FileName))
                        snap.IconUrls.Add(toAbsoluteUrl(icon.FileName)); // ehtiyat
                }
            }

            return snap;
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            foreach (var v in values)
                if (!string.IsNullOrWhiteSpace(v)) return v;
            return null;
        }
    }
}
