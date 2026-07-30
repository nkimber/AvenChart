-- Source presence is not evidence of runtime enablement, replacement, or retirement.
-- These rows reconcile the pinned legacy custom-module directories with the governed target catalog.
insert into module_catalog(module_key,display_name,category,status,description,updated_at,updated_by) values
  ('CLAIMREV_CONNECT','Claim Revolution Connect','revenue-cycle','partner-gated','Legacy custom-module source `oe-module-claimrev-connect` is present (Composer package claimrevolution/oe-module-claimrev-connect). Runtime enablement is unknown; approved clearinghouse/vendor contract is required.',now(),'legacy-module-inventory-seed'),
  ('COMLINK_TELEHEALTH','Comlink Telehealth','telehealth','partner-gated','Legacy custom-module source `oe-module-comlink-telehealth` is present (Comlink Telehealth Module v2.0.0). Runtime enablement is unknown; approved telehealth provider contract is required.',now(),'legacy-module-inventory-seed'),
  ('DASHBOARD_CONTEXT','Dashboard Context Manager','clinical-workflow','decision-required','Legacy custom-module source `oe-module-dashboard-context` is present (Legacy EHR Dashboard Context Manager). Runtime enablement is unknown; accountable owner must select a replacement, retirement, or funded implementation.',now(),'legacy-module-inventory-seed'),
  ('DORN','Diagnostic Ordering Result Network','laboratory','partner-gated','Legacy custom-module source `oe-module-dorn` is present (Diagnostic Ordering Result Network). Runtime enablement is unknown; approved laboratory-network contract is required.',now(),'legacy-module-inventory-seed'),
  ('EHI_EXPORTER','EHI Exporter','interoperability','decision-required','Legacy custom-module source `oe-module-ehi-exporter` is present (Composer package legacy-ehr/oe-module-ehi-exporter). Runtime enablement is unknown; accountable owner must select the export/retention implementation.',now(),'legacy-module-inventory-seed'),
  ('FAX_SMS','Fax and SMS','communications','partner-gated','Legacy custom-module source `oe-module-faxsms` is present (Composer package legacy-ehr/oe-module-faxsms). Runtime enablement is unknown; approved delivery-provider contract is required.',now(),'legacy-module-inventory-seed'),
  ('PRIOR_AUTHORIZATIONS','Advanced Prior Authorization','payer-workflow','decision-required','Legacy custom-module source `oe-module-prior-authorizations` is present. Runtime enablement is unknown; the local authorization documentation workflow does not establish module parity.',now(),'legacy-module-inventory-seed'),
  ('WENO','Weno EZ Integration','prescribing','partner-gated','Legacy custom-module source `oe-module-weno` is present (Weno EZ Integration). Runtime enablement is unknown; approved e-prescribing vendor credentials and contract are required.',now(),'legacy-module-inventory-seed')
on conflict(module_key) do nothing;

insert into module_catalog_revisions(module_key,display_name,category,status,description,action,occurred_at,username)
select module_key,display_name,category,status,description,'baseline',updated_at,updated_by
from module_catalog module
where module.module_key in ('CLAIMREV_CONNECT','COMLINK_TELEHEALTH','DASHBOARD_CONTEXT','DORN','EHI_EXPORTER','FAX_SMS','PRIOR_AUTHORIZATIONS','WENO')
  and not exists(select 1 from module_catalog_revisions revision where revision.module_key=module.module_key);
