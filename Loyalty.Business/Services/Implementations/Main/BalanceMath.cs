using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.Business.Dtos.Post.Main;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// Loyalty card balance application logic. Shared between RedeemService (sync flow)
    /// və ReceiptOcrJob (Hangfire async flow).
    /// </summary>
    internal static class BalanceMath
    {
        public static void ApplyDelta(LoyaltyCard card, CardTemplateSnapshot snapshot, decimal delta)
        {
            switch (snapshot.TemplateType)
            {
                case CardTemplateType.ProgressBased:
                    int totalSteps = System.Math.Max(1, snapshot.TotalSteps);
                    int stepsDelta = (int)delta;

                    if (stepsDelta > 0)
                    {
                        int newSteps = card.CurrentSteps + stepsDelta;
                        card.CurrentPoints = System.Math.Max(0, card.CurrentPoints + stepsDelta);

                        int cyclesEarned = newSteps / totalSteps;
                        if (cyclesEarned > 0)
                            card.CompletedCycles += cyclesEarned;

                        card.CurrentSteps = newSteps % totalSteps;
                    }
                    else if (stepsDelta < 0)
                    {
                        card.CurrentPoints = System.Math.Max(0, card.CurrentPoints + stepsDelta);
                        int cyclesLost = System.Math.Abs(stepsDelta) / totalSteps;
                        if (cyclesLost > 0)
                            card.CompletedCycles = System.Math.Max(0, card.CompletedCycles - cyclesLost);
                    }
                    break;

                case CardTemplateType.PointsBased:
                case CardTemplateType.TierBased:
                    card.CurrentPoints = System.Math.Max(0m, card.CurrentPoints + delta);
                    break;
            }
        }
    }
}
