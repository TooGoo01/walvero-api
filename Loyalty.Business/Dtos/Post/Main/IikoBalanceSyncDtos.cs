namespace Loyalty.Business.Dtos.Post.Main
{
    /// <summary>Walvero → iiko. IntegrationId/VenueId boşdursa bütün tapılan venue-lara.</summary>
    public class IikoPushBalanceDto
    {
        public int? IntegrationId { get; set; }
        public int? VenueId { get; set; }
        public int? CardId { get; set; }
    }

    /// <summary>iiko → Walvero. VenueId null = legacy tək-org inteqrasiya.</summary>
    public class IikoPullBalanceDto
    {
        public int IntegrationId { get; set; }
        public int? VenueId { get; set; }
        public int? CardId { get; set; }
    }
}
