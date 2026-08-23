-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- The operational flow board selects an appointment day and renders it in
-- start-time/id order. This is distinct from patient appointment history,
-- which is served by the existing (pid, appointment_date, start_time) index.
create index if not exists idx_appointments_date_start_id
    on appointments (appointment_date, start_time, id);
