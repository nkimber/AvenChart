create sequence if not exists lab_order_catalog_id_seq;
select setval(
    'lab_order_catalog_id_seq',
    greatest(coalesce((select max(id) from lab_order_catalog), 0), 1),
    coalesce((select max(id) from lab_order_catalog), 0) > 0);
alter table lab_order_catalog alter column id set default nextval('lab_order_catalog_id_seq');
alter sequence lab_order_catalog_id_seq owned by lab_order_catalog.id;

create sequence if not exists lab_providers_id_seq;
select setval(
    'lab_providers_id_seq',
    greatest(coalesce((select max(id) from lab_providers), 0), 1),
    coalesce((select max(id) from lab_providers), 0) > 0);
alter table lab_providers alter column id set default nextval('lab_providers_id_seq');
alter sequence lab_providers_id_seq owned by lab_providers.id;

create sequence if not exists lab_provider_address_book_id_seq;
select setval(
    'lab_provider_address_book_id_seq',
    greatest(coalesce((select max(id) from lab_provider_address_book), 0), 1),
    coalesce((select max(id) from lab_provider_address_book), 0) > 0);
alter table lab_provider_address_book alter column id set default nextval('lab_provider_address_book_id_seq');
alter sequence lab_provider_address_book_id_seq owned by lab_provider_address_book.id;
