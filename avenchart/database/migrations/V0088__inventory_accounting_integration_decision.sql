-- VAL-05: the target keeps financial posting outside the product until a
-- finance owner accepts an integration mapping and reconciliation contract.
create table if not exists inventory_accounting_integration_decisions (
  decision_id uuid primary key,
  mode text not null check (mode in ('external', 'integration_accepted')),
  finance_owner text not null,
  effective_date date not null,
  mapping_reference text,
  reconciliation_reference text,
  rationale text not null,
  revision integer not null check (revision > 0),
  status text not null check (status in ('active', 'superseded')),
  activated_at timestamptz not null,
  activated_by text not null,
  superseded_at timestamptz,
  superseded_by text,
  check ((mode = 'external' and mapping_reference is null and reconciliation_reference is null)
      or (mode = 'integration_accepted' and mapping_reference is not null and reconciliation_reference is not null))
);
create unique index if not exists ux_inventory_accounting_integration_decisions_active
  on inventory_accounting_integration_decisions ((1)) where status = 'active';

create table if not exists inventory_accounting_integration_change_requests (
  request_id uuid primary key,
  proposed_definition jsonb not null,
  baseline_decision_id uuid references inventory_accounting_integration_decisions(decision_id),
  baseline_revision integer,
  reason text not null,
  status text not null check (status in ('draft', 'submitted', 'approved', 'rejected', 'activated', 'cancelled')),
  version integer not null default 0 check (version >= 0),
  created_at timestamptz not null,
  created_by text not null,
  updated_at timestamptz not null,
  updated_by text not null
);
create unique index if not exists ux_inventory_accounting_integration_change_requests_open
  on inventory_accounting_integration_change_requests ((1)) where status in ('draft', 'submitted', 'approved');

create table if not exists inventory_accounting_integration_change_request_events (
  event_id bigint generated always as identity primary key,
  request_id uuid not null references inventory_accounting_integration_change_requests(request_id) on delete cascade,
  action text not null check (action in ('created', 'submitted', 'approved', 'rejected', 'activated', 'cancelled')),
  note text,
  occurred_at timestamptz not null,
  username text not null
);
create index if not exists ix_inventory_accounting_integration_change_request_events_time
  on inventory_accounting_integration_change_request_events(request_id, occurred_at desc, event_id desc);
