namespace Loyalty.Business.Dtos.Get.Main
{
    public class PosCustomerIikoStatusDto
    {
        public int CustomerId { get; set; }
        public bool HasIikoIntegration { get; set; }
        public List<IikoOurCardDto> OurCards { get; set; } = new();
        public List<IikoVenueStatusDto> Venues { get; set; } = new();
    }

    public class IikoOurCardDto
    {
        public int CardId { get; set; }
        public string CardNumber { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public decimal CurrentPoints { get; set; }
        public int ProgramId { get; set; }
        public string TemplateType { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }

    public class IikoVenueStatusDto
    {
        public int IntegrationId { get; set; }
        public int? VenueId { get; set; }
        public int? BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string OrganizationId { get; set; } = string.Empty;

        public bool MappingExists { get; set; }
        public string? ExternalCustomerId { get; set; }

        public bool Found { get; set; }
        /// <summary>id | barcode | walveroId | phone</summary>
        public string? FoundBy { get; set; }
        public Guid? IikoCustomerId { get; set; }
        public string? IikoName { get; set; }
        public string? IikoPhone { get; set; }
        public List<string> IikoCardTracks { get; set; } = new();
        public List<IikoWalletDto> Wallets { get; set; } = new();
        public Guid? SelectedWalletId { get; set; }
        public decimal? IikoBalance { get; set; }

        public decimal OurBalance { get; set; }
        /// <summary>OurBalance − IikoBalance</summary>
        public decimal? Diff { get; set; }
        public string? Error { get; set; }
    }

    public class IikoWalletDto
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public int? Type { get; set; }
        public decimal Balance { get; set; }
    }
}
