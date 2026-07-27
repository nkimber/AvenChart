-- One legacy drug sale can allocate across several eligible lots.  Keep the
-- clinical/financial sale as a parent and retain each lot debit as a child.
create table if not exists inventory_patient_sale_batches (
  sale_batch_id uuid primary key,
  item_id integer not null references inventory_items(item_id),
  patient_id text not null references patients(canonical_id),
  encounter integer not null references encounters(encounter),
  sale_date date not null,
  quantity numeric(12,2) not null check (quantity > 0),
  fee numeric(12,2) not null check (fee >= 0),
  notes text,
  sold_by text not null,
  sold_at timestamptz not null
);

alter table inventory_patient_sales
  add column if not exists sale_batch_id uuid references inventory_patient_sale_batches(sale_batch_id);

create index if not exists idx_inventory_patient_sale_batches_patient_encounter
  on inventory_patient_sale_batches (patient_id, encounter, sale_date desc);
create index if not exists idx_inventory_patient_sales_batch
  on inventory_patient_sales (sale_batch_id) where sale_batch_id is not null;
