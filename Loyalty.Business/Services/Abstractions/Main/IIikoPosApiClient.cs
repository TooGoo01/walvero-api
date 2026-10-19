namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IIikoPosApiClient
    {
        /// <summary>
        /// iiko Cloud API-dən access token alır (1 saat ömür).
        /// </summary>
        Task<string?> GetAccessTokenAsync(string apiLogin, string? appId = null, string? clientSecret = null, CancellationToken ct = default);

        /// <summary>
        /// iiko-da müştəri yaradır və ya yeniləyir.
        /// </summary>
        Task<IikoCustomerResponse?> CreateOrUpdateCustomerAsync(
            string accessToken, Guid organizationId, IikoCustomerDto customer, CancellationToken ct = default);

        /// <summary>
        /// iiko-da müştəri məlumatlarını alır (telefon, kart, email ilə axtarış).
        /// </summary>
        Task<IikoCustomerInfoResponse?> GetCustomerInfoAsync(
            string accessToken, Guid organizationId, string? phone = null, string? cardTrack = null, CancellationToken ct = default);

        /// <summary>
        /// iiko müştərisinə kart əlavə edir.
        /// </summary>
        Task<bool> AddCustomerCardAsync(
            string accessToken, Guid organizationId, Guid customerId, string cardTrack, string cardNumber, CancellationToken ct = default);

        /// <summary>
        /// iiko müştərisini loyalty proqramına əlavə edir.
        /// </summary>
        Task<bool> AddCustomerToProgramAsync(
            string accessToken, Guid organizationId, Guid customerId, Guid programId, CancellationToken ct = default);

        /// <summary>
        /// iiko müştərisinin wallet balansını artırır (topup).
        /// </summary>
        Task<bool> TopUpWalletAsync(
            string accessToken, Guid organizationId, Guid customerId, Guid walletId, decimal amount, CancellationToken ct = default);

        /// <summary>
        /// iiko müştərisinin wallet balansını azaldır (chargeoff).
        /// </summary>
        Task<bool> ChargeOffWalletAsync(
            string accessToken, Guid organizationId, Guid customerId, Guid walletId, decimal amount, CancellationToken ct = default);

        /// <summary>
        /// customer/info — HTTP status ilə; "Customer not found" (400) ilə auth/org xətasını ayırmaq üçün.
        /// </summary>
        Task<IikoApiResult<IikoCustomerInfoResponse>> FindCustomerAsync(
            string accessToken, Guid organizationId, IikoCustomerLookup lookup, string value, CancellationToken ct = default);

        /// <summary>
        /// iiko təşkilatlarının siyahısını alır.
        /// </summary>
        Task<IikoOrganization[]?> GetOrganizationsAsync(string accessToken, CancellationToken ct = default);

        /// <summary>
        /// Verilən org-lar üçün terminal qruplarını alır.
        /// </summary>
        Task<List<IikoTerminalGroupBlock>?> GetTerminalGroupsAsync(
            string accessToken, IEnumerable<Guid> organizationIds, CancellationToken ct = default);

        /// <summary>
        /// Verilən org üçün loyalty proqramlarını alır.
        /// </summary>
        Task<List<IikoLoyaltyProgram>?> GetLoyaltyProgramsAsync(
            string accessToken, Guid organizationId, CancellationToken ct = default);

        /// <summary>
        /// iiko menyusunu (nomenclature) alır.
        /// </summary>
        /// <summary>
        /// Loyallıq proqramları marketinq kampaniyaları ilə birgə (withoutMarketingCampaigns=false) —
        /// hansı məhsula neçə % düşdüyünü göstərmək üçün (read-only, admin). Xəta/404 → null.
        /// </summary>
        Task<List<IikoLoyaltyProgramDetail>?> GetLoyaltyProgramsDetailedAsync(
            string accessToken, Guid organizationId, CancellationToken ct = default);

        Task<IikoNomenclatureResponse?> GetNomenclatureAsync(
            string accessToken, Guid organizationId, CancellationToken ct = default);

        /// <summary>Org-un manual qonaq kateqoriyaları (`/loyalty/iiko/customer_category`). null = iiko cavab vermədi.</summary>
        Task<List<IikoGuestCategory>?> GetGuestCategoriesAsync(
            string accessToken, Guid organizationId, CancellationToken ct = default);

        /// <summary>Müştərini qonaq kateqoriyasına əlavə et (məs. ambassador).</summary>
        Task<bool> AddCustomerCategoryAsync(
            string accessToken, Guid organizationId, Guid customerId, Guid categoryId, CancellationToken ct = default);

        /// <summary>Müştərini qonaq kateqoriyasından çıxar.</summary>
        Task<bool> RemoveCustomerCategoryAsync(
            string accessToken, Guid organizationId, Guid customerId, Guid categoryId, CancellationToken ct = default);
    }

    public class IikoGuestCategory
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public bool IsActive { get; set; }
        public bool IsDefaultForNewGuests { get; set; }
    }

    public class IikoGuestCategoriesResponse
    {
        public List<IikoGuestCategory>? GuestCategories { get; set; }
    }

    // ── DTOs ──

    public class IikoCustomerDto
    {
        public Guid? Id { get; set; }
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? CardTrack { get; set; }
        public string? CardNumber { get; set; }
        public string? Birthday { get; set; }
    }

    public class IikoCustomerResponse
    {
        public Guid Id { get; set; }
    }

    public class IikoCustomerInfoResponse
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public string? MiddleName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public bool? IsDeleted { get; set; }
        public bool? Anonymized { get; set; }
        public List<IikoCustomerCard>? Cards { get; set; }
        public List<IikoCustomerCategory>? Categories { get; set; }
        public List<IikoWalletBalance>? WalletBalances { get; set; }
    }

    public class IikoCustomerCard
    {
        public Guid Id { get; set; }
        public string? Track { get; set; }
        public string? Number { get; set; }
        public string? ValidToDate { get; set; }
    }

    public class IikoCustomerCategory
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public bool? IsActive { get; set; }
    }

    public class IikoWalletBalance
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public int? Type { get; set; }
        public decimal Balance { get; set; }
    }

    public enum IikoCustomerLookup
    {
        Id,
        Phone,
        CardTrack,
        CardNumber,
        Email
    }

    /// <summary>iiko çağırışının HTTP nəticəsi — Data null olsa da səbəb (400 not found vs 401 vs şəbəkə) görünür.</summary>
    public sealed class IikoApiResult<T>
    {
        public T? Data { get; init; }
        public int StatusCode { get; init; }
        public string? RawBody { get; init; }
        public string? ErrorDescription { get; init; }

        public bool Success => Data is not null && StatusCode is >= 200 and < 300;
        /// <summary>
        /// iiko lookup uğursuzluğunu 400 ilə qaytarır, mətn dəyişir ("Customer not found",
        /// "There is no user with card with card track X", ...) — 400 = tapılmadı.
        /// </summary>
        public bool IsNotFound => StatusCode == 400;
        public bool IsUnauthorized => StatusCode == 401;

        public static IikoApiResult<T> Ok(T? data, int statusCode, string? rawBody)
            => new() { Data = data, StatusCode = statusCode, RawBody = rawBody };

        public static IikoApiResult<T> Failure(int statusCode, string? rawBody, string? errorDescription)
            => new() { StatusCode = statusCode, RawBody = rawBody, ErrorDescription = errorDescription };
    }

    public class IikoOrganization
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Country { get; set; }
        public string? Address { get; set; }
    }

    public class IikoTerminalGroupBlock
    {
        public Guid OrganizationId { get; set; }
        public List<IikoTerminalGroupItem>? Items { get; set; }
    }

    public class IikoTerminalGroupItem
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
    }

    public class IikoLoyaltyProgram
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public bool IsActive { get; set; }
        public Guid? WalletId { get; set; }
    }

    // ── iiko loyalty program (detailed, with marketing campaigns) ──
    public class IikoLoyaltyProgramDetail
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public DateTime? ServiceFrom { get; set; }
        public DateTime? ServiceTo { get; set; }
        public bool IsActive { get; set; }
        public Guid? WalletId { get; set; }
        public int ProgramType { get; set; }
        public int RefillType { get; set; }
        public List<IikoMarketingCampaign>? MarketingCampaigns { get; set; }
    }

    public class IikoMarketingCampaign
    {
        public Guid Id { get; set; }
        public Guid? ProgramId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime? PeriodFrom { get; set; }
        public DateTime? PeriodTo { get; set; }
        public List<IikoActionConditionBinding>? OrderActionConditionBindings { get; set; }
        public List<IikoActionConditionBinding>? PeriodicActionConditionBindings { get; set; }
        public List<IikoActionConditionBinding>? GuestRegistrationActionConditionBindings { get; set; }
        public List<IikoActionConditionBinding>? OverdraftActionConditionBindings { get; set; }
    }

    public class IikoActionConditionBinding
    {
        public Guid Id { get; set; }
        public bool StopFurtherExecution { get; set; }
        public List<IikoRuleItem>? Actions { get; set; }
        public List<IikoRuleItem>? Conditions { get; set; }
    }

    /// <summary>Action və ya condition: TypeName (məs. LoyaltyProgramEngine.Actions.RefillWalletPercentAction) + Settings (JSON mətn).</summary>
    public class IikoRuleItem
    {
        public Guid Id { get; set; }
        public string? TypeName { get; set; }
        public string? Settings { get; set; }
    }

    public class IikoNomenclatureResponse
    {
        public List<IikoProductGroup>? Groups { get; set; }
        public List<IikoProductCategory>? ProductCategories { get; set; }
        public List<IikoProduct>? Products { get; set; }
        public long Revision { get; set; }
    }

    public class IikoProductGroup
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public Guid? ParentGroup { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class IikoProductCategory
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class IikoProduct
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Code { get; set; }
        public Guid? ParentGroup { get; set; }
        public Guid? GroupId { get; set; }
        public Guid? ProductCategoryId { get; set; }
        public string? Type { get; set; }
        public bool IsDeleted { get; set; }
    }
}
