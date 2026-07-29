-- REP-02a: retain revision-pinned, reproducible local report-run evidence.
-- Scope-aware execution fails closed until an authoritative staff facility or
-- patient-assignment policy is available. Only local-download artifacts are
-- stored; this migration does not enable schedules or external delivery.

alter table saved_report_runs
  add column if not exists revision_id uuid
    references saved_report_definition_revisions(revision_id) on delete restrict;

alter table saved_report_runs
  add column if not exists revision_number integer;

alter table saved_report_runs
  add column if not exists status text not null default 'completed';

alter table saved_report_runs
  add column if not exists purpose text;

alter table saved_report_runs
  add column if not exists recipient_username text;

alter table saved_report_runs
  add column if not exists row_policy text;

alter table saved_report_runs
  add column if not exists normalized_parameters jsonb not null default '{}'::jsonb;

alter table saved_report_runs
  add column if not exists as_of_date date;

alter table saved_report_runs
  add column if not exists dataset_id text;

alter table saved_report_runs
  add column if not exists dataset_version text;

alter table saved_report_runs
  add column if not exists execution_revision text not null default 'legacy-local-run-v0';

alter table saved_report_runs
  add column if not exists source_watermark jsonb not null default '{}'::jsonb;

alter table saved_report_runs
  add column if not exists definition_snapshot_checksum text;

alter table saved_report_runs
  add column if not exists request_fingerprint text;

alter table saved_report_runs
  add column if not exists idempotency_key text;

alter table saved_report_runs
  add column if not exists started_at timestamptz;

alter table saved_report_runs
  add column if not exists finished_at timestamptz;

alter table saved_report_runs
  add column if not exists duration_ms integer;

alter table saved_report_runs
  add column if not exists result_checksum text;

alter table saved_report_runs
  add column if not exists result_summary jsonb not null default '{}'::jsonb;

alter table saved_report_runs
  add column if not exists artifact_content text;

alter table saved_report_runs
  add column if not exists artifact_content_type text;

alter table saved_report_runs
  add column if not exists artifact_file_name text;

alter table saved_report_runs
  add column if not exists failure_code text;

alter table saved_report_runs
  add column if not exists failure_message text;

alter table saved_report_runs
  drop constraint if exists saved_report_runs_status_check;

alter table saved_report_runs
  add constraint saved_report_runs_status_check
  check (status in (
    'queued',
    'running',
    'completed',
    'failed',
    'cancelled',
    'expired'
  ));

create unique index if not exists ux_saved_report_runs_actor_idempotency
  on saved_report_runs (ran_by, idempotency_key)
  where idempotency_key is not null;

create index if not exists ix_saved_report_runs_definition_history
  on saved_report_runs (definition_id, ran_at desc, run_id desc);

create index if not exists ix_saved_report_runs_recipient_history
  on saved_report_runs (recipient_username, ran_at desc, run_id desc);

create table if not exists saved_report_run_events (
  event_id uuid primary key,
  run_id text not null references saved_report_runs(run_id) on delete cascade,
  action text not null,
  from_status text,
  to_status text not null,
  actor_username text not null,
  reason text not null,
  occurred_at timestamptz not null,
  details jsonb not null default '{}'::jsonb
);

create index if not exists ix_saved_report_run_events_run
  on saved_report_run_events (run_id, occurred_at, event_id);
