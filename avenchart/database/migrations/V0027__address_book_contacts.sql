create table if not exists address_book_contacts (
  id serial primary key, organization text not null, first_name text not null, last_name text not null,
  specialty text, npi text, contact_type text not null default 'external_provider', phone text, mobile text, fax text, email text,
  street text, city text, state text, postal_code text, active boolean not null default true
);
create index if not exists ix_address_book_contacts_search on address_book_contacts (organization, last_name, first_name);
