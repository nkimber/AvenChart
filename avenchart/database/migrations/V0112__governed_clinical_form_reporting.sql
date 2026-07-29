-- REP-02e / FORM-04b: pin the form-reporting semantic revision used by
-- governed runs. Existing non-form runs remain explicitly not applicable.
-- Form/schema/renderer/field/content evidence is retained in each bounded CSV
-- row and protected by the existing result checksum.

alter table saved_report_runs
  add column if not exists form_reporting_revision text
    not null default 'not-applicable';

alter table saved_report_runs
  drop constraint if exists saved_report_runs_form_reporting_revision_check;

alter table saved_report_runs
  add constraint saved_report_runs_form_reporting_revision_check
  check (
    form_reporting_revision in (
      'not-applicable',
      'local-clinical-form-reporting-v1'
    )
  );

create index if not exists ix_saved_report_runs_form_reporting
  on saved_report_runs (
    form_reporting_revision,
    ran_at desc,
    run_id desc
  )
  where form_reporting_revision <> 'not-applicable';
