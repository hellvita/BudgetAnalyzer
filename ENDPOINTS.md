# API Endpoints

Base URL: `http://localhost:5048/api`

All endpoints except `POST /auth/register`, `POST /auth/login`, and `GET /ping` (unauthenticated ping) require a valid JWT in the `Authorization: Bearer <token>` header.

---

## Authentication

### Register

```
POST /auth/register
```

Creates a new user account and returns a JWT.

**Request**

```json
{ "email": "alice@example.com", "password": "S3c3tr!" }
```

**Response `201 Created`**

```json
{ "token": "<jwt>", "expiresAt": "2026-05-15T12:00:00Z" }
```

**Errors:** `400` invalid email/password format · `409` email already registered

---

### Login

```
POST /auth/login
```

Authenticates an existing user and returns a fresh JWT.

**Request**

```json
{ "email": "alice@example.com", "password": "S3cr3t!" }
```

**Response `200 OK`**

```json
{ "token": "<jwt>", "expiresAt": "2026-05-15T12:00:00Z" }
```

**Errors:** `400` missing fields · `404` wrong email or password

---

## Users

### Delete own account

```
DELETE /users/me
```

Permanently deletes the authenticated user's account and all associated data (categories, expenses, income records, and daily limits). The email address is freed immediately — a new account can be registered with the same email right away.

**Response `204 No Content`**

**Errors:** `401` missing or invalid token

---

## Health check

### Ping (authenticated)

```
GET /ping
```

Returns `200 OK` when the JWT is valid. Quick way to verify a token is still accepted.

**Response `200 OK`** — empty body

**Errors:** `401` missing or invalid token

---

## Budget

### Get initial budget

```
GET /me/budget
```

Returns the user's initial (starting) budget amount.

**Response `200 OK`**

```json
{ "initialBudget": 1500.00 }
```

---

### Set initial budget

```
PUT /me/budget
```

Sets the user's initial budget (must be ≥ 0). Creates or updates.

**Request**

```json
{ "initialBudget": 1500.00 }
```

**Response `200 OK`**

```json
{ "initialBudget": 1500.00 }
```

**Errors:** `400` amount < 0 or missing field

---

## Categories

### List categories

```
GET /categories?includeArchived=false
```

Returns the authenticated user's categories. Pass `includeArchived=true` to include archived ones.

**Response `200 OK`**

```json
[
  { "id": "3fa85f64-...", "name": "Groceries", "isArchived": false },
  { "id": "7cb12a88-...", "name": "Rent",      "isArchived": false }
]
```

---

### Create category

```
POST /categories
```

**Request**

```json
{ "name": "Groceries" }
```

**Response `201 Created`**

```json
{ "id": "3fa85f64-...", "name": "Groceries", "isArchived": false }
```

**Errors:** `400` empty name · `409` active category with that name already exists

---

### Rename category

```
PUT /categories/{id}
```

**Request**

```json
{ "name": "Food & Groceries" }
```

**Response `200 OK`**

```json
{ "id": "3fa85f64-...", "name": "Food & Groceries", "isArchived": false }
```

**Errors:** `400` empty name · `404` category not found / not owned by caller · `409` name conflict

---

### Archive category

```
POST /categories/{id}/archive
```

Soft-deletes a category. Existing expenses keep their FK; the category is hidden from the default list.

**Response `204 No Content`**

**Errors:** `404` category not found / not owned by caller

---

### Unarchive category

```
POST /categories/{id}/unarchive
```

Restores an archived category to active status.

**Response `204 No Content`**

**Errors:** `404` category not found / not owned by caller · `409` another active category now has the same name

---

## Expenses

### Upsert daily expense

```
PUT /expenses/{date}/{categoryId}
```

`date` format: `yyyy-MM-dd`. Inserts on first call for a given date + category; updates on subsequent calls.

**Request**

```json
{ "amount": 42.50 }
```

**Response `200 OK`**

```json
{ "date": "2026-05-14", "categoryId": "3fa85f64-...", "amount": 42.50 }
```

**Errors:** `400` amount < 0 or category is archived · `404` category not found / not owned by caller

---

### Delete daily expense

```
DELETE /expenses/{date}/{categoryId}
```

**Response `204 No Content`**

**Errors:** `404` no expense found for that date + category

---

### Expenses by date

```
GET /expenses/by-date/{date}
```

Returns all active categories for the user with their expense amounts for the given date. Categories with no entry return `0`.

**Response `200 OK`**

```json
[
  { "categoryId": "3fa85f64-...", "categoryName": "Groceries", "amount": 42.50 },
  { "categoryId": "7cb12a88-...", "categoryName": "Rent",      "amount": 0.00 }
]
```

---

### Expenses by month

```
GET /expenses/by-month/{yyyy-MM}
```

Returns a row per calendar day in the month. Each row contains per-category amounts and a daily total. Days and categories with no entries return `0`.

**Response `200 OK`**

```json
[
  {
    "date": "2026-05-01",
    "total": 42.50,
    "byCategory": [
      { "categoryId": "3fa85f64-...", "categoryName": "Groceries", "amount": 42.50 }
    ]
  },
  {
    "date": "2026-05-02",
    "total": 0.00,
    "byCategory": [
      { "categoryId": "3fa85f64-...", "categoryName": "Groceries", "amount": 0.00 }
    ]
  }
]
```

---

## Income

### Upsert daily income

```
PUT /incomes/{date}
```

One income entry per user per date. Inserts on first call; updates on subsequent calls. `amount` must be ≥ 0.

**Request**

```json
{ "amount": 200.00 }
```

**Response `200 OK`**

```json
{ "date": "2026-05-14", "amount": 200.00 }
```

**Errors:** `400` amount < 0 or missing field

---

### Delete daily income

```
DELETE /incomes/{date}
```

**Response `204 No Content`**

**Errors:** `404` no income entry for that date

---

### Income by month

```
GET /incomes/by-month/{yyyy-MM}
```

Returns a row per calendar day in the month. Days with no entry return `0`.

**Response `200 OK`**

```json
[
  { "date": "2026-05-01", "amount": 200.00 },
  { "date": "2026-05-02", "amount": 0.00 }
]
```

---

## Daily limits

Limits are **effective-dated**: each entry says "from this date onward, the daily spending limit is X". The most recent entry on or before a given date is the effective limit for that day. Full history is kept.

### List limit history

```
GET /limits
```

**Response `200 OK`**

```json
[
  { "effectiveFromDate": "2026-01-01", "amount": 50.00 },
  { "effectiveFromDate": "2026-04-01", "amount": 75.00 }
]
```

---

### Upsert limit

```
PUT /limits/{effectiveFromDate}
```

`effectiveFromDate` format: `yyyy-MM-dd`. Inserts on first call for that date; updates on subsequent calls.

**Request**

```json
{ "amount": 75.00 }
```

**Response `200 OK`**

```json
{ "effectiveFromDate": "2026-04-01", "amount": 75.00 }
```

**Errors:** `400` amount < 0 or missing field

---

### Delete limit entry

```
DELETE /limits/{effectiveFromDate}
```

**Response `204 No Content`**

**Errors:** `404` no limit entry for that date

---

## Summary

All summary endpoints require authentication. Amounts are monetary decimals.

### Day summary

```
GET /summary/day/{date}
```

`date` format: `yyyy-MM-dd`.

**Response `200 OK`**

```json
{
  "date": "2026-05-14",
  "income": 200.00,
  "expenses": [
    { "categoryId": "3fa85f64-...", "categoryName": "Groceries", "amount": 42.50 }
  ],
  "totalExpenses": 42.50,
  "effectiveLimit": 75.00,
  "limitDiff": 32.50,
  "net": 157.50
}
```

- `effectiveLimit` — the active daily limit for that date, or `null` if none set
- `limitDiff` — `effectiveLimit − totalExpenses` (positive = under budget, negative = over); `null` if no limit
- `net` — `income − totalExpenses`

---

### Month summary

```
GET /summary/month/{yyyy-MM}
```

**Response `200 OK`**

```json
{
  "days": [
    {
      "date": "2026-05-01",
      "income": 200.00,
      "totalExpenses": 42.50,
      "effectiveLimit": 75.00,
      "limitDiff": 32.50,
      "net": 157.50
    }
  ],
  "totalIncome": 200.00,
  "totalExpenses": 42.50,
  "allowedMonthlyBudget": 2325.00,
  "totalLimitDiff": 32.50,
  "net": 157.50
}
```

- `allowedMonthlyBudget` — sum of effective limits across all days in the month
- `totalLimitDiff` — sum of per-day limit diffs (only days with an effective limit)

---

### All-time summary

```
GET /summary/all-time
```

**Response `200 OK`**

```json
{
  "initialBudget": 1500.00,
  "totalIncome": 3200.00,
  "totalExpenses": 980.50,
  "totalLimitDiff": 120.00,
  "balance": 3719.50,
  "net": 2219.50
}
```

- `balance` — `initialBudget + totalIncome − totalExpenses`
- `totalLimitDiff` — summed only over days that have at least one expense or income entry
- `net` — `totalIncome − totalExpenses`

