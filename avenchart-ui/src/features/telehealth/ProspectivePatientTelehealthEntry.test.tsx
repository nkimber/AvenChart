// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import ProspectivePatientTelehealthEntry from './ProspectivePatientTelehealthEntry.tsx'
import { acknowledgeApplicantPreRequestReadiness, acknowledgeApplicantTelehealthNotice, confirmApplicantClinicalInformationSummary, confirmApplicantInsuranceHandoff, confirmApplicantRegistrationDetails, createProspectiveApplicant, evaluateProspectiveSafetyTriage, getApplicantAllergyInformation, getApplicantClinicalInformationInventory, getApplicantClinicalInformationSummary, getApplicantCommunicationAccessReadiness, getApplicantDevicePreparation, getApplicantHealthHistoryInformation, getApplicantInsuranceHandoff, getApplicantMedicationInformation, getApplicantPracticeReviewSubmission, getApplicantPreRequestReadiness, getApplicantRegistrationDetails, getApplicantTelehealthNotice, getProspectiveApplicant, getProspectivePracticeNetworkOptions, recordApplicantAllergyInformation, recordApplicantClinicalInformationInventory, recordApplicantCommunicationAccessReadiness, recordApplicantDevicePreparation, recordApplicantHealthHistoryInformation, recordApplicantMedicationInformation, recordProspectiveEligibility, recordProspectiveIdentityProofing, recordProspectiveMemberInsuranceDetails, recordProspectivePracticeNetwork, recordProspectivePracticeNetworkPrecheck, recordProspectiveVisitPurpose, submitApplicantPracticeReview, verifyProspectiveApplicantContact, type TelehealthApplicantAllergyInformation, type TelehealthApplicantClinicalInformationInventory, type TelehealthApplicantClinicalInformationSummary, type TelehealthApplicantCommunicationAccessReadiness, type TelehealthApplicantDevicePreparation, type TelehealthApplicantHealthHistoryInformation, type TelehealthApplicantInsuranceHandoff, type TelehealthApplicantMedicationInformation, type TelehealthApplicantNotice, type TelehealthApplicantPracticeReview, type TelehealthApplicantPreRequestReadiness, type TelehealthApplicantRegistrationDetails } from './api.ts'
import { runTelehealthDevicePreflight } from './devicePreflight.ts'

vi.mock('./api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('./api.ts')>()
  return {
    ...original,
    acknowledgeApplicantPreRequestReadiness: vi.fn(),
    acknowledgeApplicantTelehealthNotice: vi.fn(),
    confirmApplicantClinicalInformationSummary: vi.fn(),
    confirmApplicantInsuranceHandoff: vi.fn(),
    confirmApplicantRegistrationDetails: vi.fn(),
    createProspectiveApplicant: vi.fn(),
    evaluateProspectiveSafetyTriage: vi.fn(),
    getApplicantAllergyInformation: vi.fn(),
    getApplicantClinicalInformationInventory: vi.fn(),
    getApplicantClinicalInformationSummary: vi.fn(),
    getApplicantCommunicationAccessReadiness: vi.fn(),
    getApplicantDevicePreparation: vi.fn(),
    getApplicantHealthHistoryInformation: vi.fn(),
    getApplicantRegistrationDetails: vi.fn(),
    getApplicantInsuranceHandoff: vi.fn(),
    getApplicantMedicationInformation: vi.fn(),
    getApplicantPracticeReviewSubmission: vi.fn(),
    getApplicantPreRequestReadiness: vi.fn(),
    getApplicantTelehealthNotice: vi.fn(),
    getProspectiveApplicant: vi.fn(),
    getProspectivePracticeNetworkOptions: vi.fn(),
    recordApplicantAllergyInformation: vi.fn(),
    recordApplicantCommunicationAccessReadiness: vi.fn(),
    recordApplicantClinicalInformationInventory: vi.fn(),
    recordApplicantDevicePreparation: vi.fn(),
    recordApplicantHealthHistoryInformation: vi.fn(),
    recordApplicantMedicationInformation: vi.fn(),
    recordProspectiveEligibility: vi.fn(),
    recordProspectiveIdentityProofing: vi.fn(),
    recordProspectiveMemberInsuranceDetails: vi.fn(),
    recordProspectivePracticeNetwork: vi.fn(),
    recordProspectivePracticeNetworkPrecheck: vi.fn(),
    recordProspectiveVisitPurpose: vi.fn(),
    submitApplicantPracticeReview: vi.fn(),
    verifyProspectiveApplicantContact: vi.fn(),
  }
})

vi.mock('./devicePreflight.ts', () => ({
  runTelehealthDevicePreflight: vi.fn(),
}))

const approvedApplicant = {
  applicantId: '10000000-0000-4000-8000-000000000001',
  status: 'IdentityReviewApproved' as const,
  version: 3,
  practiceDisplayName: 'Synthetic Practice',
  residenceStateCode: 'GA',
  maskedEmail: 'a***@example.test',
  maskedPhone: '*******0199',
  contactVerified: true,
  identityAssurance: 'ContactControlOnly' as const,
  duplicateDisposition: 'NoCandidate' as const,
  canonicalPatientCreated: false as const,
  verificationAttemptsRemaining: 0,
  expiresAt: '2026-08-27T15:00:00Z',
  demonstrationVerificationCode: null,
  nextAction: 'Complete the bounded synthetic universal safety screen.',
  limitations: ['No patient or request exists.'],
}

const registrationDetailsFixture = {
  applicantId: approvedApplicant.applicantId,
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
  direction: 'Confirm the copied minimum registration details before any later intake step.',
  limitations: ['No patient record was changed and no downstream workflow was enabled.'],
} satisfies TelehealthApplicantRegistrationDetails

const insuranceHandoffFixture = {
  applicantId: approvedApplicant.applicantId,
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
} satisfies TelehealthApplicantInsuranceHandoff

const communicationAccessReadinessFixture = {
  applicantId: approvedApplicant.applicantId,
  applicantVersion: 15,
  applicantStatus: 'SyntheticInsuranceDetailsConfirmed',
  currentLocationStateCode: 'GA',
  maskedCallbackPhone: '(***) ***-0199',
  supportedSpokenLanguages: ['English', 'Spanish'] as Array<'English' | 'Spanish'>,
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
  direction: 'Record only bounded synthetic communication and access preferences.',
  limitations: ['No interpreter or accessibility service was contacted.'],
} satisfies TelehealthApplicantCommunicationAccessReadiness

const devicePreparationFixture = {
  applicantId: approvedApplicant.applicantId,
  applicantVersion: 16,
  applicantStatus: 'SyntheticCommunicationAccessReadinessRecorded',
  supportedNetworkQualities: ['Unknown', 'Good'] as Array<'Unknown' | 'Good'>,
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
  limitations: ['No media or precise device data is sent or stored.'],
} satisfies TelehealthApplicantDevicePreparation

const clinicalInformationInventoryFixture = {
  applicantId: approvedApplicant.applicantId,
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
  direction: 'Choose one coarse status for each category.',
  limitations: ['No clinical details or canonical chart content is collected.'],
} satisfies TelehealthApplicantClinicalInformationInventory

const medicationInformationFixture = {
  applicantId: approvedApplicant.applicantId,
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
} satisfies TelehealthApplicantMedicationInformation

const allergyInformationFixture = {
  applicantId: approvedApplicant.applicantId,
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
    catalogVersion: 1 as const,
    codingSystem: 'LOCAL_SYNTHETIC_ONLY' as const,
    snomedCtMapped: false as const,
    rxNormMapped: false as const,
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
} satisfies TelehealthApplicantAllergyInformation

const healthHistoryInformationFixture = {
  applicantId: approvedApplicant.applicantId,
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
    catalogVersion: 1 as const,
    codingSystem: 'LOCAL_SYNTHETIC_ONLY' as const,
    snomedCtMapped: false as const,
    icd10CmMapped: false as const,
    loincMapped: false as const,
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
} satisfies TelehealthApplicantHealthHistoryInformation

const clinicalInformationSummaryFixture = {
  applicantId: approvedApplicant.applicantId,
  applicantVersion: 21,
  applicantStatus: 'SyntheticHealthHistoryInformationRecorded',
  clinicalInformationSummarySnapshotFingerprint: 'e'.repeat(64),
  policyKey: 'SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_SUMMARY',
  policyVersion: 1,
  categories: [
    { categoryKey: 'Medications', inventoryStatus: 'ItemsToReview', selectedItemCount: 2, additionalOrUnlistedItemsReported: false, reviewRoute: 'ClinicianMedicationReviewRequired' },
    { categoryKey: 'AllergiesOrIntolerances', inventoryStatus: 'Unsure', selectedItemCount: 0, additionalOrUnlistedItemsReported: false, reviewRoute: 'AssistedAllergyReviewRequired' },
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
  direction: 'Review the server-derived summary.',
  limitations: ['No canonical clinical record, reconciliation, intake completion, or request is created.'],
} satisfies TelehealthApplicantClinicalInformationSummary

const preRequestReadinessFixture = {
  applicantId: approvedApplicant.applicantId,
  applicantVersion: 22,
  applicantStatus: 'SyntheticClinicalInformationSummaryConfirmed',
  preRequestReadinessSnapshotFingerprint: 'f'.repeat(64),
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
  direction: 'Review the five server-derived sections.',
  limitations: ['No completion, request, queue entry, or care capability is created.'],
} satisfies TelehealthApplicantPreRequestReadiness

const practiceReviewFixture = {
  applicantId: approvedApplicant.applicantId,
  applicantVersion: 23,
  applicantStatus: 'SyntheticPreRequestReadinessAcknowledged',
  practiceReviewSnapshotFingerprint: '1'.repeat(64),
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
  direction: 'Review the submission limits.',
  limitations: ['No telehealth request or care queue is created.'],
} satisfies TelehealthApplicantPracticeReview

describe('ProspectivePatientTelehealthEntry safety triage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    sessionStorage.clear()
    sessionStorage.setItem('avenchart-ui.telehealthProspectiveApplicant', JSON.stringify({
      applicantId: approvedApplicant.applicantId,
      applicantAccessKey: 'a'.repeat(64),
    }))
    vi.mocked(getProspectiveApplicant).mockResolvedValue(approvedApplicant)
    vi.mocked(getApplicantRegistrationDetails).mockResolvedValue(registrationDetailsFixture)
    vi.mocked(getApplicantInsuranceHandoff).mockResolvedValue(insuranceHandoffFixture)
    vi.mocked(getApplicantCommunicationAccessReadiness).mockResolvedValue(communicationAccessReadinessFixture)
    vi.mocked(getApplicantDevicePreparation).mockResolvedValue(devicePreparationFixture)
    vi.mocked(getApplicantClinicalInformationInventory).mockResolvedValue(clinicalInformationInventoryFixture)
    vi.mocked(getApplicantMedicationInformation).mockResolvedValue(medicationInformationFixture)
    vi.mocked(getApplicantAllergyInformation).mockResolvedValue(allergyInformationFixture)
    vi.mocked(getApplicantHealthHistoryInformation).mockResolvedValue(healthHistoryInformationFixture)
    vi.mocked(getApplicantClinicalInformationSummary).mockResolvedValue(clinicalInformationSummaryFixture)
    vi.mocked(getApplicantPreRequestReadiness).mockResolvedValue(preRequestReadinessFixture)
    vi.mocked(getApplicantPracticeReviewSubmission).mockResolvedValue(practiceReviewFixture)
    vi.mocked(runTelehealthDevicePreflight).mockResolvedValue({
      status: 'failed',
      message: 'This browser cannot run the secure telehealth device check.',
    })
    vi.mocked(getProspectivePracticeNetworkOptions).mockResolvedValue({
      applicantId: approvedApplicant.applicantId,
      applicantVersion: 5,
      applicantStatus: 'VisitPurposeRecorded',
      adapterMode: 'NON_PRODUCTION',
      catalogKey: 'avenchart-synthetic-prospective-practice-network-2026-08',
      catalogVersion: 1,
      catalogEffectiveFrom: '2026-08-27T00:00:00Z',
      catalogEffectiveThrough: '2026-10-31T23:59:59Z',
      plans: [],
      memberEligibilityChecked: false,
      memberBenefitsChecked: false,
      renderingPhysicianNetworkChecked: false,
      coverageVerified: false,
      exactNetworkConfirmed: false,
      direction: 'Choose one fictional plan.',
      limitations: ['No payer was contacted.'],
    })
  })

  it('shows immediate emergency direction and retains one retry identity after ambiguity', async () => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('20000000-0000-4000-8000-000000000002')
    vi.mocked(evaluateProspectiveSafetyTriage)
      .mockRejectedValueOnce(new Error('Result unknown; retry unchanged.'))
      .mockResolvedValue({
        evaluationId: '30000000-0000-4000-8000-000000000003',
        applicantId: approvedApplicant.applicantId,
        applicantVersion: 4,
        applicantStatus: 'SafetyScreenPassed',
        safetyDisposition: 'ContinueProspectiveIntake',
        currentLocationStateCode: 'GA',
        protocolKey: 'synthetic-universal-safety',
        protocolVersion: 1,
        evaluatedAt: '2026-08-27T14:00:00Z',
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
        direction: 'Later intake gates are still required.',
        limitations: ['No clinician reviewed these answers.'],
      })

    render(<MemoryRouter><ProspectivePatientTelehealthEntry /></MemoryRouter>)
    await screen.findByRole('heading', { name: 'Emergency direction stays available' })
    expect(screen.getAllByRole('link', { name: 'Call 911' }).length).toBeGreaterThan(0)

    const emergency = screen.getByRole('group', { name: 'Could any current symptom be an emergency?' })
    fireEvent.click(within(emergency).getByLabelText('Yes'))
    expect(screen.getByRole('alert')).toHaveTextContent('Call 911 now')
    fireEvent.click(within(emergency).getByLabelText('No'))

    fireEvent.change(screen.getByLabelText('Current physical location'), { target: { value: 'GA' } })
    fireEvent.click(screen.getByLabelText(/current physical location, not merely/i))
    fireEvent.click(within(screen.getByRole('group', { name: 'Are symptoms severe or getting worse quickly?' })).getByLabelText('No'))
    fireEvent.click(within(screen.getByRole('group', { name: 'Does this seem to require a hands-on examination or procedure?' })).getByLabelText('No'))
    fireEvent.click(within(screen.getByRole('group', { name: 'Are you unsure about any answer above?' })).getByLabelText('No'))
    fireEvent.click(screen.getByLabelText(/location and every safety answer are fictional/i))
    const submit = screen.getByRole('button', { name: 'Evaluate universal safety screen' })
    fireEvent.click(submit)

    const failure = await screen.findByRole('alert')
    expect(failure).toHaveTextContent('Result unknown; retry unchanged.')
    await waitFor(() => expect(failure).toHaveFocus())
    fireEvent.click(screen.getByRole('button', { name: 'Evaluate universal safety screen' }))

    await waitFor(() => expect(evaluateProspectiveSafetyTriage).toHaveBeenCalledTimes(2))
    expect(vi.mocked(evaluateProspectiveSafetyTriage).mock.calls[0][3]).toBe('20000000-0000-4000-8000-000000000002')
    expect(vi.mocked(evaluateProspectiveSafetyTriage).mock.calls[1][3]).toBe('20000000-0000-4000-8000-000000000002')
    expect(await screen.findByRole('heading', { name: 'Universal safety screen recorded' })).toBeInTheDocument()
    expect(screen.getAllByText(/No clinician reviewed these answers/i).length).toBeGreaterThan(0)

    const stored = sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''
    expect(stored).not.toMatch(/emergency|severe|handsOn|unsure|locationState/i)
    expect(createProspectiveApplicant).not.toHaveBeenCalled()
    expect(verifyProspectiveApplicantContact).not.toHaveBeenCalled()
  })

  it('records one controlled purpose with a stable retry identity and no browser persistence', async () => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('40000000-0000-4000-8000-000000000004')
    vi.mocked(getProspectiveApplicant).mockResolvedValue({
      ...approvedApplicant,
      status: 'SafetyScreenPassed',
      version: 4,
      nextAction: 'Choose one controlled synthetic visit-purpose category.',
    })
    vi.mocked(recordProspectiveVisitPurpose)
      .mockRejectedValueOnce(new Error('Result unknown; retry unchanged.'))
      .mockResolvedValue({
        purposeId: '50000000-0000-4000-8000-000000000005',
        applicantId: approvedApplicant.applicantId,
        applicantVersion: 5,
        applicantStatus: 'VisitPurposeRecorded',
        purposeCategory: 'migraine',
        purposeDisplayLabel: 'Headache or known migraine pattern',
        recordedAt: '2026-08-27T14:05:00Z',
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
        limitations: ['No complaint-specific clinical protocol ran.'],
      })

    render(<MemoryRouter><ProspectivePatientTelehealthEntry /></MemoryRouter>)
    const purpose = await screen.findByRole('group', { name: 'What is the synthetic visit about?' })
    expect(within(purpose).getByText(/not a diagnosis, complaint-specific clinical triage/i)).toBeInTheDocument()
    fireEvent.click(within(purpose).getByLabelText('Headache or known migraine pattern'))
    fireEvent.click(screen.getByLabelText(/visit-purpose selection is fictional synthetic demonstration data/i))
    fireEvent.click(screen.getByRole('button', { name: 'Record synthetic visit purpose' }))

    const failure = await screen.findByRole('alert')
    expect(failure).toHaveTextContent('Result unknown; retry unchanged.')
    await waitFor(() => expect(failure).toHaveFocus())
    expect(within(purpose).getByLabelText('Headache or known migraine pattern')).toBeChecked()
    fireEvent.click(screen.getByRole('button', { name: 'Record synthetic visit purpose' }))

    await waitFor(() => expect(recordProspectiveVisitPurpose).toHaveBeenCalledTimes(2))
    expect(vi.mocked(recordProspectiveVisitPurpose).mock.calls[0][3]).toBe('40000000-0000-4000-8000-000000000004')
    expect(vi.mocked(recordProspectiveVisitPurpose).mock.calls[1][3]).toBe('40000000-0000-4000-8000-000000000004')
    expect(vi.mocked(recordProspectiveVisitPurpose).mock.calls[0][2]).toEqual({
      expectedVersion: 4,
      purposeCategory: 'migraine',
      syntheticDataConfirmed: true,
    })
    const result = await screen.findByRole('status')
    expect(result).toHaveTextContent('Synthetic visit purpose recorded')
    expect(result).toHaveTextContent('No complaint-specific clinical protocol ran')
    await waitFor(() => expect(result).toHaveFocus())

    const stored = `${sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}${localStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}`
    expect(stored).not.toMatch(/migraine|sleep|purpose/i)
  })

  it('distinguishes practice fixtures from individual coverage and retains one precheck retry identity', async () => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('60000000-0000-4000-8000-000000000006')
    vi.mocked(getProspectiveApplicant).mockResolvedValue({
      ...approvedApplicant,
      status: 'VisitPurposeRecorded',
      version: 5,
      nextAction: 'Choose one fictional practice-plan fixture.',
    })
    vi.mocked(getProspectivePracticeNetworkOptions).mockResolvedValue({
      applicantId: approvedApplicant.applicantId,
      applicantVersion: 5,
      applicantStatus: 'VisitPurposeRecorded',
      adapterMode: 'NON_PRODUCTION',
      catalogKey: 'avenchart-synthetic-prospective-practice-network-2026-08',
      catalogVersion: 1,
      catalogEffectiveFrom: '2026-08-27T00:00:00Z',
      catalogEffectiveThrough: '2026-10-31T23:59:59Z',
      plans: [
        { planKey: 'harbor-mutual-hd', payerDisplayName: 'Harbor Mutual', productDisplayName: 'High Deductible', practiceNetworkStatus: 'PracticeNetworkConfirmedFixture', meaning: 'The synthetic fixture says the practice participates; the member and physician are not checked.' },
        { planKey: 'blue-valley-standard', payerDisplayName: 'Blue Valley Health', productDisplayName: 'Standard', practiceNetworkStatus: 'NetworkUnknown', meaning: 'The synthetic fixture has no authoritative result.' },
        { planKey: 'pine-state-choice', payerDisplayName: 'Pine State Choice', productDisplayName: 'Choice', practiceNetworkStatus: 'PracticeOutOfNetworkFixture', meaning: 'The synthetic fixture says the practice does not participate.' },
      ],
      memberEligibilityChecked: false,
      memberBenefitsChecked: false,
      renderingPhysicianNetworkChecked: false,
      coverageVerified: false,
      exactNetworkConfirmed: false,
      direction: 'Choose one fictional plan.',
      limitations: ['No payer was contacted.'],
    })
    vi.mocked(recordProspectivePracticeNetworkPrecheck)
      .mockRejectedValueOnce(new Error('Result unknown; retry unchanged.'))
      .mockResolvedValue({
        precheckId: '70000000-0000-4000-8000-000000000007',
        applicantId: approvedApplicant.applicantId,
        applicantVersion: 6,
        applicantStatus: 'PracticeNetworkPrecheckRecorded',
        currentLocationStateCode: 'GA',
        purposeCategory: 'migraine',
        planKey: 'harbor-mutual-hd',
        payerDisplayName: 'Harbor Mutual',
        productDisplayName: 'High Deductible',
        practiceNetworkStatus: 'PracticeNetworkConfirmedFixture',
        adapterMode: 'NON_PRODUCTION',
        catalogKey: 'avenchart-synthetic-prospective-practice-network-2026-08',
        catalogVersion: 1,
        catalogEffectiveFrom: '2026-08-27T00:00:00Z',
        catalogEffectiveThrough: '2026-10-31T23:59:59Z',
        recordedAt: '2026-08-27T14:10:00Z',
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
        direction: 'Individual eligibility and exact network remain required.',
        limitations: ['No member or physician was checked.'],
      })

    render(<MemoryRouter><ProspectivePatientTelehealthEntry /></MemoryRouter>)
    const plans = await screen.findByRole('group', { name: 'Choose one fictional plan' })
    expect(within(plans).getByText(/do not check you, your benefits, an eventual physician/i)).toBeInTheDocument()
    expect(screen.getByText(/No result on this screen is an insurance eligibility/i)).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: 'Call 911' }).length).toBeGreaterThan(0)
    fireEvent.click(within(plans).getByLabelText(/Harbor Mutual — High Deductible/i))
    fireEvent.click(screen.getByLabelText(/understand this is not an insurance verification/i))
    fireEvent.click(screen.getByRole('button', { name: 'Record synthetic practice precheck' }))

    const failure = await screen.findByRole('alert')
    expect(failure).toHaveTextContent('Result unknown; retry unchanged.')
    await waitFor(() => expect(failure).toHaveFocus())
    expect(within(plans).getByLabelText(/Harbor Mutual — High Deductible/i)).toBeChecked()
    fireEvent.click(screen.getByRole('button', { name: 'Record synthetic practice precheck' }))

    await waitFor(() => expect(recordProspectivePracticeNetworkPrecheck).toHaveBeenCalledTimes(2))
    expect(vi.mocked(recordProspectivePracticeNetworkPrecheck).mock.calls[0][3]).toBe('60000000-0000-4000-8000-000000000006')
    expect(vi.mocked(recordProspectivePracticeNetworkPrecheck).mock.calls[1][3]).toBe('60000000-0000-4000-8000-000000000006')
    expect(vi.mocked(recordProspectivePracticeNetworkPrecheck).mock.calls[0][2]).toEqual({
      expectedVersion: 5,
      planKey: 'harbor-mutual-hd',
      syntheticDataConfirmed: true,
    })
    const result = await screen.findByRole('heading', { name: 'Synthetic practice-level precheck recorded' })
    expect(result.parentElement).toHaveTextContent('not individual coverage or exact network')
    expect(result.parentElement).toHaveTextContent('No member eligibility, benefits, rendering-physician participation')
    await waitFor(() => expect(result.parentElement).toHaveFocus())

    const stored = `${sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}${localStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}`
    expect(stored).not.toMatch(/harbor|blue-valley|pine-state|planKey|network/i)
  })

  it('protects a conditional synthetic member-detail receipt with stable retry and no browser persistence', async () => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('80000000-0000-4000-8000-000000000008')
    vi.mocked(getProspectiveApplicant).mockResolvedValue({
      ...approvedApplicant,
      status: 'PracticeNetworkPrecheckRecorded',
      version: 6,
      nextAction: 'Confirm minimum fictional member details.',
    })
    vi.mocked(recordProspectiveMemberInsuranceDetails)
      .mockRejectedValueOnce(new Error('Result unknown; retry unchanged.'))
      .mockResolvedValue({
        detailsId: '90000000-0000-4000-8000-000000000009',
        applicantId: approvedApplicant.applicantId,
        applicantVersion: 7,
        applicantStatus: 'MemberInsuranceDetailsRecorded',
        currentLocationStateCode: 'GA',
        purposeCategory: 'migraine',
        planKey: 'harbor-mutual-hd',
        payerDisplayName: 'Harbor Mutual',
        productDisplayName: 'High Deductible',
        practiceNetworkStatus: 'PracticeNetworkConfirmedFixture',
        memberIdMask: '••••1001',
        groupNumberMask: '••••P-01',
        subscriberRelationship: 'Spouse',
        coveragePriority: 'Primary',
        protectionScheme: 'ASP.NET_CORE_DATA_PROTECTION',
        protectionVersion: 1,
        recordedAt: '2026-08-27T14:15:00Z',
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
        direction: 'Eligibility, benefits, and exact network verification remain required.',
        limitations: ['Raw normalized values are protected and are not returned.'],
      })

    render(<MemoryRouter><ProspectivePatientTelehealthEntry /></MemoryRouter>)
    await screen.findByRole('heading', { name: 'Synthetic practice-level precheck recorded' })
    expect(screen.getByText(/enter only fictional identifiers beginning with/i)).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: 'Call 911' }).length).toBeGreaterThan(0)

    fireEvent.change(screen.getByLabelText('Fictional member ID'), { target: { value: 'SYN-HM-1001' } })
    fireEvent.change(screen.getByLabelText(/Fictional group number/), { target: { value: 'SYN-GROUP-01' } })
    fireEvent.change(screen.getByLabelText('Subscriber relationship'), { target: { value: 'Spouse' } })
    expect(screen.getByRole('group', { name: 'Fictional subscriber identity' })).toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('Subscriber first name'), { target: { value: 'Morgan' } })
    fireEvent.change(screen.getByLabelText('Subscriber last name'), { target: { value: 'Example' } })
    fireEvent.change(screen.getByLabelText('Subscriber date of birth'), { target: { value: '1980-02-03' } })
    fireEvent.click(screen.getByLabelText(/reviewed this minimum fictional member/i))
    fireEvent.click(screen.getByLabelText(/every insurance and subscriber value is fictional/i))
    fireEvent.click(screen.getByRole('button', { name: 'Record protected synthetic receipt' }))

    const failure = await screen.findByRole('alert')
    expect(failure).toHaveTextContent('Result unknown; retry unchanged.')
    await waitFor(() => expect(failure).toHaveFocus())
    expect(screen.getByLabelText('Fictional member ID')).toHaveValue('SYN-HM-1001')
    fireEvent.click(screen.getByRole('button', { name: 'Record protected synthetic receipt' }))

    await waitFor(() => expect(recordProspectiveMemberInsuranceDetails).toHaveBeenCalledTimes(2))
    expect(vi.mocked(recordProspectiveMemberInsuranceDetails).mock.calls[0][3]).toBe('80000000-0000-4000-8000-000000000008')
    expect(vi.mocked(recordProspectiveMemberInsuranceDetails).mock.calls[1][3]).toBe('80000000-0000-4000-8000-000000000008')
    expect(vi.mocked(recordProspectiveMemberInsuranceDetails).mock.calls[0][2]).toEqual({
      expectedVersion: 6,
      memberId: 'SYN-HM-1001',
      groupNumber: 'SYN-GROUP-01',
      subscriberRelationship: 'Spouse',
      subscriberFirstName: 'Morgan',
      subscriberLastName: 'Example',
      subscriberDateOfBirth: '1980-02-03',
      detailsConfirmed: true,
      syntheticDataConfirmed: true,
    })

    const result = await screen.findByRole('heading', { name: 'Protected synthetic member-details receipt recorded' })
    expect(result.parentElement).toHaveTextContent('••••1001')
    expect(result.parentElement).toHaveTextContent('••••P-01')
    expect(result.parentElement).not.toHaveTextContent('SYN-HM-1001')
    expect(result.parentElement).not.toHaveTextContent('Morgan')
    await waitFor(() => expect(result.parentElement).toHaveFocus())

    const stored = `${sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}${localStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}`
    expect(stored).not.toMatch(/SYN-HM|SYN-GROUP|Morgan|Example|1980-02-03|subscriber|memberId/i)
  })

  it('records one normalized synthetic eligibility result with stable retry and separated network meaning', async () => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('a0000000-0000-4000-8000-00000000000a')
    vi.mocked(getProspectiveApplicant).mockResolvedValue({
      ...approvedApplicant,
      status: 'MemberInsuranceDetailsRecorded',
      version: 7,
      nextAction: 'Run the bounded synthetic eligibility check.',
    })
    vi.mocked(recordProspectiveEligibility)
      .mockRejectedValueOnce(new Error('Result unknown; retry unchanged.'))
      .mockResolvedValue({
        eligibilityResultId: 'b0000000-0000-4000-8000-00000000000b',
        applicantId: approvedApplicant.applicantId,
        applicantVersion: 8,
        applicantStatus: 'SyntheticEligibilityRecorded',
        currentLocationStateCode: 'GA',
        purposeCategory: 'migraine',
        planKey: 'harbor-mutual-hd',
        payerDisplayName: 'Harbor Mutual',
        productDisplayName: 'High Deductible',
        practiceNetworkStatus: 'PracticeNetworkConfirmedFixture',
        memberIdMask: '••••1001',
        groupNumberMask: '••••P-01',
        subscriberRelationship: 'Self',
        coveragePriority: 'Primary',
        dateOfService: '2026-08-27',
        serviceCategory: 'ProfessionalTelehealthConsultation',
        adapterMode: 'NON_PRODUCTION',
        compatibilityTarget: 'ASC_X12N_270_271_005010X279A1',
        datasetKey: 'avenchart-synthetic-prospective-eligibility-2026-08',
        datasetVersion: 1,
        datasetEffectiveFrom: '2026-08-27T00:00:00Z',
        datasetEffectiveThrough: '2026-10-31T23:59:59Z',
        inquiryTraceToken: 'c0000000-0000-4000-8000-00000000000c',
        responseTraceToken: 'd0000000-0000-4000-8000-00000000000d',
        transportOutcome: 'SimulatedAccepted',
        memberMatchStatus: 'Matched',
        eligibilityStatus: 'Active',
        benefitInformationStatus: 'Reported',
        businessOutcome: 'EligibleBenefitsReported',
        memberMatched: true,
        memberEligibilityChecked: true,
        memberBenefitsChecked: true,
        checkedAt: '2026-08-27T14:20:00Z',
        expiresAt: '2026-08-27T14:35:00Z',
        recordedAt: '2026-08-27T14:20:01Z',
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
        direction: 'Active synthetic eligibility is not exact network participation or a payment guarantee.',
        limitations: ['No raw X12 transaction or payer call was created.'],
      })

    render(<MemoryRouter><ProspectivePatientTelehealthEntry /></MemoryRouter>)
    await screen.findByRole('heading', { name: 'Protected synthetic member-details receipt recorded' })
    expect(screen.getByText(/will not create or store an X12 transaction/i)).toBeInTheDocument()
    expect(screen.getByText(/active result is not exact practice-and-physician network/i)).toBeInTheDocument()
    fireEvent.click(screen.getByLabelText(/check is synthetic, NON_PRODUCTION/i))
    fireEvent.click(screen.getByRole('button', { name: 'Record synthetic eligibility result' }))

    const failure = await screen.findByRole('alert')
    expect(failure).toHaveTextContent('Result unknown; retry unchanged.')
    await waitFor(() => expect(failure).toHaveFocus())
    expect(screen.getByLabelText(/check is synthetic, NON_PRODUCTION/i)).toBeChecked()
    fireEvent.click(screen.getByRole('button', { name: 'Record synthetic eligibility result' }))

    await waitFor(() => expect(recordProspectiveEligibility).toHaveBeenCalledTimes(2))
    expect(vi.mocked(recordProspectiveEligibility).mock.calls[0][3]).toBe('a0000000-0000-4000-8000-00000000000a')
    expect(vi.mocked(recordProspectiveEligibility).mock.calls[1][3]).toBe('a0000000-0000-4000-8000-00000000000a')
    expect(vi.mocked(recordProspectiveEligibility).mock.calls[0][2]).toEqual({
      expectedVersion: 7,
      syntheticDataConfirmed: true,
    })

    const result = await screen.findByRole('heading', { name: 'Synthetic eligibility result recorded' })
    expect(result.parentElement).toHaveTextContent('Active')
    expect(result.parentElement).toHaveTextContent('Reported')
    expect(result.parentElement).toHaveTextContent('PracticeNetworkConfirmedFixture')
    expect(result.parentElement).toHaveTextContent(/never a guarantee of coverage or payment/i)
    expect(result.parentElement).not.toHaveTextContent('SYN-HM-1001')
    await waitFor(() => expect(result.parentElement).toHaveFocus())
    expect(screen.getAllByRole('link', { name: 'Call 911' }).length).toBeGreaterThan(0)

    const stored = `${sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}${localStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}`
    expect(stored).not.toMatch(/eligibility|benefit|Active|Reported|trace|payer|network|SYN-HM/i)
  })

  it('records one synthetic practice-network result with stable retry and keeps physician participation false', async () => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('e0000000-0000-4000-8000-00000000000e')
    vi.mocked(getProspectiveApplicant).mockResolvedValue({
      ...approvedApplicant,
      status: 'SyntheticEligibilityRecorded',
      version: 8,
      nextAction: 'Run the bounded synthetic practice-network check.',
    })
    vi.mocked(recordProspectivePracticeNetwork)
      .mockRejectedValueOnce(new Error('Directory result unknown; retry unchanged.'))
      .mockResolvedValue({
        networkDeterminationId: 'f0000000-0000-4000-8000-00000000000f',
        applicantId: approvedApplicant.applicantId,
        applicantVersion: 9,
        applicantStatus: 'SyntheticPracticeNetworkRecorded',
        currentLocationStateCode: 'GA',
        purposeCategory: 'migraine',
        planKey: 'harbor-mutual-hd',
        payerDisplayName: 'Harbor Mutual',
        productDisplayName: 'High Deductible',
        practiceDisplayName: 'AvenChart Synthetic Practice',
        dateOfService: '2026-08-27',
        serviceCategory: 'ProfessionalTelehealthConsultation',
        eligibilityStatus: 'Active',
        benefitInformationStatus: 'Reported',
        eligibilityBusinessOutcome: 'EligibleBenefitsReported',
        eligibilityCheckedAt: '2026-08-27T14:20:00Z',
        eligibilityExpiresAt: '2026-08-27T14:35:00Z',
        adapterMode: 'NON_PRODUCTION',
        compatibilityTarget: 'HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0',
        datasetKey: 'avenchart-synthetic-practice-network-directory-2026-08',
        datasetVersion: 1,
        datasetEffectiveFrom: '2026-08-27T00:00:00Z',
        datasetEffectiveThrough: '2026-10-31T23:59:59Z',
        sourceLastUpdatedAt: '2026-08-27T00:00:00Z',
        requestTraceToken: '10000000-0000-4000-8000-000000000010',
        responseTraceToken: '11000000-0000-4000-8000-000000000011',
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
        checkedAt: '2026-08-27T14:21:00Z',
        expiresAt: '2026-08-27T14:36:00Z',
        recordedAt: '2026-08-27T14:21:01Z',
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
        direction: 'Practice directory evidence is recorded; rendering physician participation remains required.',
        limitations: ['No FHIR resource, directory call, physician check, coverage, payment, request, queue, or care action was created.'],
      })
    vi.mocked(recordProspectiveIdentityProofing)
      .mockRejectedValueOnce(new Error('Synthetic proofing process interrupted; retry unchanged.'))
      .mockResolvedValue({
        identityProofingResultId: '12000000-0000-4000-8000-000000000012',
        applicantId: approvedApplicant.applicantId,
        applicantVersion: 10,
        applicantStatus: 'SyntheticIdentityProofingRecorded',
        currentLocationStateCode: 'GA',
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
        requestTraceToken: '13000000-0000-4000-8000-000000000013',
        responseTraceToken: '14000000-0000-4000-8000-000000000014',
        proofingMethod: 'SYNTHETIC_REMOTE_UNATTENDED_NON_BIOMETRIC',
        transportOutcome: 'SimulatedCompleted',
        evidenceCollectionStatus: 'FixtureReferenceAccepted',
        evidenceValidationStatus: 'ValidatedFixture',
        attributeValidationStatus: 'ValidatedFixture',
        applicantVerificationStatus: 'VerifiedFixture',
        fraudCheckStatus: 'NoIndicatorFixture',
        businessOutcome: 'SyntheticProofingPassed',
        proofingSessionReference: 'syn-proof-session-15000000000040008000000000000015',
        evidencePackageReference: 'syn-evidence-11111111222243338444555555555555',
        checkedAt: '2026-08-27T14:22:00Z',
        expiresAt: '2026-08-27T14:37:00Z',
        recordedAt: '2026-08-27T14:22:01Z',
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
      })

    render(<MemoryRouter><ProspectivePatientTelehealthEntry /></MemoryRouter>)
    await screen.findByRole('heading', { name: 'Synthetic eligibility result recorded' })
    expect(screen.getByText(/No member information is sent to this adapter/i)).toBeInTheDocument()
    expect(screen.getByText(/does not check the eventual rendering physician/i)).toBeInTheDocument()
    fireEvent.click(screen.getByLabelText(/practice-network check is synthetic, NON_PRODUCTION/i))
    fireEvent.click(screen.getByRole('button', { name: 'Record synthetic practice-network result' }))

    const failure = await screen.findByRole('alert')
    expect(failure).toHaveTextContent('Directory result unknown; retry unchanged.')
    await waitFor(() => expect(failure).toHaveFocus())
    expect(screen.getByLabelText(/practice-network check is synthetic, NON_PRODUCTION/i)).toBeChecked()
    fireEvent.click(screen.getByRole('button', { name: 'Record synthetic practice-network result' }))

    await waitFor(() => expect(recordProspectivePracticeNetwork).toHaveBeenCalledTimes(2))
    expect(vi.mocked(recordProspectivePracticeNetwork).mock.calls[0][3]).toBe('e0000000-0000-4000-8000-00000000000e')
    expect(vi.mocked(recordProspectivePracticeNetwork).mock.calls[1][3]).toBe('e0000000-0000-4000-8000-00000000000e')
    expect(vi.mocked(recordProspectivePracticeNetwork).mock.calls[0][2]).toEqual({
      expectedVersion: 8,
      syntheticDataConfirmed: true,
    })

    const result = await screen.findByRole('heading', { name: 'Synthetic practice-network result recorded' })
    expect(result.parentElement).toHaveTextContent('InNetwork')
    expect(result.parentElement).toHaveTextContent('Included')
    expect(result.parentElement).toHaveTextContent('Accepting')
    expect(result.parentElement).toHaveTextContent(/Rendering-physician participation is still unchecked/i)
    expect(result.parentElement).not.toHaveTextContent('syn-network-harbor-mutual-hd')
    await waitFor(() => expect(result.parentElement).toHaveFocus())
    expect(screen.getAllByRole('link', { name: 'Call 911' }).length).toBeGreaterThan(0)

    expect(screen.getByText(/does not collect an ID document/i)).toBeInTheDocument()
    fireEvent.click(screen.getByLabelText(/acknowledge the synthetic privacy notice/i))
    fireEvent.click(screen.getByLabelText(/NON_PRODUCTION process exercise/i))
    fireEvent.click(screen.getByRole('button', { name: 'Record synthetic identity-proofing fixture' }))

    const identityFailure = await screen.findByRole('alert')
    expect(identityFailure).toHaveTextContent('Synthetic proofing process interrupted; retry unchanged.')
    await waitFor(() => expect(identityFailure).toHaveFocus())
    expect(screen.getByLabelText(/acknowledge the synthetic privacy notice/i)).toBeChecked()
    expect(screen.getByLabelText(/NON_PRODUCTION process exercise/i)).toBeChecked()
    fireEvent.click(screen.getByRole('button', { name: 'Record synthetic identity-proofing fixture' }))

    await waitFor(() => expect(recordProspectiveIdentityProofing).toHaveBeenCalledTimes(2))
    expect(vi.mocked(recordProspectiveIdentityProofing).mock.calls[0][3]).toBe('e0000000-0000-4000-8000-00000000000e')
    expect(vi.mocked(recordProspectiveIdentityProofing).mock.calls[1][3]).toBe('e0000000-0000-4000-8000-00000000000e')
    expect(vi.mocked(recordProspectiveIdentityProofing).mock.calls[0][2]).toEqual({
      expectedVersion: 9,
      privacyNoticeAcknowledged: true,
      syntheticDataConfirmed: true,
    })

    const identityResult = await screen.findByRole('heading', { name: 'Synthetic identity-proofing process recorded' })
    expect(identityResult.parentElement).toHaveTextContent('FixtureReferenceAccepted')
    expect(identityResult.parentElement).toHaveTextContent('ValidatedFixture')
    expect(identityResult.parentElement).toHaveTextContent('VerifiedFixture')
    expect(identityResult.parentElement).toHaveTextContent('Identity assurance achievedNone')
    expect(identityResult.parentElement).toHaveTextContent(/No real identity was proofed/i)
    expect(identityResult.parentElement).not.toHaveTextContent('syn-proof-session')
    expect(identityResult.parentElement).not.toHaveTextContent('syn-evidence-')
    await waitFor(() => expect(identityResult.parentElement).toHaveFocus())

    const stored = `${sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}${localStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}`
    expect(stored).not.toMatch(/InNetwork|Included|Accepting|PracticeInNetwork|ValidatedFixture|VerifiedFixture|SyntheticProofing|trace|directory|payer|networkReference|proof-session|evidence-/i)
  })

  it('requires every state-notice acknowledgment, preserves retry identity, and does not imply legal consent', async () => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('f0000000-0000-4000-8000-00000000000f')
    vi.mocked(getProspectiveApplicant).mockResolvedValue({
      ...approvedApplicant,
      status: 'SyntheticPatientPromoted',
      version: 12,
      canonicalPatientCreated: true,
      nextAction: 'Review the Georgia telehealth notice.',
    })
    const notice = {
      applicantId: approvedApplicant.applicantId,
      applicantVersion: 12,
      applicantStatus: 'SyntheticPatientPromoted',
      noticeKey: 'GA_TELEHEALTH_NOTICE_V1',
      noticeVersion: 1,
      currentLocationStateCode: 'GA',
      title: 'Georgia synthetic telehealth notice',
      summary: 'Telehealth uses remote technology and does not replace emergency or required in-person care.',
      sourceTitle: 'Georgia Composite Medical Board Rule 360-3-.07',
      sourceUrl: 'https://rules.sos.ga.gov/gac/360-3-.07',
      disclosures: ['The clinician must confirm identity, location, credentials, and emergency instructions before care.'],
      deferredRequirements: ['A licensed clinician must complete and document the legally required consent process before care.'],
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
    } satisfies TelehealthApplicantNotice
    const acknowledgedNotice = {
      ...notice,
      applicantVersion: 13,
      applicantStatus: 'SyntheticTelehealthNoticeAcknowledged' as const,
      acknowledged: true,
      acknowledgedAt: '2026-08-27T14:30:00Z',
    }
    vi.mocked(getApplicantTelehealthNotice)
      .mockResolvedValueOnce(notice)
      .mockResolvedValue(acknowledgedNotice)
    vi.mocked(acknowledgeApplicantTelehealthNotice)
      .mockRejectedValueOnce(new Error('Acknowledgment result unknown; retry unchanged.'))
      .mockResolvedValue(acknowledgedNotice)

    render(<MemoryRouter><ProspectivePatientTelehealthEntry /></MemoryRouter>)
    const heading = await screen.findByRole('heading', { name: 'Georgia synthetic telehealth notice' })
    expect(heading.parentElement).toHaveTextContent(/not final legal consent/i)
    expect(screen.getByRole('link', { name: /official source/i })).toHaveAttribute('href', notice.sourceUrl)
    const submit = screen.getByRole('button', { name: 'Acknowledge state-specific notice' })
    expect(submit).toBeDisabled()

    fireEvent.click(screen.getByLabelText(/currently located in GA/i))
    fireEvent.click(screen.getByLabelText(/remote technology and can have limits/i))
    fireEvent.click(screen.getByLabelText(/privacy and technology limitations/i))
    fireEvent.click(screen.getByLabelText(/not emergency care/i))
    fireEvent.click(screen.getByLabelText(/in-person evaluation may be available/i))
    fireEvent.click(screen.getByLabelText(/licensed clinician must later reconfirm/i))
    fireEvent.click(screen.getByLabelText(/synthetic NON_PRODUCTION demonstration/i))
    expect(submit).toBeEnabled()
    fireEvent.click(submit)

    const failure = await screen.findByRole('alert')
    expect(failure).toHaveTextContent('Acknowledgment result unknown; retry unchanged.')
    await waitFor(() => expect(failure).toHaveFocus())
    expect(screen.getByLabelText(/currently located in GA/i)).toBeChecked()
    fireEvent.click(screen.getByRole('button', { name: 'Acknowledge state-specific notice' }))

    await waitFor(() => expect(acknowledgeApplicantTelehealthNotice).toHaveBeenCalledTimes(2))
    expect(vi.mocked(acknowledgeApplicantTelehealthNotice).mock.calls[0][3]).toBe('f0000000-0000-4000-8000-00000000000f')
    expect(vi.mocked(acknowledgeApplicantTelehealthNotice).mock.calls[1][3]).toBe('f0000000-0000-4000-8000-00000000000f')
    expect(vi.mocked(acknowledgeApplicantTelehealthNotice).mock.calls[0][2]).toEqual({
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

    const result = await screen.findByRole('heading', { name: 'State-specific telehealth notice acknowledged' })
    expect(result.parentElement).toHaveTextContent('Final legal consentNot established')
    expect(result.parentElement).toHaveTextContent('Clinician consent documentationNot completed')
    expect(result.parentElement).toHaveTextContent(/no portal access, completed intake, practice acceptance, insurance record, telehealth request, queue entry, or care capability/i)
    await waitFor(() => expect(result.parentElement).toHaveFocus())

    const stored = `${sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}${localStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}`
    expect(stored).not.toMatch(/noticeKey|acknowledged|consent|currentLocation/i)
  })

  it('confirms the copied minimum registration details without editing the patient or enabling downstream care', async () => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('11000000-0000-4000-8000-000000000011')
    vi.mocked(getProspectiveApplicant).mockResolvedValue({
      ...approvedApplicant,
      status: 'SyntheticTelehealthNoticeAcknowledged',
      version: 13,
      canonicalPatientCreated: true,
      nextAction: 'Confirm the copied minimum registration details.',
    })
    vi.mocked(getApplicantTelehealthNotice).mockResolvedValue({
      applicantId: approvedApplicant.applicantId,
      applicantVersion: 13,
      applicantStatus: 'SyntheticTelehealthNoticeAcknowledged',
      noticeKey: 'GA_TELEHEALTH_NOTICE_V1',
      noticeVersion: 1,
      currentLocationStateCode: 'GA',
      title: 'Georgia synthetic telehealth notice',
      summary: 'Telehealth uses remote technology.',
      sourceTitle: 'Georgia Composite Medical Board Rule 360-3-.07',
      sourceUrl: 'https://rules.sos.ga.gov/gac/360-3-.07',
      disclosures: ['Remote care has limitations.'],
      deferredRequirements: ['A clinician must complete the legally required consent process before care.'],
      policyKey: 'SYNTHETIC_TELEHEALTH_NOTICE_ACKNOWLEDGMENT',
      policyVersion: 1,
      legalReviewStatus: 'PendingIndependentReview',
      acknowledged: true,
      acknowledgedAt: '2026-08-27T14:30:00Z',
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
      direction: 'Continue only to the minimum registration-details review.',
      limitations: ['No final legal consent or care authorization was established.'],
    } satisfies TelehealthApplicantNotice)
    const confirmedDetails = {
      ...registrationDetailsFixture,
      applicantVersion: 14,
      applicantStatus: 'SyntheticMinimumRegistrationDetailsConfirmed',
      confirmed: true,
      confirmedAt: '2026-08-27T14:35:00Z',
      direction: 'Minimum registration details are confirmed; all later intake gates remain closed.',
    } satisfies TelehealthApplicantRegistrationDetails
    vi.mocked(getApplicantRegistrationDetails)
      .mockResolvedValueOnce(registrationDetailsFixture)
      .mockResolvedValue(confirmedDetails)
    vi.mocked(confirmApplicantRegistrationDetails)
      .mockRejectedValueOnce(new Error('Confirmation result unknown; retry unchanged.'))
      .mockResolvedValue(confirmedDetails)

    render(<MemoryRouter><ProspectivePatientTelehealthEntry /></MemoryRouter>)

    const heading = await screen.findByRole('heading', { name: 'Confirm the minimum copied details' })
    expect(heading.parentElement).toHaveTextContent('Avery Notice')
    expect(heading.parentElement).toHaveTextContent('1990-03-14')
    expect(heading.parentElement).toHaveTextContent('a•••@example.test')
    expect(heading.parentElement).toHaveTextContent('(***) ***-0199')
    expect(heading.parentElement).toHaveTextContent('GA 30303')
    expect(heading.parentElement).toHaveTextContent(/cannot edit records or complete a correction/i)
    expect(heading.parentElement).not.toHaveTextContent('avery.notice@example.test')
    expect(heading.parentElement).not.toHaveTextContent('4045550199')
    expect(heading.parentElement).not.toHaveTextContent('10 Main Street')

    const submit = screen.getByRole('button', { name: 'Confirm minimum registration details' })
    expect(submit).toBeDisabled()
    fireEvent.click(screen.getByLabelText(/legal name and date of birth shown above/i))
    fireEvent.click(screen.getByLabelText(/masked verified email and callback phone/i))
    fireEvent.click(screen.getByLabelText(/residence state and postal code shown above/i))
    fireEvent.click(screen.getByLabelText(/do not need a correction/i))
    fireEvent.click(screen.getByLabelText(/synthetic NON_PRODUCTION demonstration/i))
    expect(submit).toBeEnabled()
    fireEvent.click(submit)

    const failure = await screen.findByRole('alert')
    expect(failure).toHaveTextContent('Confirmation result unknown; retry unchanged.')
    await waitFor(() => expect(failure).toHaveFocus())
    expect(screen.getByLabelText(/legal name and date of birth shown above/i)).toBeChecked()
    fireEvent.click(screen.getByRole('button', { name: 'Confirm minimum registration details' }))

    await waitFor(() => expect(confirmApplicantRegistrationDetails).toHaveBeenCalledTimes(2))
    expect(vi.mocked(confirmApplicantRegistrationDetails).mock.calls[0][3]).toBe('11000000-0000-4000-8000-000000000011')
    expect(vi.mocked(confirmApplicantRegistrationDetails).mock.calls[1][3]).toBe('11000000-0000-4000-8000-000000000011')
    expect(vi.mocked(confirmApplicantRegistrationDetails).mock.calls[0][2]).toEqual({
      expectedVersion: 13,
      detailsFingerprint: 'f'.repeat(64),
      legalNameAndBirthDateConfirmed: true,
      contactChannelsConfirmed: true,
      residenceRegionConfirmed: true,
      noCorrectionsNeededConfirmed: true,
      syntheticDataConfirmed: true,
    })

    const result = await screen.findByRole('heading', { name: 'Minimum registration details confirmed' })
    expect(result.parentElement).toHaveTextContent('Patient record changedNo')
    expect(result.parentElement).toHaveTextContent('Identity assurance establishedNo')
    expect(result.parentElement).toHaveTextContent('Canonical insurance createdNo')
    expect(result.parentElement).toHaveTextContent(/No portal, correction, completed intake, legal consent, practice acceptance, coverage, request, queue entry, appointment, encounter, or care capability was created/i)
    await waitFor(() => expect(result.parentElement).toHaveFocus())

    const stored = `${sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}${localStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}`
    expect(stored).not.toMatch(/Avery|Notice|1990-03-14|detailsFingerprint|30303|email|phone|patientId/i)
  })

  it('confirms the masked synthetic insurance handoff without claiming coverage or a rendering physician check', async () => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('12000000-0000-4000-8000-000000000012')
    vi.mocked(getProspectiveApplicant).mockResolvedValue({
      ...approvedApplicant,
      status: 'SyntheticMinimumRegistrationDetailsConfirmed',
      version: 14,
      canonicalPatientCreated: true,
      nextAction: 'Confirm the masked synthetic insurance handoff.',
    })
    const confirmedHandoff = {
      ...insuranceHandoffFixture,
      applicantVersion: 15,
      applicantStatus: 'SyntheticInsuranceDetailsConfirmed',
      insuranceDetailsConfirmed: true,
      confirmedAt: '2026-08-27T14:36:00Z',
      direction: 'The no-edit synthetic insurance-details handoff confirmation was recorded.',
    } satisfies TelehealthApplicantInsuranceHandoff
    vi.mocked(getApplicantInsuranceHandoff)
      .mockResolvedValueOnce(insuranceHandoffFixture)
      .mockResolvedValue(confirmedHandoff)
    vi.mocked(confirmApplicantInsuranceHandoff)
      .mockRejectedValueOnce(new Error('Insurance confirmation result unknown; retry unchanged.'))
      .mockResolvedValue(confirmedHandoff)

    render(<MemoryRouter><ProspectivePatientTelehealthEntry /></MemoryRouter>)

    const heading = await screen.findByRole('heading', { name: 'Confirm the masked synthetic insurance details' })
    expect(heading.parentElement).toHaveTextContent('Harbor Mutual')
    expect(heading.parentElement).toHaveTextContent('High Deductible')
    expect(heading.parentElement).toHaveTextContent('••••A123')
    expect(heading.parentElement).toHaveTextContent('••••G456')
    expect(heading.parentElement).toHaveTextContent('Rendering physician checkedNo')
    expect(heading.parentElement).toHaveTextContent('Coverage verifiedNo')
    expect(heading.parentElement).toHaveTextContent(/not guarantees of coverage, benefits, payment, cost, or rendering-physician participation/i)
    expect(heading.parentElement).not.toHaveTextContent('SYN-MEMBER-A123')
    expect(heading.parentElement).not.toHaveTextContent('SYN-GROUP-G456')

    const submit = screen.getByRole('button', { name: 'Confirm synthetic insurance details' })
    expect(submit).toBeDisabled()
    fireEvent.click(screen.getByLabelText(/payer and product shown above/i))
    fireEvent.click(screen.getByLabelText(/masked synthetic member ID and group number/i))
    fireEvent.click(screen.getByLabelText(/subscriber relationship and coverage priority/i))
    fireEvent.click(screen.getByLabelText(/eligibility and practice-level network fixtures/i))
    fireEvent.click(screen.getByLabelText(/does not create insurance coverage or request care/i))
    expect(submit).toBeEnabled()
    fireEvent.click(submit)

    const failure = await screen.findByRole('alert')
    expect(failure).toHaveTextContent('Insurance confirmation result unknown; retry unchanged.')
    await waitFor(() => expect(failure).toHaveFocus())
    expect(screen.getByLabelText(/payer and product shown above/i)).toBeChecked()
    fireEvent.click(screen.getByRole('button', { name: 'Confirm synthetic insurance details' }))

    await waitFor(() => expect(confirmApplicantInsuranceHandoff).toHaveBeenCalledTimes(2))
    expect(vi.mocked(confirmApplicantInsuranceHandoff).mock.calls[0][3]).toBe('12000000-0000-4000-8000-000000000012')
    expect(vi.mocked(confirmApplicantInsuranceHandoff).mock.calls[1][3]).toBe('12000000-0000-4000-8000-000000000012')
    expect(vi.mocked(confirmApplicantInsuranceHandoff).mock.calls[0][2]).toEqual({
      expectedVersion: 14,
      insuranceSnapshotFingerprint: 'e'.repeat(64),
      payerAndProductConfirmed: true,
      maskedMemberDetailsConfirmed: true,
      subscriberRelationshipConfirmed: true,
      evidenceLimitationsAcknowledged: true,
      syntheticDataConfirmed: true,
    })

    const result = await screen.findByRole('heading', { name: 'Synthetic insurance details confirmed' })
    expect(result.parentElement).toHaveTextContent('Rendering physician checkedNo')
    expect(result.parentElement).toHaveTextContent('Coverage verifiedNo')
    expect(result.parentElement).toHaveTextContent('Exact network confirmedNo')
    expect(result.parentElement).toHaveTextContent('Canonical coverage createdNo')
    expect(result.parentElement).toHaveTextContent(/No patient change, portal, completed intake, legal consent, practice acceptance, financial record, request, queue entry, appointment, encounter, claim, or care capability was created/i)
    await waitFor(() => expect(result.parentElement).toHaveFocus())

    const stored = `${sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}${localStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}`
    expect(stored).not.toMatch(/Harbor|A123|G456|insuranceSnapshotFingerprint|memberId|groupNumber|patientId/i)
  })

  it('records bounded communication and access preferences with a stable retry and no arranged services', async () => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('13000000-0000-4000-8000-000000000013')
    vi.mocked(getProspectiveApplicant).mockResolvedValue({
      ...approvedApplicant,
      status: 'SyntheticInsuranceDetailsConfirmed',
      version: 15,
      canonicalPatientCreated: true,
      nextAction: 'Confirm communication and access readiness.',
    })
    const recordedReadiness = {
      ...communicationAccessReadinessFixture,
      applicantVersion: 16,
      applicantStatus: 'SyntheticCommunicationAccessReadinessRecorded',
      preferredSpokenLanguage: 'Spanish',
      interpreterRequested: true,
      accessibilitySupportRequested: true,
      readinessRecorded: true,
      recordedAt: '2026-08-28T01:15:00Z',
      safeAndPrivateToCommunicateConfirmed: true,
      direction: 'The synthetic communication/access-readiness receipt was recorded.',
    } satisfies TelehealthApplicantCommunicationAccessReadiness
    vi.mocked(getApplicantCommunicationAccessReadiness)
      .mockResolvedValueOnce(communicationAccessReadinessFixture)
      .mockResolvedValue(recordedReadiness)
    vi.mocked(recordApplicantCommunicationAccessReadiness)
      .mockRejectedValueOnce(new Error('Communication readiness result unknown; retry unchanged.'))
      .mockResolvedValue(recordedReadiness)

    render(<MemoryRouter><ProspectivePatientTelehealthEntry /></MemoryRouter>)

    const heading = await screen.findByRole('heading', { name: 'Confirm how this synthetic visit could communicate' })
    expect(heading.parentElement).toHaveTextContent('Current locationGA')
    expect(heading.parentElement).toHaveTextContent('Callback number(***) ***-0199')
    expect(heading.parentElement).toHaveTextContent('Interpreter assignedNo')
    expect(heading.parentElement).toHaveTextContent('Accessibility accommodation arrangedNo')
    expect(heading.parentElement).toHaveTextContent('Technology readiness completedNo')
    expect(heading.parentElement).not.toHaveTextContent('4045550199')

    const submit = screen.getByRole('button', { name: 'Record communication readiness' })
    expect(submit).toBeDisabled()
    fireEvent.change(screen.getByLabelText('Preferred spoken language'), { target: { value: 'Spanish' } })
    fireEvent.click(screen.getByLabelText(/would request an interpreter/i))
    fireEvent.click(screen.getByLabelText(/would request accessibility support/i))
    fireEvent.click(screen.getByLabelText(/currently in GA/i))
    fireEvent.click(screen.getByLabelText(/callback number I expect/i))
    fireEvent.click(screen.getByLabelText(/communicate safely and privately/i))
    fireEvent.click(screen.getByLabelText(/real visit needs a disconnection plan/i))
    fireEvent.click(screen.getByLabelText(/synthetic NON_PRODUCTION receipt/i))
    expect(submit).toBeEnabled()
    fireEvent.click(submit)

    const failure = await screen.findByRole('alert')
    expect(failure).toHaveTextContent('Communication readiness result unknown; retry unchanged.')
    await waitFor(() => expect(failure).toHaveFocus())
    expect(screen.getByLabelText(/communicate safely and privately/i)).toBeChecked()
    fireEvent.click(screen.getByRole('button', { name: 'Record communication readiness' }))

    await waitFor(() => expect(recordApplicantCommunicationAccessReadiness).toHaveBeenCalledTimes(2))
    expect(vi.mocked(recordApplicantCommunicationAccessReadiness).mock.calls[0][3]).toBe('13000000-0000-4000-8000-000000000013')
    expect(vi.mocked(recordApplicantCommunicationAccessReadiness).mock.calls[1][3]).toBe('13000000-0000-4000-8000-000000000013')
    expect(vi.mocked(recordApplicantCommunicationAccessReadiness).mock.calls[0][2]).toEqual({
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

    const result = await screen.findByRole('heading', { name: 'Communication and access readiness recorded' })
    expect(result.parentElement).toHaveTextContent('Preferred spoken languageSpanish')
    expect(result.parentElement).toHaveTextContent('Interpreter requestedYes — preference only')
    expect(result.parentElement).toHaveTextContent('Interpreter assignedNo')
    expect(result.parentElement).toHaveTextContent('Accessibility support requestedYes — preference only')
    expect(result.parentElement).toHaveTextContent('Accommodation arrangedNo')
    expect(result.parentElement).toHaveTextContent('Technology readiness completedNo')
    expect(result.parentElement).toHaveTextContent(/No interpreter, accommodation, communication session, support request, patient change, completed intake, legal consent, practice acceptance, request, queue entry, appointment, encounter, or care capability was created/i)
    await waitFor(() => expect(result.parentElement).toHaveFocus())

    const stored = `${sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}${localStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}`
    expect(stored).not.toMatch(/Spanish|interpreter|accessibility|contextSnapshotFingerprint|4045550199|patientId/i)
  })

  it('records only a passing coarse device result with acknowledgments and a stable retry', async () => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('14000000-0000-4000-8000-000000000014')
    vi.mocked(getProspectiveApplicant).mockResolvedValue({
      ...approvedApplicant,
      status: 'SyntheticCommunicationAccessReadinessRecorded',
      version: 16,
      canonicalPatientCreated: true,
      nextAction: 'Run the local device-preparation check.',
    })
    vi.mocked(runTelehealthDevicePreflight).mockResolvedValue({
      status: 'passed',
      evidence: {
        browserSupported: true,
        cameraAvailable: true,
        microphoneAvailable: true,
        speakerAvailable: true,
        networkQuality: 'good',
        syntheticDataConfirmed: true,
      },
    })
    const recordedPreparation = {
      ...devicePreparationFixture,
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
      direction: 'The bounded synthetic device-preparation receipt was recorded.',
    } satisfies TelehealthApplicantDevicePreparation
    vi.mocked(getApplicantDevicePreparation)
      .mockResolvedValueOnce(devicePreparationFixture)
      .mockResolvedValue(recordedPreparation)
    vi.mocked(recordApplicantDevicePreparation)
      .mockRejectedValueOnce(new Error('Device preparation result unknown; retry unchanged.'))
      .mockResolvedValue(recordedPreparation)

    render(<MemoryRouter><ProspectivePatientTelehealthEntry /></MemoryRouter>)

    const heading = await screen.findByRole('heading', { name: 'Check this browser, camera, microphone, speaker, and connection' })
    expect(heading.parentElement).toHaveTextContent(/Every temporary test track is stopped immediately/i)
    expect(heading.parentElement).toHaveTextContent(/No image, audio, recording, device name, device ID, browser details, IP address, or connection diagnostic is sent or stored/i)

    const submit = screen.getByRole('button', { name: 'Record device preparation' })
    expect(submit).toBeDisabled()
    fireEvent.click(screen.getByRole('button', { name: 'Run local device check' }))
    expect(await screen.findByRole('heading', { name: 'Local check complete' })).toBeInTheDocument()
    expect(screen.getByText('Stopped')).toBeInTheDocument()
    fireEvent.click(screen.getByLabelText(/client-reported, point-in-time browser result/i))
    fireEvent.click(screen.getByLabelText(/does not certify technology readiness/i))
    fireEvent.click(screen.getByLabelText(/checked again before any consultation/i))
    expect(submit).toBeEnabled()
    fireEvent.click(submit)

    const failure = await screen.findByRole('alert')
    expect(failure).toHaveTextContent('Device preparation result unknown; retry unchanged.')
    await waitFor(() => expect(failure).toHaveFocus())
    expect(screen.getByLabelText(/client-reported, point-in-time browser result/i)).toBeChecked()
    fireEvent.click(screen.getByRole('button', { name: 'Record device preparation' }))

    await waitFor(() => expect(recordApplicantDevicePreparation).toHaveBeenCalledTimes(2))
    expect(vi.mocked(recordApplicantDevicePreparation).mock.calls[0][3]).toBe('14000000-0000-4000-8000-000000000014')
    expect(vi.mocked(recordApplicantDevicePreparation).mock.calls[1][3]).toBe('14000000-0000-4000-8000-000000000014')
    expect(vi.mocked(recordApplicantDevicePreparation).mock.calls[0][2]).toEqual({
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

    const result = await screen.findByRole('heading', { name: 'Device preparation recorded' })
    expect(result.parentElement).toHaveTextContent('Supported browser reportedYes')
    expect(result.parentElement).toHaveTextContent('Technology readyNo')
    expect(result.parentElement).toHaveTextContent('Waiting room createdNo')
    expect(result.parentElement).toHaveTextContent('Media session createdNo')
    expect(result.parentElement).toHaveTextContent(/No technology-ready status, waiting room, media session, communication, support arrangement, patient change, completed intake, legal consent, practice acceptance, request, queue entry, appointment, encounter, or care capability was created/i)
    await waitFor(() => expect(result.parentElement).toHaveFocus())

    const stored = `${sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}${localStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}`
    expect(stored).not.toMatch(/device|camera|microphone|speaker|networkQuality|preparationSnapshotFingerprint|patientId/i)
  })

  it('records only three coarse patient-reported categories with server routing and a stable retry', async () => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('15000000-0000-4000-8000-000000000015')
    vi.mocked(getProspectiveApplicant).mockResolvedValue({
      ...approvedApplicant,
      status: 'SyntheticDevicePreparationRecorded',
      version: 17,
      canonicalPatientCreated: true,
      nextAction: 'Complete the bounded clinical-information inventory.',
    })
    vi.mocked(getApplicantDevicePreparation).mockResolvedValue({
      ...devicePreparationFixture,
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
    })
    const recordedInventory = {
      ...clinicalInformationInventoryFixture,
      applicantVersion: 18,
      applicantStatus: 'SyntheticClinicalInformationInventoryRecorded',
      inventoryRecorded: true,
      recordedAt: '2026-08-28T02:20:00Z',
      medicationsStatus: 'ItemsToReview',
      allergiesOrIntolerancesStatus: 'PatientReportsNone',
      otherHealthHistoryStatus: 'Unsure',
      reviewRoute: 'DetailedCollectionRequired',
      patientReportedMayBeIncompleteAcknowledged: true,
      noClinicalDetailsCapturedAcknowledged: true,
      clinicianReconciliationRequiredAcknowledged: true,
      direction: 'A separately authorized detailed collection and clinician reconciliation workflow is required; none was created.',
    } satisfies TelehealthApplicantClinicalInformationInventory
    vi.mocked(getApplicantClinicalInformationInventory)
      .mockResolvedValueOnce(clinicalInformationInventoryFixture)
      .mockResolvedValue(recordedInventory)
    vi.mocked(recordApplicantClinicalInformationInventory)
      .mockRejectedValueOnce(new Error('Clinical inventory result unknown; retry unchanged.'))
      .mockResolvedValue(recordedInventory)

    render(<MemoryRouter><ProspectivePatientTelehealthEntry /></MemoryRouter>)

    const heading = await screen.findByRole('heading', { name: 'Tell us whether each category has items that need review' })
    expect(heading.parentElement).toHaveTextContent(/Do not enter any medication, substance, reaction, dose, diagnosis, symptom, procedure, narrative, date, identifier, or other clinical detail/i)
    expect(heading.parentElement?.querySelector('input[type="text"]')).toBeNull()
    expect(heading.parentElement?.querySelector('textarea')).toBeNull()

    const medications = screen.getByRole('group', { name: 'Medications' })
    const allergies = screen.getByRole('group', { name: 'Allergies or intolerances' })
    const history = screen.getByRole('group', { name: 'Other health history' })
    fireEvent.click(within(medications).getByLabelText(/medication items that need detailed review/i))
    fireEvent.click(within(allergies).getByLabelText(/report no allergies or intolerances/i))
    fireEvent.click(within(history).getByLabelText('I am unsure.'))
    fireEvent.click(screen.getByLabelText(/patient-reported signals and may be incomplete/i))
    fireEvent.click(screen.getByLabelText(/captures no detailed clinical information/i))
    fireEvent.click(screen.getByLabelText(/clinician must reconcile medications/i))

    const submit = screen.getByRole('button', { name: 'Record clinical-information inventory' })
    expect(submit).toBeEnabled()
    fireEvent.click(submit)
    const failure = await screen.findByRole('alert')
    expect(failure).toHaveTextContent('Clinical inventory result unknown; retry unchanged.')
    await waitFor(() => expect(failure).toHaveFocus())
    expect(within(medications).getByLabelText(/medication items that need detailed review/i)).toBeChecked()
    fireEvent.click(screen.getByRole('button', { name: 'Record clinical-information inventory' }))

    await waitFor(() => expect(recordApplicantClinicalInformationInventory).toHaveBeenCalledTimes(2))
    expect(vi.mocked(recordApplicantClinicalInformationInventory).mock.calls[0][3]).toBe('15000000-0000-4000-8000-000000000015')
    expect(vi.mocked(recordApplicantClinicalInformationInventory).mock.calls[1][3]).toBe('15000000-0000-4000-8000-000000000015')
    expect(vi.mocked(recordApplicantClinicalInformationInventory).mock.calls[0][2]).toEqual({
      expectedVersion: 17,
      inventorySnapshotFingerprint: 'b'.repeat(64),
      medicationsStatus: 'ItemsToReview',
      allergiesOrIntolerancesStatus: 'PatientReportsNone',
      otherHealthHistoryStatus: 'Unsure',
      patientReportedMayBeIncompleteAcknowledged: true,
      noClinicalDetailsCapturedAcknowledged: true,
      clinicianReconciliationRequiredAcknowledged: true,
    })

    const result = await screen.findByRole('heading', { name: 'Clinical-information inventory recorded' })
    expect(result.parentElement).toHaveTextContent('MedicationsItems need detailed review')
    expect(result.parentElement).toHaveTextContent('Allergies or intolerancesPatient reports none — not clinically reconciled')
    expect(result.parentElement).toHaveTextContent('Other health historyPatient is unsure')
    expect(result.parentElement).toHaveTextContent('Server review routeDetailedCollectionRequired')
    expect(result.parentElement).toHaveTextContent('Medication list reconciledNo')
    expect(result.parentElement).toHaveTextContent('Clinician review createdNo')
    expect(result.parentElement).toHaveTextContent('Prescribing enabledNo')
    await waitFor(() => expect(result.parentElement).toHaveFocus())

    const stored = `${sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}${localStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}`
    expect(stored).not.toMatch(/ItemsToReview|PatientReportsNone|Unsure|inventorySnapshotFingerprint|medication|allerg|history|patientId/i)
  })

  it('records bounded synthetic medication selections with stable retry and no canonical consequence', async () => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('16000000-0000-4000-8000-000000000016')
    vi.mocked(getProspectiveApplicant).mockResolvedValue({
      ...approvedApplicant,
      status: 'SyntheticClinicalInformationInventoryRecorded',
      version: 18,
      canonicalPatientCreated: true,
      nextAction: 'Record bounded patient-reported medication information.',
    })
    vi.mocked(getApplicantClinicalInformationInventory).mockResolvedValue({
      ...clinicalInformationInventoryFixture,
      applicantVersion: 18,
      applicantStatus: 'SyntheticClinicalInformationInventoryRecorded',
      inventoryRecorded: true,
      recordedAt: '2026-08-28T02:20:00Z',
      medicationsStatus: 'ItemsToReview',
      allergiesOrIntolerancesStatus: 'PatientReportsNone',
      otherHealthHistoryStatus: 'Unsure',
      reviewRoute: 'DetailedCollectionRequired',
      patientReportedMayBeIncompleteAcknowledged: true,
      noClinicalDetailsCapturedAcknowledged: true,
      clinicianReconciliationRequiredAcknowledged: true,
    })
    const recordedMedicationInformation = {
      ...medicationInformationFixture,
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
    } satisfies TelehealthApplicantMedicationInformation
    vi.mocked(getApplicantMedicationInformation)
      .mockResolvedValueOnce(medicationInformationFixture)
      .mockResolvedValue(recordedMedicationInformation)
    vi.mocked(recordApplicantMedicationInformation)
      .mockRejectedValueOnce(new Error('Medication information result unknown; retry unchanged.'))
      .mockResolvedValue(recordedMedicationInformation)

    render(<MemoryRouter><ProspectivePatientTelehealthEntry /></MemoryRouter>)

    const heading = await screen.findByRole('heading', { name: 'Confirm the medication category' })
    expect(heading.parentElement).toHaveTextContent(/fixed local synthetic ingredient catalog is incomplete/i)
    expect(heading.parentElement?.querySelector('input[type="text"]')).toBeNull()
    expect(heading.parentElement?.querySelector('textarea')).toBeNull()
    fireEvent.click(screen.getByLabelText('Sumatriptan'))
    fireEvent.click(screen.getByLabelText('Melatonin'))
    fireEvent.change(screen.getByLabelText('Reported use status for Melatonin'), { target: { value: 'Unsure' } })
    fireEvent.click(screen.getByLabelText(/additional or unlisted medication items/i))
    fireEvent.click(screen.getByLabelText(/patient-reported medication information may be incomplete/i))
    fireEvent.click(screen.getByLabelText(/catalog is incomplete and has no RxNorm/i))
    fireEvent.click(screen.getByLabelText(/no dose, directions, or other medication detail/i))
    fireEvent.click(screen.getByLabelText(/clinician must reconcile medication information/i))

    const submit = screen.getByRole('button', { name: 'Record medication information' })
    expect(submit).toBeEnabled()
    fireEvent.click(submit)
    const failure = await screen.findByRole('alert')
    expect(failure).toHaveTextContent('Medication information result unknown; retry unchanged.')
    await waitFor(() => expect(failure).toHaveFocus())
    expect(screen.getByLabelText('Sumatriptan')).toBeChecked()
    fireEvent.click(screen.getByRole('button', { name: 'Record medication information' }))

    await waitFor(() => expect(recordApplicantMedicationInformation).toHaveBeenCalledTimes(2))
    expect(vi.mocked(recordApplicantMedicationInformation).mock.calls[0][3]).toBe('16000000-0000-4000-8000-000000000016')
    expect(vi.mocked(recordApplicantMedicationInformation).mock.calls[1][3]).toBe('16000000-0000-4000-8000-000000000016')
    expect(vi.mocked(recordApplicantMedicationInformation).mock.calls[0][2]).toEqual({
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

    const result = await screen.findByRole('heading', { name: 'Medication information recorded' })
    expect(result.parentElement).toHaveTextContent('Selected synthetic ingredients2')
    expect(result.parentElement).toHaveTextContent('Sumatriptan: Taking')
    expect(result.parentElement).toHaveTextContent('Melatonin: Unsure')
    expect(result.parentElement).toHaveTextContent('MedicationStatement createdNo')
    expect(result.parentElement).toHaveTextContent('MedicationRequest createdNo')
    expect(result.parentElement).toHaveTextContent('Interaction check performedNo')
    expect(result.parentElement).toHaveTextContent('Prescribing enabledNo')
    await waitFor(() => expect(result.parentElement).toHaveFocus())

    const stored = `${sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}${localStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}`
    expect(stored).not.toMatch(/sumatriptan|melatonin|Taking|Unsure|medicationInformationSnapshotFingerprint|patientId/i)
  })

  it('recovers allergy loading, records bounded synthetic substances with stable retry, and creates no clinical consequence', async () => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('17000000-0000-4000-8000-000000000017')
    vi.mocked(getProspectiveApplicant).mockResolvedValue({
      ...approvedApplicant,
      status: 'SyntheticMedicationInformationRecorded',
      version: 19,
      canonicalPatientCreated: true,
      nextAction: 'Record bounded patient-reported allergy or intolerance information.',
    })
    const recordedMedicationInformation = {
      ...medicationInformationFixture,
      applicantVersion: 19,
      applicantStatus: 'SyntheticMedicationInformationRecorded',
      medicationInformationRecorded: true,
      recordedAt: '2026-08-28T02:25:00Z',
      medicationItems: [],
      reviewRoute: 'ClinicianMedicationReviewRequired',
      patientReportedMayBeIncompleteAcknowledged: true,
      syntheticCatalogIncompleteAcknowledged: true,
      noDoseOrDirectionsCapturedAcknowledged: true,
      clinicianReconciliationRequiredAcknowledged: true,
    } satisfies TelehealthApplicantMedicationInformation
    const recordedAllergyInformation = {
      ...allergyInformationFixture,
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
    } satisfies TelehealthApplicantAllergyInformation
    vi.mocked(getApplicantMedicationInformation).mockResolvedValue(recordedMedicationInformation)
    vi.mocked(getApplicantAllergyInformation)
      .mockRejectedValueOnce(new Error('Allergy information temporarily unavailable.'))
      .mockResolvedValueOnce(allergyInformationFixture)
      .mockResolvedValue(recordedAllergyInformation)
    vi.mocked(recordApplicantAllergyInformation)
      .mockRejectedValueOnce(new Error('Allergy information result unknown; retry unchanged.'))
      .mockResolvedValue(recordedAllergyInformation)

    render(<MemoryRouter><ProspectivePatientTelehealthEntry /></MemoryRouter>)

    const loadFailure = await screen.findByRole('alert')
    expect(loadFailure).toHaveTextContent('Allergy information temporarily unavailable.')
    expect(screen.getByRole('button', { name: 'Try loading allergy information again' })).toBeVisible()
    fireEvent.click(screen.getByRole('button', { name: 'Try loading allergy information again' }))

    const heading = await screen.findByRole('heading', { name: 'Confirm the allergy or intolerance category' })
    expect(heading.parentElement).toHaveTextContent(/fixed local synthetic substance catalog is incomplete/i)
    expect(heading.parentElement?.querySelector('input[type="text"]')).toBeNull()
    expect(heading.parentElement?.querySelector('textarea')).toBeNull()
    fireEvent.click(screen.getByLabelText(/Amoxicillin.*local category/i))
    fireEvent.click(screen.getByLabelText(/Peanut.*local category/i))
    fireEvent.click(screen.getByLabelText(/additional or unlisted allergy or intolerance substances/i))
    fireEvent.click(screen.getByLabelText(/patient-reported allergy or intolerance information may be incomplete/i))
    fireEvent.click(screen.getByLabelText(/synthetic local substance catalog is incomplete/i))
    fireEvent.click(screen.getByLabelText(/no reaction, severity, criticality/i))
    fireEvent.click(screen.getByLabelText(/clinician must verify and reconcile allergy/i))

    const submit = screen.getByRole('button', { name: 'Record allergy information' })
    expect(submit).toBeEnabled()
    fireEvent.click(submit)
    const submissionFailure = await screen.findByRole('alert')
    expect(submissionFailure).toHaveTextContent('Allergy information result unknown; retry unchanged.')
    await waitFor(() => expect(submissionFailure).toHaveFocus())
    expect(screen.getByLabelText(/Amoxicillin.*local category/i)).toBeChecked()
    fireEvent.click(screen.getByRole('button', { name: 'Record allergy information' }))

    await waitFor(() => expect(recordApplicantAllergyInformation).toHaveBeenCalledTimes(2))
    expect(vi.mocked(recordApplicantAllergyInformation).mock.calls[0][3]).toBe('17000000-0000-4000-8000-000000000017')
    expect(vi.mocked(recordApplicantAllergyInformation).mock.calls[1][3]).toBe('17000000-0000-4000-8000-000000000017')
    expect(vi.mocked(recordApplicantAllergyInformation).mock.calls[0][2]).toEqual({
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

    const result = await screen.findByRole('heading', { name: 'Allergy or intolerance information recorded' })
    expect(result.parentElement).toHaveTextContent('Selected synthetic substances2')
    expect(result.parentElement).toHaveTextContent('Amoxicillin: Medication')
    expect(result.parentElement).toHaveTextContent('Peanut: Food')
    expect(result.parentElement).toHaveTextContent('AllergyIntolerance createdNo')
    expect(result.parentElement).toHaveTextContent('Reaction assessedNo')
    expect(result.parentElement).toHaveTextContent('Criticality assessedNo')
    expect(result.parentElement).toHaveTextContent('Contraindication check performedNo')
    expect(result.parentElement).toHaveTextContent('Prescribing enabledNo')
    await waitFor(() => expect(result.parentElement).toHaveFocus())

    const stored = `${sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}${localStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}`
    expect(stored).not.toMatch(/amoxicillin|peanut|Medication|Food|allergyInformationSnapshotFingerprint|patientId/i)
  })

  it('recovers health-history loading and stable retry without converting review topics into clinical findings', async () => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('18000000-0000-4000-8000-000000000018')
    vi.mocked(getProspectiveApplicant).mockResolvedValue({
      ...approvedApplicant,
      status: 'SyntheticAllergyInformationRecorded',
      version: 20,
      canonicalPatientCreated: true,
      nextAction: 'Record bounded patient-reported health-history topics.',
    })
    const recordedAllergyInformation = {
      ...allergyInformationFixture,
      applicantVersion: 20,
      applicantStatus: 'SyntheticAllergyInformationRecorded',
      allergyInformationRecorded: true,
      recordedAt: '2026-08-28T02:30:00Z',
      allergyItems: [],
      reviewRoute: 'ClinicianAllergyReviewRequired',
      patientReportedMayBeIncompleteAcknowledged: true,
      syntheticCatalogIncompleteAcknowledged: true,
      noReactionOrCriticalityCapturedAcknowledged: true,
      clinicianVerificationRequiredAcknowledged: true,
    } satisfies TelehealthApplicantAllergyInformation
    const recordedHealthHistoryInformation = {
      ...healthHistoryInformationFixture,
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
    } satisfies TelehealthApplicantHealthHistoryInformation
    vi.mocked(getApplicantAllergyInformation).mockResolvedValue(recordedAllergyInformation)
    vi.mocked(getApplicantHealthHistoryInformation)
      .mockRejectedValueOnce(new Error('Health history temporarily unavailable.'))
      .mockResolvedValueOnce(healthHistoryInformationFixture)
      .mockResolvedValue(recordedHealthHistoryInformation)
    vi.mocked(recordApplicantHealthHistoryInformation)
      .mockRejectedValueOnce(new Error('Health history result unknown; retry unchanged.'))
      .mockResolvedValue(recordedHealthHistoryInformation)

    render(<MemoryRouter><ProspectivePatientTelehealthEntry /></MemoryRouter>)

    const loadFailure = await screen.findByRole('alert')
    expect(loadFailure).toHaveTextContent('Health history temporarily unavailable.')
    fireEvent.click(screen.getByRole('button', { name: 'Try loading health-history information again' }))

    const heading = await screen.findByRole('heading', { name: 'Identify broad topics for later review' })
    expect(heading.parentElement).toHaveTextContent(/selection is only a prompt for later review/i)
    expect(heading.parentElement?.querySelector('input[type="text"]')).toBeNull()
    expect(heading.parentElement?.querySelector('textarea')).toBeNull()
    fireEvent.click(screen.getByLabelText(/Ongoing health conditions.*local category/i))
    fireEvent.click(screen.getByLabelText(/Behavioral health or substance use information.*local category/i))
    fireEvent.click(screen.getByLabelText(/additional or unlisted health-history topics/i))
    fireEvent.click(screen.getByLabelText(/patient-reported health-history information may be incomplete/i))
    fireEvent.click(screen.getByLabelText(/topic selection is not a diagnosis/i))
    fireEvent.click(screen.getByLabelText(/no clinical or verification status, timing/i))
    fireEvent.click(screen.getByLabelText(/detailed collection and clinician verification are required/i))

    fireEvent.click(screen.getByRole('button', { name: 'Record health-history topics' }))
    const submissionFailure = await screen.findByRole('alert')
    expect(submissionFailure).toHaveTextContent('Health history result unknown; retry unchanged.')
    await waitFor(() => expect(submissionFailure).toHaveFocus())
    expect(screen.getByLabelText(/Ongoing health conditions.*local category/i)).toBeChecked()
    fireEvent.click(screen.getByRole('button', { name: 'Record health-history topics' }))

    await waitFor(() => expect(recordApplicantHealthHistoryInformation).toHaveBeenCalledTimes(2))
    expect(vi.mocked(recordApplicantHealthHistoryInformation).mock.calls[0][3]).toBe('18000000-0000-4000-8000-000000000018')
    expect(vi.mocked(recordApplicantHealthHistoryInformation).mock.calls[1][3]).toBe('18000000-0000-4000-8000-000000000018')
    expect(vi.mocked(recordApplicantHealthHistoryInformation).mock.calls[0][2]).toEqual({
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

    const result = await screen.findByRole('heading', { name: 'Health-history topics recorded' })
    expect(result.parentElement).toHaveTextContent('Selected synthetic review topics2')
    expect(result.parentElement).toHaveTextContent('Ongoing health conditions: ConditionOrConcern')
    expect(result.parentElement).toHaveTextContent('Behavioral health or substance use information: SensitiveHistory')
    expect(result.parentElement).toHaveTextContent('Condition or problem createdNo')
    expect(result.parentElement).toHaveTextContent('FamilyMemberHistory or QuestionnaireResponse createdNo')
    expect(result.parentElement).toHaveTextContent('Risk modifier evaluatedNo')
    expect(result.parentElement).toHaveTextContent('Clinical triage changedNo')
    expect(result.parentElement).toHaveTextContent('Prescribing enabledNo')
    await waitFor(() => expect(result.parentElement).toHaveFocus())

    const stored = `${sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}${localStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}`
    expect(stored).not.toMatch(/ongoing-health|behavioral-health|SensitiveHistory|healthHistoryInformationSnapshotFingerprint|patientId/i)
  })

  it('confirms the no-edit clinical-information summary with stable retry and no clinical authority', async () => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('19000000-0000-4000-8000-000000000019')
    const summaryApplicant = {
      ...approvedApplicant,
      status: 'SyntheticHealthHistoryInformationRecorded',
      version: 21,
      canonicalPatientCreated: true,
      nextAction: 'Review the synthetic clinical-information summary.',
    } as const
    const recordedHealthHistory = {
      ...healthHistoryInformationFixture,
      applicantVersion: 21,
      applicantStatus: 'SyntheticHealthHistoryInformationRecorded',
      healthHistoryInformationRecorded: true,
      recordedAt: '2026-08-28T02:35:00Z',
      healthHistoryTopics: [],
      additionalOrUnlistedTopicsReported: true,
      reviewRoute: 'AdditionalHealthHistoryCollectionRequired',
      patientReportedMayBeIncompleteAcknowledged: true,
      topicSelectionIsNotDiagnosisAcknowledged: true,
      noStatusOrTimingCapturedAcknowledged: true,
      clinicianVerificationRequiredAcknowledged: true,
    } satisfies TelehealthApplicantHealthHistoryInformation
    const confirmedSummary = {
      ...clinicalInformationSummaryFixture,
      applicantVersion: 22,
      applicantStatus: 'SyntheticClinicalInformationSummaryConfirmed',
      summaryConfirmed: true,
      confirmedAt: '2026-08-28T02:40:00Z',
      patientReportedMayBeIncompleteAcknowledged: true,
      notClinicallyVerifiedOrReconciledAcknowledged: true,
      noIntakeCompletionOrEligibilityAcknowledged: true,
      correctionRequiresSeparateWorkflowAcknowledged: true,
      direction: 'Additional collection still requires a separately authorized workflow; no task was created.',
    } satisfies TelehealthApplicantClinicalInformationSummary
    vi.mocked(getProspectiveApplicant).mockResolvedValue(summaryApplicant)
    vi.mocked(getApplicantHealthHistoryInformation).mockResolvedValue(recordedHealthHistory)
    vi.mocked(getApplicantClinicalInformationSummary)
      .mockRejectedValueOnce(new Error('Summary temporarily unavailable.'))
      .mockResolvedValueOnce(clinicalInformationSummaryFixture)
      .mockResolvedValue(confirmedSummary)
    vi.mocked(confirmApplicantClinicalInformationSummary)
      .mockRejectedValueOnce(new Error('Summary result unknown; retry unchanged.'))
      .mockResolvedValue(confirmedSummary)

    render(<MemoryRouter><ProspectivePatientTelehealthEntry /></MemoryRouter>)

    const loadFailure = await screen.findByRole('alert')
    expect(loadFailure).toHaveTextContent('Summary temporarily unavailable.')
    fireEvent.click(screen.getByRole('button', { name: 'Retry summary load' }))

    const heading = await screen.findByRole('heading', { name: 'Review your clinical-information summary' })
    expect(heading.parentElement).toHaveTextContent('Medications')
    expect(heading.parentElement).toHaveTextContent('selected; additional or unlisted')
    expect(heading.parentElement?.querySelector('input[type="text"]')).toBeNull()
    expect(heading.parentElement?.querySelector('textarea')).toBeNull()
    fireEvent.click(screen.getByLabelText(/patient-reported information and may be incomplete/i))
    fireEvent.click(screen.getByLabelText(/none of these categories has been clinically verified/i))
    fireEvent.click(screen.getByLabelText(/does not complete clinical intake/i))
    fireEvent.click(screen.getByLabelText(/corrections or omissions require a separately authorized workflow/i))
    fireEvent.click(screen.getByRole('button', { name: 'Confirm no-edit summary' }))

    const submissionFailure = await screen.findByRole('alert')
    expect(submissionFailure).toHaveTextContent('Summary result unknown; retry unchanged.')
    await waitFor(() => expect(submissionFailure).toHaveFocus())
    expect(screen.getByLabelText(/patient-reported information and may be incomplete/i)).toBeChecked()
    fireEvent.click(screen.getByRole('button', { name: 'Confirm no-edit summary' }))

    await waitFor(() => expect(confirmApplicantClinicalInformationSummary).toHaveBeenCalledTimes(2))
    expect(vi.mocked(confirmApplicantClinicalInformationSummary).mock.calls[0][3]).toBe('19000000-0000-4000-8000-000000000019')
    expect(vi.mocked(confirmApplicantClinicalInformationSummary).mock.calls[1][3]).toBe('19000000-0000-4000-8000-000000000019')
    expect(vi.mocked(confirmApplicantClinicalInformationSummary).mock.calls[0][2]).toEqual({
      expectedVersion: 21,
      clinicalInformationSummarySnapshotFingerprint: 'e'.repeat(64),
      patientReportedMayBeIncompleteAcknowledged: true,
      notClinicallyVerifiedOrReconciledAcknowledged: true,
      noIntakeCompletionOrEligibilityAcknowledged: true,
      correctionRequiresSeparateWorkflowAcknowledged: true,
    })

    const result = await screen.findByRole('heading', { name: 'Clinical-information summary confirmed' })
    expect(result.parentElement).toHaveTextContent('QuestionnaireResponse createdNo')
    expect(result.parentElement).toHaveTextContent('Medication, allergy, or history reconciledNo')
    expect(result.parentElement).toHaveTextContent('Confirmed negative establishedNo')
    expect(result.parentElement).toHaveTextContent('Clinical intake completedNo')
    expect(result.parentElement).toHaveTextContent('Clinical eligibility establishedNo')
    expect(result.parentElement).toHaveTextContent('Practice accepted or queue enteredNo')
    await waitFor(() => expect(result.parentElement).toHaveFocus())

    const stored = `${sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}${localStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}`
    expect(stored).not.toMatch(/clinicalInformationSummarySnapshotFingerprint|selectedItemCount|summaryRoute|patientId/i)
  })

  it('acknowledges minimized pre-request readiness with stable retry and no request authority', async () => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('1a000000-0000-4000-8000-00000000001a')
    const readinessApplicant = {
      ...approvedApplicant,
      status: 'SyntheticClinicalInformationSummaryConfirmed',
      version: 22,
      canonicalPatientCreated: true,
      nextAction: 'Review the synthetic pre-request readiness sections.',
    } as const
    const confirmedSummary = {
      ...clinicalInformationSummaryFixture,
      applicantVersion: 22,
      applicantStatus: 'SyntheticClinicalInformationSummaryConfirmed',
      summaryConfirmed: true,
      confirmedAt: '2026-08-28T02:40:00Z',
      patientReportedMayBeIncompleteAcknowledged: true,
      notClinicallyVerifiedOrReconciledAcknowledged: true,
      noIntakeCompletionOrEligibilityAcknowledged: true,
      correctionRequiresSeparateWorkflowAcknowledged: true,
    } satisfies TelehealthApplicantClinicalInformationSummary
    const acknowledged = {
      ...preRequestReadinessFixture,
      applicantVersion: 23,
      applicantStatus: 'SyntheticPreRequestReadinessAcknowledged',
      readinessAcknowledged: true,
      acknowledgedAt: '2026-08-28T02:45:00Z',
      priorSectionsReviewedAcknowledged: true,
      outstandingStepsRemainAcknowledged: true,
      noRequestOrQueueCreatedAcknowledged: true,
      correctionRequiresSeparateWorkflowAcknowledged: true,
      direction: 'The five sections were acknowledged; no request or queue entry was created.',
    } satisfies TelehealthApplicantPreRequestReadiness
    vi.mocked(getProspectiveApplicant).mockResolvedValue(readinessApplicant)
    vi.mocked(getApplicantHealthHistoryInformation).mockResolvedValue({
      ...healthHistoryInformationFixture,
      applicantVersion: 21,
      applicantStatus: 'SyntheticHealthHistoryInformationRecorded',
      healthHistoryInformationRecorded: true,
    })
    vi.mocked(getApplicantClinicalInformationSummary).mockResolvedValue(confirmedSummary)
    vi.mocked(getApplicantPreRequestReadiness)
      .mockRejectedValueOnce(new Error('Readiness temporarily unavailable.'))
      .mockResolvedValueOnce(preRequestReadinessFixture)
      .mockResolvedValue(acknowledged)
    vi.mocked(acknowledgeApplicantPreRequestReadiness)
      .mockRejectedValueOnce(new Error('Readiness result unknown; retry unchanged.'))
      .mockResolvedValue(acknowledged)

    render(<MemoryRouter><ProspectivePatientTelehealthEntry /></MemoryRouter>)

    const loadFailure = await screen.findByRole('alert')
    expect(loadFailure).toHaveTextContent('Readiness temporarily unavailable.')
    fireEvent.click(screen.getByRole('button', { name: 'Retry readiness load' }))

    const heading = await screen.findByRole('heading', { name: 'Review pre-request readiness' })
    expect(heading.parentElement).toHaveTextContent('Registration details')
    expect(heading.parentElement).toHaveTextContent('Insurance evidence')
    expect(heading.parentElement).toHaveTextContent('Communication and access')
    expect(heading.parentElement).toHaveTextContent('Device preparation')
    expect(heading.parentElement).toHaveTextContent('Clinical information')
    expect(heading.parentElement?.querySelector('input[type="text"]')).toBeNull()
    expect(heading.parentElement?.querySelector('textarea')).toBeNull()
    fireEvent.click(screen.getByLabelText(/reviewed the five coarse sections/i))
    fireEvent.click(screen.getByLabelText(/identity, coverage, support, technology/i))
    fireEvent.click(screen.getByLabelText(/does not create a telehealth request or queue entry/i))
    fireEvent.click(screen.getByLabelText(/corrections require a separately authorized workflow/i))
    fireEvent.click(screen.getByRole('button', { name: 'Acknowledge readiness boundaries' }))

    const submissionFailure = await screen.findByRole('alert')
    expect(submissionFailure).toHaveTextContent('Readiness result unknown; retry unchanged.')
    await waitFor(() => expect(submissionFailure).toHaveFocus())
    expect(screen.getByLabelText(/reviewed the five coarse sections/i)).toBeChecked()
    fireEvent.click(screen.getByRole('button', { name: 'Acknowledge readiness boundaries' }))

    await waitFor(() => expect(acknowledgeApplicantPreRequestReadiness).toHaveBeenCalledTimes(2))
    expect(vi.mocked(acknowledgeApplicantPreRequestReadiness).mock.calls[0][3]).toBe('1a000000-0000-4000-8000-00000000001a')
    expect(vi.mocked(acknowledgeApplicantPreRequestReadiness).mock.calls[1][3]).toBe('1a000000-0000-4000-8000-00000000001a')
    expect(vi.mocked(acknowledgeApplicantPreRequestReadiness).mock.calls[0][2]).toEqual({
      expectedVersion: 22,
      preRequestReadinessSnapshotFingerprint: 'f'.repeat(64),
      priorSectionsReviewedAcknowledged: true,
      outstandingStepsRemainAcknowledged: true,
      noRequestOrQueueCreatedAcknowledged: true,
      correctionRequiresSeparateWorkflowAcknowledged: true,
    })

    const result = await screen.findByRole('heading', { name: 'Pre-request readiness boundaries acknowledged' })
    expect(result.parentElement).toHaveTextContent('Identity assurance establishedNo')
    expect(result.parentElement).toHaveTextContent('Coverage or rendering-clinician network guaranteedNo')
    expect(result.parentElement).toHaveTextContent('Interpreter or accommodation arrangedNo')
    expect(result.parentElement).toHaveTextContent('Technology readiness establishedNo')
    expect(result.parentElement).toHaveTextContent('Clinical information reconciled or intake completedNo')
    expect(result.parentElement).toHaveTextContent('Request, queue, appointment, or encounter createdNo')
    await waitFor(() => expect(result.parentElement).toHaveFocus())

    const stored = `${sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}${localStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}`
    expect(stored).not.toMatch(/preRequestReadinessSnapshotFingerprint|outstandingRoute|overallRoute|patientId/i)
  })

  it('submits one practice review work item with stable retry and no care queue authority', async () => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('1b000000-0000-4000-8000-00000000001b')
    const readyApplicant = {
      ...approvedApplicant,
      status: 'SyntheticPreRequestReadinessAcknowledged',
      version: 23,
      canonicalPatientCreated: true,
      nextAction: 'Submit for synthetic practice review.',
    } as const
    const submitted = {
      ...practiceReviewFixture,
      applicantVersion: 24,
      applicantStatus: 'SyntheticPracticeReviewSubmitted',
      submissionReady: false,
      submitted: true,
      practiceReviewCaseId: '1c000000-0000-4000-8000-00000000001c',
      reviewStatus: 'PendingPracticeReview',
      submittedAt: '2026-08-28T03:00:00Z',
      patientReportedInformationAcknowledged: true,
      practiceMayRequestInformationOrDeclineAcknowledged: true,
      noTelehealthRequestOrCareQueueAcknowledged: true,
      worseningSymptomsRequireImmediateActionAcknowledged: true,
      staffReviewCreated: true,
      direction: 'Submitted for practice review; no request or care queue was created.',
    } satisfies TelehealthApplicantPracticeReview
    vi.mocked(getProspectiveApplicant).mockResolvedValue(readyApplicant)
    vi.mocked(getApplicantPreRequestReadiness).mockResolvedValue({
      ...preRequestReadinessFixture,
      applicantVersion: 23,
      applicantStatus: 'SyntheticPreRequestReadinessAcknowledged',
      readinessAcknowledged: true,
      acknowledgedAt: '2026-08-28T02:45:00Z',
    })
    vi.mocked(getApplicantPracticeReviewSubmission)
      .mockRejectedValueOnce(new Error('Practice review temporarily unavailable.'))
      .mockResolvedValueOnce(practiceReviewFixture)
      .mockResolvedValue(submitted)
    vi.mocked(submitApplicantPracticeReview)
      .mockRejectedValueOnce(new Error('Submission result unknown; retry unchanged.'))
      .mockResolvedValue(submitted)

    render(<MemoryRouter><ProspectivePatientTelehealthEntry /></MemoryRouter>)

    const loadFailure = await screen.findByRole('alert')
    expect(loadFailure).toHaveTextContent('Practice review temporarily unavailable.')
    fireEvent.click(screen.getByRole('button', { name: 'Retry practice review load' }))

    const heading = await screen.findByRole('heading', { name: 'Submit for practice review' })
    expect(heading.parentElement).toHaveTextContent('does not create a telehealth request')
    expect(heading.parentElement).toHaveTextContent('Patient or clinician queueNot entered')
    expect(heading.parentElement?.querySelector('input[type="text"]')).toBeNull()
    expect(heading.parentElement?.querySelector('textarea')).toBeNull()
    fireEvent.click(screen.getByLabelText(/remains patient reported/i))
    fireEvent.click(screen.getByLabelText(/may request more information or decline/i))
    fireEvent.click(screen.getByLabelText(/no telehealth request, doctor search/i))
    fireEvent.click(screen.getByLabelText(/urgent or worsening/i))
    fireEvent.click(screen.getByRole('button', { name: 'Submit for practice review' }))

    const submissionFailure = await screen.findByRole('alert')
    expect(submissionFailure).toHaveTextContent('Submission result unknown; retry unchanged.')
    expect(screen.getByLabelText(/remains patient reported/i)).toBeChecked()
    fireEvent.click(screen.getByRole('button', { name: 'Submit for practice review' }))

    await waitFor(() => expect(submitApplicantPracticeReview).toHaveBeenCalledTimes(2))
    expect(vi.mocked(submitApplicantPracticeReview).mock.calls[0][3]).toBe('1b000000-0000-4000-8000-00000000001b')
    expect(vi.mocked(submitApplicantPracticeReview).mock.calls[1][3]).toBe('1b000000-0000-4000-8000-00000000001b')
    expect(vi.mocked(submitApplicantPracticeReview).mock.calls[0][2]).toEqual({
      expectedVersion: 23,
      practiceReviewSnapshotFingerprint: '1'.repeat(64),
      patientReportedInformationAcknowledged: true,
      practiceMayRequestInformationOrDeclineAcknowledged: true,
      noTelehealthRequestOrCareQueueAcknowledged: true,
      worseningSymptomsRequireImmediateActionAcknowledged: true,
    })

    const result = await screen.findByRole('heading', { name: 'Submitted for practice review' })
    expect(result.parentElement).toHaveTextContent('Practice staff review work item createdYes')
    expect(result.parentElement).toHaveTextContent('Practice acceptedNo')
    expect(result.parentElement).toHaveTextContent('Telehealth request createdNo')
    expect(result.parentElement).toHaveTextContent('Patient or clinician queue enteredNo')
    await waitFor(() => expect(result.parentElement).toHaveFocus())

    const stored = `${sessionStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}${localStorage.getItem('avenchart-ui.telehealthProspectiveApplicant') ?? ''}`
    expect(stored).not.toMatch(/practiceReviewSnapshotFingerprint|practiceReviewCaseId|reviewRoute|patientId/i)
  })
})
