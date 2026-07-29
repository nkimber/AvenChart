create table if not exists configuration_package_import_requests (
  request_id uuid primary key,
  package_sha256 text not null,
  package_document jsonb not null,
  baseline_document jsonb not null,
  reason text not null check (length(trim(reason)) > 0),
  status text not null check (status in ('draft', 'submitted', 'approved', 'rejected', 'activated', 'cancelled')),
  version integer not null default 0 check (version >= 0),
  created_at timestamptz not null,
  created_by text not null,
  updated_at timestamptz not null,
  updated_by text not null
);

create unique index if not exists ux_configuration_package_import_requests_open
  on configuration_package_import_requests ((1))
  where status in ('draft', 'submitted', 'approved');

create index if not exists ix_configuration_package_import_requests_updated
  on configuration_package_import_requests(updated_at desc, request_id desc);

create table if not exists configuration_package_import_request_events (
  event_id bigint generated always as identity primary key,
  request_id uuid not null references configuration_package_import_requests(request_id) on delete cascade,
  action text not null check (action in ('created', 'submitted', 'approved', 'rejected', 'activated', 'cancelled')),
  note text,
  occurred_at timestamptz not null,
  username text not null
);

create index if not exists ix_configuration_package_import_request_events_request
  on configuration_package_import_request_events(request_id, occurred_at desc, event_id desc);
