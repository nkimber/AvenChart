alter table clinical_notes
    add column if not exists version integer,
    add column if not exists supersedes_note_id integer,
    add column if not exists saved_at timestamp,
    add column if not exists saved_by text,
    add column if not exists evidence_source text;

with ranked as (
    select
        id,
        row_number() over (
            partition by encounter
            order by note_datetime, id
        )::integer as version,
        lag(id) over (
            partition by encounter
            order by note_datetime, id
        ) as supersedes_note_id
    from clinical_notes
    where encounter is not null
)
update clinical_notes note
set
    version = ranked.version,
    supersedes_note_id = ranked.supersedes_note_id,
    saved_at = coalesce(note.saved_at, note.note_datetime),
    evidence_source = coalesce(note.evidence_source, 'migration-backfill')
from ranked
where note.id = ranked.id;

update clinical_notes
set
    version = coalesce(version, 1),
    saved_at = coalesce(saved_at, note_datetime),
    evidence_source = coalesce(evidence_source, 'migration-backfill')
where
    version is null
    or saved_at is null
    or evidence_source is null;

alter table clinical_notes
    alter column version set default 1,
    alter column version set not null,
    alter column saved_at set default (timezone('utc', now())),
    alter column saved_at set not null,
    alter column evidence_source set default 'runtime',
    alter column evidence_source set not null;

do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'clinical_notes_supersedes_note_fk'
    ) then
        alter table clinical_notes
            add constraint clinical_notes_supersedes_note_fk
            foreign key (supersedes_note_id)
            references clinical_notes(id)
            on delete set null;
    end if;

    if not exists (
        select 1
        from pg_constraint
        where conname = 'clinical_notes_evidence_source_check'
    ) then
        alter table clinical_notes
            add constraint clinical_notes_evidence_source_check
            check (evidence_source in ('runtime', 'migration-backfill'));
    end if;
end
$$;

create unique index if not exists ux_clinical_notes_encounter_version
    on clinical_notes (encounter, version)
    where encounter is not null;

create index if not exists ix_clinical_notes_encounter_history
    on clinical_notes (encounter, version desc, id desc);

create sequence if not exists clinical_note_id_seq;

select setval(
    'clinical_note_id_seq',
    greatest(coalesce((select max(id) from clinical_notes), 1), 1),
    true
);
