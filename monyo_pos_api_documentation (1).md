# Monyo POS -- Customer & Balance Integration API Documentation

Base URL:

    https://monyo.az/v4/api

Authentication: All requests must include:

    Authorization: Bearer {Monyo Token}

Each restaurant is provided with its own Monyo Token.

---

# 1. Customer API

Endpoint:

    POST /pos/customers

Supports bulk (list-based) operations.

---

## 1.1 Add Customers

Adds one or multiple customers to the system.

### Request

```json
[
  {
    "venue_id": "44",
    "type": "add",
    "data": [
      {
        "name": "Kamran Gasimov",
        "phone": "+994553019857",
        "address": "Haydar Aliyev 115",
        "birthday": "2026-01-24",
        "customer_code": "1",
        "barcode": "123456789",
        "discount": "5",
        "balans": "2",
        "note": "test"
      }
    ]
  }
]
```

### Request Parameters

Field Type Required Description

---

restaurant_id string Yes Restaurant ID
type string Yes Must be "add"
data array Yes List of customers

### Customer Object Fields

---

Field Type Required Description

---

name string Yes Customer full name

phone string Yes Phone number

address string No Customer address

birthday date No Birth date
(YYYY-MM-DD)

customer_code string No Customer ID in your
system

discount string/int No Discount percentage
(auto applied if
discount card system
exists)

note string No Additional notes

---

---

### Response

```json
[
  {
    "id": 50,
    "customer_code": "1"
  }
]
```

Field Description

---

id Monyo POS customer_id
customer_code External system customer ID

---

## 1.2 Update Customers

Updates existing customers in Monyo POS.

### Request

```json
[
  {
    "venue_id": "44",
    "type": "update",
    "data": [
      {
        "id": "1",
        "name": "Kamran Gasimov",
        "phone": "+994553019857",
        "address": "Haydar Aliyev 115",
        "birthday": "2026-01-24",
        "customer_code": "1",
        "barcode": "123456789",
        "discount": "0",
        "balans": "2",
        "note": "test"
      }
    ]
  }
]
```

### Additional Field (for update)

Field Required Description

---

id Yes Monyo POS customer_id

---

### Response

```json
[
  {
    "id": 50,
    "customer_code": "1"
  }
]
```

---

# 4. Wallet Integration API (For Wallet Platform Providers)

Monyo POS integrates with external wallet platforms for loyalty stamp campaigns and cashback balance. This section describes the endpoints that **your platform must implement** so Monyo POS can communicate with it.

All requests are sent as `POST` with `Content-Type: application/json` and authenticated via `Authorization: Bearer {wallet_token}`.

---

## 4.1 Get Wallet

Monyo POS calls this endpoint to retrieve loyalty campaigns and cashback balance for a customer.

### Your Endpoint

    POST {your_base_url}/get_wallet

### Incoming Request (from Monyo POS)

```json
{
  "venue_id": "44",
  "barcode": "123456789",
  "source": "monyo_pos"
}
```

| Field    | Type   | Description                |
| -------- | ------ | -------------------------- |
| venue_id | string | Restaurant ID in Monyo POS |
| barcode  | string | Customer barcode           |
| source   | string | Always `"monyo_pos"`       |

### Expected Response (from your platform)

```json
{
  "success": true,
  "loyalty": [
    {
      "id": 1,
      "count": 5,
      "maximum": 8,
      "rewards": 1,
      "product_id": "1111",
      "product_name": "Latte"
    }
  ],
  "cashback_balans": 5.0
}
```

| Field                  | Type    | Required | Description                                                      |
| ---------------------- | ------- | -------- | ---------------------------------------------------------------- |
| success                | boolean | Yes      | `true` if request was successful                                 |
| loyalty                | array   | Yes      | List of active loyalty campaigns                                 |
| loyalty[].id           | int     | Yes      | Campaign ID                                                      |
| loyalty[].count        | int     | Yes      | Current stamp count                                              |
| loyalty[].maximum      | int     | Yes      | Total stamps needed for reward                                   |
| loyalty[].rewards      | int     | Yes      | Number of available rewards                                      |
| loyalty[].product_id   | string  | Yes      | Reward product ID (must match Monyo POS product ID)              |
| loyalty[].product_name | string  | No       | Reward product name (Monyo POS will fill from local DB if empty) |
| cashback_balans        | float   | Yes      | Available cashback balance                                       |

### Error Response

```json
{
  "success": false,
  "message": "Customer not found"
}
```

---

## 4.2 Set Redeem (Send OTP)

Monyo POS calls this endpoint when a cashier initiates a redemption. Your platform should send an OTP to the customer.

### Your Endpoint

    POST {your_base_url}/set_redeem

### Incoming Request (from Monyo POS)

```json
{
  "venue_id": "44",
  "barcode": "123456789",
  "campaign_id": "1",
  "type": "loyalty",
  "amount": 0,
  "source": "monyo_pos"
}
```

| Field       | Type   | Description                                   |
| ----------- | ------ | --------------------------------------------- |
| venue_id    | string | Restaurant ID in Monyo POS                    |
| barcode     | string | Customer barcode                              |
| campaign_id | string | Campaign ID to redeem                         |
| type        | string | `"loyalty"` or `"cashback"`                   |
| amount      | float  | Amount to redeem (only for `"cashback"` type) |
| source      | string | Always `"monyo_pos"`                          |

### Expected Response

```json
{
  "status": "success",
  "message": "OTP sent"
}
```

| Field   | Type    | Required | Description            |
| ------- | ------- | -------- | ---------------------- |
| success | boolean | Yes      | `true` if OTP was sent |
| message | string  | Yes      | Status message         |

---

## 4.3 Verify OTP

Monyo POS calls this endpoint after the cashier enters the OTP code. Your platform should verify the OTP and complete the redemption.

### Your Endpoint

    POST {your_base_url}/set_verify

### Incoming Request (from Monyo POS)

```json
{
  "venue_id": "44",
  "barcode": "123456789",
  "campaign_id": "1",
  "otp": "1234",
  "type": "loyalty",
  "amount": 0,
  "source": "monyo_pos",
  "order_id": "1c1jf5u7a4srn"
}
```

| Field       | Type   | Description                         |
| ----------- | ------ | ----------------------------------- |
| venue_id    | string | Restaurant ID in Monyo POS          |
| barcode     | string | Customer barcode                    |
| campaign_id | string | Campaign ID                         |
| otp         | string | OTP code entered by cashier         |
| type        | string | `"loyalty"` or `"cashback"`         |
| amount      | float  | Amount (only for `"cashback"` type) |
| source      | string | Always `"monyo_pos"`                |

### Expected Response (Loyalty)

```json
{
  "status": "success",
  "type": "loyalty",
  "product_id": "1111",
  "product_name": "Latte"
}
```

| Field        | Type    | Required | Description                                                               |
| ------------ | ------- | -------- | ------------------------------------------------------------------------- |
| success      | boolean | Yes      | `true` if OTP is valid                                                    |
| type         | string  | Yes      | `"loyalty"`                                                               |
| product_id   | string  | Yes      | Reward product ID (Monyo POS will add this product to receipt at price 0) |
| product_name | string  | No       | Product name (Monyo POS will fill from local DB if empty)                 |

### Expected Response (Cashback)

```json
{
  "status": "success",
  "type": "cashback",
  "discount": 3.5
}
```

| Field    | Type    | Required | Description                                                              |
| -------- | ------- | -------- | ------------------------------------------------------------------------ |
| success  | boolean | Yes      | `true` if OTP is valid                                                   |
| type     | string  | Yes      | `"cashback"`                                                             |
| discount | float   | Yes      | Discount amount (Monyo POS will apply this as a discount to the receipt) |

---

## 4.4 Collect (Order Closed)

Monyo POS calls this endpoint automatically when a receipt is closed. Your platform should use this data to accumulate loyalty stamps and calculate cashback.

### Your Endpoint

    POST {your_base_url}/collect

### Incoming Request (from Monyo POS)

```json
{
  "id": "1c1jf5u7a4srn",
  "venue_id": "44",
  "customer_id": "5",
  "source": "monyo_pos",
  "status": "closed",
  "total": "1000",
  "items": [
    {
      "id": 123,
      "name": "Kofe",
      "count": 5
    },
    {
      "id": 124,
      "name": "Latte",
      "count": 2
    }
  ]
}
```

| Field         | Type         | Description                                    |
| ------------- | ------------ | ---------------------------------------------- |
| id            | string       | Order ID (unique, use this to track the order) |
| venue_id      | string       | Restaurant ID in Monyo POS                     |
| customer_id   | string       | Customer ID in Monyo POS CRM                   |
| source        | string       | Always `"monyo_pos"`                           |
| status        | string       | `"closed"` — order was completed               |
| total         | string/float | Order total amount                             |
| items         | array        | List of ordered items                          |
| items[].id    | int/string   | Product ID                                     |
| items[].name  | string       | Product name                                   |
| items[].count | int          | Quantity ordered                               |

### Expected Response

```json
{
  "success": true
}
```

### What your platform should do

- Match `items[].id` against loyalty campaign products and increment stamp counts
- Calculate cashback percentage of `total` and add to customer balance
- Store the `id` (order ID) so it can be reversed later if needed

---

## 4.5 Collect (Order Deleted — Reverse)

Monyo POS calls the **same collect endpoint** when a receipt is deleted, but with `status: "deleted"`. Your platform should reverse any previously applied stamps and cashback for this order.

### Your Endpoint

    POST {your_base_url}/collect

### Incoming Request (from Monyo POS)

```json
{
  "id": "1c1jf5u7a4srn",
  "venue_id": "44",
  "customer_id": "5",
  "source": "monyo_pos",
  "status": "deleted",
  "total": "1000",
  "items": [
    {
      "id": 123,
      "name": "Kofe",
      "count": 5
    }
  ]
}
```

All fields are the same as Section 4.4 except:

| Field  | Value       | Description                               |
| ------ | ----------- | ----------------------------------------- |
| status | `"deleted"` | Indicates the order was deleted/cancelled |

### Expected Response

```json
{
  "success": true
}
```

### What your platform should do

- Locate the order using `id`
- Reverse any loyalty stamps previously added
- Reverse any cashback previously credited
- Mark the order as cancelled in your system

---

## Setup Requirements

To integrate your wallet platform with Monyo POS, provide the following:

| Setting      | Description                           | Example               |
| ------------ | ------------------------------------- | --------------------- |
| Base URL     | Your API base URL                     | `your_base_url`       |
| Bearer Token | Authentication token for API requests | `your_api_token_here` |

These values will be configured in Monyo POS per restaurant.
