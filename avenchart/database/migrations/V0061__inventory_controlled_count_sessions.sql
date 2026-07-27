-- Controlled counts are immutable snapshots. They do not adjust stock directly;
-- discrepancies are retained for a later investigated compensating movement.
create table if not exists inventory_controlled_count_sessions (
  session_id uuid primary key,
  location_id uuid not null references inventory_controlled_locations(location_id) on delete restrict,
  count_type text not null check (count_type in ('opening', 'shift', 'cycle', 'closing')),
  status text not null check (status in ('in_progress', 'reconciled', 'discrepancy_open')),
  movement_lock_active boolean not null default false,
  reason text not null,
  idempotency_key text not null unique,
  started_by text not null,
  started_at timestamptz not null,
  submitted_by text,
  submitted_at timestamptz,
  counter_username text,
  check ((status = 'in_progress' and submitted_by is null and submitted_at is null and counter_username is null)
      or (status in ('reconciled', 'discrepancy_open') and submitted_by is not null and submitted_at is not null and counter_username is not null))
);

create table if not exists inventory_controlled_count_lines (
  line_id uuid primary key,
  session_id uuid not null references inventory_controlled_count_sessions(session_id) on delete restrict,
  lot_id integer not null references inventory_lots(lot_id) on delete restrict,
  expected_quantity numeric(12,2) not null check (expected_quantity >= 0),
  observed_quantity numeric(12,2) check (observed_quantity >= 0),
  variance_quantity numeric(12,2),
  unique (session_id, lot_id),
  check ((observed_quantity is null and variance_quantity is null) or (observed_quantity is not null and variance_quantity = observed_quantity - expected_quantity))
);

create table if not exists inventory_controlled_count_discrepancies (
  discrepancy_id uuid primary key,
  session_id uuid not null references inventory_controlled_count_sessions(session_id) on delete restrict,
  line_id uuid not null unique references inventory_controlled_count_lines(line_id) on delete restrict,
  status text not null check (status in ('open', 'investigating', 'corrected', 'reported', 'closed')) default 'open',
  opened_by text not null,
  opened_at timestamptz not null,
  investigation_notes text,
  correction_event_id uuid references inventory_controlled_custody_events(event_id) on delete restrict,
  closed_by text,
  closed_at timestamptz,
  check ((status = 'open' and closed_by is null and closed_at is null)
      or status <> 'open')
);

create index if not exists ix_inventory_controlled_count_sessions_location_status
  on inventory_controlled_count_sessions(location_id, status, started_at desc);
create index if not exists ix_inventory_controlled_count_lines_session on inventory_controlled_count_lines(session_id);
create index if not exists ix_inventory_controlled_count_discrepancies_session on inventory_controlled_count_discrepancies(session_id, status);
