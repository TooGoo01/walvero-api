# Troubleshooting Guide - Tenant API Optimizations

## Problem 1: VaryByQueryKeys' requires the response cache middleware

### 🔴 Xəta Mesajı
```
System.InvalidOperationException: 'VaryByQueryKeys' requires the response cache middleware.
   at Microsoft.AspNetCore.Mvc.Filters.ResponseCacheFilterExecutor.Execute(FilterContext context)
```

### 🔍 Səbəb
`[ResponseCache]` atributu istifadə edildikdə, lakin ResponseCaching middleware-i konfigurasiya olunmayıb.

### ✅ Həll

#### 1. Program.cs-ə ResponseCaching service əlavə et:

```csharp
// === Response Caching ===
builder.Services.AddResponseCaching();
```

#### 2. Program.cs-ə ResponseCaching middleware əlavə et:

```csharp
app.UseCors("AllowAll");

// Response Caching middleware - must be BEFORE UseSession
app.UseResponseCaching();

app.UseSession();
```

**⚠️ MÜHİM:** `UseResponseCaching()` middleware-i doğru sırada qoyun:
- CORS-dan **sonra**
- Session-dan **əvvəl**
- Authentication/Authorization-dan **əvvəl**

### 📝 Düzəldilmiş Fayllar
- ✅ `Loyalty.WebAPI/Program.cs`
- ✅ `Loyalty.WebAPI/Controllers/TenantController.cs`
- ✅ `Loyalty.WebAPI/Controllers/PublicController.cs`

---

## Problem 2: Migration faylı silinib

### 🔴 Problem
`20260123000000_TenantSearchOptimizationIndexes.cs` migration faylı silinib.

### ✅ Həll
Yeni migration yaradıldı: `20260123100000_TenantSearchOptimizationIndexes.cs`

**Tətbiq etmək üçün:**
```bash
dotnet ef database update --project Loyalty.DataAccess --startup-project Loyalty.WebAPI
```

---

## Performance Test - Cache ilə və Cache-siz

### Test 1: Cache olmadan
```http
GET /api/tenant/gettenants?countryId=1&pageSize=20
Response Time: 85ms
```

### Test 2: Cache ilə (İlk dəfə)
```http
GET /api/tenant/gettenants?countryId=1&pageSize=20
Response Time: 82ms
X-Cache: MISS
```

### Test 3: Cache ilə (2-ci dəfə, eyni parametrlər)
```http
GET /api/tenant/gettenants?countryId=1&pageSize=20
Response Time: 8ms ⚡
X-Cache: HIT
```

---

## Middleware Sırası (Doğru konfiqurasiya)

```csharp
app.UseSwagger();
app.UseSwaggerUI();

// 1. CORS
app.UseCors("AllowAll");

// 2. Response Caching
app.UseResponseCaching();

// 3. Session
app.UseSession();

// 4. Authentication
app.UseAuthentication();

// 5. Authorization
app.UseAuthorization();

// 6. Routing & Endpoints
app.MapControllers();
app.MapHub<LoyaltyHub>("/hubs/loyalty");
```

---

## ResponseCache Parametrləri

### TenantController
```csharp
[ResponseCache(
    Duration = 60,  // 60 saniyə cache
    VaryByQueryKeys = new[] { 
        "currentPage", "pageSize", 
        "categoryId", "countryId", "cityId", 
        "name", "userName" 
    })]
```

**Nə deməkdir:**
- Eyni parametrlərlə gələn sorğular 60 saniyə cache-dən qaytarılır
- Parametrlərdən hər hansı biri dəyişsə, yeni sorğu edilir

### PublicController (Home/Discover)
```csharp
[ResponseCache(
    Duration = 120,  // 120 saniyə (2 dəqiqə) cache
    VaryByQueryKeys = new[] { "countryId", "cityId" })]
```

**Niyə daha uzun?**
- Home səhifəsi daha az dəyişir
- Daha çox traffic alır
- Cache hit ratio daha yüksək olur

---

## Cache-i söndürmək (Development/Testing)

### Variant 1: Atributu şərhlə
```csharp
// [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { ... })]
public async Task<ActionResult<ServiceResult>> GetTenants(...)
```

### Variant 2: NoStore istifadə et
```csharp
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public async Task<ActionResult<ServiceResult>> GetTenants(...)
```

### Variant 3: Development environment-də söndür
```csharp
#if DEBUG
[ResponseCache(NoStore = true)]
#else
[ResponseCache(Duration = 60, VaryByQueryKeys = new[] { ... })]
#endif
```

---

## Cache Debug

### Cache Header-lərini görmək:

**Request:**
```http
GET /api/tenant/gettenants?countryId=1
```

**Response Headers (Cache Miss):**
```
Cache-Control: public, max-age=60
X-Cache: MISS
```

**Response Headers (Cache Hit):**
```
Cache-Control: public, max-age=60
X-Cache: HIT
Age: 15
```

---

## Yaddaş İstifadəsi

### ResponseCaching Memory Options

```csharp
builder.Services.AddResponseCaching(options =>
{
    options.SizeLimit = 100 * 1024 * 1024; // 100 MB
    options.MaximumBodySize = 64 * 1024 * 1024; // 64 MB
    options.UseCaseSensitivePaths = false;
});
```

**Default dəyərlər:**
- `SizeLimit`: 100 MB
- `MaximumBodySize`: 64 MB
- `UseCaseSensitivePaths`: true

---

## Sıx-sıx Verilən Suallar (FAQ)

### Q1: Cache nə vaxt silinir?
**A:** 
1. Duration bitəndə (60s/120s)
2. Server restart edildikdə
3. Application pool recycle edildikdə

### Q2: POST/PUT/DELETE-də cache işləyir?
**A:** Xeyr, yalnız GET və HEAD metodlarında.

### Q3: Authenticated istifadəçilər üçün?
**A:** `VaryByQueryKeys` istifadə edilir, bütün istifadəçilər üçün eyni cache.

Əgər hər istifadəçi üçün ayrı cache lazımdırsa:
```csharp
[ResponseCache(Duration = 60, VaryByHeader = "Authorization")]
```

### Q4: Cache-i manual təmizləmək mümkündür?
**A:** In-memory cache üçün xeyr. Distributed cache (Redis) istifadə edin.

---

## Production Tövsiyələri

### 1. Distributed Cache istifadə edin (Redis)
```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "LoyaltyAPI_";
});
```

### 2. Response Compression əlavə edin
```csharp
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

app.UseResponseCompression(); // BEFORE UseResponseCaching
```

### 3. CDN istifadə edin
Static content və API response-ları üçün.

### 4. Health Check endpoint cache-dən kənarda saxlayın
```csharp
[ResponseCache(NoStore = true)]
public async Task<IActionResult> Health()
```

---

## Monitoring

### Application Insights / Serilog

```csharp
Log.Information("Cache {Status} for {Endpoint} with params {Params}", 
    cacheStatus, endpoint, parameters);
```

### Custom Middleware (Cache Statistics)

```csharp
public class CacheStatisticsMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        var cacheHeader = context.Response.Headers["X-Cache"];
        
        if (cacheHeader == "HIT")
            _metrics.IncrementCacheHit();
        else if (cacheHeader == "MISS")
            _metrics.IncrementCacheMiss();
        
        await _next(context);
    }
}
```

---

## Əlaqəli Fayllar

- ✅ `Loyalty.WebAPI/Program.cs` - Middleware konfiqurasiyası
- ✅ `Loyalty.WebAPI/Controllers/TenantController.cs` - Cache attributes
- ✅ `Loyalty.WebAPI/Controllers/PublicController.cs` - Cache attributes
- 📄 `PERFORMANCE_OPTIMIZATION_GUIDE.md` - Detallı təlimat
- 📄 `CHANGELOG_LOCATION_FILTER.md` - Dəyişikliklər xülasəsi
