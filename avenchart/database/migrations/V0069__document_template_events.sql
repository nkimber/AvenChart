create table if not exists document_template_events (
  event_id bigint generated always as identity primary key,
  template_id uuid not null references document_templates(id) on delete cascade,
  action text not null check (
    action in (
      'created',
      'updated',
      'activated',
      'retired',
      'binary-version-uploaded',
      'patient-attachment-generated'
    )
  ),
  summary text not null,
  binary_version_id uuid null references document_template_binary_versions(id) on delete set null,
  patient_document_id bigint null,
  patient_id text null,
  occurred_at timestamptz not null default now(),
  username text not null
);

create index if not exists ix_document_template_events_template_time
  on document_template_events(template_id, occurred_at desc, event_id desc);
