using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Abstractions.Storage.Azure;
using Loyalty.Infrastructure.Utilities.Security.Encryption;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Storage;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.Repositories.Implementations.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class WalletCredentialService : IWalletCredentialService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IWalletCredentialRepository _repo;
        // Sirr faylları PRIVATE konteynerə yüklənir (anonim oxuma/siyahı yox); parol DB-də şifrəli (enc:v1:)
        private readonly IAzureStorage _storage;
        private readonly ISensitiveDataProtector _protector;

        public WalletCredentialService(IAzureStorage storage, ISensitiveDataProtector protector, IWalletCredentialRepository repo, IMapper mapper, IUnitOfWork unitOfWork)
        {
            _storage = storage;
            _protector = protector;
            _repo = repo;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        public Task<ServiceResult> Add(AddTenantBrandingDto Dto)
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult> AddOrUpdateAsync(AddWalletCredentialDto dto, CancellationToken ct = default)
        {
            if (dto is null) return new ServiceResult(false, "Model is null.");
            if (dto.TenantId <= 0) return new ServiceResult(false, "TenantId is required.");

            var platformSlug = dto.Platform == WalletPlatform.AppleWallet ? "apple" :
                               dto.Platform == WalletPlatform.GooglePay ? "google" : null;
            if (platformSlug is null)
                return new ServiceResult(false, "Unsupported platform.");

            // uploads konteyneri
            var container = $"tenant-{dto.TenantId}-wallet-{platformSlug}";

            // Mövcud aktiv credential varsa deaktiv et (eyni tenant + platform)
            var existing = await _repo.GetActiveAsync(dto.TenantId, dto.Platform, ct);
            if (existing is not null)
            {
                
                _unitOfWork.Repository<WalletCredential>().Delete(existing);
                // Qeyd: İstəsən bu mərhələdə Commit etmədən də davam edə bilərsən; ən sonda Commit kifayət edir.
            }

            var cred = new WalletCredential
            {
                TenantId = dto.TenantId,
                Platform = dto.Platform,
                IsActive = true
            };

            try
            {
                if (dto.Platform == WalletPlatform.AppleWallet)
                {
                    // --- APPLE VALIDATION ---
                    if (string.IsNullOrWhiteSpace(dto.TeamIdentifier))
                        return new ServiceResult(false, "TeamIdentifier is required for Apple.");
                    if (string.IsNullOrWhiteSpace(dto.PassTypeIdentifier))
                        return new ServiceResult(false, "PassTypeIdentifier is required for Apple.");
                    if (string.IsNullOrWhiteSpace(dto.CertificatePassword))
                        return new ServiceResult(false, "Certificate password (.p12) is required for Apple.");

                    // --- .p12: fayl > url ---
                    if (dto.AppleCertificateFile is not null && dto.AppleCertificateFile.Length > 0)
                    {
                        // (istəsən MIME/extension yoxlaması əlavə et)
                        var uploaded = await _storage.UploadPrivateFileAsync(container, new[] { dto.AppleCertificateFile });
                        var (_, relativePath) = uploaded.First();
                        cred.AppleCertificateUrl = relativePath; // private blob URL (WalletSecretFetcher authenticated oxuyur)
                    }
                    else if (!string.IsNullOrWhiteSpace(dto.AppleCertificateUrl))
                    {
                        cred.AppleCertificateUrl = dto.AppleCertificateUrl.Trim();
                    }
                    else
                    {
                        return new ServiceResult(false, "Either Apple .p12 file or AppleCertificateUrl is required.");
                    }

                    // --- WWDR .cer (opsional, amma məsləhətlidir) ---
                    if (dto.AppleWwdrCertFile is not null && dto.AppleWwdrCertFile.Length > 0)
                    {
                        var uploaded = await _storage.UploadPrivateFileAsync(container, new[] { dto.AppleWwdrCertFile });
                        var (_, relativePath) = uploaded.First();
                        cred.AppleWwdrCertUrl = relativePath;
                    }
                    else if (!string.IsNullOrWhiteSpace(dto.AppleWwdrCertUrl))
                    {
                        cred.AppleWwdrCertUrl = dto.AppleWwdrCertUrl.Trim();
                    }

                    // --- PassKit Web Service (opsional) ---
                    cred.PassKitServiceUrl = string.IsNullOrWhiteSpace(dto.PassKitServiceUrl) ? null : dto.PassKitServiceUrl.Trim();
                    cred.PassKitAuthToken = string.IsNullOrWhiteSpace(dto.PassKitAuthToken)
                                                ? GenerateStrongToken(48) // boşdursa avtomatik yarat
                                                : dto.PassKitAuthToken.Trim();

                    // --- Dəyərlər ---
                    cred.TeamIdentifier = dto.TeamIdentifier!.Trim();
                    cred.PassTypeIdentifier = dto.PassTypeIdentifier!.Trim();
                    cred.CertificatePassword = WalletSecretPaths.Protect(dto.CertificatePassword, _protector.Encrypt); // enc:v1:… (WalletSecretFetcher.RevealPassword açır)
                }
                else // --- GOOGLE ---
                {
                    if (string.IsNullOrWhiteSpace(dto.IssuerId))
                        return new ServiceResult(false, "IssuerId is required for Google.");
                    // ServiceAccountEmail məsləhətlidir (JWT tərtib edəndə işinə yarayır)
                    if (string.IsNullOrWhiteSpace(dto.ServiceAccountEmail))
                        return new ServiceResult(false, "ServiceAccountEmail is required for Google.");

                    if (dto.GooglePrivateKeyFile is not null && dto.GooglePrivateKeyFile.Length > 0)
                    {
                        var uploaded = await _storage.UploadPrivateFileAsync(container, new[] { dto.GooglePrivateKeyFile });
                        var (_, relativePath) = uploaded.First();
                        cred.GooglePrivateKeyUrl = relativePath;
                    }
                    else if (!string.IsNullOrWhiteSpace(dto.GooglePrivateKeyUrl))
                    {
                        cred.GooglePrivateKeyUrl = dto.GooglePrivateKeyUrl.Trim();
                    }
                    else
                    {
                        return new ServiceResult(false, "Either Google service account key file or GooglePrivateKeyUrl is required.");
                    }

                    cred.IssuerId = dto.IssuerId!.Trim();
                    cred.ServiceAccountEmail = dto.ServiceAccountEmail!.Trim();
                }

                // Yadda saxla
                await _unitOfWork.Repository<WalletCredential>().AddAsync(cred);
                _unitOfWork.Commit();

                // Cavabda gizli məlumatları qaytarmayaq (məsələn .p12 parolu)
                var res = new
                {
                    cred.Id,
                    cred.TenantId,
                    cred.Platform,
                    cred.IsActive,
                    // Apple
                    cred.TeamIdentifier,
                    cred.PassTypeIdentifier,
                    cred.AppleCertificateUrl,
                    cred.AppleWwdrCertUrl,
                    cred.PassKitServiceUrl,
                    HasPassKitAuthToken = !string.IsNullOrWhiteSpace(cred.PassKitAuthToken),
                    // Google
                    cred.IssuerId,
                    cred.ServiceAccountEmail,
                    cred.GooglePrivateKeyUrl
                };

                return new ServiceResult(true, res);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }
        private static string GenerateStrongToken(int length = 48)
        {
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));

            // Base64 string uzunluğu ~ bytes * 4/3 olur → lazım olan minimum byte sayını hesabla
            int minBytes = (int)Math.Ceiling(length * 0.75);
            Span<byte> buffer = stackalloc byte[Math.Max(minBytes, 16)]; // ən azı 16 byte
            RandomNumberGenerator.Fill(buffer);

            string b64 = Convert.ToBase64String(buffer)
                .Replace('+', '-')    // Base64URL
                .Replace('/', '_')
                .TrimEnd('=');        // padding sil

            // Nadir hallarda b64 uzunluğu hədəfdən kiçik ola bilər → tamamla
            while (b64.Length < length)
            {
                Span<byte> extra = stackalloc byte[16];
                RandomNumberGenerator.Fill(extra);
                b64 += Convert.ToBase64String(extra)
                        .Replace('+', '-')
                        .Replace('/', '_')
                        .TrimEnd('=');
            }

            return b64.Substring(0, length);
        }
        private static string EnsureRelative(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;
            var p = path.Replace("\\", "/");
            var idx = p.IndexOf("/resource/", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) return p[idx..];
            idx = p.IndexOf("/uploads/", StringComparison.OrdinalIgnoreCase);
            return idx >= 0 ? p[idx..] : p.TrimStart('/');
        }

        public async Task<ServiceResult> Delete(int id)
        {
            var entity = await _unitOfWork.Repository<WalletCredential>().GetByIdAsync(id);
            if (entity == null)
                return new ServiceResult(false, "Wallet credential not found.");

            entity.IsActive = false;
            _unitOfWork.Repository<WalletCredential>().Update(entity);
            _unitOfWork.Commit();

            return new ServiceResult(true, "Wallet credential deleted successfully.");
        }

        public async Task<ServiceResult> Get(int id)
        {
            var respose = await _repo.GetByIdAsync(id);
            var response = _mapper.Map<GetWalletCredentialDto>(respose);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetAll()
        {
            var partners = await _unitOfWork.Repository<WalletCredential>()
                .Query()
                .Where(w => w.IsActive)
                .Include(w => w.Tenant)
                .OrderBy(w => w.Id)
                .ToListAsync();
            if (partners != null)
            {
                var response = _mapper.Map<ICollection<GetWalletCredentialDto>>(partners);
                return new ServiceResult(true, response);
            }

            return new ServiceResult(false);
        }

        public Task<ServiceResult> GetLast()
        {
            throw new NotImplementedException();
        }

        public Task<ServiceResult> Update(AddWalletCredentialDto dto, int id)
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult> GetByProgram(int id, WalletPlatform programId)
        {
            var partners = await _repo.GetActiveAsync(id,programId);
            if (partners != null)
            {
                var response = _mapper.Map<GetWalletCredentialDto>(partners);
                return new ServiceResult(true, response);
            }

            return new ServiceResult(false);
        }
    }
}
