create table if not exists chart_tracker_locations(name text primary key,position integer not null default 0,active boolean not null default true);
insert into chart_tracker_locations(name,position) values ('Front Desk',10),('Records',20),('Laboratory',30) on conflict(name) do nothing;
create table if not exists chart_tracker_events(id uuid primary key,patient_id text not null references patients(canonical_id),location text references chart_tracker_locations(name),user_id integer references staff(id),recorded_at timestamptz not null default now(),check((location is null) <> (user_id is null)));
create index if not exists idx_chart_tracker_events_patient_recorded on chart_tracker_events(patient_id,recorded_at desc);
