create table if not exists form_layouts (
  layout_key text primary key,
  title text not null,
  mapping text not null default 'Core',
  sequence integer not null check (sequence >= 0),
  active boolean not null default true,
  updated_at timestamptz not null,
  updated_by text not null
);
create unique index if not exists ux_form_layouts_sequence on form_layouts(sequence);

create table if not exists form_layout_groups (
  layout_key text not null references form_layouts(layout_key),
  group_key text not null,
  title text not null,
  sequence integer not null check (sequence >= 0),
  active boolean not null default true,
  updated_at timestamptz not null,
  updated_by text not null,
  primary key (layout_key, group_key)
);
create unique index if not exists ux_form_layout_groups_sequence on form_layout_groups(layout_key, sequence);

create table if not exists form_layout_fields (
  layout_key text not null,
  field_key text not null,
  group_key text not null,
  label text not null,
  field_type text not null check (field_type in ('text', 'date', 'select', 'textarea', 'checkbox', 'number')),
  sequence integer not null check (sequence >= 0),
  required boolean not null default false,
  active boolean not null default true,
  max_length integer not null default 0 check (max_length >= 0 and max_length <= 4096),
  list_id text not null default '',
  default_value text not null default '',
  updated_at timestamptz not null,
  updated_by text not null,
  primary key (layout_key, field_key),
  foreign key (layout_key, group_key) references form_layout_groups(layout_key, group_key)
);
create unique index if not exists ux_form_layout_fields_sequence on form_layout_fields(layout_key, group_key, sequence);

insert into form_layouts(layout_key,title,mapping,sequence,active,updated_at,updated_by) values ('DEM','Demographics','Core',10,true,now(),'seed') on conflict (layout_key) do nothing;
insert into form_layout_groups(layout_key,group_key,title,sequence,active,updated_at,updated_by) values
  ('DEM','who','Who',10,true,now(),'seed'),
  ('DEM','contact','Contact',20,true,now(),'seed') on conflict (layout_key,group_key) do nothing;
insert into form_layout_fields(layout_key,field_key,group_key,label,field_type,sequence,required,active,max_length,list_id,default_value,updated_at,updated_by) values
  ('DEM','first_name','who','First name','text',10,true,true,63,'','',now(),'seed'),
  ('DEM','last_name','who','Last name','text',20,true,true,63,'','',now(),'seed'),
  ('DEM','birth_date','who','Date of birth','date',30,true,true,10,'','',now(),'seed'),
  ('DEM','phone','contact','Phone','text',10,false,true,40,'','',now(),'seed'),
  ('DEM','email','contact','Email','text',20,false,true,95,'','',now(),'seed') on conflict (layout_key,field_key) do nothing;
