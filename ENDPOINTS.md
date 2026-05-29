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

### Logout

```
POST /auth/logout
```

Invalidates the caller's current JWT. Any subsequent request using the same token returns `401`. The user account remains active — a fresh login issues a new valid token.

**Response `204 No Content`**

**Errors:** `401` missing, invalid, or already-revoked token

---

## Users

### Delete own account

```
DELETE /users/me
```

Permanently deletes the authenticated user's account and all associated data (categories, expenses, income records, and daily limits). The email address is freed immediately — a new account can be registered with the same email right away. The JWT used to make this request is immediately invalidated; any subsequent request with the same token returns `401`.

**Response `204 No Content`**

**Errors:** `401` missing, invalid, or already-revoked token

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

Name conflict check is **case-insensitive** — renaming to `"food"` when `"Food"` already exists returns 409.

**Request**

```json
{ "name": "Food & Groceries" }
```

**Response `200 OK`**

```json
{ "id": "3fa85f64-...", "name": "Food & Groceries", "isArchived": false }
```

**Errors:** `400` empty name · `404` category not found / not owned by caller · `409` name already taken (case-insensitive)

---

### Merge category into another

```
POST /categories/{id}/merge-into/{targetId}
```

Reassigns all expenses from `{id}` (source) to `{targetId}` (target), then permanently deletes the source category. Both categories must be owned by the authenticated user. Use this after a rename conflict — e.g. when an import created `"їжа"` but the user wants its expenses counted under the existing `"food"`.

**Response `204 No Content`**

**Errors:** `400` `id` and `targetId` are the same · `404` source or target not found / not owned by caller

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
  "year": 2026,
  "month": 5,
  "openingBalance": 1057.50,
  "days": [
    {
      "date": "2026-05-01",
      "totalIncome": 200.00,
      "totalExpenses": 42.50,
      "effectiveLimit": 75.00,
      "limitDiff": 32.50,
      "net": 157.50,
      "expensesByCategory": [
        { "categoryId": "3fa85f64-...", "categoryName": "Groceries", "amount": 42.50 }
      ]
    }
  ],
  "monthTotals": {
    "totalIncome": 200.00,
    "totalExpenses": 42.50,
    "allowedMonthlyBudget": 2325.00,
    "totalLimitDiff": 32.50,
    "net": 157.50,
    "expensesByCategory": [
      { "categoryId": "3fa85f64-...", "categoryName": "Groceries", "amount": 42.50 }
    ]
  }
}
```

- `openingBalance` — account balance at the start of the month: `initialBudget + allPriorIncome − allPriorExpenses`
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

---

### All-time monthly breakdown

```
GET /summary/all-time/monthly
```

Returns one `MonthSummaryResponse` for every calendar month in which the authenticated user has at least one expense or income entry. The list is ordered chronologically. Returns an empty array when the user has no data.

**Response `200 OK`**

```json
[
  {
    "year": 2026,
    "month": 3,
    "openingBalance": 1057.50,
    "days": [ "..." ],
    "monthTotals": { "..." }
  },
  {
    "year": 2026,
    "month": 4,
    "openingBalance": 2114.00,
    "days": [ "..." ],
    "monthTotals": { "..." }
  }
]
```

Each element has the same shape as the response from `GET /summary/month/{yyyy-MM}`.

---

## Import

Upload an `.xlsx` file and import its rows into the database through a 3-step wizard. All three endpoints require authentication.

The file must contain **exactly one sheet**. Row 1 is treated as a header row; data starts at row 2. The caller maps column indices (0-based) to roles: date, one or more expense categories, and income. An optional `scaleFactor` (default `1`) multiplies every amount; `invertSign` (default `false`) negates every amount after scaling. Categories that do not yet exist are created automatically.

### Step 1 — Parse

```
POST /import/parse
```

Upload the file. Returns detected non-empty columns and a `fileId` used in the next two steps. The file is stored in a server-side temp directory and deleted after execute completes (or after 1 hour if the wizard is abandoned).

**Request** — `multipart/form-data`, field name `file`, max 10 MB, `.xlsx` only.

**Response `200 OK`**

```json
{
  "fileId": "a1b2c3d4...",
  "columns": [
    { "index": 0, "letter": "A", "header": "Date",      "samples": ["2025-05-01", "2025-05-02", "2025-05-03"] },
    { "index": 1, "letter": "B", "header": "Groceries", "samples": ["42.50", "18.00", "35.75"] },
    { "index": 2, "letter": "C", "header": "Transport", "samples": ["12.00", "0", "8.50"] },
    { "index": 3, "letter": "D", "header": "Income",    "samples": ["0", "0", "3000.00"] }
  ]
}
```

**Errors:** `400` no file received · `400` not an `.xlsx` file · `400` file contains more than one sheet

---

### Step 2 — Preview

```
POST /import/preview
```

Apply a column mapping to the uploaded file and return the first 10 data rows for review. No data is written to the database.

**Request**

```json
{
  "fileId": "a1b2c3d4...",
  "dateColumnIndex": 0,
  "categoryColumnIndexes": [1, 2],
  "incomeColumnIndex": 3,
  "scaleFactor": 1,
  "invertSign": false
}
```

**Response `200 OK`**

```json
{
  "totalDataRows": 31,
  "skippedRows": 0,
  "preview": [
    {
      "date": "2025-05-01",
      "expenses": [
        { "categoryName": "Groceries", "amount": 42.50 },
        { "categoryName": "Transport", "amount": 12.00 }
      ],
      "income": 0.00
    }
  ]
}
```

- `skippedRows` — rows where the date column was unparseable or an expense column contained non-numeric text
- `preview` — at most 10 rows; the full import processes all `totalDataRows` rows

**Errors:** `404` `fileId` not found (file expired or never uploaded)

---

### Step 3 — Execute

```
POST /import/execute
```

Runs the full import: upserts expenses and incomes for every valid row, creates missing categories, and deletes the temp file on success. Zero-amount values are skipped and do not overwrite existing data.

**Request** — same shape as preview.

**Response `200 OK`**

```json
{
  "daysImported": 28,
  "rowsSkipped": 1,
  "categoriesCreated": ["Groceries", "Transport"],
  "expensesUpserted": 54,
  "incomesUpserted": 3
}
```

**Errors:** `404` `fileId` not found

---

## Export

### Month export

```
GET /export/month/{yyyy-MM}
```

Downloads a calendar-month summary as a formatted `.xlsx` file. The sheet contains one row per day plus a totals row. Columns: `Date | <category 1> | … | Total Expenses | Income | Net | Balance`. The `Balance` column is a running total starting from `openingBalance` (same value as in the month summary).

**Response `200 OK`**

- `Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- `Content-Disposition: attachment; filename*=budget-{yyyy-MM}.xlsx`
- Body: binary `.xlsx` file

**Errors:** `400` invalid `yyyy-MM` format

---

### ZIP export

```
GET /export/zip
```

Downloads all months that have any data for the authenticated user as a `.zip` archive. Each entry in the archive is a per-month `.xlsx` file identical in structure to the month export. Files are named `budget-{yyyy-MM}.xlsx` and are ordered chronologically.

**Response `200 OK`** (at least one month with data)

- `Content-Type: application/zip`
- `Content-Disposition: attachment; filename=budget-all.zip`
- Body: binary `.zip` file

**Response `204 No Content`** — user has no data at all (no expenses and no incomes).

**Errors:** none beyond auth.

---

### Combined XLSX export

```
GET /export/combined
```

Downloads all months that have any data for the authenticated user as a single `.xlsx` file. One sheet named `All Time` contains every month sequentially: a month header row (bold, blue background), one row per calendar day, a monthly subtotals row (bold), and a final `All Time` totals row (bold, yellow background) after the last month. Category columns are the union of all categories seen across all months.

**Response `200 OK`**

- `Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- `Content-Disposition: attachment; filename=budget-all-time.xlsx`
- Body: binary `.xlsx` file (contains only headers if the user has no data)

**Errors:** none beyond auth.

