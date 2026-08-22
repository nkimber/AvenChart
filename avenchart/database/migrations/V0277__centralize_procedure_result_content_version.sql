-- The current content version is one greater than the latest retained prior
-- version. It is a derived value, not an allocator: every mutation that uses
-- it holds the corresponding lab_results row lock before changing data.
create or replace function avenchart_current_procedure_result_content_version(p_result_id integer)
returns integer
language sql
stable
as $$
  select (coalesce(max(version_no), 0) + 1)::integer
  from procedure_result_versions
  where result_id = p_result_id;
$$;
