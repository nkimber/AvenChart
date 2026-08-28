-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- Disabled-by-default synthetic consultation wrap-up handoff authorized by
-- TH-DEC-0012. This does not complete an encounter, release the physician,
-- sign documentation, create a disposition, or create downstream work.

alter table telehealth_requests
  drop constraint if exists chk_telehealth_requests_status;
alter table telehealth_requests
  add constraint chk_telehealth_requests_status
  check (status in ('Draft','LocationConfirmed','Intake','Verification','OperationalReview','Redirected','Queued','Reserved','Connecting','InConsultation','WrapUp'));

alter table telehealth_clinician_shifts
  drop constraint if exists chk_telehealth_shift_status;
alter table telehealth_clinician_shifts
  add constraint chk_telehealth_shift_status check (status in ('Active','Busy','WrapUp','Ended'));

drop index if exists uq_telehealth_active_shift_clinician;
create unique index uq_telehealth_active_shift_clinician
  on telehealth_clinician_shifts(practice_id, clinician_staff_id)
  where status in ('Active','Busy','WrapUp');

alter table telehealth_consultation_contexts
  add column if not exists media_ended_at timestamptz;
alter table telehealth_consultation_contexts
  drop constraint if exists chk_telehealth_consultation_status;
alter table telehealth_consultation_contexts
  add constraint chk_telehealth_consultation_status
  check (status in ('Started','MediaEnded'));
alter table telehealth_consultation_contexts
  drop constraint if exists chk_telehealth_consultation_media_end;
alter table telehealth_consultation_contexts
  add constraint chk_telehealth_consultation_media_end
  check ((status='Started' and media_ended_at is null)
      or (status='MediaEnded' and media_ended_at is not null));

create or replace function govern_telehealth_consultation_context_mutation()
returns trigger language plpgsql as $$
begin
  if tg_op='DELETE' then
    raise exception 'telehealth consultation start evidence cannot be deleted';
  end if;

  if old.status <> 'Started'
     or new.status <> 'MediaEnded'
     or new.version <> old.version + 1
     or old.media_ended_at is not null
     or new.media_ended_at is null
     or (to_jsonb(new) - array['status','version','media_ended_at'])
        is distinct from
        (to_jsonb(old) - array['status','version','media_ended_at']) then
    raise exception 'telehealth consultation start evidence is immutable outside the governed wrap-up transition';
  end if;

  return new;
end $$;

drop trigger if exists trg_telehealth_consultation_contexts_append_only
  on telehealth_consultation_contexts;
create trigger trg_telehealth_consultation_contexts_append_only
before update or delete on telehealth_consultation_contexts
for each row execute function govern_telehealth_consultation_context_mutation();
