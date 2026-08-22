-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- Every scheduling mutation, including a recurrence exception, advances the
-- appointment aggregate version. This makes a stale browser submission a
-- conflict instead of an implicit last-writer-wins update.
alter table appointments
    add column if not exists row_version integer not null default 1;
