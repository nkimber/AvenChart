// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Page } from '@playwright/test'

const practiceContext = {
  available: true,
  practiceDisplayName: 'AvenChart Synthetic Practice',
  supportedStates: ['GA', 'CA', 'FL'],
  syntheticOnly: true,
  entryMessage: 'Synthetic demonstration only. This service is not available for patient care.',
}

const prospectiveApplicant = {
  applicantId: '40000000-0000-4000-8000-000000000004',
  status: 'ContactVerificationPending',
  version: 1,
  practiceDisplayName: 'AvenChart Synthetic Practice',
  residenceStateCode: 'GA',
  maskedEmail: 'a•••@example.test',
  maskedPhone: '(***) ***-0199',
  contactVerified: false,
  identityAssurance: 'UnverifiedContact',
  duplicateDisposition: null,
  canonicalPatientCreated: false,
  verificationAttemptsRemaining: 5,
  expiresAt: '2026-08-27T04:30:00Z',
  demonstrationVerificationCode: '246810',
  nextAction: 'Enter the demonstration code to verify control of the synthetic email contact.',
  limitations: [
    'Synthetic demonstration only; no message was sent.',
    'Contact verification is not identity proofing.',
    'This applicant is not a patient, has no chart, and cannot enter the telehealth queue.',
  ],
}

const safetyApprovedApplicant = {
  ...prospectiveApplicant,
  status: 'IdentityReviewApproved',
  version: 3,
  contactVerified: true,
  identityAssurance: 'ContactControlOnly',
  duplicateDisposition: 'NoCandidate',
  verificationAttemptsRemaining: 0,
  demonstrationVerificationCode: null,
  nextAction: 'Complete the synthetic universal safety screen. This is not complete triage or a request for care.',
}

const prospectiveSafetyPassed = {
  evaluationId: '22000000-0000-4000-8000-000000000002',
  applicantId: prospectiveApplicant.applicantId,
  applicantVersion: 4,
  applicantStatus: 'SafetyScreenPassed',
  safetyDisposition: 'ContinueProspectiveIntake',
  currentLocationStateCode: 'CA',
  protocolKey: 'synthetic-universal-safety',
  protocolVersion: 1,
  evaluatedAt: '2026-08-27T14:30:00Z',
  mayContinueProspectiveIntake: true,
  identityProofed: false,
  clinicalReviewPerformed: false,
  canonicalPatientCreated: false,
  chartLinked: false,
  prospectiveIntakeCompleted: false,
  coverageChecked: false,
  requestCreated: false,
  queueEnabled: false,
  careEnabled: false,
  direction: 'The universal safety screen found no stop condition in these synthetic answers. Later gates remain required.',
  limitations: [
    'No clinician reviewed these answers and no diagnosis or treatment decision was made.',
    'No patient, chart, coverage check, request, appointment, or queue entry was created.',
  ],
}

const prospectiveVisitPurposeRecorded = {
  purposeId: '23000000-0000-4000-8000-000000000003',
  applicantId: prospectiveApplicant.applicantId,
  applicantVersion: 5,
  applicantStatus: 'VisitPurposeRecorded',
  purposeCategory: 'sleep',
  purposeDisplayLabel: 'Sleep difficulty',
  recordedAt: '2026-08-27T14:35:00Z',
  clinicalProtocolPublished: false,
  clinicalEligibilityDetermined: false,
  identityProofed: false,
  canonicalPatientCreated: false,
  chartLinked: false,
  prospectiveIntakeCompleted: false,
  coverageChecked: false,
  requestCreated: false,
  queueEnabled: false,
  careEnabled: false,
  direction: 'The navigation category is recorded. No clinical eligibility decision has been made.',
  limitations: [
    'No complaint-specific clinical protocol ran and no clinician reviewed this selection.',
    'No patient, coverage check, request, appointment, queue entry, or care capability was created.',
  ],
}

const prospectivePracticeNetworkOptions = {
  applicantId: prospectiveApplicant.applicantId,
  applicantVersion: 5,
  applicantStatus: 'VisitPurposeRecorded',
  adapterMode: 'NON_PRODUCTION',
  catalogKey: 'avenchart-synthetic-prospective-practice-network-2026-08',
  catalogVersion: 1,
  catalogEffectiveFrom: '2026-08-27T00:00:00Z',
  catalogEffectiveThrough: '2026-10-31T23:59:59Z',
  plans: [
    { planKey: 'harbor-mutual-hd', payerDisplayName: 'Harbor Mutual', productDisplayName: 'High Deductible', practiceNetworkStatus: 'PracticeNetworkConfirmedFixture', meaning: 'The synthetic fixture says the practice participates for this plan, state, and visit category. It does not check the member or rendering physician.' },
    { planKey: 'blue-valley-standard', payerDisplayName: 'Blue Valley Health', productDisplayName: 'Standard', practiceNetworkStatus: 'NetworkUnknown', meaning: 'The synthetic fixture has no authoritative practice-plan participation result. Treat network status as unknown.' },
    { planKey: 'pine-state-choice', payerDisplayName: 'Pine State Choice', productDisplayName: 'Choice', practiceNetworkStatus: 'PracticeOutOfNetworkFixture', meaning: 'The synthetic fixture says the practice does not participate for this plan. No self-pay choice or estimate is created.' },
  ],
  memberEligibilityChecked: false,
  memberBenefitsChecked: false,
  renderingPhysicianNetworkChecked: false,
  coverageVerified: false,
  exactNetworkConfirmed: false,
  direction: 'Choose one fictional plan for a practice-level demonstration precheck.',
  limitations: ['No payer, directory, clearinghouse, or X12 transaction was contacted.'],
}

const prospectivePracticeNetworkRecorded = {
  precheckId: '24000000-0000-4000-8000-000000000004',
  applicantId: prospectiveApplicant.applicantId,
  applicantVersion: 6,
  applicantStatus: 'PracticeNetworkPrecheckRecorded',
  currentLocationStateCode: 'CA',
  purposeCategory: 'sleep',
  planKey: 'blue-valley-standard',
  payerDisplayName: 'Blue Valley Health',
  productDisplayName: 'Standard',
  practiceNetworkStatus: 'NetworkUnknown',
  adapterMode: 'NON_PRODUCTION',
  catalogKey: prospectivePracticeNetworkOptions.catalogKey,
  catalogVersion: 1,
  catalogEffectiveFrom: prospectivePracticeNetworkOptions.catalogEffectiveFrom,
  catalogEffectiveThrough: prospectivePracticeNetworkOptions.catalogEffectiveThrough,
  recordedAt: '2026-08-27T14:40:00Z',
  memberEligibilityChecked: false,
  memberBenefitsChecked: false,
  renderingPhysicianNetworkChecked: false,
  coverageVerified: false,
  exactNetworkConfirmed: false,
  identityProofed: false,
  canonicalPatientCreated: false,
  chartLinked: false,
  portalAccountCreated: false,
  prospectiveIntakeCompleted: false,
  consentCreated: false,
  practiceAccepted: false,
  coverageRecordCreated: false,
  estimateCreated: false,
  financialAcknowledgmentCreated: false,
  requestCreated: false,
  queueEnabled: false,
  appointmentCreated: false,
  encounterCreated: false,
  careEnabled: false,
  prescribingEnabled: false,
  billingEnabled: false,
  claimCreated: false,
  communicationEnabled: false,
  integrationEnabled: false,
  externalCallPerformed: false,
  direction: 'Individual eligibility and exact practice-and-physician network verification remain required.',
  limitations: ['No member, benefits, rendering physician, coverage, price, or payment was checked.'],
}

const prospectiveMemberInsuranceDetailsRecorded = {
  detailsId: '25000000-0000-4000-8000-000000000005',
  applicantId: prospectiveApplicant.applicantId,
  applicantVersion: 7,
  applicantStatus: 'MemberInsuranceDetailsRecorded',
  currentLocationStateCode: 'CA',
  purposeCategory: 'sleep',
  planKey: 'blue-valley-standard',
  payerDisplayName: 'Blue Valley Health',
  productDisplayName: 'Standard',
  practiceNetworkStatus: 'NetworkUnknown',
  memberIdMask: '••••2002',
  groupNumberMask: '••••UP01',
  subscriberRelationship: 'Spouse',
  coveragePriority: 'Primary',
  protectionScheme: 'ASP.NET_CORE_DATA_PROTECTION',
  protectionVersion: 1,
  recordedAt: '2026-08-27T14:45:00Z',
  memberMatched: false,
  memberEligibilityChecked: false,
  memberBenefitsChecked: false,
  renderingPhysicianNetworkChecked: false,
  coverageVerified: false,
  exactNetworkConfirmed: false,
  identityProofed: false,
  canonicalPatientCreated: false,
  chartLinked: false,
  portalAccountCreated: false,
  prospectiveIntakeCompleted: false,
  consentCreated: false,
  practiceAccepted: false,
  coverageRecordCreated: false,
  estimateCreated: false,
  financialAcknowledgmentCreated: false,
  requestCreated: false,
  queueEnabled: false,
  appointmentCreated: false,
  encounterCreated: false,
  careEnabled: false,
  prescribingEnabled: false,
  billingEnabled: false,
  claimCreated: false,
  communicationEnabled: false,
  integrationEnabled: false,
  externalCallPerformed: false,
  direction: 'The protected synthetic receipt is recorded. Eligibility and all later gates remain required.',
  limitations: ['No raw member, group, or subscriber values are returned.'],
}

const prospectiveEligibilityRecorded = {
  eligibilityResultId: '26000000-0000-4000-8000-000000000006',
  applicantId: prospectiveApplicant.applicantId,
  applicantVersion: 8,
  applicantStatus: 'SyntheticEligibilityRecorded',
  currentLocationStateCode: 'CA',
  purposeCategory: 'sleep',
  planKey: 'blue-valley-standard',
  payerDisplayName: 'Blue Valley Health',
  productDisplayName: 'Standard',
  practiceNetworkStatus: 'NetworkUnknown',
  memberIdMask: '••••2002',
  groupNumberMask: '••••UP01',
  subscriberRelationship: 'Spouse',
  coveragePriority: 'Primary',
  dateOfService: '2026-08-27',
  serviceCategory: 'ProfessionalTelehealthConsultation',
  adapterMode: 'NON_PRODUCTION',
  compatibilityTarget: 'ASC_X12N_270_271_005010X279A1',
  datasetKey: 'avenchart-synthetic-prospective-eligibility-2026-08',
  datasetVersion: 1,
  datasetEffectiveFrom: '2026-08-27T00:00:00Z',
  datasetEffectiveThrough: '2026-10-31T23:59:59Z',
  inquiryTraceToken: '27000000-0000-4000-8000-000000000007',
  responseTraceToken: '28000000-0000-4000-8000-000000000008',
  transportOutcome: 'SimulatedAccepted',
  memberMatchStatus: 'Matched',
  eligibilityStatus: 'Inactive',
  benefitInformationStatus: 'NotReported',
  businessOutcome: 'CoverageInactive',
  memberMatched: true,
  memberEligibilityChecked: true,
  memberBenefitsChecked: false,
  checkedAt: '2026-08-27T14:50:00Z',
  expiresAt: '2026-08-27T15:05:00Z',
  recordedAt: '2026-08-27T14:50:01Z',
  rawTransactionCreated: false,
  renderingPhysicianNetworkChecked: false,
  coverageVerified: false,
  exactNetworkConfirmed: false,
  identityProofed: false,
  canonicalPatientCreated: false,
  chartLinked: false,
  portalAccountCreated: false,
  prospectiveIntakeCompleted: false,
  consentCreated: false,
  practiceAccepted: false,
  coverageRecordCreated: false,
  estimateCreated: false,
  financialAcknowledgmentCreated: false,
  requestCreated: false,
  queueEnabled: false,
  appointmentCreated: false,
  encounterCreated: false,
  careEnabled: false,
  prescribingEnabled: false,
  billingEnabled: false,
  claimCreated: false,
  communicationEnabled: false,
  integrationEnabled: false,
  externalCallPerformed: false,
  direction: 'The synthetic fixture reports inactive eligibility. No request was created.',
  limitations: ['No raw X12 transaction, payer call, exact network result, or payment guarantee was created.'],
}

const prospectivePracticeNetworkDeterminationRecorded = {
  networkDeterminationId: '29000000-0000-4000-8000-000000000009',
  applicantId: prospectiveApplicant.applicantId,
  applicantVersion: 9,
  applicantStatus: 'SyntheticPracticeNetworkRecorded',
  currentLocationStateCode: 'CA',
  purposeCategory: 'sleep',
  planKey: 'blue-valley-standard',
  payerDisplayName: 'Blue Valley Health',
  productDisplayName: 'Standard',
  practiceDisplayName: 'AvenChart Synthetic Practice',
  dateOfService: '2026-08-27',
  serviceCategory: 'ProfessionalTelehealthConsultation',
  eligibilityStatus: 'Inactive',
  benefitInformationStatus: 'NotReported',
  eligibilityBusinessOutcome: 'CoverageInactive',
  eligibilityCheckedAt: '2026-08-27T14:50:00Z',
  eligibilityExpiresAt: '2026-08-27T15:05:00Z',
  adapterMode: 'NON_PRODUCTION',
  compatibilityTarget: 'HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0',
  datasetKey: 'avenchart-synthetic-practice-network-directory-2026-08',
  datasetVersion: 1,
  datasetEffectiveFrom: '2026-08-27T00:00:00Z',
  datasetEffectiveThrough: '2026-10-31T23:59:59Z',
  sourceLastUpdatedAt: '2026-08-27T00:00:00Z',
  requestTraceToken: '30000000-0000-4000-8000-000000000010',
  responseTraceToken: '31000000-0000-4000-8000-000000000011',
  transportOutcome: 'SimulatedUnavailable',
  planNetworkMatchStatus: 'Unknown',
  practiceAffiliationStatus: 'Unknown',
  serviceAvailabilityStatus: 'Unknown',
  newPatientAcceptanceStatus: 'Unknown',
  businessOutcome: 'UnableToDetermine',
  practiceNetworkChecked: false,
  practiceInNetwork: false,
  newPatientsAccepted: false,
  networkReference: null,
  organizationReference: null,
  locationReference: null,
  serviceReference: null,
  checkedAt: '2026-08-27T14:51:00Z',
  expiresAt: '2026-08-27T15:06:00Z',
  recordedAt: '2026-08-27T14:51:01Z',
  fhirResourceCreated: false,
  liveDirectoryQueried: false,
  renderingPhysicianNetworkChecked: false,
  exactNetworkConfirmed: false,
  coverageVerified: false,
  identityProofed: false,
  canonicalPatientCreated: false,
  chartLinked: false,
  portalAccountCreated: false,
  prospectiveIntakeCompleted: false,
  consentCreated: false,
  practiceAccepted: false,
  coverageRecordCreated: false,
  estimateCreated: false,
  financialAcknowledgmentCreated: false,
  requestCreated: false,
  queueEnabled: false,
  appointmentCreated: false,
  encounterCreated: false,
  careEnabled: false,
  prescribingEnabled: false,
  billingEnabled: false,
  claimCreated: false,
  communicationEnabled: false,
  integrationEnabled: false,
  externalCallPerformed: false,
  direction: 'The synthetic directory adapter could not determine practice participation.',
  limitations: ['No FHIR resource, directory call, rendering-physician check, coverage, payment, request, queue, or care action was created.'],
}

const prospectivePracticeNetworkRecordedPositive = {
  ...prospectivePracticeNetworkRecorded,
  planKey: 'harbor-mutual-hd',
  payerDisplayName: 'Harbor Mutual',
  productDisplayName: 'High Deductible',
  practiceNetworkStatus: 'PracticeNetworkConfirmedFixture',
}

const prospectiveMemberInsuranceDetailsRecordedPositive = {
  ...prospectiveMemberInsuranceDetailsRecorded,
  planKey: 'harbor-mutual-hd',
  payerDisplayName: 'Harbor Mutual',
  productDisplayName: 'High Deductible',
  practiceNetworkStatus: 'PracticeNetworkConfirmedFixture',
  memberIdMask: '••••1001',
}

const prospectiveEligibilityRecordedPositive = {
  ...prospectiveEligibilityRecorded,
  planKey: 'harbor-mutual-hd',
  payerDisplayName: 'Harbor Mutual',
  productDisplayName: 'High Deductible',
  practiceNetworkStatus: 'PracticeNetworkConfirmedFixture',
  memberIdMask: '••••1001',
  transportOutcome: 'SimulatedAccepted',
  memberMatchStatus: 'Matched',
  eligibilityStatus: 'Active',
  benefitInformationStatus: 'Reported',
  businessOutcome: 'EligibleBenefitsReported',
  memberMatched: true,
  memberEligibilityChecked: true,
  memberBenefitsChecked: true,
  direction: 'The synthetic fixture reports active eligibility and benefit information. Later gates remain required.',
}

const prospectivePracticeNetworkDeterminationRecordedPositive = {
  ...prospectivePracticeNetworkDeterminationRecorded,
  planKey: 'harbor-mutual-hd',
  payerDisplayName: 'Harbor Mutual',
  productDisplayName: 'High Deductible',
  eligibilityStatus: 'Active',
  benefitInformationStatus: 'Reported',
  eligibilityBusinessOutcome: 'EligibleBenefitsReported',
  transportOutcome: 'SimulatedAvailable',
  planNetworkMatchStatus: 'Matched',
  practiceAffiliationStatus: 'InNetwork',
  serviceAvailabilityStatus: 'Included',
  newPatientAcceptanceStatus: 'Accepting',
  businessOutcome: 'PracticeInNetworkAcceptingNewPatients',
  practiceNetworkChecked: true,
  practiceInNetwork: true,
  newPatientsAccepted: true,
  networkReference: 'syn-network-harbor-mutual-hd',
  organizationReference: 'syn-org-avenchart-practice',
  locationReference: 'syn-location-main-telehealth',
  serviceReference: 'syn-service-professional-telehealth',
  direction: 'Practice directory evidence is recorded; rendering physician participation remains required.',
}

const prospectiveIdentityProofingRecorded = {
  identityProofingResultId: '32000000-0000-4000-8000-000000000012',
  applicantId: prospectiveApplicant.applicantId,
  applicantVersion: 10,
  applicantStatus: 'SyntheticIdentityProofingRecorded',
  currentLocationStateCode: 'CA',
  planKey: 'harbor-mutual-hd',
  privacyNoticeKey: 'SYNTHETIC_IDENTITY_PROOFING_NOTICE',
  privacyNoticeVersion: 1,
  privacyNoticeAcknowledged: true,
  adapterMode: 'NON_PRODUCTION',
  compatibilityTarget: 'NIST_SP_800_63A_4_PROCESS_CONCEPTS_ONLY',
  practiceStatementKey: 'SYNTHETIC_IDENTITY_PRACTICE_STATEMENT',
  practiceStatementVersion: 1,
  datasetKey: 'avenchart-synthetic-identity-proofing-2026-08',
  datasetVersion: 1,
  datasetEffectiveFrom: '2026-08-27T00:00:00Z',
  datasetEffectiveThrough: '2026-10-31T23:59:59Z',
  sourceLastUpdatedAt: '2026-08-27T00:00:00Z',
  requestTraceToken: '33000000-0000-4000-8000-000000000013',
  responseTraceToken: '34000000-0000-4000-8000-000000000014',
  proofingMethod: 'SYNTHETIC_REMOTE_UNATTENDED_NON_BIOMETRIC',
  transportOutcome: 'SimulatedCompleted',
  evidenceCollectionStatus: 'FixtureReferenceAccepted',
  evidenceValidationStatus: 'ValidatedFixture',
  attributeValidationStatus: 'ValidatedFixture',
  applicantVerificationStatus: 'VerifiedFixture',
  fraudCheckStatus: 'NoIndicatorFixture',
  businessOutcome: 'SyntheticProofingPassed',
  proofingSessionReference: 'syn-proof-session-35000000000040008000000000000015',
  evidencePackageReference: 'syn-evidence-40000000000040008000000000000004',
  checkedAt: '2026-08-27T14:52:00Z',
  expiresAt: '2026-08-27T15:07:00Z',
  recordedAt: '2026-08-27T14:52:01Z',
  assuranceLevelAchieved: 'None',
  identityEvidenceCollected: false,
  governmentIdentifierCollected: false,
  biometricDataCollected: false,
  authoritativeSourceQueried: false,
  proofingNotificationSent: false,
  redressCaseCreated: false,
  authenticatorBound: false,
  identityProofed: false,
  canonicalPatientCreated: false,
  chartLinked: false,
  portalAccountCreated: false,
  prospectiveIntakeCompleted: false,
  consentCreated: false,
  practiceAccepted: false,
  coverageRecordCreated: false,
  estimateCreated: false,
  financialAcknowledgmentCreated: false,
  requestCreated: false,
  queueEnabled: false,
  appointmentCreated: false,
  encounterCreated: false,
  careEnabled: false,
  prescribingEnabled: false,
  billingEnabled: false,
  claimCreated: false,
  communicationEnabled: false,
  integrationEnabled: false,
  externalCallPerformed: false,
  direction: 'The synthetic process fixture was recorded; no real identity was established.',
  limitations: ['No evidence, identifier, biometric, authoritative source, IAL, patient, request, queue, or care action was created.'],
}

const draftRequest = {
  requestId: '10000000-0000-4000-8000-000000000001',
  status: 'Draft',
  complaintCategory: 'migraine',
  triageOutcome: null,
  version: 1,
  stateCode: null,
  createdAt: '2026-08-26T12:00:00Z',
  updatedAt: '2026-08-26T12:00:00Z',
  readyAt: null,
  allowedActions: ['confirm-location'],
}

const queueRequest = {
  requestId: '20000000-0000-4000-8000-000000000002',
  status: 'OperationalReview',
  complaintCategory: 'sleep',
  triageOutcome: 'TelehealthEligible',
  version: 3,
  createdAt: '2026-08-26T12:05:00Z',
}

const identityReviewApplicant = {
  applicantId: '21000000-0000-4000-8000-000000000002',
  version: 2,
  status: 'IdentityReviewPending',
  legalFirstName: 'Avery',
  legalLastName: 'Review',
  dateOfBirth: '1990-03-14',
  maskedEmail: 'a•••@example.test',
  maskedPhone: '••••••0199',
  residenceStateCode: 'GA',
  postalCode: '30301',
  contactVerifiedAt: '2026-08-27T04:28:00Z',
  duplicateDisposition: 'NoCandidate',
  allowedDecision: 'ApprovedForProspectiveIntake',
  createdAt: '2026-08-27T04:25:00Z',
  expiresAt: '2026-08-27T06:25:00Z',
}

const promotionAuthorizationApplicant = {
  applicantId: '23000000-0000-4000-8000-000000000002',
  version: 10,
  status: 'SyntheticIdentityProofingRecorded',
  legalFirstName: 'Riley',
  legalLastName: 'Synthetic',
  dateOfBirth: '1992-04-12',
  maskedEmail: 'r•••@example.test',
  maskedPhone: '••••••0166',
  residenceStateCode: 'CA',
  postalCode: '94105',
  planKey: 'harbor-mutual-hd',
  payerDisplayName: 'Harbor Mutual',
  productDisplayName: 'High Deductible',
  eligibilityStatus: 'Active',
  benefitInformationStatus: 'Reported',
  eligibilityBusinessOutcome: 'EligibleBenefitsReported',
  networkBusinessOutcome: 'PracticeInNetworkAcceptingNewPatients',
  proofingMethod: 'SYNTHETIC_REMOTE_UNATTENDED_NON_BIOMETRIC',
  transportOutcome: 'SimulatedCompleted',
  evidenceCollectionStatus: 'FixtureReferenceAccepted',
  evidenceValidationStatus: 'ValidatedFixture',
  attributeValidationStatus: 'ValidatedFixture',
  applicantVerificationStatus: 'VerifiedFixture',
  fraudCheckStatus: 'NoIndicatorFixture',
  proofingBusinessOutcome: 'SyntheticProofingPassed',
  assuranceLevelAchieved: 'None',
  identityProofed: false,
  proofingCheckedAt: '2026-08-27T04:29:00Z',
  proofingExpiresAt: '2026-08-27T04:44:00Z',
  createdAt: '2026-08-27T04:10:00Z',
  applicantExpiresAt: '2026-08-27T06:10:00Z',
  allowedDecisions: ['AuthorizedForSyntheticPromotion', 'DeniedForSyntheticPromotion'],
}

const syntheticPromotionApplicant = {
  applicantId: '25000000-0000-4000-8000-000000000002',
  version: 11,
  status: 'SyntheticPromotionAuthorized',
  legalFirstName: 'Jordan',
  legalLastName: 'Atomic',
  dateOfBirth: '1991-05-20',
  maskedEmail: 'j•••@example.test',
  maskedPhone: '••••••0177',
  residenceStateCode: 'GA',
  postalCode: '30301',
  authorizationDecision: 'AuthorizedForSyntheticPromotion',
  authorizedAt: '2026-08-27T04:31:00Z',
  assuranceLevelAchieved: 'None',
  identityProofed: false,
  applicantExpiresAt: '2026-08-27T06:10:00Z',
  proofingExpiresAt: '2026-08-27T04:44:00Z',
  allowedCommand: 'PromoteAuthorizedSyntheticApplicant',
}

const intakeRequest = {
  ...draftRequest,
  status: 'Intake',
  triageOutcome: 'TelehealthEligible',
  version: 3,
  stateCode: 'CA',
  allowedActions: ['complete-readiness'],
  coverage: null,
}

const queuedPatientRequest = {
  ...intakeRequest,
  status: 'Queued',
  version: 8,
  allowedActions: ['await-clinician'],
  readyAt: '2026-08-26T12:20:00Z',
}

const reservedPatientRequest = {
  ...queuedPatientRequest,
  status: 'Reserved',
  version: 9,
  allowedActions: ['clinician-reserved'],
}

const inConsultationPatientRequest = {
  ...queuedPatientRequest,
  status: 'InConsultation',
  version: 11,
  allowedActions: [],
}

const wrapUpPatientRequest = {
  ...inConsultationPatientRequest,
  status: 'WrapUp',
  version: 12,
}

const patientQueueStatus = {
  requestId: queuedPatientRequest.requestId,
  requestStatus: 'Queued',
  requestVersion: 8,
  phase: 'InQueue',
  headline: "You're in line",
  detail: 'Approximately 2 requests are ahead. This can change for safety or operational reasons.',
  approximateRequestsAhead: 2,
  positionIsApproximate: true,
  waitEstimateAvailable: false,
  waitEstimateMessage: 'A wait-time estimate is not available in this synthetic demonstration.',
  requestUpdatedAt: '2026-08-26T12:20:00Z',
  snapshotAt: '2026-08-26T12:20:05Z',
  refreshAfterSeconds: 5,
  realtimeAvailable: false,
  safetyActions: [
    'If symptoms worsen or you are unsure it is safe to wait, contact the practice or seek in-person care.',
    'Call 911 now for an emergency.',
  ],
}

const reservedPatientStatus = {
  ...patientQueueStatus,
  requestStatus: 'Reserved',
  requestVersion: 9,
  phase: 'PhysicianPreparing',
  headline: 'A physician is getting ready',
  detail: 'Keep this page open. You can run the synthetic device check when the connection-room action appears.',
  approximateRequestsAhead: null,
  positionIsApproximate: false,
}

const connectionGrant = {
  sessionId: '50000000-0000-4000-8000-000000000005',
  grantId: '60000000-0000-4000-8000-000000000006',
  requestId: reservedPatientRequest.requestId,
  requestVersion: 10,
  requestStatus: 'Connecting',
  participantRole: 'patient',
  adapterMode: 'NON_PRODUCTION',
  joinCredential: 'secret-credential-never-rendered-123456789012345',
  expiresAt: '2026-08-27T04:31:00Z',
  recordingEnabled: false,
  transcriptionEnabled: false,
  mediaTransportEnabled: false,
  waitingRoomMessage: 'Your private synthetic waiting room is ready. No media is connected in this demonstration.',
  limitations: [
    'NON_PRODUCTION simulator: no video, audio, signaling, TURN, or vendor connection is created.',
    'Entering this waiting room does not start a consultation or create an encounter.',
  ],
}

const consultationStart = {
  consultationId: '90000000-0000-4000-8000-000000000009',
  requestId: queueRequest.requestId,
  requestVersion: 10,
  requestStatus: 'InConsultation',
  appointmentStatus: '>',
  modality: 'SYNTHETIC_VIDEO',
  startedAt: '2026-08-27T04:32:00Z',
  legalEffect: false,
  chartAccessEnabled: true,
  documentationEnabled: true,
  prescribingEnabled: false,
  claimsEnabled: false,
  limitations: [
    'Synthetic lifecycle evidence only; no real consultation or media occurred.',
    'Only an audited, bounded chart projection and unsigned SOAP draft are available; general chart navigation remains unavailable.',
    'Diagnosis, signing, prescribing, claims, and completion are unavailable in this slice.',
  ],
}

const consultationWorkspace = {
  consultationId: consultationStart.consultationId,
  consultationStatus: 'InConsultation',
  consultationVersion: 1,
  modality: 'SYNTHETIC_VIDEO',
  startedAt: consultationStart.startedAt,
  mediaEndedAt: null,
  asOf: '2026-08-27T04:32:05Z',
  readOnly: true,
  patient: {
    displayName: 'Synthetic Workspace Patient',
    dateOfBirth: '1990-01-01',
    age: 36,
    recordedSex: 'Female',
    callbackPhone: '(555) 555-0100',
  },
  visit: {
    patientLocationState: 'CA',
    complaintCategory: 'migraine',
    complaintSummary: 'Synthetic migraine without emergency warning signs.',
    symptomDuration: '1-3-days',
    triageOutcome: 'Eligible',
  },
  allergies: [{ title: 'Synthetic penicillin', reaction: 'rash', severity: 'moderate' }],
  medications: [{ title: 'Synthetic maintenance medicine' }],
  problems: [{ title: 'Synthetic active problem', diagnosis: 'Synthetic diagnosis label' }],
  documentation: {
    version: 0,
    savedAt: null,
    savedBy: null,
    isLocked: false,
    isSigned: false,
    isFinal: false,
    subjective: null,
    objective: null,
    assessment: null,
    plan: null,
  },
  documentationEnabled: true,
  prescribingEnabled: false,
  claimsEnabled: false,
  completionEnabled: false,
  limitations: [
    'This projection is read-only and limited to the active synthetic consultation.',
    'Verify allergies, medications, problems, identity, location, and callback verbally.',
  ],
  patientId: 'forbidden-patient-id-marker',
  encounterId: 'forbidden-encounter-id-marker',
  policyNumber: 'forbidden-policy-marker',
  streetAddress: 'forbidden-address-marker',
}

const consultationWrapUp = {
  consultationId: consultationStart.consultationId,
  version: 2,
  consultationStatus: 'MediaEnded',
  mediaEndedAt: '2026-08-27T04:36:00Z',
  requestVersion: 11,
  requestStatus: 'WrapUp',
  shiftStatus: 'WrapUp',
  appointmentStatus: '>',
  documentationEnabled: true,
  completionEnabled: false,
  clinicianAvailableForNewWork: false,
  limitations: [
    'This synthetic visit remains unfinished and physician-owned.',
    'No disposition, signing, completion, after-visit summary, prescription, or claim is created.',
  ],
}

const pharmacyChoiceWorkspace = {
  consultationId: consultationStart.consultationId,
  consultationStatus: 'MediaEnded',
  adapterMode: 'NON_PRODUCTION',
  datasetId: 'avenchart-synthetic-pharmacy-directory',
  datasetVersion: '2026.08.27.1',
  asOf: '2026-08-27T04:36:05Z',
  searchState: 'CA',
  searchPostalCode: null,
  distanceOrigin: null,
  locationSearchAcknowledged: false,
  chartPreferenceCount: 1,
  pharmacies: [{
    directoryEntryId: '00000000-0000-4000-8000-000000002001',
    name: 'Synthetic Golden Gate Pharmacy',
    address: { line1: '200 Synthetic Market Street', line2: null, city: 'San Francisco', state: 'CA', postalCode: '94105', country: 'US' },
    phone: '(555) 555-0201',
    ncpdpId: null,
    npi: null,
    electronicRoutingCapability: 'NON_PRODUCTION_ONLY',
    isChartPreferred: true,
    approximateDistanceMiles: null,
  }],
  currentChoice: null,
  prescriptionEnabled: false,
  transmissionEnabled: false,
  limitations: [
    'Synthetic directory facts only; no live pharmacy network is queried.',
    'Recording a destination does not create, sign, route, or transmit a prescription.',
  ],
}

const pharmacyChoiceDraft = {
  version: 1,
  directoryEntryId: pharmacyChoiceWorkspace.pharmacies[0].directoryEntryId,
  name: pharmacyChoiceWorkspace.pharmacies[0].name,
  address: pharmacyChoiceWorkspace.pharmacies[0].address,
  phone: pharmacyChoiceWorkspace.pharmacies[0].phone,
  ncpdpId: null,
  npi: null,
  electronicRoutingCapability: 'NON_PRODUCTION_ONLY',
  directorySource: pharmacyChoiceWorkspace.datasetId,
  directoryVersion: pharmacyChoiceWorkspace.datasetVersion,
  choiceBasis: 'PatientConfirmedDuringConsultation',
  patientChoiceConfirmed: true,
  selectedAt: '2026-08-27T04:37:00Z',
  prescriptionCreated: false,
  transmitted: false,
}

const prescriptionCatalogItem = {
  rxNormCode: '860975',
  drugName: 'Metformin',
  displayName: 'Metformin 500 mg tablet',
  form: 'tablet',
  strength: '500 mg',
  route: 'oral',
}

const prescriptionPreparationWorkspace = {
  consultationId: consultationStart.consultationId,
  consultationStatus: 'MediaEnded',
  asOf: '2026-08-27T04:37:30Z',
  catalogSource: 'AvenChartSyntheticMedicationVocabulary',
  catalogDatasetId: 'avenchart-gold',
  catalogDatasetVersion: '2026.08.27',
  adapterMode: 'NON_PRODUCTION',
  canonicalModelVersion: 'AVENCHART_ERX_PREPARATION_V1',
  intendedStandard: 'NCPDP_SCRIPT_2017071',
  currentPharmacyChoiceVersion: null,
  catalogResults: [],
  currentDraft: null,
  safetyCheckEnabled: false,
  signingEnabled: false,
  prescriptionCreationEnabled: false,
  transmissionEnabled: false,
  patientDeliveryEnabled: false,
  completionEnabled: false,
  limitations: [
    'Catalog results are deterministic synthetic reference facts, not drug or dosing recommendations.',
    'This preparation draft has no interaction or contraindication check and is not a medication order or prescription.',
  ],
}

const prescriptionPreparationDraft = {
  version: 1,
  ...prescriptionCatalogItem,
  doseAmount: 1,
  doseUnit: 'tablet',
  frequency: 'once daily (synthetic)',
  quantityValue: 7,
  quantityUnit: 'tablet',
  durationDays: 7,
  refills: 0,
  indication: 'Synthetic indication authored by physician.',
  directions: 'Take one synthetic tablet once daily for seven days.',
  medicationListReviewed: true,
  allergyListReviewed: true,
  adequateEvaluationCompleted: true,
  pharmacyChoiceVersion: 1,
  recordedAt: '2026-08-27T04:37:45Z',
  legalEffect: false,
  safetyChecked: false,
  signed: false,
  transmissionQueued: false,
  transmitted: false,
  patientDelivered: false,
}

const safetyDispositionWorkspace = {
  consultationId: consultationStart.consultationId,
  consultationStatus: 'MediaEnded',
  asOf: '2026-08-27T04:37:05Z',
  dispositions: [
    { code: 'TreatedTelehealth', label: 'Treated by telehealth', requiresAdequateEvaluation: true, requiresLocationCallbackReconfirmation: false, requiresEmergencyFacts: false, requiresContactAttemptSummary: false },
    { code: 'NoTreatmentNeeded', label: 'No treatment needed', requiresAdequateEvaluation: true, requiresLocationCallbackReconfirmation: false, requiresEmergencyFacts: false, requiresContactAttemptSummary: false },
    { code: 'TestingOrReferralRequired', label: 'Testing or referral required', requiresAdequateEvaluation: true, requiresLocationCallbackReconfirmation: false, requiresEmergencyFacts: false, requiresContactAttemptSummary: false },
    { code: 'UrgentInPerson', label: 'Urgent in-person evaluation', requiresAdequateEvaluation: true, requiresLocationCallbackReconfirmation: true, requiresEmergencyFacts: false, requiresContactAttemptSummary: false },
    { code: 'EmergencyTransferRecommended', label: 'Emergency transfer recommended', requiresAdequateEvaluation: true, requiresLocationCallbackReconfirmation: true, requiresEmergencyFacts: true, requiresContactAttemptSummary: false },
    { code: 'TechnicalAbort', label: 'Technical abort', requiresAdequateEvaluation: false, requiresLocationCallbackReconfirmation: false, requiresEmergencyFacts: false, requiresContactAttemptSummary: true },
    { code: 'PatientLeft', label: 'Patient left', requiresAdequateEvaluation: false, requiresLocationCallbackReconfirmation: false, requiresEmergencyFacts: false, requiresContactAttemptSummary: true },
    { code: 'ClinicianUnableToComplete', label: 'Clinician unable to complete', requiresAdequateEvaluation: false, requiresLocationCallbackReconfirmation: false, requiresEmergencyFacts: false, requiresContactAttemptSummary: true },
  ],
  followUpOwners: ['Patient', 'Practice', 'TreatingPhysician', 'EmergencyServices', 'ExternalClinician', 'NoneClinicallyRequired'],
  communicationMethods: ['DiscussedDuringSyntheticConsultation', 'SyntheticCallback', 'NotYetCommunicated'],
  emergencyHandoffStatuses: ['RecommendedOnly', 'PatientCalling', 'PracticeCalling', 'Connected', 'UnableToConfirm'],
  currentDraft: null,
  signingEnabled: false,
  patientDeliveryEnabled: false,
  completionEnabled: false,
  limitations: [
    'This is an unsigned physician-authored synthetic safety draft. The application supplies no clinical instruction or recommendation.',
    'No patient delivery, AVS, signature, finalization, order, referral, prescription, claim, completion, or external handoff is created.',
  ],
}

const safetyDispositionDraft = {
  version: 1,
  dispositionCode: 'TreatedTelehealth',
  adequateEvaluationCompleted: true,
  followUpOwner: 'Patient',
  followUpTimeframe: 'within two synthetic days',
  nextStepInstructions: 'Physician-authored synthetic next step.',
  warningEscalationInstructions: 'Physician-authored synthetic warning and escalation instruction.',
  communicationMethod: 'DiscussedDuringSyntheticConsultation',
  communicationCompleted: true,
  locationCallbackReconfirmed: false,
  emergencyInstructionProvided: false,
  emergencyHandoffStatus: null,
  contactAttemptSummary: null,
  recordedAt: '2026-08-27T04:38:00Z',
  legalEffect: false,
  signed: false,
  finalized: false,
  patientDelivered: false,
}

const completionPrerequisitesReview = {
  consultationId: consultationStart.consultationId,
  consultationStatus: 'MediaEnded',
  requestStatus: 'WrapUp',
  shiftStatus: 'WrapUp',
  appointmentStatus: '>',
  asOf: '2026-08-27T04:38:05Z',
  documentation: {
    version: 2,
    hasAnyContent: true,
    subjectivePresent: true,
    objectivePresent: false,
    assessmentPresent: false,
    planPresent: true,
  },
  safetyDisposition: null,
  pharmacyChoice: null,
  structuralEvidencePresent: false,
  productBlockers: [
    'SAFETY_DISPOSITION_DRAFT_MISSING',
    'FINAL_CLINICAL_REVIEW_NOT_RECORDED',
    'SIGNATURE_FINALIZATION_NOT_IMPLEMENTED',
    'ATOMIC_DOWNSTREAM_OWNERSHIP_NOT_IMPLEMENTED',
  ],
  signingEnabled: false,
  completionEnabled: false,
  patientDeliveryEnabled: false,
  downstreamCreationEnabled: false,
  limitations: [
    'Field presence is structural evidence only; it does not establish clinical adequacy, accuracy, applicability, or readiness to sign.',
    'A pharmacy destination is optional and its presence does not imply that a medication or prescription exists.',
  ],
}

const readinessProjection = {
  requestId: intakeRequest.requestId,
  requestVersion: 3,
  status: 'Intake',
  patientDetails: {
    displayName: 'Synthetic Patient',
    dateOfBirth: '1990-01-01',
    email: 'synthetic-patient@example.test',
    phone: '(555) 555-0100',
    address: '100 Synthetic Avenue, Test City CA 90000',
    fingerprint: 'a'.repeat(64),
    missingFields: [],
  },
  clinicalSummary: { activeMedicationCount: 2, activeAllergyCount: 1, historyAvailable: true, fingerprint: 'b'.repeat(64) },
  coverageOptions: [{
    coverageToken: 'c'.repeat(64),
    coverageType: 'primary',
    provider: 'Harbor Mutual',
    planName: 'High Deductible',
    maskedPolicyNumber: '••••0012',
    maskedGroupNumber: '••••P111',
    subscriberRelationship: 'self',
    fingerprint: 'c'.repeat(64),
  }],
  acknowledgment: {
    kind: 'SyntheticDemonstrationAcknowledgment',
    packageKey: 'synthetic-ack',
    packageVersion: 1,
    contentHash: 'd'.repeat(64),
    title: 'Synthetic telehealth demonstration acknowledgment',
    statements: ['This is a demonstration.', 'This is not a production telehealth treatment consent.'],
    legalEffect: false,
  },
  blockingReasons: [],
}

async function signInPortal(page: Page) {
  await page.goto('/portal/login')
  await page.getByLabel('Email or username').fill(process.env.MODERN_UI_PORTAL_USERNAME ?? 'mod-pat-0004@example.test')
  await page.getByLabel('Password').fill(process.env.MODERN_UI_PORTAL_PASSWORD ?? 'PortalPass207!')
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page).toHaveURL(/\/portal\/home$/, { timeout: 20_000 })
}

async function signInClinician(page: Page, username: string) {
  await page.goto('/login')
  await page.getByLabel('Username').fill(username)
  await page.getByLabel('Password').fill(process.env.MODERN_UI_STAFF_PASSWORD ?? 'pass')
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page).toHaveURL(/\/clinician\/dashboard$/, { timeout: 20_000 })
}

async function expectNoSeriousAccessibilityViolations(page: Page) {
  const result = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze()
  expect(result.violations.filter(({ impact }) => impact === 'serious' || impact === 'critical')).toEqual([])
}

async function expectTelehealthReflow(page: Page) {
  await page.setViewportSize({ width: 320, height: 640 })
  await page.waitForTimeout(250)
  const dimensions = await page.evaluate(() => ({
    viewport: innerWidth,
    document: document.documentElement.scrollWidth,
    overflow: Array.from(document.querySelectorAll<HTMLElement>('body *'))
      .filter((element) => element.getBoundingClientRect().right > innerWidth + 1)
      .slice(0, 8)
      .map((element) => ({
        tag: element.tagName,
        className: element.className,
        right: Math.round(element.getBoundingClientRect().right),
        text: element.textContent?.trim().slice(0, 60),
      })),
  }))
  expect(dimensions.document, JSON.stringify(dimensions.overflow)).toBeLessThanOrEqual(dimensions.viewport)
}

async function installPassingMediaPreflight(page: Page) {
  await page.addInitScript(() => {
    Object.defineProperty(window, 'RTCPeerConnection', { configurable: true, value: function SyntheticPeerConnection() {} })
    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: {
        getUserMedia: async () => ({
          getTracks: () => [
            { kind: 'video', stop: () => { (window as typeof window & { __stoppedTracks?: number }).__stoppedTracks = ((window as typeof window & { __stoppedTracks?: number }).__stoppedTracks ?? 0) + 1 } },
            { kind: 'audio', stop: () => { (window as typeof window & { __stoppedTracks?: number }).__stoppedTracks = ((window as typeof window & { __stoppedTracks?: number }).__stoppedTracks ?? 0) + 1 } },
          ],
        }),
      },
    })
  })
}

test.describe('telehealth accessibility', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/telehealth/v1/context', (route) => route.fulfill({ json: practiceContext }))
  })

  test('public branded entry is keyboard operable and has no serious WCAG violations', async ({ page }) => {
    await page.goto('/telehealth')
    await expect(page.getByRole('heading', { name: 'Telehealth', exact: true })).toBeVisible()
    await expect(page.getByRole('link', { name: 'Call 911' })).toBeVisible()
    await page.getByRole('link', { name: 'Call 911' }).focus()
    await expect(page.getByRole('link', { name: 'Call 911' })).toBeFocused()
    await page.keyboard.press('Tab')
    const existingPatient = page.getByRole('link', { name: 'Sign in as an existing patient' })
    if (!await existingPatient.evaluate((element) => element === document.activeElement)) await existingPatient.focus()
    await expect(existingPatient).toBeFocused()

    await expectNoSeriousAccessibilityViolations(page)
  })

  test('public entry reflows without horizontal page overflow', async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 640 })
    await page.goto('/telehealth')
    await expect(page.getByRole('heading', { name: 'Telehealth', exact: true })).toBeVisible()
    const dimensions = await page.evaluate(() => ({ viewport: innerWidth, document: document.documentElement.scrollWidth }))
    expect(dimensions.document).toBeLessThanOrEqual(dimensions.viewport)
  })

  test('new-patient entry minimizes data, preserves recovery, and stops at privacy-safe identity review', async ({ page }) => {
    let createCalls = 0
    const createHeaders: Array<{ applicant: string | null; idempotency: string | null }> = []
    await page.route('**/api/telehealth/v1/applicants**', async (route) => {
      const request = route.request()
      const path = new URL(request.url()).pathname
      if (request.method() === 'POST' && path.endsWith('/applicants')) {
        createCalls += 1
        createHeaders.push({
          applicant: request.headers()['x-avenchart-telehealth-applicant-key'] ?? null,
          idempotency: request.headers()['x-idempotency-key'] ?? null,
        })
        if (createCalls === 1) {
          await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Synthetic applicant service unavailable.' }) })
          return
        }
        await route.fulfill({ status: 201, json: prospectiveApplicant })
        return
      }
      if (request.method() === 'POST' && path.endsWith('/contact-verification')) {
        await route.fulfill({ json: {
          ...prospectiveApplicant,
          status: 'IdentityReviewPending',
          version: 2,
          contactVerified: true,
          identityAssurance: 'ContactControlOnly',
          duplicateDisposition: 'PossibleMatchManualReview',
          verificationAttemptsRemaining: 0,
          demonstrationVerificationCode: null,
          nextAction: 'A possible existing record requires authorized manual identity review. No patient record was created or linked.',
        } })
        return
      }
      await route.fulfill({ json: prospectiveApplicant })
    })

    await page.goto('/telehealth/new')
    await expect(page.getByRole('heading', { name: 'Start as a new patient' })).toBeVisible()
    await expect(page.getByText(/Do not enter real personal, insurance, or health information/i)).toBeVisible()
    await expect(page.getByText(/Insurance and symptoms come later/i)).toBeVisible()
    await expect(page.getByLabel('Social Security number')).toHaveCount(0)
    const submit = page.getByRole('button', { name: 'Create synthetic applicant' })
    await expect(submit).toBeDisabled()

    await page.getByLabel('Legal first name').fill('Avery')
    await page.getByLabel('Legal last name').fill('Example')
    await page.getByLabel('Date of birth').fill('1990-03-14')
    await page.getByLabel('Synthetic email').fill('avery@example.test')
    await page.getByLabel('Synthetic phone').fill('404-555-0199')
    await page.getByLabel('State of residence').selectOption('GA')
    await page.getByLabel('Postal code').fill('30301')
    await page.getByLabel(/I confirm every value/).check()
    await submit.click()
    await expect(page.getByRole('alert')).toContainText('Synthetic applicant service unavailable.')
    await expect(page.getByLabel('Legal first name')).toHaveValue('Avery')

    await submit.click()
    await expect(page.getByText(/Demonstration code:/)).toContainText('246810')
    expect(createHeaders).toHaveLength(2)
    expect(createHeaders[0]).toEqual(createHeaders[1])
    expect(createHeaders[0].applicant).toMatch(/^[0-9a-f]{64}$/)
    const storedSession = await page.evaluate(() => sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant'))
    expect(storedSession).not.toContain('Avery')
    expect(storedSession).not.toContain('avery@example.test')

    await page.getByLabel('Six-digit demonstration code').fill('246810')
    await page.getByRole('button', { name: 'Verify synthetic contact' }).click()
    await expect(page.getByRole('heading', { name: 'Stopped safely at identity review' })).toBeVisible()
    await expect(page.getByText(/Possible existing record—manual review required/)).toBeVisible()
    await expect(page.getByText(/No canonical patient, chart, portal account, visit request, or queue entry was created/)).toBeVisible()
    await expect(page.getByText(/Arjun|Patel|match score/i)).toHaveCount(0)

    await expectNoSeriousAccessibilityViolations(page)
    await expectTelehealthReflow(page)
  })

  test('new-patient applicant session resumes with a masked status and keyboard restart', async ({ page }) => {
    await page.addInitScript((session) => {
      sessionStorage.setItem('avenchart-ui.telehealthProspectiveApplicant', JSON.stringify(session))
    }, { applicantId: prospectiveApplicant.applicantId, applicantAccessKey: 'd'.repeat(64) })
    await page.route('**/api/telehealth/v1/applicants/**', (route) => route.fulfill({ json: prospectiveApplicant }))

    await page.goto('/telehealth/new')
    await expect(page.getByText('a•••@example.test')).toBeVisible()
    await expect(page.getByText('(***) ***-0199')).toBeVisible()
    await expect(page.getByText('avery@example.test')).toHaveCount(0)
    const restart = page.getByRole('button', { name: 'Start again' })
    await restart.focus()
    await expect(restart).toBeFocused()
    await page.keyboard.press('Enter')
    await expect(page.getByRole('heading', { name: 'Minimum applicant details' })).toBeVisible()
    expect(await page.evaluate(() => sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant'))).toBeNull()

    await expectNoSeriousAccessibilityViolations(page)
    await expectTelehealthReflow(page)
  })

  test('authorized applicant creates one Draft request with stable retry, minimization, reflow, and no queue semantics', async ({ page }) => {
    await page.addInitScript((session) => {
      sessionStorage.setItem('avenchart-ui.telehealthProspectiveApplicant', JSON.stringify(session))
    }, { applicantId: prospectiveApplicant.applicantId, applicantAccessKey: 'g'.repeat(64) })
    const authorizedApplicant = {
      ...prospectiveApplicant,
      status: 'SyntheticPracticeReviewAuthorized',
      version: 25,
      contactVerified: true,
      identityAssurance: 'ContactControlOnly',
      duplicateDisposition: 'NoCandidate',
      canonicalPatientCreated: true,
      verificationAttemptsRemaining: 0,
      demonstrationVerificationCode: null,
      nextAction: 'Confirm the authorized synthetic request creation.',
    }
    const readyRequest = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 25,
      applicantStatus: 'SyntheticPracticeReviewAuthorized',
      policyKey: 'SYNTHETIC_APPLICANT_TELEHEALTH_REQUEST_CREATION',
      policyVersion: 1,
      authorizationPolicyVersion: 1,
      requestCreationReady: true,
      requestCreated: false,
      requestId: null,
      requestStatus: null,
      requestVersion: null,
      complaintCategory: 'migraine',
      createdAt: null,
      telehealthRequestCreated: false,
      patientContacted: false,
      patientCareQueueEntered: false,
      clinicianQueueEntered: false,
      doctorSearchStarted: false,
      queuePositionAssigned: false,
      appointmentCreated: false,
      encounterCreated: false,
      consentCreated: false,
      careAuthorized: false,
      prescribingEnabled: false,
      billingEnabled: false,
      claimCreated: false,
      integrationEnabled: false,
      externalCallPerformed: false,
      direction: 'Confirm the boundary to create one Draft request.',
      limitations: ['No queue, doctor search, appointment, encounter, consent, or care is created.'],
    }
    const createdRequest = {
      ...readyRequest,
      applicantVersion: 26,
      applicantStatus: 'SyntheticRequestCreated',
      requestCreationReady: false,
      requestCreated: true,
      requestId: '1e000000-0000-4000-8000-00000000001e',
      requestStatus: 'Draft',
      requestVersion: 1,
      createdAt: '2026-08-28T16:00:00Z',
      telehealthRequestCreated: true,
      direction: 'The Draft request was created; no doctor search or queue exists.',
    }
    let creationCalls = 0
    let requestCreated = false
    const creationKeys: Array<string | undefined> = []
    const creationBodies: Array<Record<string, unknown>> = []
    await page.route('**/api/telehealth/v1/applicants/**', async (route) => {
      const request = route.request()
      const path = new URL(request.url()).pathname
      if (path.endsWith('/telehealth-request')) {
        expect(request.headers()['x-avenchart-telehealth-applicant-key']).toBe('g'.repeat(64))
        if (request.method() === 'POST') {
          creationCalls += 1
          creationKeys.push(request.headers()['x-idempotency-key'])
          creationBodies.push(request.postDataJSON() as Record<string, unknown>)
          if (creationCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Creation result unknown; retry unchanged.' }) })
            return
          }
          requestCreated = true
          await route.fulfill({ status: 201, json: createdRequest })
          return
        }
        await route.fulfill({ json: requestCreated ? createdRequest : readyRequest })
        return
      }
      await route.fulfill({ json: authorizedApplicant })
    })

    await page.goto('/telehealth/new')
    const heading = page.getByRole('heading', { name: 'Create the authorized Draft request' })
    await expect(heading).toBeVisible()
    await expect(heading.locator('..')).toContainText('Doctor search or queueNot started')
    await expect(heading.locator('..').locator('input[type="text"]')).toHaveCount(0)
    await expect(heading.locator('..').locator('textarea')).toHaveCount(0)
    await page.getByLabel(/create this one synthetic Draft/i).check()
    await page.getByLabel(/does not start a doctor search/i).check()
    await page.getByLabel(/urgent or worsening/i).check()
    const create = page.getByRole('button', { name: 'Create Draft telehealth request' })
    await create.click()
    await expect(page.getByRole('alert')).toContainText('Creation result unknown; retry unchanged.')
    await expect(page.getByLabel(/create this one synthetic Draft/i)).toBeChecked()
    await create.click()

    const result = page.getByRole('heading', { name: 'Draft telehealth request created' })
    await expect(result).toBeVisible()
    await expect(result.locator('..')).toContainText('Request statusDraft')
    await expect(result.locator('..')).toContainText('Doctor search startedNo')
    await expect(result.locator('..')).toContainText('Patient or clinician queue enteredNo')
    await expect(result.locator('..')).toContainText('Queue position assignedNo')
    expect(creationKeys).toHaveLength(2)
    expect(creationKeys[0]).toBe(creationKeys[1])
    expect(creationBodies[0]).toEqual({
      expectedApplicantVersion: 25,
      authorizationPolicyVersion: 1,
      requestCreationConfirmed: true,
      noQueueOrCareAcknowledged: true,
      urgentOrWorseningSymptomsRequireImmediateActionAcknowledged: true,
    })
    expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toMatch(/requestId|complaintCategory|authorizationPolicyVersion|patientId/i)

    await expectNoSeriousAccessibilityViolations(page)
    await expectTelehealthReflow(page)
  })

  test('applicant confirms current location and masked callback with stable retry, reflow, and no downstream care semantics', async ({ page }) => {
    await page.addInitScript((session) => {
      sessionStorage.setItem('avenchart-ui.telehealthProspectiveApplicant', JSON.stringify(session))
    }, { applicantId: prospectiveApplicant.applicantId, applicantAccessKey: 'h'.repeat(64) })
    const requestCreatedApplicant = {
      ...prospectiveApplicant,
      status: 'SyntheticRequestCreated',
      version: 26,
      contactVerified: true,
      identityAssurance: 'ContactControlOnly',
      duplicateDisposition: 'NoCandidate',
      canonicalPatientCreated: true,
      verificationAttemptsRemaining: 0,
      demonstrationVerificationCode: null,
      nextAction: 'Confirm current physical location and callback.',
    }
    const requestReceipt = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 26,
      applicantStatus: 'SyntheticRequestCreated',
      policyKey: 'SYNTHETIC_APPLICANT_TELEHEALTH_REQUEST_CREATION',
      policyVersion: 1,
      authorizationPolicyVersion: 1,
      requestCreationReady: false,
      requestCreated: true,
      requestId: '1e000000-0000-4000-8000-00000000001e',
      requestStatus: 'Draft',
      requestVersion: 1,
      complaintCategory: 'migraine',
      createdAt: '2026-08-28T16:00:00Z',
      telehealthRequestCreated: true,
      patientContacted: false,
      patientCareQueueEntered: false,
      clinicianQueueEntered: false,
      doctorSearchStarted: false,
      queuePositionAssigned: false,
      appointmentCreated: false,
      encounterCreated: false,
      consentCreated: false,
      careAuthorized: false,
      prescribingEnabled: false,
      billingEnabled: false,
      claimCreated: false,
      integrationEnabled: false,
      externalCallPerformed: false,
      direction: 'The Draft request was created; no doctor search or queue exists.',
      limitations: ['No queue or care exists.'],
    }
    const readyLocation = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 26,
      applicantStatus: 'SyntheticRequestCreated',
      requestId: requestReceipt.requestId,
      requestVersion: 1,
      requestStatus: 'Draft',
      policyKey: 'SYNTHETIC_APPLICANT_REQUEST_LOCATION_CONFIRMATION',
      policyVersion: 1,
      contextSnapshotFingerprint: '2'.repeat(64),
      currentLocationStateCode: 'GA',
      maskedCallbackPhone: '***-***-0199',
      confirmationReady: true,
      locationConfirmed: false,
      confirmedAt: null,
      triageAssessmentCreated: false,
      clinicalReviewCreated: false,
      patientContacted: false,
      patientCareQueueEntered: false,
      clinicianQueueEntered: false,
      doctorSearchStarted: false,
      queuePositionAssigned: false,
      appointmentCreated: false,
      encounterCreated: false,
      consentCreated: false,
      careAuthorized: false,
      prescribingEnabled: false,
      billingEnabled: false,
      claimCreated: false,
      integrationEnabled: false,
      externalCallPerformed: false,
      direction: 'Confirm the current physical location and masked callback route.',
      limitations: ['No triage, queue, appointment, encounter, consent, or care is created.'],
    }
    const confirmedLocation = {
      ...readyLocation,
      requestVersion: 2,
      requestStatus: 'LocationConfirmed',
      confirmationReady: false,
      locationConfirmed: true,
      confirmedAt: '2026-08-28T17:00:00Z',
      direction: 'Location and callback are confirmed; no triage or care workflow was created.',
    }
    let loadCalls = 0
    let confirmationCalls = 0
    const confirmationKeys: Array<string | undefined> = []
    const confirmationBodies: Array<Record<string, unknown>> = []
    await page.route('**/api/telehealth/v1/applicants/**', async (route) => {
      const request = route.request()
      const path = new URL(request.url()).pathname
      expect(request.headers()['x-avenchart-telehealth-applicant-key']).toBe('h'.repeat(64))
      if (path.endsWith('/telehealth-request/location')) {
        if (request.method() === 'POST') {
          confirmationCalls += 1
          confirmationKeys.push(request.headers()['x-idempotency-key'])
          confirmationBodies.push(request.postDataJSON() as Record<string, unknown>)
          if (confirmationCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Location confirmation result unknown; retry unchanged.' }) })
            return
          }
          await route.fulfill({ json: confirmedLocation })
          return
        }
        loadCalls += 1
        if (loadCalls === 1) {
          await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Location confirmation temporarily unavailable.' }) })
          return
        }
        await route.fulfill({ json: readyLocation })
        return
      }
      if (path.endsWith('/telehealth-request')) {
        await route.fulfill({ json: requestReceipt })
        return
      }
      await route.fulfill({ json: requestCreatedApplicant })
    })

    await page.goto('/telehealth/new')
    await expect(page.getByRole('alert')).toContainText('Location confirmation temporarily unavailable.')
    await page.getByRole('button', { name: 'Retry location-confirmation load' }).click()
    const heading = page.getByRole('heading', { name: 'Confirm where you are now' })
    await expect(heading).toBeVisible()
    await expect(heading.locator('..')).toContainText('Callback number***-***-0199')
    await expect(heading.locator('..')).toContainText(/changed, stop here/i)
    await expect(heading.locator('..').locator('input[type="tel"]')).toHaveCount(0)
    await expect(heading.locator('..').locator('input[type="text"]')).toHaveCount(0)
    await expect(heading.locator('..').locator('textarea')).toHaveCount(0)
    await page.getByLabel('Current physical location', { exact: true }).selectOption('GA')
    await page.getByLabel(/state I selected is my current physical location/i).check()
    await page.getByLabel(/masked callback number remains correct/i).check()
    await page.getByLabel(/changed state or callback route requires/i).check()
    await page.getByLabel(/urgent or worsening/i).check()
    const confirm = page.getByRole('button', { name: 'Confirm location and callback' })
    await confirm.click()
    await expect(page.getByRole('alert')).toContainText('Location confirmation result unknown; retry unchanged.')
    await expect(page.getByLabel(/state I selected is my current physical location/i)).toBeChecked()
    await confirm.click()

    const result = page.getByRole('heading', { name: 'Location and callback confirmed' })
    await expect(result).toBeVisible()
    await expect(result.locator('..')).toContainText('Request statusLocationConfirmed')
    await expect(result.locator('..')).toContainText('Request version2')
    await expect(result.locator('..')).toContainText('Triage assessment createdNo')
    await expect(result.locator('..')).toContainText('Doctor search startedNo')
    await expect(result.locator('..')).toContainText('Patient or clinician queue enteredNo')
    expect(confirmationKeys).toHaveLength(2)
    expect(confirmationKeys[0]).toBe(confirmationKeys[1])
    expect(confirmationBodies[0]).toEqual({
      expectedRequestVersion: 1,
      contextSnapshotFingerprint: '2'.repeat(64),
      currentLocationStateCode: 'GA',
      currentLocationConfirmed: true,
      callbackNumberConfirmed: true,
      changedLocationRequiresRestartAcknowledged: true,
      urgentOrWorseningSymptomsRequireImmediateActionAcknowledged: true,
    })
    expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toMatch(/contextSnapshotFingerprint|maskedCallbackPhone|requestId|currentLocationStateCode|patientId/i)

    await expectNoSeriousAccessibilityViolations(page)
    await expectTelehealthReflow(page)
  })

  test('applicant completes the request universal safety screen with stable retry, emergency direction, and no downstream care semantics', async ({ page }) => {
    await page.addInitScript((session) => {
      sessionStorage.setItem('avenchart-ui.telehealthProspectiveApplicant', JSON.stringify(session))
    }, { applicantId: prospectiveApplicant.applicantId, applicantAccessKey: 'i'.repeat(64) })
    const requestCreatedApplicant = {
      ...prospectiveApplicant,
      status: 'SyntheticRequestCreated',
      version: 26,
      contactVerified: true,
      identityAssurance: 'ContactControlOnly',
      duplicateDisposition: 'NoCandidate',
      canonicalPatientCreated: true,
      verificationAttemptsRemaining: 0,
      demonstrationVerificationCode: null,
      nextAction: 'Complete the bounded synthetic universal safety screen.',
    }
    const requestReceipt = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 26,
      applicantStatus: 'SyntheticRequestCreated',
      policyKey: 'SYNTHETIC_APPLICANT_TELEHEALTH_REQUEST_CREATION',
      policyVersion: 1,
      authorizationPolicyVersion: 1,
      requestCreationReady: false,
      requestCreated: true,
      requestId: '1e000000-0000-4000-8000-00000000001e',
      requestStatus: 'LocationConfirmed',
      requestVersion: 2,
      complaintCategory: 'migraine',
      createdAt: '2026-08-28T16:00:00Z',
      telehealthRequestCreated: true,
      patientContacted: false,
      patientCareQueueEntered: false,
      clinicianQueueEntered: false,
      doctorSearchStarted: false,
      queuePositionAssigned: false,
      appointmentCreated: false,
      encounterCreated: false,
      consentCreated: false,
      careAuthorized: false,
      prescribingEnabled: false,
      billingEnabled: false,
      claimCreated: false,
      integrationEnabled: false,
      externalCallPerformed: false,
      direction: 'The Draft request was created; no doctor search or queue exists.',
      limitations: ['No queue or care exists.'],
    }
    const confirmedLocation = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 26,
      applicantStatus: 'SyntheticRequestCreated',
      requestId: requestReceipt.requestId,
      requestVersion: 2,
      requestStatus: 'LocationConfirmed',
      policyKey: 'SYNTHETIC_APPLICANT_REQUEST_LOCATION_CONFIRMATION',
      policyVersion: 1,
      contextSnapshotFingerprint: '2'.repeat(64),
      currentLocationStateCode: 'GA',
      maskedCallbackPhone: '***-***-0199',
      confirmationReady: false,
      locationConfirmed: true,
      confirmedAt: '2026-08-28T17:00:00Z',
      triageAssessmentCreated: false,
      clinicalReviewCreated: false,
      patientContacted: false,
      patientCareQueueEntered: false,
      clinicianQueueEntered: false,
      doctorSearchStarted: false,
      queuePositionAssigned: false,
      appointmentCreated: false,
      encounterCreated: false,
      consentCreated: false,
      careAuthorized: false,
      prescribingEnabled: false,
      billingEnabled: false,
      claimCreated: false,
      integrationEnabled: false,
      externalCallPerformed: false,
      direction: 'Location and callback are confirmed; no triage or care workflow was created.',
      limitations: ['No triage, queue, appointment, encounter, consent, or care is created.'],
    }
    const readySafety = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 26,
      applicantStatus: 'SyntheticRequestCreated',
      requestId: requestReceipt.requestId,
      requestVersion: 2,
      requestStatus: 'LocationConfirmed',
      policyKey: 'SYNTHETIC_APPLICANT_REQUEST_UNIVERSAL_SAFETY_ASSESSMENT',
      policyVersion: 1,
      protocolKey: 'synthetic-universal-safety',
      protocolVersion: 1,
      contextSnapshotFingerprint: '3'.repeat(64),
      contextExpiresAt: '2026-08-28T17:30:00Z',
      currentLocationStateCode: 'GA',
      maskedCallbackPhone: '***-***-0199',
      assessmentReady: true,
      assessmentCreated: false,
      outcome: null,
      publicDisposition: null,
      evaluatedAt: null,
      universalSafetyPassed: false,
      complaintSpecificTriageRequired: false,
      complaintSpecificTriageCreated: false,
      clinicalReviewRequired: false,
      clinicalReviewCreated: false,
      terminalForTelehealth: false,
      patientContacted: false,
      patientCareQueueEntered: false,
      clinicianQueueEntered: false,
      doctorSearchStarted: false,
      queuePositionAssigned: false,
      appointmentCreated: false,
      encounterCreated: false,
      consentCreated: false,
      careAuthorized: false,
      prescribingEnabled: false,
      billingEnabled: false,
      claimCreated: false,
      integrationEnabled: false,
      externalCallPerformed: false,
      direction: 'Answer every universal safety question.',
      limitations: ['This immutable synthetic fixture is not approved clinical content.'],
    }
    const emergencyResult = {
      ...readySafety,
      requestVersion: 3,
      requestStatus: 'EmergencyRedirected',
      assessmentReady: false,
      assessmentCreated: true,
      outcome: 'Emergency',
      publicDisposition: 'EmergencyCareNow',
      evaluatedAt: '2026-08-28T17:05:00Z',
      terminalForTelehealth: true,
      direction: 'Call 911 now. This application did not dispatch emergency services.',
    }
    let safetyLoadCalls = 0
    let assessmentCalls = 0
    const assessmentKeys: Array<string | undefined> = []
    const assessmentBodies: Array<Record<string, unknown>> = []
    await page.route('**/api/telehealth/v1/applicants/**', async (route) => {
      const request = route.request()
      const path = new URL(request.url()).pathname
      expect(request.headers()['x-avenchart-telehealth-applicant-key']).toBe('i'.repeat(64))
      if (path.endsWith('/telehealth-request/safety')) {
        if (request.method() === 'POST') {
          assessmentCalls += 1
          assessmentKeys.push(request.headers()['x-idempotency-key'])
          assessmentBodies.push(request.postDataJSON() as Record<string, unknown>)
          if (assessmentCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Safety result unknown; retry unchanged.' }) })
            return
          }
          await route.fulfill({ json: emergencyResult })
          return
        }
        safetyLoadCalls += 1
        if (safetyLoadCalls === 1) {
          await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Safety screen temporarily unavailable.' }) })
          return
        }
        await route.fulfill({ json: readySafety })
        return
      }
      if (path.endsWith('/telehealth-request/location')) {
        await route.fulfill({ json: confirmedLocation })
        return
      }
      if (path.endsWith('/telehealth-request')) {
        await route.fulfill({ json: requestReceipt })
        return
      }
      await route.fulfill({ json: requestCreatedApplicant })
    })

    await page.goto('/telehealth/new')
    await expect(page.getByRole('alert')).toContainText('Safety screen temporarily unavailable.')
    await page.getByRole('button', { name: 'Retry universal safety-screen load' }).click()
    const heading = page.getByRole('heading', { name: 'Request universal safety screen' })
    await expect(heading).toBeVisible()
    await expect(heading.locator('..')).toContainText('Callback number***-***-0199')
    await expect(heading.locator('..')).toContainText(/not approved clinical content/i)
    await expect(heading.locator('..').locator('input[type="text"]')).toHaveCount(0)
    await expect(heading.locator('..').locator('textarea')).toHaveCount(0)

    await page.getByRole('group', { name: 'Could any current symptom be an emergency?' }).getByLabel('Yes').check()
    await expect(page.getByRole('alert')).toContainText(/Call 911 now.*has not contacted or dispatched/i)
    await page.getByRole('group', { name: 'Are symptoms severe or getting worse quickly?' }).getByLabel('No').check()
    await page.getByRole('group', { name: 'Does this seem to require a hands-on examination or procedure?' }).getByLabel('No').check()
    await page.getByRole('group', { name: 'Are you unsure about any answer above?' }).getByLabel('No').check()
    await page.getByLabel('I confirm the displayed state is my current physical location.').check()
    await page.getByLabel('I confirm the displayed masked callback number remains correct.').check()
    await page.getByLabel('I confirm every answer is fictional synthetic demonstration data.').check()
    const evaluate = page.getByRole('button', { name: 'Evaluate request universal safety screen' })
    await evaluate.click()
    await expect(page.getByRole('alert').filter({ hasText: 'Safety result unknown; retry unchanged.' })).toBeVisible()
    await expect(page.getByRole('group', { name: 'Could any current symptom be an emergency?' }).getByLabel('Yes')).toBeChecked()
    await evaluate.click()

    const result = page.getByRole('heading', { name: 'Universal safety screen stopped progression' })
    await expect(result).toBeVisible()
    await expect(result.locator('..')).toContainText('Request statusEmergencyRedirected')
    await expect(result.locator('..')).toContainText('Public dispositionEmergencyCareNow')
    await expect(result.locator('..')).toContainText('Doctor search or queueNot started')
    await expect(result.locator('..')).toContainText(/No submitted safety answer or answer fingerprint is returned/i)
    await expect(result.locator('..').getByRole('link', { name: 'Call 911' })).toHaveAttribute('href', 'tel:911')
    expect(assessmentKeys).toHaveLength(2)
    expect(assessmentKeys[0]).toBe(assessmentKeys[1])
    expect(assessmentBodies[0]).toEqual({
      expectedRequestVersion: 2,
      contextSnapshotFingerprint: '3'.repeat(64),
      currentLocationStateCode: 'GA',
      currentLocationConfirmed: true,
      callbackNumberConfirmed: true,
      syntheticDataConfirmed: true,
      hasEmergencyWarning: true,
      severeOrWorsening: false,
      requiresHandsOnExam: false,
      unsure: false,
    })
    expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toMatch(/contextSnapshotFingerprint|hasEmergencyWarning|severeOrWorsening|requiresHandsOnExam|answerFingerprint|requestId/i)

    await expectNoSeriousAccessibilityViolations(page)
    await expectTelehealthReflow(page)
  })

  test('applicant completes sleep complaint triage with coded answers, stable retry, crisis direction, and a closed publication gate', async ({ page }) => {
    await page.addInitScript((session) => {
      sessionStorage.setItem('avenchart-ui.telehealthProspectiveApplicant', JSON.stringify(session))
    }, { applicantId: prospectiveApplicant.applicantId, applicantAccessKey: 'q'.repeat(64) })
    const requestCreatedApplicant = {
      ...prospectiveApplicant,
      status: 'SyntheticRequestCreated',
      version: 26,
      contactVerified: true,
      identityAssurance: 'ContactControlOnly',
      duplicateDisposition: 'NoCandidate',
      canonicalPatientCreated: true,
      verificationAttemptsRemaining: 0,
      demonstrationVerificationCode: null,
      nextAction: 'Complete the bounded synthetic complaint-specific triage screen.',
    }
    const requestReceipt = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 26,
      applicantStatus: 'SyntheticRequestCreated',
      policyKey: 'SYNTHETIC_APPLICANT_TELEHEALTH_REQUEST_CREATION',
      policyVersion: 1,
      authorizationPolicyVersion: 1,
      requestCreationReady: false,
      requestCreated: true,
      requestId: '43000000-0000-4000-8000-000000000043',
      requestStatus: 'SafetyScreening',
      requestVersion: 3,
      complaintCategory: 'sleep',
      createdAt: '2026-08-28T16:00:00Z',
      telehealthRequestCreated: true,
      patientContacted: false,
      patientCareQueueEntered: false,
      clinicianQueueEntered: false,
      doctorSearchStarted: false,
      queuePositionAssigned: false,
      appointmentCreated: false,
      encounterCreated: false,
      consentCreated: false,
      careAuthorized: false,
      prescribingEnabled: false,
      billingEnabled: false,
      claimCreated: false,
      integrationEnabled: false,
      externalCallPerformed: false,
      direction: 'Complaint-specific triage remains required.',
      limitations: ['No queue or care exists.'],
    }
    const confirmedLocation = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 26,
      applicantStatus: 'SyntheticRequestCreated',
      requestId: requestReceipt.requestId,
      requestVersion: 2,
      requestStatus: 'LocationConfirmed',
      policyKey: 'SYNTHETIC_APPLICANT_REQUEST_LOCATION_CONFIRMATION',
      policyVersion: 1,
      contextSnapshotFingerprint: '2'.repeat(64),
      currentLocationStateCode: 'GA',
      maskedCallbackPhone: '***-***-0199',
      confirmationReady: false,
      locationConfirmed: true,
      confirmedAt: '2026-08-28T17:00:00Z',
      triageAssessmentCreated: false,
      clinicalReviewCreated: false,
      patientContacted: false,
      patientCareQueueEntered: false,
      clinicianQueueEntered: false,
      doctorSearchStarted: false,
      queuePositionAssigned: false,
      appointmentCreated: false,
      encounterCreated: false,
      consentCreated: false,
      careAuthorized: false,
      prescribingEnabled: false,
      billingEnabled: false,
      claimCreated: false,
      integrationEnabled: false,
      externalCallPerformed: false,
      direction: 'Location and callback are confirmed.',
      limitations: ['No triage, queue, appointment, encounter, consent, or care is created.'],
    }
    const universalPass = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 26,
      applicantStatus: 'SyntheticRequestCreated',
      requestId: requestReceipt.requestId,
      requestVersion: 3,
      requestStatus: 'SafetyScreening',
      policyKey: 'SYNTHETIC_APPLICANT_REQUEST_UNIVERSAL_SAFETY_ASSESSMENT',
      policyVersion: 1,
      protocolKey: 'synthetic-universal-safety',
      protocolVersion: 1,
      contextSnapshotFingerprint: '3'.repeat(64),
      contextExpiresAt: '2026-08-28T17:30:00Z',
      currentLocationStateCode: 'GA',
      maskedCallbackPhone: '***-***-0199',
      assessmentReady: false,
      assessmentCreated: true,
      outcome: 'TelehealthEligible',
      publicDisposition: 'UniversalSafetyPassed',
      evaluatedAt: '2026-08-28T17:05:00Z',
      universalSafetyPassed: true,
      complaintSpecificTriageRequired: true,
      complaintSpecificTriageCreated: false,
      clinicalReviewRequired: false,
      clinicalReviewCreated: false,
      terminalForTelehealth: false,
      patientContacted: false,
      patientCareQueueEntered: false,
      clinicianQueueEntered: false,
      doctorSearchStarted: false,
      queuePositionAssigned: false,
      appointmentCreated: false,
      encounterCreated: false,
      consentCreated: false,
      careAuthorized: false,
      prescribingEnabled: false,
      billingEnabled: false,
      claimCreated: false,
      integrationEnabled: false,
      externalCallPerformed: false,
      direction: 'Complaint-specific triage is still required.',
      limitations: ['The universal result is not complete eligibility.'],
    }
    const readyComplaintTriage = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 26,
      applicantStatus: 'SyntheticRequestCreated',
      requestId: requestReceipt.requestId,
      requestVersion: 3,
      requestStatus: 'SafetyScreening',
      complaintCategory: 'sleep',
      policyKey: 'SYNTHETIC_APPLICANT_REQUEST_COMPLAINT_TRIAGE',
      policyVersion: 1,
      protocolKey: 'synthetic-sleep-complaint-triage',
      protocolVersion: 1,
      engineVersion: 'synthetic-complaint-triage-engine-v1',
      clinicalContentStatus: 'UNAPPROVED_SYNTHETIC',
      medicalDirectorApprovalRequired: true,
      medicalDirectorApprovalRecorded: false,
      clinicalGoldenCasePackApproved: false,
      productionPublicationAllowed: false,
      contextSnapshotFingerprint: '4'.repeat(64),
      contextExpiresAt: '2026-08-28T17:30:00Z',
      currentLocationStateCode: 'GA',
      maskedCallbackPhone: '***-***-0199',
      assessmentReady: true,
      assessmentCreated: false,
      outcome: null,
      publicDisposition: null,
      evaluatedAt: null,
      syntheticVideoEvaluationCandidate: false,
      clinicalReviewRequired: false,
      clinicalReviewCreated: false,
      terminalForTelehealth: false,
      intakeSnapshotCreated: false,
      patientContacted: false,
      patientCareQueueEntered: false,
      clinicianQueueEntered: false,
      doctorSearchStarted: false,
      queuePositionAssigned: false,
      appointmentCreated: false,
      encounterCreated: false,
      consentCreated: false,
      careAuthorized: false,
      prescribingEnabled: false,
      billingEnabled: false,
      claimCreated: false,
      integrationEnabled: false,
      externalCallPerformed: false,
      direction: 'Answer every coded synthetic question.',
      limitations: ['Medical-director approval and production publication remain closed.'],
    }
    const emergencyResult = {
      ...readyComplaintTriage,
      requestVersion: 4,
      requestStatus: 'EmergencyRedirected',
      assessmentReady: false,
      assessmentCreated: true,
      outcome: 'Emergency',
      publicDisposition: 'EmergencyCareNow',
      evaluatedAt: '2026-08-28T17:08:00Z',
      terminalForTelehealth: true,
      direction: 'Call 911 for immediate danger, or call or text 988 for crisis support. This application did not contact emergency or crisis services.',
    }
    let complaintLoadCalls = 0
    let assessmentCalls = 0
    const assessmentKeys: Array<string | undefined> = []
    const assessmentBodies: Array<Record<string, unknown>> = []
    await page.route('**/api/telehealth/v1/applicants/**', async (route) => {
      const request = route.request()
      const path = new URL(request.url()).pathname
      expect(request.headers()['x-avenchart-telehealth-applicant-key']).toBe('q'.repeat(64))
      if (path.endsWith('/telehealth-request/complaint-triage')) {
        if (request.method() === 'POST') {
          assessmentCalls += 1
          assessmentKeys.push(request.headers()['x-idempotency-key'])
          assessmentBodies.push(request.postDataJSON() as Record<string, unknown>)
          if (assessmentCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Complaint result unknown; retry unchanged.' }) })
            return
          }
          await route.fulfill({ json: emergencyResult })
          return
        }
        complaintLoadCalls += 1
        if (complaintLoadCalls === 1) {
          await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Complaint fixture temporarily unavailable.' }) })
          return
        }
        await route.fulfill({ json: readyComplaintTriage })
        return
      }
      if (path.endsWith('/telehealth-request/safety')) {
        await route.fulfill({ json: universalPass })
        return
      }
      if (path.endsWith('/telehealth-request/location')) {
        await route.fulfill({ json: confirmedLocation })
        return
      }
      if (path.endsWith('/telehealth-request')) {
        await route.fulfill({ json: requestReceipt })
        return
      }
      await route.fulfill({ json: requestCreatedApplicant })
    })

    await page.goto('/telehealth/new')
    await expect(page.getByRole('alert')).toContainText('Complaint fixture temporarily unavailable.')
    await page.getByRole('button', { name: 'Retry complaint-triage load' }).click()
    const heading = page.getByRole('heading', { name: 'Sleep difficulty synthetic triage' })
    await expect(heading).toBeVisible()
    await expect(heading.locator('..')).toContainText('Server-owned categorysleep')
    await expect(heading.locator('..')).toContainText('UNAPPROVED_SYNTHETIC')
    await expect(heading.locator('..').locator('input[type="text"]')).toHaveCount(0)
    await expect(heading.locator('..').locator('textarea')).toHaveCount(0)

    await page.getByRole('group', { name: 'Does the fictional scenario include thoughts of self-harm or suicide?' }).getByLabel('Yes', { exact: true }).check()
    await expect(page.getByRole('alert')).toContainText(/call 911.*988.*has not contacted/i)
    await page.getByRole('group', { name: 'Does the fictional scenario include possible mania or psychosis?' }).getByLabel('No', { exact: true }).check()
    await page.getByRole('group', { name: 'Could sleepiness make driving, work, or another activity unsafe in this fictional scenario?' }).getByLabel('No', { exact: true }).check()
    await page.getByRole('group', { name: 'Is withdrawal from alcohol, medication, or another substance a concern in this fictional scenario?' }).getByLabel('No', { exact: true }).check()
    await page.getByRole('group', { name: 'Are breathing pauses or severe snoring reported in this fictional scenario?' }).getByLabel('Not sure', { exact: true }).check()
    await page.getByRole('group', { name: 'Does the fictional scenario include pregnancy or a complex medication concern?' }).getByLabel('No', { exact: true }).check()
    await page.getByRole('group', { name: 'Is the fictional request specifically for a controlled sedative?' }).getByLabel('No', { exact: true }).check()
    await page.getByRole('group', { name: 'Is uncomplicated sleep difficulty the only concern in this fictional scenario?' }).getByLabel('Yes', { exact: true }).check()
    await page.getByLabel('I confirm the displayed state remains the current physical location.').check()
    await page.getByLabel('I confirm the displayed masked callback number remains correct.').check()
    await page.getByLabel('I confirm every answer is fictional synthetic demonstration data.').check()
    const evaluate = page.getByRole('button', { name: 'Evaluate synthetic complaint triage' })
    await evaluate.click()
    await expect(page.getByRole('alert').filter({ hasText: 'Complaint result unknown; retry unchanged.' })).toBeVisible()
    await expect(page.getByRole('group', { name: 'Does the fictional scenario include thoughts of self-harm or suicide?' }).getByLabel('Yes', { exact: true })).toBeChecked()
    await evaluate.click()

    const result = page.getByRole('heading', { name: 'Complaint-specific triage stopped progression' })
    await expect(result).toBeVisible()
    await expect(result.locator('..')).toContainText('Request statusEmergencyRedirected')
    await expect(result.locator('..')).toContainText('Medical-director approval recordedNo')
    await expect(result.locator('..')).toContainText('Clinical golden cases approvedNo')
    await expect(result.locator('..')).toContainText('Production publication allowedNo')
    await expect(result.locator('..')).toContainText('Doctor search or queueNot started')
    await expect(result.locator('..')).toContainText(/No submitted answer, answer fingerprint, fired rule, or reason code is returned/i)
    await expect(result.locator('..').getByRole('link', { name: 'Call 911' })).toHaveAttribute('href', 'tel:911')
    await expect(result.locator('..').getByRole('link', { name: 'Call 988' })).toHaveAttribute('href', 'tel:988')
    expect(assessmentKeys).toHaveLength(2)
    expect(assessmentKeys[0]).toBe(assessmentKeys[1])
    expect(assessmentBodies[0]).toEqual({
      expectedRequestVersion: 3,
      contextSnapshotFingerprint: '4'.repeat(64),
      currentLocationStateCode: 'GA',
      currentLocationConfirmed: true,
      callbackNumberConfirmed: true,
      syntheticDataConfirmed: true,
      migraine: null,
      sleep: {
        selfHarmThoughts: 'Yes',
        maniaOrPsychosis: 'No',
        dangerousSomnolence: 'No',
        withdrawalConcern: 'No',
        breathingPausesOrSevereSnoring: 'NotSure',
        pregnantOrComplexMedicationConcern: 'No',
        controlledSedativeRequest: 'No',
        uncomplicatedSleepDifficulty: 'Yes',
      },
    })
    expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toMatch(/contextSnapshotFingerprint|selfHarmThoughts|NotSure|answerFingerprint|firedRule|reasonCode|requestId/i)

    await expectNoSeriousAccessibilityViolations(page)
    await expectTelehealthReflow(page)
  })

  test('eligible synthetic applicant confirms intake, source, fresh eligibility, and practice network with stable retries and no downstream implication', async ({ page }) => {
    await page.addInitScript((session) => {
      sessionStorage.setItem('avenchart-ui.telehealthProspectiveApplicant', JSON.stringify(session))
    }, { applicantId: prospectiveApplicant.applicantId, applicantAccessKey: 'r'.repeat(64) })
    const requestCreatedApplicant = {
      ...prospectiveApplicant,
      status: 'SyntheticRequestCreated',
      version: 26,
      contactVerified: true,
      identityAssurance: 'ContactControlOnly',
      duplicateDisposition: 'NoCandidate',
      canonicalPatientCreated: true,
      verificationAttemptsRemaining: 0,
      demonstrationVerificationCode: null,
      nextAction: 'Confirm the bounded synthetic request intake snapshot.',
    }
    const requestReceipt = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 26,
      applicantStatus: 'SyntheticRequestCreated',
      policyKey: 'SYNTHETIC_APPLICANT_TELEHEALTH_REQUEST_CREATION',
      policyVersion: 1,
      authorizationPolicyVersion: 1,
      requestCreationReady: false,
      requestCreated: true,
      requestId: '44000000-0000-4000-8000-000000000044',
      requestStatus: 'Intake',
      requestVersion: 4,
      complaintCategory: 'sleep',
      createdAt: '2026-08-28T16:00:00Z',
      telehealthRequestCreated: true,
      patientContacted: false,
      patientCareQueueEntered: false,
      clinicianQueueEntered: false,
      doctorSearchStarted: false,
      queuePositionAssigned: false,
      appointmentCreated: false,
      encounterCreated: false,
      consentCreated: false,
      careAuthorized: false,
      prescribingEnabled: false,
      billingEnabled: false,
      claimCreated: false,
      integrationEnabled: false,
      externalCallPerformed: false,
      direction: 'The bounded intake snapshot remains pending.',
      limitations: ['No queue or care exists.'],
    }
    const confirmedLocation = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 26,
      applicantStatus: 'SyntheticRequestCreated',
      requestId: requestReceipt.requestId,
      requestVersion: 2,
      requestStatus: 'LocationConfirmed',
      policyKey: 'SYNTHETIC_APPLICANT_REQUEST_LOCATION_CONFIRMATION',
      policyVersion: 1,
      contextSnapshotFingerprint: '2'.repeat(64),
      currentLocationStateCode: 'GA',
      maskedCallbackPhone: '***-***-0199',
      confirmationReady: false,
      locationConfirmed: true,
      confirmedAt: '2026-08-28T17:00:00Z',
      triageAssessmentCreated: false,
      clinicalReviewCreated: false,
      patientContacted: false,
      patientCareQueueEntered: false,
      clinicianQueueEntered: false,
      doctorSearchStarted: false,
      queuePositionAssigned: false,
      appointmentCreated: false,
      encounterCreated: false,
      consentCreated: false,
      careAuthorized: false,
      prescribingEnabled: false,
      billingEnabled: false,
      claimCreated: false,
      integrationEnabled: false,
      externalCallPerformed: false,
      direction: 'Location and callback are confirmed.',
      limitations: ['No triage, queue, appointment, encounter, consent, or care is created.'],
    }
    const universalPass = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 26,
      applicantStatus: 'SyntheticRequestCreated',
      requestId: requestReceipt.requestId,
      requestVersion: 3,
      requestStatus: 'SafetyScreening',
      policyKey: 'SYNTHETIC_APPLICANT_REQUEST_UNIVERSAL_SAFETY_ASSESSMENT',
      policyVersion: 1,
      protocolKey: 'synthetic-universal-safety',
      protocolVersion: 1,
      contextSnapshotFingerprint: '3'.repeat(64),
      contextExpiresAt: '2026-08-28T17:30:00Z',
      currentLocationStateCode: 'GA',
      maskedCallbackPhone: '***-***-0199',
      assessmentReady: false,
      assessmentCreated: true,
      outcome: 'TelehealthEligible',
      publicDisposition: 'UniversalSafetyPassed',
      evaluatedAt: '2026-08-28T17:05:00Z',
      universalSafetyPassed: true,
      complaintSpecificTriageRequired: true,
      complaintSpecificTriageCreated: false,
      clinicalReviewRequired: false,
      clinicalReviewCreated: false,
      terminalForTelehealth: false,
      patientContacted: false,
      patientCareQueueEntered: false,
      clinicianQueueEntered: false,
      doctorSearchStarted: false,
      queuePositionAssigned: false,
      appointmentCreated: false,
      encounterCreated: false,
      consentCreated: false,
      careAuthorized: false,
      prescribingEnabled: false,
      billingEnabled: false,
      claimCreated: false,
      integrationEnabled: false,
      externalCallPerformed: false,
      direction: 'Complaint-specific triage is still required.',
      limitations: ['The universal result is not complete eligibility.'],
    }
    const complaintPass = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 26,
      applicantStatus: 'SyntheticRequestCreated',
      requestId: requestReceipt.requestId,
      requestVersion: 4,
      requestStatus: 'Intake',
      complaintCategory: 'sleep',
      policyKey: 'SYNTHETIC_APPLICANT_REQUEST_COMPLAINT_TRIAGE',
      policyVersion: 1,
      protocolKey: 'synthetic-sleep-complaint-triage',
      protocolVersion: 1,
      engineVersion: 'synthetic-complaint-triage-engine-v1',
      clinicalContentStatus: 'UNAPPROVED_SYNTHETIC',
      medicalDirectorApprovalRequired: true,
      medicalDirectorApprovalRecorded: false,
      clinicalGoldenCasePackApproved: false,
      productionPublicationAllowed: false,
      contextSnapshotFingerprint: '4'.repeat(64),
      contextExpiresAt: '2026-08-28T17:30:00Z',
      currentLocationStateCode: 'GA',
      maskedCallbackPhone: '***-***-0199',
      assessmentReady: false,
      assessmentCreated: true,
      outcome: 'TelehealthEligible',
      publicDisposition: 'SyntheticVideoEvaluationCandidate',
      evaluatedAt: '2026-08-28T17:08:00Z',
      syntheticVideoEvaluationCandidate: true,
      clinicalReviewRequired: false,
      clinicalReviewCreated: false,
      terminalForTelehealth: false,
      intakeSnapshotCreated: false,
      patientContacted: false,
      patientCareQueueEntered: false,
      clinicianQueueEntered: false,
      doctorSearchStarted: false,
      queuePositionAssigned: false,
      appointmentCreated: false,
      encounterCreated: false,
      consentCreated: false,
      careAuthorized: false,
      prescribingEnabled: false,
      billingEnabled: false,
      claimCreated: false,
      integrationEnabled: false,
      externalCallPerformed: false,
      direction: 'Synthetic intake progression can be demonstrated.',
      limitations: ['Medical-director approval and production publication remain closed.'],
    }
    const readyIntake = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 26,
      applicantStatus: 'SyntheticRequestCreated',
      requestId: requestReceipt.requestId,
      requestVersion: 4,
      requestStatus: 'Intake',
      complaintCategory: 'sleep',
      complaintDisplayLabel: 'Sleep difficulty',
      policyKey: 'SYNTHETIC_APPLICANT_REQUEST_INTAKE_SNAPSHOT_CONFIRMATION',
      policyVersion: 1,
      clinicalContentStatus: 'UNAPPROVED_SYNTHETIC',
      medicalDirectorApprovalRequired: true,
      medicalDirectorApprovalRecorded: false,
      clinicalGoldenCasePackApproved: false,
      productionPublicationAllowed: false,
      contextSnapshotFingerprint: '5'.repeat(64),
      contextExpiresAt: '2026-08-28T17:30:00Z',
      currentLocationStateCode: 'GA',
      maskedCallbackPhone: '***-***-0199',
      supportedSymptomDurations: ['less-than-day', '1-3-days', '4-14-days', 'more-than-14-days'],
      sections: [
        { sectionKey: 'registration', receiptState: 'Confirmed', outstandingRoute: 'No patient-record mutation' },
        { sectionKey: 'insurance', receiptState: 'Applicant handoff confirmed', outstandingRoute: 'Canonical coverage pending' },
      ],
      snapshotReady: true,
      snapshotCreated: false,
      symptomDuration: null,
      capturedAt: null,
      verificationPending: true,
      consentPending: true,
      coverageRecordCreated: false,
      coverageVerified: false,
      exactNetworkConfirmed: false,
      operationalReviewCreated: false,
      practiceAccepted: false,
      patientContacted: false,
      patientCareQueueEntered: false,
      clinicianQueueEntered: false,
      doctorSearchStarted: false,
      queuePositionAssigned: false,
      appointmentCreated: false,
      encounterCreated: false,
      consentCreated: false,
      careAuthorized: false,
      prescribingEnabled: false,
      billingEnabled: false,
      claimCreated: false,
      integrationEnabled: false,
      externalCallPerformed: false,
      direction: 'Choose one duration and review eight confirmations.',
      limitations: ['NON_PRODUCTION no-free-text synthetic intake.'],
    }
    const completedIntake = {
      ...readyIntake,
      requestVersion: 5,
      requestStatus: 'Verification',
      snapshotReady: false,
      snapshotCreated: true,
      symptomDuration: '1-3-days',
      capturedAt: '2026-08-28T17:10:00Z',
      direction: 'Verification and every later gate remain pending.',
    }
    const readyInsuranceSource = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 26,
      applicantStatus: 'SyntheticRequestCreated',
      requestId: requestReceipt.requestId,
      requestVersion: 5,
      requestStatus: 'Verification',
      policyKey: 'SYNTHETIC_APPLICANT_REQUEST_INSURANCE_SOURCE_CONFIRMATION',
      policyVersion: 1,
      insuranceSourceSnapshotFingerprint: '6'.repeat(64),
      contextExpiresAt: '2026-08-28T17:30:00Z',
      payerDisplayName: 'AvenChart Synthetic Health',
      productDisplayName: 'Synthetic Silver Demo',
      maskedMemberId: '••••4123',
      maskedGroupNumber: '••••6789',
      subscriberRelationship: 'Self',
      coveragePriority: 'Primary',
      previousEligibilityBusinessOutcome: 'EligibleBenefitsReported',
      previousEligibilityCheckedAt: '2026-08-28T16:30:00Z',
      previousEligibilityExpiresAt: '2026-08-28T17:00:00Z',
      previousEligibilityEvidenceExpired: true,
      previousPracticeNetworkBusinessOutcome: 'PracticeInNetworkAcceptingNewPatients',
      previousPracticeNetworkCheckedAt: '2026-08-28T16:35:00Z',
      previousPracticeNetworkExpiresAt: '2026-08-28T17:05:00Z',
      previousPracticeNetworkEvidenceExpired: true,
      previousRenderingPhysicianNetworkChecked: false,
      previousResultReusable: false,
      sourceReady: true,
      sourceConfirmed: false,
      confirmedAt: null,
      protectedPayloadReferenced: true,
      protectedPayloadCopied: false,
      protectedPayloadDecrypted: false,
      freshVerificationRequested: false,
      canonicalCoverageCreated: false,
      coverageSelected: false,
      eligibilityVerificationCreated: false,
      networkVerificationCreated: false,
      renderingPhysicianNetworkChecked: false,
      coverageVerified: false,
      exactNetworkConfirmed: false,
      financialRouteCreated: false,
      operationalReviewCreated: false,
      practiceAccepted: false,
      patientContacted: false,
      patientCareQueueEntered: false,
      clinicianQueueEntered: false,
      doctorSearchStarted: false,
      queuePositionAssigned: false,
      appointmentCreated: false,
      encounterCreated: false,
      consentCreated: false,
      careAuthorized: false,
      integrationEnabled: false,
      externalCallPerformed: false,
      direction: 'Review the masked historical source and request a future fresh verification.',
      limitations: ['Historical evidence only; no payer was contacted.'],
    }
    const completedInsuranceSource = {
      ...readyInsuranceSource,
      requestVersion: 6,
      sourceReady: false,
      sourceConfirmed: true,
      confirmedAt: '2026-08-28T17:12:00Z',
      freshVerificationRequested: true,
      direction: 'Fresh verification remains pending and unavailable.',
    }
    const readyRequestEligibility = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 26,
      applicantStatus: 'SyntheticRequestCreated',
      requestId: requestReceipt.requestId,
      requestVersion: 6,
      requestStatus: 'Verification',
      policyKey: 'SYNTHETIC_APPLICANT_REQUEST_ELIGIBILITY_VERIFICATION',
      policyVersion: 1,
      eligibilitySnapshotFingerprint: '7'.repeat(64),
      contextExpiresAt: '2026-08-28T17:30:00Z',
      payerDisplayName: 'AvenChart Synthetic Health',
      productDisplayName: 'Synthetic Silver Demo',
      maskedMemberId: '••••4123',
      maskedGroupNumber: '••••6789',
      subscriberRelationship: 'Self',
      coveragePriority: 'Primary',
      currentLocationStateCode: 'GA',
      purposeCategory: 'sleep',
      verificationReady: true,
      verificationCompleted: false,
      verificationId: null,
      dateOfService: null,
      serviceCategory: null,
      adapterMode: null,
      compatibilityTarget: null,
      datasetKey: null,
      datasetVersion: null,
      transportOutcome: null,
      memberMatchStatus: null,
      eligibilityStatus: null,
      benefitInformationStatus: null,
      businessOutcome: null,
      memberMatched: false,
      memberEligibilityChecked: false,
      memberBenefitsChecked: false,
      checkedAt: null,
      expiresAt: null,
      protectedPayloadReferenced: true,
      protectedPayloadCopied: false,
      protectedPayloadDecryptedInServerMemory: false,
      priorEligibilityResultReused: false,
      currentEligibilityEvidenceCreated: false,
      rawTransactionCreated: false,
      canonicalCoverageCreated: false,
      coverageSelected: false,
      networkVerificationCreated: false,
      renderingPhysicianNetworkChecked: false,
      coverageVerified: false,
      exactNetworkConfirmed: false,
      financialRouteCreated: false,
      operationalReviewCreated: false,
      practiceAccepted: false,
      patientContacted: false,
      patientCareQueueEntered: false,
      clinicianQueueEntered: false,
      doctorSearchStarted: false,
      queuePositionAssigned: false,
      appointmentCreated: false,
      encounterCreated: false,
      consentCreated: false,
      careAuthorized: false,
      integrationEnabled: false,
      externalCallPerformed: false,
      direction: 'Run one fresh synthetic eligibility check.',
      limitations: ['No payer or clearinghouse will be contacted.'],
    }
    const completedRequestEligibility = {
      ...readyRequestEligibility,
      requestVersion: 7,
      verificationReady: false,
      verificationCompleted: true,
      verificationId: '46000000-0000-4000-8000-000000000001',
      dateOfService: '2026-08-28',
      serviceCategory: 'ProfessionalTelehealthConsultation',
      adapterMode: 'NON_PRODUCTION',
      compatibilityTarget: 'ASC_X12N_270_271_005010X279A1',
      datasetKey: 'avenchart-synthetic-prospective-eligibility-2026-08',
      datasetVersion: 1,
      transportOutcome: 'SimulatedAccepted',
      memberMatchStatus: 'Matched',
      eligibilityStatus: 'Active',
      benefitInformationStatus: 'Reported',
      businessOutcome: 'EligibleBenefitsReported',
      memberMatched: true,
      memberEligibilityChecked: true,
      memberBenefitsChecked: true,
      checkedAt: '2026-08-28T17:13:00Z',
      expiresAt: '2026-08-28T17:28:00Z',
      protectedPayloadDecryptedInServerMemory: true,
      currentEligibilityEvidenceCreated: true,
      direction: 'Fresh eligibility is active; exact network remains required.',
    }
    const readyRequestPracticeNetwork = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 26,
      applicantStatus: 'SyntheticRequestCreated',
      requestId: requestReceipt.requestId,
      requestVersion: 7,
      requestStatus: 'Verification',
      policyKey: 'SYNTHETIC_APPLICANT_REQUEST_PRACTICE_NETWORK_VERIFICATION',
      policyVersion: 1,
      networkSnapshotFingerprint: '8'.repeat(64),
      contextExpiresAt: '2026-08-28T17:28:00Z',
      practiceDisplayName: 'AvenChart Synthetic Practice',
      payerDisplayName: 'AvenChart Synthetic Health',
      productDisplayName: 'Synthetic Silver Demo',
      currentLocationStateCode: 'GA',
      purposeCategory: 'sleep',
      eligibilityVerificationId: completedRequestEligibility.verificationId,
      eligibilityBusinessOutcome: 'EligibleBenefitsReported',
      eligibilityCheckedAt: completedRequestEligibility.checkedAt,
      eligibilityExpiresAt: completedRequestEligibility.expiresAt,
      verificationReady: true,
      verificationCompleted: false,
      verificationId: null,
      dateOfService: null,
      serviceCategory: null,
      adapterMode: null,
      compatibilityTarget: null,
      datasetKey: null,
      datasetVersion: null,
      transportOutcome: null,
      planNetworkMatchStatus: null,
      practiceAffiliationStatus: null,
      serviceAvailabilityStatus: null,
      newPatientAcceptanceStatus: null,
      businessOutcome: null,
      practiceNetworkChecked: false,
      practiceInNetwork: false,
      newPatientsAccepted: false,
      checkedAt: null,
      expiresAt: null,
      evidenceExpiresAt: null,
      currentEligibilityEvidenceReusedAsContext: true,
      practiceNetworkVerificationCreated: false,
      renderingPhysicianSelected: false,
      renderingPhysicianNetworkChecked: false,
      exactNetworkConfirmed: false,
      canonicalCoverageCreated: false,
      coverageSelected: false,
      coverageVerified: false,
      financialRouteCreated: false,
      operationalReviewCreated: false,
      practiceAccepted: false,
      patientContacted: false,
      patientCareQueueEntered: false,
      clinicianQueueEntered: false,
      doctorSearchStarted: false,
      queuePositionAssigned: false,
      appointmentCreated: false,
      encounterCreated: false,
      consentCreated: false,
      careAuthorized: false,
      integrationEnabled: false,
      externalCallPerformed: false,
      direction: 'Run the fresh practice-level network fixture.',
      limitations: ['No external provider directory or payer will be contacted.'],
    }
    const completedRequestPracticeNetwork = {
      ...readyRequestPracticeNetwork,
      requestVersion: 8,
      verificationReady: false,
      verificationCompleted: true,
      verificationId: '47000000-0000-4000-8000-000000000001',
      dateOfService: '2026-08-28',
      serviceCategory: 'ProfessionalTelehealthConsultation',
      adapterMode: 'NON_PRODUCTION',
      compatibilityTarget: 'HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0',
      datasetKey: 'avenchart-synthetic-practice-network-directory-2026-08',
      datasetVersion: 1,
      transportOutcome: 'SimulatedAvailable',
      planNetworkMatchStatus: 'Matched',
      practiceAffiliationStatus: 'InNetwork',
      serviceAvailabilityStatus: 'Included',
      newPatientAcceptanceStatus: 'Accepting',
      businessOutcome: 'PracticeInNetworkAcceptingNewPatients',
      practiceNetworkChecked: true,
      practiceInNetwork: true,
      newPatientsAccepted: true,
      checkedAt: '2026-08-28T17:14:00Z',
      expiresAt: '2026-08-28T17:29:00Z',
      evidenceExpiresAt: '2026-08-28T17:28:00Z',
      practiceNetworkVerificationCreated: true,
      direction: 'Practice-level fixture is in network; physician participation remains required.',
    }
    let intakeLoadCalls = 0
    let intakeConfirmationCalls = 0
    const intakeKeys: Array<string | undefined> = []
    const intakeBodies: Array<Record<string, unknown>> = []
    let insuranceSourceLoadCalls = 0
    let insuranceSourceConfirmationCalls = 0
    const insuranceSourceKeys: Array<string | undefined> = []
    const insuranceSourceBodies: Array<Record<string, unknown>> = []
    let requestEligibilityLoadCalls = 0
    let requestEligibilityRunCalls = 0
    const requestEligibilityKeys: Array<string | undefined> = []
    const requestEligibilityBodies: Array<Record<string, unknown>> = []
    let requestPracticeNetworkLoadCalls = 0
    let requestPracticeNetworkRunCalls = 0
    const requestPracticeNetworkKeys: Array<string | undefined> = []
    const requestPracticeNetworkBodies: Array<Record<string, unknown>> = []
    await page.route('**/api/telehealth/v1/applicants/**', async (route) => {
      const request = route.request()
      const path = new URL(request.url()).pathname
      expect(request.headers()['x-avenchart-telehealth-applicant-key']).toBe('r'.repeat(64))
      if (path.endsWith('/telehealth-request/practice-network')) {
        if (request.method() === 'POST') {
          requestPracticeNetworkRunCalls += 1
          requestPracticeNetworkKeys.push(request.headers()['x-idempotency-key'])
          requestPracticeNetworkBodies.push(request.postDataJSON() as Record<string, unknown>)
          if (requestPracticeNetworkRunCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Practice-network result unknown; retry unchanged.' }) })
            return
          }
          await route.fulfill({ json: completedRequestPracticeNetwork })
          return
        }
        requestPracticeNetworkLoadCalls += 1
        if (requestPracticeNetworkLoadCalls === 1) {
          await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Practice-network projection temporarily unavailable.' }) })
          return
        }
        await route.fulfill({ json: readyRequestPracticeNetwork })
        return
      }
      if (path.endsWith('/telehealth-request/eligibility')) {
        if (request.method() === 'POST') {
          requestEligibilityRunCalls += 1
          requestEligibilityKeys.push(request.headers()['x-idempotency-key'])
          requestEligibilityBodies.push(request.postDataJSON() as Record<string, unknown>)
          if (requestEligibilityRunCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Eligibility result unknown; retry unchanged.' }) })
            return
          }
          await route.fulfill({ json: completedRequestEligibility })
          return
        }
        requestEligibilityLoadCalls += 1
        if (requestEligibilityLoadCalls === 1) {
          await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Eligibility projection temporarily unavailable.' }) })
          return
        }
        await route.fulfill({ json: readyRequestEligibility })
        return
      }
      if (path.endsWith('/telehealth-request/insurance-source')) {
        if (request.method() === 'POST') {
          insuranceSourceConfirmationCalls += 1
          insuranceSourceKeys.push(request.headers()['x-idempotency-key'])
          insuranceSourceBodies.push(request.postDataJSON() as Record<string, unknown>)
          if (insuranceSourceConfirmationCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Insurance-source result unknown; retry unchanged.' }) })
            return
          }
          await route.fulfill({ json: completedInsuranceSource })
          return
        }
        insuranceSourceLoadCalls += 1
        if (insuranceSourceLoadCalls === 1) {
          await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Insurance-source projection temporarily unavailable.' }) })
          return
        }
        await route.fulfill({ json: readyInsuranceSource })
        return
      }
      if (path.endsWith('/telehealth-request/intake')) {
        if (request.method() === 'POST') {
          intakeConfirmationCalls += 1
          intakeKeys.push(request.headers()['x-idempotency-key'])
          intakeBodies.push(request.postDataJSON() as Record<string, unknown>)
          if (intakeConfirmationCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Intake result unknown; retry unchanged.' }) })
            return
          }
          await route.fulfill({ json: completedIntake })
          return
        }
        intakeLoadCalls += 1
        if (intakeLoadCalls === 1) {
          await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Intake projection temporarily unavailable.' }) })
          return
        }
        await route.fulfill({ json: readyIntake })
        return
      }
      if (path.endsWith('/telehealth-request/complaint-triage')) {
        await route.fulfill({ json: complaintPass })
        return
      }
      if (path.endsWith('/telehealth-request/safety')) {
        await route.fulfill({ json: universalPass })
        return
      }
      if (path.endsWith('/telehealth-request/location')) {
        await route.fulfill({ json: confirmedLocation })
        return
      }
      if (path.endsWith('/telehealth-request')) {
        await route.fulfill({ json: requestReceipt })
        return
      }
      await route.fulfill({ json: requestCreatedApplicant })
    })

    await page.goto('/telehealth/new')
    await expect(page.getByRole('alert')).toContainText('Intake projection temporarily unavailable.')
    await page.getByRole('button', { name: 'Retry request intake load' }).click()
    const heading = page.getByRole('heading', { name: 'Confirm request intake snapshot' })
    await expect(heading).toBeVisible()
    const form = heading.locator('..')
    await expect(form).toContainText('Server-owned purposeSleep difficulty')
    await expect(form).toContainText('UNAPPROVED_SYNTHETIC')
    await expect(form).toContainText('Canonical coverage pending')
    await expect(form.locator('input[type="text"]')).toHaveCount(0)
    await expect(form.locator('textarea')).toHaveCount(0)
    const duration = form.getByLabel('How long has the fictional concern been present?')
    await expect(duration).toHaveValue('')
    const submit = form.getByRole('button', { name: 'Record synthetic intake snapshot' })
    await expect(submit).toBeDisabled()
    await duration.selectOption('1-3-days')
    for (const label of [
      'I confirm the displayed state remains the current physical location.',
      'I confirm the displayed masked callback route remains correct.',
      'I reviewed the displayed prior-information receipt states and will stop if a correction is needed.',
      'I understand no canonical coverage, current eligibility, benefits, or exact network result exists.',
      'I understand legal and clinician consent remain pending and unavailable here.',
      'I understand advancing to Verification only records a pending workflow state.',
      'I understand the synthetic candidate result is not diagnosis, treatment, acceptance, or guaranteed care.',
      'I confirm the duration is fictional synthetic demonstration data.',
    ]) {
      await form.getByLabel(label).check()
    }
    await submit.click()
    const retryAlert = page.getByRole('alert').filter({ hasText: 'Intake result unknown; retry unchanged.' })
    await expect(retryAlert).toBeVisible()
    await expect(retryAlert).toBeFocused()
    await expect(duration).toHaveValue('1-3-days')
    await submit.click()

    const result = page.getByRole('heading', { name: 'Request intake snapshot recorded' })
    await expect(result).toBeVisible()
    await expect(result.locator('..')).toContainText('Request statusVerification')
    await expect(result.locator('..')).toContainText('Verification pendingYes')
    await expect(result.locator('..')).toContainText('Consent pendingYes')
    await expect(result.locator('..')).toContainText('Coverage or exact network confirmedNo')
    await expect(result.locator('..')).toContainText('Operational review or practice acceptanceNo')
    await expect(result.locator('..')).toContainText('Doctor search or queueNot started')
    expect(intakeKeys).toHaveLength(2)
    expect(intakeKeys[0]).toBe(intakeKeys[1])
    expect(intakeBodies[0]).toEqual({
      expectedRequestVersion: 4,
      contextSnapshotFingerprint: '5'.repeat(64),
      currentLocationStateCode: 'GA',
      symptomDuration: '1-3-days',
      currentLocationConfirmed: true,
      callbackNumberConfirmed: true,
      priorInformationReviewed: true,
      insuranceLimitationsAcknowledged: true,
      pendingConsentAcknowledged: true,
      pendingVerificationAcknowledged: true,
      complaintResultAcknowledged: true,
      syntheticDataConfirmed: true,
    })

    await expect(page.getByRole('alert')).toContainText('Insurance-source projection temporarily unavailable.')
    await page.getByRole('button', { name: 'Retry insurance-source load' }).click()
    const insuranceHeading = page.getByRole('heading', { name: 'Confirm insurance source for this request' })
    await expect(insuranceHeading).toBeVisible()
    const insuranceForm = insuranceHeading.locator('..')
    await expect(insuranceForm).toContainText('Historical source only')
    await expect(insuranceForm).toContainText('AvenChart Synthetic Health')
    await expect(insuranceForm).toContainText('••••4123')
    await expect(insuranceForm).toContainText('Earlier eligibility result — historical only')
    await expect(insuranceForm).toContainText('Earlier result reusableNo')
    await expect(insuranceForm.locator('textarea')).toHaveCount(0)
    await expect(insuranceForm.locator('select')).toHaveCount(0)
    await expect(insuranceForm.locator('input:not([type="checkbox"])')).toHaveCount(0)
    const insuranceSubmit = insuranceForm.getByRole('button', { name: 'Confirm synthetic insurance source' })
    await expect(insuranceSubmit).toBeDisabled()
    for (const label of [
      'I confirm the displayed payer and product match the synthetic source I previously supplied.',
      'I confirm the displayed masked member and optional group details match that source.',
      'I confirm the displayed subscriber relationship.',
      'I confirm this is the primary synthetic coverage source to carry into a future verification step.',
      'I request a future fresh eligibility and network verification and understand this step performs none.',
      'I understand every earlier eligibility and practice-network result is historical only and cannot be reused.',
      'I confirm these are fictional synthetic demonstration details.',
    ]) {
      await insuranceForm.getByLabel(label).check()
    }
    await insuranceSubmit.click()
    const insuranceRetryAlert = page.getByRole('alert').filter({ hasText: 'Insurance-source result unknown; retry unchanged.' })
    await expect(insuranceRetryAlert).toBeVisible()
    await expect(insuranceRetryAlert).toBeFocused()
    await insuranceSubmit.click()

    const insuranceResult = page.getByRole('heading', { name: 'Request insurance source confirmed' })
    await expect(insuranceResult).toBeVisible()
    await expect(insuranceResult.locator('..')).toContainText('Request statusVerification')
    await expect(insuranceResult.locator('..')).toContainText('Request version6')
    await expect(insuranceResult.locator('..')).toContainText('Intent recorded; not performed')
    await expect(insuranceResult.locator('..')).toContainText('Current coverage verifiedNo')
    await expect(insuranceResult.locator('..')).toContainText('Exact network confirmedNo')
    await expect(insuranceResult.locator('..')).toContainText('Doctor search or queueNot started')
    expect(insuranceSourceKeys).toHaveLength(2)
    expect(insuranceSourceKeys[0]).toBe(insuranceSourceKeys[1])
    expect(insuranceSourceBodies[0]).toEqual({
      expectedRequestVersion: 5,
      insuranceSourceSnapshotFingerprint: '6'.repeat(64),
      payerProductConfirmed: true,
      maskedMemberDetailsConfirmed: true,
      subscriberRelationshipConfirmed: true,
      primaryCoverageSourceConfirmed: true,
      freshVerificationRequested: true,
      evidenceLimitationsAcknowledged: true,
      syntheticDataConfirmed: true,
    })

    await expect(page.getByRole('alert')).toContainText('Eligibility projection temporarily unavailable.')
    await page.getByRole('button', { name: 'Retry eligibility load' }).click()
    const eligibilityHeading = page.getByRole('heading', { name: 'Run fresh request eligibility' })
    await expect(eligibilityHeading).toBeVisible()
    const eligibilityForm = eligibilityHeading.locator('..')
    await expect(eligibilityForm).toContainText('NON_PRODUCTION eligibility fixture only')
    await expect(eligibilityForm).toContainText('AvenChart Synthetic Health')
    await expect(eligibilityForm).toContainText('••••4123')
    await expect(eligibilityForm).toContainText('does not verify whether the practice or eventual treating physician is in network')
    await expect(eligibilityForm.locator('textarea')).toHaveCount(0)
    await expect(eligibilityForm.locator('select')).toHaveCount(0)
    await expect(eligibilityForm.locator('input:not([type="checkbox"])')).toHaveCount(0)
    const eligibilitySubmit = eligibilityForm.getByRole('button', { name: 'Run synthetic eligibility check' })
    await expect(eligibilitySubmit).toBeDisabled()
    await eligibilityForm.getByLabel('I confirm this check uses only fictional synthetic demonstration data.').check()
    await eligibilityForm.getByLabel('I understand eligibility or benefit information is not a guarantee of coverage, payment, cost, or network participation.').check()
    await eligibilitySubmit.click()
    const eligibilityRetryAlert = page.getByRole('alert').filter({ hasText: 'Eligibility result unknown; retry unchanged.' })
    await expect(eligibilityRetryAlert).toBeVisible()
    await expect(eligibilityRetryAlert).toBeFocused()
    await eligibilitySubmit.click()

    const eligibilityResult = page.getByRole('heading', { name: 'Fresh request eligibility recorded' })
    await expect(eligibilityResult).toBeVisible()
    await expect(eligibilityResult.locator('..')).toContainText('Request version7')
    await expect(eligibilityResult.locator('..')).toContainText('EligibilityActive')
    await expect(eligibilityResult.locator('..')).toContainText('Benefit informationReported')
    await expect(eligibilityResult.locator('..')).toContainText('Exact network confirmedNo — still pending')
    await expect(eligibilityResult.locator('..')).toContainText('Coverage verifiedNo')
    await expect(eligibilityResult.locator('..')).toContainText('Doctor search or queueNot started')
    expect(requestEligibilityKeys).toHaveLength(2)
    expect(requestEligibilityKeys[0]).toBe(requestEligibilityKeys[1])
    expect(requestEligibilityBodies[0]).toEqual({
      expectedRequestVersion: 6,
      eligibilitySnapshotFingerprint: '7'.repeat(64),
      syntheticDataConfirmed: true,
      noGuaranteeAcknowledged: true,
    })

    await expect(page.getByRole('alert')).toContainText('Practice-network projection temporarily unavailable.')
    await page.getByRole('button', { name: 'Retry practice-network load' }).click()
    const practiceNetworkHeading = page.getByRole('heading', { name: 'Verify the practice-level network fixture' })
    await expect(practiceNetworkHeading).toBeVisible()
    const practiceNetworkForm = practiceNetworkHeading.locator('..')
    await expect(practiceNetworkForm).toContainText('AvenChart Synthetic Practice')
    await expect(practiceNetworkForm).toContainText('EligibleBenefitsReported')
    await expect(practiceNetworkForm).toContainText('cannot establish exact network status because no rendering physician has been selected')
    await expect(practiceNetworkForm.locator('textarea')).toHaveCount(0)
    await expect(practiceNetworkForm.locator('select')).toHaveCount(0)
    await expect(practiceNetworkForm.locator('input:not([type="checkbox"])')).toHaveCount(0)
    const practiceNetworkSubmit = practiceNetworkForm.getByRole('button', { name: 'Run synthetic practice-network check' })
    await expect(practiceNetworkSubmit).toBeDisabled()
    for (const label of [
      'I confirm this check uses only fictional synthetic demonstration data.',
      'I understand this result covers only the configured practice/facility/service fixture and does not select or check a rendering physician.',
      'I understand practice-level network evidence is not a guarantee of coverage, payment, cost, physician participation, or an appointment.',
    ]) {
      await practiceNetworkForm.getByLabel(label).check()
    }
    await practiceNetworkSubmit.click()
    const practiceNetworkRetryAlert = page.getByRole('alert').filter({ hasText: 'Practice-network result unknown; retry unchanged.' })
    await expect(practiceNetworkRetryAlert).toBeVisible()
    await expect(practiceNetworkRetryAlert).toBeFocused()
    await practiceNetworkSubmit.click()

    const practiceNetworkResult = page.getByRole('heading', { name: 'Fresh practice-network result recorded' })
    await expect(practiceNetworkResult).toBeVisible()
    await expect(practiceNetworkResult.locator('..')).toContainText('Request version8')
    await expect(practiceNetworkResult.locator('..')).toContainText('Practice affiliationInNetwork')
    await expect(practiceNetworkResult.locator('..')).toContainText('Accepting new patientsAccepting')
    await expect(practiceNetworkResult.locator('..')).toContainText('Rendering physician selectedNo')
    await expect(practiceNetworkResult.locator('..')).toContainText('Exact network confirmedNo — physician check still required')
    await expect(practiceNetworkResult.locator('..')).toContainText('Doctor search or queueNot started')
    expect(requestPracticeNetworkKeys).toHaveLength(2)
    expect(requestPracticeNetworkKeys[0]).toBe(requestPracticeNetworkKeys[1])
    expect(requestPracticeNetworkBodies[0]).toEqual({
      expectedRequestVersion: 7,
      networkSnapshotFingerprint: '8'.repeat(64),
      syntheticDataConfirmed: true,
      practiceOnlyScopeAcknowledged: true,
      noGuaranteeAcknowledged: true,
    })
    expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toMatch(/contextSnapshotFingerprint|symptomDuration|sourceComplaint|intakeSnapshot|insuranceSourceSnapshotFingerprint|eligibilitySnapshotFingerprint|networkSnapshotFingerprint|payerDisplayName|maskedMemberId|businessOutcome|requestId/i)

    await expectNoSeriousAccessibilityViolations(page)
    await expectTelehealthReflow(page)
  })

  test('promoted applicant completes bounded post-promotion confirmations through practice-review submission without implying acceptance, queueing, or care', async ({ page }) => {
    await page.addInitScript(() => {
      Object.defineProperty(window, 'isSecureContext', { configurable: true, value: true })
      Object.defineProperty(window, 'RTCPeerConnection', { configurable: true, value: function SyntheticPeerConnection() {} })
      Object.defineProperty(navigator, 'mediaDevices', {
        configurable: true,
        value: {
          getUserMedia: async () => ({
            getTracks: () => [
              { kind: 'video', stop: () => undefined },
              { kind: 'audio', stop: () => undefined },
            ],
          }),
        },
      })
      Object.defineProperty(navigator, 'connection', { configurable: true, value: { effectiveType: '4g' } })
    })
    await page.addInitScript((session) => {
      sessionStorage.setItem('avenchart-ui.telehealthProspectiveApplicant', JSON.stringify(session))
    }, { applicantId: prospectiveApplicant.applicantId, applicantAccessKey: 'f'.repeat(64) })
    const promotedApplicant = {
      ...prospectiveApplicant,
      status: 'SyntheticPatientPromoted',
      version: 12,
      contactVerified: true,
      identityAssurance: 'ContactControlOnly',
      duplicateDisposition: 'NoCandidate',
      canonicalPatientCreated: true,
      demonstrationVerificationCode: null,
      nextAction: 'Review the Georgia telehealth notice.',
    }
    const notice = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 12,
      applicantStatus: 'SyntheticPatientPromoted',
      noticeKey: 'GA_TELEHEALTH_NOTICE_V1',
      noticeVersion: 1,
      currentLocationStateCode: 'GA',
      title: 'Georgia synthetic telehealth notice',
      summary: 'Telehealth uses electronic communications and can have limitations.',
      sourceTitle: 'Georgia Composite Medical Board Rule 360-3-.07',
      sourceUrl: 'https://rules.sos.ga.gov/gac/360-3-.07',
      disclosures: ['Call 911 or seek emergency care for emergency symptoms.'],
      deferredRequirements: ['A licensed clinician must later complete required disclosures and consent documentation.'],
      policyKey: 'SYNTHETIC_TELEHEALTH_NOTICE_ACKNOWLEDGMENT',
      policyVersion: 1,
      legalReviewStatus: 'PendingIndependentReview',
      acknowledged: false,
      acknowledgedAt: null,
      legalConsentEstablished: false,
      clinicianConsentDocumented: false,
      clinicianReconfirmationRequired: true,
      portalAccountCreated: false,
      intakeCompleted: false,
      practiceAccepted: false,
      insuranceCreated: false,
      requestCreated: false,
      queueEnabled: false,
      careEnabled: false,
      direction: 'A clinician must later reconfirm this notice before care.',
      limitations: ['No final legal consent or care authorization was established.'],
    }
    let acknowledged = false
    let registrationDetailsConfirmed = false
    let insuranceHandoffConfirmed = false
    let communicationAccessRecorded = false
    let devicePreparationRecorded = false
    let clinicalInformationInventoryRecorded = false
    let medicationInformationRecorded = false
    let allergyInformationRecorded = false
    let healthHistoryInformationRecorded = false
    let clinicalInformationSummaryConfirmed = false
    let preRequestReadinessAcknowledged = false
    let practiceReviewSubmitted = false
    let postCalls = 0
    let registrationDetailsPostCalls = 0
    let insuranceHandoffPostCalls = 0
    let communicationAccessPostCalls = 0
    let devicePreparationPostCalls = 0
    let clinicalInformationInventoryPostCalls = 0
    let medicationInformationGetCalls = 0
    let medicationInformationPostCalls = 0
    let allergyInformationGetCalls = 0
    let allergyInformationPostCalls = 0
    let healthHistoryInformationGetCalls = 0
    let healthHistoryInformationPostCalls = 0
    let clinicalInformationSummaryGetCalls = 0
    let clinicalInformationSummaryPostCalls = 0
    let preRequestReadinessGetCalls = 0
    let preRequestReadinessPostCalls = 0
    let practiceReviewGetCalls = 0
    let practiceReviewPostCalls = 0
    const commandKeys: Array<string | undefined> = []
    const commandBodies: Array<Record<string, unknown>> = []
    const registrationDetailsKeys: Array<string | undefined> = []
    const registrationDetailsBodies: Array<Record<string, unknown>> = []
    const insuranceHandoffKeys: Array<string | undefined> = []
    const insuranceHandoffBodies: Array<Record<string, unknown>> = []
    const communicationAccessKeys: Array<string | undefined> = []
    const communicationAccessBodies: Array<Record<string, unknown>> = []
    const devicePreparationKeys: Array<string | undefined> = []
    const devicePreparationBodies: Array<Record<string, unknown>> = []
    const clinicalInformationInventoryKeys: Array<string | undefined> = []
    const clinicalInformationInventoryBodies: Array<Record<string, unknown>> = []
    const medicationInformationKeys: Array<string | undefined> = []
    const medicationInformationBodies: Array<Record<string, unknown>> = []
    const allergyInformationKeys: Array<string | undefined> = []
    const allergyInformationBodies: Array<Record<string, unknown>> = []
    const healthHistoryInformationKeys: Array<string | undefined> = []
    const healthHistoryInformationBodies: Array<Record<string, unknown>> = []
    const clinicalInformationSummaryKeys: Array<string | undefined> = []
    const clinicalInformationSummaryBodies: Array<Record<string, unknown>> = []
    const preRequestReadinessKeys: Array<string | undefined> = []
    const preRequestReadinessBodies: Array<Record<string, unknown>> = []
    const practiceReviewKeys: Array<string | undefined> = []
    const practiceReviewBodies: Array<Record<string, unknown>> = []
    const registrationDetails = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 13,
      applicantStatus: 'SyntheticTelehealthNoticeAcknowledged',
      legalFirstName: 'Avery',
      legalLastName: 'Notice',
      dateOfBirth: '1990-03-14',
      maskedEmail: 'a•••@example.test',
      maskedPhone: '(***) ***-0199',
      residenceStateCode: 'GA',
      postalCode: '30303',
      detailsFingerprint: 'f'.repeat(64),
      policyKey: 'SYNTHETIC_MINIMUM_REGISTRATION_DETAILS_CONFIRMATION',
      policyVersion: 1,
      confirmed: false,
      confirmedAt: null,
      identityAssuranceEstablished: false,
      patientRecordChanged: false,
      correctionCompleted: false,
      intakeCompleted: false,
      legalConsentEstablished: false,
      practiceAccepted: false,
      insuranceConfirmed: false,
      coverageCreated: false,
      requestCreated: false,
      queueEnabled: false,
      careEnabled: false,
      direction: 'Review and confirm only these copied minimum details.',
      limitations: ['No patient record or downstream workflow was changed.'],
    }
    const insuranceHandoff = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 14,
      applicantStatus: 'SyntheticMinimumRegistrationDetailsConfirmed',
      payerDisplayName: 'Harbor Mutual',
      productDisplayName: 'High Deductible',
      memberIdMask: '••••A123',
      groupNumberMask: '••••G456',
      subscriberRelationship: 'Self',
      coveragePriority: 'Primary',
      eligibilityBusinessOutcome: 'EligibleBenefitsReported',
      eligibilityCheckedAt: '2026-08-27T14:31:00Z',
      eligibilityExpiresAt: '2026-08-27T14:46:00Z',
      eligibilityEvidenceCurrent: true,
      practiceNetworkBusinessOutcome: 'PracticeInNetworkAcceptingNewPatients',
      practiceNetworkCheckedAt: '2026-08-27T14:32:00Z',
      practiceNetworkExpiresAt: '2026-08-27T14:47:00Z',
      practiceNetworkEvidenceCurrent: true,
      renderingPhysicianNetworkChecked: false,
      insuranceSnapshotFingerprint: 'e'.repeat(64),
      policyKey: 'SYNTHETIC_INSURANCE_HANDOFF_CONFIRMATION',
      policyVersion: 1,
      insuranceDetailsConfirmed: false,
      confirmedAt: null,
      coverageVerified: false,
      exactNetworkConfirmed: false,
      canonicalCoverageCreated: false,
      patientRecordChanged: false,
      portalAccessEnabled: false,
      intakeCompleted: false,
      legalConsentEstablished: false,
      practiceAccepted: false,
      requestCreated: false,
      queueEnabled: false,
      careEnabled: false,
      direction: 'Confirm the masked synthetic insurance handoff.',
      limitations: ['No payer or rendering physician was contacted.'],
    }
    const communicationAccessReadiness = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 15,
      applicantStatus: 'SyntheticInsuranceDetailsConfirmed',
      currentLocationStateCode: 'GA',
      maskedCallbackPhone: '(***) ***-0199',
      supportedSpokenLanguages: ['English', 'Spanish'],
      preferredSpokenLanguage: null,
      interpreterRequested: null,
      accessibilitySupportRequested: null,
      contextSnapshotFingerprint: 'd'.repeat(64),
      policyKey: 'SYNTHETIC_COMMUNICATION_ACCESS_READINESS',
      policyVersion: 1,
      readinessRecorded: false,
      recordedAt: null,
      safeAndPrivateToCommunicateConfirmed: false,
      interpreterAssigned: false,
      accessibilityAccommodationArranged: false,
      communicationArrangementCompleted: false,
      supportRequestCreated: false,
      technologyReadinessCompleted: false,
      patientRecordChanged: false,
      intakeCompleted: false,
      legalConsentEstablished: false,
      practiceAccepted: false,
      requestCreated: false,
      queueEnabled: false,
      careEnabled: false,
      direction: 'Record bounded preferences only.',
      limitations: ['No interpreter or accessibility service was contacted.'],
    }
    const devicePreparation = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 16,
      applicantStatus: 'SyntheticCommunicationAccessReadinessRecorded',
      supportedNetworkQualities: ['Unknown', 'Good'],
      preparationSnapshotFingerprint: 'c'.repeat(64),
      policyKey: 'SYNTHETIC_APPLICANT_DEVICE_PREPARATION',
      policyVersion: 1,
      preparationRecorded: false,
      recordedAt: null,
      browserSupported: null,
      cameraAvailable: null,
      microphoneAvailable: null,
      speakerAvailable: null,
      networkQuality: null,
      clientReportedResultAcknowledged: false,
      noReadinessGuaranteeAcknowledged: false,
      recheckBeforeConsultationAcknowledged: false,
      technologyReady: false,
      waitingRoomCreated: false,
      mediaSessionCreated: false,
      communicationStarted: false,
      supportArrangementCompleted: false,
      patientRecordChanged: false,
      intakeCompleted: false,
      legalConsentEstablished: false,
      practiceAccepted: false,
      requestCreated: false,
      queueEntered: false,
      careAuthorized: false,
      direction: 'Run the bounded local device check.',
      limitations: ['No media, precise device data, or network diagnostic is sent or stored.'],
    }
    const clinicalInformationInventory = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 17,
      applicantStatus: 'SyntheticDevicePreparationRecorded',
      supportedCategoryStatuses: ['PatientReportsNone', 'ItemsToReview', 'Unsure'],
      inventorySnapshotFingerprint: 'b'.repeat(64),
      policyKey: 'SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_INVENTORY',
      policyVersion: 1,
      inventoryRecorded: false,
      recordedAt: null,
      medicationsStatus: null,
      allergiesOrIntolerancesStatus: null,
      otherHealthHistoryStatus: null,
      reviewRoute: null,
      patientReportedMayBeIncompleteAcknowledged: false,
      noClinicalDetailsCapturedAcknowledged: false,
      clinicianReconciliationRequiredAcknowledged: false,
      medicationListReconciled: false,
      allergyListReconciled: false,
      healthHistoryReconciled: false,
      clinicalIntakeCompleted: false,
      clinicalEligibilityEstablished: false,
      clinicianReviewCreated: false,
      patientRecordChanged: false,
      requestCreated: false,
      queueEntered: false,
      careAuthorized: false,
      prescribingEnabled: false,
      direction: 'Choose one coarse patient-reported status for every category.',
      limitations: ['No detailed clinical information or canonical chart content is collected.'],
    }
    const medicationInformation = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 18,
      applicantStatus: 'SyntheticClinicalInformationInventoryRecorded',
      inventoryMedicationsStatus: 'ItemsToReview',
      medicationInformationSnapshotFingerprint: 'a'.repeat(64),
      policyKey: 'SYNTHETIC_APPLICANT_MEDICATION_INFORMATION',
      policyVersion: 1,
      catalogKey: 'avenchart-synthetic-applicant-medication-ingredients-2026-08',
      catalogVersion: 1,
      codingSystem: 'LOCAL_SYNTHETIC_ONLY',
      catalogComplete: false,
      catalogItems: ([
        ['acetaminophen', 'Acetaminophen'],
        ['ibuprofen', 'Ibuprofen'],
        ['sumatriptan', 'Sumatriptan'],
        ['melatonin', 'Melatonin'],
        ['lisinopril', 'Lisinopril'],
        ['metformin', 'Metformin'],
      ] as const).map(([catalogKey, displayName], index) => ({
        catalogKey,
        displayName,
        catalogVersion: 1,
        codingSystem: 'LOCAL_SYNTHETIC_ONLY',
        rxNormMapped: false,
        sortOrder: index + 1,
      })),
      supportedReportedUseStatuses: ['Taking', 'NotTaking', 'Unsure'],
      medicationInformationRecorded: false,
      recordedAt: null,
      medicationItems: [],
      additionalOrUnlistedItemsReported: false,
      reviewRoute: null,
      patientReportedMayBeIncompleteAcknowledged: false,
      syntheticCatalogIncompleteAcknowledged: false,
      noDoseOrDirectionsCapturedAcknowledged: false,
      clinicianReconciliationRequiredAcknowledged: false,
      medicationStatementCreated: false,
      medicationRequestCreated: false,
      medicationListReconciled: false,
      interactionCheckPerformed: false,
      clinicianReviewCreated: false,
      clinicalIntakeCompleted: false,
      clinicalEligibilityEstablished: false,
      patientRecordChanged: false,
      requestCreated: false,
      queueEntered: false,
      careAuthorized: false,
      prescribingEnabled: false,
      direction: 'Select from the incomplete local synthetic ingredient catalog.',
      limitations: ['No canonical medication resource or clinical task is created.'],
    }
    const allergyInformation = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 19,
      applicantStatus: 'SyntheticMedicationInformationRecorded',
      inventoryAllergiesOrIntolerancesStatus: 'ItemsToReview',
      allergyInformationSnapshotFingerprint: 'c'.repeat(64),
      policyKey: 'SYNTHETIC_APPLICANT_ALLERGY_INFORMATION',
      policyVersion: 1,
      catalogKey: 'avenchart-synthetic-applicant-allergy-substances-2026-08',
      catalogVersion: 1,
      codingSystem: 'LOCAL_SYNTHETIC_ONLY',
      catalogComplete: false,
      catalogItems: ([
        ['amoxicillin', 'Amoxicillin', 'Medication'],
        ['ibuprofen', 'Ibuprofen', 'Medication'],
        ['peanut', 'Peanut', 'Food'],
        ['shellfish', 'Shellfish', 'Food'],
        ['latex', 'Latex', 'Environment'],
        ['bee-venom', 'Bee venom', 'Environment'],
      ] as const).map(([catalogKey, displayName, category], index) => ({
        catalogKey,
        displayName,
        category,
        catalogVersion: 1,
        codingSystem: 'LOCAL_SYNTHETIC_ONLY',
        snomedCtMapped: false,
        rxNormMapped: false,
        sortOrder: index + 1,
      })),
      allergyInformationRecorded: false,
      recordedAt: null,
      allergyItems: [],
      additionalOrUnlistedItemsReported: false,
      reviewRoute: null,
      patientReportedMayBeIncompleteAcknowledged: false,
      syntheticCatalogIncompleteAcknowledged: false,
      noReactionOrCriticalityCapturedAcknowledged: false,
      clinicianVerificationRequiredAcknowledged: false,
      allergyIntoleranceCreated: false,
      allergyListReconciled: false,
      reactionAssessed: false,
      criticalityAssessed: false,
      contraindicationCheckPerformed: false,
      clinicianReviewCreated: false,
      clinicalIntakeCompleted: false,
      clinicalEligibilityEstablished: false,
      patientRecordChanged: false,
      requestCreated: false,
      queueEntered: false,
      careAuthorized: false,
      prescribingEnabled: false,
      direction: 'Select from the incomplete local synthetic substance catalog.',
      limitations: ['No canonical allergy resource, confirmed negation, or clinical task is created.'],
    }
    const healthHistoryInformation = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 20,
      applicantStatus: 'SyntheticAllergyInformationRecorded',
      inventoryOtherHealthHistoryStatus: 'ItemsToReview',
      healthHistoryInformationSnapshotFingerprint: 'd'.repeat(64),
      policyKey: 'SYNTHETIC_APPLICANT_HEALTH_HISTORY_INFORMATION',
      policyVersion: 1,
      catalogKey: 'avenchart-synthetic-applicant-health-history-topics-2026-08',
      catalogVersion: 1,
      codingSystem: 'LOCAL_SYNTHETIC_ONLY',
      catalogComplete: false,
      catalogTopics: ([
        ['ongoing-health-conditions', 'Ongoing health conditions', 'ConditionOrConcern'],
        ['prior-surgery-or-hospital-stay', 'Prior surgeries or hospital stays', 'ProcedureOrHospitalization'],
        ['pregnancy-or-postpartum-information', 'Pregnancy or postpartum information', 'HealthStatus'],
        ['immune-system-or-active-cancer-treatment', 'Immune system or active cancer treatment information', 'RiskContext'],
        ['behavioral-health-or-substance-use', 'Behavioral health or substance use information', 'SensitiveHistory'],
        ['family-health-history', 'Family health history', 'FamilyHistory'],
      ] as const).map(([catalogKey, displayName, category], index) => ({
        catalogKey,
        displayName,
        category,
        catalogVersion: 1,
        codingSystem: 'LOCAL_SYNTHETIC_ONLY',
        snomedCtMapped: false,
        icd10CmMapped: false,
        loincMapped: false,
        sortOrder: index + 1,
      })),
      healthHistoryInformationRecorded: false,
      recordedAt: null,
      healthHistoryTopics: [],
      additionalOrUnlistedTopicsReported: false,
      reviewRoute: null,
      patientReportedMayBeIncompleteAcknowledged: false,
      topicSelectionIsNotDiagnosisAcknowledged: false,
      noStatusOrTimingCapturedAcknowledged: false,
      clinicianVerificationRequiredAcknowledged: false,
      conditionCreated: false,
      procedureCreated: false,
      observationCreated: false,
      familyMemberHistoryCreated: false,
      questionnaireResponseCreated: false,
      healthHistoryReconciled: false,
      riskModifierEvaluated: false,
      clinicalTriageChanged: false,
      clinicianReviewCreated: false,
      clinicalIntakeCompleted: false,
      clinicalEligibilityEstablished: false,
      patientRecordChanged: false,
      requestCreated: false,
      queueEntered: false,
      careAuthorized: false,
      prescribingEnabled: false,
      direction: 'Select broad topics from the incomplete local synthetic catalog.',
      limitations: ['No canonical problem, assessment, risk evaluation, or clinical task is created.'],
    }
    const clinicalInformationSummary = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 21,
      applicantStatus: 'SyntheticHealthHistoryInformationRecorded',
      clinicalInformationSummarySnapshotFingerprint: 'e'.repeat(64),
      policyKey: 'SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_SUMMARY',
      policyVersion: 1,
      categories: [
        { categoryKey: 'Medications', inventoryStatus: 'ItemsToReview', selectedItemCount: 2, additionalOrUnlistedItemsReported: false, reviewRoute: 'ClinicianMedicationReviewRequired' },
        { categoryKey: 'AllergiesOrIntolerances', inventoryStatus: 'ItemsToReview', selectedItemCount: 2, additionalOrUnlistedItemsReported: false, reviewRoute: 'ClinicianAllergyReviewRequired' },
        { categoryKey: 'OtherHealthHistory', inventoryStatus: 'ItemsToReview', selectedItemCount: 2, additionalOrUnlistedItemsReported: true, reviewRoute: 'AdditionalHealthHistoryCollectionRequired' },
      ],
      summaryRoute: 'AdditionalClinicalInformationCollectionRequired',
      summaryConfirmed: false,
      confirmedAt: null,
      patientReportedMayBeIncompleteAcknowledged: false,
      notClinicallyVerifiedOrReconciledAcknowledged: false,
      noIntakeCompletionOrEligibilityAcknowledged: false,
      correctionRequiresSeparateWorkflowAcknowledged: false,
      questionnaireResponseCreated: false,
      medicationListReconciled: false,
      allergyListReconciled: false,
      healthHistoryReconciled: false,
      confirmedNegativeEstablished: false,
      clinicianReviewCreated: false,
      clinicalIntakeCompleted: false,
      clinicalEligibilityEstablished: false,
      clinicalTriageChanged: false,
      patientRecordChanged: false,
      practiceAccepted: false,
      requestCreated: false,
      queueEntered: false,
      careAuthorized: false,
      prescribingEnabled: false,
      direction: 'Review the three server-derived categories. If anything is wrong or missing, stop.',
      limitations: ['No QuestionnaireResponse, reconciliation, intake completion, request, queue, prescribing, or care capability is created.'],
    }
    const preRequestReadiness = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 22,
      applicantStatus: 'SyntheticClinicalInformationSummaryConfirmed',
      preRequestReadinessSnapshotFingerprint: '9'.repeat(64),
      policyKey: 'SYNTHETIC_APPLICANT_PRE_REQUEST_READINESS',
      policyVersion: 1,
      sections: [
        { sectionKey: 'Registration', receiptState: 'ReceiptRecorded', outstandingRoute: 'IdentityAndIntakeCompletionStillRequired' },
        { sectionKey: 'Insurance', receiptState: 'ReceiptRecorded', outstandingRoute: 'CoverageAndRenderingClinicianVerificationStillRequired' },
        { sectionKey: 'CommunicationAccess', receiptState: 'PreferencesRecorded', outstandingRoute: 'AssistedCommunicationPlanningRequired' },
        { sectionKey: 'DevicePreparation', receiptState: 'ClientReportedPreparationRecorded', outstandingRoute: 'PreConsultationTechnologyRecheckRequired' },
        { sectionKey: 'ClinicalInformation', receiptState: 'PatientReportedSummaryConfirmed', outstandingRoute: 'AdditionalClinicalInformationCollectionRequired' },
      ],
      overallRoute: 'AdditionalClinicalInformationRequired',
      readinessAcknowledged: false,
      acknowledgedAt: null,
      priorSectionsReviewedAcknowledged: false,
      outstandingStepsRemainAcknowledged: false,
      noRequestOrQueueCreatedAcknowledged: false,
      correctionRequiresSeparateWorkflowAcknowledged: false,
      identityAssuranceEstablished: false,
      coverageGuaranteed: false,
      renderingClinicianNetworkVerified: false,
      interpreterOrAccommodationArranged: false,
      technologyReady: false,
      clinicalInformationReconciled: false,
      clinicalIntakeCompleted: false,
      clinicalEligibilityEstablished: false,
      legalConsentEstablished: false,
      staffReviewCreated: false,
      clinicianReviewCreated: false,
      practiceAccepted: false,
      patientRecordChanged: false,
      requestCreated: false,
      queueEntered: false,
      appointmentCreated: false,
      encounterCreated: false,
      careAuthorized: false,
      prescribingEnabled: false,
      billingEnabled: false,
      claimCreated: false,
      integrationEnabled: false,
      externalCallPerformed: false,
      direction: 'Review all five coarse sections. If anything is wrong or missing, stop.',
      limitations: ['No task, request, queue entry, appointment, encounter, care, prescribing, billing, claim, integration, or external action is created.'],
    }
    const practiceReview = {
      applicantId: prospectiveApplicant.applicantId,
      applicantVersion: 23,
      applicantStatus: 'SyntheticPreRequestReadinessAcknowledged',
      practiceReviewSnapshotFingerprint: '8'.repeat(64),
      policyKey: 'SYNTHETIC_APPLICANT_PRACTICE_REVIEW_SUBMISSION',
      policyVersion: 1,
      reviewRoute: 'AdditionalClinicalInformationRequired',
      submissionReady: true,
      submitted: false,
      practiceReviewCaseId: null,
      reviewStatus: null,
      submittedAt: null,
      patientReportedInformationAcknowledged: false,
      practiceMayRequestInformationOrDeclineAcknowledged: false,
      noTelehealthRequestOrCareQueueAcknowledged: false,
      worseningSymptomsRequireImmediateActionAcknowledged: false,
      staffReviewCreated: false,
      clinicianReviewCreated: false,
      practiceAccepted: false,
      patientRecordChanged: false,
      telehealthRequestCreated: false,
      patientCareQueueEntered: false,
      clinicianQueueEntered: false,
      appointmentCreated: false,
      encounterCreated: false,
      careAuthorized: false,
      prescribingEnabled: false,
      billingEnabled: false,
      claimCreated: false,
      integrationEnabled: false,
      externalCallPerformed: false,
      direction: 'Submit one synthetic practice review work item; no request or queue entry is created.',
      limitations: ['No doctor search, queue position, practice acceptance, telehealth request, appointment, encounter, prescribing, billing, claim, integration, or care capability is created.'],
    }
    await page.route('**/api/telehealth/v1/applicants/**', async (route) => {
      const request = route.request()
      const path = new URL(request.url()).pathname
      if (path.endsWith('/practice-review-submission')) {
        if (request.method() === 'GET') {
          practiceReviewGetCalls += 1
          if (practiceReviewGetCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Practice review submission could not be loaded.' }) })
            return
          }
        }
        if (request.method() === 'POST') {
          practiceReviewPostCalls += 1
          practiceReviewKeys.push(request.headers()['x-idempotency-key'])
          practiceReviewBodies.push(request.postDataJSON() as Record<string, unknown>)
          if (practiceReviewPostCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Practice review result unknown; retry unchanged.' }) })
            return
          }
          practiceReviewSubmitted = true
        }
        await route.fulfill({ json: practiceReviewSubmitted
          ? {
              ...practiceReview,
              applicantVersion: 24,
              applicantStatus: 'SyntheticPracticeReviewSubmitted',
              submitted: true,
              practiceReviewCaseId: '00000000-0000-0000-0000-000000000351',
              reviewStatus: 'PendingPracticeReview',
              submittedAt: '2026-08-28T03:10:00Z',
              patientReportedInformationAcknowledged: true,
              practiceMayRequestInformationOrDeclineAcknowledged: true,
              noTelehealthRequestOrCareQueueAcknowledged: true,
              worseningSymptomsRequireImmediateActionAcknowledged: true,
              staffReviewCreated: true,
              direction: 'Submitted for practice review; no request or care queue was created.',
            }
          : practiceReview })
        return
      }
      if (path.endsWith('/pre-request-readiness')) {
        if (request.method() === 'GET') {
          preRequestReadinessGetCalls += 1
          if (preRequestReadinessGetCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Pre-request readiness could not be loaded.' }) })
            return
          }
        }
        if (request.method() === 'POST') {
          preRequestReadinessPostCalls += 1
          preRequestReadinessKeys.push(request.headers()['x-idempotency-key'])
          preRequestReadinessBodies.push(request.postDataJSON() as Record<string, unknown>)
          if (preRequestReadinessPostCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Pre-request readiness result unknown; retry unchanged.' }) })
            return
          }
          preRequestReadinessAcknowledged = true
        }
        await route.fulfill({ json: preRequestReadinessAcknowledged
          ? {
              ...preRequestReadiness,
              applicantVersion: 23,
              applicantStatus: 'SyntheticPreRequestReadinessAcknowledged',
              readinessAcknowledged: true,
              acknowledgedAt: '2026-08-28T03:05:00Z',
              priorSectionsReviewedAcknowledged: true,
              outstandingStepsRemainAcknowledged: true,
              noRequestOrQueueCreatedAcknowledged: true,
              correctionRequiresSeparateWorkflowAcknowledged: true,
              direction: 'Additional clinical information still requires a separately authorized workflow. No request or review task was created.',
            }
          : preRequestReadiness })
        return
      }
      if (path.endsWith('/clinical-information-summary')) {
        if (request.method() === 'GET') {
          clinicalInformationSummaryGetCalls += 1
          if (clinicalInformationSummaryGetCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Clinical-information summary could not be loaded.' }) })
            return
          }
        }
        if (request.method() === 'POST') {
          clinicalInformationSummaryPostCalls += 1
          clinicalInformationSummaryKeys.push(request.headers()['x-idempotency-key'])
          clinicalInformationSummaryBodies.push(request.postDataJSON() as Record<string, unknown>)
          if (clinicalInformationSummaryPostCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Clinical-information summary result unknown; retry unchanged.' }) })
            return
          }
          clinicalInformationSummaryConfirmed = true
        }
        await route.fulfill({ json: clinicalInformationSummaryConfirmed
          ? {
              ...clinicalInformationSummary,
              applicantVersion: 22,
              applicantStatus: 'SyntheticClinicalInformationSummaryConfirmed',
              summaryConfirmed: true,
              confirmedAt: '2026-08-28T03:00:00Z',
              patientReportedMayBeIncompleteAcknowledged: true,
              notClinicallyVerifiedOrReconciledAcknowledged: true,
              noIntakeCompletionOrEligibilityAcknowledged: true,
              correctionRequiresSeparateWorkflowAcknowledged: true,
              direction: 'Additional information still requires a separately authorized workflow; no task or request was created.',
            }
          : clinicalInformationSummary })
        return
      }
      if (path.endsWith('/health-history-information')) {
        if (request.method() === 'GET') {
          healthHistoryInformationGetCalls += 1
          if (healthHistoryInformationGetCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Health history information could not be loaded.' }) })
            return
          }
        }
        if (request.method() === 'POST') {
          healthHistoryInformationPostCalls += 1
          healthHistoryInformationKeys.push(request.headers()['x-idempotency-key'])
          healthHistoryInformationBodies.push(request.postDataJSON() as Record<string, unknown>)
          if (healthHistoryInformationPostCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Health history information result unknown; retry unchanged.' }) })
            return
          }
          healthHistoryInformationRecorded = true
        }
        await route.fulfill({ json: healthHistoryInformationRecorded
          ? {
              ...healthHistoryInformation,
              applicantVersion: 21,
              applicantStatus: 'SyntheticHealthHistoryInformationRecorded',
              healthHistoryInformationRecorded: true,
              recordedAt: '2026-08-28T02:35:00Z',
              healthHistoryTopics: [
                { catalogKey: 'ongoing-health-conditions', displayName: 'Ongoing health conditions', category: 'ConditionOrConcern', catalogVersion: 1, codingSystem: 'LOCAL_SYNTHETIC_ONLY', snomedCtMapped: false, icd10CmMapped: false, loincMapped: false },
                { catalogKey: 'behavioral-health-or-substance-use', displayName: 'Behavioral health or substance use information', category: 'SensitiveHistory', catalogVersion: 1, codingSystem: 'LOCAL_SYNTHETIC_ONLY', snomedCtMapped: false, icd10CmMapped: false, loincMapped: false },
              ],
              additionalOrUnlistedTopicsReported: true,
              reviewRoute: 'AdditionalHealthHistoryCollectionRequired',
              patientReportedMayBeIncompleteAcknowledged: true,
              topicSelectionIsNotDiagnosisAcknowledged: true,
              noStatusOrTimingCapturedAcknowledged: true,
              clinicianVerificationRequiredAcknowledged: true,
              direction: 'Additional collection and clinician verification remain required; none was created.',
            }
          : healthHistoryInformation })
        return
      }
      if (path.endsWith('/allergy-information')) {
        if (request.method() === 'GET') {
          allergyInformationGetCalls += 1
          if (allergyInformationGetCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Allergy information could not be loaded.' }) })
            return
          }
        }
        if (request.method() === 'POST') {
          allergyInformationPostCalls += 1
          allergyInformationKeys.push(request.headers()['x-idempotency-key'])
          allergyInformationBodies.push(request.postDataJSON() as Record<string, unknown>)
          if (allergyInformationPostCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Allergy information result unknown; retry unchanged.' }) })
            return
          }
          allergyInformationRecorded = true
        }
        await route.fulfill({ json: allergyInformationRecorded
          ? {
              ...allergyInformation,
              applicantVersion: 20,
              applicantStatus: 'SyntheticAllergyInformationRecorded',
              allergyInformationRecorded: true,
              recordedAt: '2026-08-28T02:30:00Z',
              allergyItems: [
                { catalogKey: 'amoxicillin', displayName: 'Amoxicillin', category: 'Medication', catalogVersion: 1, codingSystem: 'LOCAL_SYNTHETIC_ONLY', snomedCtMapped: false, rxNormMapped: false },
                { catalogKey: 'peanut', displayName: 'Peanut', category: 'Food', catalogVersion: 1, codingSystem: 'LOCAL_SYNTHETIC_ONLY', snomedCtMapped: false, rxNormMapped: false },
              ],
              additionalOrUnlistedItemsReported: true,
              reviewRoute: 'AdditionalAllergyCollectionRequired',
              patientReportedMayBeIncompleteAcknowledged: true,
              syntheticCatalogIncompleteAcknowledged: true,
              noReactionOrCriticalityCapturedAcknowledged: true,
              clinicianVerificationRequiredAcknowledged: true,
              direction: 'Additional collection and clinician verification remain required; none was created.',
            }
          : allergyInformation })
        return
      }
      if (path.endsWith('/medication-information')) {
        if (request.method() === 'GET') {
          medicationInformationGetCalls += 1
          if (medicationInformationGetCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Medication information could not be loaded.' }) })
            return
          }
        }
        if (request.method() === 'POST') {
          medicationInformationPostCalls += 1
          medicationInformationKeys.push(request.headers()['x-idempotency-key'])
          medicationInformationBodies.push(request.postDataJSON() as Record<string, unknown>)
          if (medicationInformationPostCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Medication information result unknown; retry unchanged.' }) })
            return
          }
          medicationInformationRecorded = true
        }
        await route.fulfill({ json: medicationInformationRecorded
          ? {
              ...medicationInformation,
              applicantVersion: 19,
              applicantStatus: 'SyntheticMedicationInformationRecorded',
              medicationInformationRecorded: true,
              recordedAt: '2026-08-28T02:25:00Z',
              medicationItems: [
                { catalogKey: 'sumatriptan', displayName: 'Sumatriptan', catalogVersion: 1, codingSystem: 'LOCAL_SYNTHETIC_ONLY', rxNormMapped: false, reportedUseStatus: 'Taking' },
                { catalogKey: 'melatonin', displayName: 'Melatonin', catalogVersion: 1, codingSystem: 'LOCAL_SYNTHETIC_ONLY', rxNormMapped: false, reportedUseStatus: 'Unsure' },
              ],
              additionalOrUnlistedItemsReported: true,
              reviewRoute: 'AdditionalMedicationCollectionRequired',
              patientReportedMayBeIncompleteAcknowledged: true,
              syntheticCatalogIncompleteAcknowledged: true,
              noDoseOrDirectionsCapturedAcknowledged: true,
              clinicianReconciliationRequiredAcknowledged: true,
              direction: 'Additional collection and clinician reconciliation remain required; none was created.',
            }
          : medicationInformation })
        return
      }
      if (path.endsWith('/clinical-information-inventory')) {
        if (request.method() === 'POST') {
          clinicalInformationInventoryPostCalls += 1
          clinicalInformationInventoryKeys.push(request.headers()['x-idempotency-key'])
          clinicalInformationInventoryBodies.push(request.postDataJSON() as Record<string, unknown>)
          if (clinicalInformationInventoryPostCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Clinical inventory result unknown; retry unchanged.' }) })
            return
          }
          clinicalInformationInventoryRecorded = true
        }
        await route.fulfill({ json: clinicalInformationInventoryRecorded
          ? {
              ...clinicalInformationInventory,
              applicantVersion: 18,
              applicantStatus: 'SyntheticClinicalInformationInventoryRecorded',
              inventoryRecorded: true,
              recordedAt: '2026-08-28T02:20:00Z',
              medicationsStatus: 'ItemsToReview',
              allergiesOrIntolerancesStatus: 'PatientReportsNone',
              otherHealthHistoryStatus: 'ItemsToReview',
              reviewRoute: 'DetailedCollectionRequired',
              patientReportedMayBeIncompleteAcknowledged: true,
              noClinicalDetailsCapturedAcknowledged: true,
              clinicianReconciliationRequiredAcknowledged: true,
              direction: 'Detailed collection and clinician reconciliation are required; no review was created.',
            }
          : clinicalInformationInventory })
        return
      }
      if (path.endsWith('/device-preparation')) {
        if (request.method() === 'POST') {
          devicePreparationPostCalls += 1
          devicePreparationKeys.push(request.headers()['x-idempotency-key'])
          devicePreparationBodies.push(request.postDataJSON() as Record<string, unknown>)
          if (devicePreparationPostCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Device preparation result unknown; retry unchanged.' }) })
            return
          }
          devicePreparationRecorded = true
        }
        await route.fulfill({ json: devicePreparationRecorded
          ? {
              ...devicePreparation,
              applicantVersion: 17,
              applicantStatus: 'SyntheticDevicePreparationRecorded',
              preparationRecorded: true,
              recordedAt: '2026-08-28T02:15:00Z',
              browserSupported: true,
              cameraAvailable: true,
              microphoneAvailable: true,
              speakerAvailable: true,
              networkQuality: 'Good',
              clientReportedResultAcknowledged: true,
              noReadinessGuaranteeAcknowledged: true,
              recheckBeforeConsultationAcknowledged: true,
            }
          : devicePreparation })
        return
      }
      if (path.endsWith('/communication-access-readiness')) {
        if (request.method() === 'POST') {
          communicationAccessPostCalls += 1
          communicationAccessKeys.push(request.headers()['x-idempotency-key'])
          communicationAccessBodies.push(request.postDataJSON() as Record<string, unknown>)
          if (communicationAccessPostCalls === 1) {
            await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Communication readiness result unknown; retry unchanged.' }) })
            return
          }
          communicationAccessRecorded = true
        }
        await route.fulfill({ json: communicationAccessRecorded
          ? {
              ...communicationAccessReadiness,
              applicantVersion: 16,
              applicantStatus: 'SyntheticCommunicationAccessReadinessRecorded',
              preferredSpokenLanguage: 'Spanish',
              interpreterRequested: true,
              accessibilitySupportRequested: true,
              readinessRecorded: true,
              recordedAt: '2026-08-28T01:15:00Z',
              safeAndPrivateToCommunicateConfirmed: true,
            }
          : communicationAccessReadiness })
        return
      }
      if (request.method() === 'POST' && path.endsWith('/insurance-handoff/confirmation')) {
        insuranceHandoffPostCalls += 1
        insuranceHandoffKeys.push(request.headers()['x-idempotency-key'])
        insuranceHandoffBodies.push(request.postDataJSON() as Record<string, unknown>)
        if (insuranceHandoffPostCalls === 1) {
          await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Insurance confirmation result unknown; retry unchanged.' }) })
          return
        }
        insuranceHandoffConfirmed = true
        await route.fulfill({ json: { ...insuranceHandoff, applicantVersion: 15, applicantStatus: 'SyntheticInsuranceDetailsConfirmed', insuranceDetailsConfirmed: true, confirmedAt: '2026-08-27T14:36:00Z' } })
        return
      }
      if (request.method() === 'GET' && path.endsWith('/insurance-handoff')) {
        await route.fulfill({ json: insuranceHandoffConfirmed
          ? { ...insuranceHandoff, applicantVersion: 15, applicantStatus: 'SyntheticInsuranceDetailsConfirmed', insuranceDetailsConfirmed: true, confirmedAt: '2026-08-27T14:36:00Z' }
          : insuranceHandoff })
        return
      }
      if (request.method() === 'POST' && path.endsWith('/registration-details/confirmation')) {
        registrationDetailsPostCalls += 1
        registrationDetailsKeys.push(request.headers()['x-idempotency-key'])
        registrationDetailsBodies.push(request.postDataJSON() as Record<string, unknown>)
        if (registrationDetailsPostCalls === 1) {
          await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Synthetic confirmation result unavailable; retry unchanged.' }) })
          return
        }
        registrationDetailsConfirmed = true
        await route.fulfill({ json: { ...registrationDetails, applicantVersion: 14, applicantStatus: 'SyntheticMinimumRegistrationDetailsConfirmed', confirmed: true, confirmedAt: '2026-08-27T14:35:00Z' } })
        return
      }
      if (request.method() === 'GET' && path.endsWith('/registration-details')) {
        await route.fulfill({ json: registrationDetailsConfirmed
          ? { ...registrationDetails, applicantVersion: 14, applicantStatus: 'SyntheticMinimumRegistrationDetailsConfirmed', confirmed: true, confirmedAt: '2026-08-27T14:35:00Z' }
          : registrationDetails })
        return
      }
      if (request.method() === 'POST' && path.endsWith('/telehealth-notice/acknowledgment')) {
        postCalls += 1
        commandKeys.push(request.headers()['x-idempotency-key'])
        commandBodies.push(request.postDataJSON() as Record<string, unknown>)
        if (postCalls === 1) {
          await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Synthetic acknowledgment result unavailable; retry unchanged.' }) })
          return
        }
        acknowledged = true
        await route.fulfill({ json: { ...notice, applicantVersion: 13, applicantStatus: 'SyntheticTelehealthNoticeAcknowledged', acknowledged: true, acknowledgedAt: '2026-08-27T14:30:00Z' } })
        return
      }
      if (request.method() === 'GET' && path.endsWith('/telehealth-notice')) {
        await route.fulfill({ json: acknowledged
          ? { ...notice, applicantVersion: 13, applicantStatus: 'SyntheticTelehealthNoticeAcknowledged', acknowledged: true, acknowledgedAt: '2026-08-27T14:30:00Z' }
          : notice })
        return
      }
      await route.fulfill({ json: practiceReviewSubmitted
        ? { ...promotedApplicant, status: 'SyntheticPracticeReviewSubmitted', version: 24, nextAction: 'Pending practice review; no request or care queue exists.' }
        : preRequestReadinessAcknowledged
        ? { ...promotedApplicant, status: 'SyntheticPreRequestReadinessAcknowledged', version: 23, nextAction: 'No request or queue entry exists.' }
        : clinicalInformationSummaryConfirmed
        ? { ...promotedApplicant, status: 'SyntheticClinicalInformationSummaryConfirmed', version: 22, nextAction: 'All clinical and care gates remain closed.' }
        : healthHistoryInformationRecorded
        ? { ...promotedApplicant, status: 'SyntheticHealthHistoryInformationRecorded', version: 21, nextAction: 'Review the no-edit clinical-information summary.' }
        : allergyInformationRecorded
        ? { ...promotedApplicant, status: 'SyntheticAllergyInformationRecorded', version: 20, nextAction: 'All clinical and care gates remain closed.' }
        : medicationInformationRecorded
        ? { ...promotedApplicant, status: 'SyntheticMedicationInformationRecorded', version: 19, nextAction: 'All clinical and care gates remain closed.' }
        : clinicalInformationInventoryRecorded
        ? { ...promotedApplicant, status: 'SyntheticClinicalInformationInventoryRecorded', version: 18, nextAction: 'All clinical and care gates remain closed.' }
        : devicePreparationRecorded
        ? { ...promotedApplicant, status: 'SyntheticDevicePreparationRecorded', version: 17, nextAction: 'All later intake gates remain closed.' }
        : communicationAccessRecorded
        ? { ...promotedApplicant, status: 'SyntheticCommunicationAccessReadinessRecorded', version: 16, nextAction: 'All later intake gates remain closed.' }
        : insuranceHandoffConfirmed
        ? { ...promotedApplicant, status: 'SyntheticInsuranceDetailsConfirmed', version: 15, nextAction: 'Confirm communication and access readiness.' }
        : registrationDetailsConfirmed
        ? { ...promotedApplicant, status: 'SyntheticMinimumRegistrationDetailsConfirmed', version: 14, nextAction: 'Confirm the masked synthetic insurance handoff.' }
        : acknowledged
        ? { ...promotedApplicant, status: 'SyntheticTelehealthNoticeAcknowledged', version: 13, nextAction: 'A clinician must still complete required consent before care.' }
        : promotedApplicant })
    })

    await page.goto('/telehealth/new')
    await expect(page.getByRole('heading', { name: 'Georgia synthetic telehealth notice' })).toBeVisible()
    await expect(page.getByText(/not final legal consent/i)).toBeVisible()
    await expect(page.getByRole('link', { name: /official source/i })).toHaveAttribute('href', notice.sourceUrl)
    const submit = page.getByRole('button', { name: 'Acknowledge state-specific notice' })
    await expect(submit).toBeDisabled()
    await page.getByLabel(/currently located in GA/i).check()
    await page.getByLabel(/remote technology and can have limits/i).check()
    await page.getByLabel(/privacy and technology limitations/i).check()
    await page.getByLabel(/not emergency care/i).check()
    await page.getByLabel(/in-person evaluation may be available/i).check()
    await page.getByLabel(/licensed clinician must later reconfirm/i).check()
    await page.getByLabel(/synthetic NON_PRODUCTION demonstration/i).check()
    await submit.click()
    await expect(page.getByRole('alert')).toContainText('retry unchanged')
    await submit.click()

    await expect(page.getByRole('heading', { name: 'State-specific telehealth notice acknowledged' })).toBeVisible()
    await expect(page.getByText('Not established')).toBeVisible()
    await expect(page.getByText('Not completed')).toBeVisible()
    await expect(page.getByText(/no portal access, completed intake, practice acceptance, insurance record, telehealth request, queue entry, or care capability/i)).toBeVisible()
    expect(commandKeys).toHaveLength(2)
    expect(commandKeys[0]).toBe(commandKeys[1])
    expect(commandBodies[0]).toEqual({
      expectedVersion: 12,
      noticeKey: 'GA_TELEHEALTH_NOTICE_V1',
      noticeVersion: 1,
      currentLocationStateCode: 'GA',
      currentLocationConfirmed: true,
      modeOfCareAcknowledged: true,
      privacyLimitationsAcknowledged: true,
      emergencyInstructionsAcknowledged: true,
      inPersonOptionAcknowledged: true,
      clinicianReconfirmationRequiredAcknowledged: true,
      syntheticDataConfirmed: true,
    })
    expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toMatch(/noticeKey|acknowledgedAt|consent|currentLocation/i)

    const registrationHeading = page.getByRole('heading', { name: 'Confirm the minimum copied details' })
    await expect(registrationHeading).toBeVisible()
    await expect(registrationHeading.locator('..')).toContainText('Avery Notice')
    await expect(registrationHeading.locator('..')).toContainText('1990-03-14')
    await expect(registrationHeading.locator('..')).toContainText('a•••@example.test')
    await expect(registrationHeading.locator('..')).toContainText('(***) ***-0199')
    await expect(registrationHeading.locator('..')).toContainText('GA 30303')
    await expect(page.getByText(/cannot edit records or complete a correction/i)).toBeVisible()
    await expect(page.getByText('avery.notice@example.test')).toHaveCount(0)
    await expect(page.getByText('10 Main Street')).toHaveCount(0)
    const registrationSubmit = page.getByRole('button', { name: 'Confirm minimum registration details' })
    await expect(registrationSubmit).toBeDisabled()
    await page.getByLabel(/legal name and date of birth shown above/i).check()
    await page.getByLabel(/masked verified email and callback phone/i).check()
    await page.getByLabel(/residence state and postal code shown above/i).check()
    await page.getByLabel(/do not need a correction/i).check()
    await page.getByLabel(/synthetic NON_PRODUCTION demonstration/i).check()
    await registrationSubmit.click()
    await expect(page.getByRole('alert')).toContainText('retry unchanged')
    await registrationSubmit.click()

    const confirmedHeading = page.getByRole('heading', { name: 'Minimum registration details confirmed' })
    await expect(confirmedHeading).toBeVisible()
    await expect(confirmedHeading.locator('..')).toContainText('Patient record changedNo')
    await expect(confirmedHeading.locator('..')).toContainText('Identity assurance establishedNo')
    await expect(confirmedHeading.locator('..')).toContainText('Canonical insurance createdNo')
    await expect(page.getByText(/No portal, correction, completed intake, legal consent, practice acceptance, coverage, request, queue entry, appointment, encounter, or care capability was created/i)).toBeVisible()
    expect(registrationDetailsKeys).toHaveLength(2)
    expect(registrationDetailsKeys[0]).toBe(registrationDetailsKeys[1])
    expect(registrationDetailsBodies[0]).toEqual({
      expectedVersion: 13,
      detailsFingerprint: 'f'.repeat(64),
      legalNameAndBirthDateConfirmed: true,
      contactChannelsConfirmed: true,
      residenceRegionConfirmed: true,
      noCorrectionsNeededConfirmed: true,
      syntheticDataConfirmed: true,
    })
    expect(Object.keys(registrationDetailsBodies[0]).sort()).toEqual([
      'contactChannelsConfirmed',
      'detailsFingerprint',
      'expectedVersion',
      'legalNameAndBirthDateConfirmed',
      'noCorrectionsNeededConfirmed',
      'residenceRegionConfirmed',
      'syntheticDataConfirmed',
    ])
    expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toMatch(/Avery|Notice|1990-03-14|30303|detailsFingerprint|email|phone|patientId/i)

    const insuranceHeading = page.getByRole('heading', { name: 'Confirm the masked synthetic insurance details' })
    await expect(insuranceHeading).toBeVisible()
    await expect(insuranceHeading.locator('..')).toContainText('Harbor Mutual')
    await expect(insuranceHeading.locator('..')).toContainText('High Deductible')
    await expect(insuranceHeading.locator('..')).toContainText('••••A123')
    await expect(insuranceHeading.locator('..')).toContainText('••••G456')
    await expect(insuranceHeading.locator('..')).toContainText('Rendering physician checkedNo')
    await expect(insuranceHeading.locator('..')).toContainText('Coverage verifiedNo')
    await expect(page.getByText(/not guarantees of coverage, benefits, payment, cost, or rendering-physician participation/i)).toBeVisible()
    await expect(page.getByText('SYN-MEMBER-A123')).toHaveCount(0)
    await expect(page.getByText('SYN-GROUP-G456')).toHaveCount(0)
    const insuranceSubmit = page.getByRole('button', { name: 'Confirm synthetic insurance details' })
    await expect(insuranceSubmit).toBeDisabled()
    await page.getByLabel(/payer and product shown above/i).check()
    await page.getByLabel(/masked synthetic member ID and group number/i).check()
    await page.getByLabel(/subscriber relationship and coverage priority/i).check()
    await page.getByLabel(/eligibility and practice-level network fixtures/i).check()
    await page.getByLabel(/does not create insurance coverage or request care/i).check()
    await insuranceSubmit.click()
    await expect(page.getByRole('alert')).toContainText('retry unchanged')
    await insuranceSubmit.click()

    const insuranceConfirmedHeading = page.getByRole('heading', { name: 'Synthetic insurance details confirmed' })
    await expect(insuranceConfirmedHeading).toBeVisible()
    await expect(insuranceConfirmedHeading.locator('..')).toContainText('Rendering physician checkedNo')
    await expect(insuranceConfirmedHeading.locator('..')).toContainText('Coverage verifiedNo')
    await expect(insuranceConfirmedHeading.locator('..')).toContainText('Exact network confirmedNo')
    await expect(insuranceConfirmedHeading.locator('..')).toContainText('Canonical coverage createdNo')
    await expect(page.getByText(/No patient change, portal, completed intake, legal consent, practice acceptance, financial record, request, queue entry, appointment, encounter, claim, or care capability was created/i)).toBeVisible()
    expect(insuranceHandoffKeys).toHaveLength(2)
    expect(insuranceHandoffKeys[0]).toBe(insuranceHandoffKeys[1])
    expect(insuranceHandoffBodies[0]).toEqual({
      expectedVersion: 14,
      insuranceSnapshotFingerprint: 'e'.repeat(64),
      payerAndProductConfirmed: true,
      maskedMemberDetailsConfirmed: true,
      subscriberRelationshipConfirmed: true,
      evidenceLimitationsAcknowledged: true,
      syntheticDataConfirmed: true,
    })
    expect(Object.keys(insuranceHandoffBodies[0]).sort()).toEqual([
      'evidenceLimitationsAcknowledged',
      'expectedVersion',
      'insuranceSnapshotFingerprint',
      'maskedMemberDetailsConfirmed',
      'payerAndProductConfirmed',
      'subscriberRelationshipConfirmed',
      'syntheticDataConfirmed',
    ])
    expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toMatch(/Harbor|A123|G456|insuranceSnapshotFingerprint|memberId|groupNumber|patientId/i)

    const communicationHeading = page.getByRole('heading', { name: 'Confirm how this synthetic visit could communicate' })
    await expect(communicationHeading).toBeVisible()
    await expect(communicationHeading.locator('..')).toContainText('Current locationGA')
    await expect(communicationHeading.locator('..')).toContainText('Callback number(***) ***-0199')
    await expect(communicationHeading.locator('..')).toContainText('Interpreter assignedNo')
    await expect(communicationHeading.locator('..')).toContainText('Accessibility accommodation arrangedNo')
    await expect(communicationHeading.locator('..')).toContainText('Technology readiness completedNo')
    await expect(page.getByText('4045550199')).toHaveCount(0)
    const communicationSubmit = page.getByRole('button', { name: 'Record communication readiness' })
    await expect(communicationSubmit).toBeDisabled()
    await page.getByLabel('Preferred spoken language').selectOption('Spanish')
    await page.getByLabel(/would request an interpreter/i).check()
    await page.getByLabel(/would request accessibility support/i).check()
    await page.getByLabel(/currently in GA/i).check()
    await page.getByLabel(/callback number I expect/i).check()
    await page.getByLabel(/communicate safely and privately/i).check()
    await page.getByLabel(/real visit needs a disconnection plan/i).check()
    await page.getByLabel(/synthetic NON_PRODUCTION receipt/i).check()
    await expect(communicationSubmit).toBeEnabled()
    await communicationSubmit.click()
    await expect(page.getByRole('alert')).toContainText('retry unchanged')
    await communicationSubmit.click()

    const communicationRecordedHeading = page.getByRole('heading', { name: 'Communication and access readiness recorded' })
    await expect(communicationRecordedHeading).toBeVisible()
    await expect(communicationRecordedHeading.locator('..')).toContainText('Preferred spoken languageSpanish')
    await expect(communicationRecordedHeading.locator('..')).toContainText('Interpreter requestedYes — preference only')
    await expect(communicationRecordedHeading.locator('..')).toContainText('Interpreter assignedNo')
    await expect(communicationRecordedHeading.locator('..')).toContainText('Accessibility support requestedYes — preference only')
    await expect(communicationRecordedHeading.locator('..')).toContainText('Accommodation arrangedNo')
    await expect(communicationRecordedHeading.locator('..')).toContainText('Technology readiness completedNo')
    await expect(page.getByText(/No interpreter, accommodation, communication session, support request, patient change, completed intake, legal consent, practice acceptance, request, queue entry, appointment, encounter, or care capability was created/i)).toBeVisible()
    expect(communicationAccessKeys).toHaveLength(2)
    expect(communicationAccessKeys[0]).toBe(communicationAccessKeys[1])
    expect(communicationAccessBodies[0]).toEqual({
      expectedVersion: 15,
      contextSnapshotFingerprint: 'd'.repeat(64),
      preferredSpokenLanguage: 'Spanish',
      interpreterRequested: true,
      accessibilitySupportRequested: true,
      currentLocationConfirmed: true,
      callbackNumberConfirmed: true,
      safeAndPrivateToCommunicateConfirmed: true,
      disconnectionAndEmergencyPlanAcknowledged: true,
      syntheticDataConfirmed: true,
    })
    expect(Object.keys(communicationAccessBodies[0]).sort()).toEqual([
      'accessibilitySupportRequested',
      'callbackNumberConfirmed',
      'contextSnapshotFingerprint',
      'currentLocationConfirmed',
      'disconnectionAndEmergencyPlanAcknowledged',
      'expectedVersion',
      'interpreterRequested',
      'preferredSpokenLanguage',
      'safeAndPrivateToCommunicateConfirmed',
      'syntheticDataConfirmed',
    ])
    expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toMatch(/Spanish|interpreter|accessibility|contextSnapshotFingerprint|4045550199|patientId/i)

    const deviceHeading = page.getByRole('heading', { name: 'Check this browser, camera, microphone, speaker, and connection' })
    await expect(deviceHeading).toBeVisible()
    await expect(deviceHeading.locator('..')).toContainText('Every temporary test track is stopped immediately')
    await expect(deviceHeading.locator('..')).toContainText('No image, audio, recording, device name, device ID, browser details, IP address, or connection diagnostic is sent or stored')
    const deviceSubmit = page.getByRole('button', { name: 'Record device preparation' })
    await expect(deviceSubmit).toBeDisabled()
    await page.getByRole('button', { name: 'Run local device check' }).click()
    const localCheckHeading = page.getByRole('heading', { name: 'Local check complete' })
    await expect(localCheckHeading).toBeVisible()
    await expect(localCheckHeading.locator('..')).toContainText('Connection indicationGood')
    await expect(localCheckHeading.locator('..')).toContainText('Temporary media tracksStopped')
    await page.getByLabel(/client-reported, point-in-time browser result/i).check()
    await page.getByLabel(/does not certify technology readiness/i).check()
    await page.getByLabel(/checked again before any consultation/i).check()
    await expect(deviceSubmit).toBeEnabled()
    await deviceSubmit.click()
    await expect(page.getByRole('alert')).toContainText('retry unchanged')
    await deviceSubmit.click()

    const deviceRecordedHeading = page.getByRole('heading', { name: 'Device preparation recorded' })
    await expect(deviceRecordedHeading).toBeVisible()
    await expect(deviceRecordedHeading.locator('..')).toContainText('Supported browser reportedYes')
    await expect(deviceRecordedHeading.locator('..')).toContainText('Technology readyNo')
    await expect(deviceRecordedHeading.locator('..')).toContainText('Waiting room createdNo')
    await expect(deviceRecordedHeading.locator('..')).toContainText('Media session createdNo')
    await expect(page.getByText(/No technology-ready status, waiting room, media session, communication, support arrangement, patient change, completed intake, legal consent, practice acceptance, request, queue entry, appointment, encounter, or care capability was created/i)).toBeVisible()
    expect(devicePreparationKeys).toHaveLength(2)
    expect(devicePreparationKeys[0]).toBe(devicePreparationKeys[1])
    expect(devicePreparationBodies[0]).toEqual({
      expectedVersion: 16,
      preparationSnapshotFingerprint: 'c'.repeat(64),
      browserSupported: true,
      cameraAvailable: true,
      microphoneAvailable: true,
      speakerAvailable: true,
      networkQuality: 'Good',
      clientReportedResultAcknowledged: true,
      noReadinessGuaranteeAcknowledged: true,
      recheckBeforeConsultationAcknowledged: true,
    })
    expect(Object.keys(devicePreparationBodies[0]).sort()).toEqual([
      'browserSupported',
      'cameraAvailable',
      'clientReportedResultAcknowledged',
      'expectedVersion',
      'microphoneAvailable',
      'networkQuality',
      'noReadinessGuaranteeAcknowledged',
      'preparationSnapshotFingerprint',
      'recheckBeforeConsultationAcknowledged',
      'speakerAvailable',
    ])
    expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toMatch(/device|camera|microphone|speaker|networkQuality|preparationSnapshotFingerprint|patientId/i)

    const inventoryHeading = page.getByRole('heading', { name: 'Tell us whether each category has items that need review' })
    await expect(inventoryHeading).toBeVisible()
    await expect(inventoryHeading.locator('..')).toContainText('Do not enter any medication, substance, reaction, dose, diagnosis, symptom, procedure, narrative, date, identifier, or other clinical detail')
    await expect(inventoryHeading.locator('..').locator('input[type="text"]')).toHaveCount(0)
    await expect(inventoryHeading.locator('..').locator('textarea')).toHaveCount(0)
    const medicationsGroup = page.getByRole('group', { name: 'Medications' })
    const allergiesGroup = page.getByRole('group', { name: 'Allergies or intolerances' })
    const historyGroup = page.getByRole('group', { name: 'Other health history' })
    await medicationsGroup.getByLabel(/medication items that need detailed review/i).check()
    await allergiesGroup.getByLabel(/report no allergies or intolerances/i).check()
    await historyGroup.getByLabel(/health-history items that need detailed review/i).check()
    await page.getByLabel(/patient-reported signals and may be incomplete/i).check()
    await page.getByLabel(/captures no detailed clinical information/i).check()
    await page.getByLabel(/clinician must reconcile medications/i).check()
    const inventorySubmit = page.getByRole('button', { name: 'Record clinical-information inventory' })
    await expect(inventorySubmit).toBeEnabled()
    await inventorySubmit.click()
    await expect(page.getByRole('alert')).toContainText('retry unchanged')
    await inventorySubmit.click()

    const inventoryRecordedHeading = page.getByRole('heading', { name: 'Clinical-information inventory recorded' })
    await expect(inventoryRecordedHeading).toBeVisible()
    await expect(inventoryRecordedHeading.locator('..')).toContainText('MedicationsItems need detailed review')
    await expect(inventoryRecordedHeading.locator('..')).toContainText('Allergies or intolerancesPatient reports none — not clinically reconciled')
    await expect(inventoryRecordedHeading.locator('..')).toContainText('Other health historyItems need detailed review')
    await expect(inventoryRecordedHeading.locator('..')).toContainText('Server review routeDetailedCollectionRequired')
    await expect(inventoryRecordedHeading.locator('..')).toContainText('Medication list reconciledNo')
    await expect(inventoryRecordedHeading.locator('..')).toContainText('Clinician review createdNo')
    await expect(inventoryRecordedHeading.locator('..')).toContainText('Prescribing enabledNo')
    expect(clinicalInformationInventoryKeys).toHaveLength(2)
    expect(clinicalInformationInventoryKeys[0]).toBe(clinicalInformationInventoryKeys[1])
    expect(clinicalInformationInventoryBodies[0]).toEqual({
      expectedVersion: 17,
      inventorySnapshotFingerprint: 'b'.repeat(64),
      medicationsStatus: 'ItemsToReview',
      allergiesOrIntolerancesStatus: 'PatientReportsNone',
      otherHealthHistoryStatus: 'ItemsToReview',
      patientReportedMayBeIncompleteAcknowledged: true,
      noClinicalDetailsCapturedAcknowledged: true,
      clinicianReconciliationRequiredAcknowledged: true,
    })
    expect(Object.keys(clinicalInformationInventoryBodies[0]).sort()).toEqual([
      'allergiesOrIntolerancesStatus',
      'clinicianReconciliationRequiredAcknowledged',
      'expectedVersion',
      'inventorySnapshotFingerprint',
      'medicationsStatus',
      'noClinicalDetailsCapturedAcknowledged',
      'otherHealthHistoryStatus',
      'patientReportedMayBeIncompleteAcknowledged',
    ])
    expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toMatch(/ItemsToReview|PatientReportsNone|Unsure|inventorySnapshotFingerprint|medication|allerg|history|patientId/i)

    const medicationLoadRetry = page.getByRole('button', { name: 'Try loading medication information again' })
    await expect(medicationLoadRetry).toBeVisible()
    await medicationLoadRetry.click()
    expect(medicationInformationGetCalls).toBe(2)

    const medicationHeading = page.getByRole('heading', { name: 'Confirm the medication category' })
    await expect(medicationHeading).toBeVisible()
    await expect(medicationHeading.locator('..')).toContainText('fixed local synthetic ingredient catalog is incomplete')
    await expect(medicationHeading.locator('..').locator('input[type="text"]')).toHaveCount(0)
    await expect(medicationHeading.locator('..').locator('textarea')).toHaveCount(0)
    await page.getByRole('checkbox', { name: 'Sumatriptan' }).check()
    await page.getByRole('checkbox', { name: 'Melatonin' }).check()
    await page.getByLabel('Reported use status for Melatonin').selectOption('Unsure')
    await page.getByLabel(/additional or unlisted medication items/i).check()
    await page.getByLabel(/patient-reported medication information may be incomplete/i).check()
    await page.getByLabel(/catalog is incomplete and has no RxNorm/i).check()
    await page.getByLabel(/no dose, directions, or other medication detail/i).check()
    await page.getByLabel(/clinician must reconcile medication information/i).check()
    const medicationSubmit = page.getByRole('button', { name: 'Record medication information' })
    await expect(medicationSubmit).toBeEnabled()
    await medicationSubmit.click()
    await expect(page.getByRole('alert')).toContainText('retry unchanged')
    await expect(page.getByRole('checkbox', { name: 'Sumatriptan' })).toBeChecked()
    await medicationSubmit.click()

    const medicationRecordedHeading = page.getByRole('heading', { name: 'Medication information recorded' })
    await expect(medicationRecordedHeading).toBeVisible()
    await expect(medicationRecordedHeading.locator('..')).toContainText('Selected synthetic ingredients2')
    await expect(medicationRecordedHeading.locator('..')).toContainText('Sumatriptan: Taking')
    await expect(medicationRecordedHeading.locator('..')).toContainText('Melatonin: Unsure')
    await expect(medicationRecordedHeading.locator('..')).toContainText('MedicationStatement createdNo')
    await expect(medicationRecordedHeading.locator('..')).toContainText('MedicationRequest createdNo')
    await expect(medicationRecordedHeading.locator('..')).toContainText('Interaction check performedNo')
    await expect(medicationRecordedHeading.locator('..')).toContainText('Prescribing enabledNo')
    await expect(page.getByText(/No canonical medication resource or list, reconciliation, interaction check, clinician task, patient-record change, completed intake, eligibility decision, request, queue entry, prescribing, or care capability was created/i)).toBeVisible()
    expect(medicationInformationKeys).toHaveLength(2)
    expect(medicationInformationKeys[0]).toBe(medicationInformationKeys[1])
    expect(medicationInformationBodies[0]).toEqual({
      expectedVersion: 18,
      medicationInformationSnapshotFingerprint: 'a'.repeat(64),
      medicationItems: [
        { catalogKey: 'sumatriptan', reportedUseStatus: 'Taking' },
        { catalogKey: 'melatonin', reportedUseStatus: 'Unsure' },
      ],
      additionalOrUnlistedItemsReported: true,
      patientReportedMayBeIncompleteAcknowledged: true,
      syntheticCatalogIncompleteAcknowledged: true,
      noDoseOrDirectionsCapturedAcknowledged: true,
      clinicianReconciliationRequiredAcknowledged: true,
    })
    expect(Object.keys(medicationInformationBodies[0]).sort()).toEqual([
      'additionalOrUnlistedItemsReported',
      'clinicianReconciliationRequiredAcknowledged',
      'expectedVersion',
      'medicationInformationSnapshotFingerprint',
      'medicationItems',
      'noDoseOrDirectionsCapturedAcknowledged',
      'patientReportedMayBeIncompleteAcknowledged',
      'syntheticCatalogIncompleteAcknowledged',
    ])
    expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toMatch(/sumatriptan|melatonin|Taking|Unsure|medicationInformationSnapshotFingerprint|patientId/i)

    const allergyLoadRetry = page.getByRole('button', { name: 'Try loading allergy information again' })
    await expect(allergyLoadRetry).toBeVisible()
    await allergyLoadRetry.click()
    expect(allergyInformationGetCalls).toBe(2)

    const allergyHeading = page.getByRole('heading', { name: 'Confirm the allergy or intolerance category' })
    await expect(allergyHeading).toBeVisible()
    await expect(allergyHeading.locator('..')).toContainText('fixed local synthetic substance catalog is incomplete')
    await expect(allergyHeading.locator('..').locator('input[type="text"]')).toHaveCount(0)
    await expect(allergyHeading.locator('..').locator('textarea')).toHaveCount(0)
    await page.getByRole('checkbox', { name: /Amoxicillin.*local category/i }).check()
    await page.getByRole('checkbox', { name: /Peanut.*local category/i }).check()
    await page.getByLabel(/additional or unlisted allergy or intolerance substances/i).check()
    await page.getByLabel(/patient-reported allergy or intolerance information may be incomplete/i).check()
    await page.getByLabel(/synthetic local substance catalog is incomplete/i).check()
    await page.getByLabel(/no reaction, severity, criticality/i).check()
    await page.getByLabel(/clinician must verify and reconcile allergy/i).check()
    const allergySubmit = page.getByRole('button', { name: 'Record allergy information' })
    await expect(allergySubmit).toBeEnabled()
    await allergySubmit.click()
    await expect(page.getByRole('alert')).toContainText('retry unchanged')
    await expect(page.getByRole('checkbox', { name: /Amoxicillin.*local category/i })).toBeChecked()
    await allergySubmit.click()

    const allergyRecordedHeading = page.getByRole('heading', { name: 'Allergy or intolerance information recorded' })
    await expect(allergyRecordedHeading).toBeVisible()
    await expect(allergyRecordedHeading.locator('..')).toContainText('Selected synthetic substances2')
    await expect(allergyRecordedHeading.locator('..')).toContainText('Amoxicillin: Medication')
    await expect(allergyRecordedHeading.locator('..')).toContainText('Peanut: Food')
    await expect(allergyRecordedHeading.locator('..')).toContainText('AllergyIntolerance createdNo')
    await expect(allergyRecordedHeading.locator('..')).toContainText('Reaction assessedNo')
    await expect(allergyRecordedHeading.locator('..')).toContainText('Criticality assessedNo')
    await expect(allergyRecordedHeading.locator('..')).toContainText('Contraindication check performedNo')
    await expect(allergyRecordedHeading.locator('..')).toContainText('Prescribing enabledNo')
    await expect(page.getByText(/No canonical allergy resource or confirmed negation, reconciliation, reaction or criticality assessment, contraindication check, alert, clinician task, patient-record change, completed intake, eligibility decision, request, queue entry, prescribing, or care capability was created/i)).toBeVisible()
    expect(allergyInformationKeys).toHaveLength(2)
    expect(allergyInformationKeys[0]).toBe(allergyInformationKeys[1])
    expect(allergyInformationBodies[0]).toEqual({
      expectedVersion: 19,
      allergyInformationSnapshotFingerprint: 'c'.repeat(64),
      allergyItems: [
        { catalogKey: 'amoxicillin' },
        { catalogKey: 'peanut' },
      ],
      additionalOrUnlistedItemsReported: true,
      patientReportedMayBeIncompleteAcknowledged: true,
      syntheticCatalogIncompleteAcknowledged: true,
      noReactionOrCriticalityCapturedAcknowledged: true,
      clinicianVerificationRequiredAcknowledged: true,
    })
    expect(Object.keys(allergyInformationBodies[0]).sort()).toEqual([
      'additionalOrUnlistedItemsReported',
      'allergyInformationSnapshotFingerprint',
      'allergyItems',
      'clinicianVerificationRequiredAcknowledged',
      'expectedVersion',
      'noReactionOrCriticalityCapturedAcknowledged',
      'patientReportedMayBeIncompleteAcknowledged',
      'syntheticCatalogIncompleteAcknowledged',
    ])
    expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toMatch(/amoxicillin|peanut|Medication|Food|allergyInformationSnapshotFingerprint|patientId/i)

    const healthHistoryLoadRetry = page.getByRole('button', { name: 'Try loading health-history information again' })
    await expect(healthHistoryLoadRetry).toBeVisible()
    await healthHistoryLoadRetry.click()
    expect(healthHistoryInformationGetCalls).toBe(2)

    const healthHistoryHeading = page.getByRole('heading', { name: 'Identify broad topics for later review' })
    await expect(healthHistoryHeading).toBeVisible()
    await expect(healthHistoryHeading.locator('..')).toContainText('selection is only a prompt for later review')
    await expect(healthHistoryHeading.locator('..').locator('input[type="text"]')).toHaveCount(0)
    await expect(healthHistoryHeading.locator('..').locator('textarea')).toHaveCount(0)
    await page.getByRole('checkbox', { name: /Ongoing health conditions.*local category/i }).check()
    await page.getByRole('checkbox', { name: /Behavioral health or substance use information.*local category/i }).check()
    await page.getByLabel(/additional or unlisted health-history topics/i).check()
    await page.getByLabel(/patient-reported health-history information may be incomplete/i).check()
    await page.getByLabel(/topic selection is not a diagnosis/i).check()
    await page.getByLabel(/no clinical or verification status, timing/i).check()
    await page.getByLabel(/detailed collection and clinician verification are required/i).check()
    const healthHistorySubmit = page.getByRole('button', { name: 'Record health-history topics' })
    await expect(healthHistorySubmit).toBeEnabled()
    await healthHistorySubmit.click()
    await expect(page.getByRole('alert')).toContainText('retry unchanged')
    await expect(page.getByRole('checkbox', { name: /Ongoing health conditions.*local category/i })).toBeChecked()
    await healthHistorySubmit.click()

    const healthHistoryRecordedHeading = page.getByRole('heading', { name: 'Health-history topics recorded' })
    await expect(healthHistoryRecordedHeading).toBeVisible()
    await expect(healthHistoryRecordedHeading.locator('..')).toContainText('Selected synthetic review topics2')
    await expect(healthHistoryRecordedHeading.locator('..')).toContainText('Ongoing health conditions: ConditionOrConcern')
    await expect(healthHistoryRecordedHeading.locator('..')).toContainText('Behavioral health or substance use information: SensitiveHistory')
    await expect(healthHistoryRecordedHeading.locator('..')).toContainText('Condition or problem createdNo')
    await expect(healthHistoryRecordedHeading.locator('..')).toContainText('FamilyMemberHistory or QuestionnaireResponse createdNo')
    await expect(healthHistoryRecordedHeading.locator('..')).toContainText('Risk modifier evaluatedNo')
    await expect(healthHistoryRecordedHeading.locator('..')).toContainText('Clinical triage changedNo')
    await expect(healthHistoryRecordedHeading.locator('..')).toContainText('Prescribing enabledNo')
    expect(healthHistoryInformationKeys).toHaveLength(2)
    expect(healthHistoryInformationKeys[0]).toBe(healthHistoryInformationKeys[1])
    expect(healthHistoryInformationBodies[0]).toEqual({
      expectedVersion: 20,
      healthHistoryInformationSnapshotFingerprint: 'd'.repeat(64),
      healthHistoryTopics: [
        { catalogKey: 'ongoing-health-conditions' },
        { catalogKey: 'behavioral-health-or-substance-use' },
      ],
      additionalOrUnlistedTopicsReported: true,
      patientReportedMayBeIncompleteAcknowledged: true,
      topicSelectionIsNotDiagnosisAcknowledged: true,
      noStatusOrTimingCapturedAcknowledged: true,
      clinicianVerificationRequiredAcknowledged: true,
    })
    expect(Object.keys(healthHistoryInformationBodies[0]).sort()).toEqual([
      'additionalOrUnlistedTopicsReported',
      'clinicianVerificationRequiredAcknowledged',
      'expectedVersion',
      'healthHistoryInformationSnapshotFingerprint',
      'healthHistoryTopics',
      'noStatusOrTimingCapturedAcknowledged',
      'patientReportedMayBeIncompleteAcknowledged',
      'topicSelectionIsNotDiagnosisAcknowledged',
    ])
    expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toMatch(/ongoing-health|behavioral-health|SensitiveHistory|healthHistoryInformationSnapshotFingerprint|patientId/i)

    const summaryLoadRetry = page.getByRole('button', { name: 'Retry summary load' })
    await expect(summaryLoadRetry).toBeVisible()
    await summaryLoadRetry.click()
    expect(clinicalInformationSummaryGetCalls).toBe(2)

    const summaryHeading = page.getByRole('heading', { name: 'Review your clinical-information summary' })
    await expect(summaryHeading).toBeVisible()
    await expect(summaryHeading.locator('..')).toContainText('MedicationsItems need detailed review; 2 selected')
    await expect(summaryHeading.locator('..')).toContainText('Other health historyItems need detailed review; 2 selected; additional or unlisted: Yes')
    await expect(summaryHeading.locator('..')).toContainText('If anything is wrong or missing, do not confirm this step')
    await expect(summaryHeading.locator('..').locator('input[type="text"]')).toHaveCount(0)
    await expect(summaryHeading.locator('..').locator('textarea')).toHaveCount(0)
    await page.getByLabel(/summary is based on patient-reported information and may be incomplete/i).check()
    await page.getByLabel(/none of these categories has been clinically verified or reconciled/i).check()
    await page.getByLabel(/does not complete clinical intake or establish telehealth eligibility/i).check()
    await page.getByLabel(/corrections or omissions require a separately authorized workflow/i).check()
    const summarySubmit = page.getByRole('button', { name: 'Confirm no-edit summary' })
    await expect(summarySubmit).toBeEnabled()
    await summarySubmit.click()
    await expect(page.getByRole('alert')).toContainText('retry unchanged')
    await expect(page.getByLabel(/summary is based on patient-reported information and may be incomplete/i)).toBeChecked()
    await summarySubmit.click()

    const summaryConfirmedHeading = page.getByRole('heading', { name: 'Clinical-information summary confirmed' })
    await expect(summaryConfirmedHeading).toBeVisible()
    await expect(summaryConfirmedHeading.locator('..')).toContainText('QuestionnaireResponse createdNo')
    await expect(summaryConfirmedHeading.locator('..')).toContainText('Medication, allergy, or history reconciledNo')
    await expect(summaryConfirmedHeading.locator('..')).toContainText('Confirmed negative establishedNo')
    await expect(summaryConfirmedHeading.locator('..')).toContainText('Clinical intake completedNo')
    await expect(summaryConfirmedHeading.locator('..')).toContainText('Practice accepted or queue enteredNo')
    expect(clinicalInformationSummaryKeys).toHaveLength(2)
    expect(clinicalInformationSummaryKeys[0]).toBe(clinicalInformationSummaryKeys[1])
    expect(clinicalInformationSummaryBodies[0]).toEqual({
      expectedVersion: 21,
      clinicalInformationSummarySnapshotFingerprint: 'e'.repeat(64),
      patientReportedMayBeIncompleteAcknowledged: true,
      notClinicallyVerifiedOrReconciledAcknowledged: true,
      noIntakeCompletionOrEligibilityAcknowledged: true,
      correctionRequiresSeparateWorkflowAcknowledged: true,
    })
    expect(Object.keys(clinicalInformationSummaryBodies[0]).sort()).toEqual([
      'clinicalInformationSummarySnapshotFingerprint',
      'correctionRequiresSeparateWorkflowAcknowledged',
      'expectedVersion',
      'noIntakeCompletionOrEligibilityAcknowledged',
      'notClinicallyVerifiedOrReconciledAcknowledged',
      'patientReportedMayBeIncompleteAcknowledged',
    ])
    expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toMatch(/clinicalInformationSummary|ItemsToReview|AdditionalClinicalInformation|patientId/i)

    const readinessLoadRetry = page.getByRole('button', { name: 'Retry readiness load' })
    await expect(readinessLoadRetry).toBeVisible()
    await readinessLoadRetry.click()
    expect(preRequestReadinessGetCalls).toBe(2)

    const readinessHeading = page.getByRole('heading', { name: 'Review pre-request readiness' })
    await expect(readinessHeading).toBeVisible()
    await expect(readinessHeading.locator('..')).toContainText('Registration detailsReceiptRecorded')
    await expect(readinessHeading.locator('..')).toContainText('Clinical informationPatientReportedSummaryConfirmed')
    await expect(readinessHeading.locator('..')).toContainText('Overall server routeAdditionalClinicalInformationRequired')
    await expect(readinessHeading.locator('..').locator('input[type="text"]')).toHaveCount(0)
    await expect(readinessHeading.locator('..').locator('textarea')).toHaveCount(0)
    await page.getByLabel(/reviewed the five coarse sections/i).check()
    await page.getByLabel(/identity, coverage, support, technology, reconciliation, consent, eligibility, and practice-review steps remain unresolved/i).check()
    await page.getByLabel(/does not create a telehealth request or queue entry/i).check()
    await page.getByLabel(/corrections require a separately authorized workflow/i).check()
    const readinessSubmit = page.getByRole('button', { name: 'Acknowledge readiness boundaries' })
    await expect(readinessSubmit).toBeEnabled()
    await readinessSubmit.click()
    await expect(page.getByRole('alert')).toContainText('retry unchanged')
    await expect(page.getByLabel(/reviewed the five coarse sections/i)).toBeChecked()
    await readinessSubmit.click()

    const readinessAcknowledgedHeading = page.getByRole('heading', { name: 'Pre-request readiness boundaries acknowledged' })
    await expect(readinessAcknowledgedHeading).toBeVisible()
    await expect(readinessAcknowledgedHeading.locator('..')).toContainText('Identity assurance establishedNo')
    await expect(readinessAcknowledgedHeading.locator('..')).toContainText('Coverage or rendering-clinician network guaranteedNo')
    await expect(readinessAcknowledgedHeading.locator('..')).toContainText('Interpreter or accommodation arrangedNo')
    await expect(readinessAcknowledgedHeading.locator('..')).toContainText('Technology readiness establishedNo')
    await expect(readinessAcknowledgedHeading.locator('..')).toContainText('Clinical information reconciled or intake completedNo')
    await expect(readinessAcknowledgedHeading.locator('..')).toContainText('Legal consent or practice acceptance establishedNo')
    await expect(readinessAcknowledgedHeading.locator('..')).toContainText('Staff or clinician review task createdNo')
    await expect(readinessAcknowledgedHeading.locator('..')).toContainText('Request, queue, appointment, or encounter createdNo')
    await expect(readinessAcknowledgedHeading.locator('..')).toContainText('No completion, eligibility, consent, practice acceptance, request, queue, appointment, encounter, care, prescribing, billing, claim, integration, or external action was created')
    expect(preRequestReadinessKeys).toHaveLength(2)
    expect(preRequestReadinessKeys[0]).toBe(preRequestReadinessKeys[1])
    expect(preRequestReadinessBodies[0]).toEqual({
      expectedVersion: 22,
      preRequestReadinessSnapshotFingerprint: '9'.repeat(64),
      priorSectionsReviewedAcknowledged: true,
      outstandingStepsRemainAcknowledged: true,
      noRequestOrQueueCreatedAcknowledged: true,
      correctionRequiresSeparateWorkflowAcknowledged: true,
    })
    expect(Object.keys(preRequestReadinessBodies[0]).sort()).toEqual([
      'correctionRequiresSeparateWorkflowAcknowledged',
      'expectedVersion',
      'noRequestOrQueueCreatedAcknowledged',
      'outstandingStepsRemainAcknowledged',
      'preRequestReadinessSnapshotFingerprint',
      'priorSectionsReviewedAcknowledged',
    ])
    expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toMatch(/preRequestReadiness|ReceiptRecorded|AdditionalClinicalInformation|requiredRoute|patientId/i)

    const practiceReviewLoadRetry = page.getByRole('button', { name: 'Retry practice review load' })
    await expect(practiceReviewLoadRetry).toBeVisible()
    await practiceReviewLoadRetry.click()
    expect(practiceReviewGetCalls).toBe(2)

    const practiceReviewHeading = page.getByRole('heading', { name: 'Submit for practice review' })
    await expect(practiceReviewHeading).toBeVisible()
    await expect(practiceReviewHeading.locator('..')).toContainText('Practice acceptanceNot established')
    await expect(practiceReviewHeading.locator('..')).toContainText('Telehealth requestNot created')
    await expect(practiceReviewHeading.locator('..')).toContainText('Patient or clinician queueNot entered')
    await expect(practiceReviewHeading.locator('..').locator('input[type="text"]')).toHaveCount(0)
    await expect(practiceReviewHeading.locator('..').locator('textarea')).toHaveCount(0)
    await page.getByLabel(/information remains patient reported/i).check()
    await page.getByLabel(/practice may request more information or decline/i).check()
    await page.getByLabel(/creates no telehealth request, doctor search, or patient or clinician queue entry/i).check()
    await page.getByLabel(/seek appropriate immediate care instead of waiting/i).check()
    const practiceReviewSubmit = page.getByRole('button', { name: 'Submit for practice review' })
    await expect(practiceReviewSubmit).toBeEnabled()
    await practiceReviewSubmit.click()
    await expect(page.getByRole('alert')).toContainText('retry unchanged')
    await expect(page.getByLabel(/information remains patient reported/i)).toBeChecked()
    await practiceReviewSubmit.click()

    const practiceReviewSubmittedHeading = page.getByRole('heading', { name: 'Submitted for practice review' })
    await expect(practiceReviewSubmittedHeading).toBeVisible()
    await expect(practiceReviewSubmittedHeading.locator('..')).toContainText('Review statusPending practice review')
    await expect(practiceReviewSubmittedHeading.locator('..')).toContainText('Practice staff review work item createdYes')
    await expect(practiceReviewSubmittedHeading.locator('..')).toContainText('Practice acceptedNo')
    await expect(practiceReviewSubmittedHeading.locator('..')).toContainText('Telehealth request createdNo')
    await expect(practiceReviewSubmittedHeading.locator('..')).toContainText('Patient or clinician queue enteredNo')
    await expect(practiceReviewSubmittedHeading.locator('..')).toContainText('No doctor search, queue position, response-time promise, acceptance, telehealth request, appointment, encounter, prescribing, billing, claim, integration, or care capability was created')
    expect(practiceReviewKeys).toHaveLength(2)
    expect(practiceReviewKeys[0]).toBe(practiceReviewKeys[1])
    expect(practiceReviewBodies[0]).toEqual({
      expectedVersion: 23,
      practiceReviewSnapshotFingerprint: '8'.repeat(64),
      patientReportedInformationAcknowledged: true,
      practiceMayRequestInformationOrDeclineAcknowledged: true,
      noTelehealthRequestOrCareQueueAcknowledged: true,
      worseningSymptomsRequireImmediateActionAcknowledged: true,
    })
    expect(Object.keys(practiceReviewBodies[0]).sort()).toEqual([
      'expectedVersion',
      'noTelehealthRequestOrCareQueueAcknowledged',
      'patientReportedInformationAcknowledged',
      'practiceMayRequestInformationOrDeclineAcknowledged',
      'practiceReviewSnapshotFingerprint',
      'worseningSymptomsRequireImmediateActionAcknowledged',
    ])
    expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toMatch(/practiceReview|PendingPracticeReview|practiceReviewCaseId|patientId/i)

    await expectNoSeriousAccessibilityViolations(page)
    await expectTelehealthReflow(page)
  })

  test('approved prospective applicant through identity proofing preserves emergency action, stable retry, minimization, reflow, and WCAG behavior', async ({ page }) => {
    await page.addInitScript((session) => {
      sessionStorage.setItem('avenchart-ui.telehealthProspectiveApplicant', JSON.stringify(session))
    }, { applicantId: prospectiveApplicant.applicantId, applicantAccessKey: 'e'.repeat(64) })
    let safetyCalls = 0
    let purposeCalls = 0
    let networkCalls = 0
    let memberCalls = 0
    let eligibilityCalls = 0
    let practiceNetworkDeterminationCalls = 0
    let identityProofingCalls = 0
    const safetyKeys: Array<string | undefined> = []
    const safetyBodies: Array<Record<string, unknown>> = []
    const purposeKeys: Array<string | undefined> = []
    const purposeBodies: Array<Record<string, unknown>> = []
    const networkKeys: Array<string | undefined> = []
    const networkBodies: Array<Record<string, unknown>> = []
    const memberKeys: Array<string | undefined> = []
    const memberBodies: Array<Record<string, unknown>> = []
    const eligibilityKeys: Array<string | undefined> = []
    const eligibilityBodies: Array<Record<string, unknown>> = []
    const practiceNetworkDeterminationKeys: Array<string | undefined> = []
    const practiceNetworkDeterminationBodies: Array<Record<string, unknown>> = []
    const identityProofingKeys: Array<string | undefined> = []
    const identityProofingBodies: Array<Record<string, unknown>> = []
    await page.route('**/api/telehealth/v1/applicants/**', async (route) => {
      const request = route.request()
      if (request.method() === 'POST' && new URL(request.url()).pathname.endsWith('/safety-triage')) {
        safetyCalls += 1
        safetyKeys.push(request.headers()['x-idempotency-key'])
        safetyBodies.push(request.postDataJSON() as Record<string, unknown>)
        if (safetyCalls === 1) {
          await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Synthetic safety service unavailable.' }) })
          return
        }
        await route.fulfill({ json: prospectiveSafetyPassed })
        return
      }
      if (request.method() === 'POST' && new URL(request.url()).pathname.endsWith('/visit-purpose')) {
        purposeCalls += 1
        purposeKeys.push(request.headers()['x-idempotency-key'])
        purposeBodies.push(request.postDataJSON() as Record<string, unknown>)
        if (purposeCalls === 1) {
          await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Synthetic visit-purpose service unavailable.' }) })
          return
        }
        await route.fulfill({ json: prospectiveVisitPurposeRecorded })
        return
      }
      if (request.method() === 'GET' && new URL(request.url()).pathname.endsWith('/practice-network-precheck/options')) {
        await route.fulfill({ json: prospectivePracticeNetworkOptions })
        return
      }
      if (request.method() === 'POST' && new URL(request.url()).pathname.endsWith('/practice-network-precheck')) {
        networkCalls += 1
        networkKeys.push(request.headers()['x-idempotency-key'])
        networkBodies.push(request.postDataJSON() as Record<string, unknown>)
        if (networkCalls === 1) {
          await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Synthetic practice-network service unavailable.' }) })
          return
        }
        await route.fulfill({ json: prospectivePracticeNetworkRecordedPositive })
        return
      }
      if (request.method() === 'POST' && new URL(request.url()).pathname.endsWith('/member-insurance-details')) {
        memberCalls += 1
        memberKeys.push(request.headers()['x-idempotency-key'])
        memberBodies.push(request.postDataJSON() as Record<string, unknown>)
        if (memberCalls === 1) {
          await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Protected synthetic receipt service unavailable.' }) })
          return
        }
        await route.fulfill({ json: prospectiveMemberInsuranceDetailsRecordedPositive })
        return
      }
      if (request.method() === 'POST' && new URL(request.url()).pathname.endsWith('/eligibility')) {
        eligibilityCalls += 1
        eligibilityKeys.push(request.headers()['x-idempotency-key'])
        eligibilityBodies.push(request.postDataJSON() as Record<string, unknown>)
        if (eligibilityCalls === 1) {
          await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Synthetic eligibility service unavailable.' }) })
          return
        }
        await route.fulfill({ json: prospectiveEligibilityRecordedPositive })
        return
      }
      if (request.method() === 'POST' && new URL(request.url()).pathname.endsWith('/practice-network-determination')) {
        practiceNetworkDeterminationCalls += 1
        practiceNetworkDeterminationKeys.push(request.headers()['x-idempotency-key'])
        practiceNetworkDeterminationBodies.push(request.postDataJSON() as Record<string, unknown>)
        if (practiceNetworkDeterminationCalls === 1) {
          await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Synthetic provider-directory fixture unavailable.' }) })
          return
        }
        await route.fulfill({ json: prospectivePracticeNetworkDeterminationRecordedPositive })
        return
      }
      if (request.method() === 'POST' && new URL(request.url()).pathname.endsWith('/identity-proofing')) {
        identityProofingCalls += 1
        identityProofingKeys.push(request.headers()['x-idempotency-key'])
        identityProofingBodies.push(request.postDataJSON() as Record<string, unknown>)
        if (identityProofingCalls === 1) {
          await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Synthetic proofing process interrupted.' }) })
          return
        }
        await route.fulfill({ json: prospectiveIdentityProofingRecorded })
        return
      }
      await route.fulfill({ json: safetyApprovedApplicant })
    })

    await page.goto('/telehealth/new')
    await expect(page.getByRole('link', { name: 'Call 911' })).toHaveCount(2)
    await expect(page.getByText(/one synthetic universal safety screen, not diagnosis or complete telehealth eligibility/i)).toBeVisible()
    await page.getByRole('combobox', { name: /Current physical location/ }).selectOption('CA')
    await page.getByLabel(/current physical location, not merely a residence address/i).check()
    const emergencyGroup = page.getByRole('group', { name: 'Could any current symptom be an emergency?' })
    await emergencyGroup.getByLabel('Yes').check()
    await expect(page.getByRole('alert')).toContainText('Call 911 now')
    await emergencyGroup.getByLabel('No').check()
    await page.getByRole('group', { name: 'Are symptoms severe or getting worse quickly?' }).getByLabel('No').check()
    await page.getByRole('group', { name: 'Does this seem to require a hands-on examination or procedure?' }).getByLabel('No').check()
    await page.getByRole('group', { name: 'Are you unsure about any answer above?' }).getByLabel('No').check()
    await page.getByLabel(/location and every safety answer are fictional synthetic demonstration data/i).check()

    const submit = page.getByRole('button', { name: 'Evaluate universal safety screen' })
    await submit.click()
    await expect(page.getByRole('alert')).toContainText('Synthetic safety service unavailable.')
    await expect(page.getByRole('alert')).toBeFocused()
    await expect(emergencyGroup.getByLabel('No')).toBeChecked()
    await submit.click()

    const result = page.getByRole('status')
    await expect(result).toBeFocused()
    await expect(result).toContainText('Universal safety screen recorded')
    await expect(result).toContainText('No clinician reviewed these answers')
    expect(safetyCalls).toBe(2)
    expect(safetyKeys[0]).toBe(safetyKeys[1])
    expect(safetyKeys[0]).toMatch(/^[0-9a-f-]{36}$/)
    expect(safetyBodies).toEqual([
      {
        expectedVersion: 3,
        currentLocationStateCode: 'CA',
        currentLocationConfirmed: true,
        hasEmergencyWarning: false,
        severeOrWorsening: false,
        requiresHandsOnExam: false,
        unsure: false,
        syntheticDataConfirmed: true,
      },
      {
        expectedVersion: 3,
        currentLocationStateCode: 'CA',
        currentLocationConfirmed: true,
        hasEmergencyWarning: false,
        severeOrWorsening: false,
        requiresHandsOnExam: false,
        unsure: false,
        syntheticDataConfirmed: true,
      },
    ])
    const storedSession = await page.evaluate(() => sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant'))
    expect(storedSession).toContain(prospectiveApplicant.applicantId)
    expect(storedSession).not.toMatch(/hasEmergencyWarning|severeOrWorsening|requiresHandsOnExam|unsure|currentLocationStateCode/)

    const purposeGroup = page.getByRole('group', { name: 'What is the synthetic visit about?' })
    await expect(purposeGroup).toContainText(/not a diagnosis, complaint-specific clinical triage/i)
    await expect(page.getByRole('link', { name: 'Call 911' })).toHaveCount(2)
    await purposeGroup.getByLabel('Sleep difficulty').check()
    await page.getByLabel(/visit-purpose selection is fictional synthetic demonstration data/i).check()
    const purposeSubmit = page.getByRole('button', { name: 'Record synthetic visit purpose' })
    await purposeSubmit.click()
    await expect(page.getByRole('alert')).toContainText('Synthetic visit-purpose service unavailable.')
    await expect(page.getByRole('alert')).toBeFocused()
    await expect(purposeGroup.getByLabel('Sleep difficulty')).toBeChecked()
    await purposeSubmit.click()

    const purposeResult = page.getByRole('status').filter({ hasText: 'Synthetic visit purpose recorded' })
    await expect(purposeResult).toBeFocused()
    await expect(purposeResult).toContainText('Synthetic visit purpose recorded')
    await expect(purposeResult).toContainText('No complaint-specific clinical protocol ran')
    expect(purposeCalls).toBe(2)
    expect(purposeKeys[0]).toBe(purposeKeys[1])
    expect(purposeKeys[0]).toMatch(/^[0-9a-f-]{36}$/)
    expect(purposeBodies).toEqual([
      { expectedVersion: 4, purposeCategory: 'sleep', syntheticDataConfirmed: true },
      { expectedVersion: 4, purposeCategory: 'sleep', syntheticDataConfirmed: true },
    ])
    const finalStoredSession = await page.evaluate(() => sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant'))
    expect(finalStoredSession).not.toMatch(/migraine|sleep|purposeCategory/)

    const planGroup = page.getByRole('group', { name: 'Choose one fictional plan' })
    await expect(planGroup).toContainText(/do not check you, your benefits, an eventual physician/i)
    await expect(page.getByText(/No result on this screen is an insurance eligibility/i)).toBeVisible()
    await expect(page.getByRole('link', { name: 'Call 911' })).toHaveCount(2)
    await planGroup.getByLabel(/Harbor Mutual — High Deductible/i).check()
    await page.getByLabel(/understand this is not an insurance verification/i).check()
    const networkSubmit = page.getByRole('button', { name: 'Record synthetic practice precheck' })
    await networkSubmit.click()
    await expect(page.getByRole('alert')).toContainText('Synthetic practice-network service unavailable.')
    await expect(page.getByRole('alert')).toBeFocused()
    await expect(planGroup.getByLabel(/Harbor Mutual — High Deductible/i)).toBeChecked()
    await networkSubmit.click()

    const networkResult = page.getByRole('status')
    await expect(networkResult).toBeFocused()
    await expect(networkResult).toContainText('Synthetic practice-level precheck recorded')
    await expect(networkResult).toContainText('No member eligibility, benefits, rendering-physician participation')
    expect(networkCalls).toBe(2)
    expect(networkKeys[0]).toBe(networkKeys[1])
    expect(networkKeys[0]).toMatch(/^[0-9a-f-]{36}$/)
    expect(networkBodies).toEqual([
      { expectedVersion: 5, planKey: 'harbor-mutual-hd', syntheticDataConfirmed: true },
      { expectedVersion: 5, planKey: 'harbor-mutual-hd', syntheticDataConfirmed: true },
    ])
    const networkStoredSession = await page.evaluate(() => sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant'))
    expect(networkStoredSession).not.toMatch(/harbor|blue-valley|pine-state|planKey|network/i)

    await expect(page.getByText(/Raw normalized member, group, and subscriber values are protected/i)).toBeVisible()
    await expect(page.getByRole('link', { name: 'Call 911' })).toHaveCount(2)
    await page.getByLabel('Fictional member ID').fill('SYN-HM-1001')
    await page.getByLabel(/Fictional group number/).fill('SYN-GROUP-01')
    await page.getByLabel('Subscriber relationship').selectOption('Spouse')
    const subscriberGroup = page.getByRole('group', { name: 'Fictional subscriber identity' })
    await expect(subscriberGroup).toBeVisible()
    await subscriberGroup.getByLabel('Subscriber first name').fill('Morgan')
    await subscriberGroup.getByLabel('Subscriber last name').fill('Synthetic')
    await subscriberGroup.getByLabel('Subscriber date of birth').fill('1980-02-03')
    await page.getByLabel(/reviewed this minimum fictional member/i).check()
    await page.getByLabel(/every insurance and subscriber value is fictional/i).check()
    const memberSubmit = page.getByRole('button', { name: 'Record protected synthetic receipt' })
    await memberSubmit.click()
    await expect(page.getByRole('alert')).toContainText('Protected synthetic receipt service unavailable.')
    await expect(page.getByRole('alert')).toBeFocused()
    await expect(page.getByLabel('Fictional member ID')).toHaveValue('SYN-HM-1001')
    await expect(subscriberGroup.getByLabel('Subscriber first name')).toHaveValue('Morgan')
    await memberSubmit.click()

    const memberResult = page.getByRole('status').filter({ hasText: 'Protected synthetic member-details receipt recorded' })
    await expect(memberResult).toBeFocused()
    await expect(memberResult).toContainText('••••1001')
    await expect(memberResult).toContainText('••••UP01')
    await expect(memberResult).toContainText(/No raw values are returned/i)
    await expect(memberResult).not.toContainText('SYN-HM-1001')
    await expect(memberResult).not.toContainText('SYN-GROUP-01')
    await expect(memberResult).not.toContainText('Morgan')
    expect(memberCalls).toBe(2)
    expect(memberKeys[0]).toBe(memberKeys[1])
    expect(memberKeys[0]).toMatch(/^[0-9a-f-]{36}$/)
    expect(memberBodies).toEqual([
      {
        expectedVersion: 6,
        memberId: 'SYN-HM-1001',
        groupNumber: 'SYN-GROUP-01',
        subscriberRelationship: 'Spouse',
        subscriberFirstName: 'Morgan',
        subscriberLastName: 'Synthetic',
        subscriberDateOfBirth: '1980-02-03',
        detailsConfirmed: true,
        syntheticDataConfirmed: true,
      },
      {
        expectedVersion: 6,
        memberId: 'SYN-HM-1001',
        groupNumber: 'SYN-GROUP-01',
        subscriberRelationship: 'Spouse',
        subscriberFirstName: 'Morgan',
        subscriberLastName: 'Synthetic',
        subscriberDateOfBirth: '1980-02-03',
        detailsConfirmed: true,
        syntheticDataConfirmed: true,
      },
    ])
    const memberStoredSession = await page.evaluate(() => `${localStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}${sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}`)
    expect(memberStoredSession).not.toMatch(/SYN-HM-1001|SYN-GROUP-01|Morgan|subscriber/i)

    await expect(page.getByText(/will not create or store an X12 transaction/i)).toBeVisible()
    await expect(page.getByText(/active result is not exact practice-and-physician network confirmation/i)).toBeVisible()
    await expect(page.getByRole('link', { name: 'Call 911' })).toHaveCount(2)
    await page.getByLabel(/check is synthetic, NON_PRODUCTION/i).check()
    const eligibilitySubmit = page.getByRole('button', { name: 'Record synthetic eligibility result' })
    await eligibilitySubmit.click()
    await expect(page.getByRole('alert')).toContainText('Synthetic eligibility service unavailable.')
    await expect(page.getByRole('alert')).toBeFocused()
    await expect(page.getByLabel(/check is synthetic, NON_PRODUCTION/i)).toBeChecked()
    await eligibilitySubmit.click()

    const eligibilityResult = page.getByRole('status').filter({ hasText: 'Synthetic eligibility result recorded' })
    await expect(eligibilityResult).toBeFocused()
    await expect(eligibilityResult).toContainText('Active')
    await expect(eligibilityResult).toContainText('Reported')
    await expect(eligibilityResult).toContainText('PracticeNetworkConfirmedFixture')
    await expect(eligibilityResult).toContainText(/never a guarantee of coverage or payment/i)
    await expect(eligibilityResult).not.toContainText('SYN-HM-1001')
    expect(eligibilityCalls).toBe(2)
    expect(eligibilityKeys[0]).toBe(eligibilityKeys[1])
    expect(eligibilityKeys[0]).toMatch(/^[0-9a-f-]{36}$/)
    expect(eligibilityBodies).toEqual([
      { expectedVersion: 7, syntheticDataConfirmed: true },
      { expectedVersion: 7, syntheticDataConfirmed: true },
    ])
    const eligibilityStoredSession = await page.evaluate(() => `${localStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}${sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}`)
    expect(eligibilityStoredSession).not.toMatch(/eligibility|benefit|Active|Reported|trace|payer|network|SYN-HM/i)

    await expect(page.getByText(/No member information is sent to this adapter/i)).toBeVisible()
    await expect(page.getByText(/No FHIR resource or external directory call is created/i)).toBeVisible()
    await expect(page.getByRole('link', { name: 'Call 911' })).toHaveCount(2)
    await page.getByLabel(/practice-network check is synthetic, NON_PRODUCTION/i).check()
    const determinationSubmit = page.getByRole('button', { name: 'Record synthetic practice-network result' })
    await determinationSubmit.click()
    await expect(page.getByRole('alert')).toContainText('Synthetic provider-directory fixture unavailable.')
    await expect(page.getByRole('alert')).toBeFocused()
    await expect(page.getByLabel(/practice-network check is synthetic, NON_PRODUCTION/i)).toBeChecked()
    await determinationSubmit.click()

    const determinationResult = page.getByRole('status').filter({ hasText: 'Synthetic practice-network result recorded' })
    await expect(determinationResult).toBeFocused()
    await expect(determinationResult).toContainText('Active')
    await expect(determinationResult).toContainText('SimulatedAvailable')
    await expect(determinationResult).toContainText('InNetwork')
    await expect(determinationResult).toContainText('Included')
    await expect(determinationResult).toContainText('Accepting')
    await expect(determinationResult).toContainText(/Rendering-physician participation is still unchecked/i)
    await expect(determinationResult).not.toContainText('SYN-HM-1001')
    expect(practiceNetworkDeterminationCalls).toBe(2)
    expect(practiceNetworkDeterminationKeys[0]).toBe(practiceNetworkDeterminationKeys[1])
    expect(practiceNetworkDeterminationKeys[0]).toMatch(/^[0-9a-f-]{36}$/)
    expect(practiceNetworkDeterminationBodies).toEqual([
      { expectedVersion: 8, syntheticDataConfirmed: true },
      { expectedVersion: 8, syntheticDataConfirmed: true },
    ])
    const determinationStoredSession = await page.evaluate(() => `${localStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}${sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}`)
    expect(determinationStoredSession).not.toMatch(/eligibility|benefit|Active|SimulatedAvailable|trace|payer|network|directory|SYN-HM/i)

    await expect(page.getByText(/does not collect an ID document/i)).toBeVisible()
    await expect(page.getByRole('link', { name: 'Call 911' })).toHaveCount(2)
    await page.getByLabel(/acknowledge the synthetic privacy notice/i).check()
    await page.getByLabel(/NON_PRODUCTION process exercise/i).check()
    const identitySubmit = page.getByRole('button', { name: 'Record synthetic identity-proofing fixture' })
    await identitySubmit.click()
    await expect(page.getByRole('alert')).toContainText('Synthetic proofing process interrupted.')
    await expect(page.getByRole('alert')).toBeFocused()
    await expect(page.getByLabel(/acknowledge the synthetic privacy notice/i)).toBeChecked()
    await expect(page.getByLabel(/NON_PRODUCTION process exercise/i)).toBeChecked()
    await identitySubmit.click()

    const identityResult = page.getByRole('status').filter({ hasText: 'Synthetic identity-proofing process recorded' })
    await expect(identityResult).toBeFocused()
    await expect(identityResult).toContainText('FixtureReferenceAccepted')
    await expect(identityResult).toContainText('ValidatedFixture')
    await expect(identityResult).toContainText('VerifiedFixture')
    await expect(identityResult).toContainText('Identity assurance achievedNone')
    await expect(identityResult).toContainText(/No real identity was proofed/i)
    await expect(identityResult).not.toContainText('syn-proof-session')
    await expect(identityResult).not.toContainText('syn-evidence-')
    expect(identityProofingCalls).toBe(2)
    expect(identityProofingKeys[0]).toBe(identityProofingKeys[1])
    expect(identityProofingKeys[0]).toMatch(/^[0-9a-f-]{36}$/)
    expect(identityProofingBodies).toEqual([
      { expectedVersion: 9, privacyNoticeAcknowledged: true, syntheticDataConfirmed: true },
      { expectedVersion: 9, privacyNoticeAcknowledged: true, syntheticDataConfirmed: true },
    ])
    const identityStoredSession = await page.evaluate(() => `${localStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}${sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}`)
    expect(identityStoredSession).not.toMatch(/ValidatedFixture|VerifiedFixture|SyntheticProofing|proof-session|evidence-|identityProofing|trace/i)

    await expectNoSeriousAccessibilityViolations(page)
    await expectTelehealthReflow(page)
  })

  test('authenticated patient workspace supports keyboard, semantics, reflow, and automated WCAG checks', async ({ page }) => {
    await page.route('**/api/telehealth/v1/patient/requests', (route) => route.fulfill({ json: { requests: [draftRequest] } }))
    await signInPortal(page)
    await page.goto('/portal/telehealth')

    await expect(page.getByRole('heading', { name: 'Immediate telehealth request' })).toBeVisible()
    await expect(page.getByRole('note')).toContainText('Demonstration data only')
    const migraineButton = page.getByRole('button', { name: 'Start migraine demo' })
    await migraineButton.focus()
    await expect(migraineButton).toBeFocused()
    await page.keyboard.press('Tab')
    await expect(page.getByRole('button', { name: 'Start sleep demo' })).toBeFocused()

    await expectNoSeriousAccessibilityViolations(page)
    await expectTelehealthReflow(page)
  })

  test('patient readiness form exposes masked coverage, explicit affirmations, keyboard focus, and accessible reflow', async ({ page }) => {
    await page.route('**/api/telehealth/v1/patient/requests', (route) => route.fulfill({ json: { requests: [intakeRequest] } }))
    await page.route('**/api/telehealth/v1/patient/requests/*/readiness', (route) => route.fulfill({ json: readinessProjection }))
    await signInPortal(page)
    await page.goto('/portal/telehealth')

    await expect(page.getByRole('group', { name: 'Current patient details' })).toBeVisible()
    await expect(page.getByText('••••0012')).toBeVisible()
    await expect(page.getByText(/POL100012/)).toHaveCount(0)
    const summary = page.getByLabel('Synthetic complaint summary')
    await summary.focus()
    await expect(summary).toBeFocused()
    await page.keyboard.press('Tab')
    await expect(page.getByLabel('Synthetic symptom duration')).toBeFocused()
    await expect(page.getByRole('button', { name: 'Submit readiness for synthetic verification' })).toBeDisabled()

    await expectNoSeriousAccessibilityViolations(page)
    await expectTelehealthReflow(page)
  })

  test('patient queue status is approximate, resilient, keyboard operable, and accessible', async ({ page }) => {
    let failStatus = false
    let statusCalls = 0
    await page.route('**/api/telehealth/v1/patient/requests', (route) => route.fulfill({ json: { requests: [queuedPatientRequest] } }))
    await page.route('**/api/telehealth/v1/patient/requests/*/status', (route) => {
      statusCalls += 1
      return failStatus
        ? route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Synthetic status unavailable.' }) })
        : route.fulfill({ json: patientQueueStatus })
    })
    await signInPortal(page)
    await page.goto('/portal/telehealth')

    await expect(page.getByRole('heading', { name: "You're in line" })).toBeVisible()
    await expect(page.getByText(/Approximate requests ahead:\s*2/)).toBeVisible()
    await expect(page.getByText(/wait-time estimate is not available/i)).toBeVisible()
    await expect(page.getByText(/realtime delivery is not enabled/i)).toBeVisible()
    await expect(page.getByText(/Call 911 now for an emergency/i)).toBeVisible()
    const refreshStatus = page.getByRole('button', { name: 'Refresh status now' })
    await refreshStatus.focus()
    await expect(refreshStatus).toBeFocused()

    const callsBeforeVisibilityCycle = statusCalls
    await page.evaluate(() => {
      Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'hidden' })
      document.dispatchEvent(new Event('visibilitychange'))
    })
    await expect(page.getByText('Paused while this page is hidden')).toBeVisible()
    await page.evaluate(() => {
      Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'visible' })
      document.dispatchEvent(new Event('visibilitychange'))
    })
    await expect.poll(() => statusCalls).toBeGreaterThan(callsBeforeVisibilityCycle)
    await expect(page.getByText('Status connected')).toBeVisible()

    failStatus = true
    await refreshStatus.click()
    await expect(page.getByRole('status')).toContainText('last confirmed status remains shown')
    await expect(page.getByRole('heading', { name: "You're in line" })).toBeVisible()
    await expect(page.getByText('Connection interrupted')).toBeVisible()

    await expectNoSeriousAccessibilityViolations(page)
    await expectTelehealthReflow(page)
  })

  test('patient device preflight stops tracks, retries exactly, and enters a private simulator room accessibly', async ({ page }) => {
    await installPassingMediaPreflight(page)
    let connectionCalls = 0
    let connecting = false
    const commandKeys: Array<string | undefined> = []
    await page.route('**/api/telehealth/v1/patient/requests', (route) => route.fulfill({ json: { requests: [reservedPatientRequest] } }))
    await page.route('**/api/telehealth/v1/patient/requests/*/status', (route) => route.fulfill({
      json: connecting ? {
        ...reservedPatientStatus,
        requestStatus: 'Connecting', requestVersion: 10, phase: 'ConnectionRoom',
        headline: 'Your private connection room is ready',
      } : reservedPatientStatus,
    }))
    await page.route('**/api/telehealth/v1/patient/requests/*/connection-grants', async (route) => {
      connectionCalls += 1
      commandKeys.push(route.request().headers()['x-idempotency-key'])
      if (connectionCalls === 1) {
        await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Synthetic connection service unavailable.' }) })
        return
      }
      connecting = true
      await route.fulfill({ json: connectionGrant })
    })
    await signInPortal(page)
    await page.goto('/portal/telehealth')

    const check = page.getByRole('button', { name: 'Check camera and microphone' })
    await check.focus()
    await expect(check).toBeFocused()
    await page.keyboard.press('Enter')
    await expect(page.getByRole('status')).toContainText('Device check passed')
    await expect.poll(() => page.evaluate(() => (window as typeof window & { __stoppedTracks?: number }).__stoppedTracks ?? 0)).toBe(2)

    const enter = page.getByRole('button', { name: 'Enter synthetic waiting room' })
    await enter.click()
    await expect(page.getByRole('alert')).toContainText('Synthetic connection service unavailable.')
    await enter.click()
    await expect(page.getByRole('heading', { name: 'Waiting room ready' })).toBeVisible()
    expect(commandKeys).toHaveLength(2)
    expect(commandKeys[0]).toBe(commandKeys[1])
    await expect(page.getByText(connectionGrant.joinCredential)).toHaveCount(0)
    expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toContain(connectionGrant.joinCredential)
    await expect(page.getByText(/does not start a consultation/i)).toBeVisible()

    await expectNoSeriousAccessibilityViolations(page)
    await expectTelehealthReflow(page)
  })

  test('patient wrap-up lifecycle is terminal, privacy-bounded, and accessible', async ({ page }) => {
    let statusCalls = 0
    await page.route('**/api/telehealth/v1/patient/requests', (route) => route.fulfill({ json: { requests: [wrapUpPatientRequest] } }))
    await page.route('**/api/telehealth/v1/patient/requests/*/status', (route) => {
      statusCalls += 1
      return route.fulfill({ status: 500, json: { detail: 'Terminal lifecycle must not poll.' } })
    })
    await signInPortal(page)
    await page.goto('/portal/telehealth')

    await expect(page.getByRole('heading', { name: 'Your physician is finishing the synthetic visit record' })).toBeVisible()
    await expect(page.getByText(/This visit is not complete.*after-visit summary.*prescription.*claim/i)).toBeVisible()
    await expect(page.getByText(/Call 911 now for an emergency/i)).toBeVisible()
    await expect(page.getByText(/gold-provider-01|physicianStaffId|encounterId|joinCredential/i)).toHaveCount(0)
    await page.waitForTimeout(100)
    expect(statusCalls).toBe(0)

    await expectNoSeriousAccessibilityViolations(page)
    await expectTelehealthReflow(page)
  })

  test('administrator workspace supports keyboard, semantics, reflow, and automated WCAG checks', async ({ page }) => {
    await page.route('**/api/telehealth/v1/admin/operational-review', (route) => route.fulfill({ json: { requests: [queueRequest] } }))
    let practiceReviewInboxCalls = 0
    let practiceReviewInboxMaySucceed = false
    let practiceReviewClaimed = false
    let practiceReviewPacketMaySucceed = false
    let practiceReviewAuthorizationMaySucceed = false
    let practiceReviewAuthorized = false
    const practiceReviewInboxRequests: Array<{ method: string, body: string | null, idempotency: string | undefined }> = []
    const practiceReviewClaimRequests: Array<{ body: Record<string, unknown>, idempotency: string | undefined }> = []
    const practiceReviewPacketRequests: Array<{ method: string, body: string | null, idempotency: string | undefined }> = []
    const practiceReviewAuthorizationRequests: Array<{ body: Record<string, unknown>, idempotency: string | undefined }> = []
    await page.route('**/api/telehealth/v1/admin/applicant-practice-review', (route) => {
      practiceReviewInboxCalls += 1
      practiceReviewInboxRequests.push({
        method: route.request().method(),
        body: route.request().postData(),
        idempotency: route.request().headers()['x-idempotency-key'],
      })
      if (!practiceReviewInboxMaySucceed) {
        return route.fulfill({ status: 503, json: { detail: 'Practice review unavailable' } })
      }
      return route.fulfill({ json: {
        policyKey: 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_INBOX',
        policyVersion: 1,
        practiceDisplayName: 'AvenChart Synthetic Practice',
        serverTime: '2026-08-28T14:00:00Z',
        limitations: ['No review action, assignment, priority, or response-time promise is available.'],
        items: practiceReviewAuthorized ? [] : [{
          practiceReviewCaseId: '39000000-0000-4000-8000-000000000039',
          applicantVersion: 24,
          applicantStatus: 'SyntheticPracticeReviewSubmitted',
          reviewStatus: 'PendingPracticeReview',
          legalFirstName: 'Avery',
          legalLastName: 'Pending',
          dateOfBirth: '1990-03-14',
          maskedEmail: 'a•••@example.test',
          maskedPhone: '(***) ***-0199',
          residenceStateCode: 'GA',
          postalCode: '30303',
          purposeCategory: 'migraine',
          purposeDisplayLabel: 'Headache or known migraine pattern',
          safetyOutcome: 'TelehealthEligible',
          reviewRoute: 'AdditionalClinicalInformationRequired',
          sections: [
            { sectionKey: 'Registration', receiptState: 'ReceiptRecorded', outstandingRoute: 'IdentityAndIntakeCompletionStillRequired' },
            { sectionKey: 'Insurance', receiptState: 'ReceiptRecorded', outstandingRoute: 'CoverageAndRenderingClinicianVerificationStillRequired' },
            { sectionKey: 'CommunicationAccess', receiptState: 'PreferencesRecorded', outstandingRoute: 'CommunicationReconfirmationRequired' },
            { sectionKey: 'DevicePreparation', receiptState: 'ClientReportedPreparationRecorded', outstandingRoute: 'PreConsultationTechnologyRecheckRequired' },
            { sectionKey: 'ClinicalInformation', receiptState: 'PatientReportedSummaryConfirmed', outstandingRoute: 'AdditionalClinicalInformationCollectionRequired' },
          ],
          submittedAt: '2026-08-28T13:59:00Z',
          staffReviewWorkItemExists: true,
          staffActionTaken: practiceReviewClaimed,
          assigned: practiceReviewClaimed,
          assignedToCurrentUser: practiceReviewClaimed,
          assignmentExpiresAt: practiceReviewClaimed ? '2026-08-28T14:02:00Z' : null,
          priorityAssigned: false,
          practiceAccepted: false,
          practiceDeclined: false,
          patientContacted: false,
          clinicianReviewCreated: false,
          telehealthRequestCreated: false,
          patientCareQueueEntered: false,
          clinicianQueueEntered: false,
          appointmentCreated: false,
          encounterCreated: false,
          careAuthorized: false,
          prescribingEnabled: false,
          billingEnabled: false,
          claimCreated: false,
          integrationEnabled: false,
          externalCallPerformed: false,
        }],
      } })
    })
    await page.route('**/api/telehealth/v1/admin/applicant-practice-review/39000000-0000-4000-8000-000000000039/authorization', async (route) => {
      practiceReviewAuthorizationRequests.push({
        body: route.request().postDataJSON() as Record<string, unknown>,
        idempotency: route.request().headers()['x-idempotency-key'],
      })
      if (!practiceReviewAuthorizationMaySucceed) {
        return route.fulfill({ status: 503, json: { detail: 'Authorization result unknown; retry the unchanged command.' } })
      }
      practiceReviewAuthorized = true
      return route.fulfill({ json: {
        practiceReviewCaseId: '39000000-0000-4000-8000-000000000039',
        applicantVersion: 25,
        applicantStatus: 'SyntheticPracticeReviewAuthorized',
        decision: 'AuthorizedForSyntheticRequestCreation',
        rationaleCode: 'OperationalPrerequisitesReviewed',
        policyKey: 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_AUTHORIZATION',
        policyVersion: 1,
        evidenceType: 'CURRENT_CLAIMANT_MINIMIZED_PACKET_REVIEW_ONLY',
        decidedAt: '2026-08-28T14:00:30Z', requestCreationAuthorized: true,
        practiceAccepted: false, patientContacted: false, clinicianReviewCreated: false,
        telehealthRequestCreated: false, patientCareQueueEntered: false,
        clinicianQueueEntered: false, appointmentCreated: false, encounterCreated: false,
        consentCreated: false, careAuthorized: false, prescribingEnabled: false,
        billingEnabled: false, claimCreated: false, integrationEnabled: false,
        externalCallPerformed: false,
        limitations: ['No request, queue, appointment, encounter, consent, or care action was created.'],
      } })
    })
    await page.route('**/api/telehealth/v1/admin/applicant-practice-review/*/claim', async (route) => {
      practiceReviewClaimRequests.push({
        body: route.request().postDataJSON() as Record<string, unknown>,
        idempotency: route.request().headers()['x-idempotency-key'],
      })
      practiceReviewClaimed = true
      await route.fulfill({ json: {
        claimId: '40000000-0000-4000-8000-000000000040',
        practiceReviewCaseId: '39000000-0000-4000-8000-000000000039',
        applicantVersion: 24,
        policyKey: 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_CLAIM',
        policyVersion: 1,
        evidenceType: 'PENDING_PRACTICE_REVIEW_SHORT_LEASE_RECEIPT',
        assignedAt: '2026-08-28T14:00:00Z',
        assignmentExpiresAt: '2026-08-28T14:02:00Z',
        assigned: true,
        assignedToCurrentUser: true,
        staffReviewWorkItemExists: true,
        staffActionTaken: true,
        priorityAssigned: false,
        practiceAccepted: false,
        practiceDeclined: false,
        patientContacted: false,
        clinicianReviewCreated: false,
        telehealthRequestCreated: false,
        patientCareQueueEntered: false,
        clinicianQueueEntered: false,
        appointmentCreated: false,
        encounterCreated: false,
        careAuthorized: false,
        prescribingEnabled: false,
        billingEnabled: false,
        claimCreated: false,
        integrationEnabled: false,
        externalCallPerformed: false,
        limitations: ['This claim expires after 120 seconds and prevents duplicate staff work only.'],
      } })
    })
    await page.route('**/api/telehealth/v1/admin/applicant-practice-review/39000000-0000-4000-8000-000000000039', async (route) => {
      practiceReviewPacketRequests.push({
        method: route.request().method(),
        body: route.request().postData(),
        idempotency: route.request().headers()['x-idempotency-key'],
      })
      if (!practiceReviewPacketMaySucceed) {
        return route.fulfill({ status: 503, json: { detail: 'The short claim may have expired. Reload before retrying.' } })
      }
      return route.fulfill({ json: {
        practiceReviewCaseId: '39000000-0000-4000-8000-000000000039',
        applicantVersion: 24,
        applicantStatus: 'SyntheticPracticeReviewSubmitted',
        reviewStatus: 'PendingPracticeReview',
        policyKey: 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_PACKET',
        policyVersion: 1,
        practiceDisplayName: 'AvenChart Synthetic Practice',
        serverTime: '2026-08-28T14:00:15Z',
        assignmentExpiresAt: '2026-08-28T14:02:00Z',
        legalFirstName: 'Avery', legalLastName: 'Pending', dateOfBirth: '1990-03-14',
        maskedEmail: 'a•••@example.test', maskedPhone: '(***) ***-0199',
        residenceStateCode: 'GA', postalCode: '30303', purposeCategory: 'migraine',
        purposeDisplayLabel: 'Headache or known migraine pattern', safetyOutcome: 'TelehealthEligible',
        reviewRoute: 'AdditionalClinicalInformationRequired', submittedAt: '2026-08-28T13:59:00Z',
        sections: [],
        registration: { receiptRecorded: true, confirmedAt: '2026-08-28T13:30:00Z', identityAssuranceEstablished: false, patientRecordChanged: false },
        insurance: {
          payerDisplayName: 'Harbor Mutual', productDisplayName: 'High Deductible',
          memberIdMask: '••••A123', groupNumberMask: '••••B456', subscriberRelationship: 'Self', coveragePriority: 'Primary',
          eligibilityBusinessOutcome: 'EligibleBenefitsReported', eligibilityCheckedAt: '2026-08-28T13:31:00Z', eligibilityExpiresAt: '2026-08-28T15:00:00Z', eligibilityEvidenceCurrent: true,
          practiceNetworkBusinessOutcome: 'PracticeInNetworkAcceptingNewPatients', practiceNetworkCheckedAt: '2026-08-28T13:32:00Z', practiceNetworkExpiresAt: '2026-08-28T15:00:00Z', practiceNetworkEvidenceCurrent: true,
          renderingPhysicianNetworkChecked: false, confirmedAt: '2026-08-28T13:33:00Z', coverageVerified: false, exactNetworkConfirmed: false, canonicalCoverageCreated: false,
        },
        communicationAccess: {
          preferredSpokenLanguage: 'English', interpreterRequested: false, accessibilitySupportRequested: true,
          safePrivateCommunicationConfirmed: true, recordedAt: '2026-08-28T13:34:00Z',
          interpreterAssigned: false, accessibilityAccommodationArranged: false, communicationArrangementCompleted: false,
        },
        devicePreparation: {
          browserSupported: true, cameraAvailable: true, microphoneAvailable: true, speakerAvailable: true,
          networkQuality: 'Good', recordedAt: '2026-08-28T13:35:00Z', technologyReady: false,
          waitingRoomCreated: false, mediaSessionCreated: false,
        },
        clinicalInformationSummaryRoute: 'AdditionalClinicalInformationCollectionRequired',
        clinicalInformationSummaryConfirmedAt: '2026-08-28T13:55:00Z',
        staffReviewWorkItemExists: true, staffActionTaken: true, assigned: true, assignedToCurrentUser: true,
        priorityAssigned: false, practiceAccepted: false, practiceDeclined: false, patientContacted: false,
        clinicianReviewCreated: false, telehealthRequestCreated: false, patientCareQueueEntered: false,
        clinicianQueueEntered: false, appointmentCreated: false, encounterCreated: false, careAuthorized: false,
        prescribingEnabled: false, billingEnabled: false, claimCreated: false, integrationEnabled: false,
        externalCallPerformed: false,
        limitations: [
          'Synthetic operational evidence only; this packet is not a patient chart, clinical review, coverage guarantee, or rendering-clinician network determination.',
          'The short review claim is not extended by reading this packet and may expire while the packet is open.',
        ],
      } })
    })
    let identityDecisionRecorded = false
    const identityBodies: Array<Record<string, unknown>> = []
    let promotionDecisionRecorded = false
    const promotionBodies: Array<Record<string, unknown>> = []
    let syntheticPromotionRecorded = false
    const syntheticPromotionBodies: Array<Record<string, unknown>> = []
    await page.route('**/api/telehealth/v1/admin/applicant-identity-review', (route) => route.fulfill({ json: {
      practiceDisplayName: 'AvenChart Synthetic Practice',
      serverTime: '2026-08-27T04:29:00Z',
      applicants: identityDecisionRecorded ? [] : [identityReviewApplicant],
      limitations: ['Contact control is not identity proofing.', 'The applicant remains prospective.'],
    } }))
    await page.route('**/api/telehealth/v1/admin/applicants/*/identity-review-decision', async (route) => {
      identityBodies.push(route.request().postDataJSON() as Record<string, unknown>)
      identityDecisionRecorded = true
      await route.fulfill({ json: {
        decisionId: '22000000-0000-4000-8000-000000000002',
        applicantId: identityReviewApplicant.applicantId,
        applicantVersion: 3,
        applicantStatus: 'IdentityReviewApproved',
        decision: 'ApprovedForProspectiveIntake',
        reason: identityBodies[0].reason,
        policyKey: 'SYNTHETIC_STAFF_IDENTITY_REVIEW',
        policyVersion: 1,
        evidenceType: 'CONTACT_CONTROL_AND_DUPLICATE_DISPOSITION_ONLY',
        decidedAt: '2026-08-27T04:30:00Z',
        identityProofed: false,
        canonicalPatientCreated: false,
        chartLinked: false,
        prospectiveIntakeCompleted: false,
        requestCreated: false,
        queueEnabled: false,
        limitations: ['The applicant remains prospective.'],
      } })
    })
    await page.route('**/api/telehealth/v1/admin/applicant-promotion-authorization', (route) => route.fulfill({ json: {
      practiceDisplayName: 'AvenChart Synthetic Practice',
      serverTime: '2026-08-27T04:29:00Z',
      applicants: promotionDecisionRecorded ? [] : [promotionAuthorizationApplicant],
      limitations: ['Assurance remains None.', 'No patient is created.'],
    } }))
    await page.route('**/api/telehealth/v1/admin/applicants/*/promotion-authorization-decision', async (route) => {
      promotionBodies.push(route.request().postDataJSON() as Record<string, unknown>)
      promotionDecisionRecorded = true
      await route.fulfill({ json: {
        decisionId: '24000000-0000-4000-8000-000000000002',
        applicantId: promotionAuthorizationApplicant.applicantId,
        applicantVersion: 11,
        applicantStatus: 'SyntheticPromotionAuthorized',
        decision: 'AuthorizedForSyntheticPromotion',
        reason: promotionBodies[0].reason,
        policyKey: 'SYNTHETIC_PROSPECTIVE_PROMOTION_AUTHORIZATION',
        policyVersion: 1,
        evidenceType: 'COMPLETE_SYNTHETIC_INTAKE_AND_PROCESS_STATUS_ONLY',
        decidedAt: '2026-08-27T04:31:00Z',
        noneAssuranceAcknowledged: true,
        realIdentityProofed: false,
        canonicalPatientCreated: false,
        chartLinked: false,
        portalAccountCreated: false,
        prospectiveIntakeCompleted: false,
        consentCreated: false,
        practiceAccepted: false,
        requestCreated: false,
        queueEnabled: false,
        limitations: ['No patient is created.'],
      } })
    })
    await page.route('**/api/telehealth/v1/admin/applicant-synthetic-promotion', (route) => route.fulfill({ json: {
      practiceDisplayName: 'AvenChart Synthetic Practice',
      serverTime: '2026-08-27T04:31:00Z',
      applicants: syntheticPromotionRecorded ? [] : [syntheticPromotionApplicant],
      limitations: ['A current duplicate recheck is mandatory.', 'No portal or care capability is created.'],
    } }))
    await page.route('**/api/telehealth/v1/admin/applicants/*/synthetic-promotion', async (route) => {
      syntheticPromotionBodies.push(route.request().postDataJSON() as Record<string, unknown>)
      syntheticPromotionRecorded = true
      await route.fulfill({ json: {
        promotionId: '26000000-0000-4000-8000-000000000002',
        applicantId: syntheticPromotionApplicant.applicantId,
        applicantVersion: 12,
        applicantStatus: 'SyntheticPatientPromoted',
        outcome: 'SyntheticPatientCreated',
        possibleMatchDetected: false,
        canonicalPatientCreated: true,
        policyKey: 'SYNTHETIC_PROSPECTIVE_PATIENT_PROMOTION',
        policyVersion: 1,
        evidenceType: 'AUTHORIZED_SYNTHETIC_APPLICANT_AND_CURRENT_DUPLICATE_RECHECK',
        executedAt: '2026-08-27T04:32:00Z',
        portalAccountCreated: false,
        prospectiveIntakeCompleted: false,
        consentCreated: false,
        practiceAccepted: false,
        insuranceCreated: false,
        requestCreated: false,
        queueEnabled: false,
        careEnabled: false,
        limitations: ['No portal or care capability is created.'],
      } })
    })
    await signInClinician(page, process.env.MODERN_UI_STAFF_USERNAME ?? 'admin')
    await page.goto('/clinician/telehealth/admin')

    await expect(page.getByRole('heading', { name: 'Telehealth administration' })).toBeVisible()
    const practiceReviewSection = page.getByRole('heading', { name: 'Pending practice review' }).locator('..')
    await expect(practiceReviewSection.getByRole('alert')).toContainText('Practice review unavailable')
    practiceReviewInboxMaySucceed = true
    await practiceReviewSection.getByRole('button', { name: 'Reload practice-review inbox' }).click()
    await expect(practiceReviewSection.getByText('Avery Pending')).toBeVisible()
    await expect(practiceReviewSection.getByText('Headache or known migraine pattern · universal safety screen passed')).toBeVisible()
    await expect(practiceReviewSection.getByText(/no priority, accept, decline, contact, request, queue, appointment/i)).toBeVisible()
    const claimReview = practiceReviewSection.getByRole('button', { name: 'Claim for review' })
    await expect(claimReview).toBeDisabled()
    await practiceReviewSection.getByText('Coarse submitted sections').click()
    await expect(practiceReviewSection.getByText(/Registration.*ReceiptRecorded/)).toBeVisible()
    await expect(practiceReviewSection.getByText(/Insurance.*ReceiptRecorded/)).toBeVisible()
    await expect(practiceReviewSection.getByText(/CommunicationAccess.*PreferencesRecorded/)).toBeVisible()
    await expect(practiceReviewSection.getByText(/DevicePreparation.*ClientReportedPreparationRecorded/)).toBeVisible()
    await expect(practiceReviewSection.getByText(/ClinicalInformation.*PatientReportedSummaryConfirmed/)).toBeVisible()
    await expect(practiceReviewSection).not.toContainText('39000000-0000-4000-8000-000000000039')
    expect(practiceReviewInboxCalls).toBeGreaterThanOrEqual(2)
    expect(practiceReviewInboxRequests.every((request) =>
      request.method === 'GET' && request.body === null && request.idempotency === undefined)).toBe(true)
    await page.getByLabel(/this claim is not an accept, decline, or clinical decision/i).check()
    await page.getByLabel(/this claim does not contact the patient/i).check()
    await page.getByLabel(/this claim creates no telehealth request or care queue/i).check()
    await expect(claimReview).toBeEnabled()
    await claimReview.click()
    await expect(practiceReviewSection.getByText(/You hold this short review claim/i)).toBeVisible()
    expect(practiceReviewClaimRequests).toHaveLength(1)
    expect(practiceReviewClaimRequests[0].body).toEqual({
      expectedApplicantVersion: 24,
      inboxPolicyVersion: 1,
      noDecisionAcknowledged: true,
      noPatientContactAcknowledged: true,
      noRequestOrCareQueueAcknowledged: true,
    })
    expect(practiceReviewClaimRequests[0].idempotency).toBeTruthy()
    await expect(practiceReviewSection.getByText(/gold-frontdesk|gold-provider|admin|staffId|actorId/i)).toHaveCount(0)
    const openPacket = practiceReviewSection.getByRole('button', { name: 'Open operational review packet' })
    await openPacket.click()
    await expect(practiceReviewSection.getByRole('alert')).toContainText('short claim may have expired')
    practiceReviewPacketMaySucceed = true
    await practiceReviewSection.getByRole('button', { name: 'Retry packet' }).click()
    const packetSection = practiceReviewSection.getByRole('heading', { name: 'Claimant-only operational review packet' }).locator('xpath=ancestor::section[1]')
    await expect(packetSection).toContainText('Harbor Mutual · High Deductible')
    await expect(packetSection).toContainText('rendering physician not checked')
    await expect(packetSection).toContainText('technology readiness not established')
    await expect(packetSection).toContainText('no clinical selections or patient chart shown')
    await expect(packetSection.getByRole('button', { name: /accept|decline|contact patient|enter queue/i })).toHaveCount(0)
    await expect(packetSection.getByRole('button', { name: 'Authorize later request creation' })).toBeDisabled()
    await expect(packetSection).not.toContainText('40000000-0000-4000-8000-000000000040')
    await expect(packetSection).not.toContainText('39000000-0000-4000-8000-000000000039')
    expect(practiceReviewPacketRequests).toHaveLength(2)
    expect(practiceReviewPacketRequests.every((request) =>
      request.method === 'GET' && request.body === null && request.idempotency === undefined)).toBe(true)
    await packetSection.getByRole('button', { name: 'Close packet' }).click()
    await expect(page.getByRole('note')).toContainText('does not establish real identity')
    await expect(page.getByText(/Possible matching patient information is never shown/i)).toBeVisible()
    const refreshButton = page.getByRole('button', { name: 'Refresh all' })
    await refreshButton.focus()
    await expect(refreshButton).toBeFocused()
    await page.keyboard.press('Tab')
    await expect(practiceReviewSection.getByText('Coarse submitted sections')).toBeFocused()
    await page.keyboard.press('Tab')
    await expect(openPacket).toBeFocused()
    await page.keyboard.press('Tab')
    await expect(page.getByLabel('Review reason')).toBeFocused()
    await openPacket.click()
    await expect(packetSection).toBeVisible()
    await packetSection.getByLabel(/not a clinical eligibility decision/i).check()
    await packetSection.getByLabel(/not a coverage guarantee/i).check()
    await packetSection.getByLabel(/creates no request, queue, appointment/i).check()
    const authorizeLaterRequest = packetSection.getByRole('button', { name: 'Authorize later request creation' })
    await expect(authorizeLaterRequest).toBeEnabled()
    await authorizeLaterRequest.click()
    await expect(packetSection.getByRole('alert')).toContainText('Authorization result unknown')
    practiceReviewAuthorizationMaySucceed = true
    await packetSection.getByRole('button', { name: 'Retry unchanged authorization' }).click()
    await expect(practiceReviewSection.getByText('No synthetic work items are pending practice review.')).toBeVisible()
    expect(practiceReviewAuthorizationRequests).toHaveLength(2)
    expect(practiceReviewAuthorizationRequests[0].body).toEqual({
      expectedApplicantVersion: 24,
      packetPolicyVersion: 1,
      decision: 'AuthorizedForSyntheticRequestCreation',
      rationaleCode: 'OperationalPrerequisitesReviewed',
      noClinicalEligibilityAcknowledged: true,
      noCoverageGuaranteeAcknowledged: true,
      noRequestOrQueueAcknowledged: true,
    })
    expect(practiceReviewAuthorizationRequests[1].body).toEqual(practiceReviewAuthorizationRequests[0].body)
    expect(practiceReviewAuthorizationRequests[0].idempotency).toBeTruthy()
    expect(practiceReviewAuthorizationRequests[1].idempotency).toBe(practiceReviewAuthorizationRequests[0].idempotency)
    await page.getByLabel('Review reason').fill('Synthetic bounded evidence reviewed by authorized staff.')
    await page.getByLabel(/I confirm this uses synthetic data and is not identity proofing/i).check()
    await page.getByRole('button', { name: 'Approve for later prospective intake' }).click()
    await expect(page.getByText('No applicants are awaiting bounded identity review.')).toBeVisible()
    expect(identityBodies).toEqual([{
      expectedVersion: 2,
      decision: 'ApprovedForProspectiveIntake',
      reason: 'Synthetic bounded evidence reviewed by authorized staff.',
      syntheticDataConfirmed: true,
    }])
    await expect(page.getByRole('heading', { name: 'Synthetic promotion authorization' })).toBeVisible()
    await expect(page.getByText(/Assurance remains None and identity was not proved/i)).toBeVisible()
    await page.getByLabel('Promotion decision reason').fill('Complete synthetic process chain reviewed by authorized staff.')
    await page.getByLabel(/I acknowledge assurance is None/i).check()
    await page.getByLabel(/I confirm this uses synthetic data and creates no patient/i).check()
    await page.getByRole('button', { name: 'Record promotion decision' }).click()
    await expect(page.getByText('No applicants are awaiting synthetic promotion authorization.')).toBeVisible()
    expect(promotionBodies).toEqual([{
      expectedVersion: 10,
      decision: 'AuthorizedForSyntheticPromotion',
      reason: 'Complete synthetic process chain reviewed by authorized staff.',
      noneAssuranceAcknowledged: true,
      syntheticDataConfirmed: true,
    }])
    await expect(page.getByRole('heading', { name: 'Atomic synthetic patient promotion' })).toBeVisible()
    await expect(page.getByText(/possible match blocks creation without identifying or linking anyone/i)).toBeVisible()
    await page.getByLabel('Atomic promotion reason').fill('Authorized synthetic patient shell transaction reviewed.')
    await page.getByLabel(/a no-match result creates one minimal canonical synthetic patient shell/i).check()
    await page.getByLabel(/creates no portal, completed intake, consent, coverage/i).check()
    await page.getByRole('button', { name: 'Run duplicate check and promote' }).click()
    await expect(page.getByText('No applicants are authorized for atomic synthetic promotion.')).toBeVisible()
    expect(syntheticPromotionBodies).toEqual([{
      expectedVersion: 11,
      command: 'PromoteAuthorizedSyntheticApplicant',
      reason: 'Authorized synthetic patient shell transaction reviewed.',
      canonicalPatientCreationAcknowledged: true,
      noPortalNoCareAcknowledged: true,
    }])
    expect(await page.evaluate(() => JSON.stringify(localStorage))).not.toContain('Synthetic bounded evidence')
    expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toContain('Synthetic bounded evidence')
    expect(await page.evaluate(() => JSON.stringify(localStorage))).not.toContain('Complete synthetic process chain')
    expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toContain('Complete synthetic process chain')
    expect(await page.evaluate(() => JSON.stringify(localStorage))).not.toContain('Authorized synthetic patient shell')
    expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toContain('Authorized synthetic patient shell')

    await expectNoSeriousAccessibilityViolations(page)
    await expectTelehealthReflow(page)
  })

  test('physician workspace, pharmacy and prescription preparation, and safety disposition support recovery, semantics, reflow, and WCAG checks', async ({ page }) => {
    await installPassingMediaPreflight(page)
    let consultationCalls = 0
    let workspaceCalls = 0
    let documentationCalls = 0
    let wrapUpCalls = 0
    let pharmacyChoiceCalls = 0
    let prescriptionPreparationCalls = 0
    let safetyDispositionCalls = 0
    let completionReviewCalls = 0
    let failCompletionReview = true
    let currentDocumentation = consultationWorkspace.documentation
    const documentationBodies: Array<Record<string, unknown>> = []
    const consultationCommandKeys: Array<string | undefined> = []
    const wrapUpCommandKeys: Array<string | undefined> = []
    const wrapUpBodies: Array<Record<string, unknown>> = []
    const pharmacyChoiceCommandKeys: Array<string | undefined> = []
    const pharmacyChoiceBodies: Array<Record<string, unknown>> = []
    const prescriptionPreparationCommandKeys: Array<string | undefined> = []
    const prescriptionPreparationBodies: Array<Record<string, unknown>> = []
    const safetyDispositionCommandKeys: Array<string | undefined> = []
    const safetyDispositionBodies: Array<Record<string, unknown>> = []
    await page.route('**/api/telehealth/v1/clinician/queue', (route) => route.fulfill({ json: { requests: [{ ...queueRequest, status: 'Queued' }] } }))
    await page.route('**/api/telehealth/v1/clinician/shifts', (route) => route.fulfill({
      json: {
        shiftId: '30000000-0000-4000-8000-000000000003',
        status: 'Active',
        facilityId: 10,
        clinicianStaffId: 101,
        startedAt: '2026-08-26T12:10:00Z',
        version: 1,
      },
    }))
    await page.route('**/api/telehealth/v1/clinician/reservations/reserve-next', (route) => route.fulfill({ json: {
      reservationId: '70000000-0000-4000-8000-000000000007',
      requestId: queueRequest.requestId,
      queueEntryId: '80000000-0000-4000-8000-000000000008',
      shiftId: '30000000-0000-4000-8000-000000000003',
      clinicianStaffId: 101,
      reservedAt: '2026-08-26T12:10:00Z',
      leaseExpiresAt: '2026-08-27T04:31:00Z',
      status: 'Active',
      requestVersion: 9,
    } }))
    await page.route('**/api/telehealth/v1/clinician/reservations/*/connection-grants', (route) => route.fulfill({ json: { ...connectionGrant, participantRole: 'physician' } }))
    await page.route('**/api/telehealth/v1/clinician/reservations/*/consultations/start', async (route) => {
      consultationCalls += 1
      consultationCommandKeys.push(route.request().headers()['x-idempotency-key'])
      if (consultationCalls === 1) {
        await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Synthetic consultation-start service unavailable.' }) })
        return
      }
      await route.fulfill({ json: consultationStart })
    })
    await page.route('**/api/telehealth/v1/clinician/consultations/*/workspace', async (route) => {
      workspaceCalls += 1
      if (workspaceCalls === 1) {
        await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Synthetic read-only workspace unavailable.' }) })
        return
      }
      await route.fulfill({ json: { ...consultationWorkspace, documentation: currentDocumentation } })
    })
    await page.route('**/api/telehealth/v1/clinician/consultations/*/documentation/draft', async (route) => {
      documentationCalls += 1
      const body = route.request().postDataJSON() as Record<string, unknown>
      documentationBodies.push(body)
      if (documentationCalls === 1) {
        await route.fulfill({ status: 409, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'The current draft is version 0. Reload it before making another change.' }) })
        return
      }
      currentDocumentation = {
        version: Number(body.expectedVersion) + 1,
        savedAt: '2026-08-27T04:34:00Z',
        savedBy: 'gold-provider-01',
        isLocked: false,
        isSigned: false,
        isFinal: false,
        subjective: String(body.subjective),
        objective: null,
        assessment: null,
        plan: null,
      }
      await route.fulfill({ json: currentDocumentation })
    })
    await page.route('**/api/telehealth/v1/clinician/consultations/*/wrap-up', async (route) => {
      wrapUpCalls += 1
      wrapUpCommandKeys.push(route.request().headers()['x-idempotency-key'])
      wrapUpBodies.push(route.request().postDataJSON() as Record<string, unknown>)
      if (wrapUpCalls === 1) {
        await route.fulfill({ status: 409, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Synthetic wrap-up conflict; retry the exact command.' }) })
        return
      }
      await route.fulfill({ json: consultationWrapUp })
    })
    await page.route('**/api/telehealth/v1/clinician/consultations/*/pharmacy-choices**', (route) => route.fulfill({ json: pharmacyChoiceWorkspace }))
    await page.route('**/api/telehealth/v1/clinician/consultations/*/pharmacy-choice', async (route) => {
      pharmacyChoiceCalls += 1
      pharmacyChoiceCommandKeys.push(route.request().headers()['x-idempotency-key'])
      pharmacyChoiceBodies.push(route.request().postDataJSON() as Record<string, unknown>)
      if (pharmacyChoiceCalls === 1) {
        await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Synthetic pharmacy destination service unavailable.' }) })
        return
      }
      await route.fulfill({ json: pharmacyChoiceDraft })
    })
    await page.route('**/api/telehealth/v1/clinician/consultations/*/prescription-preparation-draft**', async (route) => {
      if (route.request().method() === 'GET') {
        const query = new URL(route.request().url()).searchParams.get('query')
        await route.fulfill({ json: {
          ...prescriptionPreparationWorkspace,
          currentPharmacyChoiceVersion: pharmacyChoiceCalls >= 2 ? 1 : null,
          catalogResults: query === 'metformin' ? [prescriptionCatalogItem] : [],
        } })
        return
      }
      prescriptionPreparationCalls += 1
      prescriptionPreparationCommandKeys.push(route.request().headers()['x-idempotency-key'])
      prescriptionPreparationBodies.push(route.request().postDataJSON() as Record<string, unknown>)
      if (prescriptionPreparationCalls === 1) {
        await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Synthetic prescription-preparation service unavailable.' }) })
        return
      }
      await route.fulfill({ json: prescriptionPreparationDraft })
    })
    await page.route('**/api/telehealth/v1/clinician/consultations/*/safety-disposition-draft', async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({ json: safetyDispositionWorkspace })
        return
      }
      safetyDispositionCalls += 1
      safetyDispositionCommandKeys.push(route.request().headers()['x-idempotency-key'])
      safetyDispositionBodies.push(route.request().postDataJSON() as Record<string, unknown>)
      if (safetyDispositionCalls === 1) {
        await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Synthetic safety-disposition service unavailable.' }) })
        return
      }
      await route.fulfill({ json: safetyDispositionDraft })
    })
    await page.route('**/api/telehealth/v1/clinician/consultations/*/completion-prerequisites', async (route) => {
      completionReviewCalls += 1
      if (failCompletionReview) {
        await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Synthetic completion review unavailable.' }) })
        return
      }
      const evidenceRecorded = pharmacyChoiceCalls >= 2 && safetyDispositionCalls >= 2
      await route.fulfill({ json: evidenceRecorded ? {
        ...completionPrerequisitesReview,
        safetyDisposition: {
          version: 1,
          dispositionCode: 'TreatedTelehealth',
          adequateEvaluationCompleted: true,
          followUpOwnerPresent: true,
          followUpTimeframePresent: true,
          nextStepInstructionsPresent: true,
          warningEscalationInstructionsPresent: true,
          communicationMethod: 'DiscussedDuringSyntheticConsultation',
          communicationCompleted: true,
          locationCallbackReconfirmed: false,
          emergencyInstructionProvided: false,
          emergencyHandoffStatusPresent: false,
          contactAttemptSummaryPresent: false,
        },
        pharmacyChoice: { version: 1, patientChoiceConfirmed: true },
        structuralEvidencePresent: true,
        productBlockers: completionPrerequisitesReview.productBlockers.slice(1),
      } : completionPrerequisitesReview })
    })
    await signInClinician(page, process.env.MODERN_UI_PHYSICIAN_USERNAME ?? 'gold-provider-01')
    await page.goto('/clinician/telehealth/physician')

    await expect(page.getByRole('heading', { name: 'Telehealth clinician queue' })).toBeVisible()
    await expect(page.getByRole('note')).toContainText(/No real consultation.*prescribing.*patient care is enabled/i)
    const startButton = page.getByRole('button', { name: 'Start telehealth shift' })
    await startButton.focus()
    await expect(startButton).toBeFocused()
    await page.keyboard.press('Enter')
    await expect(page.getByRole('button', { name: 'Shift active' })).toBeDisabled()
    await expect(page.getByText('Active at facility 10')).toBeVisible()
    await page.getByRole('button', { name: 'Reserve next request' }).click()
    await expect(page.getByRole('heading', { name: 'Reserved synthetic request' })).toBeVisible()
    await page.getByRole('button', { name: 'Check camera and microphone' }).click()
    await expect(page.getByRole('status')).toContainText('Device check passed')
    await page.getByRole('button', { name: 'Enter physician waiting room' }).click()
    await expect(page.getByRole('heading', { name: 'Physician grant ready' })).toBeVisible()
    await expect(page.getByText(connectionGrant.joinCredential)).toHaveCount(0)
    await expect(page.getByText(/does not start a consultation/i)).toBeVisible()

    await page.getByLabel('Reconfirmed patient state').selectOption('CA')
    await page.getByLabel('Patient identity discussion completed').check()
    await page.getByLabel('Callback number reconfirmed').check()
    await page.getByLabel('Privacy and other participants discussed').check()
    await page.getByLabel(/Telehealth consent discussion completed/).check()
    await page.getByLabel(/No concerning symptom change/).check()
    await page.getByLabel('Emergency plan reviewed').check()
    await page.getByLabel(/Synthetic communication check is sufficient/).check()
    const startLifecycle = page.getByRole('button', { name: 'Start synthetic lifecycle' })
    await expect(startLifecycle).toBeEnabled()
    await startLifecycle.click()
    await expect(page.getByRole('alert')).toContainText('Synthetic consultation-start service unavailable.')
    await expect(page.getByLabel('Emergency plan reviewed')).toBeChecked()
    await startLifecycle.click()
    await expect(page.getByRole('heading', { name: 'Synthetic consultation lifecycle started' })).toBeVisible()
    await expect(page.getByText(/sequential encounter key is not exposed/i)).toBeVisible()
    await expect(page.getByText(/diagnosis, signing, prescribing, claims, and completion are unavailable/i)).toBeVisible()
    await expect(page.getByRole('alert')).toContainText('Synthetic read-only workspace unavailable.')
    await expect(page.getByText('Synthetic Workspace Patient')).toHaveCount(0)
    const reloadProjection = page.getByRole('button', { name: 'Reload workspace' })
    await reloadProjection.focus()
    await expect(reloadProjection).toBeFocused()
    await page.keyboard.press('Enter')
    await expect(page.getByRole('heading', { name: 'Consultation workspace' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Patient and callback' })).toBeVisible()
    await expect(page.getByText('Synthetic Workspace Patient')).toBeVisible()
    await expect(page.getByText('Synthetic maintenance medicine')).toBeVisible()
    await expect(page.getByText('Synthetic active problem')).toBeVisible()
    await expect(page.getByText(/Verify allergies, medications, problems/i)).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Unsigned SOAP draft' })).toBeVisible()
    await expect(page.getByLabel('Subjective')).toHaveValue('')
    const saveDraft = page.getByRole('button', { name: 'Save unsigned draft' })
    await expect(saveDraft).toBeDisabled()
    await page.getByLabel('Subjective').fill('Synthetic clinician-entered history.')
    await expect(page.getByText('Unsaved changes. Nothing is saved automatically.')).toBeVisible()
    await saveDraft.click()
    await expect(page.getByRole('alert')).toContainText('current draft is version 0')
    await expect(page.getByLabel('Subjective')).toHaveValue('Synthetic clinician-entered history.')
    await saveDraft.click()
    await expect(page.getByText(/Unsigned synthetic draft version 1 saved/)).toBeVisible()
    await page.getByLabel('Plan').fill('Unsaved local plan text.')
    await reloadProjection.click()
    await expect(page.getByRole('heading', { name: 'Unsaved changes' })).toBeVisible()
    expect(workspaceCalls).toBe(2)
    await page.getByRole('button', { name: 'Keep editing' }).click()
    await expect(page.getByLabel('Plan')).toHaveValue('Unsaved local plan text.')
    await reloadProjection.click()
    await page.getByRole('button', { name: 'Replace with saved draft' }).click()
    await expect(page.getByLabel('Plan')).toHaveValue('')
    await expect(page.getByLabel('Subjective')).toHaveValue('Synthetic clinician-entered history.')
    expect(workspaceCalls).toBe(3)
    await expect(page.getByRole('heading', { name: 'Enter unfinished wrap-up' })).toBeVisible()
    await page.getByLabel(/synthetic session is ended/i).check()
    await page.getByLabel(/Documentation and any future safety disposition remain incomplete/i).check()
    await page.getByLabel(/I remain responsible.*unavailable for new work/i).check()
    const enterWrapUp = page.getByRole('button', { name: 'End synthetic session and enter wrap-up' })
    await expect(enterWrapUp).toBeEnabled()
    await enterWrapUp.click()
    await expect(page.getByRole('alert')).toContainText('Synthetic wrap-up conflict')
    await expect(page.getByLabel(/synthetic session is ended/i)).toBeChecked()
    await enterWrapUp.click()
    await expect(page.getByRole('heading', { name: 'Wrap-up is active' })).toBeVisible()
    await expect(page.getByText(/still unfinished.*remain responsible.*unavailable for new work/i)).toBeVisible()
    await expect(page.getByText(/Lifecycle: unfinished wrap-up/i)).toBeVisible()
    const completionPanel = page.locator('.telehealth-completion-review')
    const completionAlert = completionPanel.getByRole('alert')
    await expect(completionAlert).toContainText('Synthetic completion review unavailable.')
    await expect(completionAlert).toBeFocused()
    failCompletionReview = false
    await completionPanel.getByRole('button', { name: 'Reload review' }).click()
    await expect(completionPanel.getByRole('heading', { name: 'Pre-finalization evidence review' })).toBeVisible()
    await expect(completionPanel.getByRole('note')).toContainText(/does not judge clinical completeness.*readiness to sign/i)
    await expect(completionPanel.getByText(/None recorded.*optional.*not a blocker/i)).toBeVisible()
    await expect(completionPanel.getByText(/Unavailable:.*signing.*completion.*patient delivery.*downstream creation/i)).toBeVisible()
    await page.getByLabel('Plan').fill('Synthetic unfinished wrap-up plan.')
    await page.getByRole('button', { name: 'Save unsigned draft' }).click()
    await expect(page.getByText(/Unsigned synthetic draft version 2 saved/)).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Patient-confirmed pharmacy destination draft' })).toBeVisible()
    await expect(page.locator('.telehealth-pharmacy-choice').getByRole('note')).toContainText(/does not create, sign, route, or transmit a prescription/i)
    await page.getByLabel(/Synthetic Golden Gate Pharmacy/).check()
    await page.getByLabel(/The patient chose or confirmed this destination/).check()
    const recordDestination = page.getByRole('button', { name: 'Record destination draft' })
    await recordDestination.click()
    await expect(page.getByRole('alert')).toContainText('Synthetic pharmacy destination service unavailable.')
    await expect(page.getByLabel(/Synthetic Golden Gate Pharmacy/)).toBeChecked()
    await expect(page.getByLabel(/The patient chose or confirmed this destination/)).toBeChecked()
    await recordDestination.click()
    await expect(page.getByRole('heading', { name: 'Current unsigned destination draft' })).toBeVisible()
    await expect(page.getByText(/Destination draft version 1 recorded.*No prescription was created or transmitted/i)).toBeVisible()
    await expect(page.getByText(/Prescription created: no.*Transmitted: no/i)).toBeVisible()
    const prescriptionPanel = page.locator('.telehealth-prescription-preparation')
    await expect(prescriptionPanel.getByRole('heading', { name: 'Prescription preparation' })).toBeVisible()
    await expect(prescriptionPanel.getByRole('note')).toContainText(/not a prescription or drug recommendation.*No interaction or contraindication service/i)
    await expect(prescriptionPanel.locator('p').filter({ hasText: /^No medication selected\./ })).toBeVisible()
    await expect(prescriptionPanel.getByLabel('Dose amount')).toHaveValue('')
    await expect(prescriptionPanel.getByRole('button', { name: /sign|send|transmit|prescribe/i })).toHaveCount(0)
    await prescriptionPanel.getByRole('button', { name: 'Reload saved draft' }).click()
    await expect(prescriptionPanel.getByText(/Current patient-confirmed pharmacy draft version 1/i)).toBeVisible()
    await prescriptionPanel.getByLabel('Search the synthetic medication catalog').fill('metformin')
    await prescriptionPanel.getByRole('button', { name: 'Search catalog' }).click()
    await prescriptionPanel.getByLabel(/Metformin 500 mg tablet/i).check()
    await expect(prescriptionPanel.getByLabel('Dose amount')).toHaveValue('')
    await expect(prescriptionPanel.getByLabel('Physician-entered directions')).toHaveValue('')
    await prescriptionPanel.getByLabel('Dose amount').fill('1')
    await prescriptionPanel.getByLabel('Dose unit').fill('tablet')
    await prescriptionPanel.getByLabel('Frequency').fill('once daily (synthetic)')
    await prescriptionPanel.getByLabel('Quantity', { exact: true }).fill('7')
    await prescriptionPanel.getByLabel('Quantity unit').fill('tablet')
    await prescriptionPanel.getByLabel('Duration in days').fill('7')
    await prescriptionPanel.getByLabel('Refills (0–5)').fill('0')
    await prescriptionPanel.getByLabel('Physician-entered indication').fill(prescriptionPreparationDraft.indication)
    await prescriptionPanel.getByLabel('Physician-entered directions').fill(prescriptionPreparationDraft.directions)
    await prescriptionPanel.getByLabel(/reviewed the current medication information/i).check()
    await prescriptionPanel.getByLabel(/reviewed the current allergy information/i).check()
    await prescriptionPanel.getByLabel(/available evaluation was adequate/i).check()
    await prescriptionPanel.getByLabel(/contains synthetic demonstration data only and has no legal or patient-care effect/i).check()
    const recordPreparation = prescriptionPanel.getByRole('button', { name: 'Record preparation draft' })
    await expect(recordPreparation).toBeEnabled()
    await recordPreparation.click()
    await expect(prescriptionPanel.getByRole('alert')).toContainText('Synthetic prescription-preparation service unavailable.')
    await expect(prescriptionPanel.getByRole('alert')).toBeFocused()
    await expect(prescriptionPanel.getByLabel(/Metformin 500 mg tablet/i)).toBeChecked()
    await expect(prescriptionPanel.getByLabel('Physician-entered directions')).toHaveValue(prescriptionPreparationDraft.directions)
    await expect(prescriptionPanel.getByLabel(/contains synthetic demonstration data only and has no legal or patient-care effect/i)).toBeChecked()
    await recordPreparation.click()
    await expect(prescriptionPanel.getByRole('heading', { name: 'Current preparation draft' })).toBeVisible()
    await expect(prescriptionPanel.getByText(/Preparation draft version 1 recorded.*not safety checked, signed, created as a prescription, or transmitted/i)).toBeVisible()
    await expect(prescriptionPanel.getByText(/Safety checked: no.*Signed: no.*Canonical prescription created: no.*Transmission queued: no.*Patient delivered: no.*Legal effect: no/i)).toBeVisible()
    const dispositionPanel = page.locator('.telehealth-disposition-draft')
    await expect(dispositionPanel.getByRole('heading', { name: 'Safety disposition and follow-up draft' })).toBeVisible()
    await expect(dispositionPanel.getByRole('note')).toContainText(/unsigned and not delivered.*supplies no medical advice/i)
    await dispositionPanel.getByRole('combobox', { name: 'Disposition', exact: true }).selectOption('TreatedTelehealth')
    await dispositionPanel.getByLabel(/available evaluation was adequate/i).check()
    await dispositionPanel.getByRole('combobox', { name: 'Follow-up owner', exact: true }).selectOption('Patient')
    await dispositionPanel.getByLabel('Physician-authored follow-up timeframe').fill(safetyDispositionDraft.followUpTimeframe)
    await dispositionPanel.getByLabel('Physician-authored next-step instructions').fill(safetyDispositionDraft.nextStepInstructions)
    await dispositionPanel.getByLabel('Physician-authored warning signs and escalation instructions').fill(safetyDispositionDraft.warningEscalationInstructions)
    await dispositionPanel.getByRole('combobox', { name: 'Communication method', exact: true }).selectOption('DiscussedDuringSyntheticConsultation')
    await dispositionPanel.getByLabel(/I completed this selected synthetic communication method/i).check()
    await dispositionPanel.getByLabel(/I confirm this draft contains synthetic demonstration data only/i).check()
    const recordSafetyDraft = dispositionPanel.getByRole('button', { name: 'Record safety draft' })
    await expect(recordSafetyDraft).toBeEnabled()
    await recordSafetyDraft.click()
    await expect(page.getByRole('alert')).toContainText('Synthetic safety-disposition service unavailable.')
    await expect(dispositionPanel.getByRole('combobox', { name: 'Disposition', exact: true })).toHaveValue('TreatedTelehealth')
    await expect(dispositionPanel.getByLabel(/I confirm this draft contains synthetic demonstration data only/i)).toBeChecked()
    await recordSafetyDraft.click()
    await expect(dispositionPanel.getByRole('heading', { name: 'Current unsigned, undelivered draft' })).toBeVisible()
    await expect(page.getByText(/Unsigned safety-disposition draft version 1 recorded.*not signed, finalized, or delivered/i)).toBeVisible()
    await expect(page.getByText(/Signed: no.*Finalized: no.*Patient delivered: no.*Legal effect: no/i)).toBeVisible()
    await completionPanel.getByRole('button', { name: 'Reload review' }).click()
    await expect(completionPanel.getByText(/Treated telehealth.*version 1/i)).toBeVisible()
    await expect(completionPanel.getByText(/Structural drafts recorded: yes.*not a clinical readiness result/i)).toBeVisible()
    await expect(completionPanel.getByText(safetyDispositionDraft.nextStepInstructions)).toHaveCount(0)
    await expect(completionPanel.getByRole('button', { name: /sign|complete|finalize|deliver/i })).toHaveCount(0)
    expect(completionReviewCalls).toBeGreaterThanOrEqual(3)
    expect(documentationCalls).toBe(3)
    expect(documentationBodies).toEqual([
      { expectedVersion: 0, subjective: 'Synthetic clinician-entered history.', objective: null, assessment: null, plan: null },
      { expectedVersion: 0, subjective: 'Synthetic clinician-entered history.', objective: null, assessment: null, plan: null },
      { expectedVersion: 1, subjective: 'Synthetic clinician-entered history.', objective: null, assessment: null, plan: 'Synthetic unfinished wrap-up plan.' },
    ])
    expect(consultationCommandKeys).toHaveLength(2)
    expect(consultationCommandKeys[0]).toBe(consultationCommandKeys[1])
    expect(consultationCommandKeys[0]).toMatch(/^[0-9a-f-]{36}$/)
    expect(wrapUpCalls).toBe(2)
    expect(wrapUpCommandKeys[0]).toBe(wrapUpCommandKeys[1])
    expect(wrapUpCommandKeys[0]).toMatch(/^[0-9a-f-]{36}$/)
    expect(wrapUpBodies).toEqual([
      { expectedVersion: 1, syntheticSessionEndedConfirmed: true, documentationStillIncompleteAcknowledged: true, wrapUpResponsibilityAcknowledged: true },
      { expectedVersion: 1, syntheticSessionEndedConfirmed: true, documentationStillIncompleteAcknowledged: true, wrapUpResponsibilityAcknowledged: true },
    ])
    expect(pharmacyChoiceCalls).toBe(2)
    expect(pharmacyChoiceCommandKeys[0]).toBe(pharmacyChoiceCommandKeys[1])
    expect(pharmacyChoiceCommandKeys[0]).toMatch(/^[0-9a-f-]{36}$/)
    expect(pharmacyChoiceBodies).toEqual([
      { expectedVersion: 0, directoryEntryId: pharmacyChoiceDraft.directoryEntryId, patientChoiceConfirmed: true, syntheticDataConfirmed: true },
      { expectedVersion: 0, directoryEntryId: pharmacyChoiceDraft.directoryEntryId, patientChoiceConfirmed: true, syntheticDataConfirmed: true },
    ])
    expect(prescriptionPreparationCalls).toBe(2)
    expect(prescriptionPreparationCommandKeys[0]).toBe(prescriptionPreparationCommandKeys[1])
    expect(prescriptionPreparationCommandKeys[0]).toMatch(/^[0-9a-f-]{36}$/)
    expect(prescriptionPreparationBodies).toEqual([
      {
        expectedVersion: 0,
        rxNormCode: prescriptionCatalogItem.rxNormCode,
        doseAmount: 1,
        doseUnit: 'tablet',
        frequency: 'once daily (synthetic)',
        quantityValue: 7,
        quantityUnit: 'tablet',
        durationDays: 7,
        refills: 0,
        indication: prescriptionPreparationDraft.indication,
        directions: prescriptionPreparationDraft.directions,
        medicationListReviewed: true,
        allergyListReviewed: true,
        adequateEvaluationCompleted: true,
        syntheticDataConfirmed: true,
      },
      {
        expectedVersion: 0,
        rxNormCode: prescriptionCatalogItem.rxNormCode,
        doseAmount: 1,
        doseUnit: 'tablet',
        frequency: 'once daily (synthetic)',
        quantityValue: 7,
        quantityUnit: 'tablet',
        durationDays: 7,
        refills: 0,
        indication: prescriptionPreparationDraft.indication,
        directions: prescriptionPreparationDraft.directions,
        medicationListReviewed: true,
        allergyListReviewed: true,
        adequateEvaluationCompleted: true,
        syntheticDataConfirmed: true,
      },
    ])
    expect(safetyDispositionCalls).toBe(2)
    expect(safetyDispositionCommandKeys[0]).toBe(safetyDispositionCommandKeys[1])
    expect(safetyDispositionCommandKeys[0]).toMatch(/^[0-9a-f-]{36}$/)
    expect(safetyDispositionBodies).toEqual([
      {
        expectedVersion: 0,
        dispositionCode: 'TreatedTelehealth',
        adequateEvaluationCompleted: true,
        followUpOwner: 'Patient',
        followUpTimeframe: safetyDispositionDraft.followUpTimeframe,
        nextStepInstructions: safetyDispositionDraft.nextStepInstructions,
        warningEscalationInstructions: safetyDispositionDraft.warningEscalationInstructions,
        communicationMethod: 'DiscussedDuringSyntheticConsultation',
        communicationCompleted: true,
        locationCallbackReconfirmed: false,
        emergencyInstructionProvided: false,
        emergencyHandoffStatus: null,
        contactAttemptSummary: null,
        syntheticDataConfirmed: true,
      },
      {
        expectedVersion: 0,
        dispositionCode: 'TreatedTelehealth',
        adequateEvaluationCompleted: true,
        followUpOwner: 'Patient',
        followUpTimeframe: safetyDispositionDraft.followUpTimeframe,
        nextStepInstructions: safetyDispositionDraft.nextStepInstructions,
        warningEscalationInstructions: safetyDispositionDraft.warningEscalationInstructions,
        communicationMethod: 'DiscussedDuringSyntheticConsultation',
        communicationCompleted: true,
        locationCallbackReconfirmed: false,
        emergencyInstructionProvided: false,
        emergencyHandoffStatus: null,
        contactAttemptSummary: null,
        syntheticDataConfirmed: true,
      },
    ])
    await expect(page.getByText(connectionGrant.joinCredential)).toHaveCount(0)
    await expect(page.getByText(/encounter\s+\d+/i)).toHaveCount(0)
    await expect(page.getByText(/forbidden-patient-id-marker|forbidden-encounter-id-marker|forbidden-policy-marker|forbidden-address-marker/i)).toHaveCount(0)
    const browserStorage = await page.evaluate(() => JSON.stringify({ session: sessionStorage, local: localStorage }))
    expect(browserStorage).not.toContain(connectionGrant.joinCredential)
    expect(browserStorage).not.toContain('Synthetic Workspace Patient')
    expect(browserStorage).not.toContain(prescriptionPreparationDraft.directions)
    expect(browserStorage).not.toContain(safetyDispositionDraft.nextStepInstructions)

    await expectNoSeriousAccessibilityViolations(page)
    await expectTelehealthReflow(page)
  })
})
