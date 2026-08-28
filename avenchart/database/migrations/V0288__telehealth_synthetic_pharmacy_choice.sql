-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- Disabled-by-default synthetic pharmacy-choice workspace authorized by
-- TH-DEC-0013. These records are not prescriptions, transmission instructions,
-- pharmacy acknowledgments, dispense evidence, or claims.

create table if not exists telehealth_patient_pharmacy_preferences (
  preference_id uuid primary key,
  practice_id text not null,
  facility_id integer not null references facilities(id),
  patient_id text not null references patients(canonical_id),
  directory_entry_id uuid not null,
  directory_source text not null,
  directory_version text not null,
  preference_status text not null,
  supersedes_preference_id uuid references telehealth_patient_pharmacy_preferences(preference_id),
  recorded_at timestamptz not null default now(),
  recorded_by_actor_id text not null,
  constraint chk_telehealth_pharmacy_preference_status check (preference_status in ('Added','Removed')),
  constraint chk_telehealth_pharmacy_preference_actor check (length(trim(recorded_by_actor_id)) between 1 and 128),
  constraint chk_telehealth_pharmacy_preference_source check (length(trim(directory_source)) between 1 and 128),
  constraint chk_telehealth_pharmacy_preference_version check (length(trim(directory_version)) between 1 and 64)
);

create index if not exists ix_telehealth_patient_pharmacy_preferences
  on telehealth_patient_pharmacy_preferences(practice_id,facility_id,patient_id,directory_entry_id,recorded_at desc,preference_id desc);

create table if not exists telehealth_consultation_pharmacy_choice_versions (
  choice_version_id uuid primary key,
  consultation_id uuid not null references telehealth_consultation_contexts(consultation_id),
  version integer not null,
  directory_entry_id uuid not null,
  directory_source text not null,
  directory_version text not null,
  pharmacy_name text not null,
  address_line1 text not null,
  address_line2 text,
  city text not null,
  state_code character(2) not null,
  postal_code text not null,
  country_code character(2) not null,
  phone text not null,
  ncpdp_id text,
  npi text,
  electronic_routing_capability text not null,
  choice_basis text not null,
  patient_choice_confirmed boolean not null,
  selected_at timestamptz not null default now(),
  selected_by_staff_id integer not null references staff(id),
  constraint uq_telehealth_pharmacy_choice_version unique (consultation_id,version),
  constraint chk_telehealth_pharmacy_choice_version check (version >= 1),
  constraint chk_telehealth_pharmacy_choice_state check (state_code in ('GA','CA','FL')),
  constraint chk_telehealth_pharmacy_choice_country check (country_code='US'),
  constraint chk_telehealth_pharmacy_choice_basis check (choice_basis='PatientConfirmedDuringConsultation'),
  constraint chk_telehealth_pharmacy_choice_confirmed check (patient_choice_confirmed=true),
  constraint chk_telehealth_pharmacy_choice_routing check (electronic_routing_capability='NON_PRODUCTION_ONLY'),
  constraint chk_telehealth_pharmacy_choice_snapshot check (
    length(trim(directory_source)) between 1 and 128
    and length(trim(directory_version)) between 1 and 64
    and length(trim(pharmacy_name)) between 1 and 160
    and length(trim(address_line1)) between 1 and 160
    and length(trim(city)) between 1 and 100
    and length(trim(postal_code)) between 5 and 10
    and length(trim(phone)) between 7 and 32)
);

create table if not exists telehealth_consultation_pharmacy_choice_events (
  event_id uuid primary key,
  consultation_id uuid not null references telehealth_consultation_contexts(consultation_id),
  choice_version_id uuid not null references telehealth_consultation_pharmacy_choice_versions(choice_version_id),
  aggregate_version integer not null,
  action text not null,
  actor_type text not null,
  actor_id text not null,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  occurred_at timestamptz not null default now(),
  constraint uq_telehealth_pharmacy_choice_event_version unique (consultation_id,aggregate_version),
  constraint uq_telehealth_pharmacy_choice_event_idempotency unique (consultation_id,idempotency_key),
  constraint chk_telehealth_pharmacy_choice_event_version check (aggregate_version >= 1),
  constraint chk_telehealth_pharmacy_choice_event_action check (action in ('DestinationRecorded','DestinationChanged')),
  constraint chk_telehealth_pharmacy_choice_event_actor check (actor_type='physician'),
  constraint chk_telehealth_pharmacy_choice_event_actor_id check (length(trim(actor_id)) between 1 and 128),
  constraint chk_telehealth_pharmacy_choice_event_idempotency check (length(idempotency_key) between 8 and 128)
);

drop trigger if exists trg_telehealth_patient_pharmacy_preferences_append_only
  on telehealth_patient_pharmacy_preferences;
create trigger trg_telehealth_patient_pharmacy_preferences_append_only
before update or delete on telehealth_patient_pharmacy_preferences
for each row execute function reject_telehealth_evidence_mutation();

drop trigger if exists trg_telehealth_pharmacy_choice_versions_append_only
  on telehealth_consultation_pharmacy_choice_versions;
create trigger trg_telehealth_pharmacy_choice_versions_append_only
before update or delete on telehealth_consultation_pharmacy_choice_versions
for each row execute function reject_telehealth_evidence_mutation();

drop trigger if exists trg_telehealth_consultation_pharmacy_choice_events_append_only
  on telehealth_consultation_pharmacy_choice_events;
create trigger trg_telehealth_consultation_pharmacy_choice_events_append_only
before update or delete on telehealth_consultation_pharmacy_choice_events
for each row execute function reject_telehealth_evidence_mutation();
