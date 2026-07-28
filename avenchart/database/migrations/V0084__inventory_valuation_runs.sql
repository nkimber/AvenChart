-- VAL-03: a valuation result is a durable, reproducible snapshot, not a
-- recalculation from the mutable lot carrying value.
create table inventory_valuation_runs (
  run_id uuid primary key,
  requested_at timestamptz not null,
  requested_by text not null,
  as_of_at timestamptz not null,
  facility_id integer references facilities(id),
  policy_id uuid not null references inventory_cost_policies(policy_id),
  policy_revision integer not null check (policy_revision > 0),
  method text not null,
  currency text not null check (currency ~ '^[A-Z]{3}$'),
  rounding_rule text not null check (rounding_rule in ('half_up', 'half_even', 'truncate')),
  status text not null check (status in ('completed', 'completed_with_exceptions')),
  layer_count integer not null check (layer_count >= 0),
  application_count integer not null check (application_count >= 0),
  exception_count integer not null check (exception_count >= 0),
  unvalued_layer_count integer not null check (unvalued_layer_count >= 0),
  quantity_total numeric(14,2) not null,
  value_total numeric(16,4) not null,
  calculation_version text not null,
  result_checksum text not null check (result_checksum ~ '^[0-9a-f]{64}$'),
  completed_at timestamptz not null
);
create index ix_inventory_valuation_runs_as_of on inventory_valuation_runs(as_of_at desc, run_id desc);

create table inventory_valuation_run_lines (
  run_id uuid not null references inventory_valuation_runs(run_id),
  layer_id uuid not null references inventory_cost_layers(layer_id),
  lot_id integer not null references inventory_lots(lot_id),
  item_id integer not null references inventory_items(item_id),
  facility_id integer not null references facilities(id),
  received_quantity numeric(12,2) not null,
  remaining_quantity numeric(12,2) not null,
  unit_cost numeric(12,4) not null,
  value_total numeric(16,4) not null,
  application_count integer not null check (application_count >= 0),
  primary key(run_id, layer_id)
);
