-- Adds explicit optimistic-concurrency tokens for EF-managed patient aggregates.

alter table patient_record_requests
  add column if not exists row_version bigint not null default 1;

alter table patient_sdoh_assessments
  add column if not exists row_version bigint not null default 1;
