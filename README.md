# BudgetAnalyzer Backend (Phase 1)

This repository contains the backend for **BudgetAnalyzer**, a privacy-first budgeting application.

Phase 1 is focused on the API "Brain": a multi-user REST backend in .NET with Clean Architecture and PostgreSQL persistence for local development and tests.

## Current status

Steps 1-7 from `docs/2026-05-07-budget-analyzer-phase-1-plan.md` are implemented:

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
  - `ICurrentUser` is implemented in the API as `CurrentUser` (reads the authenticated user id from claims).

## Solution layout

- `src/BudgetAnalyzer.Domain` - domain entities and domain exceptions.
- `src/BudgetAnalyzer.Application` - use cases and abstraction contracts.
- `src/BudgetAnalyzer.Infrastructure` - persistence and technical implementations.
- `src/BudgetAnalyzer.Api` - controllers, middleware, and API host.
- `tests/BudgetAnalyzer.UnitTests` - unit tests.
- `tests/BudgetAnalyzer.IntegrationTests` - integration tests.

## Validation

Run from repo root:

`dotnet build BudgetAnalyzer.slnx`

`cp .env.example .env` (fill values, including `Jwt__SigningKey`; generate a key with e.g. `openssl rand -base64 64`)

`docker compose up -d`

## Next steps

- Step 8+: budget, categories, expenses, and remaining API workflows per the phase plan.
