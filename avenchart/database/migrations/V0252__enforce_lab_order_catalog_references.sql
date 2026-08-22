-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- A catalog child must continue to refer to an extant catalog group.  The existing
-- deterministic data is validated by this migration before the constraint is added.
do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'fk_lab_order_catalog_parent'
          and conrelid = 'lab_order_catalog'::regclass
    ) then
        alter table lab_order_catalog
            add constraint fk_lab_order_catalog_parent
            foreign key (parent_id)
            references lab_order_catalog(id)
            on delete restrict;
    end if;
end
$$;

-- Imported order rows use a parent, code, and item type as their natural identity.
-- Group entries intentionally have no code, so their provider relationship is the
-- stable identity instead of treating every blank code as the same entry.
create unique index if not exists ux_lab_order_catalog_parent_code_item
    on lab_order_catalog(parent_id, code, item_type)
    where parent_id is not null
      and code is not null
      and btrim(code) <> '';

create unique index if not exists ux_lab_order_catalog_parent_lab_group
    on lab_order_catalog(parent_id, lab_id)
    where parent_id is not null
      and lab_id is not null
      and item_type = 'grp';
