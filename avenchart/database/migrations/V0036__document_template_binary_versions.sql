create table if not exists document_template_binary_versions (
  id uuid primary key,
  template_id uuid not null references document_templates(id) on delete cascade,
  version integer not null check (version > 0),
  file_name text not null,
  mimetype text not null,
  size_bytes integer not null check (size_bytes > 0),
  sha256 text not null,
  content bytea not null,
  created_at timestamptz not null default now(),
  unique(template_id, version)
);
