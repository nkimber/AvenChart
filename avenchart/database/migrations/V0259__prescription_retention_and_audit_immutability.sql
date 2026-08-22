-- CLN-11: A prescription is a longitudinal clinical record. Retain both the
-- prescription and its evidence trail; deactivation is the supported way to
-- end therapy. The tightly scoped fixture exception keeps automated smoke
-- data removable without making production evidence mutable.
create or replace function avenchart_prevent_prescription_retention_violation()
returns trigger
language plpgsql
as $$
begin
  if tg_op = 'DELETE' and old.patient_id like 'TMP-PAT-REG-%' then
    return old;
  end if;

  raise exception using
    errcode = '55000',
    message = 'Prescriptions and prescription audit events are retained and cannot be altered or deleted.';
end;
$$;

drop trigger if exists trg_prescription_audit_events_immutable on prescription_audit_events;
create trigger trg_prescription_audit_events_immutable
before update or delete on prescription_audit_events
for each row execute function avenchart_prevent_prescription_retention_violation();

drop trigger if exists trg_prescriptions_retained on prescriptions;
create trigger trg_prescriptions_retained
before delete on prescriptions
for each row execute function avenchart_prevent_prescription_retention_violation();
