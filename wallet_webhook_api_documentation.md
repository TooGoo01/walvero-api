# Walvero Wallet Webhook API Documentation

## Overview

Bu API, POS sistemlərinin (Monyo və s.) Walvero loyalty platforması ilə inteqrasiyası üçün nəzərdə tutulub.
Bütün endpoint-lər `Bearer wallet_token` autentifikasiya tələb edir.

**Base URL:** `https://dev.walvero.com/api/wallet`

**Auth:** `Authorization: Bearer {wallet_token}`

---

## Endpoints

### 1. Get Wallet

Müştərinin loyalty kampaniyalarını və cashback balansını qaytarır.

**POST** `/api/wallet/get_wallet`

#### Request Body

| Field     | Type   | Required | Description                        |
|-----------|--------|----------|------------------------------------|
| venue_id  | string | no       | POS venue ID                       |
| barcode   | string | yes      | Müştərinin barkodu və ya WalveroId |
| source    | string | no       | İnteqrasiya mənbəyi                |

```json
{
  "venue_id": "44",
  "barcode": "123456789",
  "source": "monyo_pos"
}
```

#### Response (Success)

```json
{
  "success": true,
  "loyalty": [
    {
      "id": 1,
      "count": 5,
      "maximum": 10,
      "rewards": 2,
      "product_id": "101",
      "product_name": "Pulsuz Kofe"
    }
  ],
  "cashback_balans": 50.0
}
```

- **loyalty** — ProgressBased (stamp card) proqramlar üçün kampaniya siyahısı
  - `count` — cari addımlar (möhürlər)
  - `maximum` — tamamlamaq üçün lazım olan addımlar
  - `rewards` — tamamlanmış dövrlər (istifadə oluna bilən mükafatlar)
  - `product_id` — reward/proqram ID
  - `product_name` — reward adı
- **cashback_balans** — müştərinin cashback balansı (AZN). `CurrentPoints × CurrencyPerPoint` ilə hesablanır.
- TierBased/PointsBased proqramlarda `loyalty` boş olur, yalnız `cashback_balans` qaytarılır.

#### Response (Error)

```json
{
  "success": false,
  "message": "Customer not found"
}
```

---

### 2. Set Redeem (OTP Göndər)

Kassir redeem başlatdıqda çağırılır. Müştəriyə OTP göndərilir.

**POST** `/api/wallet/set_redeem`

#### Request Body

| Field       | Type    | Required | Description                                           |
|-------------|---------|----------|-------------------------------------------------------|
| venue_id    | string  | no       | POS venue ID                                          |
| barcode     | string  | yes      | Müştərinin barkodu və ya WalveroId                    |
| campaign_id | string  | no       | Loyalty kampaniya ID (ProgressBased üçün)             |
| type        | string  | yes      | `"loyalty"` və ya `"cashback"`                        |
| amount      | decimal | yes*     | Çıxılacaq məbləğ (point). Cashback üçün mütləqdir.   |
| source      | string  | no       | İnteqrasiya mənbəyi                                  |

**Loyalty (stamp card) nümunə:**
```json
{
  "venue_id": "44",
  "barcode": "123456789",
  "campaign_id": "1",
  "type": "loyalty",
  "amount": 1,
  "source": "monyo_pos"
}
```

**Cashback nümunə:**
```json
{
  "venue_id": "44",
  "barcode": "123456789",
  "type": "cashback",
  "amount": 10,
  "source": "monyo_pos"
}
```

#### Daxili davranış

| Proqram tipi    | amount dəyəri | Nəticə                                               |
|-----------------|---------------|-------------------------------------------------------|
| ProgressBased   | 1             | `delta=0`, `spendCount=1` (1 reward xərclənir)        |
| ProgressBased   | 3             | `delta=0`, `spendCount=3` (3 reward xərclənir)        |
| Cashback        | 10            | `delta=-10` (10 point çıxılır)                        |
| Cashback        | 50            | `delta=-50` (50 point çıxılır)                        |

#### Response

```json
{
  "status": "success",
  "message": "OTP sent"
}
```

```json
{
  "status": "error",
  "message": "Customer not found"
}
```

---

### 3. Set Verify (OTP Təsdiqlə)

Kassir OTP daxil etdikdən sonra çağırılır. Redeem tamamlanır.

**POST** `/api/wallet/set_verify`

#### Request Body

| Field       | Type    | Required | Description                                  |
|-------------|---------|----------|----------------------------------------------|
| venue_id    | string  | no       | POS venue ID                                 |
| barcode     | string  | yes      | Müştərinin barkodu və ya WalveroId           |
| campaign_id | string  | no       | Loyalty kampaniya ID                         |
| otp         | string  | yes      | Müştərinin daxil etdiyi OTP kod              |
| type        | string  | yes      | `"loyalty"` və ya `"cashback"`               |
| amount      | decimal | no       | Cashback üçün endirim məbləği                |
| order_id    | string  | yes      | POS sifariş ID (delete/reverse üçün vacibdir)|
| source      | string  | no       | İnteqrasiya mənbəyi                         |

```json
{
  "venue_id": "44",
  "barcode": "123456789",
  "campaign_id": "1",
  "otp": "1234",
  "type": "loyalty",
  "amount": 0,
  "order_id": "1c1jf5u7a4srn",
  "source": "monyo_pos"
}
```

#### Response (Loyalty)

```json
{
  "status": "success",
  "type": "loyalty",
  "product_id": "101",
  "product_name": "Pulsuz Kofe"
}
```

#### Response (Cashback)

```json
{
  "status": "success",
  "type": "cashback",
  "discount": 10.0
}
```

#### Response (Error)

```json
{
  "status": "error"
}
```

> **Vacib:** `order_id` göndərilməlidir. Sifariş silinəndə (collect deleted) bu `order_id` ilə redeem transaction-ı tapılıb geri qaytarılır.

---

### 4. Collect (Sifariş Bağlandı)

POS-da çek bağlandıqda avtomatik çağırılır. Loyalty stamp/point toplanır.

**POST** `/api/wallet/collect`

#### Request Body

| Field       | Type    | Required | Description                              |
|-------------|---------|----------|------------------------------------------|
| id          | string  | yes      | POS sifariş ID                           |
| venue_id    | string  | no       | POS venue ID                             |
| customer_id | string  | yes      | POS-dakı müştəri ID (external mapping)   |
| status      | string  | yes      | `"closed"` və ya `"deleted"`             |
| total       | decimal | no       | Sifarişin ümumi məbləği                  |
| items       | array   | no       | Sifariş məhsulları                       |
| source      | string  | no       | İnteqrasiya mənbəyi                     |

**items array:**

| Field | Type   | Description      |
|-------|--------|------------------|
| id    | int    | Məhsul ID        |
| name  | string | Məhsul adı       |
| count | int    | Miqdar            |

#### Collect (status: closed) — Point toplama

```json
{
  "id": "1c1jf5u7a4srn",
  "venue_id": "44",
  "customer_id": "5",
  "status": "closed",
  "total": 1000,
  "items": [
    { "id": 123, "name": "Kofe", "count": 5 },
    { "id": 124, "name": "Latte", "count": 2 }
  ],
  "source": "monyo_pos"
}
```

#### Collect (status: deleted) — Reverse (geri qaytarma)

Sifariş silinəndə çağırılır. Həm earn (toplanan), həm redeem (xərclənən) transaction-lar geri qaytarılır.

```json
{
  "id": "1c1jf5u7a4srn",
  "venue_id": "44",
  "customer_id": "5",
  "status": "deleted",
  "total": 1000,
  "items": [
    { "id": 123, "name": "Kofe", "count": 5 }
  ],
  "source": "monyo_pos"
}
```

#### Response

```json
{
  "success": true
}
```

```json
{
  "success": false
}
```

#### Point hesablama qaydası

1. **EarnRule əsaslı:** Əgər məhsul üçün `PosProductEarnRule` konfiqurasiya olunubsa, hər məhsula görə `StepsPerUnit × count` və `PointsPerUnit × count` hesablanır.
2. **Ümumi məbləğ əsaslı:** EarnRule yoxdursa, `total × PointsPerCurrency` ilə point hesablanır.

#### Delete davranışı

`status: "deleted"` olduqda:
- Bu `order_id` ilə bağlı **bütün transaction-lar** (həm earn, həm redeem) tapılır
- Hər transaction üçün reverse (əks) transaction yaradılır
- Earn point-lər çıxılır, redeem point-lər geri qaytarılır

---

## Autentifikasiya

Bütün endpoint-lər `Bearer` token tələb edir:

```
Authorization: Bearer {wallet_token}
```

Token `TenantIntegration` cədvəlindəki `WalletTokenEncrypted` field-indən yoxlanılır.
Hər token bir tenant + loyalty proqram + inteqrasiyaya bağlıdır.

---

## Tipik POS Axını

```
1. Müştəri gəlir → POS get_wallet çağırır → balans/kampaniya göstərilir
2. Müştəri reward istifadə etmək istəyir → POS set_redeem çağırır → OTP göndərilir
3. Müştəri OTP deyir → POS set_verify çağırır (order_id ilə!) → redeem tamamlanır
4. Çek bağlanır → POS collect çağırır (status: closed) → point toplanır
5. (Opsional) Çek silinir → POS collect çağırır (status: deleted) → hər şey geri qaytarılır
```
