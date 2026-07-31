// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

export const clinicianRoutes = [
  "/clinician/dashboard",
  "/clinician/schedule",
  "/clinician/calendar",
  "/clinician/flow",
  "/clinician/scheduling",
  "/clinician/patients",
  "/clinician/labs",
  "/clinician/messages",
  "/clinician/office-notes",
  "/clinician/address-book",
  "/clinician/tracks",
  "/clinician/track-entries",
  "/clinician/track-history",
  "/clinician/patient-education",
  "/clinician/recalls",
  "/clinician/batch-communication",
  "/clinician/chart-tracker",
  "/clinician/documents",
  "/clinician/document-ocr",
  "/clinician/document-templates",
  "/clinician/duplicate-review",
  "/clinician/renewals",
  "/clinician/reports",
  "/clinician/groups",
  "/clinician/billing",
  "/clinician/inventory",
  "/clinician/admin",
  "/clinician/experience",
  "/clinician/encounters/new",
] as const;

export const patientChartRoutes = [
  "summary",
  "chart",
  "timeline",
  "encounters",
  "documents",
  "labs",
  "appointments",
  "messages",
  "referrals",
  "authorizations",
  "sdoh",
  "print",
].map((section) => `/clinician/patients/MOD-PAT-0004/${section}`);

export const portalRoutes = [
  "/portal/home",
  "/portal/messages",
  "/portal/appointments",
  "/portal/records",
  "/portal/account",
] as const;
