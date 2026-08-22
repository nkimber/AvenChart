-- LAB-REVIEW-03: a laboratory-review event must identify the exact report and
-- result content that was available when the event was recorded.  The trigger
-- is deliberately database-owned because local clinical edits and external
-- FHIR intake both create review events.

alter table lab_report_review_events
  add column if not exists content_revision text,
  add column if not exists content_checksum text,
  add column if not exists content_manifest jsonb;

alter table lab_report_review_events
  add constraint lab_report_review_events_content_binding_check
  check (
    (content_revision is null and content_checksum is null and content_manifest is null)
    or (
      content_revision = 'lab-report-review-content-v1'
      and content_checksum ~ '^[0-9a-f]{64}$'
      and content_manifest is not null
    )
  );

create or replace function avenchart_capture_lab_report_review_content()
returns trigger
language plpgsql
as $$
declare
  snapshot jsonb;
begin
  -- Historical events deliberately remain unbound.  New evidence is always
  -- derived from the database state, never trusted from an application caller.
  if tg_op = 'UPDATE' then
    if new.content_revision is distinct from old.content_revision
       or new.content_checksum is distinct from old.content_checksum
       or new.content_manifest is distinct from old.content_manifest then
      raise exception 'lab report review content evidence is immutable';
    end if;
    return new;
  end if;

  select jsonb_build_object(
    'revision', 'lab-report-review-content-v1',
    'report', jsonb_build_object(
      'id', report.id,
      'orderId', report.order_id,
      'specimenId', report.specimen_id,
      'dateCollected', report.date_collected,
      'reportDate', report.report_date,
      'specimenNumber', report.specimen_number,
      'status', report.status,
      'reviewStatus', report.review_status,
      'reviewedBy', report.reviewed_by,
      'reviewedAt', report.reviewed_at,
      'reviewVersion', report.review_version,
      'notes', report.notes),
    'results', coalesce((
      select jsonb_agg(jsonb_build_object(
        'id', result.id,
        'contentVersion', coalesce((
          select max(version.version_no)
          from procedure_result_versions version
          where version.result_id = result.id), 0) + 1,
        'code', result.code,
        'text', result.text,
        'units', result.units,
        'result', result.result,
        'range', result.range,
        'abnormal', result.abnormal,
        'resultDate', result.result_date,
        'status', result.result_status)
        order by result.id)
      from lab_results result
      where result.report_id = report.id), '[]'::jsonb))
  into snapshot
  from lab_reports report
  where report.id = new.report_id;

  if snapshot is null then
    raise exception 'lab report % was not found while recording review evidence', new.report_id;
  end if;

  new.content_revision := 'lab-report-review-content-v1';
  new.content_manifest := snapshot;
  new.content_checksum := encode(sha256(convert_to(snapshot::text, 'utf8')), 'hex');
  return new;
end;
$$;

drop trigger if exists trg_lab_report_review_event_content on lab_report_review_events;
create trigger trg_lab_report_review_event_content
before insert or update on lab_report_review_events
for each row execute function avenchart_capture_lab_report_review_content();

comment on function avenchart_capture_lab_report_review_content() is
  'Captures immutable report/result evidence for each post-governance laboratory review event.';
