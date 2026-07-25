create table if not exists api_client_registry (
  client_key text primary key,
  display_name text not null,
  redirect_uri text not null,
  scopes text not null,
  active boolean not null default true,
  updated_at timestamptz not null,
  updated_by text not null
);
insert into api_client_registry(client_key,display_name,redirect_uri,scopes,active,updated_at,updated_by) values
 ('LOCAL_PORTAL','Local patient portal','https://portal.example.test/callback','patient.read patient.write',true,now(),'seed')
on conflict(client_key) do nothing;
