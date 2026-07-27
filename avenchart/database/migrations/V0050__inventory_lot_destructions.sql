-- Legacy destroy_lot.php records a destruction date, method, witness, and notes,
-- then excludes the lot from active inventory without deleting its history.
create table if not exists inventory_lot_destructions (
  destruction_id uuid primary key,
  lot_id integer not null unique references inventory_lots(lot_id),
  destruction_date date not null,
  destruction_method text,
  destruction_witness text,
  destruction_notes text,
  destroyed_by text not null,
  recorded_at timestamptz not null
);

create index if not exists idx_inventory_lot_destructions_lot_recorded
  on inventory_lot_destructions (lot_id, recorded_at desc);
