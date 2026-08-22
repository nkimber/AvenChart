-- SEC-02: Every protected staff request has an explicit, server-authorized
-- purpose of use and facility context. Grants are principal-based so the
-- boundary can survive a future external identity-provider mapping.
create table if not exists auth_principal_facility_grants (
  username text not null references auth_accounts(username) on delete cascade,
  facility_id integer not null references facilities(id) on delete restrict,
  is_default boolean not null default false,
  active boolean not null default true,
  granted_at timestamptz not null default now(),
  granted_by text not null default 'migration-bootstrap',
  updated_at timestamptz not null default now(),
  updated_by text not null default 'migration-bootstrap',
  primary key (username, facility_id)
);

create unique index if not exists ux_auth_principal_facility_grants_default
  on auth_principal_facility_grants(username)
  where is_default and active;

create table if not exists auth_principal_purpose_of_use_grants (
  username text not null references auth_accounts(username) on delete cascade,
  purpose_of_use text not null check (purpose_of_use in ('treatment', 'payment', 'healthcare-operations')),
  active boolean not null default true,
  granted_at timestamptz not null default now(),
  granted_by text not null default 'migration-bootstrap',
  updated_at timestamptz not null default now(),
  updated_by text not null default 'migration-bootstrap',
  primary key (username, purpose_of_use)
);

create table if not exists auth_access_context_grant_events (
  event_id uuid primary key,
  occurred_at timestamptz not null default now(),
  username text not null references auth_accounts(username) on delete restrict,
  action text not null check (action in ('granted', 'updated')),
  facility_ids integer[] not null,
  default_facility_id integer not null references facilities(id) on delete restrict,
  purposes text[] not null,
  changed_by text not null
);

create index if not exists ix_auth_access_context_grant_events_username_time
  on auth_access_context_grant_events(username, occurred_at desc, event_id desc);

-- Preserve each linked staff member's existing home facility. Service and
-- administrator identities without a staff row start at the practice's first
-- active facility; an administrator can grant additional facilities through
-- the governed access-context endpoint.
insert into auth_principal_facility_grants(username,facility_id,is_default,active,granted_at,granted_by,updated_at,updated_by)
select account.username,staff.facility_id,true,true,now(),'migration-bootstrap',now(),'migration-bootstrap'
from auth_accounts account
join staff on staff.id=account.staff_id
join facilities on facilities.id=staff.facility_id and facilities.inactive=false
where account.active=true and staff.active=true
on conflict(username,facility_id) do nothing;

insert into auth_principal_facility_grants(username,facility_id,is_default,active,granted_at,granted_by,updated_at,updated_by)
select account.username,facility.id,true,true,now(),'migration-bootstrap',now(),'migration-bootstrap'
from auth_accounts account
cross join lateral (
  select id
  from facilities
  where inactive=false
  order by id
  limit 1
) facility
where account.active=true
  and not exists(
    select 1
    from auth_principal_facility_grants facility_grant
    where facility_grant.username=account.username
      and facility_grant.active=true
  )
on conflict(username,facility_id) do nothing;

-- Local bootstrap grants are constrained further by the existing ACL matrix:
-- treatment is available to authenticated care workflows, while payment and
-- healthcare operations require their matching capability (or super admin).
insert into auth_principal_purpose_of_use_grants(username,purpose_of_use,active,granted_at,granted_by,updated_at,updated_by)
select account.username,'treatment',true,now(),'migration-bootstrap',now(),'migration-bootstrap'
from auth_accounts account
where account.active=true
on conflict(username,purpose_of_use) do nothing;

insert into auth_principal_purpose_of_use_grants(username,purpose_of_use,active,granted_at,granted_by,updated_at,updated_by)
select account.username,'payment',true,now(),'migration-bootstrap',now(),'migration-bootstrap'
from auth_accounts account
where account.active=true
  and (
    lower(account.username)='admin'
    or exists (
      select 1
      from access_user_memberships membership
      join access_group_permissions permission on permission.group_value=membership.group_value
      where lower(membership.user_value)=lower(account.username)
        and (
          (permission.section_value='acct' and permission.permission_value='bill')
          or (permission.section_value='admin' and permission.permission_value='super')
        )
    )
  )
on conflict(username,purpose_of_use) do nothing;

insert into auth_principal_purpose_of_use_grants(username,purpose_of_use,active,granted_at,granted_by,updated_at,updated_by)
select account.username,'healthcare-operations',true,now(),'migration-bootstrap',now(),'migration-bootstrap'
from auth_accounts account
where account.active=true
  and (
    lower(account.username)='admin'
    or exists (
      select 1
      from access_user_memberships membership
      join access_group_permissions permission on permission.group_value=membership.group_value
      where lower(membership.user_value)=lower(account.username)
        and (
          permission.section_value='admin'
          or permission.section_value='inventory'
          or (permission.section_value='patients' and permission.permission_value='pat_rep')
        )
    )
  )
on conflict(username,purpose_of_use) do nothing;

alter table phi_access_audit_events
  add column if not exists facility_id integer references facilities(id) on delete set null,
  add column if not exists facility_code text,
  add column if not exists purpose_of_use text;

create index if not exists ix_phi_access_audit_context_time
  on phi_access_audit_events(facility_id, purpose_of_use, occurred_at desc);
