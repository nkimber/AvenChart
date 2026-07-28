alter table practice_setting_change_requests
  add column if not exists baseline_value text,
  add column if not exists baseline_updated_at timestamptz,
  add column if not exists version integer not null default 0;

update practice_setting_change_requests request
set baseline_value = setting.setting_value,
    baseline_updated_at = setting.updated_at
from practice_settings setting
where setting.setting_key = request.setting_key
  and (
    request.baseline_value is null
    or request.baseline_updated_at is null
  );

alter table practice_setting_change_requests
  alter column baseline_value set not null,
  alter column baseline_updated_at set not null;

create index if not exists ix_practice_setting_change_requests_status_updated
  on practice_setting_change_requests(status, updated_at desc, request_id desc);
