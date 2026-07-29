alter table practice_setting_revisions
  drop constraint if exists practice_setting_revisions_action_check;

alter table practice_setting_revisions
  add constraint practice_setting_revisions_action_check
  check (action in ('baseline', 'updated', 'rolled-back', 'activated', 'package-imported', 'package-rolled-back'));
