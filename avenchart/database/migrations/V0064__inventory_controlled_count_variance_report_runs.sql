alter table inventory_controlled_report_runs
  drop constraint if exists inventory_controlled_report_runs_report_key_check;

alter table inventory_controlled_report_runs
  add constraint inventory_controlled_report_runs_report_key_check
  check (report_key in ('as_of_inventory', 'custody_activity', 'count_variance'));
