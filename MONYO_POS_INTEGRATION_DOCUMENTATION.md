# Monyo POS və Loyallıq Sistemi İnteqrasiya Dokumentasiyası

Bu sənəd, Loyalty API layihəsində Monyo POS inteqrasiyası üçün edilən dəyişiklikləri təsvir edir.

---

## 1. Ümumi Baxış

İnteqrasiya iki istiqamətdə işləyir:

| İstiqamət   | Çağıran                    | Məqsəd                                                                 |
|-------------|----------------------------|------------------------------------------------------------------------|
| **Outbound**| Loyallıq API → Monyo       | Müştəriləri Monyo POS-a əlavə/yenilə (POST /pos/customers)            |
| **Inbound** | Monyo POS → Loyallıq API   | Cüzdan məlumatı, OTP, sifariş bağlananda collect əməliyyatları         |

---

## 2. Verilənlər Bazası Dəyişiklikləri

### 2.1 IntegrationType (cədvəl)
- **Təyinat:** Mövcud kassa/POS növlərinin siyahısı (reference data)
- **Sahələr:** `Id`, `Name`, `Source` (unique, məs: `monyo_pos`), `IsActive`
- **Seed:** "Monyo POS", Source = `monyo_pos`

### 2.2 TenantIntegration (cədvəl)
- **Təyinat:** Hər tenant üçün inteqrasiya konfiqurasiyası
- **Əsas sahələr:**
  - `TenantId`, `IntegrationTypeId` (FK)
  - `DisplayName`, `ExternalVenueId` (Monyo venue_id)
  - `LoyaltyProgramId` — bu inteqrasiya üçün hansı loyallıq proqramı
  - `ApiBaseUrl` — Monyo API ünvanı
  - `ApiTokenEncrypted` — şifrəli Monyo token
  - `WalletTokenHash` — bizim verdiyimiz Bearer token-un SHA256 hash-i
  - `IsActive`

### 2.3 PosProductEarnRule (cədvəl)
- **Təyinat:** Collect zamanı hansı məhsulların neçə step/point verdiyini təyin edir
- **Sahələr:** `TenantIntegrationId`, `ExternalProductId` (kassa məhsul ID-si), `ProductName`, `StepsPerUnit`, `PointsPerUnit`

### 2.4 PosCustomerMapping (cədvəl)
- **Təyinat:** Monyo customer_id ilə bizim CustomerId arasında əlaqə
- **Sahələr:** `TenantIntegrationId`, `ExternalCustomerId`, `CustomerId`

### 2.5 Reward.ExtendedProductId (sahə əlavəsi)
- **Təyinat:** Pulsuz mükafat məhsulunun Monyo POS-da product_id-si (get_wallet və set_verify cavablarında istifadə olunur)

---

## 3. Təhlükəsizlik

### 3.1 Token idarəetməsi
- **ISensitiveDataProtector:** ApiToken və s. sahələr üçün şifrələmə (Data Protection API)
- **IWalletTokenGenerator:** Yeni wallet token generasiyası (cryptographically random)
- **Wallet token doğrulama:** Gələn request-də Bearer token hash-lə müqayisə olunur (plain text DB-də saxlanmır)

### 3.2 ITenantIntegrationResolver
- `ResolveByWalletTokenAsync` — Bearer token hash əsasında TenantIntegration tapır
- `ResolveByVenueAndSourceAsync` — venue_id + source əsasında tapır

### 3.3 WalletWebhookAuthFilter / WalletWebhookAuthAttribute
- Monyo-dan gələn webhook request-ləri yalnız `Authorization: Bearer {wallet_token}` ilə qəbul edilir
- JWT yox, bizim verdiyimiz sabit token istifadə olunur

---

## 4. Inbound Webhook Endpointləri (Monyo bizə çağırır)

**Base route:** `api/wallet`  
**Auth:** `Authorization: Bearer {wallet_token}`

| Endpoint     | Request                               | Məqsəd                                              |
|--------------|----------------------------------------|-----------------------------------------------------|
| POST get_wallet  | venue_id, barcode, source              | Cüzdan balansı, loyalty stamp/points, rewards, cashback |
| POST set_redeem  | venue_id, barcode, campaign_id, type, amount, source | OTP göndərmək (loyalty və ya cashback üçün)   |
| POST set_verify  | venue_id, barcode, campaign_id, otp, type, amount, order_id, source | OTP doğrulama və discount/product_id qaytarma |
| POST collect     | id (order_id), venue_id, customer_id, status (closed/deleted), total, items[] | Sifariş bağlandıqda xal artırımı, storno zamanı geri çevirmə |

### Collect məntiqi
- **closed:** items[] üzrə PosProductEarnRule-a görə step/point əlavə olunur; Transaction yaradılır
- **deleted:** Eyni OrderId ilə əvvəlki transaction(lar) geri çevrilir

---

## 5. Outbound (Biz Monyo-ya çağırırıq)

### 5.1 IMonyoPosApiClient / MonyoPosApiClient
- `AddCustomersAsync` — yeni müştəriləri Monyo-ya əlavə etmək
- `UpdateCustomersAsync` — mövcud müştəriləri yeniləmək

### 5.2 ICustomerSyncService / CustomerSyncService
- **Avtomatik sync:** `CustomerEnrollmentService.StartEnrollmentAsync` — müştəri kart aldıqda və ya aktivləşdikdə Monyo-ya sync
- **Manual sync:** PosController vasitəsilə:
  - `POST api/pos/sync-customers` — bütün müştəriləri sync
  - `POST api/pos/sync-customers/{customerId}` — tək müştəri sync

---

## 6. Admin API Endpointləri

### 6.1 TenantIntegration CRUD
**Route:** `api/tenant-integrations`  
**Auth:** JWT, TenantAdminOrModerator policy

| Metod | Endpoint | Təsvir |
|-------|----------|--------|
| GET   | /types   | İnteqrasiya növləri siyahısı (məs: Monyo POS) |
| POST  | /        | Yeni inteqrasiya yarat (response-da `walletToken` bir dəfə göstərilir) |
| GET   | /        | Tenant üçün inteqrasiyalar siyahısı |
| GET   | /{id}    | İnteqrasiya detalları |
| PUT   | /{id}    | İnteqrasiya yenilə |
| DELETE| /{id}    | İnteqrasiya deaktiv et (soft delete) |
| POST  | /{id}/regenerate-wallet-token | Yeni wallet token generasiya et |

### 6.2 PosProductEarnRule CRUD
**Route:** `api/tenant-integrations/{tenantIntegrationId}/earn-rules`  
**Auth:** JWT, TenantAdminOrModerator policy

| Metod | Endpoint | Təsvir |
|-------|----------|--------|
| POST  | /        | Uduş artıran məhsul qaydası əlavə et |
| GET   | /        | Bu inteqrasiya üçün qaydalar siyahısı |
| PUT   | /{id}    | Qayda yenilə |
| DELETE| /{id}    | Qayda sil |

---

## 7. Yeni/Redaktə Edilmiş Fayllar

### Entity
- `Loyalty.DataAccess/Entities/Main/IntegrationType.cs`
- `Loyalty.DataAccess/Entities/Main/TenantIntegration.cs`
- `Loyalty.DataAccess/Entities/Main/PosProductEarnRule.cs`
- `Loyalty.DataAccess/Entities/Main/PosCustomerMapping.cs`
- `Reward.ExternalProductId` sahəsi

### Repository
- `IIntegrationTypeRepository` / `IntegrationTypeRepository`
- `ITenantIntegrationRepository` / `TenantIntegrationRepository`
- `IPosProductEarnRuleRepository` / `PosProductEarnRuleRepository`
- `IPosCustomerMappingRepository` / `PosCustomerMappingRepository`

### Servis
- `ITenantIntegrationResolver` / `TenantIntegrationResolver`
- `ITenantIntegrationService` / `TenantIntegrationService`
- `IPosProductEarnRuleService` / `PosProductEarnRuleService`
- `IWalletWebhookService` / `WalletWebhookService`
- `IMonyoPosApiClient` / `MonyoPosApiClient`
- `ICustomerSyncService` / `CustomerSyncService`

### Security
- `ISensitiveDataProtector` / `SensitiveDataProtector`
- `IWalletTokenGenerator` / `WalletTokenGenerator`
- `WalletWebhookAuthFilter`, `WalletWebhookAuthAttribute`

### Controller
- `WalletWebhookController` — api/wallet
- `TenantIntegrationController` — api/tenant-integrations
- `PosProductEarnRuleController` — api/tenant-integrations/{id}/earn-rules
- `PosController` — sync-customers endpointləri əlavə edildi

### Service dəyişiklikləri
- `CustomerEnrollmentService` — ICustomerSyncService inject, StartEnrollmentAsync-də avtomatik sync

---

## 8. Konfiqurasiya Axını (Admin panel üçün)

1. **Tenant seçimi** — Super-admin bütün tenant-ları, tenant-admin yalnız öz tenant-ini seçir
2. **İnteqrasiya əlavə et** — IntegrationType siyahısından "Monyo POS" seç
3. **Credential daxil et:** ApiBaseUrl, Monyo Token (ApiToken), ExternalVenueId, LoyaltyProgramId
4. **Yaradılanda** — Wallet Token bir dəfə göstərilir; Monyo-ya bu tokenu webhook base URL ilə birlikdə vermək lazımdır
5. **Uduş artıran məhsullar** — Earn rules CRUD ilə hansı məhsulların neçə step/point verdiyini təyin et
6. **Reward üçün ExternalProductId** — Admin panel hər Reward üçün Monyo product_id daxil edə bilməlidir

---

## 9. Migration

Dəyişiklikləri tətbiq etmək üçün:
```bash
dotnet ef database update --project Loyalty.DataAccess --startup-project Loyalty.WebAPI
```

---

## 10. Plan Sənədi

Ətraflı inteqrasiya planı: `monyo_pos_və_kassa_inteqrasiyası_*.plan.md`
