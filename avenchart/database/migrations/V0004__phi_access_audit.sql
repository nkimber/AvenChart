-- Records protected API authorization decisions without request payloads,
-- query strings, document contents, or patient identifiers.
create table if not exists phi_access_audit_events (
  audit_id uuid primary key,
  occurred_at timestamptz not null,
  username text not null,
  session_id uuid,
  http_method text not null,
  endpoint_name text not null,
  required_permission text not null,
  authorized boolean not null,
  response_status integer not null
);

create index if not exists idx_phi_access_audit_username_occurred
  on phi_access_audit_events (username, occurred_at desc);

create index if not exists idx_phi_access_audit_endpoint_occurred
  on phi_access_audit_events (endpoint_name, occurred_at desc);
