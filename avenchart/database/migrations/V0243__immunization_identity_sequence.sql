create sequence if not exists immunizations_id_seq;

select setval(
    'immunizations_id_seq',
    greatest(coalesce((select max(id) from immunizations), 0), 8500000),
    true);

alter table immunizations
    alter column id set default nextval('immunizations_id_seq');

alter sequence immunizations_id_seq owned by immunizations.id;
