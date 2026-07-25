-- Legacy Legacy EHR models selectable code systems as active, ordered code types.
create table if not exists coding_catalogs (
  catalog_key text primary key,
  display_name text not null,
  sequence integer not null check (sequence >= 0),
  active boolean not null default true,
  claim_enabled boolean not null default false,
  fee_enabled boolean not null default false,
  modifier_length integer not null default 0 check (modifier_length >= 0),
  updated_at timestamptz not null,
  updated_by text not null
);
create unique index if not exists ux_coding_catalogs_sequence on coding_catalogs (sequence);
insert into coding_catalogs (catalog_key, display_name, sequence, active, claim_enabled, fee_enabled, modifier_length, updated_at, updated_by) values
  ('ICD10', 'ICD-10-CM', 10, true, true, false, 0, now(), 'seed'),
  ('CPT4', 'CPT', 20, true, true, true, 2, now(), 'seed'),
  ('SNOMED', 'SNOMED CT', 30, true, false, false, 0, now(), 'seed')
on conflict (catalog_key) do nothing;
