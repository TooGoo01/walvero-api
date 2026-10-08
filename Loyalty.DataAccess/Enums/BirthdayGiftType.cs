namespace Loyalty.DataAccess.Enums
{
    /// <summary>Ad günü hədiyyəsinin növü (SignupGiftType ilə eyni dəyərlər, ayrı tip — xoş gəldin məntiqindən asılı deyil).</summary>
    public enum BirthdayGiftType
    {
        Points = 1,    // Xal/Tier kartı: N ₼ bonus (müddətli ola bilər)
        Stamps = 2,    // Möhür kartı: N möhür
        FreeItem = 3   // Möhür kartı: N pulsuz məhsul (CompletedCycles +N, hər biri TotalSteps addım)
    }
}
