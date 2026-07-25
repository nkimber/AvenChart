create table if not exists encounter_layout_form_records (
  record_id uuid primary key,
  encounter integer not null,
  layout_key text not null references form_layouts(layout_key),
  revision integer not null check (revision > 0),
  saved_at timestamptz not null,
  saved_by text not null,
  unique (encounter, layout_key, revision)
);

create table if not exists encounter_layout_form_values (
  record_id uuid not null references encounter_layout_form_records(record_id),
  field_key text not null,
  field_label text not null,
  field_value text not null,
  primary key (record_id, field_key)
);

create index if not exists ix_encounter_layout_form_records_encounter on encounter_layout_form_records(encounter, layout_key, revision desc);

insert into form_option_lists(list_key,title,active,updated_at,updated_by) values
  ('yesno','Yes or no',true,now(),'seed')
on conflict(list_key) do nothing;

insert into form_option_values(list_key,option_key,title,sequence,is_default,active,option_value,updated_at,updated_by) values
  ('yesno','yes','Yes',10,false,true,'yes',now(),'seed'),
  ('yesno','no','No',20,true,true,'no',now(),'seed')
on conflict(list_key,option_key) do nothing;

insert into form_layouts(layout_key,title,mapping,sequence,active,updated_at,updated_by) values
  ('INTAKE','Encounter intake','Encounter',20,true,now(),'seed')
on conflict(layout_key) do nothing;

insert into form_layout_groups(layout_key,group_key,title,sequence,active,updated_at,updated_by) values
  ('INTAKE','screening','Screening',10,true,now(),'seed')
on conflict(layout_key,group_key) do nothing;

insert into form_layout_fields(layout_key,field_key,group_key,label,field_type,sequence,required,active,max_length,list_id,default_value,updated_at,updated_by) values
  ('INTAKE','chief_concern','screening','Chief concern','textarea',10,true,true,1000,'','',now(),'seed'),
  ('INTAKE','follow_up_needed','screening','Follow-up needed','select',20,true,true,3,'yesno','no',now(),'seed')
on conflict(layout_key,field_key) do nothing;
