-- Local inter-facility transfer support. A transfer is represented by two immutable
-- inventory ledger entries sharing transfer_id: an outbound debit and inbound credit.
create sequence if not exists inventory_lot_id_seq;

select setval(
  'inventory_lot_id_seq',
  coalesce((select max(lot_id) from inventory_lots), 0) + 1,
  false
);

alter table inventory_lots
  alter column lot_id set default nextval('inventory_lot_id_seq');

alter table inventory_transactions
  add column if not exists transfer_id uuid;

create index if not exists idx_inventory_transactions_transfer
  on inventory_transactions (transfer_id, occurred_at desc)
  where transfer_id is not null;
