# BudgetAnalyzer Backend (Phase 1)

This repository contains the backend foundation for **BudgetAnalyzer**, a privacy-first budgeting application.

Phase 1 focuses on the "Brain" of the system: a multi-user REST API built with Clean Architecture in .NET, backed by PostgreSQL, and designed to track:

- daily expenses by category (one entry per user/category/date),
- daily income (one entry per user/date),
- effective-dated daily spending limits,
- initial budget per user,
- day/month/all-time summaries.

The current commit provides the initial backend project structure so implementation can proceed in clear, isolated layers.

## Why this structure

The layout follows the implementation plan in `docs/2026-05-07-budget-analyzer-phase-1-plan.md`:

- `src/BudgetAnalyzer.Domain` for entities and domain-level exceptions,
- `src/BudgetAnalyzer.Application` for use cases/services and abstraction contracts,
- `src/BudgetAnalyzer.Infrastructure` for EF Core persistence and technical implementations,
- `src/BudgetAnalyzer.Api` for HTTP controllers and middleware,
- `tests/*` for unit and integration test suites.

This structure keeps business logic independent from infrastructure concerns and supports incremental delivery of API features.

## Scope in this stage

Included now:

- backend folder architecture and placeholder files,
- top-level repository scaffolding.

Deferred to follow-up implementation:

- actual .NET solution/project files and code,
- database model + migrations,
- authentication and REST endpoints,
- automated tests and CI wiring.

## Notes

- `docker-compose.yml` and `.gitignore` are intentionally empty placeholders in this scaffold step.
- Empty directories include `.gitkeep` files so they are visible in Git and GitHub.
