namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IIikoPosApiClient
    {
        /// <summary>
        /// iiko Cloud API-dən access token alır (1 saat ömür).
        /// </summary>
        Task<string?> GetAccessTokenAsync(string apiLogin, CancellationToken ct = default);

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
        /// iiko təşkilatlarının siyahısını alır.
        /// </summary>
        Task<IikoOrganization[]?> GetOrganizationsAsync(string accessToken, CancellationToken ct = default);

        /// <summary>
        /// iiko menyusunu (nomenclature) alır.
        /// </summary>
        Task<IikoNomenclatureResponse?> GetNomenclatureAsync(
            string accessToken, Guid organizationId, CancellationToken ct = default);
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
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public List<IikoCustomerCard>? Cards { get; set; }
        public List<IikoWalletBalance>? WalletBalances { get; set; }
    }

    public class IikoCustomerCard
    {
        public Guid Id { get; set; }
        public string? Track { get; set; }
        public string? Number { get; set; }
    }

    public class IikoWalletBalance
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public decimal Balance { get; set; }
    }

    public class IikoOrganization
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Country { get; set; }
        public string? Address { get; set; }
    }

    public class IikoNomenclatureResponse
    {
        public List<IikoProductGroup>? Groups { get; set; }
        public List<IikoProduct>? Products { get; set; }
        public long Revision { get; set; }
    }

    public class IikoProductGroup
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public Guid? ParentGroup { get; set; }
    }

    public class IikoProduct
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Code { get; set; }
        public Guid? ParentGroup { get; set; }
        public string? Type { get; set; }
    }
}
