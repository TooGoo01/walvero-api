namespace Loyalty.DataAccess.Enums
{
    /// <summary>
    /// IikoWebhookFailure.Status dəyərləri (int sütun, migration tələb etmir).
    /// </summary>
    public static class IikoWebhookFailureStatus
    {
        /// <summary>Retry növbəsində — NextAttemptAt çatanda yenidən emal olunur.</summary>
        public const int Pending = 0;
        /// <summary>Uğurla emal olundu (və ya artıq emal olunmuşdu).</summary>
        public const int Resolved = 1;
        /// <summary>Cəhd/yaş limiti keçildi — daha retry olunmur, əl ilə requeue mümkündür.</summary>
        public const int Abandoned = 2;
        /// <summary>Daimi xəta (yanlış payload, deterministik exception) — retry mənasızdır, audit üçün saxlanır.</summary>
        public const int Rejected = 3;
    }

    /// <summary>
    /// IikoWebhookFailure.FailureKind dəyərləri (Business tərəfdəki IikoFailureKind ilə eyni rəqəmlər).
    /// 0 = köhnə sətir (növ məlum deyil, Retriable kimi davranılır).
    /// </summary>
    public static class IikoWebhookFailureKind
    {
        public const int Unknown = 0;
        public const int Transient = 1;
        public const int Retriable = 2;
        public const int Permanent = 3;
    }
}
