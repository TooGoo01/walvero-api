# POS Batch Webhook — Inteqrasiya Sənədi

POS sistemi (iiko və ya analoji partner) bir HTTP sorğusu ilə **bir neçə loyalty əməliyyatı** (xal qazanma / xal istifadə) Walvero Loyalty platformasına göndərir. Hər əməliyyat asinxron işlənir — server cavabı dərhal qaytarır, müştəri kartlarını arxa fonda yeniləyir.

---

## 1. Endpoint

```
POST https://appapi.walvero.com/api/wallet/pos_batch_webhook
```

| Aspekt | Dəyər |
|---|---|
| Method | `POST` |
| Path | `/api/wallet/pos_batch_webhook` |
| Content-Type | `application/json` |
| Charset | `UTF-8` |
| Auth header | **Yoxdur** — tenant payload-dakı `organizationId` üzərindən resolve olunur |
| Response status | Həmişə `200 OK` (webhook best practice — retry storm-un qarşısı) |
| Response body | JSON, processing nəticəsi və xəta sahələri orada |
| İdempotency | Var — eyni `transactionId` + `walletId` təkrar göndərilərsə bir dəfə işlənir |
| Processing | Asinxron (Hangfire background worker) |

---

## 2. Tələblər

Webhook işləməsi üçün partner sisteminin Walvero-da əvvəlcədən **TenantIntegration** qeydiyyatı olmalıdır:

- `Source` = `iiko_pos`
- `ExternalVenueId` = partner tərəfindən göndəriləcək `organizationId` ilə **dəqiq uyğun** olmalıdır
- `LoyaltyProgramId` təyin edilməlidir (hansı proqramın kartları update olunacaq)
- Hər müştəri üçün `PosCustomerMapping` row mövcud olmalıdır:
  - `TenantIntegrationId` = yuxarıdakı integration row
  - `ExternalCustomerId` = partner sistemindəki müştəri ID-si (payload-dakı `customerId`)
  - `CustomerId` = Walvery daxili müştəri ID-si

Bu mapping-lər yoxdursa, əməliyyat `Customer not found for externalId: ...` xətası ilə dropp olunur.

---

## 3. Request Body

### Top-level struktur

```json
{
  "organizationId": "string (REQUIRED)",
  "uocId": "string (optional fallback)",
  "operations": [ /* PosBatchOperation[] */ ]
}
```

| Sahə | Tip | Tələb | Təsvir |
|---|---|---|---|
| `organizationId` | string | **TƏLƏB** | Partner sistemindəki venue/organization ID. `TenantIntegration.ExternalVenueId` ilə eyni olmalıdır |
| `uocId` | string | opsional | Əgər `organizationId` tenant tapmırsa, fallback kimi `uocId` ilə yenidən resolve olunur |
| `operations` | array | **TƏLƏB** | Loyalty əməliyyatları siyahısı. Boş ola bilməz |

### Operation obyekti

`operations` array-dakı hər bir element:

```json
{
  "transactionId": "string (REQUIRED)",
  "customerId": "string (REQUIRED)",
  "transactionType": "earn | redeem",
  "sum": 12.5,
  "balance": 137.5,
  "walletId": "wallet-xyz",
  "orderId": "order-9001",
  "orderNumber": 9001,
  "isDelivery": false,
  "terminalGroupId": "terminal-A",
  "phone": "+994501234567",
  "id": "external-event-id",
  "notificationType": 1,
  "subscriptionPassword": null,
  "changedOn": "2026-06-11T10:15:30.000Z"
}
```

| Sahə | Tip | Tələb | Təsvir |
|---|---|---|---|
| `transactionId` | string | **TƏLƏB** | Partner sistemindəki unikal əməliyyat ID-si. Idempotency açarının bir hissəsi |
| `customerId` | string | **TƏLƏB** | Partner sistemindəki müştəri ID-si. `PosCustomerMapping.ExternalCustomerId` ilə uyğun olmalıdır |
| `transactionType` | string | məsləhət | `"earn"` / `"redeem"` və s. — log/audit üçün. Məntiq `sum` işarəsindən hesablanır (müsbət = earn, mənfi = redeem) |
| `sum` | decimal | **TƏLƏB** | Əməliyyatın **dəyişiklik miqdarı** (delta). Müsbət = xal qazanma, mənfi = xal istifadə |
| `balance` | decimal | **TƏLƏB** | Əməliyyatdan **sonrakı total balans** (authoritative state). Walvero məhz bu dəyərdən kart balansını yeniləyir — drift olmasın deyə |
| `walletId` | string | məsləhət | Partner tərəfindəki wallet ID. Idempotency açarının ikinci hissəsi (`{transactionId}:{walletId}`) |
| `orderId` | string | opsional | Sifariş ID-si (log/audit) |
| `orderNumber` | int | opsional | Sifariş nömrəsi |
| `isDelivery` | bool | opsional | Çatdırılma əməliyyatıdır? |
| `terminalGroupId` | string | opsional | Terminal qrupu ID-si |
| `phone` | string | opsional | Müştəri telefonu (audit üçün) |
| `id` | string | opsional | Xarici event ID |
| `notificationType` | int | opsional | iiko notification type kodu |
| `subscriptionPassword` | string | opsional | iiko qiymət xidməti şifrəsi |
| `changedOn` | string (ISO-8601) | opsional | Partner tərəfində əməliyyatın baş verdiyi vaxt |

### Vacib qaydalar

- `sum` = **delta**, `balance` = **absolute state**. İkisi də göndərilməlidir. Walvero `balance`-ı authoritative qəbul edir (drift bağ olmasın), `sum`-ı yalnız Transaction log və `LifetimePoints` artımı üçün istifadə edir.
- Idempotency açarı: `"{transactionId}:{walletId}"`. Eyni payload təkrar göndərilsə, **balans dəyişmir**, response duplicate olduğunu bildirir.
- `customerId` mütləq Walvero-da əvvəlcədən mapping olunmuş ID olmalıdır. Yeni müştəri yaratmaq bu endpoint-in işi deyil.
- Bütün decimal sahələr nöqtə (`.`) ilə (məs. `12.50`), vergüllə (`,`) yox.

---

## 4. Tam Request nümunəsi

```http
POST /api/wallet/pos_batch_webhook HTTP/1.1
Host: appapi.walvero.com
Content-Type: application/json

{
  "organizationId": "8a3c5b1e-9d2f-4e7a-b8c9-1f2e3d4c5b6a",
  "uocId": null,
  "operations": [
    {
      "transactionId": "batch-2026-06-11-001",
      "customerId": "ext-cust-1001",
      "transactionType": "earn",
      "sum": 12.5,
      "balance": 137.5,
      "walletId": "wallet-1001",
      "orderId": "order-9001",
      "orderNumber": 9001,
      "terminalGroupId": "terminal-A",
      "isDelivery": false,
      "phone": "+994501234567",
      "changedOn": "2026-06-11T10:15:30.000Z",
      "notificationType": 1
    },
    {
      "transactionId": "batch-2026-06-11-002",
      "customerId": "ext-cust-1002",
      "transactionType": "redeem",
      "sum": -5.0,
      "balance": 20.0,
      "walletId": "wallet-1002",
      "orderId": "order-9002",
      "orderNumber": 9002
    },
    {
      "transactionId": "batch-2026-06-11-003",
      "customerId": "ext-cust-1003",
      "transactionType": "earn",
      "sum": 8.0,
      "balance": 48.0,
      "walletId": "wallet-1003"
    }
  ]
}
```

curl analoqu:
```bash
curl -X POST https://appapi.walvero.com/api/wallet/pos_batch_webhook \
  -H "Content-Type: application/json" \
  -d @batch.json
```

---

## 5. Response

HTTP status **həmişə 200**. Cavab body-də nəticə:

### 5.1. Uğurlu (bütün əməliyyatlar enqueue olundu)

```json
{
  "success": true,
  "total": 3,
  "enqueued": 3,
  "failedToEnqueue": 0,
  "message": "Accepted for background processing",
  "errors": []
}
```

> Diqqət: `success: true` o deməkdir ki, əməliyyatlar **növbəyə (queue) qəbul olundu**, yəni processing dərhal başlayacaq. Hər əməliyyatın **faktiki nəticəsi** (kart tapıldı / xal yazıldı / wallet push uğurlu) background-da olur. Real-time per-item nəticə bu endpoint qaytarmır.

### 5.2. Tenant tapılmadı

```json
{
  "success": false,
  "message": "Tenant integration not found"
}
```

Səbəb: `organizationId` (və `uocId` fallback) heç bir `TenantIntegration` row ilə uyğun gəlmir. Walvero komandası ilə əlaqə.

### 5.3. Validation xətaları

```json
{ "success": false, "message": "organizationId is required" }
```
```json
{ "success": false, "message": "operations array is required" }
```

### 5.4. Bəzi əməliyyatlar enqueue olunmadı (nadir)

```json
{
  "success": false,
  "total": 5,
  "enqueued": 4,
  "failedToEnqueue": 1,
  "message": "Accepted for background processing",
  "errors": [
    {
      "index": 2,
      "transactionId": "batch-2026-06-11-XYZ",
      "customerId": "ext-cust-9999",
      "message": "Failed to enqueue"
    }
  ]
}
```

### 5.5. Daxili xəta

```json
{ "success": false, "message": "Internal error" }
```

---

## 6. İdempotency

- Açar: `"{transactionId}:{walletId}"`
- Davranış: əgər eyni açar artıq Walvero-da işlənibsə (`Transaction.OrderId` ilə uyğunluq), background job onu **skip** edir. Kart balansı və `LifetimePoints` dəyişmir, ikinci dəfə wallet push olmur.
- Bu o deməkdir ki, **partner tərəfindən retry etmək təhlükəsizdir**. Network timeout, 5xx və ya naməlum vəziyyətdə eyni payload-ı təkrar göndərmək olar.

---

## 7. Asinxron processing — gözləntilər

1. HTTP sorğu gəlir → validation + tenant resolve (millisaniyələr).
2. Hər operation Hangfire queue-suna yazılır.
3. HTTP 200 + JSON cavab — adətən **<500ms**.
4. Background worker hər jobu paralel işləyir:
   - Müştəri mapping-i tap (`PosCustomerMapping`)
   - Aktiv `LoyaltyCard` götür
   - `CurrentPoints` / `CurrentSteps` / `CompletedCycles` / `LifetimePoints` yenilə
   - `Transaction` row yarat (audit log)
   - Apple Wallet + Google Wallet push (müştərinin telefonunda kart real-time yenilənir)
5. Job daxilində exception olarsa, Hangfire **3 dəfə avtomatik retry** edir. Sonra "Failed" queue-ya düşür və Walvero Tenant Admin-i dashboard-dan görür.

---

## 8. Xətalar və davranış cədvəli

| Səbəb | HTTP status | `success` | `message` | Düzəliş |
|---|---|---|---|---|
| `organizationId` yoxdur | 200 | false | `organizationId is required` | Sahəni əlavə et |
| `operations` boş və ya yoxdur | 200 | false | `operations array is required` | Ən azı 1 op göndər |
| Tenant tapılmadı | 200 | false | `Tenant integration not found` | Walvero ilə əlaqə — venue ID konfiqurasiyası |
| Operasiya enqueue olmadı (SQL issue) | 200 | false | `errors[].message="Failed to enqueue"` | Retry et |
| Daxili xəta (catch-all) | 200 | false | `Internal error` | Walvero ilə əlaqə |
| Müştəri mapping yoxdur (background) | — | — | log-da: `Customer not found for externalId: X` | Walvero-da `PosCustomerMapping` row əlavə et |
| Aktiv kart yoxdur (background) | — | — | log-da: `Active loyalty card not found` | Müştəriyə kart yaradıl |
| `transactionId` boş (background) | — | — | log-da: `TransactionId is required` | Hər op-da `transactionId` göndər |
| Dublikat əməliyyat (background) | — | — | log-da: `Already processed (duplicate)` | Davranış doğrudur, narahat olma |

**"Background" sətirlər partnerə sync olaraq görünmür** — yalnız Walvero log-larında və Hangfire dashboard-da yer alır.

---

## 9. `transactionType` dəyərləri

`sum`-un işarəsi delta-nın istiqamətini müəyyən edir, amma `transactionType` audit log-da string kimi saxlanılır (Walvero `Transaction.Description = "iiko {transactionType}"`). Tövsiyə olunan dəyərlər:

| Dəyər | Mənası | `sum` |
|---|---|---|
| `earn` | Xal qazanma (al-ver) | müsbət |
| `redeem` | Xal istifadə (endirim) | mənfi |
| `refund` | Geri qaytarma | mənfi (və ya müsbət — partner qaydası) |
| `adjust` | Manual düzəliş | müsbət/mənfi |

---

## 10. Test / Sandbox

İndilik staging endpoint partner ilə razılaşdırılır. Test üçün:

1. Walvero komandasından `organizationId` və test `customerId`-ləri al.
2. Yuxarıdakı `curl` nümunəsini istifadə et.
3. Cavabda `success: true, enqueued: N` görünməlidir.
4. ~5 saniyə gözlə → Walvero dashboard-da müştərinin kart balansı yenilənib olmalıdır.
5. Eyni payload-ı təkrar göndər → balans dəyişməməlidir (idempotency).

---

## 11. Limitlər

| Aspekt | Limit |
|---|---|
| `operations` array maksimum | Texniki limit yoxdur, amma **1 sorğuda <500 op** tövsiyə olunur (HTTP timeout və SQL transaction üçün) |
| Sorğunun maksimum body ölçüsü | ASP.NET Core default ~28 MB |
| Rate limit | Hazırda yoxdur, lazım gələrsə tətbiq olunacaq |
| Retry / timeout | Partner sıxlığı: 5xx və ya timeout halında **eyni payload** retry oluna bilər (idempotency var) |

---

## 12. Əlaqə

- Texniki suallar: Walvero backend komandası
- TenantIntegration / PosCustomerMapping konfiqurasiyası: Walvero admin paneli (Tenant Admin)
- Hangfire dashboard (job nəzarəti — yalnız daxili istifadə): `/hangfire`
