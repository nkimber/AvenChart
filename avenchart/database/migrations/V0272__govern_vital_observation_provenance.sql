-- Vital observations are append-only clinical evidence. Record who entered each
-- observation and explicitly link any corrective entry to the observation it replaces.
alter table vitals
    add column if not exists recorded_at timestamp,
    add column if not exists recorded_by text,
    add column if not exists correction_of_vital_id integer,
    add column if not exists correction_reason text;

-- V0271's live encounter-lock trigger is intentionally active before this
-- migration runs. This one-time historical provenance backfill does not
-- change a clinical observation's value or encounter association, so perform
-- it with that single trigger disabled inside the migrator transaction and
-- restore the live guard before the migration commits.
alter table vitals disable trigger trg_vitals_signature_lock_guard;

update vitals
set recorded_at = coalesce(recorded_at, vital_datetime, current_timestamp),
    recorded_by = coalesce(nullif(btrim(recorded_by), ''), 'migration')
where recorded_at is null
   or recorded_by is null
   or btrim(recorded_by) = '';

alter table vitals enable trigger trg_vitals_signature_lock_guard;

alter table vitals
    alter column recorded_at set not null,
    alter column recorded_by set not null;

do $$
begin
    if not exists (
        select 1 from pg_constraint where conname = 'fk_vitals_correction_of_vital') then
        alter table vitals
            add constraint fk_vitals_correction_of_vital
            foreign key (correction_of_vital_id) references vitals(id) on delete restrict;
    end if;

    if not exists (
        select 1 from pg_constraint where conname = 'ck_vitals_observation_present') then
        alter table vitals
            add constraint ck_vitals_observation_present
            check (
                bps is not null
                or bpd is not null
                or weight is not null
                or height is not null
                or temperature is not null
                or pulse is not null
                or respiration is not null
                or oxygen_saturation is not null) not valid;
    end if;

    if not exists (
        select 1 from pg_constraint where conname = 'ck_vitals_physical_bounds') then
        alter table vitals
            add constraint ck_vitals_physical_bounds
            check (
                (bps is null or bps between 1 and 400)
                and (bpd is null or bpd between 1 and 300)
                and (bps is null or bpd is null or bpd < bps)
                and (weight is null or weight between 0.1 and 2000)
                and (height is null or height between 0.1 and 120)
                and (temperature is null or temperature between 1 and 150)
                and (pulse is null or pulse between 1 and 400)
                and (respiration is null or respiration between 1 and 200)
                and (oxygen_saturation is null or oxygen_saturation between 0 and 100)) not valid;
    end if;

    if not exists (
        select 1 from pg_constraint where conname = 'ck_vitals_correction_reason') then
        alter table vitals
            add constraint ck_vitals_correction_reason
            check (
                (correction_of_vital_id is null and correction_reason is null)
                or (correction_of_vital_id is not null
                    and correction_reason is not null
                    and char_length(btrim(correction_reason)) between 3 and 500)) not valid;
    end if;
end $$;

create index if not exists idx_vitals_encounter_observation_history
    on vitals (encounter, vital_datetime desc, id desc);
