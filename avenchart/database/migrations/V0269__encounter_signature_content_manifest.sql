-- A signature must identify the clinical state that was attested. Historical
-- signatures deliberately remain unbound: assigning a fabricated snapshot to
-- them would misrepresent the evidence available at the time of signature.
alter table encounter_signatures
  add column if not exists content_revision text,
  add column if not exists content_checksum text,
  add column if not exists content_manifest jsonb;

alter table encounter_signatures
  drop constraint if exists encounter_signatures_content_manifest_pair_check;

alter table encounter_signatures
  add constraint encounter_signatures_content_manifest_pair_check
  check (
    (content_revision is null and content_checksum is null and content_manifest is null)
    or (
      content_revision = 'encounter-signature-content-v1'
      and content_checksum ~ '^[0-9a-f]{64}$'
      and content_manifest is not null
    )
  );

create index if not exists idx_encounter_signatures_content_checksum
  on encounter_signatures (content_checksum)
  where content_checksum is not null;
