-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- The clinician patient search always scopes to a selected facility, excludes
-- merged source charts, and presents matching charts in this display order.
-- Keeping that access path in the database avoids a full-table scan and sort
-- as the number of charts grows.
create index if not exists idx_patients_facility_active_display
    on patients (facility_id, last_name, first_name, canonical_id)
    where merged_into_patient_id is null;
