-- Initial inventory/supply vertical slice: catalog items, facility lots, and
-- immutable quantity activity. Purchase, adjustment, consumption, destruction,
-- and transfer behavior is recorded as inventory transactions.
create table if not exists inventory_items (
  item_id integer primary key,
  item_code text not null unique,
  name text not null,
  category text not null,
  unit text not null,
  reorder_point numeric(12,2) not null default 0,
  preferred_quantity numeric(12,2) not null default 0,
  active boolean not null default true
);

create table if not exists inventory_lots (
  lot_id integer primary key,
  item_id integer not null references inventory_items(item_id),
  facility_id integer not null references facilities(id),
  lot_number text not null,
  expiration_date date,
  quantity_on_hand numeric(12,2) not null default 0,
  unit_cost numeric(12,2) not null default 0,
  status text not null default 'active',
  unique (item_id, facility_id, lot_number)
);

create table if not exists inventory_transactions (
  transaction_id uuid primary key,
  lot_id integer not null references inventory_lots(lot_id),
  transaction_type text not null,
  quantity_delta numeric(12,2) not null,
  reason text,
  performed_by text not null,
  occurred_at timestamptz not null
);

create index if not exists idx_inventory_lots_item_facility
  on inventory_lots (item_id, facility_id, status);

create index if not exists idx_inventory_transactions_lot_occurred
  on inventory_transactions (lot_id, occurred_at desc);
