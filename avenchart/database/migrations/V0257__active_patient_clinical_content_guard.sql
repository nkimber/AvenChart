-- CLN-09: New clinical content must be anchored to the active, canonical
-- patient record. Application checks provide useful request errors; this
-- trigger is the final concurrency-safe guard for all writers.
create or replace function avenchart_require_active_patient_for_new_clinical_content()
returns trigger
language plpgsql
as $$
declare
  patient_record record;
begin
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
      message = 'Clinical content must reference an existing patient.';
  end if;

  if patient_record.merged_into_patient_id is not null
     or patient_record.lifecycle_status <> 'active'
     or patient_record.deceased_date is not null then
    raise exception using
      errcode = '23514',
      message = 'New clinical content is not allowed for a merged, retired, or deceased patient.';
  end if;

  return new;
end;
$$;

drop trigger if exists trg_allergies_require_active_patient_for_new_content on allergies;
create trigger trg_allergies_require_active_patient_for_new_content
before insert on allergies
for each row execute function avenchart_require_active_patient_for_new_clinical_content();

drop trigger if exists trg_problems_require_active_patient_for_new_content on problems;
create trigger trg_problems_require_active_patient_for_new_content
before insert on problems
for each row execute function avenchart_require_active_patient_for_new_clinical_content();

drop trigger if exists trg_medications_require_active_patient_for_new_content on medications;
create trigger trg_medications_require_active_patient_for_new_content
before insert on medications
for each row execute function avenchart_require_active_patient_for_new_clinical_content();

drop trigger if exists trg_immunizations_require_active_patient_for_new_content on immunizations;
create trigger trg_immunizations_require_active_patient_for_new_content
before insert on immunizations
for each row execute function avenchart_require_active_patient_for_new_clinical_content();

drop trigger if exists trg_prescriptions_require_active_patient_for_new_content on prescriptions;
create trigger trg_prescriptions_require_active_patient_for_new_content
before insert on prescriptions
for each row execute function avenchart_require_active_patient_for_new_clinical_content();
