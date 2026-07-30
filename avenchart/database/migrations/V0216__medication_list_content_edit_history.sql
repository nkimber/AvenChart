alter table medication_list_lifecycle_events
    drop constraint if exists medication_list_lifecycle_events_action_check;

alter table medication_list_lifecycle_events
    add constraint medication_list_lifecycle_events_action_check
    check (action in ('created', 'deactivated', 'restored', 'edited'));
