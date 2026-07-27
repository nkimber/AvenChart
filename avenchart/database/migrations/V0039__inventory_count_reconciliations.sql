-- Physical counts retain the expected quantity, actual count, reason, actor, and
-- adjustment ledger entry as one auditable reconciliation event.
create table if not exists inventory_count_reconciliations (
  reconciliation_id uuid primary key,
  lot_id integer not null references inventory_lots(lot_id),
  expected_quantity numeric(12,2) not null,
  counted_quantity numeric(12,2) not null check (counted_quantity >= 0),
  notes text not null,
  counted_by text not null,
  counted_at timestamptz not null
);

alter table inventory_transactions
  add column if not exists reconciliation_id uuid references inventory_count_reconciliations(reconciliation_id);

create index if not exists idx_inventory_count_reconciliations_lot_counted
  on inventory_count_reconciliations (lot_id, counted_at desc);

create index if not exists idx_inventory_transactions_reconciliation
  on inventory_transactions (reconciliation_id)
  where reconciliation_id is not null;
