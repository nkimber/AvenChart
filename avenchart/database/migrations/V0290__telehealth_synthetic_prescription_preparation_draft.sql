-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0016: an unsigned, unchecked, NON_PRODUCTION prescription-
-- preparation draft. These rows are not canonical prescriptions, medication
-- list entries, signatures, NCPDP transactions, or transmission requests.

create table if not exists telehealth_consultation_prescription_draft_versions (
  prescription_draft_version_id uuid primary key,
  consultation_id uuid not null references telehealth_consultation_contexts(consultation_id),
  encounter_id integer not null references encounters(encounter),
  version integer not null,
  rx_norm_code text not null references medication_vocabulary(rx_norm_code),
  drug_name_snapshot text not null,
  display_name_snapshot text not null,
  form_snapshot text not null,
  strength_snapshot text not null,
  route_snapshot text not null,
  controlled_substance_schedule_snapshot text,
  dose_amount numeric(10,2) not null,
  dose_unit text not null,
  frequency text not null,
  quantity_value numeric(10,2) not null,
  quantity_unit text not null,
  duration_days integer not null,
  refills integer not null,
  indication text not null,
  directions text not null,
  medication_list_reviewed boolean not null,
  allergy_list_reviewed boolean not null,
  adequate_evaluation_completed boolean not null,
  pharmacy_choice_version integer not null,
  catalog_source text not null,
  catalog_dataset_id text not null,
  catalog_dataset_version text not null,
  canonical_model_version text not null,
  intended_standard text not null,
  adapter_mode text not null,
  legal_effect boolean not null default false,
  safety_checked boolean not null default false,
  signed boolean not null default false,
  transmission_queued boolean not null default false,
  transmitted boolean not null default false,
  patient_delivered boolean not null default false,
  recorded_at timestamptz not null default now(),
  recorded_by_staff_id integer not null references staff(id),
  constraint uq_telehealth_prescription_draft_version unique (consultation_id,version),
  constraint fk_telehealth_prescription_draft_pharmacy_choice
    foreign key (consultation_id,pharmacy_choice_version)
    references telehealth_consultation_pharmacy_choice_versions(consultation_id,version),
  constraint chk_telehealth_prescription_draft_version check (version >= 1),
  constraint chk_telehealth_prescription_draft_catalog_snapshot check (
    controlled_substance_schedule_snapshot is null
    and length(trim(rx_norm_code)) between 1 and 64
    and length(trim(drug_name_snapshot)) between 1 and 160
    and length(trim(display_name_snapshot)) between 1 and 240
    and length(trim(form_snapshot)) between 1 and 80
    and length(trim(strength_snapshot)) between 1 and 80
    and length(trim(route_snapshot)) between 1 and 80),
  constraint chk_telehealth_prescription_draft_directions check (
    dose_amount > 0 and dose_amount <= 100000
    and length(trim(dose_unit)) between 1 and 40
    and length(trim(frequency)) between 1 and 160
    and quantity_value > 0 and quantity_value <= 100000
    and length(trim(quantity_unit)) between 1 and 40
    and duration_days between 1 and 365
    and refills between 0 and 5
    and length(trim(indication)) between 1 and 500
    and length(trim(directions)) between 1 and 1000),
  constraint chk_telehealth_prescription_draft_reviews check (
    medication_list_reviewed and allergy_list_reviewed and adequate_evaluation_completed),
  constraint chk_telehealth_prescription_draft_standard check (
    catalog_source='AvenChartSyntheticMedicationVocabulary'
    and length(trim(catalog_dataset_id)) between 1 and 128
    and length(trim(catalog_dataset_version)) between 1 and 64
    and canonical_model_version='AVENCHART_ERX_PREPARATION_V1'
    and intended_standard='NCPDP_SCRIPT_2017071'
    and adapter_mode='NON_PRODUCTION'),
  constraint chk_telehealth_prescription_draft_nonlegal check (
    not legal_effect and not safety_checked and not signed and not transmission_queued
    and not transmitted and not patient_delivered)
);

create table if not exists telehealth_consultation_prescription_draft_events (
  event_id uuid primary key,
  consultation_id uuid not null references telehealth_consultation_contexts(consultation_id),
  prescription_draft_version_id uuid not null
    references telehealth_consultation_prescription_draft_versions(prescription_draft_version_id),
  aggregate_version integer not null,
  action text not null,
  actor_type text not null,
  actor_id text not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  occurred_at timestamptz not null default now(),
  constraint uq_telehealth_prescription_draft_event_version unique (consultation_id,aggregate_version),
  constraint uq_telehealth_prescription_draft_event_idempotency unique (consultation_id,idempotency_key),
  constraint chk_telehealth_prescription_draft_event_version check (aggregate_version >= 1),
  constraint chk_telehealth_prescription_draft_event_action check (action in ('DraftRecorded','DraftRevised')),
  constraint chk_telehealth_prescription_draft_event_actor check (actor_type='physician'),
  constraint chk_telehealth_prescription_draft_event_actor_id check (length(trim(actor_id)) between 1 and 128),
  constraint chk_telehealth_prescription_draft_event_idempotency check (length(idempotency_key) between 8 and 128),
  constraint chk_telehealth_prescription_draft_event_fingerprint check (command_fingerprint ~ '^[0-9a-f]{64}$')
);

create or replace function enforce_telehealth_prescription_draft_catalog()
returns trigger
language plpgsql
as $$
declare
  catalog_row medication_vocabulary%rowtype;
begin
  select * into catalog_row
  from medication_vocabulary
  where rx_norm_code=new.rx_norm_code
  for key share;

  if not found or not catalog_row.active
     or nullif(trim(catalog_row.controlled_substance_schedule),'') is not null then
    raise exception using
      errcode='P0001',
      message='telehealth_prescription_catalog_item_not_permitted';
  end if;

  if new.drug_name_snapshot <> catalog_row.drug_name
     or new.display_name_snapshot <> catalog_row.display_name
     or new.form_snapshot <> catalog_row.form
     or new.strength_snapshot <> catalog_row.strength
     or new.route_snapshot <> catalog_row.route
     or new.controlled_substance_schedule_snapshot is not null then
    raise exception using
      errcode='P0001',
      message='telehealth_prescription_catalog_snapshot_mismatch';
  end if;

  return new;
end;
$$;

drop trigger if exists trg_telehealth_prescription_draft_catalog
  on telehealth_consultation_prescription_draft_versions;
create trigger trg_telehealth_prescription_draft_catalog
before insert on telehealth_consultation_prescription_draft_versions
for each row execute function enforce_telehealth_prescription_draft_catalog();

drop trigger if exists trg_telehealth_prescription_draft_versions_append_only
  on telehealth_consultation_prescription_draft_versions;
create trigger trg_telehealth_prescription_draft_versions_append_only
before update or delete on telehealth_consultation_prescription_draft_versions
for each row execute function reject_telehealth_evidence_mutation();

drop trigger if exists trg_telehealth_prescription_draft_events_append_only
  on telehealth_consultation_prescription_draft_events;
create trigger trg_telehealth_prescription_draft_events_append_only
before update or delete on telehealth_consultation_prescription_draft_events
for each row execute function reject_telehealth_evidence_mutation();
