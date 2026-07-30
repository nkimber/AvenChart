-- Local dispatch-lease recovery for the generic outbox. It does not establish
-- a partner retry policy, a worker lease policy, or production operations.
alter table integration_outbox_events
  drop constraint if exists integration_outbox_events_action_check;

alter table integration_outbox_events
  add constraint integration_outbox_events_action_check
  check (action in ('quarantined', 'requeued', 'lease-recovered'));
