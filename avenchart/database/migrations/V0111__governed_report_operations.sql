-- REP-02d: bounded operator discovery and local monitoring indexes for
-- governed report runs. The operator surface remains read-only and local; it
-- does not grant delegated lifecycle or artifact access.

create index if not exists ix_saved_report_runs_operations_status
  on saved_report_runs (status, ran_at desc, run_id desc);

create index if not exists ix_saved_report_runs_operations_requester
  on saved_report_runs (lower(ran_by), ran_at desc, run_id desc);

create index if not exists ix_saved_report_runs_operations_failure
  on saved_report_runs (failure_code, ran_at desc, run_id desc)
  where status in ('failed', 'expired');

create index if not exists ix_saved_report_definition_revisions_family
  on saved_report_definition_revisions (report_family, revision_id);
