# BudgetAnalyzer Backend (Phase 1)

This repository contains the backend for **BudgetAnalyzer**, a privacy-first budgeting application.

Phase 1 is focused on the API "Brain": a multi-user REST backend in .NET with Clean Architecture and PostgreSQL persistence for local development and tests.

## Current status

Steps 1-12 from `docs/2026-05-07-budget-analyzer-phase-1-plan.md` are implemented:

- solution file is created (`BudgetAnalyzer.slnx`),
- core projects are bootstrapped under `src/`,
- test projects are bootstrapped under `tests/`,
- project references follow the planned dependency direction,
- solution build passes,
- local PostgreSQL is configured via `docker-compose.yml`,
- development connection string template is set in `src/BudgetAnalyzer.Api/appsettings.Development.json`,
- domain entities are implemented in `src/BudgetAnalyzer.Domain/Entities`,
- domain exception base/specializations are implemented in `src/BudgetAnalyzer.Domain/Exceptions`,
- application abstraction contracts are implemented in `src/BudgetAnalyzer.Application/Abstractions`:
  - `IRepository<TEntity>` with basic CRUD/query shape,
  - `IUnitOfWork` with `SaveChangesAsync`,
  - `IPasswordHasher` for hashing and verification,
  - `IJwtTokenService` for issuing auth tokens,
  - `IClock` for deterministic time access in services/tests,
- EF Core persistence is wired in `src/BudgetAnalyzer.Infrastructure/Persistence`:
  - `AppDbContext` contains DbSets for all core entities and applies entity configurations,
  - per-entity configurations define table mappings, money precision, unique indexes, and the expense-category FK,
  - DI registration adds `AppDbContext` with Npgsql in `src/BudgetAnalyzer.Api/Program.cs`,
  - initial migration files are generated under `src/BudgetAnalyzer.Infrastructure/Persistence/Migrations`,
- EF-backed adapters for application persistence contracts live in `src/BudgetAnalyzer.Infrastructure/Persistence/Repositories`:
  - `Repository<TEntity>` implements `IRepository<TEntity>` against `AppDbContext`,
  - `UnitOfWork` implements `IUnitOfWork` and calls `SaveChangesAsync` on the same scoped `AppDbContext`,
  - `Program.cs` registers `IRepository<>` and `IUnitOfWork` as scoped services.
- Authentication (JWT) is wired in the API and infrastructure:
  - `POST /api/auth/register` and `POST /api/auth/login` return `{ token, expiresAt }` (`AuthController`, `AuthService`),
  - passwords are hashed with `BcryptPasswordHasher` (BCrypt work factor 11),
  - tokens are issued with `JwtTokenService` (HS256; `sub` = user id),
  - `Jwt` issuer, audience, and expiry live in `appsettings.Development.json`; set **`Jwt__SigningKey`** in `.env` (see `.env.example`) — do not commit real signing keys,
  - `GET /api/ping` is `[Authorize]`-protected for quick JWT checks (401 without `Authorization: Bearer …`, 200 with a valid token),
  - `ICurrentUser` is implemented in the API as `CurrentUser` (reads the authenticated user id from claims),
- Initial budget endpoint is available under `/api/me`:
  - `GET /api/me/budget` returns `{ initialBudget }` for the authenticated user,
  - `PUT /api/me/budget` with `{ initialBudget }` updates the value (must be ≥ 0; missing field returns 400),
  - implemented in `BudgetService` (Application) and `BudgetController` (Api).
- Categories CRUD is available under `/api/categories`:
  - `GET /api/categories?includeArchived=false` lists the authenticated user's categories (active only by default),
  - `POST /api/categories` with `{ name }` creates a new category (409 if an active category with the same name exists),
  - `PUT /api/categories/{id}` renames a category (404 if not owned by caller; 409 on name conflict),
  - `POST /api/categories/{id}/archive` soft-deletes a category (expenses keep their FK; category hidden from default list),
  - `POST /api/categories/{id}/unarchive` restores an archived category (409 if another active category now has the same name),
  - implemented in `CategoryService` (Application) and `CategoriesController` (Api).
- Expenses are available under `/api/expenses`:
  - `PUT /api/expenses/{date}/{categoryId}` with `{ amount }` upserts a daily expense (inserts on first call; updates on subsequent calls for the same date+category; amount must be ≥ 0; 404 if category doesn't belong to user; 400 if category is archived),
  - `DELETE /api/expenses/{date}/{categoryId}` removes the expense row (404 if it doesn't exist),
  - `GET /api/expenses/by-date/{yyyy-MM-dd}` returns all active categories for the user with their amounts for that date (0 for categories with no entry),
  - `GET /api/expenses/by-month/{yyyy-MM}` returns a row per calendar day in the month with per-category amounts and a daily total (0 for days/categories with no entries),
  - implemented in `ExpenseService` (Application) and `ExpensesController` (Api).
- Daily income is available under `/api/incomes`:
  - `PUT /api/incomes/{date}` with `{ amount }` upserts the income for that day (one entry per user per date; amount must be ≥ 0),
  - `DELETE /api/incomes/{date}` removes the income row for that day (404 if it doesn't exist),
  - `GET /api/incomes/by-month/{yyyy-MM}` returns a row per calendar day in the month with the income amount (0 for days with no entry),
  - implemented in `IncomeService` (Application) and `IncomesController` (Api).
- Daily limits (effective-dated) are available under `/api/limits`:
  - `GET /api/limits` returns the full limit history for the authenticated user, ordered by `effectiveFromDate` ascending,
  - `PUT /api/limits/{effectiveFromDate}` with `{ amount }` upserts a limit entry — inserts on first call for that date, updates the amount on subsequent calls; amount must be ≥ 0,
  - `DELETE /api/limits/{effectiveFromDate}` removes that history entry (404 if it doesn't exist),
  - `GetEffectiveAsync(userId, date)` returns the most recent limit amount with `effectiveFromDate ≤ date`, or `null` if no limit has been set yet for any date on or before that day — used by the summary service (Step 13),
  - implemented in `LimitService` (Application) and `LimitsController` (Api).

## Solution layout

- `src/BudgetAnalyzer.Domain` - domain entities and domain exceptions.
- `src/BudgetAnalyzer.Application` - use cases and abstraction contracts.
- `src/BudgetAnalyzer.Infrastructure` - persistence and technical implementations.
- `src/BudgetAnalyzer.Api` - controllers, middleware, and API host.
- `tests/BudgetAnalyzer.UnitTests` - unit tests.
- `tests/BudgetAnalyzer.IntegrationTests` - integration tests.

## Local dev setup

**1. Create your secrets file** (gitignored — never committed):

```bash
cp .env.example .env
```

Edit `.env`:
- Set `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB` (used by docker-compose to configure the container).
- Set `ConnectionStrings__Default` to the full Postgres connection string for the app and `dotnet ef` tools.
- Generate and set `Jwt__SigningKey`: `openssl rand -base64 64`

**2. Start Postgres:**

```bash
docker compose up -d
```

**3. Export the ASP.NET Core vars into your shell, then run the API:**

```bash
export $(grep -v '^#' .env | grep '__' | xargs)
dotnet run --project src/BudgetAnalyzer.Api
```

The `grep '__'` filter selects only the `__`-separator vars meant for ASP.NET Core and skips the docker-compose-only `POSTGRES_*` vars. Both sets live in `.env` to keep all local secrets in one place.

**Why env vars instead of JSON placeholders:** ASP.NET Core's configuration system natively reads environment variables using `__` as the section separator (e.g. `Jwt__SigningKey` → `Jwt:SigningKey`). No code changes or extra packages are needed — secrets never touch JSON files.

**Build check:**

```bash
dotnet build BudgetAnalyzer.slnx
```

## Next steps

- Step 13+: summary calculations, validation middleware, tests, and final wiring per the phase plan.
