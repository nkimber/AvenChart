-- Local, immutable encounter-summary mutation audit. Deliberately stores field
-- names only; historical clinical values are not duplicated in the audit stream.
create table if not exists encounter_audit_events (
  event_id uuid primary key,
  encounter integer not null,
  occurred_at timestamptz not null,
  username text not null,
  action text not null,
  changed_fields text not null
);

create index if not exists idx_encounter_audit_events_encounter
  on encounter_audit_events (encounter, occurred_at desc);
