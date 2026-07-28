import { apiBaseUrl, apiFetch } from "./transport.ts";

export type ExperienceBaselineCounts = {
  roles: number;
  environments: number;
  tasks: number;
  criteria: number;
  metLocal: number;
  measuredLocal: number;
  ownerGated: number;
  proposed: number;
  analyticsEvents: number;
  analyticsEventsCollected: number;
  gaps: number;
};

export type ExperienceRole = {
  id: string;
  label: string;
  scope: string;
};

export type ExperienceEnvironment = {
  id: string;
  browser: string;
  deviceClass: string;
  viewport: string;
  testLevels: string[];
  status: string;
  evidence: string;
};

export type ExperienceTask = {
  id: string;
  label: string;
  roleIds: string[];
  route: string;
  risk: string;
  successCriterion: string;
  errorCriterion: string;
  recoveryCriterion: string;
  accessibilityCriterion: string;
  performanceCriterion: string;
  evidence: string;
};

export type ExperienceCriterion = {
  id: string;
  category: string;
  label: string;
  lifecycleState: string;
  target: string;
  measurement: string;
  evidence: string;
  ownerRole: string;
};

export type ExperienceAnalyticsEvent = {
  eventId: string;
  purpose: string;
  allowedProperties: string[];
  collectionEnabled: boolean;
  lifecycleState: string;
};

export type ExperienceGap = {
  id: string;
  area: string;
  state: string;
  requiredDecision: string;
  ownerRole: string;
  blocksProduction: boolean;
};

export type ExperienceBaseline = {
  revision: string;
  lifecycleState: string;
  ownerRole: string;
  accessibilityStandard: string;
  scope: string;
  counts: ExperienceBaselineCounts;
  roles: ExperienceRole[];
  environments: ExperienceEnvironment[];
  tasks: ExperienceTask[];
  criteria: ExperienceCriterion[];
  analyticsEvents: ExperienceAnalyticsEvent[];
  forbiddenAnalyticsProperties: string[];
  gaps: ExperienceGap[];
};

export async function getExperienceBaseline(
  sessionId: string,
  signal?: AbortSignal,
): Promise<ExperienceBaseline> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/administration/experience-baseline`,
    {
      headers: { "X-Legacy EHR-Session": sessionId },
      signal,
    },
  );
  return (await response.json()) as ExperienceBaseline;
}
