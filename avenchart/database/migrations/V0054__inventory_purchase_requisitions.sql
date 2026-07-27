-- Legacy EHR's inventory lot screens do not implement a purchase-requisition
-- workflow. This local operations extension records the request and approval
-- decision separately from the later receiving/reconciliation workflow.
create table if not exists inventory_purchase_requisitions (
  requisition_id uuid primary key,
  facility_id integer not null references facilities(id),
  vendor_id uuid references inventory_vendors(vendor_id),
  status text not null check (status in ('draft', 'submitted', 'approved', 'rejected')),
  notes text,
  requested_by text not null,
  requested_at timestamptz not null,
  submitted_by text,
  submitted_at timestamptz,
  decided_by text,
  decided_at timestamptz,
  decision_notes text,
  check ((status = 'draft' and submitted_at is null and decided_at is null)
      or (status = 'submitted' and submitted_at is not null and decided_at is null)
      or (status in ('approved', 'rejected') and submitted_at is not null and decided_at is not null))
);

create table if not exists inventory_purchase_requisition_lines (
  requisition_line_id uuid primary key,
  requisition_id uuid not null references inventory_purchase_requisitions(requisition_id) on delete cascade,
  item_id integer not null references inventory_items(item_id),
  requested_quantity numeric(12,2) not null check (requested_quantity > 0),
  unique (requisition_id, item_id)
);

create table if not exists inventory_purchase_requisition_events (
  event_id uuid primary key,
  requisition_id uuid not null references inventory_purchase_requisitions(requisition_id) on delete cascade,
  action text not null check (action in ('created', 'submitted', 'approved', 'rejected')),
  note text,
  actor text not null,
  occurred_at timestamptz not null
);

create index if not exists idx_inventory_purchase_requisitions_status_requested
  on inventory_purchase_requisitions (status, requested_at desc);
create index if not exists idx_inventory_purchase_requisition_events_requisition
  on inventory_purchase_requisition_events (requisition_id, occurred_at desc);
