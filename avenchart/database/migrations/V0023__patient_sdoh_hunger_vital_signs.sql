alter table patient_sdoh_assessments add column if not exists hunger_q1 text;
alter table patient_sdoh_assessments add column if not exists hunger_q2 text;
alter table patient_sdoh_assessments add column if not exists hunger_score integer not null default 0;
