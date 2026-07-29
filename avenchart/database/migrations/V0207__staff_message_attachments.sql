create table if not exists staff_message_attachments (
  id uuid primary key,
  message_id text not null references messages(id),
  patient_id text not null references patients(canonical_id),
  file_name text not null,
  content_type text not null check (content_type in ('application/pdf', 'image/png', 'image/jpeg', 'text/plain')),
  size_bytes integer not null check (size_bytes > 0 and size_bytes <= 4194304),
  sha256 text not null,
  content bytea not null,
  uploaded_by text not null,
  uploaded_at timestamptz not null default now()
);

create index if not exists ix_staff_message_attachments_message_time
  on staff_message_attachments(message_id, uploaded_at, id);
