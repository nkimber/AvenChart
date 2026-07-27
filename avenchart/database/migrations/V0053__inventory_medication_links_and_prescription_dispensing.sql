-- Inventory products and clinical medications are intentionally linked by
-- RXCUI instead of product-name matching. This keeps dispensing decisions
-- deterministic and auditable when multiple similar product names exist.
create table if not exists inventory_item_medication_links (
  item_id integer primary key references inventory_items(item_id),
  rx_norm_code text not null unique references medication_vocabulary(rx_norm_code),
  linked_by text not null,
  linked_at timestamptz not null
);

create table if not exists inventory_item_medication_link_audits (
  audit_id uuid primary key,
  item_id integer not null references inventory_items(item_id),
  prior_rx_norm_code text references medication_vocabulary(rx_norm_code),
  new_rx_norm_code text references medication_vocabulary(rx_norm_code),
  action text not null check (action in ('linked', 'updated', 'unlinked')),
  changed_by text not null,
  changed_at timestamptz not null
);

alter table inventory_patient_sales
  add column if not exists prescription_id text references prescriptions(id);

create index if not exists idx_inventory_medication_link_audits_item
  on inventory_item_medication_link_audits (item_id, changed_at desc);
create index if not exists idx_inventory_patient_sales_prescription
  on inventory_patient_sales (prescription_id, sale_date desc)
  where prescription_id is not null;
