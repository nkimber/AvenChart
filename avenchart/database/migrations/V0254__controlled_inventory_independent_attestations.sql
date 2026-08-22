-- A controlled-inventory attestation is a one-time approval of an exact action
-- payload. It is deliberately not an authentication credential: the approver
-- must use their own active session to approve, and the action transaction
-- consumes the approved record atomically with the resulting evidence.
create table if not exists inventory_controlled_action_attestations (
  attestation_id uuid primary key,
  action text not null check (action in ('custody_movement', 'count_submit', 'discrepancy_correction')),
  context_id uuid,
  payload_digest text not null check (length(payload_digest) = 64),
  summary text not null check (length(summary) between 1 and 500),
  requested_by text not null,
  requested_at timestamptz not null,
  expires_at timestamptz not null,
  status text not null check (status in ('pending', 'approved', 'consumed', 'cancelled', 'expired')),
  approved_by text,
  approved_at timestamptz,
  consumed_by text,
  consumed_at timestamptz,
  check (expires_at > requested_at),
  check ((status = 'pending' and approved_by is null and approved_at is null and consumed_by is null and consumed_at is null)
      or (status = 'approved' and approved_by is not null and approved_at is not null and consumed_by is null and consumed_at is null)
      or (status = 'consumed' and approved_by is not null and approved_at is not null and consumed_by is not null and consumed_at is not null)
      or (status in ('cancelled', 'expired') and consumed_by is null and consumed_at is null))
);

create index if not exists ix_inventory_controlled_action_attestations_pending
  on inventory_controlled_action_attestations(action, requested_at desc)
  where status in ('pending', 'approved');

create index if not exists ix_inventory_controlled_action_attestations_context
  on inventory_controlled_action_attestations(context_id, requested_at desc)
  where context_id is not null;

alter table inventory_controlled_custody_events
  add column if not exists attestation_id uuid references inventory_controlled_action_attestations(attestation_id) on delete restrict;

alter table inventory_controlled_count_sessions
  add column if not exists counter_attestation_id uuid references inventory_controlled_action_attestations(attestation_id) on delete restrict;

create unique index if not exists ux_inventory_controlled_custody_events_attestation
  on inventory_controlled_custody_events(attestation_id)
  where attestation_id is not null;

create unique index if not exists ux_inventory_controlled_count_sessions_counter_attestation
  on inventory_controlled_count_sessions(counter_attestation_id)
  where counter_attestation_id is not null;
