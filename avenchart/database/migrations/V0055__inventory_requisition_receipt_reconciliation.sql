-- Legacy EHR receives inventory directly into lots and does not model a purchase
-- requisition. The modernized local operations extension preserves receipt
-- stock behavior while separately recording immutable request-to-receipt links.
create table if not exists inventory_purchase_requisition_receipts (
  reconciliation_id uuid primary key,
  requisition_id uuid not null references inventory_purchase_requisitions(requisition_id),
  requisition_line_id uuid not null references inventory_purchase_requisition_lines(requisition_line_id),
  receipt_id uuid not null unique references inventory_purchase_receipts(receipt_id),
  received_quantity numeric(12,2) not null check (received_quantity > 0),
  reconciled_by text not null,
  reconciled_at timestamptz not null
);

create index if not exists idx_inventory_purchase_requisition_receipts_requisition
  on inventory_purchase_requisition_receipts (requisition_id, reconciled_at desc);
create index if not exists idx_inventory_purchase_requisition_receipts_line
  on inventory_purchase_requisition_receipts (requisition_line_id);

alter table inventory_purchase_requisition_events
  drop constraint if exists inventory_purchase_requisition_events_action_check;
alter table inventory_purchase_requisition_events
  add constraint inventory_purchase_requisition_events_action_check
  check (action in ('created', 'submitted', 'approved', 'rejected', 'receipt_reconciled'));
