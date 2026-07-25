create table if not exists office_notes (
    id uuid primary key,
    body text not null check (length(body) <= 4000),
    author text not null,
    group_name text,
    active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create index if not exists ix_office_notes_created_at on office_notes (created_at desc);
create index if not exists ix_office_notes_active_created_at on office_notes (active, created_at desc);
