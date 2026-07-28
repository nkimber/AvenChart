-- VAL-01: an explicit accounting policy is required before the target can
-- calculate inventory valuation.  This migration deliberately creates no
-- default policy and does not alter quantity or lot carrying values.
create table if not exists inventory_cost_policies (
  policy_id uuid primary key,
  scope_type text not null check (scope_type = 'organization'),
  method text not null check (method in ('fifo', 'weighted_average', 'specific_identification', 'practice_specific')),
  currency text not null check (currency ~ '^[A-Z]{3}$'),
  tax_treatment text not null,
  freight_treatment text not null,
  landed_cost_treatment text not null,
  rounding_rule text not null check (rounding_rule in ('half_up', 'half_even', 'truncate')),
  backdated_entry_rule text not null check (backdated_entry_rule in ('prohibited', 'restatement')),
  effective_date date not null,
  approval_reference text not null,
  rationale text not null,
  revision integer not null check (revision > 0),
  status text not null check (status in ('active', 'superseded')),
  activated_at timestamptz not null,
  activated_by text not null,
  superseded_at timestamptz,
  superseded_by text
);
create unique index if not exists ux_inventory_cost_policies_active_organization
  on inventory_cost_policies(scope_type) where status = 'active';

create table if not exists inventory_cost_policy_change_requests (
  request_id uuid primary key,
  proposed_definition jsonb not null,
  baseline_policy_id uuid references inventory_cost_policies(policy_id),
  baseline_revision integer,
  reason text not null,
  status text not null check (status in ('draft', 'submitted', 'approved', 'rejected', 'activated', 'cancelled')),
  version integer not null default 0 check (version >= 0),
  created_at timestamptz not null,
  created_by text not null,
  updated_at timestamptz not null,
  updated_by text not null
);
create unique index if not exists ux_inventory_cost_policy_change_requests_open
  on inventory_cost_policy_change_requests ((1)) where status in ('draft', 'submitted', 'approved');

create table if not exists inventory_cost_policy_change_request_events (
  event_id bigint generated always as identity primary key,
  request_id uuid not null references inventory_cost_policy_change_requests(request_id) on delete cascade,
  action text not null check (action in ('created', 'submitted', 'approved', 'rejected', 'activated', 'cancelled')),
  note text,
  occurred_at timestamptz not null,
  username text not null
);
create index if not exists ix_inventory_cost_policy_change_request_events_time
  on inventory_cost_policy_change_request_events(request_id, occurred_at desc, event_id desc);
