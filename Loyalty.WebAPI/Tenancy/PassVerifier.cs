using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

public sealed class PassValidationResult
{
    // Phase status
    public bool ManifestHashesOk { get; set; }
    public bool SignatureMatchesManifest { get; set; }
    public bool? ChainIsValid { get; set; }       // null = yoxlanmayıb
    public List<string> ChainErrors { get; set; } = new();

    // Findings
    public List<string> MissingFiles { get; set; } = new();
    public List<string> HashMismatches { get; set; } = new();
    public List<string> ExtraFiles { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    // Signer info
    public string? SignerSubject { get; set; }
    public string? SignerIssuer { get; set; }
    public string? SignerThumbprint { get; set; }
    public DateTimeOffset? SignerNotBefore { get; set; }
    public DateTimeOffset? SignerNotAfter { get; set; }

    // Pass.json fields observed
    public string? PassTypeIdentifier { get; set; }
    public string? TeamIdentifier { get; set; }

    // Error (first fatal error)
    public string? Error { get; set; }
}

public static class ApplePassValidator
{
    /// <summary>
    /// Verilən .pkpass ZIP faylını müvəqqəti qovluğa açıb VerifyFolder ilə yoxlayır.
    /// </summary>
    public static PassValidationResult VerifyPkpass(string pkpassPath, string? wwdrPath = null)
    {
        var result = new PassValidationResult();
        if (!File.Exists(pkpassPath))
        {
            result.Error = "PKPASS tapılmadı: " + pkpassPath;
            return result;
        }

        string? tempDir = null;
        try
        {
            tempDir = Path.Combine(Path.GetTempPath(), "passchk-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            ZipFile.ExtractToDirectory(pkpassPath, tempDir);
            return VerifyFolder(tempDir, wwdrPath);
        }
        catch (Exception ex)
        {
            result.Error = "VerifyPkpass səhvi: " + ex.Message;
            return result;
        }
        finally
        {
            try { if (tempDir != null && Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Artıq açılmış pass qovluğunu yoxlayır.
    /// </summary>
    public static PassValidationResult VerifyFolder(string folder, string? wwdrPath = null)
    {
        var result = new PassValidationResult();

        try
        {
            if (!Directory.Exists(folder))
                throw new DirectoryNotFoundException("Qovluq tapılmadı: " + folder);

            var manifestPath = Path.Combine(folder, "manifest.json");
            var signaturePath = Path.Combine(folder, "signature");
            var passJsonPath = Path.Combine(folder, "pass.json");

            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("manifest.json tapılmadı", manifestPath);
            if (!File.Exists(signaturePath))
                throw new FileNotFoundException("signature tapılmadı", signaturePath);
            if (!File.Exists(passJsonPath))
                result.Warnings.Add("pass.json tapılmadı (Apple üçün tələb olunur).");

            // ---- 0) pass.json-dan bəzi identifikatorlar ----
            try
            {
                if (File.Exists(passJsonPath))
                {
                    var passDoc = JsonDocument.Parse(File.ReadAllText(passJsonPath));
                    if (passDoc.RootElement.TryGetProperty("passTypeIdentifier", out var pti))
                        result.PassTypeIdentifier = pti.GetString();
                    if (passDoc.RootElement.TryGetProperty("teamIdentifier", out var ti))
                        result.TeamIdentifier = ti.GetString();
                }
            }
            catch (Exception e)
            {
                result.Warnings.Add("pass.json oxunarkən problem: " + e.Message);
            }

            // ---- 1) manifest.json ↔ fayllar (SHA-1) ----
            var manifestJson = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<Dictionary<string, string>>(manifestJson)
                           ?? new Dictionary<string, string>(StringComparer.Ordinal);

            // Manifestdə yazılan hər fayl üçün real hash-i hesabla
            foreach (var kv in manifest)
            {
                var name = NormalizeRel(kv.Key);
                var declHex = (kv.Value ?? "").ToLowerInvariant();
                var filePath = Path.Combine(folder, name.Replace('/', Path.DirectorySeparatorChar));

                if (!File.Exists(filePath))
                {
                    result.MissingFiles.Add(name);
                    continue;
                }

                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var sha1 = SHA1.Create();
                var calc = BitConverter.ToString(sha1.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
                if (!StringComparer.Ordinal.Equals(calc, declHex))
                    result.HashMismatches.Add($"{name} → calc:{calc} ≠ decl:{declHex}");
            }

            // Paketdə olub manifestdə qeyd olunmayan “extra” faylları tap (alt qovluqlarla)
            var allFiles = Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                                    .Select(p => NormalizeRel(Path.GetRelativePath(folder, p)))
                                    .ToList();

            foreach (var rel in allFiles)
            {
                if (rel.Equals("manifest.json", StringComparison.OrdinalIgnoreCase)) continue;
                if (rel.Equals("signature", StringComparison.OrdinalIgnoreCase)) continue;
                if (!manifest.ContainsKey(rel))
                    result.ExtraFiles.Add(rel);
            }

            result.ManifestHashesOk = result.HashMismatches.Count == 0 && result.MissingFiles.Count == 0;

            // ---- 1.1) Minimum fayl tövsiyəsi ----
            // icon.png və logo.png olmaması Apple UI-də problem yarada bilər (warning kimi qeyd edək)
            WarnIfMissing(result, allFiles, "icon.png");
            WarnIfMissing(result, allFiles, "logo.png");

            // ---- 2) signature ↔ manifest.json (PKCS#7 detached) ----
            var manifestBytes = File.ReadAllBytes(manifestPath);
            var sigBytes = File.ReadAllBytes(signaturePath);

            var cms = new SignedCms(new ContentInfo(manifestBytes), detached: true);
            cms.Decode(sigBytes);

            // Signer info
            if (cms.SignerInfos.Count > 0)
            {
                var cert = cms.SignerInfos[0].Certificate;
                if (cert != null)
                {
                    result.SignerSubject = cert.Subject;
                    result.SignerIssuer = cert.Issuer;
                    result.SignerThumbprint = cert.Thumbprint;
                    result.SignerNotBefore = cert.NotBefore;
                    result.SignerNotAfter = cert.NotAfter;
                }
            }

            // Yalnız imza uyğunluğu
            cms.CheckSignature(verifySignatureOnly: true);
            result.SignatureMatchesManifest = true;

            // (Opsional) 2.1 Zəncir yoxlaması
            if (!string.IsNullOrWhiteSpace(wwdrPath) && File.Exists(wwdrPath) && cms.SignerInfos.Count > 0)
            {
                var signer = cms.SignerInfos[0].Certificate;
                if (signer != null)
                {
                    var chain = new X509Chain
                    {
                        ChainPolicy =
                        {
                            RevocationMode = X509RevocationMode.NoCheck,
                            VerificationFlags = X509VerificationFlags.NoFlag
                        }
                    };
                    chain.ChainPolicy.ExtraStore.Add(new X509Certificate2(wwdrPath));

                    bool ok = chain.Build(signer);
                    result.ChainIsValid = ok;
                    if (!ok)
                    {
                        foreach (var s in chain.ChainStatus)
                            result.ChainErrors.Add(s.StatusInformation?.Trim() ?? s.Status.ToString());
                    }
                }
            }

            // ---- 3) pass.json vs sertifikat subject uyğunluğu ----
            if (!string.IsNullOrWhiteSpace(result.PassTypeIdentifier) && !string.IsNullOrWhiteSpace(result.SignerSubject))
            {
                if (!result.SignerSubject.Contains(result.PassTypeIdentifier, StringComparison.OrdinalIgnoreCase))
                    result.Warnings.Add($"passTypeIdentifier sertifikat subject ilə uyğun gəlmir: {result.PassTypeIdentifier}");
            }

            if (!string.IsNullOrWhiteSpace(result.TeamIdentifier) && !string.IsNullOrWhiteSpace(result.SignerSubject))
            {
                // Apple-da TeamID çox vaxt OU=TEAMID kimi subject-də olur
                if (!result.SignerSubject.Contains($"OU={result.TeamIdentifier}", StringComparison.OrdinalIgnoreCase))
                    result.Warnings.Add($"teamIdentifier sertifikat subject ilə uyğun gəlmir: {result.TeamIdentifier}");
            }
        }
        catch (CryptographicException ex)
        {
            result.SignatureMatchesManifest = false;
            result.Error = "Kripto yoxlama səhvi: " + ex.Message;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    private static string NormalizeRel(string rel)
        => rel.Replace('\\', '/');

    private static void WarnIfMissing(PassValidationResult res, List<string> allFiles, string needed)
    {
        if (!allFiles.Any(f => f.Equals(needed, StringComparison.OrdinalIgnoreCase)))
            res.Warnings.Add($"Tövsiyə olunan fayl yoxdur: {needed}");
    }
}
