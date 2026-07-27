-- A custody event may require a second, independently authenticated actor.
-- Witness identity is stored with the append-only event, never supplied as a
-- free-text assertion or mutable after the movement is posted.
alter table inventory_controlled_custody_events
  add column if not exists witness_username text,
  add column if not exists witnessed_at timestamptz;

alter table inventory_controlled_custody_events
  drop constraint if exists inventory_controlled_custody_events_witness_check;

alter table inventory_controlled_custody_events
  add constraint inventory_controlled_custody_events_witness_check
  check ((witness_username is null and witnessed_at is null)
      or (witness_username is not null and witnessed_at is not null and witness_username <> performed_by));

create index if not exists ix_inventory_controlled_custody_events_witness
  on inventory_controlled_custody_events(witness_username, witnessed_at desc)
  where witness_username is not null;
