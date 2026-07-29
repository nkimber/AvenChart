-- Adds an accountable local review/approval lifecycle to the deterministic
-- Clinic Note migration manifest. Local approval does not authorize
-- production migration: the V0201 production and execution checks remain.
alter table clinical_form_migration_manifests
  add column if not exists version integer not null default 1 check (version > 0),
  add column if not exists updated_at timestamptz,
  add column if not exists updated_by text;

update clinical_form_migration_manifests
set updated_at = coalesce(updated_at, created_at),
    updated_by = coalesce(updated_by, 'seed')
where updated_at is null
   or updated_by is null;

alter table clinical_form_migration_manifests
  alter column updated_at set not null,
  alter column updated_by set not null;

create table if not exists clinical_form_migration_manifest_events (
  event_id bigint generated always as identity primary key,
  manifest_id uuid not null
    references clinical_form_migration_manifests(manifest_id) on delete cascade,
  version integer not null check (version > 0),
  action text not null check (action in ('created', 'review', 'approve', 'reject')),
  from_status text,
  to_status text not null
    check (to_status in ('draft', 'in-review', 'locally-approved', 'rejected')),
  actor text not null,
  reason text not null check (char_length(reason) between 10 and 500),
  occurred_at timestamptz not null,
  snapshot_sha256 text not null check (snapshot_sha256 ~ '^[0-9a-f]{64}$'),
  unique (manifest_id, version)
);

create index if not exists ix_clinical_form_migration_manifest_events_series
  on clinical_form_migration_manifest_events(manifest_id, event_id);

with initial_event as (
  select
    manifest_id,
    version,
    'created'::text as action,
    null::text as from_status,
    status as to_status,
    'seed'::text as actor,
    'Seeded non-executing local manifest.'::text as reason,
    created_at as occurred_at
  from clinical_form_migration_manifests
)
insert into clinical_form_migration_manifest_events (
  manifest_id,
  version,
  action,
  from_status,
  to_status,
  actor,
  reason,
  occurred_at,
  snapshot_sha256
)
select
  event.manifest_id,
  event.version,
  event.action,
  event.from_status,
  event.to_status,
  event.actor,
  event.reason,
  event.occurred_at,
  encode(sha256(convert_to(
    jsonb_build_object(
      'manifestId', event.manifest_id,
      'version', event.version,
      'action', event.action,
      'fromStatus', event.from_status,
      'toStatus', event.to_status,
      'actor', event.actor,
      'reason', event.reason,
      'occurredAt', event.occurred_at
    )::text,
    'utf8'
  )), 'hex')
from initial_event event
on conflict (manifest_id, version) do nothing;

-- Clinicians can record the clinical-owner review. Local approval/rejection
-- remains restricted to the existing administrator superuser permission.
insert into access_group_permissions (
  group_value,
  section_value,
  permission_value,
  permission_name,
  return_value
)
values (
  'clin',
  'admin',
  'forms',
  'Forms Administration',
  'write'
)
on conflict (
  group_value,
  section_value,
  permission_value,
  return_value
) do nothing;
