alter table encounters
  add column if not exists row_version bigint not null default 1;

create sequence if not exists vitals_id_seq;
select setval(
  'vitals_id_seq',
  greatest(coalesce((select max(id) from vitals), 0) + 1, 1),
  false);
alter sequence vitals_id_seq owned by vitals.id;
alter table vitals alter column id set default nextval('vitals_id_seq');
