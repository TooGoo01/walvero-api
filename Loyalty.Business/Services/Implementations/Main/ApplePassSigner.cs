using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class ApplePassSigner : IApplePassSigner
    {
        private readonly IHttpClientFactory _http;
        private readonly ILogger<ApplePassSigner> _log;

        public ApplePassSigner(IHttpClientFactory http, ILogger<ApplePassSigner> log)
        {
            _http = http;
            _log = log;
        }

        /// <summary>
        /// Creates a detached PKCS#7 signature over manifest.json using the Apple Pass certificate.
        /// </summary>
        public byte[] Sign(byte[] manifestBytes, string p12PathOrUrl, string p12Password, string wwdrPathOrUrl)
        {
            try
            {
                var p12Bytes = LoadBytes(p12PathOrUrl);
                var wwdrBytes = LoadBytes(wwdrPathOrUrl);

                using var passCert = new X509Certificate2(
                    p12Bytes,
                    p12Password ?? string.Empty,
                    X509KeyStorageFlags.MachineKeySet |
                    X509KeyStorageFlags.Exportable |
                    X509KeyStorageFlags.PersistKeySet);

                using var wwdrCert = new X509Certificate2(wwdrBytes);

                var content = new ContentInfo(manifestBytes);
                var cms = new SignedCms(content, detached: true);

                var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, passCert)
                {
                    DigestAlgorithm = new Oid("1.3.14.3.2.26"), // SHA-1
                    IncludeOption = X509IncludeOption.ExcludeRoot
                };

                signer.Certificates.Add(wwdrCert);
                signer.SignedAttributes.Add(new Pkcs9SigningTime(DateTime.UtcNow));

                cms.ComputeSignature(signer);
                return cms.Encode();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error signing manifest for pass.");
                throw;
            }
        }


        /// <summary>
        /// Generates manifest.json, signs it, and creates a .pkpass file.
        /// </summary>
        public async Task SignAsync(SignPassRequest req, CancellationToken ct = default)
        {
            try
            {
                // 1️⃣ Build manifest.json with SHA-1 hashes of each file
                var files = Directory.GetFiles(req.WorkingDirectory)
                                     .Where(p =>
                                     {
                                         var n = Path.GetFileName(p);
                                         return n != "manifest.json"
                                             && n != "signature"
                                             && n != Path.GetFileName(req.OutputPkPassPath);
                                     })
                                     .OrderBy(p => p, StringComparer.Ordinal)
                                     .ToList();

                var manifestDict = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var f in files)
                {
                    var sha1 = SHA1.HashData(await File.ReadAllBytesAsync(f, ct));
                    manifestDict[Path.GetFileName(f)] = Convert.ToHexString(sha1).ToLowerInvariant();
                }

                var manifestPath = Path.Combine(req.WorkingDirectory, "manifest.json");
                await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifestDict), ct);

                // 2️⃣ Sign the manifest to produce the signature file
                var manifestBytes = await File.ReadAllBytesAsync(manifestPath, ct);
                var signatureBytes = Sign(manifestBytes, req.AppleCertP12Path, req.AppleCertPassword, req.AppleWwdrCertPath);

                var signaturePath = Path.Combine(req.WorkingDirectory, "signature");
                await File.WriteAllBytesAsync(signaturePath, signatureBytes, ct);

                // 3️⃣ Package all files into .pkpass
                var finalPath = req.OutputPkPassPath;
                var outDir = Path.GetDirectoryName(finalPath)!;
                Directory.CreateDirectory(outDir);

                var tmpPath = Path.Combine(outDir, Path.GetRandomFileName() + ".pkpass");

                using (var zip = ZipFile.Open(tmpPath, ZipArchiveMode.Create))
                {
                    foreach (var file in Directory.EnumerateFiles(req.WorkingDirectory))
                    {
                        var name = Path.GetFileName(file);
                        zip.CreateEntryFromFile(file, name, CompressionLevel.NoCompression);
                    }
                }

                // Move temp to final
                File.Move(tmpPath, finalPath, overwrite: true);
                _log.LogInformation("Pass signed and created: {Path}", finalPath);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error creating Apple pass.");
                throw;
            }
        }

        /// <summary>
        /// Loads bytes from either local file or HTTPS URL.
        /// </summary>
        private byte[] LoadBytes(string pathOrUrl)
        {
            try
            {
                if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
                {
                    var client = _http.CreateClient("wallet");
                    using var res = client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)
                                          .GetAwaiter().GetResult();
                    res.EnsureSuccessStatusCode();
                    return res.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                }

                var full = Path.GetFullPath(pathOrUrl.Trim().Trim('"'));
                return File.ReadAllBytes(full);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error loading certificate or file from {PathOrUrl}", pathOrUrl);
                throw;
            }
        }
    }
}