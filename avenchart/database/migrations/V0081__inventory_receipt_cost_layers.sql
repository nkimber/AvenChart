-- VAL-02: preserve receipt cost basis independently from the mutable lot
-- carrying value. No accounting method is inferred when VAL-01 is unselected.
create table inventory_cost_layers (
  layer_id uuid primary key,
  source_transaction_id uuid not null unique references inventory_transactions(transaction_id),
  receipt_id uuid not null references inventory_purchase_receipts(receipt_id),
  lot_id integer not null references inventory_lots(lot_id),
  item_id integer not null references inventory_items(item_id),
  facility_id integer not null references facilities(id),
  received_quantity numeric(12,2) not null check (received_quantity > 0),
  remaining_quantity numeric(12,2) not null check (remaining_quantity >= 0 and remaining_quantity <= received_quantity),
  unit_cost numeric(12,4) not null check (unit_cost >= 0),
  currency text not null check (currency ~ '^[A-Z]{3}$'),
  policy_id uuid references inventory_cost_policies(policy_id),
  policy_revision integer,
  method text,
  status text not null check (status in ('open','pending_policy','exhausted','corrected')),
  created_at timestamptz not null,
  created_by text not null,
  check ((status = 'pending_policy' and policy_id is null and policy_revision is null and method is null) or (status <> 'pending_policy' and policy_id is not null and policy_revision is not null and method is not null))
);
create index ix_inventory_cost_layers_lot_status on inventory_cost_layers(lot_id, status, created_at);

create table inventory_cost_layer_applications (
  application_id uuid primary key,
  layer_id uuid not null references inventory_cost_layers(layer_id),
  source_transaction_id uuid not null references inventory_transactions(transaction_id),
  application_type text not null check (application_type in ('issue','return','adjustment','correction')),
  quantity numeric(12,2) not null check (quantity <> 0),
  unit_cost numeric(12,4) not null check (unit_cost >= 0),
  extended_cost numeric(14,4) not null,
  rounding_trace text not null,
  reversal_application_id uuid references inventory_cost_layer_applications(application_id),
  applied_at timestamptz not null,
  applied_by text not null,
  unique(layer_id, source_transaction_id)
);
create index ix_inventory_cost_layer_applications_layer_time on inventory_cost_layer_applications(layer_id, applied_at, application_id);
