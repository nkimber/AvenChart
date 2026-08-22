-- LAB-CRIT-02: bind a local acknowledgement to the exact result-content
-- version reviewed. A later correction reopens the acknowledgement rather
-- than implying that the changed content was already reviewed.

alter table critical_lab_result_acknowledgements
  add column if not exists result_content_version integer;

update critical_lab_result_acknowledgements acknowledgement
set result_content_version=coalesce((
  select max(version.version_no)
  from procedure_result_versions version
  where version.result_id=acknowledgement.result_id), 0)+1
where acknowledgement.result_content_version is null;

alter table critical_lab_result_acknowledgements
  alter column result_content_version set not null;

alter table critical_lab_result_acknowledgements
  add constraint critical_lab_result_acknowledgements_content_version_check
  check (result_content_version > 0);

alter table critical_lab_result_acknowledgement_events
  add column if not exists result_content_version integer;

-- Historical events predate content-version evidence and remain explicitly
-- unbound rather than assigning a version that cannot be proven.
