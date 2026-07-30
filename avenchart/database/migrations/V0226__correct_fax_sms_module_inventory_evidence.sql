-- V0014 seeded FAX_SMS as a partner-gated placeholder. Preserve the source-discovery fact
-- in a forward correction instead of rewriting the applied baseline migration.
update module_catalog
set description='Legacy custom-module source `oe-module-faxsms` is present (Composer package legacy-ehr/oe-module-faxsms). Runtime enablement is unknown; approved delivery-provider contract is required.',
    updated_at=now(),
    updated_by='legacy-module-inventory-seed'
where module_key='FAX_SMS'
  and description='Requires approved delivery provider.';

insert into module_catalog_revisions(module_key,display_name,category,status,description,action,occurred_at,username)
select module_key,display_name,category,status,description,'updated',updated_at,updated_by
from module_catalog module
where module.module_key='FAX_SMS'
  and module.description like 'Legacy custom-module source `oe-module-faxsms`%'
  and not exists(
    select 1
    from module_catalog_revisions revision
    where revision.module_key=module.module_key
      and revision.description=module.description
  );
