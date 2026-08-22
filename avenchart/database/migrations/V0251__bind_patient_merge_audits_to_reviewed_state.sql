-- Binds a reviewed merge plan to the patient-administration revisions and
-- supported-record populations that were visible when the reviewer approved it.

alter table patient_merge_audit_plans
  add column if not exists target_administration_version bigint not null default 1,
  add column if not exists source_administration_version bigint not null default 1,
  add column if not exists target_record_fingerprint text not null default '',
  add column if not exists source_record_fingerprint text not null default '';
