create table if not exists inventory_controlled_report_exports (
  export_id uuid primary key,
  run_id uuid not null references inventory_controlled_report_runs(run_id) on delete restrict,
  exported_by text not null,
  exported_at timestamptz not null,
  format text not null check (format in ('csv')),
  result_checksum text not null
);

create index if not exists ix_inventory_controlled_report_exports_run
  on inventory_controlled_report_exports(run_id, exported_at desc);
