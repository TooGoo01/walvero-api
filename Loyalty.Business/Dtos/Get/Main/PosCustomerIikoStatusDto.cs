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
        /// <summary>Telefonla bağlanıb, köhnə kassa balansı hələ Walvero-ya yazılmayıb (bu müddətdə balans push olunmur).</summary>
        public decimal? LegacyPendingBalance { get; set; }

        public bool Found { get; set; }
        /// <summary>id | barcode | walveroId | phone</summary>
        public string? FoundBy { get; set; }
        /// <summary>Tapılmayanda iiko-nun son cavab mətni (məs. "There is no user with card track …").</summary>
        public string? LookupNote { get; set; }
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

    /// <summary>Toplu telefonla uyğunlaşdırma önizləməsi: kassaya bağlanmamış (mapping-siz) müştərilər.</summary>
    public class IikoPhoneMatchPreviewDto
    {
        public int IntegrationId { get; set; }
        public bool MatchExistingByPhone { get; set; }
        public int Total { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
        public List<IikoPhoneMatchRowDto> Rows { get; set; } = new();
    }

    public class IikoPhoneMatchRowDto
    {
        public int CustomerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public bool Found { get; set; }
        public Guid? IikoCustomerId { get; set; }
        public decimal? IikoBalance { get; set; }
        public decimal OurBalance { get; set; }
        /// <summary>Kassa qonağı artıq başqa Walvero müştərisinə bağlıdır.</summary>
        public int? ConflictCustomerId { get; set; }
    }

    public class IikoWalletDto
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public int? Type { get; set; }
        public decimal Balance { get; set; }
    }
}
