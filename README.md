# BudgetAnalyzer Backend (Phase 1)

This repository contains the backend for **BudgetAnalyzer**, a privacy-first budgeting application.

Phase 1 is focused on the API "Brain": a multi-user REST backend in .NET with Clean Architecture and PostgreSQL persistence planned in the next steps.

## Current status

Step 1 from `docs/2026-05-07-budget-analyzer-phase-1-plan.md` is implemented:

- solution file is created (`BudgetAnalyzer.slnx`),
- core projects are bootstrapped under `src/`,
- test projects are bootstrapped under `tests/`,
- project references follow the planned dependency direction,
- solution build passes.

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

## Next steps

- Step 2: configure PostgreSQL via `docker-compose.yml`.
- Add development connection string in `src/BudgetAnalyzer.Api/appsettings.Development.json`.
- Continue with domain model and EF Core wiring.
