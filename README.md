# BudgetAnalyzer Backend (Phase 1)

This repository contains the backend for **BudgetAnalyzer**, a privacy-first budgeting application.

Phase 1 is focused on the API "Brain": a multi-user REST backend in .NET with Clean Architecture and PostgreSQL persistence for local development and tests.

## Current status

Steps 1-2 from `docs/2026-05-07-budget-analyzer-phase-1-plan.md` are implemented:

- solution file is created (`BudgetAnalyzer.slnx`),
- core projects are bootstrapped under `src/`,
- test projects are bootstrapped under `tests/`,
- project references follow the planned dependency direction,
- solution build passes,
- local PostgreSQL is configured via `docker-compose.yml`,
- development connection string template is set in `src/BudgetAnalyzer.Api/appsettings.Development.json`.

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

`cp .env.example .env` (fill values)

`docker compose up -d`

## Next steps

- Step 3: implement domain entities and domain exceptions.
- Step 4: define application abstractions (`IRepository<T>`, `IUnitOfWork`, auth/time contracts).
- Continue with EF Core wiring and first migration.
