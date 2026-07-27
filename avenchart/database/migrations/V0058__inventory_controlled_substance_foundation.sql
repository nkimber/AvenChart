create table if not exists inventory_controlled_locations (
  location_id uuid primary key,
  facility_id integer not null references facilities(id) on delete restrict,
  location_code text not null,
  display_name text not null,
  dual_attestation_required boolean not null default false,
  active boolean not null default true,
  created_at timestamptz not null,
  created_by text not null,
  updated_at timestamptz not null,
  updated_by text not null,
  unique (facility_id, location_code)
);

create table if not exists inventory_controlled_location_events (
  event_id bigint generated always as identity primary key,
  location_id uuid not null references inventory_controlled_locations(location_id) on delete restrict,
  action text not null check (action in ('created', 'updated')),
  prior_active boolean,
  resulting_active boolean not null,
  occurred_at timestamptz not null,
  username text not null
);

alter table inventory_items
  add column if not exists controlled_schedule text;

alter table inventory_items
  drop constraint if exists inventory_items_controlled_schedule_check;

alter table inventory_items
  add constraint inventory_items_controlled_schedule_check
  check (controlled_schedule is null or controlled_schedule in ('II', 'III', 'IV', 'V'));

create table if not exists inventory_controlled_item_classification_events (
  event_id bigint generated always as identity primary key,
  item_id integer not null references inventory_items(item_id) on delete restrict,
  prior_schedule text,
  resulting_schedule text,
  occurred_at timestamptz not null,
  username text not null
);

create index if not exists ix_inventory_controlled_locations_facility_active
  on inventory_controlled_locations(facility_id, active, location_code);

create index if not exists ix_inventory_controlled_item_classification_events_item_time
  on inventory_controlled_item_classification_events(item_id, occurred_at desc, event_id desc);
