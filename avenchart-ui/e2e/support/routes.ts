// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

export const clinicianNavigationRoutes = [
  "/clinician/dashboard",
  "/clinician/schedule",
  "/clinician/calendar",
  "/clinician/flow",
  "/clinician/scheduling",
  "/clinician/patients",
  "/clinician/labs",
  "/clinician/lab-directory",
  "/clinician/lab-catalog",
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
  "/clinician/referrals",
  "/clinician/authorizations",
  "/clinician/document-templates",
  "/clinician/duplicate-review",
  "/clinician/renewals",
  "/clinician/reports",
  "/clinician/groups",
  "/clinician/billing",
  "/clinician/inventory",
  "/clinician/admin",
  "/clinician/experience",
] as const;

export const clinicianRoutes = [
  ...clinicianNavigationRoutes,
  "/clinician/encounters/new",
  "/clinician/patients/new",
] as const;

export const patientChartRoutes = [
  "summary",
  "chart",
  "timeline",
  "encounters",
  "encounters/new",
  "documents",
  "labs",
  "appointments",
  "messages",
  "referrals",
  "authorizations",
  "sdoh",
  "forms",
  "print",
].map((section) => `/clinician/patients/MOD-PAT-0004/${section}`);

export const portalRoutes = [
  "/portal/home",
  "/portal/messages",
  "/portal/appointments",
  "/portal/records",
  "/portal/account",
] as const;
