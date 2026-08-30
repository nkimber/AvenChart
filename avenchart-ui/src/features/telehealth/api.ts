// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { loadClinicianSession, loadPortalSession } from '../../auth/session.ts'
import { apiBaseUrl, apiFetch } from '../../api/transport.ts'

export type TelehealthPracticeContext = {
  available: boolean
  practiceDisplayName: string
  supportedStates: string[]
  syntheticOnly: boolean
  entryMessage: string
}

export type TelehealthProspectiveApplicantInput = {
  legalFirstName: string
  legalLastName: string
  dateOfBirth: string
  email: string
  phone: string
  residenceStateCode: string
  postalCode: string
  syntheticDataConfirmed: true
}

export type TelehealthProspectiveApplicant = {
  applicantId: string
  status: 'ContactVerificationPending' | 'IdentityReviewPending' | 'IdentityReviewApproved' | 'ManualReviewRequired' | 'SafetyScreenPassed' | 'SafetyClinicalReviewRequired' | 'SafetyInPersonRequired' | 'SafetyEmergencyRedirect' | 'VisitPurposeRecorded' | 'PracticeNetworkPrecheckRecorded' | 'MemberInsuranceDetailsRecorded' | 'SyntheticEligibilityRecorded' | 'SyntheticPracticeNetworkRecorded' | 'SyntheticIdentityProofingRecorded' | 'SyntheticPromotionAuthorized' | 'SyntheticPromotionDenied' | 'SyntheticPatientPromoted' | 'SyntheticTelehealthNoticeAcknowledged' | 'SyntheticMinimumRegistrationDetailsConfirmed' | 'SyntheticInsuranceDetailsConfirmed' | 'SyntheticCommunicationAccessReadinessRecorded' | 'SyntheticDevicePreparationRecorded' | 'SyntheticClinicalInformationInventoryRecorded' | 'SyntheticMedicationInformationRecorded' | 'SyntheticAllergyInformationRecorded' | 'SyntheticHealthHistoryInformationRecorded' | 'SyntheticClinicalInformationSummaryConfirmed' | 'SyntheticPreRequestReadinessAcknowledged' | 'SyntheticPracticeReviewSubmitted' | 'SyntheticPracticeReviewAuthorized' | 'SyntheticRequestCreated' | 'SyntheticPromotionBlockedPossibleMatch' | 'VerificationLocked' | 'Expired'
  version: number
  practiceDisplayName: string
  residenceStateCode: string
  maskedEmail: string
  maskedPhone: string
  contactVerified: boolean
  identityAssurance: 'UnverifiedContact' | 'ContactControlOnly'
  duplicateDisposition: 'NoCandidate' | 'PossibleMatchManualReview' | null
  canonicalPatientCreated: boolean
  verificationAttemptsRemaining: number
  expiresAt: string
  demonstrationVerificationCode: string | null
  nextAction: string
  limitations: string[]
}

export type TelehealthProspectiveSafetyTriageInput = {
  expectedVersion: number
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  currentLocationConfirmed: true
  hasEmergencyWarning: boolean
  severeOrWorsening: boolean
  requiresHandsOnExam: boolean
  unsure: boolean
  syntheticDataConfirmed: true
}

export type TelehealthProspectiveSafetyTriage = {
  evaluationId: string
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SafetyScreenPassed' | 'SafetyClinicalReviewRequired' | 'SafetyInPersonRequired' | 'SafetyEmergencyRedirect'
  safetyDisposition: 'EmergencyCareNow' | 'PromptInPersonCare' | 'InPersonCareRequired' | 'ClinicalReviewRequired' | 'ContinueProspectiveIntake'
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  protocolKey: 'synthetic-universal-safety'
  protocolVersion: 1
  evaluatedAt: string
  mayContinueProspectiveIntake: boolean
  identityProofed: false
  clinicalReviewPerformed: false
  canonicalPatientCreated: false
  chartLinked: false
  prospectiveIntakeCompleted: false
  coverageChecked: false
  requestCreated: false
  queueEnabled: false
  careEnabled: false
  direction: string
  limitations: string[]
}

export type TelehealthProspectiveVisitPurposeInput = {
  expectedVersion: number
  purposeCategory: 'migraine' | 'sleep'
  syntheticDataConfirmed: true
}

export type TelehealthProspectiveVisitPurpose = {
  purposeId: string
  applicantId: string
  applicantVersion: number
  applicantStatus: 'VisitPurposeRecorded'
  purposeCategory: 'migraine' | 'sleep'
  purposeDisplayLabel: 'Headache or known migraine pattern' | 'Sleep difficulty'
  recordedAt: string
  clinicalProtocolPublished: false
  clinicalEligibilityDetermined: false
  identityProofed: false
  canonicalPatientCreated: false
  chartLinked: false
  prospectiveIntakeCompleted: false
  coverageChecked: false
  requestCreated: false
  queueEnabled: false
  careEnabled: false
  direction: string
  limitations: string[]
}

export type TelehealthProspectivePracticeNetworkOption = {
  planKey: 'harbor-mutual-hd' | 'blue-valley-standard' | 'pine-state-choice'
  payerDisplayName: string
  productDisplayName: string
  practiceNetworkStatus: 'PracticeNetworkConfirmedFixture' | 'NetworkUnknown' | 'PracticeOutOfNetworkFixture'
  meaning: string
}

export type TelehealthProspectivePracticeNetworkOptions = {
  applicantId: string
  applicantVersion: number
  applicantStatus: 'VisitPurposeRecorded'
  adapterMode: 'NON_PRODUCTION'
  catalogKey: 'avenchart-synthetic-prospective-practice-network-2026-08'
  catalogVersion: 1
  catalogEffectiveFrom: string
  catalogEffectiveThrough: string
  plans: TelehealthProspectivePracticeNetworkOption[]
  memberEligibilityChecked: false
  memberBenefitsChecked: false
  renderingPhysicianNetworkChecked: false
  coverageVerified: false
  exactNetworkConfirmed: false
  direction: string
  limitations: string[]
}

export type TelehealthProspectivePracticeNetworkPrecheckInput = {
  expectedVersion: number
  planKey: TelehealthProspectivePracticeNetworkOption['planKey']
  syntheticDataConfirmed: true
}

export type TelehealthProspectivePracticeNetworkPrecheck = {
  precheckId: string
  applicantId: string
  applicantVersion: number
  applicantStatus: 'PracticeNetworkPrecheckRecorded'
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  purposeCategory: 'migraine' | 'sleep'
  planKey: TelehealthProspectivePracticeNetworkOption['planKey']
  payerDisplayName: string
  productDisplayName: string
  practiceNetworkStatus: TelehealthProspectivePracticeNetworkOption['practiceNetworkStatus']
  adapterMode: 'NON_PRODUCTION'
  catalogKey: 'avenchart-synthetic-prospective-practice-network-2026-08'
  catalogVersion: 1
  catalogEffectiveFrom: string
  catalogEffectiveThrough: string
  recordedAt: string
  memberEligibilityChecked: false
  memberBenefitsChecked: false
  renderingPhysicianNetworkChecked: false
  coverageVerified: false
  exactNetworkConfirmed: false
  identityProofed: false
  canonicalPatientCreated: false
  chartLinked: false
  portalAccountCreated: false
  prospectiveIntakeCompleted: false
  consentCreated: false
  practiceAccepted: false
  coverageRecordCreated: false
  estimateCreated: false
  financialAcknowledgmentCreated: false
  requestCreated: false
  queueEnabled: false
  appointmentCreated: false
  encounterCreated: false
  careEnabled: false
  prescribingEnabled: false
  billingEnabled: false
  claimCreated: false
  communicationEnabled: false
  integrationEnabled: false
  externalCallPerformed: false
  direction: string
  limitations: string[]
}

export type TelehealthProspectiveMemberInsuranceDetailsInput = {
  expectedVersion: number
  memberId: string
  groupNumber: string | null
  subscriberRelationship: 'Self' | 'Spouse' | 'Parent' | 'Other'
  subscriberFirstName: string | null
  subscriberLastName: string | null
  subscriberDateOfBirth: string | null
  detailsConfirmed: true
  syntheticDataConfirmed: true
}

export type TelehealthProspectiveMemberInsuranceDetails = {
  detailsId: string
  applicantId: string
  applicantVersion: number
  applicantStatus: 'MemberInsuranceDetailsRecorded'
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  purposeCategory: 'migraine' | 'sleep'
  planKey: TelehealthProspectivePracticeNetworkOption['planKey']
  payerDisplayName: string
  productDisplayName: string
  practiceNetworkStatus: TelehealthProspectivePracticeNetworkOption['practiceNetworkStatus']
  memberIdMask: string
  groupNumberMask: string | null
  subscriberRelationship: TelehealthProspectiveMemberInsuranceDetailsInput['subscriberRelationship']
  coveragePriority: 'Primary'
  protectionScheme: 'ASP.NET_CORE_DATA_PROTECTION'
  protectionVersion: 1
  recordedAt: string
  memberMatched: false
  memberEligibilityChecked: false
  memberBenefitsChecked: false
  renderingPhysicianNetworkChecked: false
  coverageVerified: false
  exactNetworkConfirmed: false
  identityProofed: false
  canonicalPatientCreated: false
  chartLinked: false
  portalAccountCreated: false
  prospectiveIntakeCompleted: false
  consentCreated: false
  practiceAccepted: false
  coverageRecordCreated: false
  estimateCreated: false
  financialAcknowledgmentCreated: false
  requestCreated: false
  queueEnabled: false
  appointmentCreated: false
  encounterCreated: false
  careEnabled: false
  prescribingEnabled: false
  billingEnabled: false
  claimCreated: false
  communicationEnabled: false
  integrationEnabled: false
  externalCallPerformed: false
  direction: string
  limitations: string[]
}

export type TelehealthProspectiveEligibilityInput = {
  expectedVersion: number
  syntheticDataConfirmed: true
}

export type TelehealthProspectiveEligibility = {
  eligibilityResultId: string
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SyntheticEligibilityRecorded'
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  purposeCategory: 'migraine' | 'sleep'
  planKey: TelehealthProspectivePracticeNetworkOption['planKey']
  payerDisplayName: string
  productDisplayName: string
  practiceNetworkStatus: TelehealthProspectivePracticeNetworkOption['practiceNetworkStatus']
  memberIdMask: string
  groupNumberMask: string | null
  subscriberRelationship: TelehealthProspectiveMemberInsuranceDetailsInput['subscriberRelationship']
  coveragePriority: 'Primary'
  dateOfService: string
  serviceCategory: 'ProfessionalTelehealthConsultation'
  adapterMode: 'NON_PRODUCTION'
  compatibilityTarget: 'ASC_X12N_270_271_005010X279A1'
  datasetKey: 'avenchart-synthetic-prospective-eligibility-2026-08'
  datasetVersion: 1
  datasetEffectiveFrom: string
  datasetEffectiveThrough: string
  inquiryTraceToken: string
  responseTraceToken: string
  transportOutcome: 'SimulatedAccepted' | 'SimulatedUnavailable'
  memberMatchStatus: 'Matched' | 'NotMatched' | 'Unknown'
  eligibilityStatus: 'Active' | 'Inactive' | 'Unknown'
  benefitInformationStatus: 'Reported' | 'NotReported' | 'Unknown'
  businessOutcome: 'EligibleBenefitsReported' | 'CoverageInactive' | 'SubscriberNotFound' | 'UnableToDetermine'
  memberMatched: boolean
  memberEligibilityChecked: boolean
  memberBenefitsChecked: boolean
  checkedAt: string
  expiresAt: string
  recordedAt: string
  rawTransactionCreated: false
  renderingPhysicianNetworkChecked: false
  coverageVerified: false
  exactNetworkConfirmed: false
  identityProofed: false
  canonicalPatientCreated: false
  chartLinked: false
  portalAccountCreated: false
  prospectiveIntakeCompleted: false
  consentCreated: false
  practiceAccepted: false
  coverageRecordCreated: false
  estimateCreated: false
  financialAcknowledgmentCreated: false
  requestCreated: false
  queueEnabled: false
  appointmentCreated: false
  encounterCreated: false
  careEnabled: false
  prescribingEnabled: false
  billingEnabled: false
  claimCreated: false
  communicationEnabled: false
  integrationEnabled: false
  externalCallPerformed: false
  direction: string
  limitations: string[]
}

export type TelehealthProspectivePracticeNetworkInput = {
  expectedVersion: number
  syntheticDataConfirmed: true
}

export type TelehealthProspectivePracticeNetwork = {
  networkDeterminationId: string
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SyntheticPracticeNetworkRecorded'
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  purposeCategory: 'migraine' | 'sleep'
  planKey: TelehealthProspectivePracticeNetworkOption['planKey']
  payerDisplayName: string
  productDisplayName: string
  practiceDisplayName: string
  dateOfService: string
  serviceCategory: 'ProfessionalTelehealthConsultation'
  eligibilityStatus: 'Active' | 'Inactive' | 'Unknown'
  benefitInformationStatus: 'Reported' | 'NotReported' | 'Unknown'
  eligibilityBusinessOutcome: TelehealthProspectiveEligibility['businessOutcome']
  eligibilityCheckedAt: string
  eligibilityExpiresAt: string
  adapterMode: 'NON_PRODUCTION'
  compatibilityTarget: 'HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0'
  datasetKey: 'avenchart-synthetic-practice-network-directory-2026-08'
  datasetVersion: 1
  datasetEffectiveFrom: string
  datasetEffectiveThrough: string
  sourceLastUpdatedAt: string
  requestTraceToken: string
  responseTraceToken: string
  transportOutcome: 'SimulatedAvailable' | 'SimulatedUnavailable'
  planNetworkMatchStatus: 'Matched' | 'Unknown'
  practiceAffiliationStatus: 'InNetwork' | 'OutOfNetwork' | 'Unknown'
  serviceAvailabilityStatus: 'Included' | 'Excluded' | 'Unknown'
  newPatientAcceptanceStatus: 'Accepting' | 'NotAccepting' | 'Unknown'
  businessOutcome: 'PracticeInNetworkAcceptingNewPatients' | 'PracticeOutOfNetwork' | 'UnableToDetermine'
  practiceNetworkChecked: boolean
  practiceInNetwork: boolean
  newPatientsAccepted: boolean
  networkReference: string | null
  organizationReference: string | null
  locationReference: string | null
  serviceReference: string | null
  checkedAt: string
  expiresAt: string
  recordedAt: string
  fhirResourceCreated: false
  liveDirectoryQueried: false
  renderingPhysicianNetworkChecked: false
  exactNetworkConfirmed: false
  coverageVerified: false
  identityProofed: false
  canonicalPatientCreated: false
  chartLinked: false
  portalAccountCreated: false
  prospectiveIntakeCompleted: false
  consentCreated: false
  practiceAccepted: false
  coverageRecordCreated: false
  estimateCreated: false
  financialAcknowledgmentCreated: false
  requestCreated: false
  queueEnabled: false
  appointmentCreated: false
  encounterCreated: false
  careEnabled: false
  prescribingEnabled: false
  billingEnabled: false
  claimCreated: false
  communicationEnabled: false
  integrationEnabled: false
  externalCallPerformed: false
  direction: string
  limitations: string[]
}

export type TelehealthProspectiveIdentityProofingInput = {
  expectedVersion: number
  privacyNoticeAcknowledged: true
  syntheticDataConfirmed: true
}

export type TelehealthProspectiveIdentityProofing = {
  identityProofingResultId: string
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SyntheticIdentityProofingRecorded'
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  planKey: 'harbor-mutual-hd'
  privacyNoticeKey: 'SYNTHETIC_IDENTITY_PROOFING_NOTICE'
  privacyNoticeVersion: 1
  privacyNoticeAcknowledged: true
  adapterMode: 'NON_PRODUCTION'
  compatibilityTarget: 'NIST_SP_800_63A_4_PROCESS_CONCEPTS_ONLY'
  practiceStatementKey: 'SYNTHETIC_IDENTITY_PRACTICE_STATEMENT'
  practiceStatementVersion: 1
  datasetKey: 'avenchart-synthetic-identity-proofing-2026-08'
  datasetVersion: 1
  datasetEffectiveFrom: string
  datasetEffectiveThrough: string
  sourceLastUpdatedAt: string
  requestTraceToken: string
  responseTraceToken: string
  proofingMethod: 'SYNTHETIC_REMOTE_UNATTENDED_NON_BIOMETRIC'
  transportOutcome: 'SimulatedCompleted'
  evidenceCollectionStatus: 'FixtureReferenceAccepted'
  evidenceValidationStatus: 'ValidatedFixture'
  attributeValidationStatus: 'ValidatedFixture'
  applicantVerificationStatus: 'VerifiedFixture'
  fraudCheckStatus: 'NoIndicatorFixture'
  businessOutcome: 'SyntheticProofingPassed'
  proofingSessionReference: string
  evidencePackageReference: string
  checkedAt: string
  expiresAt: string
  recordedAt: string
  assuranceLevelAchieved: 'None'
  identityEvidenceCollected: false
  governmentIdentifierCollected: false
  biometricDataCollected: false
  authoritativeSourceQueried: false
  proofingNotificationSent: false
  redressCaseCreated: false
  authenticatorBound: false
  identityProofed: false
  canonicalPatientCreated: false
  chartLinked: false
  portalAccountCreated: false
  prospectiveIntakeCompleted: false
  consentCreated: false
  practiceAccepted: false
  coverageRecordCreated: false
  estimateCreated: false
  financialAcknowledgmentCreated: false
  requestCreated: false
  queueEnabled: false
  appointmentCreated: false
  encounterCreated: false
  careEnabled: false
  prescribingEnabled: false
  billingEnabled: false
  claimCreated: false
  communicationEnabled: false
  integrationEnabled: false
  externalCallPerformed: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantIdentityReviewItem = {
  applicantId: string
  version: number
  status: 'IdentityReviewPending'
  legalFirstName: string
  legalLastName: string
  dateOfBirth: string
  maskedEmail: string
  maskedPhone: string
  residenceStateCode: 'GA' | 'CA' | 'FL'
  postalCode: string
  contactVerifiedAt: string
  duplicateDisposition: 'NoCandidate' | 'PossibleMatchManualReview'
  allowedDecision: 'ApprovedForProspectiveIntake' | 'ManualReviewRequired'
  createdAt: string
  expiresAt: string
}

export type TelehealthApplicantIdentityReviewQueue = {
  practiceDisplayName: string
  serverTime: string
  applicants: TelehealthApplicantIdentityReviewItem[]
  limitations: string[]
}

export type TelehealthApplicantPracticeReviewInboxSection = {
  sectionKey: 'Registration' | 'Insurance' | 'CommunicationAccess' | 'DevicePreparation' | 'ClinicalInformation'
  receiptState: 'ReceiptRecorded' | 'PreferencesRecorded' | 'ClientReportedPreparationRecorded' | 'PatientReportedSummaryConfirmed'
  outstandingRoute: string
}

export type TelehealthApplicantPracticeReviewInboxItem = {
  practiceReviewCaseId: string
  applicantVersion: number
  applicantStatus: 'SyntheticPracticeReviewSubmitted'
  reviewStatus: 'PendingPracticeReview'
  legalFirstName: string
  legalLastName: string
  dateOfBirth: string
  maskedEmail: string
  maskedPhone: string
  residenceStateCode: 'GA' | 'CA' | 'FL'
  postalCode: string
  purposeCategory: 'migraine' | 'sleep'
  purposeDisplayLabel: string
  safetyOutcome: 'TelehealthEligible'
  reviewRoute: 'AdditionalClinicalInformationRequired' | 'AssistedPreRequestSupportRequired' | 'PendingPracticePreRequestReview'
  sections: TelehealthApplicantPracticeReviewInboxSection[]
  submittedAt: string
  staffReviewWorkItemExists: true
  staffActionTaken: boolean
  assigned: boolean
  assignedToCurrentUser: boolean
  assignmentExpiresAt: string | null
  priorityAssigned: false
  practiceAccepted: false
  practiceDeclined: false
  patientContacted: false
  clinicianReviewCreated: false
  telehealthRequestCreated: false
  patientCareQueueEntered: false
  clinicianQueueEntered: false
  appointmentCreated: false
  encounterCreated: false
  careAuthorized: false
  prescribingEnabled: false
  billingEnabled: false
  claimCreated: false
  integrationEnabled: false
  externalCallPerformed: false
}

export type TelehealthApplicantPracticeReviewInbox = {
  policyKey: 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_INBOX'
  policyVersion: 1
  practiceDisplayName: string
  serverTime: string
  items: TelehealthApplicantPracticeReviewInboxItem[]
  limitations: string[]
}

export type TelehealthApplicantPracticeReviewClaim = {
  claimId: string
  practiceReviewCaseId: string
  applicantVersion: number
  policyKey: 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_CLAIM'
  policyVersion: 1
  evidenceType: 'PENDING_PRACTICE_REVIEW_SHORT_LEASE_RECEIPT'
  assignedAt: string
  assignmentExpiresAt: string
  assigned: true
  assignedToCurrentUser: true
  staffReviewWorkItemExists: true
  staffActionTaken: true
  priorityAssigned: false
  practiceAccepted: false
  practiceDeclined: false
  patientContacted: false
  clinicianReviewCreated: false
  telehealthRequestCreated: false
  patientCareQueueEntered: false
  clinicianQueueEntered: false
  appointmentCreated: false
  encounterCreated: false
  careAuthorized: false
  prescribingEnabled: false
  billingEnabled: false
  claimCreated: false
  integrationEnabled: false
  externalCallPerformed: false
  limitations: string[]
}

export type TelehealthApplicantPracticeReviewPacket = {
  practiceReviewCaseId: string
  applicantVersion: number
  applicantStatus: 'SyntheticPracticeReviewSubmitted'
  reviewStatus: 'PendingPracticeReview'
  policyKey: 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_PACKET'
  policyVersion: 1
  practiceDisplayName: string
  serverTime: string
  assignmentExpiresAt: string
  legalFirstName: string
  legalLastName: string
  dateOfBirth: string
  maskedEmail: string
  maskedPhone: string
  residenceStateCode: 'GA' | 'CA' | 'FL'
  postalCode: string
  purposeCategory: 'migraine' | 'sleep'
  purposeDisplayLabel: string
  safetyOutcome: 'TelehealthEligible'
  reviewRoute: 'AdditionalClinicalInformationRequired' | 'AssistedPreRequestSupportRequired' | 'PendingPracticePreRequestReview'
  submittedAt: string
  sections: TelehealthApplicantPracticeReviewInboxSection[]
  registration: {
    receiptRecorded: true
    confirmedAt: string
    identityAssuranceEstablished: false
    patientRecordChanged: false
  }
  insurance: {
    payerDisplayName: string
    productDisplayName: string
    memberIdMask: string
    groupNumberMask: string | null
    subscriberRelationship: 'Self' | 'Spouse' | 'Parent' | 'Other'
    coveragePriority: 'Primary'
    eligibilityBusinessOutcome: 'EligibleBenefitsReported'
    eligibilityCheckedAt: string
    eligibilityExpiresAt: string
    eligibilityEvidenceCurrent: boolean
    practiceNetworkBusinessOutcome: 'PracticeInNetworkAcceptingNewPatients'
    practiceNetworkCheckedAt: string
    practiceNetworkExpiresAt: string
    practiceNetworkEvidenceCurrent: boolean
    renderingPhysicianNetworkChecked: false
    confirmedAt: string
    coverageVerified: false
    exactNetworkConfirmed: false
    canonicalCoverageCreated: false
  }
  communicationAccess: {
    preferredSpokenLanguage: 'English' | 'Spanish'
    interpreterRequested: boolean
    accessibilitySupportRequested: boolean
    safePrivateCommunicationConfirmed: true
    recordedAt: string
    interpreterAssigned: false
    accessibilityAccommodationArranged: false
    communicationArrangementCompleted: false
  }
  devicePreparation: {
    browserSupported: true
    cameraAvailable: true
    microphoneAvailable: true
    speakerAvailable: true
    networkQuality: 'Unknown' | 'Good'
    recordedAt: string
    technologyReady: false
    waitingRoomCreated: false
    mediaSessionCreated: false
  }
  clinicalInformationSummaryRoute: string
  clinicalInformationSummaryConfirmedAt: string
  staffReviewWorkItemExists: true
  staffActionTaken: true
  assigned: true
  assignedToCurrentUser: true
  priorityAssigned: false
  practiceAccepted: false
  practiceDeclined: false
  patientContacted: false
  clinicianReviewCreated: false
  telehealthRequestCreated: false
  patientCareQueueEntered: false
  clinicianQueueEntered: false
  appointmentCreated: false
  encounterCreated: false
  careAuthorized: false
  prescribingEnabled: false
  billingEnabled: false
  claimCreated: false
  integrationEnabled: false
  externalCallPerformed: false
  limitations: string[]
}

export type TelehealthApplicantPracticeReviewAuthorization = {
  practiceReviewCaseId: string
  applicantVersion: number
  applicantStatus: 'SyntheticPracticeReviewAuthorized'
  decision: 'AuthorizedForSyntheticRequestCreation'
  rationaleCode: 'OperationalPrerequisitesReviewed'
  policyKey: 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_AUTHORIZATION'
  policyVersion: 1
  evidenceType: 'CURRENT_CLAIMANT_MINIMIZED_PACKET_REVIEW_ONLY'
  decidedAt: string
  requestCreationAuthorized: true
  practiceAccepted: false
  patientContacted: false
  clinicianReviewCreated: false
  telehealthRequestCreated: false
  patientCareQueueEntered: false
  clinicianQueueEntered: false
  appointmentCreated: false
  encounterCreated: false
  consentCreated: false
  careAuthorized: false
  prescribingEnabled: false
  billingEnabled: false
  claimCreated: false
  integrationEnabled: false
  externalCallPerformed: false
  limitations: string[]
}

export type TelehealthApplicantIdentityReviewDecision = {
  decisionId: string
  applicantId: string
  applicantVersion: number
  applicantStatus: 'IdentityReviewApproved' | 'ManualReviewRequired'
  decision: 'ApprovedForProspectiveIntake' | 'ManualReviewRequired'
  reason: string
  policyKey: 'SYNTHETIC_STAFF_IDENTITY_REVIEW'
  policyVersion: 1
  evidenceType: 'CONTACT_CONTROL_AND_DUPLICATE_DISPOSITION_ONLY'
  decidedAt: string
  identityProofed: false
  canonicalPatientCreated: false
  chartLinked: false
  prospectiveIntakeCompleted: false
  requestCreated: false
  queueEnabled: false
  limitations: string[]
}

export type TelehealthApplicantPromotionAuthorizationDecisionType =
  | 'AuthorizedForSyntheticPromotion'
  | 'DeniedForSyntheticPromotion'

export type TelehealthApplicantPromotionAuthorizationItem = {
  applicantId: string
  version: number
  status: 'SyntheticIdentityProofingRecorded'
  legalFirstName: string
  legalLastName: string
  dateOfBirth: string
  maskedEmail: string
  maskedPhone: string
  residenceStateCode: 'GA' | 'CA' | 'FL'
  postalCode: string
  planKey: string
  payerDisplayName: string
  productDisplayName: string
  eligibilityStatus: 'Active'
  benefitInformationStatus: 'Reported'
  eligibilityBusinessOutcome: 'EligibleBenefitsReported'
  networkBusinessOutcome: 'PracticeInNetworkAcceptingNewPatients'
  proofingMethod: string
  transportOutcome: string
  evidenceCollectionStatus: string
  evidenceValidationStatus: string
  attributeValidationStatus: string
  applicantVerificationStatus: string
  fraudCheckStatus: string
  proofingBusinessOutcome: 'SyntheticProofingPassed'
  assuranceLevelAchieved: 'None'
  identityProofed: false
  proofingCheckedAt: string
  proofingExpiresAt: string
  createdAt: string
  applicantExpiresAt: string
  allowedDecisions: TelehealthApplicantPromotionAuthorizationDecisionType[]
}

export type TelehealthApplicantPromotionAuthorizationQueue = {
  practiceDisplayName: string
  serverTime: string
  applicants: TelehealthApplicantPromotionAuthorizationItem[]
  limitations: string[]
}

export type TelehealthApplicantPromotionAuthorizationDecision = {
  decisionId: string
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SyntheticPromotionAuthorized' | 'SyntheticPromotionDenied'
  decision: TelehealthApplicantPromotionAuthorizationDecisionType
  reason: string
  policyKey: 'SYNTHETIC_PROSPECTIVE_PROMOTION_AUTHORIZATION'
  policyVersion: 1
  evidenceType: 'COMPLETE_SYNTHETIC_INTAKE_AND_PROCESS_STATUS_ONLY'
  decidedAt: string
  noneAssuranceAcknowledged: true
  realIdentityProofed: false
  canonicalPatientCreated: false
  chartLinked: false
  portalAccountCreated: false
  prospectiveIntakeCompleted: false
  consentCreated: false
  practiceAccepted: false
  requestCreated: false
  queueEnabled: false
  limitations: string[]
}

export type TelehealthApplicantSyntheticPromotionItem = {
  applicantId: string
  version: number
  status: 'SyntheticPromotionAuthorized'
  legalFirstName: string
  legalLastName: string
  dateOfBirth: string
  maskedEmail: string
  maskedPhone: string
  residenceStateCode: 'GA' | 'CA' | 'FL'
  postalCode: string
  authorizationDecision: 'AuthorizedForSyntheticPromotion'
  authorizedAt: string
  assuranceLevelAchieved: 'None'
  identityProofed: false
  applicantExpiresAt: string
  proofingExpiresAt: string
  allowedCommand: 'PromoteAuthorizedSyntheticApplicant'
}

export type TelehealthApplicantSyntheticPromotionQueue = {
  practiceDisplayName: string
  serverTime: string
  applicants: TelehealthApplicantSyntheticPromotionItem[]
  limitations: string[]
}

export type TelehealthApplicantSyntheticPromotion = {
  promotionId: string
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SyntheticPatientPromoted' | 'SyntheticPromotionBlockedPossibleMatch'
  outcome: 'SyntheticPatientCreated' | 'BlockedPossiblePatientMatch'
  possibleMatchDetected: boolean
  canonicalPatientCreated: boolean
  policyKey: 'SYNTHETIC_PROSPECTIVE_PATIENT_PROMOTION'
  policyVersion: 1
  evidenceType: 'AUTHORIZED_SYNTHETIC_APPLICANT_AND_CURRENT_DUPLICATE_RECHECK'
  executedAt: string
  portalAccountCreated: false
  prospectiveIntakeCompleted: false
  consentCreated: false
  practiceAccepted: false
  insuranceCreated: false
  requestCreated: false
  queueEnabled: false
  careEnabled: false
  limitations: string[]
}

export type TelehealthApplicantNoticeInput = {
  expectedVersion: number
  noticeKey: 'GA_TELEHEALTH_NOTICE_V1' | 'CA_TELEHEALTH_NOTICE_V1' | 'FL_TELEHEALTH_NOTICE_V1'
  noticeVersion: 1
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  currentLocationConfirmed: true
  modeOfCareAcknowledged: true
  privacyLimitationsAcknowledged: true
  emergencyInstructionsAcknowledged: true
  inPersonOptionAcknowledged: true
  clinicianReconfirmationRequiredAcknowledged: true
  syntheticDataConfirmed: true
}

export type TelehealthApplicantNotice = {
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SyntheticPatientPromoted' | 'SyntheticTelehealthNoticeAcknowledged'
  noticeKey: TelehealthApplicantNoticeInput['noticeKey']
  noticeVersion: 1
  currentLocationStateCode: TelehealthApplicantNoticeInput['currentLocationStateCode']
  title: string
  summary: string
  sourceTitle: string
  sourceUrl: string
  disclosures: string[]
  deferredRequirements: string[]
  policyKey: 'SYNTHETIC_TELEHEALTH_NOTICE_ACKNOWLEDGMENT'
  policyVersion: 1
  legalReviewStatus: 'PendingIndependentReview'
  acknowledged: boolean
  acknowledgedAt: string | null
  legalConsentEstablished: false
  clinicianConsentDocumented: false
  clinicianReconfirmationRequired: true
  portalAccountCreated: false
  intakeCompleted: false
  practiceAccepted: false
  insuranceCreated: false
  requestCreated: false
  queueEnabled: false
  careEnabled: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantRegistrationDetailsInput = {
  expectedVersion: number
  detailsFingerprint: string
  legalNameAndBirthDateConfirmed: true
  contactChannelsConfirmed: true
  residenceRegionConfirmed: true
  noCorrectionsNeededConfirmed: true
  syntheticDataConfirmed: true
}

export type TelehealthApplicantRegistrationDetails = {
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SyntheticTelehealthNoticeAcknowledged' | 'SyntheticMinimumRegistrationDetailsConfirmed'
  legalFirstName: string
  legalLastName: string
  dateOfBirth: string
  maskedEmail: string
  maskedPhone: string
  residenceStateCode: 'GA' | 'CA' | 'FL'
  postalCode: string
  detailsFingerprint: string
  policyKey: 'SYNTHETIC_MINIMUM_REGISTRATION_DETAILS_CONFIRMATION'
  policyVersion: 1
  confirmed: boolean
  confirmedAt: string | null
  identityAssuranceEstablished: false
  patientRecordChanged: false
  correctionCompleted: false
  intakeCompleted: false
  legalConsentEstablished: false
  practiceAccepted: false
  insuranceConfirmed: false
  coverageCreated: false
  requestCreated: false
  queueEnabled: false
  careEnabled: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantInsuranceHandoffInput = {
  expectedVersion: number
  insuranceSnapshotFingerprint: string
  payerAndProductConfirmed: true
  maskedMemberDetailsConfirmed: true
  subscriberRelationshipConfirmed: true
  evidenceLimitationsAcknowledged: true
  syntheticDataConfirmed: true
}

export type TelehealthApplicantInsuranceHandoff = {
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SyntheticMinimumRegistrationDetailsConfirmed' | 'SyntheticInsuranceDetailsConfirmed'
  payerDisplayName: string
  productDisplayName: string
  memberIdMask: string
  groupNumberMask: string | null
  subscriberRelationship: 'Self' | 'Spouse' | 'Parent' | 'Other'
  coveragePriority: 'Primary'
  eligibilityBusinessOutcome: string
  eligibilityCheckedAt: string
  eligibilityExpiresAt: string
  eligibilityEvidenceCurrent: boolean
  practiceNetworkBusinessOutcome: string
  practiceNetworkCheckedAt: string
  practiceNetworkExpiresAt: string
  practiceNetworkEvidenceCurrent: boolean
  renderingPhysicianNetworkChecked: false
  insuranceSnapshotFingerprint: string
  policyKey: 'SYNTHETIC_INSURANCE_HANDOFF_CONFIRMATION'
  policyVersion: 1
  insuranceDetailsConfirmed: boolean
  confirmedAt: string | null
  coverageVerified: false
  exactNetworkConfirmed: false
  canonicalCoverageCreated: false
  patientRecordChanged: false
  portalAccessEnabled: false
  intakeCompleted: false
  legalConsentEstablished: false
  practiceAccepted: false
  requestCreated: false
  queueEnabled: false
  careEnabled: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantCommunicationAccessReadinessInput = {
  expectedVersion: number
  contextSnapshotFingerprint: string
  preferredSpokenLanguage: 'English' | 'Spanish'
  interpreterRequested: boolean
  accessibilitySupportRequested: boolean
  currentLocationConfirmed: true
  callbackNumberConfirmed: true
  safeAndPrivateToCommunicateConfirmed: true
  disconnectionAndEmergencyPlanAcknowledged: true
  syntheticDataConfirmed: true
}

export type TelehealthApplicantCommunicationAccessReadiness = {
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SyntheticInsuranceDetailsConfirmed' | 'SyntheticCommunicationAccessReadinessRecorded'
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  maskedCallbackPhone: string
  supportedSpokenLanguages: Array<'English' | 'Spanish'>
  preferredSpokenLanguage: 'English' | 'Spanish' | null
  interpreterRequested: boolean | null
  accessibilitySupportRequested: boolean | null
  contextSnapshotFingerprint: string
  policyKey: 'SYNTHETIC_COMMUNICATION_ACCESS_READINESS'
  policyVersion: 1
  readinessRecorded: boolean
  recordedAt: string | null
  safeAndPrivateToCommunicateConfirmed: boolean
  interpreterAssigned: false
  accessibilityAccommodationArranged: false
  communicationArrangementCompleted: false
  supportRequestCreated: false
  technologyReadinessCompleted: false
  patientRecordChanged: false
  intakeCompleted: false
  legalConsentEstablished: false
  practiceAccepted: false
  requestCreated: false
  queueEnabled: false
  careEnabled: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantDevicePreparationInput = {
  expectedVersion: number
  preparationSnapshotFingerprint: string
  browserSupported: true
  cameraAvailable: true
  microphoneAvailable: true
  speakerAvailable: true
  networkQuality: 'Unknown' | 'Good'
  clientReportedResultAcknowledged: true
  noReadinessGuaranteeAcknowledged: true
  recheckBeforeConsultationAcknowledged: true
}

export type TelehealthApplicantDevicePreparation = {
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SyntheticCommunicationAccessReadinessRecorded' | 'SyntheticDevicePreparationRecorded'
  supportedNetworkQualities: Array<'Unknown' | 'Good'>
  preparationSnapshotFingerprint: string
  policyKey: 'SYNTHETIC_APPLICANT_DEVICE_PREPARATION'
  policyVersion: 1
  preparationRecorded: boolean
  recordedAt: string | null
  browserSupported: boolean | null
  cameraAvailable: boolean | null
  microphoneAvailable: boolean | null
  speakerAvailable: boolean | null
  networkQuality: 'Unknown' | 'Good' | null
  clientReportedResultAcknowledged: boolean
  noReadinessGuaranteeAcknowledged: boolean
  recheckBeforeConsultationAcknowledged: boolean
  technologyReady: false
  waitingRoomCreated: false
  mediaSessionCreated: false
  communicationStarted: false
  supportArrangementCompleted: false
  patientRecordChanged: false
  intakeCompleted: false
  legalConsentEstablished: false
  practiceAccepted: false
  requestCreated: false
  queueEntered: false
  careAuthorized: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantClinicalInformationCategoryStatus =
  | 'PatientReportsNone'
  | 'ItemsToReview'
  | 'Unsure'

export type TelehealthApplicantClinicalInformationInventoryInput = {
  expectedVersion: number
  inventorySnapshotFingerprint: string
  medicationsStatus: TelehealthApplicantClinicalInformationCategoryStatus
  allergiesOrIntolerancesStatus: TelehealthApplicantClinicalInformationCategoryStatus
  otherHealthHistoryStatus: TelehealthApplicantClinicalInformationCategoryStatus
  patientReportedMayBeIncompleteAcknowledged: true
  noClinicalDetailsCapturedAcknowledged: true
  clinicianReconciliationRequiredAcknowledged: true
}

export type TelehealthApplicantClinicalInformationInventory = {
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SyntheticDevicePreparationRecorded' | 'SyntheticClinicalInformationInventoryRecorded'
  supportedCategoryStatuses: TelehealthApplicantClinicalInformationCategoryStatus[]
  inventorySnapshotFingerprint: string
  policyKey: 'SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_INVENTORY'
  policyVersion: 1
  inventoryRecorded: boolean
  recordedAt: string | null
  medicationsStatus: TelehealthApplicantClinicalInformationCategoryStatus | null
  allergiesOrIntolerancesStatus: TelehealthApplicantClinicalInformationCategoryStatus | null
  otherHealthHistoryStatus: TelehealthApplicantClinicalInformationCategoryStatus | null
  reviewRoute: 'DetailedCollectionRequired' | 'AssistedReviewRequired' | 'PendingClinicianReconciliation' | null
  patientReportedMayBeIncompleteAcknowledged: boolean
  noClinicalDetailsCapturedAcknowledged: boolean
  clinicianReconciliationRequiredAcknowledged: boolean
  medicationListReconciled: false
  allergyListReconciled: false
  healthHistoryReconciled: false
  clinicalIntakeCompleted: false
  clinicalEligibilityEstablished: false
  clinicianReviewCreated: false
  patientRecordChanged: false
  requestCreated: false
  queueEntered: false
  careAuthorized: false
  prescribingEnabled: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantMedicationCatalogItem = {
  catalogKey: 'acetaminophen' | 'ibuprofen' | 'sumatriptan' | 'melatonin' | 'lisinopril' | 'metformin'
  displayName: string
  catalogVersion: 1
  codingSystem: 'LOCAL_SYNTHETIC_ONLY'
  rxNormMapped: false
  sortOrder: number
}

export type TelehealthApplicantReportedMedicationUseStatus = 'Taking' | 'NotTaking' | 'Unsure'

export type TelehealthApplicantMedicationInformationInput = {
  expectedVersion: number
  medicationInformationSnapshotFingerprint: string
  medicationItems: Array<{
    catalogKey: TelehealthApplicantMedicationCatalogItem['catalogKey']
    reportedUseStatus: TelehealthApplicantReportedMedicationUseStatus
  }>
  additionalOrUnlistedItemsReported: boolean
  patientReportedMayBeIncompleteAcknowledged: true
  syntheticCatalogIncompleteAcknowledged: true
  noDoseOrDirectionsCapturedAcknowledged: true
  clinicianReconciliationRequiredAcknowledged: true
}

export type TelehealthApplicantMedicationInformation = {
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SyntheticClinicalInformationInventoryRecorded' | 'SyntheticMedicationInformationRecorded'
  inventoryMedicationsStatus: TelehealthApplicantClinicalInformationCategoryStatus
  medicationInformationSnapshotFingerprint: string
  policyKey: 'SYNTHETIC_APPLICANT_MEDICATION_INFORMATION'
  policyVersion: 1
  catalogKey: string
  catalogVersion: 1
  codingSystem: 'LOCAL_SYNTHETIC_ONLY'
  catalogComplete: false
  catalogItems: TelehealthApplicantMedicationCatalogItem[]
  supportedReportedUseStatuses: TelehealthApplicantReportedMedicationUseStatus[]
  medicationInformationRecorded: boolean
  recordedAt: string | null
  medicationItems: Array<Omit<TelehealthApplicantMedicationCatalogItem, 'sortOrder'> & {
    reportedUseStatus: TelehealthApplicantReportedMedicationUseStatus
  }>
  additionalOrUnlistedItemsReported: boolean
  reviewRoute: 'AdditionalMedicationCollectionRequired' | 'ClinicianMedicationReviewRequired' | 'AssistedMedicationReviewRequired' | 'PendingClinicianConfirmationOfNone' | null
  patientReportedMayBeIncompleteAcknowledged: boolean
  syntheticCatalogIncompleteAcknowledged: boolean
  noDoseOrDirectionsCapturedAcknowledged: boolean
  clinicianReconciliationRequiredAcknowledged: boolean
  medicationStatementCreated: false
  medicationRequestCreated: false
  medicationListReconciled: false
  interactionCheckPerformed: false
  clinicianReviewCreated: false
  clinicalIntakeCompleted: false
  clinicalEligibilityEstablished: false
  patientRecordChanged: false
  requestCreated: false
  queueEntered: false
  careAuthorized: false
  prescribingEnabled: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantAllergyCatalogItem = {
  catalogKey: 'amoxicillin' | 'ibuprofen' | 'peanut' | 'shellfish' | 'latex' | 'bee-venom'
  displayName: string
  category: 'Medication' | 'Food' | 'Environment'
  catalogVersion: 1
  codingSystem: 'LOCAL_SYNTHETIC_ONLY'
  snomedCtMapped: false
  rxNormMapped: false
  sortOrder: number
}

export type TelehealthApplicantAllergyInformationInput = {
  expectedVersion: number
  allergyInformationSnapshotFingerprint: string
  allergyItems: Array<{ catalogKey: TelehealthApplicantAllergyCatalogItem['catalogKey'] }>
  additionalOrUnlistedItemsReported: boolean
  patientReportedMayBeIncompleteAcknowledged: true
  syntheticCatalogIncompleteAcknowledged: true
  noReactionOrCriticalityCapturedAcknowledged: true
  clinicianVerificationRequiredAcknowledged: true
}

export type TelehealthApplicantAllergyInformation = {
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SyntheticMedicationInformationRecorded' | 'SyntheticAllergyInformationRecorded'
  inventoryAllergiesOrIntolerancesStatus: TelehealthApplicantClinicalInformationCategoryStatus
  allergyInformationSnapshotFingerprint: string
  policyKey: 'SYNTHETIC_APPLICANT_ALLERGY_INFORMATION'
  policyVersion: 1
  catalogKey: string
  catalogVersion: 1
  codingSystem: 'LOCAL_SYNTHETIC_ONLY'
  catalogComplete: false
  catalogItems: TelehealthApplicantAllergyCatalogItem[]
  allergyInformationRecorded: boolean
  recordedAt: string | null
  allergyItems: Array<Omit<TelehealthApplicantAllergyCatalogItem, 'sortOrder'>>
  additionalOrUnlistedItemsReported: boolean
  reviewRoute: 'AdditionalAllergyCollectionRequired' | 'ClinicianAllergyReviewRequired' | 'AssistedAllergyReviewRequired' | 'PendingClinicianConfirmationOfPatientReportedNone' | null
  patientReportedMayBeIncompleteAcknowledged: boolean
  syntheticCatalogIncompleteAcknowledged: boolean
  noReactionOrCriticalityCapturedAcknowledged: boolean
  clinicianVerificationRequiredAcknowledged: boolean
  allergyIntoleranceCreated: false
  allergyListReconciled: false
  reactionAssessed: false
  criticalityAssessed: false
  contraindicationCheckPerformed: false
  clinicianReviewCreated: false
  clinicalIntakeCompleted: false
  clinicalEligibilityEstablished: false
  patientRecordChanged: false
  requestCreated: false
  queueEntered: false
  careAuthorized: false
  prescribingEnabled: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantHealthHistoryTopic = {
  catalogKey: 'ongoing-health-conditions' | 'prior-surgery-or-hospital-stay' | 'pregnancy-or-postpartum-information' | 'immune-system-or-active-cancer-treatment' | 'behavioral-health-or-substance-use' | 'family-health-history'
  displayName: string
  category: 'ConditionOrConcern' | 'ProcedureOrHospitalization' | 'HealthStatus' | 'RiskContext' | 'SensitiveHistory' | 'FamilyHistory'
  catalogVersion: 1
  codingSystem: 'LOCAL_SYNTHETIC_ONLY'
  snomedCtMapped: false
  icd10CmMapped: false
  loincMapped: false
  sortOrder: number
}

export type TelehealthApplicantHealthHistoryInformationInput = {
  expectedVersion: number
  healthHistoryInformationSnapshotFingerprint: string
  healthHistoryTopics: Array<{ catalogKey: TelehealthApplicantHealthHistoryTopic['catalogKey'] }>
  additionalOrUnlistedTopicsReported: boolean
  patientReportedMayBeIncompleteAcknowledged: true
  topicSelectionIsNotDiagnosisAcknowledged: true
  noStatusOrTimingCapturedAcknowledged: true
  clinicianVerificationRequiredAcknowledged: true
}

export type TelehealthApplicantHealthHistoryInformation = {
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SyntheticAllergyInformationRecorded' | 'SyntheticHealthHistoryInformationRecorded'
  inventoryOtherHealthHistoryStatus: TelehealthApplicantClinicalInformationCategoryStatus
  healthHistoryInformationSnapshotFingerprint: string
  policyKey: 'SYNTHETIC_APPLICANT_HEALTH_HISTORY_INFORMATION'
  policyVersion: 1
  catalogKey: string
  catalogVersion: 1
  codingSystem: 'LOCAL_SYNTHETIC_ONLY'
  catalogComplete: false
  catalogTopics: TelehealthApplicantHealthHistoryTopic[]
  healthHistoryInformationRecorded: boolean
  recordedAt: string | null
  healthHistoryTopics: Array<Omit<TelehealthApplicantHealthHistoryTopic, 'sortOrder'>>
  additionalOrUnlistedTopicsReported: boolean
  reviewRoute: 'AdditionalHealthHistoryCollectionRequired' | 'ClinicianHealthHistoryReviewRequired' | 'AssistedHealthHistoryReviewRequired' | 'PendingClinicianConfirmationOfPatientReportedNone' | null
  patientReportedMayBeIncompleteAcknowledged: boolean
  topicSelectionIsNotDiagnosisAcknowledged: boolean
  noStatusOrTimingCapturedAcknowledged: boolean
  clinicianVerificationRequiredAcknowledged: boolean
  conditionCreated: false
  procedureCreated: false
  observationCreated: false
  familyMemberHistoryCreated: false
  questionnaireResponseCreated: false
  healthHistoryReconciled: false
  riskModifierEvaluated: false
  clinicalTriageChanged: false
  clinicianReviewCreated: false
  clinicalIntakeCompleted: false
  clinicalEligibilityEstablished: false
  patientRecordChanged: false
  requestCreated: false
  queueEntered: false
  careAuthorized: false
  prescribingEnabled: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantClinicalInformationSummaryInput = {
  expectedVersion: number
  clinicalInformationSummarySnapshotFingerprint: string
  patientReportedMayBeIncompleteAcknowledged: true
  notClinicallyVerifiedOrReconciledAcknowledged: true
  noIntakeCompletionOrEligibilityAcknowledged: true
  correctionRequiresSeparateWorkflowAcknowledged: true
}

export type TelehealthApplicantClinicalInformationSummaryCategory = {
  categoryKey: 'Medications' | 'AllergiesOrIntolerances' | 'OtherHealthHistory'
  inventoryStatus: TelehealthApplicantClinicalInformationCategoryStatus
  selectedItemCount: number
  additionalOrUnlistedItemsReported: boolean
  reviewRoute: string
}

export type TelehealthApplicantClinicalInformationSummary = {
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SyntheticHealthHistoryInformationRecorded' | 'SyntheticClinicalInformationSummaryConfirmed'
  clinicalInformationSummarySnapshotFingerprint: string
  policyKey: 'SYNTHETIC_APPLICANT_CLINICAL_INFORMATION_SUMMARY'
  policyVersion: 1
  categories: TelehealthApplicantClinicalInformationSummaryCategory[]
  summaryRoute: 'AdditionalClinicalInformationCollectionRequired' | 'AssistedClinicalInformationReviewRequired' | 'ClinicianClinicalInformationReviewRequired' | 'PendingClinicianReconciliationOfPatientReportedNone'
  summaryConfirmed: boolean
  confirmedAt: string | null
  patientReportedMayBeIncompleteAcknowledged: boolean
  notClinicallyVerifiedOrReconciledAcknowledged: boolean
  noIntakeCompletionOrEligibilityAcknowledged: boolean
  correctionRequiresSeparateWorkflowAcknowledged: boolean
  questionnaireResponseCreated: false
  medicationListReconciled: false
  allergyListReconciled: false
  healthHistoryReconciled: false
  confirmedNegativeEstablished: false
  clinicianReviewCreated: false
  clinicalIntakeCompleted: false
  clinicalEligibilityEstablished: false
  clinicalTriageChanged: false
  patientRecordChanged: false
  practiceAccepted: false
  requestCreated: false
  queueEntered: false
  careAuthorized: false
  prescribingEnabled: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantPreRequestReadinessInput = {
  expectedVersion: number
  preRequestReadinessSnapshotFingerprint: string
  priorSectionsReviewedAcknowledged: true
  outstandingStepsRemainAcknowledged: true
  noRequestOrQueueCreatedAcknowledged: true
  correctionRequiresSeparateWorkflowAcknowledged: true
}

export type TelehealthApplicantPreRequestReadinessSection = {
  sectionKey: 'Registration' | 'Insurance' | 'CommunicationAccess' | 'DevicePreparation' | 'ClinicalInformation'
  receiptState: 'ReceiptRecorded' | 'PreferencesRecorded' | 'ClientReportedPreparationRecorded' | 'PatientReportedSummaryConfirmed'
  outstandingRoute: string
}

export type TelehealthApplicantPreRequestReadiness = {
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SyntheticClinicalInformationSummaryConfirmed' | 'SyntheticPreRequestReadinessAcknowledged'
  preRequestReadinessSnapshotFingerprint: string
  policyKey: 'SYNTHETIC_APPLICANT_PRE_REQUEST_READINESS'
  policyVersion: 1
  sections: TelehealthApplicantPreRequestReadinessSection[]
  overallRoute: 'AdditionalClinicalInformationRequired' | 'AssistedPreRequestSupportRequired' | 'PendingPracticePreRequestReview'
  readinessAcknowledged: boolean
  acknowledgedAt: string | null
  priorSectionsReviewedAcknowledged: boolean
  outstandingStepsRemainAcknowledged: boolean
  noRequestOrQueueCreatedAcknowledged: boolean
  correctionRequiresSeparateWorkflowAcknowledged: boolean
  identityAssuranceEstablished: false
  coverageGuaranteed: false
  renderingClinicianNetworkVerified: false
  interpreterOrAccommodationArranged: false
  technologyReady: false
  clinicalInformationReconciled: false
  clinicalIntakeCompleted: false
  clinicalEligibilityEstablished: false
  legalConsentEstablished: false
  staffReviewCreated: false
  clinicianReviewCreated: false
  practiceAccepted: false
  patientRecordChanged: false
  requestCreated: false
  queueEntered: false
  appointmentCreated: false
  encounterCreated: false
  careAuthorized: false
  prescribingEnabled: false
  billingEnabled: false
  claimCreated: false
  integrationEnabled: false
  externalCallPerformed: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantPracticeReviewInput = {
  expectedVersion: number
  practiceReviewSnapshotFingerprint: string
  patientReportedInformationAcknowledged: true
  practiceMayRequestInformationOrDeclineAcknowledged: true
  noTelehealthRequestOrCareQueueAcknowledged: true
  worseningSymptomsRequireImmediateActionAcknowledged: true
}

export type TelehealthApplicantPracticeReview = {
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SyntheticPreRequestReadinessAcknowledged' | 'SyntheticPracticeReviewSubmitted'
  practiceReviewSnapshotFingerprint: string
  policyKey: 'SYNTHETIC_APPLICANT_PRACTICE_REVIEW_SUBMISSION'
  policyVersion: 1
  reviewRoute: 'AdditionalClinicalInformationRequired' | 'AssistedPreRequestSupportRequired' | 'PendingPracticePreRequestReview'
  submissionReady: boolean
  submitted: boolean
  practiceReviewCaseId: string | null
  reviewStatus: 'PendingPracticeReview' | null
  submittedAt: string | null
  patientReportedInformationAcknowledged: boolean
  practiceMayRequestInformationOrDeclineAcknowledged: boolean
  noTelehealthRequestOrCareQueueAcknowledged: boolean
  worseningSymptomsRequireImmediateActionAcknowledged: boolean
  staffReviewCreated: boolean
  clinicianReviewCreated: false
  practiceAccepted: false
  patientRecordChanged: false
  telehealthRequestCreated: false
  patientCareQueueEntered: false
  clinicianQueueEntered: false
  appointmentCreated: false
  encounterCreated: false
  careAuthorized: false
  prescribingEnabled: false
  billingEnabled: false
  claimCreated: false
  integrationEnabled: false
  externalCallPerformed: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantRequestCreationInput = {
  expectedApplicantVersion: number
  authorizationPolicyVersion: 1
  requestCreationConfirmed: true
  noQueueOrCareAcknowledged: true
  urgentOrWorseningSymptomsRequireImmediateActionAcknowledged: true
}

export type TelehealthApplicantRequestCreation = {
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SyntheticPracticeReviewAuthorized' | 'SyntheticRequestCreated'
  policyKey: 'SYNTHETIC_APPLICANT_TELEHEALTH_REQUEST_CREATION'
  policyVersion: 1
  authorizationPolicyVersion: 1
  requestCreationReady: boolean
  requestCreated: boolean
  requestId: string | null
  requestStatus: 'Draft' | null
  requestVersion: 1 | null
  complaintCategory: 'migraine' | 'sleep'
  createdAt: string | null
  telehealthRequestCreated: boolean
  patientContacted: false
  patientCareQueueEntered: false
  clinicianQueueEntered: false
  doctorSearchStarted: false
  queuePositionAssigned: false
  appointmentCreated: false
  encounterCreated: false
  consentCreated: false
  careAuthorized: false
  prescribingEnabled: false
  billingEnabled: false
  claimCreated: false
  integrationEnabled: false
  externalCallPerformed: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantRequestLocationInput = {
  expectedRequestVersion: number
  contextSnapshotFingerprint: string
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  currentLocationConfirmed: true
  callbackNumberConfirmed: true
  changedLocationRequiresRestartAcknowledged: true
  urgentOrWorseningSymptomsRequireImmediateActionAcknowledged: true
}

export type TelehealthApplicantRequestLocation = {
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SyntheticRequestCreated'
  requestId: string
  requestVersion: 1 | 2
  requestStatus: 'Draft' | 'LocationConfirmed'
  policyKey: 'SYNTHETIC_APPLICANT_REQUEST_LOCATION_CONFIRMATION'
  policyVersion: 1
  contextSnapshotFingerprint: string
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  maskedCallbackPhone: string
  confirmationReady: boolean
  locationConfirmed: boolean
  confirmedAt: string | null
  triageAssessmentCreated: false
  clinicalReviewCreated: false
  patientContacted: false
  patientCareQueueEntered: false
  clinicianQueueEntered: false
  doctorSearchStarted: false
  queuePositionAssigned: false
  appointmentCreated: false
  encounterCreated: false
  consentCreated: false
  careAuthorized: false
  prescribingEnabled: false
  billingEnabled: false
  claimCreated: false
  integrationEnabled: false
  externalCallPerformed: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantRequestUniversalSafetyInput = {
  expectedRequestVersion: number
  contextSnapshotFingerprint: string
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  currentLocationConfirmed: true
  callbackNumberConfirmed: true
  syntheticDataConfirmed: true
  hasEmergencyWarning: boolean
  severeOrWorsening: boolean
  requiresHandsOnExam: boolean
  unsure: boolean
}

export type TelehealthApplicantRequestUniversalSafety = {
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SyntheticRequestCreated'
  requestId: string
  requestVersion: 2 | 3
  requestStatus: 'LocationConfirmed' | 'SafetyScreening' | 'EmergencyRedirected' | 'InPersonRecommended' | 'ClinicalReview'
  policyKey: 'SYNTHETIC_APPLICANT_REQUEST_UNIVERSAL_SAFETY_ASSESSMENT'
  policyVersion: 1
  protocolKey: 'synthetic-universal-safety'
  protocolVersion: 1
  contextSnapshotFingerprint: string
  contextExpiresAt: string
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  maskedCallbackPhone: string
  assessmentReady: boolean
  assessmentCreated: boolean
  outcome: 'Emergency' | 'UrgentInPerson' | 'InPersonRequired' | 'ClinicalReview' | 'TelehealthEligible' | null
  publicDisposition: 'EmergencyCareNow' | 'PromptInPersonCare' | 'InPersonCareRequired' | 'ClinicalReviewRequired' | 'UniversalSafetyPassed' | null
  evaluatedAt: string | null
  universalSafetyPassed: boolean
  complaintSpecificTriageRequired: boolean
  complaintSpecificTriageCreated: false
  clinicalReviewRequired: boolean
  clinicalReviewCreated: false
  terminalForTelehealth: boolean
  patientContacted: false
  patientCareQueueEntered: false
  clinicianQueueEntered: false
  doctorSearchStarted: false
  queuePositionAssigned: false
  appointmentCreated: false
  encounterCreated: false
  consentCreated: false
  careAuthorized: false
  prescribingEnabled: false
  billingEnabled: false
  claimCreated: false
  integrationEnabled: false
  externalCallPerformed: false
  direction: string
  limitations: string[]
}

export type TelehealthSyntheticComplaintAnswer = 'Yes' | 'No' | 'NotSure'

export type TelehealthSyntheticMigraineComplaintTriageAnswers = {
  suddenOrWorstOnset: TelehealthSyntheticComplaintAnswer
  newNeurologicOrVisionChange: TelehealthSyntheticComplaintAnswer
  feverOrStiffNeck: TelehealthSyntheticComplaintAnswer
  recentHeadInjury: TelehealthSyntheticComplaintAnswer
  pregnantOrPostpartum: TelehealthSyntheticComplaintAnswer
  cancerOrImmunocompromised: TelehealthSyntheticComplaintAnswer
  knownSimilarPattern: TelehealthSyntheticComplaintAnswer
  persistentVomiting: TelehealthSyntheticComplaintAnswer
}

export type TelehealthSyntheticSleepComplaintTriageAnswers = {
  selfHarmThoughts: TelehealthSyntheticComplaintAnswer
  maniaOrPsychosis: TelehealthSyntheticComplaintAnswer
  dangerousSomnolence: TelehealthSyntheticComplaintAnswer
  withdrawalConcern: TelehealthSyntheticComplaintAnswer
  breathingPausesOrSevereSnoring: TelehealthSyntheticComplaintAnswer
  pregnantOrComplexMedicationConcern: TelehealthSyntheticComplaintAnswer
  controlledSedativeRequest: TelehealthSyntheticComplaintAnswer
  uncomplicatedSleepDifficulty: TelehealthSyntheticComplaintAnswer
}

export type TelehealthApplicantRequestComplaintTriageInput = {
  expectedRequestVersion: number
  contextSnapshotFingerprint: string
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  currentLocationConfirmed: true
  callbackNumberConfirmed: true
  syntheticDataConfirmed: true
  migraine: TelehealthSyntheticMigraineComplaintTriageAnswers | null
  sleep: TelehealthSyntheticSleepComplaintTriageAnswers | null
}

export type TelehealthApplicantRequestComplaintTriage = {
  applicantId: string
  applicantVersion: number
  applicantStatus: 'SyntheticRequestCreated'
  requestId: string
  requestVersion: 3 | 4
  requestStatus: 'SafetyScreening' | 'EmergencyRedirected' | 'InPersonRecommended' | 'Unsupported' | 'ClinicalReview' | 'Intake'
  complaintCategory: 'migraine' | 'sleep'
  policyKey: 'SYNTHETIC_APPLICANT_REQUEST_COMPLAINT_TRIAGE'
  policyVersion: 1
  protocolKey: 'synthetic-migraine-complaint-triage' | 'synthetic-sleep-complaint-triage'
  protocolVersion: 1
  engineVersion: 'synthetic-complaint-triage-engine-v1'
  clinicalContentStatus: 'UNAPPROVED_SYNTHETIC'
  medicalDirectorApprovalRequired: true
  medicalDirectorApprovalRecorded: false
  clinicalGoldenCasePackApproved: false
  productionPublicationAllowed: false
  contextSnapshotFingerprint: string
  contextExpiresAt: string
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  maskedCallbackPhone: string
  assessmentReady: boolean
  assessmentCreated: boolean
  outcome: 'Emergency' | 'UrgentInPerson' | 'InPersonRequired' | 'Unsupported' | 'ClinicalReview' | 'TelehealthEligible' | null
  publicDisposition: 'EmergencyCareNow' | 'PromptInPersonCare' | 'InPersonCareRequired' | 'TelehealthServiceUnsupported' | 'ClinicalReviewRequired' | 'SyntheticVideoEvaluationCandidate' | null
  evaluatedAt: string | null
  syntheticVideoEvaluationCandidate: boolean
  clinicalReviewRequired: boolean
  clinicalReviewCreated: false
  terminalForTelehealth: boolean
  intakeSnapshotCreated: false
  patientContacted: false
  patientCareQueueEntered: false
  clinicianQueueEntered: false
  doctorSearchStarted: false
  queuePositionAssigned: false
  appointmentCreated: false
  encounterCreated: false
  consentCreated: false
  careAuthorized: false
  prescribingEnabled: false
  billingEnabled: false
  claimCreated: false
  integrationEnabled: false
  externalCallPerformed: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantRequestIntakeInput = {
  expectedRequestVersion: number
  contextSnapshotFingerprint: string
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  symptomDuration: 'less-than-day' | '1-3-days' | '4-14-days' | 'more-than-14-days'
  currentLocationConfirmed: true
  callbackNumberConfirmed: true
  priorInformationReviewed: true
  insuranceLimitationsAcknowledged: true
  pendingConsentAcknowledged: true
  pendingVerificationAcknowledged: true
  complaintResultAcknowledged: true
  syntheticDataConfirmed: true
}

export type TelehealthApplicantRequestIntakeSection = {
  sectionKey: string
  receiptState: string
  outstandingRoute: string
}

export type TelehealthApplicantRequestIntake = {
  applicantId: string
  applicantVersion: 26
  applicantStatus: 'SyntheticRequestCreated'
  requestId: string
  requestVersion: 4 | 5
  requestStatus: 'Intake' | 'Verification'
  complaintCategory: 'migraine' | 'sleep'
  complaintDisplayLabel: string
  policyKey: 'SYNTHETIC_APPLICANT_REQUEST_INTAKE_SNAPSHOT_CONFIRMATION'
  policyVersion: 1
  clinicalContentStatus: 'UNAPPROVED_SYNTHETIC'
  medicalDirectorApprovalRequired: true
  medicalDirectorApprovalRecorded: false
  clinicalGoldenCasePackApproved: false
  productionPublicationAllowed: false
  contextSnapshotFingerprint: string
  contextExpiresAt: string
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  maskedCallbackPhone: string
  supportedSymptomDurations: TelehealthApplicantRequestIntakeInput['symptomDuration'][]
  sections: TelehealthApplicantRequestIntakeSection[]
  snapshotReady: boolean
  snapshotCreated: boolean
  symptomDuration: TelehealthApplicantRequestIntakeInput['symptomDuration'] | null
  capturedAt: string | null
  verificationPending: true
  consentPending: true
  coverageRecordCreated: false
  coverageVerified: false
  exactNetworkConfirmed: false
  operationalReviewCreated: false
  practiceAccepted: false
  patientContacted: false
  patientCareQueueEntered: false
  clinicianQueueEntered: false
  doctorSearchStarted: false
  queuePositionAssigned: false
  appointmentCreated: false
  encounterCreated: false
  consentCreated: false
  careAuthorized: false
  prescribingEnabled: false
  billingEnabled: false
  claimCreated: false
  integrationEnabled: false
  externalCallPerformed: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantRequestInsuranceSourceInput = {
  expectedRequestVersion: number
  insuranceSourceSnapshotFingerprint: string
  payerProductConfirmed: true
  maskedMemberDetailsConfirmed: true
  subscriberRelationshipConfirmed: true
  primaryCoverageSourceConfirmed: true
  freshVerificationRequested: true
  evidenceLimitationsAcknowledged: true
  syntheticDataConfirmed: true
}

export type TelehealthApplicantRequestInsuranceSource = {
  applicantId: string
  applicantVersion: 26
  applicantStatus: 'SyntheticRequestCreated'
  requestId: string
  requestVersion: 5 | 6
  requestStatus: 'Verification'
  policyKey: 'SYNTHETIC_APPLICANT_REQUEST_INSURANCE_SOURCE_CONFIRMATION'
  policyVersion: 1
  insuranceSourceSnapshotFingerprint: string
  contextExpiresAt: string
  payerDisplayName: string
  productDisplayName: string
  maskedMemberId: string
  maskedGroupNumber: string | null
  subscriberRelationship: 'Self' | 'Spouse' | 'Parent' | 'Other'
  coveragePriority: 'Primary'
  previousEligibilityBusinessOutcome: string
  previousEligibilityCheckedAt: string
  previousEligibilityExpiresAt: string
  previousEligibilityEvidenceExpired: boolean
  previousPracticeNetworkBusinessOutcome: string
  previousPracticeNetworkCheckedAt: string
  previousPracticeNetworkExpiresAt: string
  previousPracticeNetworkEvidenceExpired: boolean
  previousRenderingPhysicianNetworkChecked: false
  previousResultReusable: false
  sourceReady: boolean
  sourceConfirmed: boolean
  confirmedAt: string | null
  protectedPayloadReferenced: true
  protectedPayloadCopied: false
  protectedPayloadDecrypted: false
  freshVerificationRequested: boolean
  canonicalCoverageCreated: false
  coverageSelected: false
  eligibilityVerificationCreated: false
  networkVerificationCreated: false
  renderingPhysicianNetworkChecked: false
  coverageVerified: false
  exactNetworkConfirmed: false
  financialRouteCreated: false
  operationalReviewCreated: false
  practiceAccepted: false
  patientContacted: false
  patientCareQueueEntered: false
  clinicianQueueEntered: false
  doctorSearchStarted: false
  queuePositionAssigned: false
  appointmentCreated: false
  encounterCreated: false
  consentCreated: false
  careAuthorized: false
  integrationEnabled: false
  externalCallPerformed: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantRequestEligibilityInput = {
  expectedRequestVersion: number
  eligibilitySnapshotFingerprint: string
  syntheticDataConfirmed: true
  noGuaranteeAcknowledged: true
}

export type TelehealthApplicantRequestEligibility = {
  applicantId: string
  applicantVersion: 26
  applicantStatus: 'SyntheticRequestCreated'
  requestId: string
  requestVersion: 6 | 7
  requestStatus: 'Verification'
  policyKey: 'SYNTHETIC_APPLICANT_REQUEST_ELIGIBILITY_VERIFICATION'
  policyVersion: 1
  eligibilitySnapshotFingerprint: string
  contextExpiresAt: string
  payerDisplayName: string
  productDisplayName: string
  maskedMemberId: string
  maskedGroupNumber: string | null
  subscriberRelationship: 'Self' | 'Spouse' | 'Parent' | 'Other'
  coveragePriority: 'Primary'
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  purposeCategory: 'migraine' | 'sleep'
  verificationReady: boolean
  verificationCompleted: boolean
  verificationId: string | null
  dateOfService: string | null
  serviceCategory: string | null
  adapterMode: 'NON_PRODUCTION' | null
  compatibilityTarget: string | null
  datasetKey: string | null
  datasetVersion: number | null
  transportOutcome: string | null
  memberMatchStatus: string | null
  eligibilityStatus: 'Active' | 'Inactive' | 'Unknown' | null
  benefitInformationStatus: 'Reported' | 'NotReported' | 'Unknown' | null
  businessOutcome: 'EligibleBenefitsReported' | 'CoverageInactive' | 'SubscriberNotFound' | 'UnableToDetermine' | null
  memberMatched: boolean
  memberEligibilityChecked: boolean
  memberBenefitsChecked: boolean
  checkedAt: string | null
  expiresAt: string | null
  protectedPayloadReferenced: true
  protectedPayloadCopied: false
  protectedPayloadDecryptedInServerMemory: boolean
  priorEligibilityResultReused: false
  currentEligibilityEvidenceCreated: boolean
  rawTransactionCreated: false
  canonicalCoverageCreated: false
  coverageSelected: false
  networkVerificationCreated: false
  renderingPhysicianNetworkChecked: false
  coverageVerified: false
  exactNetworkConfirmed: false
  financialRouteCreated: false
  operationalReviewCreated: false
  practiceAccepted: false
  patientContacted: false
  patientCareQueueEntered: false
  clinicianQueueEntered: false
  doctorSearchStarted: false
  queuePositionAssigned: false
  appointmentCreated: false
  encounterCreated: false
  consentCreated: false
  careAuthorized: false
  integrationEnabled: false
  externalCallPerformed: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantRequestPracticeNetworkInput = {
  expectedRequestVersion: number
  networkSnapshotFingerprint: string
  syntheticDataConfirmed: true
  practiceOnlyScopeAcknowledged: true
  noGuaranteeAcknowledged: true
}

export type TelehealthApplicantRequestPracticeNetwork = {
  applicantId: string
  applicantVersion: 26
  applicantStatus: 'SyntheticRequestCreated'
  requestId: string
  requestVersion: 7 | 8
  requestStatus: 'Verification'
  policyKey: 'SYNTHETIC_APPLICANT_REQUEST_PRACTICE_NETWORK_VERIFICATION'
  policyVersion: 1
  networkSnapshotFingerprint: string
  contextExpiresAt: string
  practiceDisplayName: string
  payerDisplayName: string
  productDisplayName: string
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  purposeCategory: 'migraine' | 'sleep'
  eligibilityVerificationId: string
  eligibilityBusinessOutcome: 'EligibleBenefitsReported'
  eligibilityCheckedAt: string
  eligibilityExpiresAt: string
  verificationReady: boolean
  verificationCompleted: boolean
  verificationId: string | null
  dateOfService: string | null
  serviceCategory: string | null
  adapterMode: 'NON_PRODUCTION' | null
  compatibilityTarget: string | null
  datasetKey: string | null
  datasetVersion: number | null
  transportOutcome: 'SimulatedAvailable' | 'SimulatedUnavailable' | null
  planNetworkMatchStatus: 'Matched' | 'Unknown' | null
  practiceAffiliationStatus: 'InNetwork' | 'OutOfNetwork' | 'Unknown' | null
  serviceAvailabilityStatus: 'Included' | 'Excluded' | 'Unknown' | null
  newPatientAcceptanceStatus: 'Accepting' | 'Unknown' | null
  businessOutcome: 'PracticeInNetworkAcceptingNewPatients' | 'PracticeOutOfNetwork' | 'UnableToDetermine' | null
  practiceNetworkChecked: boolean
  practiceInNetwork: boolean
  newPatientsAccepted: boolean
  checkedAt: string | null
  expiresAt: string | null
  evidenceExpiresAt: string | null
  currentEligibilityEvidenceReusedAsContext: true
  practiceNetworkVerificationCreated: boolean
  renderingPhysicianSelected: false
  renderingPhysicianNetworkChecked: false
  exactNetworkConfirmed: false
  canonicalCoverageCreated: false
  coverageSelected: false
  coverageVerified: false
  financialRouteCreated: false
  operationalReviewCreated: false
  practiceAccepted: false
  patientContacted: false
  patientCareQueueEntered: false
  clinicianQueueEntered: false
  doctorSearchStarted: false
  queuePositionAssigned: false
  appointmentCreated: false
  encounterCreated: false
  consentCreated: false
  careAuthorized: false
  integrationEnabled: false
  externalCallPerformed: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantRequestRenderingCandidateInput = {
  expectedRequestVersion: number
  candidateSnapshotFingerprint: string
  syntheticDataConfirmed: true
  candidateOnlyScopeAcknowledged: true
  noAssignmentAcknowledged: true
  networkCheckStillRequiredAcknowledged: true
}

export type TelehealthApplicantRequestRenderingCandidate = {
  applicantId: string
  applicantVersion: 26
  applicantStatus: 'SyntheticRequestCreated'
  requestId: string
  requestVersion: 8 | 9
  requestStatus: 'Verification'
  policyKey: 'SYNTHETIC_APPLICANT_REQUEST_RENDERING_CANDIDATE_SELECTION'
  policyVersion: 1
  catalogKey: 'avenchart-synthetic-rendering-candidate-roster-2026-08'
  catalogVersion: 1
  candidateSnapshotFingerprint: string
  contextExpiresAt: string
  practiceDisplayName: string
  payerDisplayName: string
  productDisplayName: string
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  purposeCategory: 'migraine' | 'sleep'
  eligibilityVerificationId: string
  practiceNetworkVerificationId: string
  practiceNetworkBusinessOutcome: 'PracticeInNetworkAcceptingNewPatients'
  practiceNetworkCheckedAt: string
  practiceNetworkExpiresAt: string
  candidateDisplayName: string
  maskedProviderReference: string
  practitionerReference: string
  stateAuthorityReference: string
  serviceCategory: string
  modality: 'RealTimeAudioVideo'
  candidatePurpose: 'NETWORK_EVALUATION_ONLY'
  selectionReady: boolean
  selectionCompleted: boolean
  selectionId: string | null
  selectedAt: string | null
  candidateSelectedForNetworkEvaluation: boolean
  renderingPhysicianAssigned: false
  renderingPhysicianNetworkChecked: false
  exactNetworkConfirmed: false
  canonicalCoverageCreated: false
  coverageSelected: false
  coverageVerified: false
  financialRouteCreated: false
  operationalReviewCreated: false
  practiceAccepted: false
  patientContacted: false
  patientCareQueueEntered: false
  clinicianQueueEntered: false
  doctorSearchStarted: false
  queuePositionAssigned: false
  appointmentCreated: false
  encounterCreated: false
  consentCreated: false
  careAuthorized: false
  integrationEnabled: false
  externalCallPerformed: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantRequestParticipationContextInput = {
  expectedRequestVersion: number
  contextSnapshotFingerprint: string
  syntheticDataConfirmed: true
  npiNotCredentialAcknowledged: true
  realAuthorityNotVerifiedAcknowledged: true
  exactParticipationStillRequiredAcknowledged: true
}

export type TelehealthApplicantRequestParticipationContext = {
  applicantId: string
  applicantVersion: 26
  applicantStatus: 'SyntheticRequestCreated'
  requestId: string
  requestVersion: 9 | 10
  requestStatus: 'Verification'
  policyKey: 'SYNTHETIC_APPLICANT_REQUEST_PARTICIPATION_CONTEXT'
  policyVersion: 1
  catalogKey: 'avenchart-synthetic-participation-context-2026-08'
  catalogVersion: 1
  contextSnapshotFingerprint: string
  contextExpiresAt: string
  practiceDisplayName: string
  payerDisplayName: string
  productDisplayName: string
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  purposeCategory: 'migraine' | 'sleep'
  eligibilityVerificationId: string
  practiceNetworkVerificationId: string
  candidateSelectionId: string
  candidateDisplayName: string
  maskedProviderReference: string
  maskedBillingProviderReference: string
  authorityJurisdiction: 'GA' | 'CA' | 'FL'
  authorityKind: 'PHYSICIAN_PRACTICE_AUTHORITY'
  authorityContextStatus: 'SYNTHETIC_ACTIVE'
  practitionerRoleContextStatus: 'SYNTHETIC_ACTIVE'
  organizationAffiliationContextStatus: 'SYNTHETIC_ACTIVE'
  billingContractContextStatus: 'SYNTHETIC_ACTIVE'
  serviceCategory: string
  modality: 'RealTimeAudioVideo'
  effectiveFrom: string
  effectiveThrough: string
  contextPurpose: 'PARTICIPATION_EVALUATION_PREREQUISITES_ONLY'
  confirmationReady: boolean
  confirmationCompleted: boolean
  confirmationId: string | null
  confirmedAt: string | null
  participationEvaluationContextConfirmed: boolean
  realStateAuthorityVerified: false
  realCredentialingVerified: false
  renderingPhysicianAssigned: false
  renderingPhysicianNetworkChecked: false
  exactNetworkConfirmed: false
  canonicalCoverageCreated: false
  coverageSelected: false
  coverageVerified: false
  financialRouteCreated: false
  operationalReviewCreated: false
  practiceAccepted: false
  patientContacted: false
  patientCareQueueEntered: false
  clinicianQueueEntered: false
  doctorSearchStarted: false
  queuePositionAssigned: false
  appointmentCreated: false
  encounterCreated: false
  consentCreated: false
  careAuthorized: false
  integrationEnabled: false
  externalCallPerformed: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantRequestParticipationEvaluationInput = {
  expectedRequestVersion: number
  evaluationSnapshotFingerprint: string
  syntheticDataConfirmed: true
  exactTupleScopeAcknowledged: true
  noCoverageGuaranteeAcknowledged: true
  realVerificationStillRequiredAcknowledged: true
}

export type TelehealthApplicantRequestParticipationEvaluation = {
  applicantId: string
  applicantVersion: 26
  applicantStatus: 'SyntheticRequestCreated'
  requestId: string
  requestVersion: 10 | 11
  requestStatus: 'Verification'
  policyKey: 'SYNTHETIC_APPLICANT_REQUEST_PARTICIPATION_EVALUATION'
  policyVersion: 1
  catalogKey: 'avenchart-synthetic-participation-evaluation-2026-08'
  catalogVersion: 1
  sourceMode: 'NON_PRODUCTION'
  compatibilityTarget: 'HL7_FHIR_R4_DAVINCI_PDEX_PLAN_NET_1_2_0'
  evaluationScope: 'BILLING_ENTITY_RENDERING_PROVIDER_NETWORK_LOCATION_SERVICE_MODALITY_NEW_PATIENT'
  evaluationSnapshotFingerprint: string
  resultValidThrough: string
  practiceDisplayName: string
  payerDisplayName: string
  productDisplayName: string
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  purposeCategory: 'migraine' | 'sleep'
  dateOfService: string
  eligibilityVerificationId: string
  practiceNetworkVerificationId: string
  candidateSelectionId: string
  participationContextConfirmationId: string
  candidateDisplayName: string
  maskedProviderReference: string
  maskedBillingProviderReference: string
  serviceCategory: string
  modality: 'RealTimeAudioVideo'
  effectiveFrom: string
  effectiveThrough: string
  evaluationReady: boolean
  evaluationCompleted: boolean
  evaluationId: string | null
  evaluatedAt: string | null
  businessOutcome: 'SyntheticExactParticipationMatched' | null
  syntheticParticipationEvaluated: boolean
  syntheticBillingEntityInNetwork: boolean
  syntheticRenderingProviderInNetwork: boolean
  syntheticPlanNetworkMatched: boolean
  syntheticServiceLocationMatched: boolean
  syntheticNewPatientsAccepted: boolean
  syntheticExactNetworkMatched: boolean
  realStateAuthorityVerified: false
  realCredentialingVerified: false
  renderingPhysicianAssigned: false
  renderingPhysicianNetworkChecked: false
  exactNetworkConfirmed: false
  canonicalCoverageCreated: false
  coverageSelected: false
  coverageVerified: false
  financialRouteCreated: false
  operationalReviewCreated: false
  practiceAccepted: false
  patientContacted: false
  patientCareQueueEntered: false
  clinicianQueueEntered: false
  doctorSearchStarted: false
  queuePositionAssigned: false
  appointmentCreated: false
  encounterCreated: false
  consentCreated: false
  careAuthorized: false
  integrationEnabled: false
  externalCallPerformed: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantRequestOperationalReviewSubmissionInput = {
  expectedRequestVersion: number
  submissionSnapshotFingerprint: string
  syntheticEvidenceAcknowledged: true
  noCoverageGuaranteeAcknowledged: true
  practiceReviewPendingAcknowledged: true
  noCareRelationshipAcknowledged: true
}

export type TelehealthApplicantRequestOperationalReviewSubmission = {
  applicantId: string
  applicantVersion: 26
  applicantStatus: 'SyntheticRequestCreated'
  requestId: string
  requestVersion: 11 | 12
  requestStatus: 'Verification' | 'OperationalReview'
  policyKey: 'SYNTHETIC_APPLICANT_REQUEST_OPERATIONAL_REVIEW_SUBMISSION'
  policyVersion: 1
  sourceMode: 'NON_PRODUCTION'
  compatibilityTarget: 'AVENCHART_SYNTHETIC_OPERATIONAL_REVIEW_V1'
  submissionSnapshotFingerprint: string
  resultValidThrough: string
  practiceDisplayName: string
  payerDisplayName: string
  productDisplayName: string
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  purposeCategory: 'migraine' | 'sleep'
  dateOfService: string
  candidateDisplayName: string
  maskedProviderReference: string
  maskedBillingProviderReference: string
  serviceCategory: string
  modality: 'RealTimeAudioVideo'
  submissionReady: boolean
  submissionCompleted: boolean
  submittedAt: string | null
  businessOutcome: 'SyntheticRequestSubmittedForOperationalReview' | null
  syntheticAutomatedChecksComplete: boolean
  operationalReviewCreated: boolean
  realStateAuthorityVerified: false
  realCredentialingVerified: false
  renderingPhysicianAssigned: false
  renderingPhysicianNetworkChecked: false
  exactNetworkConfirmed: false
  canonicalCoverageCreated: false
  coverageSelected: false
  coverageVerified: false
  financialRouteCreated: false
  practiceAccepted: false
  patientContacted: false
  patientCareQueueEntered: false
  clinicianQueueEntered: false
  doctorSearchStarted: false
  queuePositionAssigned: false
  appointmentCreated: false
  encounterCreated: false
  consentCreated: false
  careAuthorized: false
  integrationEnabled: false
  externalCallPerformed: false
  direction: string
  limitations: string[]
}

export type TelehealthApplicantRequestQueueStatus = {
  requestId: string
  requestStatus: 'OperationalReview' | 'Queued' | 'Reserved' | 'Connecting' | 'InConsultation' | 'WrapUp'
  requestVersion: number
  policyKey: 'SYNTHETIC_APPLICANT_REQUEST_QUEUE_STATUS'
  policyVersion: 1
  sourceMode: 'NON_PRODUCTION'
  phase: 'Reviewing' | 'InQueue' | 'PhysicianPreparing' | 'ConnectionRoom' | 'Consultation' | 'WrapUp'
  headline: string
  detail: string
  approximateRequestsAhead: number | null
  positionIsApproximate: boolean
  exactQueuePositionAssigned: false
  waitEstimateAvailable: false
  waitEstimateMessage: string
  requestUpdatedAt: string
  snapshotAt: string
  refreshAfterSeconds: number
  realtimeAvailable: false
  practiceAccepted: boolean
  doctorSearchStarted: boolean
  renderingPhysicianAssigned: boolean
  renderingPhysicianIdentityDisclosed: false
  syntheticRenderingCandidateMatched: boolean
  realRenderingPhysicianNetworkConfirmed: false
  connectionRoomCreated: boolean
  patientWaitingRoomEntered: boolean
  mediaSessionCreated: false
  communicationStarted: false
  coverageVerified: false
  consentCreated: false
  careAuthorized: false
  integrationEnabled: false
  externalCallPerformed: false
  safetyActions: string[]
  limitations: string[]
}

export type TelehealthRequest = {
  requestId: string
  status: TelehealthRequestStatus
  complaintCategory: 'migraine' | 'sleep'
  triageOutcome: string | null
  version: number
  stateCode: string | null
  createdAt: string
  updatedAt: string
  readyAt: string | null
  allowedActions: string[]
  coverage: TelehealthCoverageStatus | null
}

export type TelehealthRequestStatus = 'Draft' | 'LocationConfirmed' | 'SafetyScreening' | 'EmergencyRedirected' | 'InPersonRecommended' | 'Unsupported' | 'ClinicalReview' | 'Intake' | 'Verification' | 'OperationalReview' | 'Redirected' | 'Queued' | 'Reserved' | 'Connecting' | 'InConsultation' | 'WrapUp'

export type TelehealthPatientQueueStatus = {
  requestId: string
  requestStatus: TelehealthRequestStatus
  requestVersion: number
  phase: 'Reviewing' | 'InQueue' | 'PhysicianPreparing' | 'ConnectionRoom' | 'Consultation' | 'Redirected'
  headline: string
  detail: string
  approximateRequestsAhead: number | null
  positionIsApproximate: boolean
  waitEstimateAvailable: false
  waitEstimateMessage: string
  requestUpdatedAt: string
  snapshotAt: string
  refreshAfterSeconds: number
  realtimeAvailable: false
  safetyActions: string[]
}

export type TelehealthCoverageStatus = {
  adapterMode: 'NON_PRODUCTION'
  eligibilityStatus: 'Active' | 'Inactive' | 'Unknown'
  networkStatus: 'ConfirmedInNetwork' | 'OutOfNetwork' | 'Unknown'
  financialRoute: 'ConfirmedInNetwork' | 'CoverageActiveNetworkPending' | 'OutOfNetworkOrSelfPay' | 'UnableToVerify' | 'CoverageInactive'
  limitations: string[]
  verifiedAt: string
  expiresAt: string
}

export type TelehealthReadiness = {
  requestId: string
  requestVersion: number
  status: string
  patientDetails: {
    displayName: string
    dateOfBirth: string
    email: string | null
    phone: string | null
    address: string
    fingerprint: string
    missingFields: string[]
  }
  clinicalSummary: {
    activeMedicationCount: number
    activeAllergyCount: number
    historyAvailable: boolean
    fingerprint: string
  }
  coverageOptions: Array<{
    coverageToken: string
    coverageType: string
    provider: string
    planName: string
    maskedPolicyNumber: string
    maskedGroupNumber: string
    subscriberRelationship: string
    fingerprint: string
  }>
  acknowledgment: {
    kind: string
    packageKey: string
    packageVersion: number
    contentHash: string
    title: string
    statements: string[]
    legalEffect: false
  }
  blockingReasons: string[]
}

export type TelehealthQueueItem = Pick<
  TelehealthRequest,
  'requestId' | 'status' | 'complaintCategory' | 'triageOutcome' | 'version' | 'createdAt'
> & { applicantOriginated: boolean }

export type TelehealthApplicantRequestQueueAuthorization = {
  requestId: string
  requestVersion: number
  requestStatus: 'OperationalReview' | 'Queued'
  policyKey: 'SYNTHETIC_APPLICANT_REQUEST_QUEUE_AUTHORIZATION'
  policyVersion: 1
  sourceMode: 'NON_PRODUCTION'
  compatibilityTarget: 'AVENCHART_SYNTHETIC_QUEUE_AUTHORIZATION_V1'
  authorizationSnapshotFingerprint: string
  resultValidThrough: string
  practiceDisplayName: string
  payerDisplayName: string
  productDisplayName: string
  currentLocationStateCode: 'GA' | 'CA' | 'FL'
  purposeCategory: 'migraine' | 'sleep'
  dateOfService: string
  candidateDisplayName: string
  maskedProviderReference: string
  maskedBillingProviderReference: string
  serviceCategory: 'ProfessionalTelehealthConsultation'
  modality: 'RealTimeAudioVideo'
  authorizationReady: boolean
  authorizationCompleted: boolean
  authorizedAt: string | null
  businessOutcome: 'SyntheticRequestAuthorizedToQueue' | null
  syntheticEvidenceReviewed: boolean
  practiceAccepted: boolean
  patientCareQueueEntered: boolean
  clinicianQueueEntered: boolean
  doctorSearchStarted: boolean
  appointmentCreated: boolean
  realStateAuthorityVerified: false
  realCredentialingVerified: false
  renderingPhysicianAssigned: false
  renderingPhysicianNetworkChecked: false
  exactNetworkConfirmed: false
  canonicalCoverageCreated: false
  coverageSelected: false
  coverageVerified: false
  financialRouteCreated: false
  patientContacted: false
  queuePositionAssigned: false
  encounterCreated: false
  consentCreated: false
  careAuthorized: false
  prescribingEnabled: false
  billingEnabled: false
  claimCreated: false
  integrationEnabled: false
  externalCallPerformed: false
  direction: string
  limitations: string[]
}

export type TelehealthShift = {
  shiftId: string
  status: string
  facilityId: number
  clinicianStaffId: number
  startedAt: string
  version: number
}

export type TelehealthReservation = {
  reservationId: string
  requestId: string
  queueEntryId: string
  shiftId: string
  clinicianStaffId: number
  reservedAt: string
  leaseExpiresAt: string
  status: string
  requestVersion: number
  applicantOriginated: boolean
}

export type TelehealthDevicePreflight = {
  browserSupported: boolean
  cameraAvailable: boolean
  microphoneAvailable: boolean
  speakerAvailable: boolean
  networkQuality: 'unknown' | 'limited' | 'good'
  syntheticDataConfirmed: true
}

export type TelehealthConnectionGrant = {
  sessionId: string
  grantId: string
  requestId: string
  requestVersion: number
  requestStatus: 'Connecting'
  participantRole: 'patient' | 'physician'
  adapterMode: 'NON_PRODUCTION'
  joinCredential: string
  expiresAt: string
  recordingEnabled: false
  transcriptionEnabled: false
  mediaTransportEnabled: false
  waitingRoomMessage: string
  limitations: string[]
}

export type TelehealthConsultationStartInput = {
  expectedVersion: number
  patientLocationState: 'GA' | 'CA' | 'FL'
  patientIdentityDiscussed: boolean
  callbackConfirmed: boolean
  privacyConfirmed: boolean
  consentDiscussed: boolean
  noConcerningSymptomChange: boolean
  emergencyPlanConfirmed: boolean
  communicationSufficient: boolean
  syntheticDataConfirmed: true
}

export type TelehealthConsultationStart = {
  consultationId: string
  requestId: string
  requestVersion: number
  requestStatus: 'InConsultation'
  appointmentStatus: '>'
  modality: 'SYNTHETIC_VIDEO'
  startedAt: string
  legalEffect: false
  chartAccessEnabled: true
  documentationEnabled: true
  prescribingEnabled: false
  claimsEnabled: false
  limitations: string[]
}

export type TelehealthConsultationWorkspace = {
  consultationId: string
  consultationStatus: 'InConsultation' | 'WrapUp'
  consultationVersion: number
  mediaEndedAt: string | null
  modality: 'SYNTHETIC_VIDEO'
  startedAt: string
  asOf: string
  readOnly: true
  patient: {
    displayName: string
    dateOfBirth: string
    age: number
    recordedSex: string | null
    callbackPhone: string | null
  }
  visit: {
    patientLocationState: 'GA' | 'CA' | 'FL'
    complaintCategory: string
    complaintSummary: string
    symptomDuration: string
    triageOutcome: string
  }
  allergies: Array<{ title: string; reaction: string | null; severity: string | null }>
  medications: Array<{ title: string }>
  problems: Array<{ title: string; diagnosis: string | null }>
  documentation: TelehealthConsultationDocumentationDraft
  documentationEnabled: true
  prescribingEnabled: false
  claimsEnabled: false
  completionEnabled: false
  limitations: string[]
}

export type TelehealthConsultationDocumentationDraftInput = {
  expectedVersion: number
  subjective: string | null
  objective: string | null
  assessment: string | null
  plan: string | null
}

export type TelehealthConsultationDocumentationDraft = {
  version: number
  savedAt: string | null
  savedBy: string | null
  isLocked: boolean
  isSigned: false
  isFinal: false
  subjective: string | null
  objective: string | null
  assessment: string | null
  plan: string | null
}

export type EnterTelehealthConsultationWrapUpInput = {
  expectedVersion: number
  syntheticSessionEndedConfirmed: true
  documentationStillIncompleteAcknowledged: true
  wrapUpResponsibilityAcknowledged: true
}

export type TelehealthConsultationWrapUp = {
  consultationId: string
  version: number
  consultationStatus: 'MediaEnded'
  mediaEndedAt: string
  requestVersion: number
  requestStatus: 'WrapUp'
  shiftStatus: 'WrapUp'
  appointmentStatus: '>'
  documentationEnabled: true
  completionEnabled: false
  clinicianAvailableForNewWork: false
  limitations: string[]
}

export type TelehealthPharmacyAddress = {
  line1: string
  line2: string | null
  city: string
  state: 'GA' | 'CA' | 'FL'
  postalCode: string
  country: 'US'
}

export type TelehealthPharmacyDirectoryEntry = {
  directoryEntryId: string
  name: string
  address: TelehealthPharmacyAddress
  phone: string
  ncpdpId: string | null
  npi: string | null
  electronicRoutingCapability: 'NON_PRODUCTION_ONLY'
  isChartPreferred: boolean
  approximateDistanceMiles: number | null
}

export type TelehealthPharmacyChoiceDraft = {
  version: number
  directoryEntryId: string
  name: string
  address: TelehealthPharmacyAddress
  phone: string
  ncpdpId: string | null
  npi: string | null
  electronicRoutingCapability: 'NON_PRODUCTION_ONLY'
  directorySource: string
  directoryVersion: string
  choiceBasis: 'PatientConfirmedDuringConsultation'
  patientChoiceConfirmed: true
  selectedAt: string
  prescriptionCreated: false
  transmitted: false
}

export type TelehealthPharmacyChoiceWorkspace = {
  consultationId: string
  consultationStatus: 'MediaEnded'
  adapterMode: 'NON_PRODUCTION'
  datasetId: string
  datasetVersion: string
  asOf: string
  searchState: 'GA' | 'CA' | 'FL' | null
  searchPostalCode: string | null
  distanceOrigin: 'EnteredPostalCode' | null
  locationSearchAcknowledged: boolean
  chartPreferenceCount: number
  pharmacies: TelehealthPharmacyDirectoryEntry[]
  currentChoice: TelehealthPharmacyChoiceDraft | null
  prescriptionEnabled: false
  transmissionEnabled: false
  limitations: string[]
}

export type TelehealthPharmacySearch = {
  query?: string
  state?: 'GA' | 'CA' | 'FL'
  postalCode?: string
  originPostalCode?: string
  locationSearchAcknowledged?: boolean
  limit?: number
}

export type TelehealthSafetyDispositionOption = {
  code: string
  label: string
  requiresAdequateEvaluation: boolean
  requiresLocationCallbackReconfirmation: boolean
  requiresEmergencyFacts: boolean
  requiresContactAttemptSummary: boolean
}

export type TelehealthSafetyDispositionDraft = {
  version: number
  dispositionCode: string
  adequateEvaluationCompleted: boolean
  followUpOwner: string
  followUpTimeframe: string
  nextStepInstructions: string
  warningEscalationInstructions: string
  communicationMethod: string
  communicationCompleted: boolean
  locationCallbackReconfirmed: boolean
  emergencyInstructionProvided: boolean
  emergencyHandoffStatus: string | null
  contactAttemptSummary: string | null
  recordedAt: string
  legalEffect: false
  signed: false
  finalized: false
  patientDelivered: false
}

export type TelehealthSafetyDispositionWorkspace = {
  consultationId: string
  consultationStatus: 'MediaEnded'
  asOf: string
  dispositions: TelehealthSafetyDispositionOption[]
  followUpOwners: string[]
  communicationMethods: string[]
  emergencyHandoffStatuses: string[]
  currentDraft: TelehealthSafetyDispositionDraft | null
  signingEnabled: false
  patientDeliveryEnabled: false
  completionEnabled: false
  limitations: string[]
}

export type TelehealthSafetyDispositionDraftInput = {
  expectedVersion: number
  dispositionCode: string
  adequateEvaluationCompleted: boolean
  followUpOwner: string
  followUpTimeframe: string
  nextStepInstructions: string
  warningEscalationInstructions: string
  communicationMethod: string
  communicationCompleted: boolean
  locationCallbackReconfirmed: boolean
  emergencyInstructionProvided: boolean
  emergencyHandoffStatus: string | null
  contactAttemptSummary: string | null
  syntheticDataConfirmed: true
}

export type TelehealthCompletionPrerequisites = {
  consultationId: string
  consultationStatus: 'MediaEnded'
  requestStatus: 'WrapUp'
  shiftStatus: 'WrapUp'
  appointmentStatus: '>'
  asOf: string
  documentation: {
    version: number
    hasAnyContent: boolean
    subjectivePresent: boolean
    objectivePresent: boolean
    assessmentPresent: boolean
    planPresent: boolean
  }
  safetyDisposition: null | {
    version: number
    dispositionCode: string
    adequateEvaluationCompleted: boolean
    followUpOwnerPresent: boolean
    followUpTimeframePresent: boolean
    nextStepInstructionsPresent: boolean
    warningEscalationInstructionsPresent: boolean
    communicationMethod: string
    communicationCompleted: boolean
    locationCallbackReconfirmed: boolean
    emergencyInstructionProvided: boolean
    emergencyHandoffStatusPresent: boolean
    contactAttemptSummaryPresent: boolean
  }
  pharmacyChoice: null | {
    version: number
    patientChoiceConfirmed: boolean
  }
  structuralEvidencePresent: boolean
  productBlockers: string[]
  signingEnabled: false
  completionEnabled: false
  patientDeliveryEnabled: false
  downstreamCreationEnabled: false
  limitations: string[]
}

export type TelehealthPrescriptionCatalogItem = {
  rxNormCode: string
  drugName: string
  displayName: string
  form: string
  strength: string
  route: string
}

export type TelehealthPrescriptionPreparationDraftInput = {
  expectedVersion: number
  rxNormCode: string
  doseAmount: number
  doseUnit: string
  frequency: string
  quantityValue: number
  quantityUnit: string
  durationDays: number
  refills: number
  indication: string
  directions: string
  medicationListReviewed: true
  allergyListReviewed: true
  adequateEvaluationCompleted: true
  syntheticDataConfirmed: true
}

export type TelehealthPrescriptionPreparationDraft = {
  version: number
  rxNormCode: string
  drugName: string
  displayName: string
  form: string
  strength: string
  route: string
  doseAmount: number
  doseUnit: string
  frequency: string
  quantityValue: number
  quantityUnit: string
  durationDays: number
  refills: number
  indication: string
  directions: string
  medicationListReviewed: true
  allergyListReviewed: true
  adequateEvaluationCompleted: true
  pharmacyChoiceVersion: number
  recordedAt: string
  legalEffect: false
  safetyChecked: false
  signed: false
  transmissionQueued: false
  transmitted: false
  patientDelivered: false
}

export type TelehealthPrescriptionPreparationWorkspace = {
  consultationId: string
  consultationStatus: 'MediaEnded'
  asOf: string
  catalogSource: 'AvenChartSyntheticMedicationVocabulary'
  catalogDatasetId: string
  catalogDatasetVersion: string
  adapterMode: 'NON_PRODUCTION'
  canonicalModelVersion: 'AVENCHART_ERX_PREPARATION_V1'
  intendedStandard: 'NCPDP_SCRIPT_2017071'
  currentPharmacyChoiceVersion: number | null
  catalogResults: TelehealthPrescriptionCatalogItem[]
  currentDraft: TelehealthPrescriptionPreparationDraft | null
  safetyCheckEnabled: false
  signingEnabled: false
  prescriptionCreationEnabled: false
  transmissionEnabled: false
  patientDeliveryEnabled: false
  completionEnabled: false
  limitations: string[]
}

function portalHeaders(extra?: Record<string, string>) {
  const session = loadPortalSession()
  if (!session) throw new Error('Sign in to the patient portal before continuing.')
  return {
    'X-AvenChart-Patient-Portal-Session': session.sessionId,
    ...extra,
  }
}

function clinicianHeaders(extra?: Record<string, string>) {
  const session = loadClinicianSession()
  if (!session) throw new Error('Sign in to the clinician application before continuing.')
  return {
    'X-AvenChart-Session': session.sessionId,
    'X-AvenChart-Facility-Id': String(session.facilityId ?? ''),
    'X-AvenChart-Purpose-Of-Use': session.purposeOfUse ?? 'treatment',
    ...extra,
  }
}

function applicantHeaders(applicantAccessKey: string, extra?: Record<string, string>) {
  return {
    'X-AvenChart-Telehealth-Applicant-Key': applicantAccessKey,
    ...extra,
  }
}

async function json<T>(path: string, init: RequestInit = {}): Promise<T> {
  const response = await apiFetch(`${apiBaseUrl}${path}`, init)
  return (await response.json()) as T
}

function commandInit(body?: unknown, scope: 'portal' | 'clinician' = 'portal'): RequestInit {
  const extra = {
    'Content-Type': 'application/json',
    'X-Idempotency-Key': crypto.randomUUID(),
  }
  return {
    method: 'POST',
    headers: scope === 'portal' ? portalHeaders(extra) : clinicianHeaders(extra),
    body: body === undefined ? undefined : JSON.stringify(body),
  }
}

export function getPracticeContext(signal?: AbortSignal) {
  return json<TelehealthPracticeContext>('/api/telehealth/v1/context', { signal })
}

export function createProspectiveApplicant(
  input: TelehealthProspectiveApplicantInput,
  applicantAccessKey: string,
  idempotencyKey: string,
) {
  return json<TelehealthProspectiveApplicant>('/api/telehealth/v1/applicants', {
    method: 'POST',
    headers: applicantHeaders(applicantAccessKey, {
      'Content-Type': 'application/json',
      'X-Idempotency-Key': idempotencyKey,
    }),
    body: JSON.stringify(input),
  })
}

export function getProspectiveApplicant(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthProspectiveApplicant>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}`,
    { headers: applicantHeaders(applicantAccessKey), signal },
  )
}

export function verifyProspectiveApplicantContact(
  applicantId: string,
  applicantAccessKey: string,
  expectedVersion: number,
  verificationCode: string,
  idempotencyKey: string,
) {
  return json<TelehealthProspectiveApplicant>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/contact-verification`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      body: JSON.stringify({ expectedVersion, verificationCode }),
    },
  )
}

export function evaluateProspectiveSafetyTriage(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthProspectiveSafetyTriageInput,
  idempotencyKey: string,
) {
  return json<TelehealthProspectiveSafetyTriage>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/safety-triage`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function recordProspectiveVisitPurpose(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthProspectiveVisitPurposeInput,
  idempotencyKey: string,
) {
  return json<TelehealthProspectiveVisitPurpose>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/visit-purpose`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getProspectivePracticeNetworkOptions(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthProspectivePracticeNetworkOptions>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/practice-network-precheck/options`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function recordProspectivePracticeNetworkPrecheck(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthProspectivePracticeNetworkPrecheckInput,
  idempotencyKey: string,
) {
  return json<TelehealthProspectivePracticeNetworkPrecheck>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/practice-network-precheck`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function recordProspectiveMemberInsuranceDetails(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthProspectiveMemberInsuranceDetailsInput,
  idempotencyKey: string,
) {
  return json<TelehealthProspectiveMemberInsuranceDetails>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/member-insurance-details`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function recordProspectiveEligibility(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthProspectiveEligibilityInput,
  idempotencyKey: string,
) {
  return json<TelehealthProspectiveEligibility>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/eligibility`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function recordProspectivePracticeNetwork(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthProspectivePracticeNetworkInput,
  idempotencyKey: string,
) {
  return json<TelehealthProspectivePracticeNetwork>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/practice-network-determination`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function recordProspectiveIdentityProofing(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthProspectiveIdentityProofingInput,
  idempotencyKey: string,
) {
  return json<TelehealthProspectiveIdentityProofing>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/identity-proofing`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantTelehealthNotice(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantNotice>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-notice`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function acknowledgeApplicantTelehealthNotice(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantNoticeInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantNotice>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-notice/acknowledgment`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantRegistrationDetails(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantRegistrationDetails>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/registration-details`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function confirmApplicantRegistrationDetails(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantRegistrationDetailsInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantRegistrationDetails>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/registration-details/confirmation`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantInsuranceHandoff(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantInsuranceHandoff>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/insurance-handoff`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function confirmApplicantInsuranceHandoff(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantInsuranceHandoffInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantInsuranceHandoff>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/insurance-handoff/confirmation`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantCommunicationAccessReadiness(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantCommunicationAccessReadiness>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/communication-access-readiness`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function recordApplicantCommunicationAccessReadiness(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantCommunicationAccessReadinessInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantCommunicationAccessReadiness>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/communication-access-readiness`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantDevicePreparation(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantDevicePreparation>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/device-preparation`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function recordApplicantDevicePreparation(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantDevicePreparationInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantDevicePreparation>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/device-preparation`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantClinicalInformationInventory(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantClinicalInformationInventory>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/clinical-information-inventory`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function recordApplicantClinicalInformationInventory(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantClinicalInformationInventoryInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantClinicalInformationInventory>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/clinical-information-inventory`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantMedicationInformation(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantMedicationInformation>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/medication-information`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function recordApplicantMedicationInformation(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantMedicationInformationInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantMedicationInformation>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/medication-information`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantAllergyInformation(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantAllergyInformation>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/allergy-information`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function recordApplicantAllergyInformation(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantAllergyInformationInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantAllergyInformation>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/allergy-information`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantHealthHistoryInformation(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantHealthHistoryInformation>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/health-history-information`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function recordApplicantHealthHistoryInformation(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantHealthHistoryInformationInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantHealthHistoryInformation>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/health-history-information`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantClinicalInformationSummary(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantClinicalInformationSummary>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/clinical-information-summary`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function confirmApplicantClinicalInformationSummary(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantClinicalInformationSummaryInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantClinicalInformationSummary>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/clinical-information-summary`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantPreRequestReadiness(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantPreRequestReadiness>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/pre-request-readiness`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function acknowledgeApplicantPreRequestReadiness(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantPreRequestReadinessInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantPreRequestReadiness>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/pre-request-readiness`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantPracticeReviewSubmission(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantPracticeReview>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/practice-review-submission`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function submitApplicantPracticeReview(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantPracticeReviewInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantPracticeReview>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/practice-review-submission`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantTelehealthRequest(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantRequestCreation>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function createApplicantTelehealthRequest(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantRequestCreationInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantRequestCreation>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantTelehealthRequestLocation(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantRequestLocation>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/location`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function confirmApplicantTelehealthRequestLocation(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantRequestLocationInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantRequestLocation>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/location`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantTelehealthRequestUniversalSafety(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantRequestUniversalSafety>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/safety`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function assessApplicantTelehealthRequestUniversalSafety(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantRequestUniversalSafetyInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantRequestUniversalSafety>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/safety`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantTelehealthRequestComplaintTriage(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantRequestComplaintTriage>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/complaint-triage`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function assessApplicantTelehealthRequestComplaintTriage(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantRequestComplaintTriageInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantRequestComplaintTriage>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/complaint-triage`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantTelehealthRequestIntake(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantRequestIntake>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/intake`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function confirmApplicantTelehealthRequestIntake(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantRequestIntakeInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantRequestIntake>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/intake`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantTelehealthRequestInsuranceSource(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantRequestInsuranceSource>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/insurance-source`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function confirmApplicantTelehealthRequestInsuranceSource(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantRequestInsuranceSourceInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantRequestInsuranceSource>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/insurance-source`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantTelehealthRequestEligibility(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantRequestEligibility>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/eligibility`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function runApplicantTelehealthRequestEligibility(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantRequestEligibilityInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantRequestEligibility>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/eligibility`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantTelehealthRequestPracticeNetwork(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantRequestPracticeNetwork>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/practice-network`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function runApplicantTelehealthRequestPracticeNetwork(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantRequestPracticeNetworkInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantRequestPracticeNetwork>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/practice-network`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantTelehealthRequestRenderingCandidate(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantRequestRenderingCandidate>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/rendering-candidate`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function selectApplicantTelehealthRequestRenderingCandidate(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantRequestRenderingCandidateInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantRequestRenderingCandidate>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/rendering-candidate`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantTelehealthRequestParticipationContext(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantRequestParticipationContext>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/participation-context`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function confirmApplicantTelehealthRequestParticipationContext(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantRequestParticipationContextInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantRequestParticipationContext>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/participation-context`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantTelehealthRequestParticipationEvaluation(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantRequestParticipationEvaluation>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/participation-evaluation`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function evaluateApplicantTelehealthRequestParticipation(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantRequestParticipationEvaluationInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantRequestParticipationEvaluation>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/participation-evaluation`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantTelehealthRequestOperationalReviewSubmission(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantRequestOperationalReviewSubmission>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/operational-review-submission`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export function submitApplicantTelehealthRequestForOperationalReview(
  applicantId: string,
  applicantAccessKey: string,
  input: TelehealthApplicantRequestOperationalReviewSubmissionInput,
  idempotencyKey: string,
) {
  return json<TelehealthApplicantRequestOperationalReviewSubmission>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/operational-review-submission`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantTelehealthRequestQueueStatus(
  applicantId: string,
  applicantAccessKey: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantRequestQueueStatus>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/queue-status`,
    {
      headers: applicantHeaders(applicantAccessKey),
      cache: 'no-store',
      signal,
    },
  )
}

export async function listPatientRequests(signal?: AbortSignal) {
  const result = await json<{ requests: TelehealthRequest[] }>('/api/telehealth/v1/patient/requests', {
    headers: portalHeaders(),
    signal,
  })
  return result.requests
}

export function getPatientQueueStatus(requestId: string, signal?: AbortSignal) {
  return json<TelehealthPatientQueueStatus>(
    `/api/telehealth/v1/patient/requests/${encodeURIComponent(requestId)}/status`,
    { headers: portalHeaders(), signal },
  )
}

export function createPatientRequest(complaintCategory: 'migraine' | 'sleep') {
  return json<TelehealthRequest>('/api/telehealth/v1/patient/requests', commandInit({ complaintCategory }))
}

export function confirmPatientLocation(requestId: string, stateCode: string, expectedVersion: number) {
  return json<TelehealthRequest>(
    `/api/telehealth/v1/patient/requests/${encodeURIComponent(requestId)}/location`,
    commandInit({ stateCode, expectedVersion }),
  )
}

export function evaluatePatientTriage(
  requestId: string,
  answers: {
    hasEmergencyWarning: boolean
    severeOrWorsening: boolean
    requiresHandsOnExam: boolean
    unsure: boolean
    expectedVersion: number
  },
) {
  return json<TelehealthRequest>(
    `/api/telehealth/v1/patient/requests/${encodeURIComponent(requestId)}/triage`,
    commandInit(answers),
  )
}

export function getPatientReadiness(requestId: string, signal?: AbortSignal) {
  return json<TelehealthReadiness>(
    `/api/telehealth/v1/patient/requests/${encodeURIComponent(requestId)}/readiness`,
    { headers: portalHeaders(), signal },
  )
}

export function completePatientReadiness(
  requestId: string,
  readiness: TelehealthReadiness,
  values: {
    complaintSummary: string
    symptomDuration: string
    coverageToken: string
  },
) {
  const coverage = readiness.coverageOptions.find((item) => item.coverageToken === values.coverageToken)
  if (!coverage) throw new Error('Choose a current coverage record before continuing.')
  return json<TelehealthRequest>(
    `/api/telehealth/v1/patient/requests/${encodeURIComponent(requestId)}/readiness`,
    commandInit({
      expectedVersion: readiness.requestVersion,
      demographicsFingerprint: readiness.patientDetails.fingerprint,
      clinicalSummaryFingerprint: readiness.clinicalSummary.fingerprint,
      demographicsConfirmed: true,
      contactConfirmed: true,
      clinicalSummaryConfirmed: true,
      complaintSummary: values.complaintSummary,
      symptomDuration: values.symptomDuration,
      syntheticDataConfirmed: true,
      coverageToken: coverage.coverageToken,
      coverageFingerprint: coverage.fingerprint,
      coverageConfirmed: true,
      acknowledgmentPackageKey: readiness.acknowledgment.packageKey,
      acknowledgmentPackageVersion: readiness.acknowledgment.packageVersion,
      acknowledgmentContentHash: readiness.acknowledgment.contentHash,
      acknowledgmentAccepted: true,
    }),
  )
}

export function verifyPatientCoverage(requestId: string, expectedVersion: number) {
  return json<TelehealthRequest>(
    `/api/telehealth/v1/patient/requests/${encodeURIComponent(requestId)}/coverage/verify`,
    commandInit({ expectedVersion }),
  )
}

export async function listOperationalReview(signal?: AbortSignal) {
  const result = await json<{ requests: TelehealthQueueItem[] }>('/api/telehealth/v1/admin/operational-review', {
    headers: clinicianHeaders(),
    signal,
  })
  return result.requests
}

export function listApplicantIdentityReview(signal?: AbortSignal) {
  return json<TelehealthApplicantIdentityReviewQueue>('/api/telehealth/v1/admin/applicant-identity-review', {
    headers: clinicianHeaders(),
    cache: 'no-store',
    signal,
  })
}

export function listApplicantPracticeReviewInbox(signal?: AbortSignal) {
  return json<TelehealthApplicantPracticeReviewInbox>('/api/telehealth/v1/admin/applicant-practice-review', {
    headers: clinicianHeaders(),
    cache: 'no-store',
    signal,
  })
}

export function claimApplicantPracticeReview(
  practiceReviewCaseId: string,
  input: {
    expectedApplicantVersion: number
    inboxPolicyVersion: 1
    noDecisionAcknowledged: true
    noPatientContactAcknowledged: true
    noRequestOrCareQueueAcknowledged: true
  },
  idempotencyKey: string,
) {
  return json<TelehealthApplicantPracticeReviewClaim>(
    `/api/telehealth/v1/admin/applicant-practice-review/${encodeURIComponent(practiceReviewCaseId)}/claim`,
    {
      method: 'POST',
      headers: clinicianHeaders({
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function getApplicantPracticeReviewPacket(
  practiceReviewCaseId: string,
  signal?: AbortSignal,
) {
  return json<TelehealthApplicantPracticeReviewPacket>(
    `/api/telehealth/v1/admin/applicant-practice-review/${encodeURIComponent(practiceReviewCaseId)}`,
    {
      headers: clinicianHeaders(),
      cache: 'no-store',
      signal,
    },
  )
}

export function authorizeApplicantPracticeReview(
  practiceReviewCaseId: string,
  input: {
    expectedApplicantVersion: number
    packetPolicyVersion: 1
    decision: 'AuthorizedForSyntheticRequestCreation'
    rationaleCode: 'OperationalPrerequisitesReviewed'
    noClinicalEligibilityAcknowledged: true
    noCoverageGuaranteeAcknowledged: true
    noRequestOrQueueAcknowledged: true
  },
  idempotencyKey: string,
) {
  return json<TelehealthApplicantPracticeReviewAuthorization>(
    `/api/telehealth/v1/admin/applicant-practice-review/${encodeURIComponent(practiceReviewCaseId)}/authorization`,
    {
      method: 'POST',
      headers: clinicianHeaders({
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function recordApplicantIdentityReview(
  applicantId: string,
  input: {
    expectedVersion: number
    decision: 'ApprovedForProspectiveIntake' | 'ManualReviewRequired'
    reason: string
    syntheticDataConfirmed: true
  },
  idempotencyKey: string,
) {
  return json<TelehealthApplicantIdentityReviewDecision>(
    `/api/telehealth/v1/admin/applicants/${encodeURIComponent(applicantId)}/identity-review-decision`,
    {
      method: 'PUT',
      headers: clinicianHeaders({
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function listApplicantPromotionAuthorization(signal?: AbortSignal) {
  return json<TelehealthApplicantPromotionAuthorizationQueue>(
    '/api/telehealth/v1/admin/applicant-promotion-authorization',
    {
      headers: clinicianHeaders(),
      cache: 'no-store',
      signal,
    },
  )
}

export function recordApplicantPromotionAuthorization(
  applicantId: string,
  input: {
    expectedVersion: number
    decision: TelehealthApplicantPromotionAuthorizationDecisionType
    reason: string
    noneAssuranceAcknowledged: true
    syntheticDataConfirmed: true
  },
  idempotencyKey: string,
) {
  return json<TelehealthApplicantPromotionAuthorizationDecision>(
    `/api/telehealth/v1/admin/applicants/${encodeURIComponent(applicantId)}/promotion-authorization-decision`,
    {
      method: 'PUT',
      headers: clinicianHeaders({
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function listApplicantSyntheticPromotion(signal?: AbortSignal) {
  return json<TelehealthApplicantSyntheticPromotionQueue>(
    '/api/telehealth/v1/admin/applicant-synthetic-promotion',
    {
      headers: clinicianHeaders(),
      cache: 'no-store',
      signal,
    },
  )
}

export function executeApplicantSyntheticPromotion(
  applicantId: string,
  input: {
    expectedVersion: number
    command: 'PromoteAuthorizedSyntheticApplicant'
    reason: string
    canonicalPatientCreationAcknowledged: true
    noPortalNoCareAcknowledged: true
  },
  idempotencyKey: string,
) {
  return json<TelehealthApplicantSyntheticPromotion>(
    `/api/telehealth/v1/admin/applicants/${encodeURIComponent(applicantId)}/synthetic-promotion`,
    {
      method: 'PUT',
      headers: clinicianHeaders({
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export function authorizeRequest(requestId: string, expectedVersion: number) {
  return json<TelehealthRequest>(
    `/api/telehealth/v1/admin/requests/${encodeURIComponent(requestId)}/authorize`,
    commandInit({ expectedVersion }, 'clinician'),
  )
}

export function getApplicantRequestQueueAuthorization(requestId: string, signal?: AbortSignal) {
  return json<TelehealthApplicantRequestQueueAuthorization>(
    `/api/telehealth/v1/admin/applicant-requests/${encodeURIComponent(requestId)}/queue-authorization`,
    {
      headers: clinicianHeaders(),
      cache: 'no-store',
      signal,
    },
  )
}

export function authorizeApplicantRequestToQueue(
  requestId: string,
  input: {
    expectedRequestVersion: number
    authorizationSnapshotFingerprint: string
    syntheticEvidenceReviewed: true
    noCoverageGuaranteeAcknowledged: true
    practiceAcceptsForQueueAcknowledged: true
    queueNotCareAcknowledged: true
  },
  idempotencyKey: string,
) {
  return json<TelehealthApplicantRequestQueueAuthorization>(
    `/api/telehealth/v1/admin/applicant-requests/${encodeURIComponent(requestId)}/queue-authorization`,
    {
      method: 'POST',
      headers: clinicianHeaders({
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify(input),
    },
  )
}

export async function listClinicianQueue(signal?: AbortSignal) {
  const result = await json<{ requests: TelehealthQueueItem[] }>('/api/telehealth/v1/clinician/queue', {
    headers: clinicianHeaders(),
    signal,
  })
  return result.requests
}

export function startClinicianShift() {
  return json<TelehealthShift>('/api/telehealth/v1/clinician/shifts', commandInit(undefined, 'clinician'))
}

export async function reserveNextRequest() {
  const response = await apiFetch(`${apiBaseUrl}/api/telehealth/v1/clinician/reservations/reserve-next`, commandInit(undefined, 'clinician'))
  return response.status === 204 ? null : ((await response.json()) as TelehealthReservation)
}

function connectionCommandInit(
  preflight: TelehealthDevicePreflight,
  expectedVersion: number,
  idempotencyKey: string,
  scope: 'portal' | 'clinician',
): RequestInit {
  const extra = {
    'Content-Type': 'application/json',
    'X-Idempotency-Key': idempotencyKey,
  }
  return {
    method: 'POST',
    headers: scope === 'portal' ? portalHeaders(extra) : clinicianHeaders(extra),
    body: JSON.stringify({ expectedVersion, ...preflight }),
  }
}

export function preparePatientConnection(
  requestId: string,
  expectedVersion: number,
  preflight: TelehealthDevicePreflight,
  idempotencyKey: string,
) {
  return json<TelehealthConnectionGrant>(
    `/api/telehealth/v1/patient/requests/${encodeURIComponent(requestId)}/connection-grants`,
    connectionCommandInit(preflight, expectedVersion, idempotencyKey, 'portal'),
  )
}

export function prepareApplicantConnection(
  applicantId: string,
  applicantAccessKey: string,
  requestId: string,
  expectedVersion: number,
  preflight: TelehealthDevicePreflight,
  idempotencyKey: string,
) {
  return json<TelehealthConnectionGrant>(
    `/api/telehealth/v1/applicants/${encodeURIComponent(applicantId)}/telehealth-request/${encodeURIComponent(requestId)}/connection-grants`,
    {
      method: 'POST',
      headers: applicantHeaders(applicantAccessKey, {
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      cache: 'no-store',
      body: JSON.stringify({ expectedVersion, ...preflight }),
    },
  )
}

export function preparePhysicianConnection(
  reservationId: string,
  expectedVersion: number,
  preflight: TelehealthDevicePreflight,
  idempotencyKey: string,
) {
  return json<TelehealthConnectionGrant>(
    `/api/telehealth/v1/clinician/reservations/${encodeURIComponent(reservationId)}/connection-grants`,
    connectionCommandInit(preflight, expectedVersion, idempotencyKey, 'clinician'),
  )
}

export function startTelehealthConsultation(
  reservationId: string,
  input: TelehealthConsultationStartInput,
  idempotencyKey: string,
) {
  return json<TelehealthConsultationStart>(
    `/api/telehealth/v1/clinician/reservations/${encodeURIComponent(reservationId)}/consultations/start`,
    {
      method: 'POST',
      headers: clinicianHeaders({
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      body: JSON.stringify(input),
    },
  )
}

export function getTelehealthConsultationWorkspace(consultationId: string, signal?: AbortSignal) {
  return json<TelehealthConsultationWorkspace>(
    `/api/telehealth/v1/clinician/consultations/${encodeURIComponent(consultationId)}/workspace`,
    { headers: clinicianHeaders(), cache: 'no-store', signal },
  )
}

export function saveTelehealthConsultationDocumentationDraft(
  consultationId: string,
  input: TelehealthConsultationDocumentationDraftInput,
) {
  return json<TelehealthConsultationDocumentationDraft>(
    `/api/telehealth/v1/clinician/consultations/${encodeURIComponent(consultationId)}/documentation/draft`,
    {
      method: 'PUT',
      headers: clinicianHeaders({ 'Content-Type': 'application/json' }),
      body: JSON.stringify(input),
      cache: 'no-store',
    },
  )
}

export function enterTelehealthConsultationWrapUp(
  consultationId: string,
  input: EnterTelehealthConsultationWrapUpInput,
  idempotencyKey: string,
) {
  return json<TelehealthConsultationWrapUp>(
    `/api/telehealth/v1/clinician/consultations/${encodeURIComponent(consultationId)}/wrap-up`,
    {
      method: 'POST',
      headers: clinicianHeaders({
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      body: JSON.stringify(input),
      cache: 'no-store',
    },
  )
}

export function getTelehealthPharmacyChoices(
  consultationId: string,
  search: TelehealthPharmacySearch,
  signal?: AbortSignal,
) {
  const parameters = new URLSearchParams()
  if (search.query) parameters.set('query', search.query)
  if (search.state) parameters.set('state', search.state)
  if (search.postalCode) parameters.set('postalCode', search.postalCode)
  if (search.originPostalCode) parameters.set('originPostalCode', search.originPostalCode)
  if (search.locationSearchAcknowledged) parameters.set('locationSearchAcknowledged', 'true')
  if (search.limit) parameters.set('limit', String(search.limit))
  const query = parameters.size ? `?${parameters.toString()}` : ''
  return json<TelehealthPharmacyChoiceWorkspace>(
    `/api/telehealth/v1/clinician/consultations/${encodeURIComponent(consultationId)}/pharmacy-choices${query}`,
    { headers: clinicianHeaders(), cache: 'no-store', signal },
  )
}

export function recordTelehealthPharmacyChoice(
  consultationId: string,
  expectedVersion: number,
  directoryEntryId: string,
  idempotencyKey: string,
) {
  return json<TelehealthPharmacyChoiceDraft>(
    `/api/telehealth/v1/clinician/consultations/${encodeURIComponent(consultationId)}/pharmacy-choice`,
    {
      method: 'PUT',
      headers: clinicianHeaders({
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      body: JSON.stringify({
        expectedVersion,
        directoryEntryId,
        patientChoiceConfirmed: true,
        syntheticDataConfirmed: true,
      }),
      cache: 'no-store',
    },
  )
}

export function getTelehealthSafetyDispositionDraft(
  consultationId: string,
  signal?: AbortSignal,
) {
  return json<TelehealthSafetyDispositionWorkspace>(
    `/api/telehealth/v1/clinician/consultations/${encodeURIComponent(consultationId)}/safety-disposition-draft`,
    { headers: clinicianHeaders(), cache: 'no-store', signal },
  )
}

export function getTelehealthCompletionPrerequisites(
  consultationId: string,
  signal?: AbortSignal,
) {
  return json<TelehealthCompletionPrerequisites>(
    `/api/telehealth/v1/clinician/consultations/${encodeURIComponent(consultationId)}/completion-prerequisites`,
    { headers: clinicianHeaders(), cache: 'no-store', signal },
  )
}

export function getTelehealthPrescriptionPreparationDraft(
  consultationId: string,
  query?: string,
  signal?: AbortSignal,
) {
  const parameters = new URLSearchParams()
  if (query?.trim()) parameters.set('query', query.trim())
  const suffix = parameters.size ? `?${parameters.toString()}` : ''
  return json<TelehealthPrescriptionPreparationWorkspace>(
    `/api/telehealth/v1/clinician/consultations/${encodeURIComponent(consultationId)}/prescription-preparation-draft${suffix}`,
    { headers: clinicianHeaders(), cache: 'no-store', signal },
  )
}

export function recordTelehealthPrescriptionPreparationDraft(
  consultationId: string,
  input: TelehealthPrescriptionPreparationDraftInput,
  idempotencyKey: string,
) {
  return json<TelehealthPrescriptionPreparationDraft>(
    `/api/telehealth/v1/clinician/consultations/${encodeURIComponent(consultationId)}/prescription-preparation-draft`,
    {
      method: 'PUT',
      headers: clinicianHeaders({
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      body: JSON.stringify(input),
      cache: 'no-store',
    },
  )
}

export function recordTelehealthSafetyDispositionDraft(
  consultationId: string,
  input: TelehealthSafetyDispositionDraftInput,
  idempotencyKey: string,
) {
  return json<TelehealthSafetyDispositionDraft>(
    `/api/telehealth/v1/clinician/consultations/${encodeURIComponent(consultationId)}/safety-disposition-draft`,
    {
      method: 'PUT',
      headers: clinicianHeaders({
        'Content-Type': 'application/json',
        'X-Idempotency-Key': idempotencyKey,
      }),
      body: JSON.stringify(input),
      cache: 'no-store',
    },
  )
}
