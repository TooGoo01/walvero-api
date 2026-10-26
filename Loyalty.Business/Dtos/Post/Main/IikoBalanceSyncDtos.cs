namespace Loyalty.Business.Dtos.Post.Main
{
    /// <summary>Walvero → iiko. IntegrationId/VenueId boşdursa bütün tapılan venue-lara.</summary>
    public class IikoPushBalanceDto
    {
        public int? IntegrationId { get; set; }
        public int? VenueId { get; set; }
        public int? CardId { get; set; }
        /// <summary>Əlavə iiko kart-track axtarışı (köhnə sistemin fiziki kartı, məs. PrimeHill "105201").</summary>
        public string? LookupCardTrack { get; set; }
    }

    /// <summary>
    /// Kassada (telefonla) tapılan mövcud qonağa bağla: onun köhnə cüzdan balansı Walvero kartına ƏLAVƏ olunur,
    /// sonra kassa = Walvero. IntegrationId boşdursa bütün iiko inteqrasiyaları.
    /// </summary>
    public class IikoMergeByPhoneDto
    {
        public int? IntegrationId { get; set; }
        public int? CardId { get; set; }
    }

    /// <summary>iiko → Walvero. VenueId null = legacy tək-org inteqrasiya.</summary>
    public class IikoPullBalanceDto
    {
        public int IntegrationId { get; set; }
        public int? VenueId { get; set; }
        public int? CardId { get; set; }
    }

    /// <summary>
    /// iiko → Walvero toplu: proqramın bütün aktiv kartları seçilmiş kassa cüzdanının balansına bərabərləşdirilir.
    /// Apply=false — yalnız önbaxış (heç nə yazılmır).
    /// </summary>
    public class IikoProgramPullDto
    {
        public int ProgramId { get; set; }
        public string WalletId { get; set; } = string.Empty;
        public bool Apply { get; set; }
    }
}
