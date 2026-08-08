#!/bin/sh
set -eu

POSTGRES_HOST="${POSTGRES_HOST:-127.0.0.1}"
POSTGRES_PORT="${POSTGRES_PORT:-5432}"
POSTGRES_DB="${POSTGRES_DB:-avenchart}"
POSTGRES_USER="${POSTGRES_USER:-avenchart}"
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-avenchart_demo}"
DEMO_SEED_WAIT_SECONDS="${DEMO_SEED_WAIT_SECONDS:-120}"
DEMO_SEED_ON_STARTUP="${DEMO_SEED_ON_STARTUP:-true}"
DEMO_RESET_ON_STARTUP="${DEMO_RESET_ON_STARTUP:-true}"
DEMO_SEED_ON_STARTUP="$(printf '%s' "$DEMO_SEED_ON_STARTUP" | tr '[:upper:]' '[:lower:]')"
DEMO_RESET_ON_STARTUP="$(printf '%s' "$DEMO_RESET_ON_STARTUP" | tr '[:upper:]' '[:lower:]')"

export PGPASSWORD="$POSTGRES_PASSWORD"
export ConnectionStrings__AvenChart="Host=${POSTGRES_HOST};Port=${POSTGRES_PORT};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"

if [ "$DEMO_SEED_ON_STARTUP" = "true" ] && [ -f /app/demo-seed.sql ]; then
  deadline=$(( $(date +%s) + DEMO_SEED_WAIT_SECONDS ))
  until pg_isready -h "$POSTGRES_HOST" -p "$POSTGRES_PORT" -U "$POSTGRES_USER" -d "$POSTGRES_DB" >/dev/null 2>&1; do
    if [ "$(date +%s)" -ge "$deadline" ]; then
      echo "PostgreSQL was not ready within ${DEMO_SEED_WAIT_SECONDS} seconds." >&2
      exit 1
    fi
    sleep 2
  done

  should_seed="$DEMO_RESET_ON_STARTUP"
  if [ "$DEMO_RESET_ON_STARTUP" != "true" ]; then
    existing_dataset="$(psql -h "$POSTGRES_HOST" -p "$POSTGRES_PORT" -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tAc "select to_regclass('public.dataset_metadata') is not null" 2>/dev/null || true)"
    if [ "$existing_dataset" != "t" ]; then
      should_seed="true"
    fi
  fi

  if [ "$should_seed" = "true" ]; then
    echo "Seeding AvenChart demo database."
    psql -h "$POSTGRES_HOST" -p "$POSTGRES_PORT" -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -f /app/demo-seed.sql
  else
    echo "Demo seed skipped because dataset_metadata already exists and reset is disabled."
  fi
fi

exec dotnet AvenChart.Api.dll
