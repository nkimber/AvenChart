-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- POC-only plain-text transcript. It is available only during a synthetic
-- in-consultation lifecycle and deliberately creates no media, notification,
-- delivery, clinical, billing, claim, or external communication capability.

create table if not exists telehealth_consultation_transcript_messages (
  message_id uuid primary key,
  consultation_id uuid not null references telehealth_consultation_contexts(consultation_id),
  request_id uuid not null references telehealth_requests(request_id),
  practice_id text not null,
  patient_id text not null references patients(canonical_id),
  physician_staff_id integer not null references staff(id),
  sender_role text not null,
  body text not null,
  synthetic_data_confirmed boolean not null,
  legal_effect boolean not null default false,
  sent_at timestamptz not null default now(),
  constraint chk_telehealth_transcript_sender check (sender_role in ('patient','physician')),
  constraint chk_telehealth_transcript_body check (length(trim(body)) between 1 and 1000 and body !~ '[[:cntrl:]]'),
  constraint chk_telehealth_transcript_synthetic check (synthetic_data_confirmed and legal_effect=false)
);

create index if not exists ix_telehealth_transcript_consultation_sent
  on telehealth_consultation_transcript_messages(consultation_id,sent_at,message_id);

create index if not exists ix_telehealth_transcript_patient_request
  on telehealth_consultation_transcript_messages(practice_id,patient_id,request_id,sent_at);

drop trigger if exists trg_telehealth_transcript_messages_append_only on telehealth_consultation_transcript_messages;
create trigger trg_telehealth_transcript_messages_append_only
before update or delete on telehealth_consultation_transcript_messages
for each row execute function reject_telehealth_evidence_mutation();
