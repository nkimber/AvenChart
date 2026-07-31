-- VAL-04: replenishment settings are controlled, effective-dated policy.
-- Recommendations remain read-only; no purchase order is created by this model.
create table if not exists inventory_replenishment_policies (
  policy_id uuid primary key,
  item_id integer not null references inventory_items(item_id),
  facility_id integer not null references facilities(id),
  reorder_point numeric(12,2) not null check (reorder_point >= 0),
  target_quantity numeric(12,2) not null check (target_quantity >= 0),
  lead_time_days integer not null check (lead_time_days >= 0),
  safety_stock numeric(12,2) not null check (safety_stock >= 0),
  preferred_vendor_id uuid references inventory_vendors(vendor_id),
  pack_size numeric(12,2) not null check (pack_size > 0),
  approval_threshold numeric(12,2) not null check (approval_threshold >= 0),
  effective_date date not null,
  approval_reference text not null,
  rationale text not null,
  revision integer not null check (revision > 0),
  status text not null check (status in ('active', 'superseded')),
  activated_at timestamptz not null,
  activated_by text not null,
  superseded_at timestamptz,
  superseded_by text,
  unique (item_id, facility_id, revision)
);
create unique index if not exists ux_inventory_replenishment_policies_active_scope
  on inventory_replenishment_policies(item_id, facility_id) where status = 'active';

create table if not exists inventory_replenishment_policy_change_requests (
  request_id uuid primary key,
  item_id integer not null references inventory_items(item_id),
  facility_id integer not null references facilities(id),
  proposed_definition jsonb not null,
  baseline_policy_id uuid references inventory_replenishment_policies(policy_id),
  baseline_revision integer,
  reason text not null,
  status text not null check (status in ('draft', 'submitted', 'approved', 'rejected', 'activated', 'cancelled')),
  version integer not null default 0 check (version >= 0),
  created_at timestamptz not null,
  created_by text not null,
  updated_at timestamptz not null,
  updated_by text not null
);
create unique index if not exists ux_inventory_replenishment_policy_change_requests_open_scope
  on inventory_replenishment_policy_change_requests(item_id, facility_id)
  where status in ('draft', 'submitted', 'approved');

create table if not exists inventory_replenishment_policy_change_request_events (
  event_id bigint generated always as identity primary key,
  request_id uuid not null references inventory_replenishment_policy_change_requests(request_id) on delete cascade,
  action text not null check (action in ('created', 'submitted', 'approved', 'rejected', 'activated', 'cancelled')),
  note text,
  occurred_at timestamptz not null,
  username text not null
);
create index if not exists ix_inventory_replenishment_policy_change_request_events_time
  on inventory_replenishment_policy_change_request_events(request_id, occurred_at desc, event_id desc);
