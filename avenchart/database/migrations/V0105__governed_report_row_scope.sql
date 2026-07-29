-- REP-02b: pin the locally resolved report row scope used by each governed run.
-- The local mapping is an executable development contract, not production
-- authorization-policy approval.

alter table saved_report_runs
  add column if not exists scope_revision text
    not null default 'legacy-scope-unavailable-v0';

alter table saved_report_runs
  add column if not exists scope_snapshot jsonb
    not null default '{}'::jsonb;

alter table saved_report_runs
  add column if not exists scope_snapshot_checksum text;

alter table saved_report_runs
  add column if not exists scope_facility_id integer
    references facilities(id) on delete restrict;

alter table saved_report_runs
  add column if not exists scope_subject_count integer;

alter table saved_report_runs
  add constraint saved_report_runs_scope_subject_count_check
  check (scope_subject_count is null or scope_subject_count >= 0);

create index if not exists ix_saved_report_runs_scope_evidence
  on saved_report_runs (
    scope_revision,
    row_policy,
    scope_facility_id,
    ran_at desc
  );
