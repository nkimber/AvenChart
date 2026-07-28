-- VAL-02: transfer-created layers retain the exact source application rather
-- than pretending that an inter-facility movement is a purchase receipt.
alter table inventory_cost_layers
  alter column receipt_id drop not null;

alter table inventory_cost_layers
  drop constraint if exists inventory_cost_layers_source_transaction_id_key;

alter table inventory_cost_layers
  add column if not exists transfer_id uuid,
  add column if not exists origin_application_id uuid references inventory_cost_layer_applications(application_id);

create unique index if not exists ux_inventory_cost_layers_transfer_origin_application
  on inventory_cost_layers(origin_application_id)
  where origin_application_id is not null;

create index if not exists ix_inventory_cost_layers_transfer
  on inventory_cost_layers(transfer_id, lot_id)
  where transfer_id is not null;
