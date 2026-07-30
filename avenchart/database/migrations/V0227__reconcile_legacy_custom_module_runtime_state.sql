-- The pinned legacy baseline's `modules` registry records each listed custom module as
-- registered but disabled: mod_active=0, mod_ui_active=0, and sql_run=0.
update module_catalog
set description=case module_key
  when 'CLAIMREV_CONNECT' then 'Legacy custom-module source `oe-module-claimrev-connect` is registered in the pinned baseline as ClaimRev Clearinghouse Connector (mod_active=0, mod_ui_active=0, sql_run=0). It is disabled; approved clearinghouse/vendor contract is required before any target enablement.'
  when 'COMLINK_TELEHEALTH' then 'Legacy custom-module source `oe-module-comlink-telehealth` is registered in the pinned baseline as Comlink Telehealth Module v2.0.0 (mod_active=0, mod_ui_active=0, sql_run=0). It is disabled; approved telehealth provider contract is required before any target enablement.'
  when 'DASHBOARD_CONTEXT' then 'Legacy custom-module source `oe-module-dashboard-context` is registered in the pinned baseline as Dashboard Context Service v1.0.0 (mod_active=0, mod_ui_active=0, sql_run=0). It is disabled; accountable owner must select a replacement, retirement, or funded implementation.'
  when 'DORN' then 'Legacy custom-module source `oe-module-dorn` is registered in the pinned baseline as Diagnostic Ordering Result Network (DORN) (mod_active=0, mod_ui_active=0, sql_run=0). It is disabled; approved laboratory-network contract is required before any target enablement.'
  when 'EHI_EXPORTER' then 'Legacy custom-module source `oe-module-ehi-exporter` is registered in the pinned baseline as Electronic Health Information Exporter v1.0.1 (mod_active=0, mod_ui_active=0, sql_run=0). It is disabled; accountable owner must select the export/retention implementation.'
  when 'FAX_SMS' then 'Legacy custom-module source `oe-module-faxsms` is registered in the pinned baseline as Fax SMS Email Voice Module (mod_active=0, mod_ui_active=0, sql_run=0). It is disabled; approved delivery-provider contract is required before any target enablement.'
  when 'PRIOR_AUTHORIZATIONS' then 'Legacy custom-module source `oe-module-prior-authorizations` is registered in the pinned baseline as Advanced Prior Auth (mod_active=0, mod_ui_active=0, sql_run=0). It is disabled; the local authorization documentation workflow does not establish module parity.'
  when 'WENO' then 'Legacy custom-module source `oe-module-weno` is registered in the pinned baseline as Weno EZ Integration eRx Module (mod_active=0, mod_ui_active=0, sql_run=0). It is disabled; approved e-prescribing vendor credentials and contract are required before any target enablement.'
end,
updated_at=now(),
updated_by='legacy-module-runtime-reconciliation'
where module_key in ('CLAIMREV_CONNECT','COMLINK_TELEHEALTH','DASHBOARD_CONTEXT','DORN','EHI_EXPORTER','FAX_SMS','PRIOR_AUTHORIZATIONS','WENO');

insert into module_catalog_revisions(module_key,display_name,category,status,description,action,occurred_at,username)
select module_key,display_name,category,status,description,'updated',updated_at,updated_by
from module_catalog module
where module.module_key in ('CLAIMREV_CONNECT','COMLINK_TELEHEALTH','DASHBOARD_CONTEXT','DORN','EHI_EXPORTER','FAX_SMS','PRIOR_AUTHORIZATIONS','WENO')
  and module.description like '%mod_active=0, mod_ui_active=0, sql_run=0%'
  and not exists(
    select 1
    from module_catalog_revisions revision
    where revision.module_key=module.module_key
      and revision.description=module.description
  );
