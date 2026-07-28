-- Complete the protected medication-link lifecycle without deleting its
-- existing audit evidence. A locally retired vocabulary entry remains
-- historically resolvable but cannot be selected for a new mapping.
alter table medication_vocabulary
  add column if not exists active boolean not null default true;

alter table inventory_item_medication_link_audits
  add column if not exists reason text;

alter table inventory_item_medication_link_audits
  drop constraint if exists inventory_item_medication_link_audits_unlink_reason_check;

alter table inventory_item_medication_link_audits
  add constraint inventory_item_medication_link_audits_unlink_reason_check
  check ((action <> 'unlinked') or (reason is not null and length(btrim(reason)) > 0));
