-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- Disabled-by-default synthetic closure only. It must not complete the
-- appointment, create delivery, financial, claim, integration, or external work.

alter table telehealth_requests
  drop constraint if exists chk_telehealth_requests_status;
alter table telehealth_requests
  add constraint chk_telehealth_requests_status
  check (status in ('Draft','LocationConfirmed','SafetyScreening','EmergencyRedirected','InPersonRecommended','Unsupported','ClinicalReview','Intake','Verification','OperationalReview','Redirected','Queued','Reserved','Connecting','InConsultation','WrapUp','Closed'));

alter table telehealth_consultation_contexts
  add column if not exists closed_at timestamptz;
alter table telehealth_consultation_contexts
  drop constraint if exists chk_telehealth_consultation_status;
alter table telehealth_consultation_contexts
  add constraint chk_telehealth_consultation_status check (status in ('Started','MediaEnded','Closed'));
alter table telehealth_consultation_contexts
  drop constraint if exists chk_telehealth_consultation_media_end;
alter table telehealth_consultation_contexts
  drop constraint if exists chk_telehealth_consultation_closure;
alter table telehealth_consultation_contexts
  add constraint chk_telehealth_consultation_closure check (
    (status='Started' and media_ended_at is null and closed_at is null)
    or (status='MediaEnded' and media_ended_at is not null and closed_at is null)
    or (status='Closed' and media_ended_at is not null and closed_at is not null and closed_at>=media_ended_at));

create or replace function govern_telehealth_consultation_context_mutation()
returns trigger language plpgsql as $$
begin
  if tg_op='DELETE' then raise exception 'telehealth consultation evidence cannot be deleted'; end if;
  if old.status='Started' and new.status='MediaEnded' and new.version=old.version+1
     and old.media_ended_at is null and new.media_ended_at is not null and new.closed_at is null
     and (to_jsonb(new) - array['status','version','media_ended_at']) is not distinct from (to_jsonb(old) - array['status','version','media_ended_at']) then return new; end if;
  if old.status='MediaEnded' and new.status='Closed' and new.version=old.version+1
     and old.closed_at is null and new.closed_at is not null and new.media_ended_at=old.media_ended_at
     and (to_jsonb(new) - array['status','version','closed_at']) is not distinct from (to_jsonb(old) - array['status','version','closed_at']) then return new; end if;
  raise exception 'telehealth consultation evidence is immutable outside governed wrap-up or synthetic closure';
end $$;
