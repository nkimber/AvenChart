alter table procedure_result_versions
    add column if not exists correction_actor text,
    add column if not exists correction_reason text,
    add column if not exists resulting_version integer;

create index if not exists idx_procedure_result_versions_correction_actor
    on procedure_result_versions (correction_actor, captured_at desc)
    where correction_actor is not null;
