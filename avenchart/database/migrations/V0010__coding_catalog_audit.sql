create table if not exists coding_catalog_audit_events (
  event_id uuid primary key,
  catalog_key text not null references coding_catalogs(catalog_key),
  action text not null check (action in ('created', 'updated')),
  occurred_at timestamptz not null,
  username text not null
);
