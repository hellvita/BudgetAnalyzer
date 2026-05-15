# BudgetAnalyzer

A privacy-first personal budgeting REST API. Track daily income and expenses across custom categories, set per-day spending limits, and query rich financial summaries — all per-user with JWT authentication.

Data never leaves your machine: the only external dependency is a local PostgreSQL instance.

## Features

- **Multi-user** — each user sees only their own data; auth via JWT (HS256)
- **Account management** — delete your own account and all associated data; the token is immediately invalidated on deletion; same email can be re-registered immediately
- **Categories** — create, rename, archive/unarchive spending categories
- **Expenses** — record daily spending per category; query by day or month
- **Income** — record daily income; query by month
- **Limits** — effective-dated daily spending limits (full history kept)
- **Summaries** — rich day / month / all-time calculations: totals, limit diffs, net balance

## Solution layout

```
src/
  BudgetAnalyzer.Domain          # Entities and domain exceptions
  BudgetAnalyzer.Application     # Use-case services and abstraction contracts
  BudgetAnalyzer.Infrastructure  # EF Core persistence (PostgreSQL via Npgsql)
  BudgetAnalyzer.Api             # ASP.NET Core host, controllers, middleware
tests/
  BudgetAnalyzer.UnitTests       # Unit tests (Moq, in-memory async queryable)
  BudgetAnalyzer.IntegrationTests # Integration tests (Testcontainers + real Postgres)
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://docs.docker.com/get-docker/) (for the local Postgres container)

## Local dev setup

**1. Create your secrets file** (gitignored — never committed):

```bash
cp .env.example .env
```

Edit `.env`:
- Set `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB` (used by Docker Compose).
- Set `ConnectionStrings__Default` to the full Postgres connection string for the app and EF tools.
- Generate and set `Jwt__SigningKey`: `openssl rand -base64 64`

**2. Start Postgres:**

```bash
docker compose up -d
```

**3. Export secrets into your shell, then run the API:**

```bash
export $(grep -v '^#' .env | grep '__' | xargs)
dotnet run --project src/BudgetAnalyzer.Api
```

The `grep '__'` filter selects only ASP.NET Core configuration vars (double-underscore separator) and skips the Docker-only `POSTGRES_*` vars.

The API starts on `http://localhost:5048` by default.

## Running tests

Tests use [Testcontainers](https://dotnet.testcontainers.org/) — Docker must be running. No extra configuration needed.

```bash
# All tests
dotnet test BudgetAnalyzer.slnx

# Unit tests only
dotnet test tests/BudgetAnalyzer.UnitTests

# Integration tests only (spins up a real Postgres container)
dotnet test tests/BudgetAnalyzer.IntegrationTests
```

## API reference

See **[ENDPOINTS.md](ENDPOINTS.md)** for the full list of endpoints with descriptions and usage examples.
