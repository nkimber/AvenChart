-- Acknowledgements must be scoped to the exact rule revision and the allergy
-- state that was reviewed. A later rule edit or any allergy-list mutation must
-- make the old acknowledgement inapplicable rather than silently suppressing
-- the banner.
create table if not exists patient_allergy_review_states (
  pid integer primary key,
  state_version integer not null check (state_version > 0),
  updated_at timestamptz not null
);

insert into patient_allergy_review_states(pid,state_version,updated_at)
select distinct legacy_pid,1,now()
from patients
on conflict(pid) do nothing;

create or replace function avenchart_initialize_allergy_review_state()
returns trigger
language plpgsql
as $$
begin
  insert into patient_allergy_review_states(pid,state_version,updated_at)
  values(new.legacy_pid,1,now())
  on conflict(pid) do nothing;
  return new;
end;
$$;

drop trigger if exists trg_patients_initialize_allergy_review_state on patients;
create trigger trg_patients_initialize_allergy_review_state
after insert on patients
for each row execute function avenchart_initialize_allergy_review_state();

create or replace function avenchart_advance_allergy_review_state()
returns trigger
language plpgsql
as $$
begin
  if tg_op = 'INSERT' then
    if coalesce(new.type, '') = 'allergy' and new.pid is not null then
      insert into patient_allergy_review_states(pid,state_version,updated_at)
      values(new.pid,1,now())
      on conflict(pid) do update set state_version=patient_allergy_review_states.state_version+1,updated_at=excluded.updated_at;
    end if;
    return new;
  end if;

  if tg_op = 'DELETE' then
    if coalesce(old.type, '') = 'allergy' and old.pid is not null then
      insert into patient_allergy_review_states(pid,state_version,updated_at)
      values(old.pid,1,now())
      on conflict(pid) do update set state_version=patient_allergy_review_states.state_version+1,updated_at=excluded.updated_at;
    end if;
    return old;
  end if;

  -- An update can move an allergy between patients. Advance both patient
  -- states in that case so neither patient's prior review is reusable.
  if coalesce(old.type, '') = 'allergy' and old.pid is not null then
    insert into patient_allergy_review_states(pid,state_version,updated_at)
    values(old.pid,1,now())
    on conflict(pid) do update set state_version=patient_allergy_review_states.state_version+1,updated_at=excluded.updated_at;
  end if;
  if coalesce(new.type, '') = 'allergy'
     and new.pid is not null
     and (coalesce(old.type, '') <> 'allergy' or new.pid is distinct from old.pid) then
    insert into patient_allergy_review_states(pid,state_version,updated_at)
    values(new.pid,1,now())
    on conflict(pid) do update set state_version=patient_allergy_review_states.state_version+1,updated_at=excluded.updated_at;
  end if;
  return new;
end;
$$;

drop trigger if exists trg_allergies_advance_review_state on allergies;
create trigger trg_allergies_advance_review_state
after insert or update or delete on allergies
for each row execute function avenchart_advance_allergy_review_state();

alter table encounter_clinical_alert_acknowledgments
  add column if not exists rule_revision_id bigint references clinical_alert_rule_revisions(revision_id) on delete restrict,
  add column if not exists allergy_state_version integer;

update encounter_clinical_alert_acknowledgments acknowledgement
set rule_revision_id = (
      select revision.revision_id
      from clinical_alert_rule_revisions revision
      where revision.rule_key = acknowledgement.rule_key
      order by revision.revision_id desc
      limit 1),
    allergy_state_version = coalesce((
      select state.state_version
      from encounters encounter
      join patient_allergy_review_states state on state.pid = encounter.pid
      where encounter.encounter = acknowledgement.encounter), 1);

alter table encounter_clinical_alert_acknowledgments
  alter column rule_revision_id set not null,
  alter column allergy_state_version set not null;

alter table encounter_clinical_alert_acknowledgments
  drop constraint if exists encounter_clinical_alert_acknowledgments_pkey;

alter table encounter_clinical_alert_acknowledgments
  add primary key (encounter,rule_key,rule_revision_id,allergy_state_version);

drop index if exists ix_encounter_clinical_alert_acknowledgments_open;
create index if not exists ix_encounter_clinical_alert_acknowledgments_open
  on encounter_clinical_alert_acknowledgments(encounter, rule_key, rule_revision_id, allergy_state_version)
  where reopened_at is null;
