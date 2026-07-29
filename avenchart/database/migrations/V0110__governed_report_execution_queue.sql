-- REP-02c: durable local queue and artifact-lifecycle evidence for governed
-- report execution. This remains a local development worker/storage contract;
-- it does not approve production infrastructure, retention, or legal hold.

alter table saved_report_runs
  add column if not exists queue_revision text
    not null default 'legacy-report-queue-unavailable-v0';

alter table saved_report_runs
  add column if not exists lifecycle_version integer
    not null default 0;

alter table saved_report_runs
  add column if not exists attempt_count integer
    not null default 0;

alter table saved_report_runs
  add column if not exists max_attempts integer
    not null default 1;

alter table saved_report_runs
  add column if not exists manual_retry_count integer
    not null default 0;

alter table saved_report_runs
  add column if not exists next_attempt_at timestamptz;

alter table saved_report_runs
  add column if not exists last_attempt_at timestamptz;

alter table saved_report_runs
  add column if not exists lease_owner text;

alter table saved_report_runs
  add column if not exists lease_expires_at timestamptz;

alter table saved_report_runs
  add column if not exists last_heartbeat_at timestamptz;

alter table saved_report_runs
  add column if not exists queue_expires_at timestamptz;

alter table saved_report_runs
  add column if not exists cancel_requested_at timestamptz;

alter table saved_report_runs
  add column if not exists cancel_requested_by text;

alter table saved_report_runs
  add column if not exists cancel_reason text;

alter table saved_report_runs
  add column if not exists failure_retryable boolean;

alter table saved_report_runs
  add column if not exists artifact_expires_at timestamptz;

alter table saved_report_runs
  add column if not exists artifact_expired_at timestamptz;

alter table saved_report_runs
  add constraint saved_report_runs_lifecycle_version_check
  check (lifecycle_version >= 0);

alter table saved_report_runs
  add constraint saved_report_runs_attempt_count_check
  check (attempt_count >= 0 and max_attempts between 1 and 10);

alter table saved_report_runs
  add constraint saved_report_runs_manual_retry_count_check
  check (manual_retry_count >= 0);

create index if not exists ix_saved_report_runs_queue_claim
  on saved_report_runs (next_attempt_at, ran_at, run_id)
  where status = 'queued';

create index if not exists ix_saved_report_runs_running_lease
  on saved_report_runs (lease_expires_at, run_id)
  where status = 'running';

create index if not exists ix_saved_report_runs_artifact_expiry
  on saved_report_runs (artifact_expires_at, run_id)
  where status = 'completed' and artifact_content is not null;
