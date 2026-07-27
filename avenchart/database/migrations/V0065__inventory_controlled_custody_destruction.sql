alter table inventory_controlled_custody_events
  drop constraint if exists inventory_controlled_custody_events_action_check;

alter table inventory_controlled_custody_events
  add constraint inventory_controlled_custody_events_action_check
  check (action in ('receipt', 'transfer', 'dispense', 'administration', 'return', 'waste', 'destruction', 'correction'));

alter table inventory_controlled_custody_events
  drop constraint if exists inventory_controlled_custody_events_check;

alter table inventory_controlled_custody_events
  add constraint inventory_controlled_custody_events_check
  check ((action = 'receipt' and source_location_id is null and destination_location_id is not null and quantity_delta > 0)
      or (action = 'transfer' and source_location_id is not null and destination_location_id is not null and source_location_id <> destination_location_id and counterparty_lot_id is not null and quantity_delta < 0)
      or (action in ('dispense', 'administration', 'waste', 'destruction') and source_location_id is not null and destination_location_id is null and quantity_delta < 0)
      or (action = 'return' and source_location_id is null and destination_location_id is not null and quantity_delta > 0 and related_event_id is not null)
      or (action = 'correction' and related_event_id is not null and ((source_location_id is not null and destination_location_id is null and quantity_delta < 0) or (source_location_id is null and destination_location_id is not null and quantity_delta > 0))));
