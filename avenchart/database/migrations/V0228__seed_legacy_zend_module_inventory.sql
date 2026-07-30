-- These are registered legacy Zend modules, not target enablement decisions.
insert into module_catalog(module_key,display_name,category,status,description,updated_at,updated_by) values
  ('CARECOORDINATION','Care Coordination','interoperability','decision-required','Legacy Zend module `Carecoordination` is registered and enabled in the pinned baseline (mod_active=1, mod_ui_active=0, sql_run=1). Target replacement, data, integration, and governance evidence remain required.',now(),'legacy-module-inventory-seed'),
  ('CCR','Continuity of Care Record','interoperability','decision-required','Legacy Zend module `Ccr` is registered and enabled in the pinned baseline (mod_active=1, mod_ui_active=0, sql_run=1). Target replacement, data, integration, and governance evidence remain required.',now(),'legacy-module-inventory-seed'),
  ('DOCUMENTS_MODULE','Documents Module','records','decision-required','Legacy Zend module `Documents` is registered and enabled in the pinned baseline (mod_active=1, mod_ui_active=0, sql_run=1). Existing target document workflows do not establish module parity.',now(),'legacy-module-inventory-seed'),
  ('IMMUNIZATION_MODULE','Immunization Module','clinical-workflow','decision-required','Legacy Zend module `Immunization` is registered and enabled in the pinned baseline (mod_active=1, mod_ui_active=0, sql_run=1). Existing target immunization records do not establish module parity.',now(),'legacy-module-inventory-seed'),
  ('SYNDROMIC_SURVEILLANCE','Syndromic Surveillance','public-health','decision-required','Legacy Zend module `Syndromicsurveillance` is registered and enabled in the pinned baseline (mod_active=1, mod_ui_active=0, sql_run=1). Target reporting and jurisdictional acceptance remain required.',now(),'legacy-module-inventory-seed'),
  ('CODE_TYPES','Code Types','configuration','decision-required','Legacy Zend module `CodeTypes` is registered but disabled in the pinned baseline (mod_active=0, mod_ui_active=0, sql_run=0). Existing target coding catalogs do not establish module parity.',now(),'legacy-module-inventory-seed'),
  ('PATIENT_FILTER','Patient Filter','patient-chart','decision-required','Legacy Zend module `PatientFilter` is registered but disabled in the pinned baseline (mod_active=0, mod_ui_active=0, sql_run=0). Target replacement and owner decision remain required.',now(),'legacy-module-inventory-seed'),
  ('PATIENT_VALIDATION','Patient Validation','patient-chart','decision-required','Legacy Zend module `Patientvalidation` is registered but disabled in the pinned baseline (mod_active=0, mod_ui_active=0, sql_run=0). Target replacement and owner decision remain required.',now(),'legacy-module-inventory-seed'),
  ('PRESCRIPTION_TEMPLATES','Prescription Templates','prescribing','decision-required','Legacy Zend module `PrescriptionTemplates` is registered but disabled in the pinned baseline (mod_active=0, mod_ui_active=0, sql_run=0). Target replacement and owner decision remain required.',now(),'legacy-module-inventory-seed')
on conflict(module_key) do nothing;

insert into module_catalog_revisions(module_key,display_name,category,status,description,action,occurred_at,username)
select module_key,display_name,category,status,description,'baseline',updated_at,updated_by
from module_catalog module
where module.module_key in ('CARECOORDINATION','CCR','DOCUMENTS_MODULE','IMMUNIZATION_MODULE','SYNDROMIC_SURVEILLANCE','CODE_TYPES','PATIENT_FILTER','PATIENT_VALIDATION','PRESCRIPTION_TEMPLATES')
  and not exists(select 1 from module_catalog_revisions revision where revision.module_key=module.module_key);
