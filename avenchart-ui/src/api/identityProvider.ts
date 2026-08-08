// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { apiBaseUrl, apiFetch } from "./transport.ts";

export type IdentityProviderReadinessCounts = {
  identityTypes: number;
  routedThroughAdapter: number;
  productionApproved: number;
  cryptographicallyValidated: number;
  facilityScoped: number;
  emergencyEnabled: number;
  blockingGaps: number;
};

export type IdentityAdapterContract = {
  adapterId: string;
  adapterKind: string;
  interface: string;
  credentialSource: string;
  subjectKey: string;
  resolvedClaims: string[];
  sessionStates: string[];
  productionApproved: boolean;
  validatesIssuer: boolean;
  validatesAudience: boolean;
  validatesSignature: boolean;
  enforcesMfa: boolean;
  enforcesDevicePolicy: boolean;
  enforcesFacilityScope: boolean;
};

export type IdentityTypeReadiness = {
  identityType: string;
  state: string;
  resolutionPath: string;
  lifecycleCoverage: string;
  capabilityMapping: string;
  evidence: string;
  routedThroughAdapter: boolean;
  productionApproved: boolean;
};

export type IdentityBoundaryControl = {
  controlId: string;
  state: string;
  contract: string;
  evidence: string;
};

export type IdentityVerification = {
  scenario: string;
  expectedResult: string;
  evidenceState: string;
};

export type IdentityProviderGap = {
  gapId: string;
  requiredDecision: string;
  ownerRole: string;
  currentState: string;
  blocksProduction: boolean;
};

export type IdentityProviderReadiness = {
  revision: string;
  lifecycleState: string;
  activeAdapterId: string;
  activeAdapterKind: string;
  environmentBoundary: string;
  counts: IdentityProviderReadinessCounts;
  adapter: IdentityAdapterContract;
  identityTypes: IdentityTypeReadiness[];
  boundaryControls: IdentityBoundaryControl[];
  verification: IdentityVerification[];
  gaps: IdentityProviderGap[];
};

export async function getIdentityProviderReadiness(
  sessionId: string,
  signal?: AbortSignal,
): Promise<IdentityProviderReadiness> {
  const response = await apiFetch(
    `${apiBaseUrl}/api/administration/identity-provider/readiness`,
    {
      headers: { "X-AvenChart-Session": sessionId },
      signal,
    },
  );
  return (await response.json()) as IdentityProviderReadiness;
}
