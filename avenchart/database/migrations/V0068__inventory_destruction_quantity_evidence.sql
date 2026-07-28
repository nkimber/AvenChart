-- Destruction and expired-lot return/destruction now update remaining quantity
-- and immutable ledger evidence atomically. Preserve the action-specific audit
-- reference while requiring the matching ledger debit for destruction.
alter table inventory_lot_expiry_dispositions
  drop constraint if exists inventory_lot_expiry_dispositions_check;

with pending as materialized (
  select
    d.disposition_id,
    gen_random_uuid() as transaction_id,
    d.lot_id,
    -d.quantity_affected as quantity_delta,
    d.notes,
    d.disposed_by,
    d.disposed_at
  from inventory_lot_expiry_dispositions d
  where d.disposition = 'destroy'
    and d.transaction_id is null
),
inserted as (
  insert into inventory_transactions (
    transaction_id,
    lot_id,
    transaction_type,
    quantity_delta,
    reason,
    performed_by,
    occurred_at
  )
  select
    p.transaction_id,
    p.lot_id,
    'destruction',
    p.quantity_delta,
    p.notes,
    p.disposed_by,
    p.disposed_at
  from pending p
  returning transaction_id
)
update inventory_lot_expiry_dispositions d
set transaction_id = p.transaction_id
from pending p
where d.disposition_id = p.disposition_id
  and p.transaction_id in (select i.transaction_id from inserted i);

insert into inventory_transactions (
  transaction_id,
  lot_id,
  transaction_type,
  quantity_delta,
  reason,
  performed_by,
  occurred_at
)
select
  gen_random_uuid(),
  d.lot_id,
  'destruction',
  -l.quantity_on_hand,
  coalesce(d.destruction_notes, 'Historical full-lot destruction'),
  d.destroyed_by,
  d.recorded_at
from inventory_lot_destructions d
join inventory_lots l on l.lot_id = d.lot_id
where l.quantity_on_hand > 0
  and not exists (
    select 1
    from inventory_transactions t
    where t.lot_id = d.lot_id
      and t.transaction_type = 'destruction'
  );

update inventory_lots l
set quantity_on_hand = 0
where l.status = 'inactive'
  and l.quantity_on_hand <> 0
  and (
    exists (
      select 1
      from inventory_lot_destructions d
      where d.lot_id = l.lot_id
    )
    or exists (
      select 1
      from inventory_lot_expiry_dispositions d
      where d.lot_id = l.lot_id
        and d.disposition in ('return', 'destroy')
    )
  );

alter table inventory_lot_expiry_dispositions
  add constraint inventory_lot_expiry_dispositions_check
  check (
    (disposition = 'return' and transaction_id is not null and destruction_id is null)
    or (disposition = 'destroy' and transaction_id is not null and destruction_id is not null)
    or (disposition = 'quarantine' and transaction_id is null and destruction_id is null)
  );
