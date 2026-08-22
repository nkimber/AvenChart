-- Local laboratory work must retain the authenticated actor and a durable
-- resource-specific mutation trail. External intake has its own source-bound
-- provenance; these tables cover the professional workflows that create and
-- change orders and results inside AvenChart.
create table if not exists procedure_order_events (
  id bigserial primary key,
  order_id integer not null references lab_orders(id) on delete restrict,
  action text not null check (action in ('created', 'content-updated', 'status-updated', 'transmitted', 'baseline-import')),
  actor text not null,
  detail text not null,
  occurred_at timestamp not null
);

create index if not exists ix_procedure_order_events_order
  on procedure_order_events(order_id, occurred_at desc, id desc);

create table if not exists procedure_result_events (
  id bigserial primary key,
  result_id integer not null references lab_results(id) on delete restrict,
  action text not null check (action in ('created', 'corrected', 'baseline-import')),
  actor text not null,
  detail text not null,
  previous_content_version integer,
  resulting_content_version integer not null check (resulting_content_version > 0),
  occurred_at timestamp not null,
  check (previous_content_version is null or (previous_content_version > 0 and previous_content_version < resulting_content_version))
);

create index if not exists ix_procedure_result_events_result
  on procedure_result_events(result_id, occurred_at desc, id desc);

-- Existing records predate server-derived actor provenance. Retain them as
-- explicit imported evidence rather than inventing an authenticated actor.
insert into procedure_order_events(order_id, action, actor, detail, occurred_at)
select orders.id,
       'baseline-import',
       'legacy-import',
       'Order existed before local laboratory provenance governance.',
       coalesce(orders.date_transmitted, orders.order_date::timestamp, current_timestamp)
from lab_orders orders
where not exists (
  select 1
  from procedure_order_events event
  where event.order_id = orders.id);

insert into procedure_result_events(
  result_id, action, actor, detail, previous_content_version,
  resulting_content_version, occurred_at)
select result.id,
       'baseline-import',
       'legacy-import',
       'Result existed before local laboratory provenance governance.',
       null,
       coalesce((
         select max(version.version_no)
         from procedure_result_versions version
         where version.result_id = result.id), 0) + 1,
       coalesce(result.result_date, current_timestamp)
from lab_results result
where not exists (
  select 1
  from procedure_result_events event
  where event.result_id = result.id);
