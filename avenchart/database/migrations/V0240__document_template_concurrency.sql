alter table document_templates
  add column if not exists row_version bigint not null default 1;
