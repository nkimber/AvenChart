alter table encounter_signatures
  add column if not exists encounter_version bigint;
