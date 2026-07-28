alter table inventory_controlled_report_runs
  add column if not exists result_artifact jsonb;
