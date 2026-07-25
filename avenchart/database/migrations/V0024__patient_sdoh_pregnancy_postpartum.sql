alter table patient_sdoh_assessments add column if not exists pregnancy_status text;
alter table patient_sdoh_assessments add column if not exists pregnancy_edd date;
alter table patient_sdoh_assessments add column if not exists pregnancy_intent text;
alter table patient_sdoh_assessments add column if not exists postpartum_status text;
alter table patient_sdoh_assessments add column if not exists postpartum_end date;
