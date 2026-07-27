-- Controlled stock is segregated by secure location. The custody ledger is
-- append-only and is separate from general inventory shortcuts.
alter table inventory_lots
  add column if not exists controlled_location_id uuid references inventory_controlled_locations(location_id) on delete restrict;

alter table inventory_lots
  drop constraint if exists inventory_lots_item_id_facility_id_lot_number_key;

create unique index if not exists ux_inventory_lots_item_facility_lot_controlled_location
  on inventory_lots (item_id, facility_id, lot_number, coalesce(controlled_location_id, '00000000-0000-0000-0000-000000000000'::uuid));

create table if not exists inventory_controlled_custody_events (
  event_id uuid primary key,
  action text not null check (action in ('receipt', 'transfer', 'dispense', 'administration', 'return', 'waste', 'correction')),
  lot_id integer not null references inventory_lots(lot_id) on delete restrict,
  counterparty_lot_id integer references inventory_lots(lot_id) on delete restrict,
  source_location_id uuid references inventory_controlled_locations(location_id) on delete restrict,
  destination_location_id uuid references inventory_controlled_locations(location_id) on delete restrict,
  patient_id text references patients(canonical_id) on delete restrict,
  encounter integer references encounters(encounter) on delete restrict,
  quantity numeric(12,2) not null check (quantity > 0),
  quantity_delta numeric(12,2) not null check (quantity_delta <> 0),
  reason text not null,
  related_event_id uuid references inventory_controlled_custody_events(event_id) on delete restrict,
  idempotency_key text not null unique,
  source_quantity_before numeric(12,2),
  source_quantity_after numeric(12,2),
  destination_quantity_before numeric(12,2),
  destination_quantity_after numeric(12,2),
  performed_by text not null,
  occurred_at timestamptz not null,
  entered_at timestamptz not null,
  check ((action = 'receipt' and source_location_id is null and destination_location_id is not null and quantity_delta > 0)
      or (action = 'transfer' and source_location_id is not null and destination_location_id is not null and source_location_id <> destination_location_id and counterparty_lot_id is not null and quantity_delta < 0)
      or (action in ('dispense', 'administration', 'waste') and source_location_id is not null and destination_location_id is null and quantity_delta < 0)
      or (action = 'return' and source_location_id is null and destination_location_id is not null and quantity_delta > 0 and related_event_id is not null)
      or (action = 'correction' and related_event_id is not null and ((source_location_id is not null and destination_location_id is null and quantity_delta < 0) or (source_location_id is null and destination_location_id is not null and quantity_delta > 0))))
);

create index if not exists ix_inventory_controlled_custody_events_lot_time
  on inventory_controlled_custody_events(lot_id, occurred_at desc, event_id desc);

create index if not exists ix_inventory_controlled_custody_events_related
  on inventory_controlled_custody_events(related_event_id)
  where related_event_id is not null;
