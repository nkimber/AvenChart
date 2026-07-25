create table if not exists module_catalog (
  module_key text primary key,
  display_name text not null,
  category text not null,
  status text not null check (status in ('enabled', 'disabled', 'decision-required', 'partner-gated')),
  description text not null,
  updated_at timestamptz not null,
  updated_by text not null
);
insert into module_catalog(module_key,display_name,category,status,description,updated_at,updated_by) values
 ('THERAPY_GROUPS','Therapy groups','specialty','enabled','Local group workflow.',now(),'seed'),
 ('EASIPRO','EasiPRO','specialty','decision-required','Requires accountable owner decision.',now(),'seed'),
 ('FAX_SMS','Fax/SMS','communications','partner-gated','Requires approved delivery provider.',now(),'seed')
on conflict(module_key) do nothing;
