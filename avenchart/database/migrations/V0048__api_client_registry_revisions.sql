create table if not exists api_client_registry_revisions (
  revision_id bigint generated always as identity primary key,
  client_key text not null references api_client_registry(client_key) on delete restrict,
  display_name text not null,
  redirect_uri text not null,
  scopes text not null,
  active boolean not null,
  action text not null check (action in ('baseline','updated','rolled-back')),
  restored_from_revision_id bigint references api_client_registry_revisions(revision_id) on delete restrict,
  occurred_at timestamptz not null,
  username text not null
);
create index if not exists ix_api_client_registry_revisions_key_time on api_client_registry_revisions(client_key,occurred_at desc,revision_id desc);
insert into api_client_registry_revisions(client_key,display_name,redirect_uri,scopes,active,action,occurred_at,username)
select client_key,display_name,redirect_uri,scopes,active,'baseline',updated_at,updated_by
from api_client_registry client
where not exists(select 1 from api_client_registry_revisions revision where revision.client_key=client.client_key);
