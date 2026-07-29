alter table configuration_package_import_requests
  add column if not exists kind text not null default 'import',
  add column if not exists source_request_id uuid references configuration_package_import_requests(request_id) on delete restrict;

alter table configuration_package_import_requests
  drop constraint if exists configuration_package_import_requests_kind_check;

alter table configuration_package_import_requests
  add constraint configuration_package_import_requests_kind_check
  check (kind in ('import', 'rollback'));

create unique index if not exists ux_configuration_package_import_requests_rollback_source
  on configuration_package_import_requests(source_request_id)
  where kind = 'rollback';
