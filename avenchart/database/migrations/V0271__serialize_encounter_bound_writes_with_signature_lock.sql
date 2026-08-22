-- ENCOUNTER-LOCK-03: a locking signature and any ordinary encounter-bound
-- write must serialize through the encounter row.  Application prechecks are
-- retained for friendly validation, but this trigger is the final database
-- boundary that closes the check-then-write race.

create or replace function avenchart_reject_locked_encounter_mutation()
returns trigger
language plpgsql
as $$
declare
  mutation_row jsonb;
  target_encounter integer;
  is_locked boolean;
begin
  mutation_row := case when tg_op = 'DELETE' then to_jsonb(old) else to_jsonb(new) end;
  target_encounter := nullif(mutation_row ->> 'encounter', '')::integer;

  -- Laboratory and track-child tables inherit encounter ownership through
  -- their governed parent.  Resolve it here so those writers cannot bypass
  -- the same signature boundary merely because the child table has no direct
  -- encounter column.
  if target_encounter is null and tg_table_name in ('lab_specimens', 'lab_reports') then
    select orders.encounter
    into target_encounter
    from lab_orders orders
    where orders.id = (mutation_row ->> 'order_id')::integer;
  elsif target_encounter is null and tg_table_name = 'lab_results' then
    select orders.encounter
    into target_encounter
    from lab_reports report
    inner join lab_orders orders on orders.id = report.order_id
    where report.id = (mutation_row ->> 'report_id')::integer;
  elsif target_encounter is null and tg_table_name = 'encounter_track_readings' then
    select track_record.encounter
    into target_encounter
    from encounter_track_records track_record
    where track_record.record_id = (mutation_row ->> 'record_id')::uuid;
  end if;

  -- Some historical document rows are not encounter-bound.  They remain
  -- governed by their own patient/document lifecycle and do not participate
  -- in the encounter signature boundary.
  if target_encounter is null then
    return case when tg_op = 'DELETE' then old else new end;
  end if;

  -- SignAsync holds this same row FOR UPDATE until its signature and content
  -- manifest commit.  KEY SHARE makes the write either precede that snapshot
  -- or wait until the new locking signature is visible and be rejected.
  select exists (
    select 1
    from encounter_signatures signature
    where signature.encounter = current_encounter.encounter
      and signature.is_lock)
  into is_locked
  from encounters current_encounter
  where current_encounter.encounter = target_encounter
  for key share;

  if coalesce(is_locked, false) then
    raise exception using
      errcode = 'P0001',
      message = 'encounter_locked',
      detail = 'This encounter has a locking signature. Add clinical changes through the governed amendment workflow.';
  end if;

  return case when tg_op = 'DELETE' then old else new end;
end;
$$;

do $$
declare
  target_table text;
begin
  foreach target_table in array array[
    'encounters',
    'vitals',
    'clinical_notes',
    'encounter_layout_form_records',
    'encounter_clinical_alert_acknowledgments',
    'lab_orders',
    'lab_specimens',
    'lab_reports',
    'lab_results',
    'billing',
    'claims',
    'payment_activities',
    'patient_documents',
    'encounter_track_records',
    'encounter_track_readings',
    'inventory_patient_sales',
    'inventory_patient_sale_batches'
  ]
  loop
    execute format(
      'drop trigger if exists %I on %I',
      'trg_' || target_table || '_signature_lock_guard',
      target_table);
    execute format(
      'create trigger %I before insert or update or delete on %I for each row execute function avenchart_reject_locked_encounter_mutation()',
      'trg_' || target_table || '_signature_lock_guard',
      target_table);
  end loop;
end;
$$;

comment on function avenchart_reject_locked_encounter_mutation() is
  'Serializes ordinary encounter-bound writes with locking encounter signatures and rejects post-sign mutations.';
