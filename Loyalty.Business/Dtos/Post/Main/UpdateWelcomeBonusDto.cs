using Loyalty.DataAccess.Enums;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class UpdateWelcomeBonusDto
    {
        public bool FirstPurchaseBonusEnabled { get; set; }
        public WelcomeBonusType FirstPurchaseBonusType { get; set; } = WelcomeBonusType.Percent;
        public decimal FirstPurchaseBonusValue { get; set; }
        public decimal? FirstPurchaseBonusMaxPoints { get; set; }

        public bool SignupGiftEnabled { get; set; }
        public SignupGiftType SignupGiftType { get; set; } = SignupGiftType.Points;
        public int SignupGiftValue { get; set; }
    }
}
