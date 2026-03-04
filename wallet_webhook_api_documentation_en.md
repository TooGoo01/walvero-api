# Walvero Wallet Webhook API Documentation

## Overview

This API enables POS systems to integrate with the Walvero loyalty platform. It provides endpoints for querying customer loyalty data, processing redeems (loyalty rewards and cashback), and collecting points when orders are closed.

**Base URL:** `https://dev.walvero.com`

---

## Authentication

All endpoints require a **Bearer token** in the `Authorization` header:

```
Authorization: Bearer {wallet_token}
```

A unique `wallet_token` will be provided to you by the Walvero team for each venue/integration. Include this token in **every** API request exactly as shown above.

If the token is missing or invalid, the API returns **401 Unauthorized**.

### Example request

```http
POST /api/wallet/get_wallet HTTP/1.1
Host: dev.walvero.com
Authorization: Bearer YOUR_WALLET_TOKEN_HERE
Content-Type: application/json
Accept: application/json

{
  "venue_id": "44",
  "barcode": "123456789"
}
```

---

## Customer Identification

The `barcode` field is used to identify the customer. It accepts the following formats:

| Format              | Example             | Description                                               |
|---------------------|---------------------|-----------------------------------------------------------|
| Card barcode        | `"ABC123"`          | The barcode printed on the customer's loyalty card        |
| Walvero ID          | `"98765"`           | The customer's numeric Walvero user ID                    |
| Combined            | `"98765,ABC123"`    | Comma-separated; the system tries each value in order     |

---

## Endpoints

### 1. Get Wallet

Returns the customer's loyalty campaigns and/or cashback balance.

**POST** `/api/wallet/get_wallet`

#### Request Body

| Field     | Type   | Required | Description                    |
|-----------|--------|----------|--------------------------------|
| venue_id  | string | no       | POS venue ID                   |
| barcode   | string | **yes**  | Customer barcode or Walvero ID |
| source    | string | no       | Integration source identifier  |

```json
{
  "venue_id": "44",
  "barcode": "123456789",
  "source": "monyo_pos"
}
```

#### Response — Loyalty (stamp card) program

When the customer is enrolled in a stamp-card (loyalty) program, the `loyalty` array contains the campaign details:

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
      "product_name": "Free Coffee"
    }
  ],
  "cashback_balans": 0
}
```

| Field              | Description                                      |
|--------------------|--------------------------------------------------|
| `loyalty[].id`     | Campaign ID                                      |
| `loyalty[].count`  | Current stamps collected (progress)              |
| `loyalty[].maximum`| Total stamps required to earn one reward         |
| `loyalty[].rewards`| Number of available rewards ready to redeem      |
| `loyalty[].product_id`  | Product ID (use this in `set_redeem`)       |
| `loyalty[].product_name`| Reward name (e.g., "Free Coffee")           |
| `cashback_balans`  | Always `0` for loyalty programs                  |

#### Response — Cashback program

When the customer is enrolled in a cashback (points-based) program:

```json
{
  "success": true,
  "loyalty": [],
  "cashback_balans": 50.0
}
```

| Field             | Description                                        |
|-------------------|----------------------------------------------------|
| `loyalty`         | Always empty for cashback programs                 |
| `cashback_balans` | Customer's available cashback balance (in AZN)     |

#### Response — Error

```json
{
  "success": false,
  "message": "Customer not found"
}
```

---

### 2. Set Redeem (Initiate Redeem)

Initiates a redeem operation. Depending on the venue configuration, an OTP code may be sent to the customer via SMS/push notification.

**POST** `/api/wallet/set_redeem`

#### Request Body

| Field       | Type    | Required | Description                                              |
|-------------|---------|----------|----------------------------------------------------------|
| venue_id    | string  | no       | POS venue ID                                             |
| barcode     | string  | **yes**  | Customer barcode or Walvero ID                           |
| campaign_id | string  | no       | Campaign ID from `get_wallet` response (for loyalty)     |
| type        | string  | **yes**  | `"loyalty"` or `"cashback"`                              |
| amount      | decimal | **yes**  | Number of rewards to use (loyalty) or points to deduct (cashback) |
| source      | string  | no       | Integration source identifier                            |

#### Loyalty example — redeem 1 reward:

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

#### Cashback example — deduct 10 points:

```json
{
  "venue_id": "44",
  "barcode": "123456789",
  "type": "cashback",
  "amount": 10,
  "source": "monyo_pos"
}
```

#### What `amount` means

| type         | amount | Effect                                |
|--------------|--------|---------------------------------------|
| `"loyalty"`  | 1      | 1 reward is used (from `rewards`)     |
| `"loyalty"`  | 3      | 3 rewards are used                    |
| `"cashback"` | 10     | 10 points deducted from balance       |
| `"cashback"` | 50     | 50 points deducted from balance       |

#### OTP behavior

Some venues are configured with OTP verification enabled, others are not:

| OTP enabled | What happens                                                        |
|-------------|---------------------------------------------------------------------|
| Yes         | Customer receives an OTP via SMS/push. You must call `set_verify` with the OTP. |
| No          | No OTP is sent. You can skip `set_redeem` and call `set_verify` directly. |

> The Walvero team will inform you whether OTP is enabled for your integration.

#### Response — Success

```json
{
  "status": "success",
  "message": "OTP sent"
}
```

#### Response — Error

```json
{
  "status": "error",
  "message": "Customer not found"
}
```

---

### 3. Set Verify (Confirm Redeem)

Confirms and finalizes the redeem operation. If OTP is enabled, the code is validated. If OTP is not enabled, any value is accepted.

**POST** `/api/wallet/set_verify`

#### Request Body

| Field       | Type    | Required | Description                                                 |
|-------------|---------|----------|-------------------------------------------------------------|
| venue_id    | string  | no       | POS venue ID                                                |
| barcode     | string  | **yes**  | Customer barcode or Walvero ID                              |
| campaign_id | string  | no       | Campaign ID (for loyalty)                                   |
| otp         | string  | **yes**  | OTP code from customer. If OTP not enabled, send any value (e.g., `"0000"`) |
| type        | string  | **yes**  | `"loyalty"` or `"cashback"`                                 |
| amount      | decimal | no       | Discount amount (for cashback; used for display)            |
| order_id    | string  | **yes**  | Your POS order/receipt ID (**must be provided**)            |
| source      | string  | no       | Integration source identifier                               |

> **Important:** `order_id` must always be provided. This is used to link the redeem to a specific order. If the order is later deleted (via `collect` with `status=deleted`), the redeem will be automatically reversed using this ID.

#### Loyalty verify example:

```json
{
  "venue_id": "44",
  "barcode": "123456789",
  "campaign_id": "1",
  "otp": "123456",
  "type": "loyalty",
  "amount": 0,
  "order_id": "1c1jf5u7a4srn",
  "source": "monyo_pos"
}
```

#### Cashback verify example:

```json
{
  "venue_id": "44",
  "barcode": "123456789",
  "otp": "123456",
  "type": "cashback",
  "amount": 10,
  "order_id": "1c1jf5u7a4srn",
  "source": "monyo_pos"
}
```

#### Response — Loyalty success

The response includes the product details of the redeemed reward:

```json
{
  "status": "success",
  "type": "loyalty",
  "product_id": "101",
  "product_name": "Free Coffee"
}
```

#### Response — Cashback success

The response includes the discount amount applied:

```json
{
  "status": "success",
  "type": "cashback",
  "discount": 10.0
}
```

#### Response — Error

```json
{
  "status": "error"
}
```

#### When OTP is not enabled

If your integration does not require OTP:

1. You can skip `set_redeem` entirely and call `set_verify` directly.
2. The `otp` field is still required in the request body — send any value (e.g., `"0000"`).
3. The redeem will be created and confirmed in one step.

---

### 4. Collect (Order Closed / Deleted)

Called when a POS receipt is closed or an order is deleted/cancelled.

**POST** `/api/wallet/collect`

#### Request Body

| Field       | Type    | Required | Description                                         |
|-------------|---------|----------|-----------------------------------------------------|
| id          | string  | **yes**  | Your POS order/receipt ID                           |
| venue_id    | string  | no       | POS venue ID                                        |
| customer_id | string  | **yes**  | Customer ID in your POS system                      |
| status      | string  | **yes**  | `"closed"` or `"deleted"`                           |
| total       | decimal | yes*     | Total order amount (*required when status = closed) |
| items       | array   | yes*     | Order items (*required when status = closed)        |
| payments    | object  | no       | Payment breakdown by method. See below.             |
| source      | string  | no       | Integration source identifier                       |

#### Items array

| Field | Type   | Description            |
|-------|--------|------------------------|
| id    | int    | Your product ID        |
| name  | string | Product name           |
| count | int    | Quantity ordered       |

#### Payments object (optional)

| Field    | Type   | Description                    |
|----------|--------|--------------------------------|
| cash     | string | Cash payment amount            |
| cashless | string | Card/cashless payment amount   |

> If the venue is configured with separate cashback rates for cash vs. card payments, include the `payments` object. Otherwise, it can be omitted and points will be calculated from `total`.

---

#### 4a. Collect — status: "closed" (earn points)

Called when the receipt is closed. The customer earns points/stamps based on the order.

**Without payment split:**

```json
{
  "id": "1c1jf5u7a4srn",
  "venue_id": "44",
  "customer_id": "5",
  "status": "closed",
  "total": 100,
  "items": [
    { "id": 123, "name": "Coffee", "count": 5 },
    { "id": 124, "name": "Latte", "count": 2 }
  ],
  "source": "monyo_pos"
}
```

**With payment split (cash/card):**

```json
{
  "id": "1c1jf5u7a4srn",
  "venue_id": "44",
  "customer_id": "5",
  "status": "closed",
  "total": 100,
  "items": [
    { "id": 123, "name": "Coffee", "count": 5 },
    { "id": 124, "name": "Latte", "count": 2 }
  ],
  "payments": {
    "cash": "30.00",
    "cashless": "70.00"
  },
  "source": "monyo_pos"
}
```

#### How points are earned

The exact point/stamp calculation depends on the venue's configuration in Walvero:

- **Product-based rules:** If specific products have earn rules configured, stamps/points are calculated per product and quantity.
- **Percentage of total:** For cashback programs, a percentage of the order total is credited as points. The percentage may vary by the customer's loyalty tier and payment method (cash vs. card).
- **Fixed rate:** Points are calculated as a fixed rate per currency unit spent.

> The Walvero team will configure these rules based on your business agreement.

---

#### 4b. Collect — status: "deleted" (reverse / cancel order)

Called when an order is deleted or cancelled. All loyalty transactions (both earned and redeemed) associated with this order are automatically reversed.

```json
{
  "id": "1c1jf5u7a4srn",
  "venue_id": "44",
  "customer_id": "5",
  "status": "deleted",
  "total": 100,
  "items": [
    { "id": 123, "name": "Coffee", "count": 5 }
  ],
  "source": "monyo_pos"
}
```

When a `deleted` collect is received:
- Points/stamps that were earned from this order are **removed**.
- If a reward or cashback was redeemed on this order (via `set_verify`), it is **restored** to the customer's balance.
- Calling `deleted` multiple times for the same order is safe (idempotent).

#### Response

```json
{
  "success": true
}
```

---

## Typical POS Flows

### Flow 1: Full flow with OTP

```
1. Customer scans loyalty card
   -> POST /api/wallet/get_wallet  { barcode: "..." }
   <- Response with loyalty campaigns and/or cashback balance

2. Customer wants to redeem a reward or cashback
   -> POST /api/wallet/set_redeem  { barcode, type, amount, campaign_id }
   <- Customer receives OTP via SMS/push

3. Customer tells OTP to cashier
   -> POST /api/wallet/set_verify  { barcode, otp, type, amount, order_id }
   <- Redeem confirmed. Response includes product details or discount amount.

4. Receipt is closed
   -> POST /api/wallet/collect  { id, customer_id, status: "closed", total, items }
   <- Points/stamps earned

5. (If order is cancelled)
   -> POST /api/wallet/collect  { id, customer_id, status: "deleted" }
   <- All transactions for this order are reversed
```

### Flow 2: Without OTP

```
1. Customer scans loyalty card
   -> POST /api/wallet/get_wallet  { barcode: "..." }
   <- Response with loyalty campaigns and/or cashback balance

2. Customer wants to redeem
   -> POST /api/wallet/set_verify  { barcode, otp: "0000", type, amount, order_id }
   <- Redeem confirmed in one step (no OTP needed)

3. Receipt is closed
   -> POST /api/wallet/collect  { id, customer_id, status: "closed", total, items }
   <- Points/stamps earned
```

### Flow 3: Earn only (no redeem)

```
1. Receipt is closed
   -> POST /api/wallet/collect  { id, customer_id, status: "closed", total, items }
   <- Points/stamps earned based on order items and total
```

---

## Error Handling

| HTTP Status | Meaning                                              |
|-------------|------------------------------------------------------|
| 200         | Request processed. Check the `success` or `status` field in the response body. |
| 401         | Invalid or missing wallet token                      |
| 400         | Malformed request body                               |

Business-level errors are returned with HTTP 200:

```json
{
  "success": false,
  "message": "Customer not found"
}
```

Common error messages:

| Message                          | Description                                    |
|----------------------------------|------------------------------------------------|
| `"Customer not found"`           | The barcode does not match any customer         |
| `"No loyalty program configured"`| The integration has no linked loyalty program   |
| `"No active card for program"`   | The customer has no active card in this program |
| `"Insufficient balance"`         | Not enough points or rewards to redeem          |

---

## JSON Field Reference

All request and response fields use **snake_case** naming convention:

| Field           | Description                                |
|-----------------|--------------------------------------------|
| `venue_id`      | POS venue identifier                       |
| `barcode`       | Customer card barcode or Walvero ID        |
| `campaign_id`   | Loyalty campaign/reward ID                 |
| `order_id`      | POS order/receipt identifier               |
| `customer_id`   | Customer ID in the POS system              |
| `product_id`    | Product identifier                         |
| `product_name`  | Product/reward display name                |
| `cashback_balans`| Customer's cashback balance (AZN)         |

---

## Quick Reference

| Action                | Endpoint                        | When to call                    |
|-----------------------|---------------------------------|---------------------------------|
| Check balance         | `POST /api/wallet/get_wallet`   | Customer scans card             |
| Start redeem          | `POST /api/wallet/set_redeem`   | Customer wants to use reward    |
| Confirm redeem        | `POST /api/wallet/set_verify`   | Cashier enters OTP              |
| Earn points           | `POST /api/wallet/collect`      | Receipt is closed (`"closed"`)  |
| Cancel/reverse order  | `POST /api/wallet/collect`      | Order is deleted (`"deleted"`)  |
