create table if not exists batch_communication_campaigns(
  id uuid primary key, process_type text not null check(process_type in ('csv','email','phone')), filter_json jsonb not null,
  email_sender text, email_subject text, email_body text, recipient_count integer not null, created_at timestamptz not null default now()
);
create table if not exists batch_communication_recipients(
  campaign_id uuid not null references batch_communication_campaigns(id) on delete cascade, patient_id text not null references patients(canonical_id),
  display_name text not null, email text, phone_home text, phone_cell text, postal_code text, next_appointment_date date, last_appointment_date date, last_visit_date date,
  rendered_subject text, rendered_body text, primary key(campaign_id,patient_id)
);
