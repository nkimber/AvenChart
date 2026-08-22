-- CLN-10: Allergies, problems, and immunizations are longitudinal records.
-- Preserve every API-managed lifecycle transition with actor, reason, and a
-- post-mutation state snapshot. The database denies later rewriting of the
-- evidence even when a writer bypasses the API.
create table if not exists clinical_list_audit_events (
  event_id uuid primary key,
  resource_type text not null check (resource_type in ('allergy', 'problem', 'immunization')),
  resource_id text not null,
  patient_id text not null references patients(canonical_id) on delete restrict,
  action text not null check (action in ('created', 'deactivated', 'entered-in-error')),
  actor text not null check (length(btrim(actor)) between 1 and 120),
  reason text,
  state_json jsonb not null,
  occurred_at timestamptz not null default now()
);

create index if not exists idx_clinical_list_audit_events_resource_occurred
  on clinical_list_audit_events(resource_type, resource_id, occurred_at desc, event_id desc);

create index if not exists idx_clinical_list_audit_events_patient_occurred
  on clinical_list_audit_events(patient_id, occurred_at desc, event_id desc);

create or replace function avenchart_prevent_clinical_list_audit_mutation()
returns trigger
language plpgsql
as $$
begin
  -- The only supported hard-delete path is the existing TMP-PAT-REG fixture
  -- cleanup route. It must be able to remove test data atomically; real
  -- patient evidence is never eligible for that exception.
  if tg_op = 'DELETE' and old.patient_id like 'TMP-PAT-REG-%' then
    return old;
  end if;

  raise exception using
    errcode = '55000',
    message = 'Clinical-list audit events are immutable and cannot be altered or deleted.';
end;
$$;

drop trigger if exists trg_clinical_list_audit_events_immutable on clinical_list_audit_events;
create trigger trg_clinical_list_audit_events_immutable
before update or delete on clinical_list_audit_events
for each row execute function avenchart_prevent_clinical_list_audit_mutation();
