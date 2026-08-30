alter table telehealth_clinician_shifts
  add column if not exists end_idempotency_key text,
  add column if not exists end_fingerprint character(64);

alter table telehealth_clinician_shifts
  add constraint chk_telehealth_shift_end_command check (
    (status='Ended' and ended_at is not null and ((end_idempotency_key is null and end_fingerprint is null) or (end_idempotency_key is not null and end_fingerprint is not null)))
    or (status<>'Ended' and ended_at is null and end_idempotency_key is null and end_fingerprint is null));

create unique index if not exists uq_telehealth_shift_end_idempotency
  on telehealth_clinician_shifts(practice_id,clinician_staff_id,end_idempotency_key)
  where end_idempotency_key is not null;
