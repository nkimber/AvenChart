create table if not exists form_option_lists (
  list_key text primary key,
  title text not null,
  active boolean not null default true,
  updated_at timestamptz not null,
  updated_by text not null
);

create table if not exists form_option_values (
  list_key text not null references form_option_lists(list_key),
  option_key text not null,
  title text not null,
  sequence integer not null check (sequence >= 0),
  is_default boolean not null default false,
  active boolean not null default true,
  option_value text not null default '',
  updated_at timestamptz not null,
  updated_by text not null,
  primary key (list_key, option_key)
);

create index if not exists ix_form_option_values_list_sequence on form_option_values(list_key, sequence, option_key);

insert into form_option_lists(list_key,title,active,updated_at,updated_by) values
  ('state','State or province',true,now(),'seed')
on conflict(list_key) do nothing;

insert into form_option_values(list_key,option_key,title,sequence,is_default,active,option_value,updated_at,updated_by) values
  ('state','MA','Massachusetts',10,false,true,'MA',now(),'seed'),
  ('state','NY','New York',20,false,true,'NY',now(),'seed'),
  ('state','PA','Pennsylvania',30,false,true,'PA',now(),'seed')
on conflict(list_key,option_key) do nothing;
