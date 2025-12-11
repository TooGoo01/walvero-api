using Loyalty.Business.Dtos.Post.Main;
using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Helpers
{
    public static class LoyaltyMath
    {
        public static LoyaltyDisplayState BuildDisplayState(
            CardTemplateSnapshot tpl,
            int currentPoints,
            int currentSteps)
        {
            var state = new LoyaltyDisplayState();

            switch (tpl.TemplateType)
            {
                case CardTemplateType.ProgressBased:
                    {
                        int total = Math.Max(1, tpl.TotalSteps);
                        int cur = Math.Clamp(currentSteps, 0, total);
                        state.Current = cur;
                        state.Total = total;

                        // Header & Body (emoji progress)
                        state.Header = $"{total} {tpl.StepLabel} = {tpl.RewardDescription}".Trim();
                        state.Body = BuildEmojiProgress(cur, total);

                        // Icon seçimi: index = cur (yəni 0 addım üçün 0-cı ikon)
                        state.CurrentIconUrl = PickProgressIcon(tpl.IconUrls, cur);
                        break;
                    }

                case CardTemplateType.PointsBased:
                    {
                        state.Current = currentPoints;
                        state.Total = Math.Max(0, tpl.MinPointsToRedeem ?? 0);

                        state.Header = $"Points Balance: {currentPoints}";
                        if (tpl.MinPointsToRedeem > 0)
                            state.Body = $"Redeem at {tpl.MinPointsToRedeem} pts";
                        else
                            state.Body = "Earn points on every purchase";

                        // Dinamik ikon istəsəniz: threshold-a görə dəyişən ikonlar (optional)
                        state.CurrentIconUrl = PickTierIconByPoints(tpl, currentPoints, out var tierName);
                        state.TierName = tierName;
                        break;
                    }

                case CardTemplateType.TierBased:
                    {
                        state.Current = currentPoints;
                        state.Total = 0; // tier-based üçün göstərməyə bilərik

                        state.CurrentIconUrl = PickTierIconByPoints(tpl, currentPoints, out var tierName);
                        state.TierName = tierName;
                        state.Header = $"Tier: {tierName}";
                        state.Body = $"Points: {currentPoints}";
                        break;
                    }
            }

            return state;
        }

        // 🔹 Emoji progress
        public static string BuildEmojiProgress(int current, int total)
        {
            const string FULL = "🌟";
            const string EMPTY = "⚪";
            if (total <= 0) total = 1;
            current = Math.Clamp(current, 0, total);

            var sb = new StringBuilder(total * 2);
            for (int i = 0; i < total; i++) sb.Append(i < current ? FULL : EMPTY);
            return $"{sb}  ({current}/{total})";
        }

        // 🔹 Progress ikon seçimi: cur addıma uyğun indeks (0..total)
        // Icon siyahınızda ən azı 1 element varsa, “cur < count ? cur : last” qaydasını tətbiq edirik
        private static string? PickProgressIcon(List<string> icons, int curIndex)
        {
            if (icons == null || icons.Count == 0) return null;
            if (curIndex < 0) curIndex = 0;
            if (curIndex >= icons.Count) curIndex = icons.Count - 1;
            return icons[curIndex];
        }

        // 🔹 Tier ikon seçimi (threshold-lara görə)
        private static string? PickTierIconByPoints(CardTemplateSnapshot tpl, int pts, out string tier)
        {
            // Eşikləri artan sıralama kimi qəbul edirik
            // Bronze < Silver < Gold < Platinum
            tier = "Bronze";
            string? icon = tpl.BronzeIconUrl ?? tpl.LogoUrl;

            if (tpl.SilverThreshold.HasValue && pts >= tpl.SilverThreshold.Value)
            { tier = "Silver"; icon = tpl.SilverIconUrl ?? icon; }

            if (tpl.GoldThreshold.HasValue && pts >= tpl.GoldThreshold.Value)
            { tier = "Gold"; icon = tpl.GoldIconUrl ?? icon; }

            if (tpl.PlatinumThreshold.HasValue && pts >= tpl.PlatinumThreshold.Value)
            { tier = "Platinum"; icon = tpl.PlatinumIconUrl ?? icon; }

            return icon;
        }
    }

}
