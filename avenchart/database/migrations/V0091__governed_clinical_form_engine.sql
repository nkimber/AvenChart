create table if not exists clinical_form_definitions (
  definition_id uuid primary key,
  stable_key text not null unique
    check (stable_key ~ '^[a-z][a-z0-9]*(\.[a-z0-9]+)*$'),
  latest_revision integer not null check (latest_revision > 0),
  effective_revision integer,
  created_at timestamptz not null,
  created_by text not null,
  updated_at timestamptz not null,
  updated_by text not null
);

create table if not exists clinical_form_revisions (
  definition_id uuid not null
    references clinical_form_definitions(definition_id) on delete restrict,
  revision integer not null check (revision > 0),
  status text not null
    check (status in (
      'draft', 'in-review', 'approved', 'effective',
      'suspended', 'superseded', 'retired', 'rejected'
    )),
  version integer not null default 0 check (version >= 0),
  schema_json jsonb not null check (jsonb_typeof(schema_json) = 'object'),
  renderer_version text not null,
  schema_hash char(64) not null check (schema_hash ~ '^[0-9a-f]{64}$'),
  author text not null,
  reviewed_by text,
  approved_by text,
  effective_from timestamptz,
  effective_to timestamptz,
  predecessor_revision integer,
  created_at timestamptz not null,
  updated_at timestamptz not null,
  updated_by text not null,
  primary key (definition_id, revision),
  foreign key (definition_id, predecessor_revision)
    references clinical_form_revisions(definition_id, revision) on delete restrict,
  check (effective_to is null or effective_from is null or effective_to > effective_from)
);

alter table clinical_form_definitions
  drop constraint if exists clinical_form_definitions_effective_revision_fkey;
alter table clinical_form_definitions
  add constraint clinical_form_definitions_effective_revision_fkey
  foreign key (definition_id, effective_revision)
  references clinical_form_revisions(definition_id, revision)
  on delete restrict;

create index if not exists ix_clinical_form_revisions_status
  on clinical_form_revisions(status, updated_at desc);

create table if not exists clinical_form_definition_events (
  event_id bigint generated always as identity primary key,
  definition_id uuid not null
    references clinical_form_definitions(definition_id) on delete cascade,
  revision integer not null,
  action text not null,
  from_status text,
  to_status text not null,
  actor text not null,
  reason text not null,
  occurred_at timestamptz not null,
  snapshot_hash char(64) not null check (snapshot_hash ~ '^[0-9a-f]{64}$'),
  foreign key (definition_id, revision)
    references clinical_form_revisions(definition_id, revision) on delete cascade
);

create index if not exists ix_clinical_form_definition_events_series
  on clinical_form_definition_events(definition_id, event_id desc);

create table if not exists clinical_form_instances (
  instance_id uuid primary key,
  definition_id uuid not null,
  definition_revision integer not null,
  patient_id text not null references patients(canonical_id) on delete restrict,
  encounter_id integer,
  state text not null
    check (state in (
      'draft', 'ready-for-signature', 'awaiting-co-sign',
      'signed', 'amended', 'corrected'
    )),
  version integer not null default 0 check (version >= 0),
  author text not null,
  values_json jsonb not null default '{}'::jsonb
    check (jsonb_typeof(values_json) = 'object'),
  validation_json jsonb not null default '{}'::jsonb
    check (jsonb_typeof(validation_json) = 'object'),
  idempotency_key text not null,
  predecessor_instance_id uuid references clinical_form_instances(instance_id) on delete restrict,
  successor_instance_id uuid references clinical_form_instances(instance_id) on delete restrict,
  amendment_reason text,
  created_at timestamptz not null,
  updated_at timestamptz not null,
  finalized_at timestamptz,
  signed_at timestamptz,
  foreign key (definition_id, definition_revision)
    references clinical_form_revisions(definition_id, revision) on delete restrict,
  unique (author, idempotency_key)
);

create index if not exists ix_clinical_form_instances_patient
  on clinical_form_instances(patient_id, updated_at desc);

create index if not exists ix_clinical_form_instances_encounter
  on clinical_form_instances(encounter_id, updated_at desc)
  where encounter_id is not null;

create table if not exists clinical_form_signatures (
  signature_id uuid primary key,
  instance_id uuid not null
    references clinical_form_instances(instance_id) on delete restrict,
  role text not null check (role in ('signer', 'co-signer')),
  signer text not null,
  method text not null,
  policy_revision text not null,
  credential_context text not null,
  signed_at timestamptz not null,
  content_hash char(64) not null check (content_hash ~ '^[0-9a-f]{64}$'),
  unique (instance_id, role)
);

create index if not exists ix_clinical_form_signatures_signer
  on clinical_form_signatures(signer, signed_at desc);

create table if not exists clinical_form_instance_events (
  event_id bigint generated always as identity primary key,
  instance_id uuid not null
    references clinical_form_instances(instance_id) on delete cascade,
  version integer not null,
  action text not null,
  from_state text,
  to_state text not null,
  actor text not null,
  reason text not null,
  occurred_at timestamptz not null,
  snapshot_hash char(64) not null check (snapshot_hash ~ '^[0-9a-f]{64}$')
);

create index if not exists ix_clinical_form_instance_events_instance
  on clinical_form_instance_events(instance_id, event_id desc);

insert into clinical_form_definitions (
  definition_id, stable_key, latest_revision, effective_revision,
  created_at, created_by, updated_at, updated_by
)
values (
  '90f00000-0000-4000-8000-000000000001',
  'clinical.observation',
  1,
  null,
  now(),
  'seed',
  now(),
  'seed'
)
on conflict (stable_key) do nothing;

insert into clinical_form_revisions (
  definition_id, revision, status, version, schema_json, renderer_version,
  schema_hash, author, reviewed_by, approved_by, effective_from,
  predecessor_revision, created_at, updated_at, updated_by
)
select
  d.definition_id,
  1,
  'effective',
  3,
  '{
    "stableKey":"clinical.observation",
    "name":"Clinical observation",
    "purpose":"Capture a bounded encounter observation with explainable follow-up validation.",
    "contextScope":"encounter",
    "owningService":"clinical-operations",
    "capability":"encounters.auth_a",
    "signaturePolicy":"author-only",
    "sections":[
      {"key":"observation","title":"Observation","sequence":10,"description":"Current patient-reported and measured facts."},
      {"key":"plan","title":"Plan","sequence":20,"description":"Follow-up and disposition."}
    ],
    "fields":[
      {"key":"chief_concern","sectionKey":"observation","label":"Chief concern","type":"multiline","sequence":10,"required":true,"accessibilityLabel":"Chief concern","helpText":"Describe the primary concern.","maxLength":500,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
      {"key":"pain_score","sectionKey":"observation","label":"Pain score","type":"integer","sequence":20,"required":false,"accessibilityLabel":"Pain score from zero to ten","helpText":"Optional 0 to 10 patient-reported score.","maxLength":null,"minimum":0,"maximum":10,"precision":0,"unit":"score","codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
      {"key":"follow_up","sectionKey":"plan","label":"Follow-up needed","type":"boolean","sequence":10,"required":true,"accessibilityLabel":"Follow-up needed","helpText":null,"maxLength":null,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
      {"key":"disposition","sectionKey":"plan","label":"Disposition","type":"select","sequence":20,"required":false,"accessibilityLabel":"Disposition","helpText":"Select the planned disposition when follow-up is needed.","maxLength":null,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":"local-disposition-v1","options":[{"code":"routine","display":"Routine follow-up"},{"code":"urgent","display":"Urgent follow-up"}],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false},
      {"key":"notes","sectionKey":"plan","label":"Plan notes","type":"multiline","sequence":30,"required":false,"accessibilityLabel":"Plan notes","helpText":null,"maxLength":1000,"minimum":null,"maximum":null,"precision":null,"unit":null,"codeSystem":null,"options":[],"repeatMinimum":null,"repeatMaximum":null,"children":[],"readOnly":false}
    ],
    "rules":[
      {"key":"require_disposition","condition":{"fieldKey":"follow_up","operator":"equals","value":true},"action":"require","targetFieldKey":"disposition","message":"Disposition is required when follow-up is needed.","calculation":null},
      {"key":"warn_high_pain","condition":{"fieldKey":"pain_score","operator":"greater-than-or-equal","value":8},"action":"warning","targetFieldKey":"pain_score","message":"High pain score requires clinical attention.","calculation":null}
    ]
  }'::jsonb,
  'local-clinical-form-renderer-v1',
  '5e78a3058a0d6c58a1bb96c8d4bb0a6858356d743f15a0ccdb4d3a5fcc873a42',
  'seed',
  'seed',
  'seed',
  now(),
  null,
  now(),
  now(),
  'seed'
from clinical_form_definitions d
where d.stable_key = 'clinical.observation'
on conflict (definition_id, revision) do nothing;

update clinical_form_definitions d
set effective_revision = 1
where d.stable_key = 'clinical.observation'
  and d.effective_revision is null
  and exists (
    select 1
    from clinical_form_revisions r
    where r.definition_id = d.definition_id
      and r.revision = 1
      and r.status = 'effective'
  );

insert into clinical_form_definition_events (
  definition_id, revision, action, from_status, to_status,
  actor, reason, occurred_at, snapshot_hash
)
select
  d.definition_id,
  1,
  'seeded-effective',
  null,
  'effective',
  'seed',
  'Install the bounded synthetic clinical observation form.',
  now(),
  r.schema_hash
from clinical_form_definitions d
join clinical_form_revisions r
  on r.definition_id = d.definition_id and r.revision = 1
where d.stable_key = 'clinical.observation'
  and not exists (
    select 1
    from clinical_form_definition_events e
    where e.definition_id = d.definition_id
      and e.revision = 1
      and e.action = 'seeded-effective'
  );
