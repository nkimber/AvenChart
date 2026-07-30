alter table referrals
  add column if not exists workflow_version integer not null default 1,
  add column if not exists assigned_to text,
  add column if not exists due_at timestamptz,
  add column if not exists created_by text;

update referrals
set assigned_to = coalesce(assigned_to, created_by, 'admin'),
    created_by = coalesce(created_by, 'legacy')
where assigned_to is null
   or created_by is null;

create index if not exists ix_referrals_patient_work_queue
  on referrals(patient_id, status, due_at, requested_at desc);
