-- Legacy lot edits change lot metadata without creating stock movement. Retain
-- the before/after values and authenticated actor as immutable local evidence.
create table if not exists inventory_lot_metadata_audits (
  audit_id uuid primary key,
  lot_id integer not null references inventory_lots(lot_id),
  prior_lot_number text not null,
  new_lot_number text not null,
  prior_expiration_date date,
  new_expiration_date date,
  changed_by text not null,
  changed_at timestamptz not null
);

create index if not exists idx_inventory_lot_metadata_audits_lot_changed
  on inventory_lot_metadata_audits (lot_id, changed_at desc);
