-- Legacy Track Anything selects a top-level track for an encounter, then records
-- timestamped rows across that track's active direct child items.
create table if not exists encounter_track_records (
  record_id uuid primary key,
  encounter integer not null references encounters(encounter) on delete cascade,
  track_type_id integer not null references track_anything_types(id) on delete restrict,
  track_name text not null,
  created_at timestamptz not null default now(),
  created_by text not null
);

create table if not exists encounter_track_readings (
  reading_id uuid primary key,
  record_id uuid not null references encounter_track_records(record_id) on delete cascade,
  recorded_at timestamptz not null,
  recorded_by text not null
);

create table if not exists encounter_track_reading_values (
  reading_id uuid not null references encounter_track_readings(reading_id) on delete cascade,
  item_type_id integer not null references track_anything_types(id) on delete restrict,
  item_name text not null,
  value text not null,
  primary key (reading_id, item_type_id)
);

create index if not exists idx_encounter_track_records_encounter on encounter_track_records(encounter, created_at desc);
create index if not exists idx_encounter_track_readings_recorded on encounter_track_readings(record_id, recorded_at desc);
