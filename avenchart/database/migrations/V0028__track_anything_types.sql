create table if not exists track_anything_types (id serial primary key,parent_id integer references track_anything_types(id) on delete cascade,name text not null,description text,position integer not null default 0,active boolean not null default true);
create index if not exists ix_track_anything_types_parent_position on track_anything_types(parent_id,position);
