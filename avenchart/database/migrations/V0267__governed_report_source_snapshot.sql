-- REP-07: A queued governed report must render the bounded data result that
-- was authorized at request acceptance, not re-read mutable source tables.
-- The source copy is transient and cleared at each terminal lifecycle state;
-- the retained artifact keeps the existing definition retention behavior.

alter table saved_report_runs
  add column if not exists source_snapshot_content text;

alter table saved_report_runs
  add column if not exists source_snapshot_checksum text;

alter table saved_report_runs
  add constraint saved_report_runs_source_snapshot_pair_check
  check (
    (source_snapshot_content is null and source_snapshot_checksum is null)
    or (
      source_snapshot_content is not null
      and btrim(source_snapshot_checksum) <> ''
    )
  );
