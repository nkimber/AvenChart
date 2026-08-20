create sequence if not exists staff_id_seq;
select setval(
  'staff_id_seq',
  greatest(coalesce((select max(id) from staff), 0) + 1, 1),
  false);
alter sequence staff_id_seq owned by staff.id;
alter table staff alter column id set default nextval('staff_id_seq');

create sequence if not exists facilities_id_seq;
select setval(
  'facilities_id_seq',
  greatest(coalesce((select max(id) from facilities), 0) + 1, 1),
  false);
alter sequence facilities_id_seq owned by facilities.id;
alter table facilities alter column id set default nextval('facilities_id_seq');
