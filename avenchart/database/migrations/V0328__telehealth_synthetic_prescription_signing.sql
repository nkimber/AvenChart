-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- TH-DEC-0061: one immutable, safety-gated, signed NON_PRODUCTION
-- prescription record and an uncertified prepared-only NCPDP SCRIPT seam.
-- No pharmacy or network is contacted and the record has no legal effect.

create table if not exists telehealth_consultation_prescription_orders (
  order_id uuid primary key,
  consultation_id uuid not null unique references telehealth_consultation_contexts(consultation_id),
  prescription_id text not null unique references prescriptions(id),
  prescription_draft_version_id uuid not null unique
    references telehealth_consultation_prescription_draft_versions(prescription_draft_version_id),
  draft_version integer not null,
  pharmacy_choice_version integer not null,
  drug_name_snapshot text not null,
  rx_norm_code_snapshot text not null,
  directions_snapshot text not null,
  pharmacy_name_snapshot text not null,
  pharmacy_state_code_snapshot text not null,
  safety_outcome text not null,
  safety_ruleset_version text not null,
  active_medication_count integer not null,
  active_allergy_count integer not null,
  signed_at timestamptz not null,
  signed_by_staff_id integer not null references staff(id),
  content_hash character(64) not null,
  adapter_mode text not null,
  canonical_model_version text not null,
  target_standard text not null,
  transition_standard text not null,
  transaction_type text not null,
  transmission_state text not null,
  certified boolean not null default false,
  external_destination_contacted boolean not null default false,
  legal_effect boolean not null default false,
  patient_delivered boolean not null default false,
  idempotency_key text not null,
  command_fingerprint character(64) not null,
  constraint uq_telehealth_prescription_order_idempotency unique (consultation_id,idempotency_key),
  constraint fk_telehealth_prescription_order_draft_version
    foreign key (consultation_id,draft_version)
    references telehealth_consultation_prescription_draft_versions(consultation_id,version),
  constraint fk_telehealth_prescription_order_pharmacy_choice
    foreign key (consultation_id,pharmacy_choice_version)
    references telehealth_consultation_pharmacy_choice_versions(consultation_id,version),
  constraint chk_telehealth_prescription_order_snapshots check (
    draft_version >= 1
    and length(trim(drug_name_snapshot)) between 1 and 160
    and length(trim(rx_norm_code_snapshot)) between 1 and 64
    and length(trim(directions_snapshot)) between 1 and 1000
    and length(trim(pharmacy_name_snapshot)) between 1 and 240
    and pharmacy_state_code_snapshot in ('GA','CA','FL')),
  constraint chk_telehealth_prescription_order_safety check (
    safety_outcome='SYNTHETIC_ZERO_LIST_GATE_PASSED'
    and safety_ruleset_version='AVENCHART_SYNTHETIC_ZERO_LIST_GATE_V1'
    and active_medication_count=0
    and active_allergy_count=0),
  constraint chk_telehealth_prescription_order_integrity check (
    content_hash ~ '^[0-9a-f]{64}$'
    and command_fingerprint ~ '^[0-9a-f]{64}$'
    and length(idempotency_key) between 8 and 128),
  constraint chk_telehealth_prescription_order_stub check (
    adapter_mode='NON_PRODUCTION'
    and canonical_model_version='AVENCHART_ERX_CANONICAL_V1'
    and target_standard='NCPDP_SCRIPT_2023011'
    and transition_standard='NCPDP_SCRIPT_2017071_THROUGH_2027_12_31'
    and transaction_type='NewRx'
    and transmission_state='PreparedOnly'
    and not certified
    and not external_destination_contacted
    and not legal_effect
    and not patient_delivered)
);

create index if not exists idx_telehealth_prescription_orders_prescriber
  on telehealth_consultation_prescription_orders(signed_by_staff_id,signed_at desc);

drop trigger if exists trg_telehealth_prescription_orders_append_only
  on telehealth_consultation_prescription_orders;
create trigger trg_telehealth_prescription_orders_append_only
before update or delete on telehealth_consultation_prescription_orders
for each row execute function reject_telehealth_evidence_mutation();

create or replace function reject_signed_telehealth_prescription_mutation()
returns trigger
language plpgsql
as $$
begin
  if exists (
    select 1 from telehealth_consultation_prescription_orders
    where prescription_id=old.id
  ) then
    raise exception using
      errcode='P0001',
      message='signed_telehealth_prescription_is_immutable';
  end if;
  return case when tg_op='DELETE' then old else new end;
end;
$$;

drop trigger if exists trg_prescriptions_reject_signed_telehealth_mutation on prescriptions;
create trigger trg_prescriptions_reject_signed_telehealth_mutation
before update or delete on prescriptions
for each row execute function reject_signed_telehealth_prescription_mutation();
