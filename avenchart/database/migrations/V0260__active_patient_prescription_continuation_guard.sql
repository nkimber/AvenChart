-- CLN-12: Refills, order edits, and pharmacy routes are clinical
-- continuations. They are allowed only while the linked patient is current,
-- active, and not deceased. A deactivation remains available to close an
-- existing medication order after a patient lifecycle transition.
create or replace function avenchart_require_active_patient_for_prescription_continuation()
returns trigger
language plpgsql
as $$
declare
  patient_record record;
begin
  if new.active = 0 and old.active = 1 then
    return new;
  end if;

  select
      merged_into_patient_id,
      coalesce(lower(lifecycle_status), 'active') as lifecycle_status,
      deceased_date
  into patient_record
  from patients
  where canonical_id = new.patient_id
  for key share;

  if not found then
    raise exception using
      errcode = '23503',
      message = 'Prescription continuation must reference an existing patient.';
  end if;

  if patient_record.merged_into_patient_id is not null
     or patient_record.lifecycle_status <> 'active'
     or patient_record.deceased_date is not null then
    raise exception using
      errcode = '23514',
      message = 'Prescription continuation is not permitted for a merged, retired, or deceased patient.';
  end if;

  return new;
end;
$$;

drop trigger if exists trg_prescriptions_require_active_patient_for_continuation on prescriptions;
create trigger trg_prescriptions_require_active_patient_for_continuation
before update on prescriptions
for each row execute function avenchart_require_active_patient_for_prescription_continuation();
