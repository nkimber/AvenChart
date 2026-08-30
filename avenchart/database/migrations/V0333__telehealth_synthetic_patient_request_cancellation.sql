-- SPDX-License-Identifier: GPL-3.0-or-later

-- Disabled-by-default patient-owned cancellation of a synthetic request before
-- any practice queue authorization. This creates no appointment, care, billing,
-- claim, integration, or external consequence.

alter table telehealth_requests
  drop constraint if exists chk_telehealth_requests_status;
alter table telehealth_requests
  add constraint chk_telehealth_requests_status
  check (status in ('Draft','LocationConfirmed','SafetyScreening','EmergencyRedirected','InPersonRecommended','Unsupported','ClinicalReview','Intake','Verification','OperationalReview','Redirected','Queued','Reserved','Connecting','InConsultation','WrapUp','Closed','Cancelled'));
