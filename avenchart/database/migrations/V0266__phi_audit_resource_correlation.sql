alter table phi_access_audit_events
  add column if not exists resource_type text,
  add column if not exists resource_id text;

create index if not exists ix_phi_access_audit_resource_time
  on phi_access_audit_events(resource_type, resource_id, occurred_at desc)
  where resource_id is not null;
