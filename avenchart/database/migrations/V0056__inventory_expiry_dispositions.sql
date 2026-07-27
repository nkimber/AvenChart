-- Legacy Legacy EHR exposes expiry and a separate lot-destruction record, but it
-- has no explicit expiry-disposition workflow. This local extension captures
-- the operational decision while retaining the legacy destruction evidence.
create table if not exists inventory_lot_expiry_dispositions (
  disposition_id uuid primary key,
  lot_id integer not null references inventory_lots(lot_id),
  disposition text not null check (disposition in ('quarantine', 'return', 'destroy')),
  quantity_affected numeric(12,2) not null check (quantity_affected >= 0),
  notes text not null,
  method text,
  witness text,
  transaction_id uuid references inventory_transactions(transaction_id),
  destruction_id uuid references inventory_lot_destructions(destruction_id),
  disposed_by text not null,
  disposed_at timestamptz not null,
  check ((disposition = 'return' and transaction_id is not null and destruction_id is null)
      or (disposition = 'destroy' and transaction_id is null and destruction_id is not null)
      or (disposition = 'quarantine' and transaction_id is null and destruction_id is null))
);

create index if not exists idx_inventory_lot_expiry_dispositions_lot
  on inventory_lot_expiry_dispositions (lot_id, disposed_at desc);
