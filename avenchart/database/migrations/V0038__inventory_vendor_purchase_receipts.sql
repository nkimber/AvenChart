-- Local purchasing foundation based on legacy add_edit_lot.php Purchase/Receipt:
-- a named vendor, receiving facility, item/lot, expiry, quantity, unit cost,
-- reference, and required notes create one immutable receipt and ledger entry.
create table if not exists inventory_vendors (
  vendor_id uuid primary key,
  name text not null,
  contact_name text,
  phone text,
  email text,
  active boolean not null default true,
  created_at timestamptz not null default now(),
  created_by text not null
);

create unique index if not exists ux_inventory_vendors_name_lower
  on inventory_vendors (lower(name));

create table if not exists inventory_purchase_receipts (
  receipt_id uuid primary key,
  vendor_id uuid not null references inventory_vendors(vendor_id),
  facility_id integer not null references facilities(id),
  reference_number text,
  received_at timestamptz not null,
  received_by text not null,
  notes text not null,
  created_at timestamptz not null default now(),
  unique (vendor_id, reference_number)
);

alter table inventory_transactions
  add column if not exists receipt_id uuid references inventory_purchase_receipts(receipt_id);

create index if not exists idx_inventory_purchase_receipts_facility_received
  on inventory_purchase_receipts (facility_id, received_at desc);

create index if not exists idx_inventory_transactions_receipt
  on inventory_transactions (receipt_id)
  where receipt_id is not null;
