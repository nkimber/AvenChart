-- Legacy drug_sales records a patient/encounter-linked dispensed item, charge,
-- sale date, and quantity movement against the selected inventory lot.
create table if not exists inventory_patient_sales (
  sale_id uuid primary key,
  lot_id integer not null references inventory_lots(lot_id),
  patient_id text not null references patients(canonical_id),
  encounter integer not null references encounters(encounter),
  sale_date date not null,
  quantity numeric(12,2) not null check (quantity > 0),
  fee numeric(12,2) not null check (fee >= 0),
  notes text,
  transaction_id uuid not null unique references inventory_transactions(transaction_id),
  sold_by text not null,
  sold_at timestamptz not null
);

create index if not exists idx_inventory_patient_sales_patient_encounter
  on inventory_patient_sales (patient_id, encounter, sale_date desc);
