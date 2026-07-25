alter table patient_sdoh_assessments add column if not exists disability_status text;
alter table patient_sdoh_assessments add column if not exists disability_status_notes text;
alter table patient_sdoh_assessments add column if not exists disability_scale jsonb not null default '{}'::jsonb;
