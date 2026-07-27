create table if not exists inventory_controlled_report_runs (
  run_id uuid primary key,
  report_key text not null check (report_key in ('as_of_inventory')),
  as_of_date date not null,
  location_id uuid references inventory_controlled_locations(location_id) on delete restrict,
  requested_by text not null,
  requested_at timestamptz not null,
  row_count integer not null check (row_count >= 0),
  result_checksum text not null
);

create index if not exists ix_inventory_controlled_report_runs_requested
  on inventory_controlled_report_runs(requested_at desc, report_key);
