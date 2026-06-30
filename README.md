# BudgetAnalyzer

A privacy-first personal budgeting REST API. Track daily income and expenses across custom categories, set per-day spending limits, and query rich financial summaries — all per-user with JWT authentication.

Data never leaves your machine: the only external dependency is a local PostgreSQL instance.

## Features

- **Multi-user** — each user sees only their own data; auth via JWT (HS256)
- **Account management** — logout (invalidates current JWT without deleting the account); delete your own account and all associated data; the token is immediately invalidated on both operations; same email can be re-registered immediately after deletion
- **Categories** — create, rename, archive/unarchive spending categories
- **Expenses** — record daily spending per category; query by day or month
- **Income** — record daily income; query by month
- **Limits** — effective-dated daily spending limits (full history kept)
- **Summaries** — rich day / month / all-time calculations: totals, limit diffs, net balance, opening balance
- **Import** — upload an `.xlsx` file through a 3-step wizard (parse → preview → execute); columns are mapped by the caller; scale factor and sign inversion supported; missing categories are created automatically
- **Export** — download a calendar-month as a formatted `.xlsx` file with a running Balance column

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
- Set `AllowedHosts` for the environment (dev default in `.env.example`; use your real domain(s) in production).

**2. Start Postgres:**

```bash
docker compose up -d
```

**3. Export secrets into your shell, then run the API:**

```bash
export $(grep -v '^#' .env | grep '__' | xargs)
export $(grep -v '^#' .env | grep '^AllowedHosts=' | xargs)
dotnet run --project src/BudgetAnalyzer.Api
```

The first line exports ASP.NET Core config vars (`ConnectionStrings__*`, `Jwt__*`). The second line exports `AllowedHosts`, which is a top-level key without `__` in its name. Docker-only `POSTGRES_*` vars are skipped by both greps.

The API starts on `http://localhost:5048` by default.

## Development setup

After cloning, install the pre-commit hook once:

```bash
./scripts/install-hooks.sh
```

The hook runs automatically on every `git commit`:

```
Merge conflict markers...............................................Passed
Large files (> 500 KB)...............................................Passed
Code format (dotnet format)..........................................Passed
Build (dotnet build).................................................Passed
Unit tests (dotnet test).............................................Passed
```

- **Code format**: uses `--verify-no-changes` — run `dotnet format BudgetAnalyzer.slnx` manually to fix, then commit.
- **Build**: incremental; the first run after a fresh clone is slower.
- **Unit tests**: targets `tests/BudgetAnalyzer.UnitTests` only — integration tests run in CI.

A failed check prints the error output and blocks the commit.

The hook requires `dotnet` in your PATH (already needed to build and run the project).

> The hook script lives at `scripts/pre-commit` (tracked by git). `install-hooks.sh` copies it into `.git/hooks/`, which git executes but does not track. Any team member must re-run the install script after a fresh clone.

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
