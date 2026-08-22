-- SEC-03: A validated issuer/subject pair is not itself an AvenChart
-- principal.  Provider-scoped mappings make that binding explicit, revocable,
-- and independently auditable.  External subjects are case-sensitive by the
-- OpenID Connect contract; provider identifiers are normalized to lower case.
create table if not exists auth_external_identity_mappings (
  mapping_id uuid primary key,
  provider_id text not null check (
    provider_id = lower(provider_id)
    and length(provider_id) between 2 and 80
    and provider_id ~ '^[a-z0-9][a-z0-9._-]*[a-z0-9]$'
  ),
  external_subject text not null check (
    length(external_subject) between 1 and 512
    and external_subject = btrim(external_subject)
    and external_subject !~ '[[:cntrl:]]'
  ),
  username text not null references auth_accounts(username) on delete restrict,
  active boolean not null default true,
  created_at timestamptz not null default now(),
  created_by text not null check (length(btrim(created_by)) between 1 and 120),
  deactivated_at timestamptz,
  deactivated_by text,
  deactivation_reason text,
  check (
    (active and deactivated_at is null and deactivated_by is null and deactivation_reason is null)
    or (not active and deactivated_at is not null and deactivated_by is not null and deactivation_reason is not null)
  )
);

-- A local account can have one active subject in a given provider, and an
-- external subject can bind to only one active local account.  Historical
-- bindings remain available after deactivation without being reusable by a
-- replayed bearer token.
create unique index if not exists ux_auth_external_identity_mappings_active_subject
  on auth_external_identity_mappings(provider_id, external_subject)
  where active;

create unique index if not exists ux_auth_external_identity_mappings_active_principal
  on auth_external_identity_mappings(provider_id, username)
  where active;

create index if not exists ix_auth_external_identity_mappings_principal
  on auth_external_identity_mappings(username, provider_id, created_at desc);

create table if not exists auth_external_identity_mapping_events (
  event_id uuid primary key,
  mapping_id uuid not null references auth_external_identity_mappings(mapping_id) on delete restrict,
  action text not null check (action in ('created', 'deactivated')),
  actor text not null check (length(btrim(actor)) between 1 and 120),
  reason text,
  occurred_at timestamptz not null default now()
);

create index if not exists ix_auth_external_identity_mapping_events_mapping_time
  on auth_external_identity_mapping_events(mapping_id, occurred_at desc, event_id desc);

create or replace function avenchart_prevent_external_identity_mapping_event_mutation()
returns trigger
language plpgsql
as $$
begin
  raise exception using
    errcode = '55000',
    message = 'External identity mapping events are immutable and cannot be altered or deleted.';
end;
$$;

drop trigger if exists trg_auth_external_identity_mapping_events_immutable on auth_external_identity_mapping_events;
create trigger trg_auth_external_identity_mapping_events_immutable
before update or delete on auth_external_identity_mapping_events
for each row execute function avenchart_prevent_external_identity_mapping_event_mutation();

-- The development-only first-party test IdP deliberately shares the local
-- development account verifier.  Seed its explicit mappings so the isolated
-- test path exercises the same registry lookup as a commercial OIDC provider.
-- The IdP is prohibited in Production by application startup validation.
insert into auth_external_identity_mappings(
  mapping_id, provider_id, external_subject, username, active, created_at, created_by)
select
  md5('test-oidc:' || account.username)::uuid,
  'test-oidc',
  account.username,
  account.username,
  true,
  now(),
  'migration-test-idp-bootstrap'
from auth_accounts account
where account.active = true
  and not exists (
    select 1
    from auth_external_identity_mappings mapping
    where mapping.provider_id = 'test-oidc'
      and mapping.external_subject = account.username
      and mapping.active = true
  );

insert into auth_external_identity_mapping_events(event_id, mapping_id, action, actor, reason, occurred_at)
select
  md5('test-oidc-bootstrap-event:' || mapping.mapping_id::text)::uuid,
  mapping.mapping_id,
  'created',
  'migration-test-idp-bootstrap',
  'Development-only test identity mapping bootstrap.',
  mapping.created_at
from auth_external_identity_mappings mapping
where mapping.provider_id = 'test-oidc'
  and mapping.created_by = 'migration-test-idp-bootstrap'
  and not exists (
    select 1
    from auth_external_identity_mapping_events event
    where event.mapping_id = mapping.mapping_id
      and event.action = 'created'
  );
