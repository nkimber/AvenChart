-- REP-01: replace request-time report-definition DDL with a migration-owned,
-- versioned governance model. Existing saved definitions are retained as
-- explicitly unreviewed drafts and cannot run until they complete governance.

create table if not exists saved_report_definitions (
  id uuid primary key,
  name text not null,
  report_type text not null,
  schedule text not null,
  active boolean not null default false,
  created_by text not null,
  created_at timestamptz not null,
  last_run_at timestamptz,
  run_count integer not null default 0
);

create table if not exists saved_report_runs (
  run_id text primary key,
  definition_id uuid not null references saved_report_definitions(id),
  ran_at timestamptz not null,
  ran_by text not null,
  output_format text not null,
  row_count integer not null
);

alter table saved_report_definitions
  add column if not exists stable_key text;

alter table saved_report_definitions
  add column if not exists latest_revision_id uuid;

alter table saved_report_definitions
  add column if not exists active_revision_id uuid;

alter table saved_report_definitions
  add column if not exists governance_version integer not null default 0;

alter table saved_report_definitions
  add column if not exists legacy_active_before_governance boolean;

update saved_report_definitions
set stable_key = 'legacy.' || replace(id::text, '-', '')
where stable_key is null or btrim(stable_key) = '';

alter table saved_report_definitions
  alter column stable_key set not null;

create unique index if not exists ux_saved_report_definitions_stable_key
  on saved_report_definitions (stable_key);

create table if not exists saved_report_definition_revisions (
  revision_id uuid primary key,
  definition_id uuid not null references saved_report_definitions(id) on delete restrict,
  revision_number integer not null check (revision_number > 0),
  title text not null,
  owner_username text not null,
  purpose text not null,
  report_family text not null,
  metric_dictionary jsonb not null,
  parameter_schema jsonb not null,
  source_datasets jsonb not null,
  output_schema jsonb not null,
  sensitivity text not null,
  row_policy text not null,
  retention_days integer,
  allowed_recipients jsonb not null,
  delivery_modes jsonb not null,
  validation_fixture jsonb not null,
  status text not null check (
    status in ('draft', 'reviewed', 'approved', 'active', 'suspended', 'retired')
  ),
  version integer not null default 0 check (version >= 0),
  predecessor_revision_id uuid references saved_report_definition_revisions(revision_id) on delete restrict,
  created_at timestamptz not null,
  created_by text not null,
  updated_at timestamptz not null,
  updated_by text not null,
  effective_from timestamptz,
  effective_to timestamptz,
  unique (definition_id, revision_number)
);

create index if not exists ix_saved_report_definition_revisions_definition
  on saved_report_definition_revisions (definition_id, revision_number desc);

create index if not exists ix_saved_report_definition_revisions_status
  on saved_report_definition_revisions (status, updated_at desc);

create table if not exists saved_report_definition_events (
  event_id uuid primary key,
  definition_id uuid not null references saved_report_definitions(id) on delete restrict,
  revision_id uuid not null references saved_report_definition_revisions(revision_id) on delete restrict,
  revision_number integer not null check (revision_number > 0),
  action text not null,
  from_status text,
  to_status text not null,
  reason text not null,
  actor_username text not null,
  occurred_at timestamptz not null,
  snapshot_checksum text not null
);

create index if not exists ix_saved_report_definition_events_definition
  on saved_report_definition_events (definition_id, occurred_at desc, event_id desc);

-- Backfill old saved definitions with a safe draft. Unknown policy facts stay
-- visibly unknown; this migration does not invent owner approval or sensitivity.
insert into saved_report_definition_revisions (
  revision_id,
  definition_id,
  revision_number,
  title,
  owner_username,
  purpose,
  report_family,
  metric_dictionary,
  parameter_schema,
  source_datasets,
  output_schema,
  sensitivity,
  row_policy,
  retention_days,
  allowed_recipients,
  delivery_modes,
  validation_fixture,
  status,
  version,
  predecessor_revision_id,
  created_at,
  created_by,
  updated_at,
  updated_by,
  effective_from,
  effective_to
)
select
  gen_random_uuid(),
  definition.id,
  1,
  definition.name,
  definition.created_by,
  'Migrated saved definition; purpose requires report-owner review.',
  definition.report_type,
  jsonb_build_array(
    jsonb_build_object(
      'key', 'legacy-output',
      'label', 'Legacy output',
      'definition', 'Unreviewed legacy output dictionary.',
      'unit', 'row',
      'sourceField', 'legacy'
    )
  ),
  case
    when definition.report_type in (
      'appointments', 'encounters', 'referrals', 'chart-tracker', 'inventory'
    ) then jsonb_build_array(
      jsonb_build_object(
        'key', 'from',
        'label', 'From date',
        'type', 'date',
        'required', false,
        'maxSpanDays', 366
      ),
      jsonb_build_object(
        'key', 'to',
        'label', 'To date',
        'type', 'date',
        'required', false,
        'maxSpanDays', 366
      )
    )
    else '[]'::jsonb
  end,
  jsonb_build_array(
    jsonb_build_object(
      'key', definition.report_type,
      'description', 'Legacy source dataset requires owner review.',
      'fields', jsonb_build_array('legacy')
    )
  ),
  jsonb_build_array(
    jsonb_build_object(
      'key', 'legacy',
      'label', 'Legacy output',
      'type', 'string',
      'sensitivity', 'unknown'
    )
  ),
  'unknown',
  'owner-review-required',
  null,
  '["requesting-user"]'::jsonb,
  '["local-download"]'::jsonb,
  jsonb_build_object(
    'datasetId', 'gold-legacy-ehr-synthetic',
    'scenario', 'legacy-migration:' || definition.report_type,
    'expectedColumns', jsonb_build_array('legacy'),
    'expectedRowCount', null
  ),
  'draft',
  0,
  null,
  definition.created_at,
  definition.created_by,
  definition.created_at,
  definition.created_by,
  null,
  null
from saved_report_definitions definition
where not exists (
  select 1
  from saved_report_definition_revisions revision
  where revision.definition_id = definition.id
);

update saved_report_definitions definition
set
  latest_revision_id = revision.revision_id,
  legacy_active_before_governance = coalesce(
    definition.legacy_active_before_governance,
    definition.active
  ),
  active = false
from saved_report_definition_revisions revision
where revision.definition_id = definition.id
  and revision.revision_number = 1
  and definition.latest_revision_id is null;

insert into saved_report_definition_events (
  event_id,
  definition_id,
  revision_id,
  revision_number,
  action,
  from_status,
  to_status,
  reason,
  actor_username,
  occurred_at,
  snapshot_checksum
)
select
  gen_random_uuid(),
  revision.definition_id,
  revision.revision_id,
  revision.revision_number,
  'migrated',
  null,
  'draft',
  'Legacy saved definition promoted to an unreviewed governed draft.',
  revision.created_by,
  revision.created_at,
  'legacy-md5:' || md5(
    concat_ws(
      '|',
      revision.definition_id::text,
      revision.revision_number::text,
      revision.title,
      revision.report_family,
      revision.status
    )
  )
from saved_report_definition_revisions revision
where revision.revision_number = 1
  and not exists (
    select 1
    from saved_report_definition_events event
    where event.revision_id = revision.revision_id
  );
