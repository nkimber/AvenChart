-- SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
-- SPDX-License-Identifier: GPL-3.0-or-later

-- A synthetic request can return to the queue after a reservation or connection
-- expires. Preserve the immutable expired connection-room evidence and create a
-- separate session for a later reservation of the same request.
alter table telehealth_video_sessions
  drop constraint if exists telehealth_video_sessions_request_id_key;

alter table telehealth_video_sessions
  drop constraint if exists telehealth_video_sessions_reservation_id_key;

create unique index if not exists uq_telehealth_video_session_reservation
  on telehealth_video_sessions(reservation_id);

create index if not exists ix_telehealth_video_session_request_reservation
  on telehealth_video_sessions(request_id, reservation_id);
