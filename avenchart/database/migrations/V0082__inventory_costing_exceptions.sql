-- VAL-02: do not hide quantity movements that cannot yet be valued.
create table inventory_costing_exceptions (
  exception_id uuid primary key,
  source_transaction_id uuid not null unique references inventory_transactions(transaction_id),
  lot_id integer not null references inventory_lots(lot_id),
  status text not null check (status in ('pending_policy','unsupported_method','no_open_layer','insufficient_layer')),
  reason text not null,
  created_at timestamptz not null,
  created_by text not null
);
create index ix_inventory_costing_exceptions_lot_time on inventory_costing_exceptions(lot_id, created_at desc);
