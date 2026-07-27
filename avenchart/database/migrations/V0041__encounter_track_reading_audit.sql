-- Legacy Track Anything allows correction of an existing timestamped row. Retain
-- the actor and time of that correction instead of silently overwriting it.
alter table encounter_track_readings
  add column if not exists updated_at timestamptz,
  add column if not exists updated_by text;
