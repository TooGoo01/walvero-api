using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Ocsp;
using System.Collections.Concurrent;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.ConstrainedExecution;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using File = System.IO.File;

public sealed class ApnsPasskitPushService : IPasskitPushService, IDisposable
{
    private readonly IUnitOfWork _uow;
    private readonly IWalletCredentialProvider _creds;
    private readonly ILogger<ApnsPasskitPushService> _log;
    private readonly ConcurrentDictionary<int, HttpClientWrapper> _httpClients = new();
    private bool _disposed;

    private class HttpClientWrapper : IDisposable
    {
        public HttpClient Client { get; }
        public X509Certificate2 Certificate { get; }

        public HttpClientWrapper(HttpClient client, X509Certificate2 certificate)
        {
            Client = client;
            Certificate = certificate;
        }

        public void Dispose()
        {
            Client?.Dispose();
            Certificate?.Dispose();
        }
    }

    public ApnsPasskitPushService(
        IUnitOfWork uow,
        IWalletCredentialProvider creds,
        ILogger<ApnsPasskitPushService> log)
    {
        _uow = uow;
        _creds = creds;
        _log = log;
    }

  private async Task<HttpClientWrapper> GetOrCreateHttpClientAsync(int tenantId, CancellationToken ct)
{
    if (_httpClients.TryGetValue(tenantId, out var existing))
    {
        return existing;
    }

    var a = await _creds.GetAppleAsync(tenantId, ct);
    
    // 1) Əvvəlcə raw bytes olaraq yüklə
    byte[] certBytes = await File.ReadAllBytesAsync(a.CertP12Path, ct);
    
    // 2) Certificate-i yüklə və yoxla
    var passCert = new X509Certificate2(
        certBytes,
        a.CertPassword,
        X509KeyStorageFlags.MachineKeySet |
        X509KeyStorageFlags.PersistKeySet |
        X509KeyStorageFlags.Exportable);

        Console.WriteLine($"Subject: {passCert.Subject}");
        Console.WriteLine($"Issuer: {passCert.Issuer}");
        Console.WriteLine($"Valid From: {passCert.NotBefore}");
        Console.WriteLine($"Valid To: {passCert.NotAfter}");
        Console.WriteLine($"Has Private Key: {passCert.HasPrivateKey}");
        Console.WriteLine($"Thumbprint: {passCert.Thumbprint}");

        // 3) Certificate məlumatlarını log et (debug üçün)
        _log.LogInformation("Certificate loaded: Subject={Subject}, Issuer={Issuer}, Valid={ValidFrom} to {ValidTo}, HasPrivateKey={HasKey}",
        passCert.Subject,
        passCert.Issuer,
        passCert.NotBefore,
        passCert.NotAfter,
        passCert.HasPrivateKey);

    // 4) Yoxla ki, müddəti bitməyib
    if (passCert.NotAfter < DateTime.UtcNow)
    {
        throw new InvalidOperationException($"Certificate expired on {passCert.NotAfter}");
    }

    if (!passCert.HasPrivateKey)
    {
        throw new InvalidOperationException("Certificate does not contain a private key");
    }

        // 5) Sandbox vs Production - Subject-dən anla
        // Sandbox cert: "Apple Push Services: ..."  
        // Production cert: "Apple Production IOS Push Services: ..."
        bool isSandbox = false;
            
            //passCert.Subject.Contains("Development", StringComparison.OrdinalIgnoreCase) ||
            //         passCert.Subject.Contains("Sandbox", StringComparison.OrdinalIgnoreCase) ||
            //         !passCert.Subject.Contains("Production", StringComparison.OrdinalIgnoreCase);

    var baseUri = isSandbox 
        ? new Uri("https://api.sandbox.push.apple.com/")  // Sandbox
        : new Uri("https://api.push.apple.com/");         // Production

    _log.LogInformation("Using APNs endpoint: {Endpoint} (Sandbox={IsSandbox})", baseUri, isSandbox);

    var certCollection = new X509CertificateCollection { passCert };

    var handler = new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 10,
        ConnectTimeout = TimeSpan.FromSeconds(10)
    };

    handler.SslOptions = new SslClientAuthenticationOptions
    {
        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
        ClientCertificates = certCollection,
        ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http2 },
        // APNs serverlərini validate et
        RemoteCertificateValidationCallback = (sender, cert, chain, errors) =>
        {
            if (errors == SslPolicyErrors.None)
                return true;

            _log.LogWarning("SSL validation error: {Errors}", errors);
            return false;
        }
    };

    var httpClient = new HttpClient(handler, disposeHandler: true)
    {
        BaseAddress = baseUri,
        DefaultRequestVersion = HttpVersion.Version20,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
        Timeout = TimeSpan.FromSeconds(30)
    };

    var wrapper = new HttpClientWrapper(httpClient, passCert);
    
    return _httpClients.GetOrAdd(tenantId, wrapper);
}

    public async Task NotifyUpdatedAsync(int tenantId, string passTypeIdentifier, string serialNumber,string passUrl, CancellationToken ct = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ApnsPasskitPushService));

        if (string.IsNullOrWhiteSpace(passTypeIdentifier))
            throw new ArgumentException("passTypeIdentifier boş ola bilməz.");

        var wrapper = await GetOrCreateHttpClientAsync(tenantId, ct);
        var http = wrapper.Client;

        var repo = _uow.Repository<PassDeviceRegistration>();
        var regs = repo.GetAll(r =>
            r.TenantId == tenantId &&
            r.PassTypeIdentifier == passTypeIdentifier &&
            r.SerialNumber == serialNumber && r.IsActive).ToList();

        if (regs.Count == 0)
        {
            _log.LogInformation("APNs: device yoxdur. PTI={PTI}, Serial={Serial}", passTypeIdentifier, serialNumber);
            return;
        }


        var now = DateTime.UtcNow;
        foreach (var r in regs)
        {
            r.LastUpdatedUtc = now;
            r.EditDate = now; // varsa
            r.PassUrl = passUrl;
            _uow.Repository<PassDeviceRegistration>().Update(r);
        }
        _uow.Commit();
        _log.LogInformation("Marked {Count} registrations updated at {Now:o}", regs.Count, now);


        foreach (var reg in regs)
        {
            ct.ThrowIfCancellationRequested();
            var token = reg.PushToken?.Trim();
            if (string.IsNullOrWhiteSpace(token) || !IsValidPushToken(token))
            {
                _log.LogWarning("Invalid or missing push token: {Token}", token);
                continue;
            }
            var payload = new
            {
                aps = new
                {
                    alert = new { title="Salam", body="Kart melumatlari yenilendi!" },
                    sound = "default",
                    badge = 1
                },
                lastUpdated = now.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };
            var json = JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post, $"3/device/{token}")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
                Version = HttpVersion.Version20
            };

            req.Headers.TryAddWithoutValidation("apns-push-type", "alert");
            req.Headers.TryAddWithoutValidation("apns-topic", passTypeIdentifier);
            req.Headers.TryAddWithoutValidation("apns-priority", "5");

            HttpResponseMessage? res = null;
            for (int retry = 0; retry < 3; retry++)
            {
                try
                {
                    res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                    var apnsId = res.Headers.TryGetValues("apns-id", out var ids) ? string.Join(",", ids) : "<none>";

                    if (res.IsSuccessStatusCode)
                    {
                        _log.LogInformation("APNs OK token={Token} apns-id={ApnsId} protocol={Protocol}", token, apnsId, res.Version);
                        break;
                    }

                    var body = await res.Content.ReadAsStringAsync(ct);
                    var code = (int)res.StatusCode;

                    if (code == 410 || code == 400)
                    {
                        _log.LogWarning("APNs invalid token (remove). token={Token} status={Status} apns-id={ApnsId} body={Body}", token, res.StatusCode, apnsId, body);
                        break;
                    }
                    else if (code == 429)
                    {
                        _log.LogWarning("APNs 429 rate-limit. token={Token} apns-id={ApnsId} body={Body}", token, apnsId, body);
                        await Task.Delay(TimeSpan.FromSeconds(30), ct); // Wait longer for rate limits
                    }
                    else
                    {
                        _log.LogWarning("APNs error. token={Token} status={Status} apns-id={ApnsId} body={Body}", token, res.StatusCode, apnsId, body);
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retry)), ct);
                    }
                }
                catch (HttpRequestException ex)
                {
                    _log.LogWarning(ex, "APNs retry {Retry}/{MaxRetries} failed for token={Token}", retry + 1, 3, token);
                    if (retry == 2) _log.LogError(ex, "APNs request failed after retries for token={Token}", token);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retry)), ct);
                }
                catch (TaskCanceledException ex)
                {
                    _log.LogWarning(ex, "APNs request timeout for token={Token}", token);
                    if (retry == 2) _log.LogError(ex, "APNs request timed out after retries for token={Token}", token);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retry)), ct);
                }
                finally
                {
                    res?.Dispose();
                }
            }
        }
    }

    private bool IsValidPushToken(string token)
    {
        return !string.IsNullOrWhiteSpace(token) && token.Length == 64 && token.All(c => char.IsLetterOrDigit(c));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var wrapper in _httpClients.Values)
        {
            try
            {
                wrapper.Dispose();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error disposing HTTP client wrapper");
            }
        }

        _httpClients.Clear();
    }

    public async Task NotifyUpdatedMessageAsync(int tenantId, string passTypeIdentifier, string serialNumber, string header, string description, CancellationToken ct = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ApnsPasskitPushService));

        if (string.IsNullOrWhiteSpace(passTypeIdentifier))
            throw new ArgumentException("passTypeIdentifier boş ola bilməz.");

        var wrapper = await GetOrCreateHttpClientAsync(tenantId, ct);
        var http = wrapper.Client;

        var repo = _uow.Repository<PassDeviceRegistration>();
        var regs = repo.GetAll(r =>
            r.TenantId == tenantId &&
            r.PassTypeIdentifier == passTypeIdentifier &&
            r.SerialNumber == serialNumber && r.IsActive).ToList();

        if (regs.Count == 0)
        {
            _log.LogInformation("APNs: device yoxdur. PTI={PTI}, Serial={Serial}", passTypeIdentifier, serialNumber);
            return;
        }


        
       
       


        foreach (var reg in regs)
        {
            ct.ThrowIfCancellationRequested();
            var token = reg.PushToken?.Trim();
            if (string.IsNullOrWhiteSpace(token) || !IsValidPushToken(token))
            {
                _log.LogWarning("Invalid or missing push token: {Token}", token);
                continue;
            }
            var payload = new
            {
                aps = new
                {
                    alert = new { title = header, body = description },
                    sound = "default",
                    badge = 1
                },
               
            };
            var json = JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post, $"3/device/{token}")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
                Version = HttpVersion.Version20
            };

            req.Headers.TryAddWithoutValidation("apns-push-type", "alert");
            req.Headers.TryAddWithoutValidation("apns-topic", passTypeIdentifier);
            req.Headers.TryAddWithoutValidation("apns-priority", "10");

            HttpResponseMessage? res = null;
            for (int retry = 0; retry < 3; retry++)
            {
                try
                {
                    res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                    var apnsId = res.Headers.TryGetValues("apns-id", out var ids) ? string.Join(",", ids) : "<none>";

                    if (res.IsSuccessStatusCode)
                    {
                        _log.LogInformation("APNs OK token={Token} apns-id={ApnsId} protocol={Protocol}", token, apnsId, res.Version);
                        break;
                    }

                    var body = await res.Content.ReadAsStringAsync(ct);
                    var code = (int)res.StatusCode;

                    if (code == 410 || code == 400)
                    {
                        _log.LogWarning("APNs invalid token (remove). token={Token} status={Status} apns-id={ApnsId} body={Body}", token, res.StatusCode, apnsId, body);
                        break;
                    }
                    else if (code == 429)
                    {
                        _log.LogWarning("APNs 429 rate-limit. token={Token} apns-id={ApnsId} body={Body}", token, apnsId, body);
                        await Task.Delay(TimeSpan.FromSeconds(30), ct); // Wait longer for rate limits
                    }
                    else
                    {
                        _log.LogWarning("APNs error. token={Token} status={Status} apns-id={ApnsId} body={Body}", token, res.StatusCode, apnsId, body);
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retry)), ct);
                    }
                }
                catch (HttpRequestException ex)
                {
                    _log.LogWarning(ex, "APNs retry {Retry}/{MaxRetries} failed for token={Token}", retry + 1, 3, token);
                    if (retry == 2) _log.LogError(ex, "APNs request failed after retries for token={Token}", token);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retry)), ct);
                }
                catch (TaskCanceledException ex)
                {
                    _log.LogWarning(ex, "APNs request timeout for token={Token}", token);
                    if (retry == 2) _log.LogError(ex, "APNs request timed out after retries for token={Token}", token);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retry)), ct);
                }
                finally
                {
                    res?.Dispose();
                }
            }
        }
    }
}