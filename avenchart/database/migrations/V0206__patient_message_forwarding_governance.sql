alter table message_assignment_events
  drop constraint if exists message_assignment_events_action_check;

alter table message_assignment_events
  add constraint message_assignment_events_action_check
  check (action in ('assigned', 'reassigned', 'unassigned', 'forwarded'));
