-- A controlled-count discrepancy is resolved by exactly one compensating
-- custody event.  The repository claims the discrepancy and posts both records
-- in one transaction; this index preserves that relationship at the database
-- boundary as well.
create unique index if not exists ux_inventory_controlled_discrepancies_correction_event
    on inventory_controlled_count_discrepancies (correction_event_id)
    where correction_event_id is not null;

alter table inventory_controlled_count_discrepancies
    add constraint ck_inventory_controlled_discrepancy_correction_state
    check (
        status not in ('corrected', 'closed')
        or correction_event_id is not null
    ) not valid;
