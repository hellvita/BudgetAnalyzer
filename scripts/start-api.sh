#!/bin/sh
# Starts the BudgetAnalyzer API for local testing.
# Checks Docker is running first, then exports .env config and runs the API.
# Usage: ./scripts/start-api.sh (from anywhere in the repo)

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT" || exit 1

if ! docker info >/dev/null 2>&1; then
  echo "Docker is not running. Start Docker Desktop first, then re-run this script." >&2
  exit 1
fi

if [ ! -f .env ]; then
  echo ".env not found in $ROOT. Copy .env.example to .env and fill in the values first." >&2
  exit 1
fi

if [ -z "$(docker ps --filter name=budget-analyzer-db --filter status=running -q)" ]; then
  echo "Postgres container is not running. Starting it..."
  docker compose up -d
else
  echo "Postgres container is already running."
fi

export $(grep -v '^#' .env | grep '__' | xargs)
export $(grep -v '^#' .env | grep '^AllowedHosts=' | xargs)
dotnet run --project src/BudgetAnalyzer.Api
