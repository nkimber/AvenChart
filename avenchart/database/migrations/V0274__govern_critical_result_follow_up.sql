-- Critical-result acknowledgement is not completion of clinical follow-up.  This
-- lifecycle records accepted ownership, an explicitly selected due time,
-- communication and clinical-action evidence, escalation/coverage transfer, and
-- closure.  It deliberately does not prescribe a clinical response interval:
-- policy owners must set the due time for each result.
create table if not exists critical_lab_result_follow_ups (
    result_id integer primary key references lab_results(id) on delete restrict,
    status text not null default 'open'
        check (status in ('open', 'accepted', 'actioned', 'closed')),
    version integer not null default 1 check (version > 0),
    result_content_version integer not null check (result_content_version > 0),
    owner_username text,
    due_at timestamp,
    accepted_by text,
    accepted_at timestamp,
    closed_by text,
    closed_at timestamp,
    closure_reason text,
    check ((owner_username is null) = (due_at is null)),
    check (
        (status = 'open'
            and owner_username is null
            and due_at is null
            and accepted_by is null
            and accepted_at is null
            and closed_by is null
            and closed_at is null
            and closure_reason is null)
        or (status in ('accepted', 'actioned')
            and owner_username is not null
            and due_at is not null
            and accepted_by is not null
            and accepted_at is not null
            and closed_by is null
            and closed_at is null
            and closure_reason is null)
        or (status = 'closed'
            and owner_username is not null
            and due_at is not null
            and accepted_by is not null
            and accepted_at is not null
            and closed_by is not null
            and closed_at is not null
            and closure_reason is not null
            and char_length(btrim(closure_reason)) between 3 and 500))
);

create table if not exists critical_lab_result_follow_up_events (
    event_id bigserial primary key,
    result_id integer not null references lab_results(id) on delete restrict,
    action text not null check (action in (
        'accepted',
        'ownership-transferred',
        'communication-recorded',
        'clinical-action-recorded',
        'escalated',
        'closed',
        'reopened-after-result-correction')),
    previous_status text,
    current_status text not null check (current_status in ('open', 'accepted', 'actioned', 'closed')),
    prior_version integer not null check (prior_version > 0),
    resulting_version integer not null check (resulting_version = prior_version + 1),
    result_content_version integer not null check (result_content_version > 0),
    actor text not null,
    owner_username text,
    due_at timestamp,
    recipient text,
    communication_channel text,
    communication_outcome text,
    detail text not null check (char_length(btrim(detail)) between 3 and 500),
    occurred_at timestamp not null default current_timestamp
);

create index if not exists idx_critical_lab_result_follow_ups_worklist
    on critical_lab_result_follow_ups (status, due_at, result_id)
    where status <> 'closed';

create index if not exists idx_critical_lab_result_follow_up_events_result
    on critical_lab_result_follow_up_events (result_id, occurred_at desc, event_id desc);

-- Existing acknowledgement-only records were never evidence of a responsible
-- owner, due date, communication, action, or closure.  Surface them as open
-- work instead of inferring facts that were not captured historically.
insert into critical_lab_result_follow_ups (result_id, status, version, result_content_version)
select result.id,
       'open',
       1,
       coalesce(acknowledgement.result_content_version, coalesce((
           select max(version.version_no)
           from procedure_result_versions version
           where version.result_id = result.id), 0) + 1)
from lab_results result
left join critical_lab_result_acknowledgements acknowledgement on acknowledgement.result_id = result.id
where lower(coalesce(result.abnormal, '')) in ('c', 'critical', 'panic', 'hh', 'll')
on conflict (result_id) do nothing;

-- Follow-up events are append-only evidence; corrections are represented by a
-- later event, never by changing or deleting prior history.
create or replace function avenchart_reject_critical_follow_up_event_mutation()
returns trigger
language plpgsql
as $$
begin
    raise exception 'critical_lab_result_follow_up_events are append-only';
end;
$$;

drop trigger if exists trg_critical_follow_up_events_append_only
    on critical_lab_result_follow_up_events;

create trigger trg_critical_follow_up_events_append_only
before update or delete on critical_lab_result_follow_up_events
for each row execute function avenchart_reject_critical_follow_up_event_mutation();
