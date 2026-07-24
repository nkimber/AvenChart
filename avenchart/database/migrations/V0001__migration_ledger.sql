-- Establishes the durable ledger used by the local migration runner.
-- This migration is intentionally idempotent so a new PostgreSQL database can
-- bootstrap the ledger before the runner records this file's checksum.
create table if not exists schema_migrations (
  migration_id text primary key,
  checksum_sha256 text not null,
  description text not null,
  applied_at timestamptz not null,
  applied_by text not null
);
