// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { ApiRequestError, isRequestCancellation } from '../../api/transport.ts'
import {
  acknowledgeApplicantPreRequestReadiness,
  acknowledgeApplicantTelehealthNotice,
  assessApplicantTelehealthRequestComplaintTriage,
  assessApplicantTelehealthRequestUniversalSafety,
  confirmApplicantTelehealthRequestInsuranceSource,
  confirmApplicantTelehealthRequestIntake,
  confirmApplicantInsuranceHandoff,
  confirmApplicantClinicalInformationSummary,
  confirmApplicantRegistrationDetails,
  confirmApplicantTelehealthRequestLocation,
  createProspectiveApplicant,
  createApplicantTelehealthRequest,
  evaluateProspectiveSafetyTriage,
  getApplicantClinicalInformationInventory,
  getApplicantAllergyInformation,
  getApplicantHealthHistoryInformation,
  getApplicantClinicalInformationSummary,
  getApplicantPreRequestReadiness,
  getApplicantPracticeReviewSubmission,
  getApplicantTelehealthRequest,
  getApplicantTelehealthRequestComplaintTriage,
  getApplicantTelehealthRequestInsuranceSource,
  getApplicantTelehealthRequestEligibility,
  getApplicantTelehealthRequestPracticeNetwork,
  getApplicantTelehealthRequestRenderingCandidate,
  getApplicantTelehealthRequestIntake,
  getApplicantTelehealthRequestLocation,
  getApplicantTelehealthRequestUniversalSafety,
  getApplicantMedicationInformation,
  getApplicantDevicePreparation,
  getApplicantTelehealthNotice,
  getApplicantCommunicationAccessReadiness,
  getApplicantInsuranceHandoff,
  getApplicantRegistrationDetails,
  getProspectivePracticeNetworkOptions,
  getProspectiveApplicant,
  recordProspectiveIdentityProofing,
  recordProspectiveEligibility,
  recordProspectivePracticeNetwork,
  recordProspectiveMemberInsuranceDetails,
  recordProspectivePracticeNetworkPrecheck,
  recordProspectiveVisitPurpose,
  recordApplicantCommunicationAccessReadiness,
  recordApplicantClinicalInformationInventory,
  recordApplicantAllergyInformation,
  recordApplicantHealthHistoryInformation,
  recordApplicantMedicationInformation,
  recordApplicantDevicePreparation,
  runApplicantTelehealthRequestEligibility,
  runApplicantTelehealthRequestPracticeNetwork,
  selectApplicantTelehealthRequestRenderingCandidate,
  submitApplicantPracticeReview,
  verifyProspectiveApplicantContact,
  type TelehealthProspectiveApplicant,
  type TelehealthProspectiveApplicantInput,
  type TelehealthProspectivePracticeNetworkOption,
  type TelehealthProspectivePracticeNetworkOptions,
  type TelehealthProspectivePracticeNetworkPrecheck,
  type TelehealthProspectivePracticeNetworkPrecheckInput,
  type TelehealthProspectiveMemberInsuranceDetails,
  type TelehealthProspectiveMemberInsuranceDetailsInput,
  type TelehealthProspectiveEligibility,
  type TelehealthProspectiveEligibilityInput,
  type TelehealthProspectiveIdentityProofing,
  type TelehealthProspectiveIdentityProofingInput,
  type TelehealthProspectivePracticeNetwork,
  type TelehealthProspectivePracticeNetworkInput,
  type TelehealthProspectiveSafetyTriage,
  type TelehealthProspectiveSafetyTriageInput,
  type TelehealthProspectiveVisitPurpose,
  type TelehealthProspectiveVisitPurposeInput,
  type TelehealthApplicantNotice,
  type TelehealthApplicantNoticeInput,
  type TelehealthApplicantRegistrationDetails,
  type TelehealthApplicantRegistrationDetailsInput,
  type TelehealthApplicantInsuranceHandoff,
  type TelehealthApplicantInsuranceHandoffInput,
  type TelehealthApplicantCommunicationAccessReadiness,
  type TelehealthApplicantCommunicationAccessReadinessInput,
  type TelehealthApplicantClinicalInformationCategoryStatus,
  type TelehealthApplicantClinicalInformationInventory,
  type TelehealthApplicantClinicalInformationInventoryInput,
  type TelehealthApplicantMedicationInformation,
  type TelehealthApplicantMedicationInformationInput,
  type TelehealthApplicantReportedMedicationUseStatus,
  type TelehealthApplicantAllergyInformation,
  type TelehealthApplicantAllergyInformationInput,
  type TelehealthApplicantHealthHistoryInformation,
  type TelehealthApplicantHealthHistoryInformationInput,
  type TelehealthApplicantClinicalInformationSummary,
  type TelehealthApplicantClinicalInformationSummaryInput,
  type TelehealthApplicantPreRequestReadiness,
  type TelehealthApplicantPreRequestReadinessInput,
  type TelehealthApplicantPracticeReview,
  type TelehealthApplicantPracticeReviewInput,
  type TelehealthApplicantRequestCreation,
  type TelehealthApplicantRequestCreationInput,
  type TelehealthApplicantRequestComplaintTriage,
  type TelehealthApplicantRequestComplaintTriageInput,
  type TelehealthApplicantRequestInsuranceSource,
  type TelehealthApplicantRequestInsuranceSourceInput,
  type TelehealthApplicantRequestEligibility,
  type TelehealthApplicantRequestEligibilityInput,
  type TelehealthApplicantRequestPracticeNetwork,
  type TelehealthApplicantRequestPracticeNetworkInput,
  type TelehealthApplicantRequestRenderingCandidate,
  type TelehealthApplicantRequestRenderingCandidateInput,
  type TelehealthApplicantRequestIntake,
  type TelehealthApplicantRequestIntakeInput,
  type TelehealthApplicantRequestLocation,
  type TelehealthApplicantRequestLocationInput,
  type TelehealthApplicantRequestUniversalSafety,
  type TelehealthApplicantRequestUniversalSafetyInput,
  type TelehealthApplicantDevicePreparation,
  type TelehealthApplicantDevicePreparationInput,
  type TelehealthSyntheticComplaintAnswer,
} from './api.ts'
import {
  clearApplicantSession,
  createApplicantAccessKey,
  loadApplicantSession,
  saveApplicantSession,
  type TelehealthApplicantSession,
} from './applicantSession.ts'
import { runTelehealthDevicePreflight, type TelehealthPreflightResult } from './devicePreflight.ts'
import './telehealth.css'

type FormValues = Omit<TelehealthProspectiveApplicantInput, 'syntheticDataConfirmed'> & {
  syntheticDataConfirmed: boolean
}

const initialValues: FormValues = {
  legalFirstName: '',
  legalLastName: '',
  dateOfBirth: '',
  email: '',
  phone: '',
  residenceStateCode: '',
  postalCode: '',
  syntheticDataConfirmed: false,
}

type PendingCreate = { content: string; applicantAccessKey: string; idempotencyKey: string }
type PendingVerification = { content: string; idempotencyKey: string }
type PendingSafetyTriage = { content: string; idempotencyKey: string }
type PendingVisitPurpose = { content: string; idempotencyKey: string }
type PendingPracticeNetworkPrecheck = { content: string; idempotencyKey: string }
type PendingMemberInsuranceDetails = { content: string; idempotencyKey: string }
type PendingEligibility = { content: string; idempotencyKey: string }
type PendingPracticeNetwork = { content: string; idempotencyKey: string }
type PendingIdentityProofing = { content: string; idempotencyKey: string }
type PendingNoticeAcknowledgment = { content: string; idempotencyKey: string }
type PendingRegistrationDetailsConfirmation = { content: string; idempotencyKey: string }
type PendingInsuranceHandoffConfirmation = { content: string; idempotencyKey: string }
type PendingCommunicationAccessReadiness = { content: string; idempotencyKey: string }
type PendingDevicePreparation = { content: string; idempotencyKey: string }
type PendingClinicalInformationInventory = { content: string; idempotencyKey: string }
type PendingMedicationInformation = { content: string; idempotencyKey: string }
type PendingAllergyInformation = { content: string; idempotencyKey: string }
type PendingHealthHistoryInformation = { content: string; idempotencyKey: string }
type PendingClinicalInformationSummary = { content: string; idempotencyKey: string }
type PendingPreRequestReadiness = { content: string; idempotencyKey: string }
type PendingPracticeReview = { content: string; idempotencyKey: string }
type PendingRequestCreation = { content: string; idempotencyKey: string }
type PendingRequestLocation = { content: string; idempotencyKey: string }
type PendingRequestSafety = { content: string; idempotencyKey: string }
type PendingRequestComplaintTriage = { content: string; idempotencyKey: string }
type PendingRequestIntake = { content: string; idempotencyKey: string }
type PendingRequestInsuranceSource = { content: string; idempotencyKey: string }
type PendingRequestEligibility = { content: string; idempotencyKey: string }
type PendingRequestPracticeNetwork = { content: string; idempotencyKey: string }
type PendingRequestRenderingCandidate = { content: string; idempotencyKey: string }
type YesNoAnswer = '' | 'yes' | 'no'
type ComplaintAnswer = '' | TelehealthSyntheticComplaintAnswer

const initialSafetyAnswers = {
  emergency: '' as YesNoAnswer,
  severe: '' as YesNoAnswer,
  handsOn: '' as YesNoAnswer,
  unsure: '' as YesNoAnswer,
}

const initialNoticeAcknowledgments = {
  location: false,
  mode: false,
  privacy: false,
  emergency: false,
  inPerson: false,
  clinicianReconfirmation: false,
  synthetic: false,
}

const initialRegistrationDetailsConfirmations = {
  nameAndBirthDate: false,
  contacts: false,
  residence: false,
  noCorrections: false,
  synthetic: false,
}

const initialInsuranceHandoffConfirmations = {
  payerAndProduct: false,
  memberDetails: false,
  subscriberRelationship: false,
  evidenceLimitations: false,
  synthetic: false,
}

const initialCommunicationAccessConfirmations = {
  currentLocation: false,
  callbackNumber: false,
  safeAndPrivate: false,
  disconnectionAndEmergencyPlan: false,
  synthetic: false,
}

const initialDevicePreparationAcknowledgments = {
  clientReported: false,
  noReadinessGuarantee: false,
  recheckBeforeConsultation: false,
}

const initialClinicalInformationInventoryAcknowledgments = {
  patientReportedMayBeIncomplete: false,
  noClinicalDetailsCaptured: false,
  clinicianReconciliationRequired: false,
}

const initialMedicationInformationAcknowledgments = {
  patientReportedMayBeIncomplete: false,
  syntheticCatalogIncomplete: false,
  noDoseOrDirectionsCaptured: false,
  clinicianReconciliationRequired: false,
}

const initialAllergyInformationAcknowledgments = {
  patientReportedMayBeIncomplete: false,
  syntheticCatalogIncomplete: false,
  noReactionOrCriticalityCaptured: false,
  clinicianVerificationRequired: false,
}

const initialHealthHistoryInformationAcknowledgments = {
  patientReportedMayBeIncomplete: false,
  topicSelectionIsNotDiagnosis: false,
  noStatusOrTimingCaptured: false,
  clinicianVerificationRequired: false,
}

const initialClinicalInformationSummaryAcknowledgments = {
  patientReportedMayBeIncomplete: false,
  notClinicallyVerifiedOrReconciled: false,
  noIntakeCompletionOrEligibility: false,
  correctionRequiresSeparateWorkflow: false,
}

const initialPreRequestReadinessAcknowledgments = {
  priorSectionsReviewed: false,
  outstandingStepsRemain: false,
  noRequestOrQueueCreated: false,
  correctionRequiresSeparateWorkflow: false,
}

const initialPracticeReviewAcknowledgments = {
  patientReported: false,
  practiceMayRequestInformationOrDecline: false,
  noTelehealthRequestOrCareQueue: false,
  worseningSymptomsRequireImmediateAction: false,
}

const initialRequestCreationAcknowledgments = {
  createRequest: false,
  noQueueOrCare: false,
  urgentOrWorseningSymptomsRequireImmediateAction: false,
}

const initialRequestLocationAcknowledgments = {
  currentLocation: false,
  callbackNumber: false,
  changedLocationRequiresRestart: false,
  urgentOrWorseningSymptomsRequireImmediateAction: false,
}

const initialRequestSafetyAnswers = {
  emergency: '' as YesNoAnswer,
  severe: '' as YesNoAnswer,
  handsOn: '' as YesNoAnswer,
  unsure: '' as YesNoAnswer,
}

const initialRequestSafetyConfirmations = {
  currentLocation: false,
  callbackNumber: false,
  syntheticData: false,
}

const initialMigraineComplaintAnswers = {
  suddenOrWorstOnset: '' as ComplaintAnswer,
  newNeurologicOrVisionChange: '' as ComplaintAnswer,
  feverOrStiffNeck: '' as ComplaintAnswer,
  recentHeadInjury: '' as ComplaintAnswer,
  pregnantOrPostpartum: '' as ComplaintAnswer,
  cancerOrImmunocompromised: '' as ComplaintAnswer,
  knownSimilarPattern: '' as ComplaintAnswer,
  persistentVomiting: '' as ComplaintAnswer,
}

const initialSleepComplaintAnswers = {
  selfHarmThoughts: '' as ComplaintAnswer,
  maniaOrPsychosis: '' as ComplaintAnswer,
  dangerousSomnolence: '' as ComplaintAnswer,
  withdrawalConcern: '' as ComplaintAnswer,
  breathingPausesOrSevereSnoring: '' as ComplaintAnswer,
  pregnantOrComplexMedicationConcern: '' as ComplaintAnswer,
  controlledSedativeRequest: '' as ComplaintAnswer,
  uncomplicatedSleepDifficulty: '' as ComplaintAnswer,
}

const initialRequestComplaintTriageConfirmations = {
  currentLocation: false,
  callbackNumber: false,
  syntheticData: false,
}

const initialRequestIntakeConfirmations = {
  currentLocation: false,
  callbackNumber: false,
  priorInformation: false,
  insuranceLimitations: false,
  pendingConsent: false,
  pendingVerification: false,
  complaintResult: false,
  syntheticData: false,
}

const initialRequestInsuranceSourceConfirmations = {
  payerProduct: false,
  maskedMemberDetails: false,
  subscriberRelationship: false,
  primaryCoverageSource: false,
  freshVerification: false,
  evidenceLimitations: false,
  syntheticData: false,
}

const initialRequestEligibilityAcknowledgments = {
  syntheticData: false,
  noGuarantee: false,
}

const initialRequestPracticeNetworkAcknowledgments = {
  syntheticData: false,
  practiceOnly: false,
  noGuarantee: false,
}

const initialRequestRenderingCandidateAcknowledgments = {
  syntheticData: false,
  candidateOnly: false,
  noAssignment: false,
  networkCheckStillRequired: false,
}

function ComplaintAnswerField({
  legend,
  name,
  answer,
  onChange,
}: {
  legend: string
  name: string
  answer: ComplaintAnswer
  onChange: (answer: TelehealthSyntheticComplaintAnswer) => void
}) {
  return (
    <fieldset>
      <legend>{legend}</legend>
      {(['Yes', 'No', 'NotSure'] as const).map((choice) => (
        <label className="telehealth-check" key={choice}>
          <input
            required
            type="radio"
            name={name}
            value={choice}
            checked={answer === choice}
            onChange={() => onChange(choice)}
          />
          <span>{choice === 'NotSure' ? 'Not sure' : choice}</span>
        </label>
      ))}
    </fieldset>
  )
}

function requireComplaintAnswer(answer: ComplaintAnswer): TelehealthSyntheticComplaintAnswer {
  if (!answer) throw new Error('Answer every complaint-specific question before continuing.')
  return answer
}

function clinicalInformationStatusLabel(status: TelehealthApplicantClinicalInformationCategoryStatus | null) {
  if (status === 'PatientReportsNone') return 'Patient reports none — not clinically reconciled'
  if (status === 'ItemsToReview') return 'Items need detailed review'
  if (status === 'Unsure') return 'Patient is unsure'
  return 'Not recorded'
}

function clinicalInformationSummaryCategoryLabel(categoryKey: 'Medications' | 'AllergiesOrIntolerances' | 'OtherHealthHistory') {
  if (categoryKey === 'Medications') return 'Medications'
  if (categoryKey === 'AllergiesOrIntolerances') return 'Allergies or intolerances'
  return 'Other health history'
}

function preRequestReadinessSectionLabel(
  sectionKey: 'Registration' | 'Insurance' | 'CommunicationAccess' | 'DevicePreparation' | 'ClinicalInformation',
) {
  if (sectionKey === 'Registration') return 'Registration details'
  if (sectionKey === 'Insurance') return 'Insurance evidence'
  if (sectionKey === 'CommunicationAccess') return 'Communication and access'
  if (sectionKey === 'DevicePreparation') return 'Device preparation'
  return 'Clinical information'
}

export default function ProspectivePatientTelehealthEntry() {
  const [values, setValues] = useState<FormValues>(initialValues)
  const [applicantSession, setApplicantSession] = useState<TelehealthApplicantSession | null>(() => loadApplicantSession())
  const [applicant, setApplicant] = useState<TelehealthProspectiveApplicant | null>(null)
  const [verificationCode, setVerificationCode] = useState('')
  const [currentLocationStateCode, setCurrentLocationStateCode] = useState<'' | 'GA' | 'CA' | 'FL'>('')
  const [locationConfirmed, setLocationConfirmed] = useState(false)
  const [safetyAnswers, setSafetyAnswers] = useState(initialSafetyAnswers)
  const [safetySyntheticConfirmed, setSafetySyntheticConfirmed] = useState(false)
  const [safetyResult, setSafetyResult] = useState<TelehealthProspectiveSafetyTriage | null>(null)
  const [purposeCategory, setPurposeCategory] = useState<'' | 'migraine' | 'sleep'>('')
  const [purposeSyntheticConfirmed, setPurposeSyntheticConfirmed] = useState(false)
  const [purposeResult, setPurposeResult] = useState<TelehealthProspectiveVisitPurpose | null>(null)
  const [networkOptions, setNetworkOptions] = useState<TelehealthProspectivePracticeNetworkOptions | null>(null)
  const [networkOptionsLoading, setNetworkOptionsLoading] = useState(false)
  const [networkOptionsLoadAttempt, setNetworkOptionsLoadAttempt] = useState(0)
  const [planKey, setPlanKey] = useState<'' | TelehealthProspectivePracticeNetworkOption['planKey']>('')
  const [networkSyntheticConfirmed, setNetworkSyntheticConfirmed] = useState(false)
  const [networkResult, setNetworkResult] = useState<TelehealthProspectivePracticeNetworkPrecheck | null>(null)
  const [memberId, setMemberId] = useState('')
  const [groupNumber, setGroupNumber] = useState('')
  const [subscriberRelationship, setSubscriberRelationship] = useState<'' | TelehealthProspectiveMemberInsuranceDetailsInput['subscriberRelationship']>('')
  const [subscriberFirstName, setSubscriberFirstName] = useState('')
  const [subscriberLastName, setSubscriberLastName] = useState('')
  const [subscriberDateOfBirth, setSubscriberDateOfBirth] = useState('')
  const [memberDetailsConfirmed, setMemberDetailsConfirmed] = useState(false)
  const [memberSyntheticConfirmed, setMemberSyntheticConfirmed] = useState(false)
  const [memberDetailsResult, setMemberDetailsResult] = useState<TelehealthProspectiveMemberInsuranceDetails | null>(null)
  const [eligibilitySyntheticConfirmed, setEligibilitySyntheticConfirmed] = useState(false)
  const [eligibilityResult, setEligibilityResult] = useState<TelehealthProspectiveEligibility | null>(null)
  const [practiceNetworkSyntheticConfirmed, setPracticeNetworkSyntheticConfirmed] = useState(false)
  const [practiceNetworkResult, setPracticeNetworkResult] = useState<TelehealthProspectivePracticeNetwork | null>(null)
  const [identityPrivacyNoticeAcknowledged, setIdentityPrivacyNoticeAcknowledged] = useState(false)
  const [identitySyntheticConfirmed, setIdentitySyntheticConfirmed] = useState(false)
  const [identityProofingResult, setIdentityProofingResult] = useState<TelehealthProspectiveIdentityProofing | null>(null)
  const [notice, setNotice] = useState<TelehealthApplicantNotice | null>(null)
  const [noticeLoading, setNoticeLoading] = useState(false)
  const [noticeLoadAttempt, setNoticeLoadAttempt] = useState(0)
  const [noticeAcknowledgments, setNoticeAcknowledgments] = useState(initialNoticeAcknowledgments)
  const [registrationDetails, setRegistrationDetails] = useState<TelehealthApplicantRegistrationDetails | null>(null)
  const [registrationDetailsLoading, setRegistrationDetailsLoading] = useState(false)
  const [registrationDetailsLoadAttempt, setRegistrationDetailsLoadAttempt] = useState(0)
  const [registrationDetailsConfirmations, setRegistrationDetailsConfirmations] = useState(initialRegistrationDetailsConfirmations)
  const [insuranceHandoff, setInsuranceHandoff] = useState<TelehealthApplicantInsuranceHandoff | null>(null)
  const [insuranceHandoffLoading, setInsuranceHandoffLoading] = useState(false)
  const [insuranceHandoffLoadAttempt, setInsuranceHandoffLoadAttempt] = useState(0)
  const [insuranceHandoffConfirmations, setInsuranceHandoffConfirmations] = useState(initialInsuranceHandoffConfirmations)
  const [communicationAccessReadiness, setCommunicationAccessReadiness] = useState<TelehealthApplicantCommunicationAccessReadiness | null>(null)
  const [communicationAccessLoading, setCommunicationAccessLoading] = useState(false)
  const [communicationAccessLoadAttempt, setCommunicationAccessLoadAttempt] = useState(0)
  const [preferredSpokenLanguage, setPreferredSpokenLanguage] = useState<'' | 'English' | 'Spanish'>('')
  const [interpreterRequested, setInterpreterRequested] = useState(false)
  const [accessibilitySupportRequested, setAccessibilitySupportRequested] = useState(false)
  const [communicationAccessConfirmations, setCommunicationAccessConfirmations] = useState(initialCommunicationAccessConfirmations)
  const [devicePreparation, setDevicePreparation] = useState<TelehealthApplicantDevicePreparation | null>(null)
  const [devicePreparationLoading, setDevicePreparationLoading] = useState(false)
  const [devicePreparationLoadAttempt, setDevicePreparationLoadAttempt] = useState(0)
  const [deviceCheckResult, setDeviceCheckResult] = useState<TelehealthPreflightResult | null>(null)
  const [deviceCheckRunning, setDeviceCheckRunning] = useState(false)
  const [devicePreparationAcknowledgments, setDevicePreparationAcknowledgments] = useState(initialDevicePreparationAcknowledgments)
  const [clinicalInformationInventory, setClinicalInformationInventory] = useState<TelehealthApplicantClinicalInformationInventory | null>(null)
  const [clinicalInformationInventoryLoading, setClinicalInformationInventoryLoading] = useState(false)
  const [clinicalInformationInventoryLoadAttempt, setClinicalInformationInventoryLoadAttempt] = useState(0)
  const [medicationsInventoryStatus, setMedicationsInventoryStatus] = useState<'' | TelehealthApplicantClinicalInformationCategoryStatus>('')
  const [allergiesInventoryStatus, setAllergiesInventoryStatus] = useState<'' | TelehealthApplicantClinicalInformationCategoryStatus>('')
  const [otherHealthHistoryInventoryStatus, setOtherHealthHistoryInventoryStatus] = useState<'' | TelehealthApplicantClinicalInformationCategoryStatus>('')
  const [clinicalInformationInventoryAcknowledgments, setClinicalInformationInventoryAcknowledgments] = useState(initialClinicalInformationInventoryAcknowledgments)
  const [medicationInformation, setMedicationInformation] = useState<TelehealthApplicantMedicationInformation | null>(null)
  const [medicationInformationLoading, setMedicationInformationLoading] = useState(false)
  const [medicationInformationLoadAttempt, setMedicationInformationLoadAttempt] = useState(0)
  const [reportedMedicationUseStatuses, setReportedMedicationUseStatuses] = useState<Record<string, '' | TelehealthApplicantReportedMedicationUseStatus>>({})
  const [additionalOrUnlistedMedicationReported, setAdditionalOrUnlistedMedicationReported] = useState(false)
  const [medicationInformationAcknowledgments, setMedicationInformationAcknowledgments] = useState(initialMedicationInformationAcknowledgments)
  const [allergyInformation, setAllergyInformation] = useState<TelehealthApplicantAllergyInformation | null>(null)
  const [allergyInformationLoading, setAllergyInformationLoading] = useState(false)
  const [allergyInformationLoadAttempt, setAllergyInformationLoadAttempt] = useState(0)
  const [reportedAllergyItems, setReportedAllergyItems] = useState<Record<string, boolean>>({})
  const [additionalOrUnlistedAllergyReported, setAdditionalOrUnlistedAllergyReported] = useState(false)
  const [allergyInformationAcknowledgments, setAllergyInformationAcknowledgments] = useState(initialAllergyInformationAcknowledgments)
  const [healthHistoryInformation, setHealthHistoryInformation] = useState<TelehealthApplicantHealthHistoryInformation | null>(null)
  const [healthHistoryInformationLoading, setHealthHistoryInformationLoading] = useState(false)
  const [healthHistoryInformationLoadAttempt, setHealthHistoryInformationLoadAttempt] = useState(0)
  const [reportedHealthHistoryTopics, setReportedHealthHistoryTopics] = useState<Record<string, boolean>>({})
  const [additionalOrUnlistedHealthHistoryReported, setAdditionalOrUnlistedHealthHistoryReported] = useState(false)
  const [healthHistoryInformationAcknowledgments, setHealthHistoryInformationAcknowledgments] = useState(initialHealthHistoryInformationAcknowledgments)
  const [clinicalInformationSummary, setClinicalInformationSummary] = useState<TelehealthApplicantClinicalInformationSummary | null>(null)
  const [clinicalInformationSummaryLoading, setClinicalInformationSummaryLoading] = useState(false)
  const [clinicalInformationSummaryLoadAttempt, setClinicalInformationSummaryLoadAttempt] = useState(0)
  const [clinicalInformationSummaryAcknowledgments, setClinicalInformationSummaryAcknowledgments] = useState(initialClinicalInformationSummaryAcknowledgments)
  const [preRequestReadiness, setPreRequestReadiness] = useState<TelehealthApplicantPreRequestReadiness | null>(null)
  const [preRequestReadinessLoading, setPreRequestReadinessLoading] = useState(false)
  const [preRequestReadinessLoadAttempt, setPreRequestReadinessLoadAttempt] = useState(0)
  const [preRequestReadinessAcknowledgments, setPreRequestReadinessAcknowledgments] = useState(initialPreRequestReadinessAcknowledgments)
  const [practiceReview, setPracticeReview] = useState<TelehealthApplicantPracticeReview | null>(null)
  const [practiceReviewLoading, setPracticeReviewLoading] = useState(false)
  const [practiceReviewLoadAttempt, setPracticeReviewLoadAttempt] = useState(0)
  const [practiceReviewAcknowledgments, setPracticeReviewAcknowledgments] = useState(initialPracticeReviewAcknowledgments)
  const [requestCreation, setRequestCreation] = useState<TelehealthApplicantRequestCreation | null>(null)
  const [requestCreationLoading, setRequestCreationLoading] = useState(false)
  const [requestCreationLoadAttempt, setRequestCreationLoadAttempt] = useState(0)
  const [requestCreationAcknowledgments, setRequestCreationAcknowledgments] = useState(initialRequestCreationAcknowledgments)
  const [requestLocation, setRequestLocation] = useState<TelehealthApplicantRequestLocation | null>(null)
  const [requestLocationLoading, setRequestLocationLoading] = useState(false)
  const [requestLocationLoadAttempt, setRequestLocationLoadAttempt] = useState(0)
  const [requestLocationStateCode, setRequestLocationStateCode] = useState<'' | 'GA' | 'CA' | 'FL'>('')
  const [requestLocationAcknowledgments, setRequestLocationAcknowledgments] = useState(initialRequestLocationAcknowledgments)
  const [requestSafety, setRequestSafety] = useState<TelehealthApplicantRequestUniversalSafety | null>(null)
  const [requestSafetyLoading, setRequestSafetyLoading] = useState(false)
  const [requestSafetyLoadAttempt, setRequestSafetyLoadAttempt] = useState(0)
  const [requestSafetyAnswers, setRequestSafetyAnswers] = useState(initialRequestSafetyAnswers)
  const [requestSafetyConfirmations, setRequestSafetyConfirmations] = useState(initialRequestSafetyConfirmations)
  const [requestComplaintTriage, setRequestComplaintTriage] = useState<TelehealthApplicantRequestComplaintTriage | null>(null)
  const [requestComplaintTriageLoading, setRequestComplaintTriageLoading] = useState(false)
  const [requestComplaintTriageLoadAttempt, setRequestComplaintTriageLoadAttempt] = useState(0)
  const [migraineComplaintAnswers, setMigraineComplaintAnswers] = useState(initialMigraineComplaintAnswers)
  const [sleepComplaintAnswers, setSleepComplaintAnswers] = useState(initialSleepComplaintAnswers)
  const [requestComplaintTriageConfirmations, setRequestComplaintTriageConfirmations] = useState(initialRequestComplaintTriageConfirmations)
  const [requestIntake, setRequestIntake] = useState<TelehealthApplicantRequestIntake | null>(null)
  const [requestIntakeLoading, setRequestIntakeLoading] = useState(false)
  const [requestIntakeLoadAttempt, setRequestIntakeLoadAttempt] = useState(0)
  const [requestIntakeSymptomDuration, setRequestIntakeSymptomDuration] = useState<'' | TelehealthApplicantRequestIntakeInput['symptomDuration']>('')
  const [requestIntakeConfirmations, setRequestIntakeConfirmations] = useState(initialRequestIntakeConfirmations)
  const [requestInsuranceSource, setRequestInsuranceSource] = useState<TelehealthApplicantRequestInsuranceSource | null>(null)
  const [requestInsuranceSourceLoading, setRequestInsuranceSourceLoading] = useState(false)
  const [requestInsuranceSourceLoadAttempt, setRequestInsuranceSourceLoadAttempt] = useState(0)
  const [requestInsuranceSourceConfirmations, setRequestInsuranceSourceConfirmations] = useState(initialRequestInsuranceSourceConfirmations)
  const [requestEligibility, setRequestEligibility] = useState<TelehealthApplicantRequestEligibility | null>(null)
  const [requestEligibilityLoading, setRequestEligibilityLoading] = useState(false)
  const [requestEligibilityLoadAttempt, setRequestEligibilityLoadAttempt] = useState(0)
  const [requestEligibilityAcknowledgments, setRequestEligibilityAcknowledgments] = useState(initialRequestEligibilityAcknowledgments)
  const [requestPracticeNetwork, setRequestPracticeNetwork] = useState<TelehealthApplicantRequestPracticeNetwork | null>(null)
  const [requestPracticeNetworkLoading, setRequestPracticeNetworkLoading] = useState(false)
  const [requestPracticeNetworkLoadAttempt, setRequestPracticeNetworkLoadAttempt] = useState(0)
  const [requestPracticeNetworkAcknowledgments, setRequestPracticeNetworkAcknowledgments] = useState(initialRequestPracticeNetworkAcknowledgments)
  const [requestRenderingCandidate, setRequestRenderingCandidate] = useState<TelehealthApplicantRequestRenderingCandidate | null>(null)
  const [requestRenderingCandidateLoading, setRequestRenderingCandidateLoading] = useState(false)
  const [requestRenderingCandidateLoadAttempt, setRequestRenderingCandidateLoadAttempt] = useState(0)
  const [requestRenderingCandidateAcknowledgments, setRequestRenderingCandidateAcknowledgments] = useState(initialRequestRenderingCandidateAcknowledgments)
  const [loading, setLoading] = useState(Boolean(applicantSession))
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [resumeAttempt, setResumeAttempt] = useState(0)
  const pendingCreate = useRef<PendingCreate | null>(null)
  const pendingVerification = useRef<PendingVerification | null>(null)
  const pendingSafetyTriage = useRef<PendingSafetyTriage | null>(null)
  const pendingVisitPurpose = useRef<PendingVisitPurpose | null>(null)
  const pendingPracticeNetworkPrecheck = useRef<PendingPracticeNetworkPrecheck | null>(null)
  const pendingMemberInsuranceDetails = useRef<PendingMemberInsuranceDetails | null>(null)
  const pendingEligibility = useRef<PendingEligibility | null>(null)
  const pendingPracticeNetwork = useRef<PendingPracticeNetwork | null>(null)
  const pendingIdentityProofing = useRef<PendingIdentityProofing | null>(null)
  const pendingNoticeAcknowledgment = useRef<PendingNoticeAcknowledgment | null>(null)
  const pendingRegistrationDetailsConfirmation = useRef<PendingRegistrationDetailsConfirmation | null>(null)
  const pendingInsuranceHandoffConfirmation = useRef<PendingInsuranceHandoffConfirmation | null>(null)
  const pendingCommunicationAccessReadiness = useRef<PendingCommunicationAccessReadiness | null>(null)
  const pendingDevicePreparation = useRef<PendingDevicePreparation | null>(null)
  const pendingClinicalInformationInventory = useRef<PendingClinicalInformationInventory | null>(null)
  const pendingMedicationInformation = useRef<PendingMedicationInformation | null>(null)
  const pendingAllergyInformation = useRef<PendingAllergyInformation | null>(null)
  const pendingHealthHistoryInformation = useRef<PendingHealthHistoryInformation | null>(null)
  const pendingClinicalInformationSummary = useRef<PendingClinicalInformationSummary | null>(null)
  const pendingPreRequestReadiness = useRef<PendingPreRequestReadiness | null>(null)
  const pendingPracticeReview = useRef<PendingPracticeReview | null>(null)
  const pendingRequestCreation = useRef<PendingRequestCreation | null>(null)
  const pendingRequestLocation = useRef<PendingRequestLocation | null>(null)
  const pendingRequestSafety = useRef<PendingRequestSafety | null>(null)
  const pendingRequestComplaintTriage = useRef<PendingRequestComplaintTriage | null>(null)
  const pendingRequestIntake = useRef<PendingRequestIntake | null>(null)
  const pendingRequestInsuranceSource = useRef<PendingRequestInsuranceSource | null>(null)
  const pendingRequestEligibility = useRef<PendingRequestEligibility | null>(null)
  const pendingRequestPracticeNetwork = useRef<PendingRequestPracticeNetwork | null>(null)
  const pendingRequestRenderingCandidate = useRef<PendingRequestRenderingCandidate | null>(null)
  const errorRef = useRef<HTMLDivElement>(null)
  const safetyResultRef = useRef<HTMLDivElement>(null)
  const purposeResultRef = useRef<HTMLDivElement>(null)
  const networkResultRef = useRef<HTMLDivElement>(null)
  const memberDetailsResultRef = useRef<HTMLDivElement>(null)
  const eligibilityResultRef = useRef<HTMLDivElement>(null)
  const practiceNetworkResultRef = useRef<HTMLDivElement>(null)
  const identityProofingResultRef = useRef<HTMLDivElement>(null)
  const noticeResultRef = useRef<HTMLDivElement>(null)
  const registrationDetailsResultRef = useRef<HTMLDivElement>(null)
  const insuranceHandoffResultRef = useRef<HTMLDivElement>(null)
  const communicationAccessResultRef = useRef<HTMLDivElement>(null)
  const devicePreparationResultRef = useRef<HTMLDivElement>(null)
  const clinicalInformationInventoryResultRef = useRef<HTMLDivElement>(null)
  const medicationInformationResultRef = useRef<HTMLDivElement>(null)
  const allergyInformationResultRef = useRef<HTMLDivElement>(null)
  const healthHistoryInformationResultRef = useRef<HTMLDivElement>(null)
  const clinicalInformationSummaryResultRef = useRef<HTMLDivElement>(null)
  const preRequestReadinessResultRef = useRef<HTMLDivElement>(null)
  const practiceReviewResultRef = useRef<HTMLDivElement>(null)
  const requestCreationResultRef = useRef<HTMLDivElement>(null)
  const requestLocationResultRef = useRef<HTMLDivElement>(null)
  const requestSafetyResultRef = useRef<HTMLDivElement>(null)
  const requestComplaintTriageResultRef = useRef<HTMLDivElement>(null)
  const requestIntakeResultRef = useRef<HTMLDivElement>(null)
  const requestInsuranceSourceResultRef = useRef<HTMLDivElement>(null)
  const requestEligibilityResultRef = useRef<HTMLDivElement>(null)
  const requestPracticeNetworkResultRef = useRef<HTMLDivElement>(null)
  const requestRenderingCandidateResultRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (error) errorRef.current?.focus()
  }, [error])

  useEffect(() => {
    if (safetyResult) safetyResultRef.current?.focus()
  }, [safetyResult])

  useEffect(() => {
    if (purposeResult) purposeResultRef.current?.focus()
  }, [purposeResult])

  useEffect(() => {
    if (networkResult) networkResultRef.current?.focus()
  }, [networkResult])

  useEffect(() => {
    if (memberDetailsResult) memberDetailsResultRef.current?.focus()
  }, [memberDetailsResult])

  useEffect(() => {
    if (eligibilityResult) eligibilityResultRef.current?.focus()
  }, [eligibilityResult])

  useEffect(() => {
    if (practiceNetworkResult) practiceNetworkResultRef.current?.focus()
  }, [practiceNetworkResult])

  useEffect(() => {
    if (identityProofingResult) identityProofingResultRef.current?.focus()
  }, [identityProofingResult])

  useEffect(() => {
    if (notice?.acknowledged) noticeResultRef.current?.focus()
  }, [notice])

  useEffect(() => {
    if (registrationDetails?.confirmed) registrationDetailsResultRef.current?.focus()
  }, [registrationDetails])

  useEffect(() => {
    if (insuranceHandoff?.insuranceDetailsConfirmed) insuranceHandoffResultRef.current?.focus()
  }, [insuranceHandoff])

  useEffect(() => {
    if (communicationAccessReadiness?.readinessRecorded) communicationAccessResultRef.current?.focus()
  }, [communicationAccessReadiness])

  useEffect(() => {
    if (devicePreparation?.preparationRecorded) devicePreparationResultRef.current?.focus()
  }, [devicePreparation])

  useEffect(() => {
    if (clinicalInformationInventory?.inventoryRecorded) clinicalInformationInventoryResultRef.current?.focus()
  }, [clinicalInformationInventory])

  useEffect(() => {
    if (medicationInformation?.medicationInformationRecorded) medicationInformationResultRef.current?.focus()
  }, [medicationInformation])

  useEffect(() => {
    if (allergyInformation?.allergyInformationRecorded) allergyInformationResultRef.current?.focus()
  }, [allergyInformation])

  useEffect(() => {
    if (healthHistoryInformation?.healthHistoryInformationRecorded) healthHistoryInformationResultRef.current?.focus()
  }, [healthHistoryInformation])

  useEffect(() => {
    if (clinicalInformationSummary?.summaryConfirmed) clinicalInformationSummaryResultRef.current?.focus()
  }, [clinicalInformationSummary])

  useEffect(() => {
    if (preRequestReadiness?.readinessAcknowledged) preRequestReadinessResultRef.current?.focus()
  }, [preRequestReadiness])

  useEffect(() => {
    if (practiceReview?.submitted) practiceReviewResultRef.current?.focus()
  }, [practiceReview])

  useEffect(() => {
    if (requestCreation?.requestCreated) requestCreationResultRef.current?.focus()
  }, [requestCreation])

  useEffect(() => {
    if (requestLocation?.locationConfirmed) requestLocationResultRef.current?.focus()
  }, [requestLocation])

  useEffect(() => {
    if (requestSafety?.assessmentCreated) requestSafetyResultRef.current?.focus()
  }, [requestSafety])

  useEffect(() => {
    if (requestComplaintTriage?.assessmentCreated) requestComplaintTriageResultRef.current?.focus()
  }, [requestComplaintTriage])

  useEffect(() => {
    if (requestIntake?.snapshotCreated) requestIntakeResultRef.current?.focus()
  }, [requestIntake])

  useEffect(() => {
    if (requestInsuranceSource?.sourceConfirmed) requestInsuranceSourceResultRef.current?.focus()
  }, [requestInsuranceSource])

  useEffect(() => {
    if (requestEligibility?.verificationCompleted) requestEligibilityResultRef.current?.focus()
  }, [requestEligibility])

  useEffect(() => {
    if (requestPracticeNetwork?.verificationCompleted) requestPracticeNetworkResultRef.current?.focus()
  }, [requestPracticeNetwork])

  useEffect(() => {
    if (requestRenderingCandidate?.selectionCompleted) requestRenderingCandidateResultRef.current?.focus()
  }, [requestRenderingCandidate])

  useEffect(() => {
    if (!applicantSession) {
      setLoading(false)
      return
    }
    const controller = new AbortController()
    setLoading(true)
    setError(null)
    getProspectiveApplicant(applicantSession.applicantId, applicantSession.applicantAccessKey, controller.signal)
      .then(setApplicant)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'The synthetic applicant session could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [applicantSession, resumeAttempt])

  useEffect(() => {
    if (!applicantSession || applicant?.status !== 'VisitPurposeRecorded') return
    const controller = new AbortController()
    setNetworkOptionsLoading(true)
    setError(null)
    getProspectivePracticeNetworkOptions(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setNetworkOptions)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'The synthetic plan catalog could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setNetworkOptionsLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, networkOptionsLoadAttempt])

  useEffect(() => {
    if (!applicantSession
      || (applicant?.status !== 'SyntheticPatientPromoted'
        && applicant?.status !== 'SyntheticTelehealthNoticeAcknowledged')) return
    const controller = new AbortController()
    setNoticeLoading(true)
    setError(null)
    getApplicantTelehealthNotice(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setNotice)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'The state-specific telehealth notice could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setNoticeLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, noticeLoadAttempt])

  useEffect(() => {
    if (!applicantSession
      || (applicant?.status !== 'SyntheticTelehealthNoticeAcknowledged'
        && applicant?.status !== 'SyntheticMinimumRegistrationDetailsConfirmed')) return
    const controller = new AbortController()
    setRegistrationDetailsLoading(true)
    setError(null)
    getApplicantRegistrationDetails(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setRegistrationDetails)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'The minimum registration details could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setRegistrationDetailsLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, registrationDetailsLoadAttempt])

  useEffect(() => {
    if (!applicantSession
      || (applicant?.status !== 'SyntheticMinimumRegistrationDetailsConfirmed'
        && applicant?.status !== 'SyntheticInsuranceDetailsConfirmed')) return
    const controller = new AbortController()
    setInsuranceHandoffLoading(true)
    setError(null)
    getApplicantInsuranceHandoff(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setInsuranceHandoff)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'The synthetic insurance handoff could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setInsuranceHandoffLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, insuranceHandoffLoadAttempt])

  useEffect(() => {
    if (!applicantSession
      || (applicant?.status !== 'SyntheticInsuranceDetailsConfirmed'
        && applicant?.status !== 'SyntheticCommunicationAccessReadinessRecorded')) return
    const controller = new AbortController()
    setCommunicationAccessLoading(true)
    setError(null)
    getApplicantCommunicationAccessReadiness(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setCommunicationAccessReadiness)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'The synthetic communication and access readiness step could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setCommunicationAccessLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, communicationAccessLoadAttempt])

  useEffect(() => {
    if (!applicantSession
      || (applicant?.status !== 'SyntheticCommunicationAccessReadinessRecorded'
        && applicant?.status !== 'SyntheticDevicePreparationRecorded'
        && applicant?.status !== 'SyntheticClinicalInformationInventoryRecorded'
        && applicant?.status !== 'SyntheticMedicationInformationRecorded'
        && applicant?.status !== 'SyntheticAllergyInformationRecorded'
        && applicant?.status !== 'SyntheticHealthHistoryInformationRecorded')) return
    const controller = new AbortController()
    setDevicePreparationLoading(true)
    setError(null)
    getApplicantDevicePreparation(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setDevicePreparation)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'The synthetic device-preparation step could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setDevicePreparationLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, devicePreparationLoadAttempt])

  useEffect(() => {
    if (!applicantSession
      || (applicant?.status !== 'SyntheticDevicePreparationRecorded'
        && applicant?.status !== 'SyntheticClinicalInformationInventoryRecorded'
        && applicant?.status !== 'SyntheticMedicationInformationRecorded'
        && applicant?.status !== 'SyntheticAllergyInformationRecorded'
        && applicant?.status !== 'SyntheticHealthHistoryInformationRecorded')) return
    const controller = new AbortController()
    setClinicalInformationInventoryLoading(true)
    setError(null)
    getApplicantClinicalInformationInventory(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setClinicalInformationInventory)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'The synthetic clinical-information inventory could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setClinicalInformationInventoryLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, clinicalInformationInventoryLoadAttempt])

  useEffect(() => {
    if (!applicantSession
      || (applicant?.status !== 'SyntheticClinicalInformationInventoryRecorded'
        && applicant?.status !== 'SyntheticMedicationInformationRecorded'
        && applicant?.status !== 'SyntheticAllergyInformationRecorded'
        && applicant?.status !== 'SyntheticHealthHistoryInformationRecorded')) return
    const controller = new AbortController()
    setMedicationInformationLoading(true)
    setError(null)
    getApplicantMedicationInformation(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setMedicationInformation)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'The synthetic medication-information step could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setMedicationInformationLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, medicationInformationLoadAttempt])

  useEffect(() => {
    if (!applicantSession
      || (applicant?.status !== 'SyntheticMedicationInformationRecorded'
        && applicant?.status !== 'SyntheticAllergyInformationRecorded'
        && applicant?.status !== 'SyntheticHealthHistoryInformationRecorded')) return
    const controller = new AbortController()
    setAllergyInformationLoading(true)
    setError(null)
    getApplicantAllergyInformation(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setAllergyInformation)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'The synthetic allergy-information step could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setAllergyInformationLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, allergyInformationLoadAttempt])

  useEffect(() => {
    if (!applicantSession
      || (applicant?.status !== 'SyntheticAllergyInformationRecorded'
        && applicant?.status !== 'SyntheticHealthHistoryInformationRecorded'
        && applicant?.status !== 'SyntheticClinicalInformationSummaryConfirmed')) return
    const controller = new AbortController()
    setHealthHistoryInformationLoading(true)
    setError(null)
    getApplicantHealthHistoryInformation(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setHealthHistoryInformation)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'The synthetic health-history information step could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setHealthHistoryInformationLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, healthHistoryInformationLoadAttempt])

  useEffect(() => {
    if (!applicantSession
      || (applicant?.status !== 'SyntheticHealthHistoryInformationRecorded'
        && applicant?.status !== 'SyntheticClinicalInformationSummaryConfirmed')) return
    const controller = new AbortController()
    setClinicalInformationSummaryLoading(true)
    setError(null)
    getApplicantClinicalInformationSummary(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setClinicalInformationSummary)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'The synthetic clinical-information summary could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setClinicalInformationSummaryLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, clinicalInformationSummaryLoadAttempt])

  useEffect(() => {
    if (!applicantSession
      || (applicant?.status !== 'SyntheticClinicalInformationSummaryConfirmed'
        && applicant?.status !== 'SyntheticPreRequestReadinessAcknowledged')) return
    const controller = new AbortController()
    setPreRequestReadinessLoading(true)
    setError(null)
    getApplicantPreRequestReadiness(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setPreRequestReadiness)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'The synthetic pre-request readiness review could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setPreRequestReadinessLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, preRequestReadinessLoadAttempt])

  useEffect(() => {
    if (!applicantSession
      || (applicant?.status !== 'SyntheticPreRequestReadinessAcknowledged'
        && applicant?.status !== 'SyntheticPracticeReviewSubmitted')) return
    const controller = new AbortController()
    setPracticeReviewLoading(true)
    setError(null)
    getApplicantPracticeReviewSubmission(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setPracticeReview)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'The synthetic practice review submission could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setPracticeReviewLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, practiceReviewLoadAttempt])

  useEffect(() => {
    if (!applicantSession
      || (applicant?.status !== 'SyntheticPracticeReviewAuthorized'
        && applicant?.status !== 'SyntheticRequestCreated')) return
    const controller = new AbortController()
    setRequestCreationLoading(true)
    setError(null)
    getApplicantTelehealthRequest(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setRequestCreation)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'The synthetic request-creation state could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setRequestCreationLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, requestCreationLoadAttempt])

  useEffect(() => {
    if (!applicantSession || applicant?.status !== 'SyntheticRequestCreated') return
    const controller = new AbortController()
    setRequestLocationLoading(true)
    setError(null)
    getApplicantTelehealthRequestLocation(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setRequestLocation)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'The request location-confirmation step could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setRequestLocationLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, requestLocationLoadAttempt])

  useEffect(() => {
    if (!applicantSession
      || applicant?.status !== 'SyntheticRequestCreated'
      || !requestLocation?.locationConfirmed) return
    const controller = new AbortController()
    setRequestSafetyLoading(true)
    setError(null)
    getApplicantTelehealthRequestUniversalSafety(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setRequestSafety)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'The request universal safety screen could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setRequestSafetyLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, requestLocation?.locationConfirmed, requestSafetyLoadAttempt])

  useEffect(() => {
    if (!applicantSession
      || applicant?.status !== 'SyntheticRequestCreated'
      || !requestSafety?.complaintSpecificTriageRequired) return
    const controller = new AbortController()
    setRequestComplaintTriageLoading(true)
    setError(null)
    getApplicantTelehealthRequestComplaintTriage(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setRequestComplaintTriage)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'Complaint-specific triage could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setRequestComplaintTriageLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, requestSafety?.complaintSpecificTriageRequired, requestComplaintTriageLoadAttempt])

  useEffect(() => {
    if (!applicantSession
      || applicant?.status !== 'SyntheticRequestCreated'
      || !requestComplaintTriage?.assessmentCreated
      || !requestComplaintTriage.syntheticVideoEvaluationCandidate) return
    const controller = new AbortController()
    setRequestIntakeLoading(true)
    setError(null)
    getApplicantTelehealthRequestIntake(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setRequestIntake)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'The request intake confirmation could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setRequestIntakeLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, requestComplaintTriage?.assessmentCreated, requestComplaintTriage?.syntheticVideoEvaluationCandidate, requestIntakeLoadAttempt])

  useEffect(() => {
    if (!applicantSession
      || applicant?.status !== 'SyntheticRequestCreated'
      || !requestIntake?.snapshotCreated) return
    const controller = new AbortController()
    setRequestInsuranceSourceLoading(true)
    setError(null)
    getApplicantTelehealthRequestInsuranceSource(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setRequestInsuranceSource)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'The request insurance source could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setRequestInsuranceSourceLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, requestIntake?.snapshotCreated, requestInsuranceSourceLoadAttempt])

  useEffect(() => {
    if (!applicantSession
      || applicant?.status !== 'SyntheticRequestCreated'
      || !requestInsuranceSource?.sourceConfirmed) return
    const controller = new AbortController()
    setRequestEligibilityLoading(true)
    setError(null)
    getApplicantTelehealthRequestEligibility(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setRequestEligibility)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'Fresh request eligibility could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setRequestEligibilityLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, requestInsuranceSource?.sourceConfirmed, requestEligibilityLoadAttempt])

  useEffect(() => {
    if (!applicantSession
      || applicant?.status !== 'SyntheticRequestCreated'
      || !requestEligibility?.verificationCompleted
      || requestEligibility.businessOutcome !== 'EligibleBenefitsReported') return
    const controller = new AbortController()
    setRequestPracticeNetworkLoading(true)
    setError(null)
    getApplicantTelehealthRequestPracticeNetwork(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setRequestPracticeNetwork)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'Fresh practice-network verification could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setRequestPracticeNetworkLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, requestEligibility?.verificationCompleted, requestEligibility?.businessOutcome, requestPracticeNetworkLoadAttempt])

  useEffect(() => {
    if (!applicantSession
      || applicant?.status !== 'SyntheticRequestCreated'
      || !requestPracticeNetwork?.verificationCompleted
      || requestPracticeNetwork.businessOutcome !== 'PracticeInNetworkAcceptingNewPatients') return
    const controller = new AbortController()
    setRequestRenderingCandidateLoading(true)
    setError(null)
    getApplicantTelehealthRequestRenderingCandidate(
      applicant.applicantId,
      applicantSession.applicantAccessKey,
      controller.signal,
    )
      .then(setRequestRenderingCandidate)
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setError(caught instanceof Error ? caught.message : 'The rendering candidate could not be loaded.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setRequestRenderingCandidateLoading(false)
      })
    return () => controller.abort()
  }, [applicant?.applicantId, applicant?.status, applicant?.version, applicantSession, requestPracticeNetwork?.verificationCompleted, requestPracticeNetwork?.businessOutcome, requestRenderingCandidateLoadAttempt])

  function updateValue<Key extends keyof FormValues>(key: Key, value: FormValues[Key]) {
    pendingCreate.current = null
    setValues((current) => ({ ...current, [key]: value }))
  }

  async function createApplicant(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    const input = { ...values, syntheticDataConfirmed: true } satisfies TelehealthProspectiveApplicantInput
    const content = JSON.stringify(input)
    if (!pendingCreate.current || pendingCreate.current.content !== content) {
      pendingCreate.current = {
        content,
        applicantAccessKey: createApplicantAccessKey(),
        idempotencyKey: crypto.randomUUID(),
      }
    }
    setSubmitting(true)
    try {
      const pending = pendingCreate.current
      const result = await createProspectiveApplicant(input, pending.applicantAccessKey, pending.idempotencyKey)
      const session = { applicantId: result.applicantId, applicantAccessKey: pending.applicantAccessKey }
      saveApplicantSession(session)
      setApplicantSession(session)
      setApplicant(result)
      pendingCreate.current = null
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) pendingCreate.current = null
      setError(caught instanceof Error ? caught.message : 'The synthetic applicant could not be created.')
    } finally {
      setSubmitting(false)
    }
  }

  async function verifyContact(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession) return
    setError(null)
    const content = JSON.stringify({ applicantId: applicant.applicantId, expectedVersion: applicant.version, verificationCode })
    if (!pendingVerification.current || pendingVerification.current.content !== content) {
      pendingVerification.current = { content, idempotencyKey: crypto.randomUUID() }
    }
    setSubmitting(true)
    try {
      const result = await verifyProspectiveApplicantContact(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        applicant.version,
        verificationCode,
        pendingVerification.current.idempotencyKey,
      )
      setApplicant(result)
      setVerificationCode('')
      pendingVerification.current = null
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) pendingVerification.current = null
      setError(caught instanceof Error ? caught.message : 'The synthetic contact could not be verified.')
      if (caught instanceof ApiRequestError && caught.status === 410) {
        setApplicant((current) => current ? { ...current, status: 'VerificationLocked', verificationAttemptsRemaining: 0 } : current)
      }
    } finally {
      setSubmitting(false)
    }
  }

  function updateSafetyAnswer(key: keyof typeof initialSafetyAnswers, value: YesNoAnswer) {
    pendingSafetyTriage.current = null
    setSafetyAnswers((current) => ({ ...current, [key]: value }))
  }

  async function evaluateSafetyTriage(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !currentLocationStateCode) return
    if (!locationConfirmed || !safetySyntheticConfirmed) {
      setError('Confirm the current synthetic location and synthetic-data boundary before continuing.')
      return
    }
    if (Object.values(safetyAnswers).some((answer) => answer === '')) {
      setError('Answer every universal safety question explicitly. Missing answers cannot pass.')
      return
    }
    setError(null)
    const input = {
      expectedVersion: applicant.version,
      currentLocationStateCode,
      currentLocationConfirmed: true,
      hasEmergencyWarning: safetyAnswers.emergency === 'yes',
      severeOrWorsening: safetyAnswers.severe === 'yes',
      requiresHandsOnExam: safetyAnswers.handsOn === 'yes',
      unsure: safetyAnswers.unsure === 'yes',
      syntheticDataConfirmed: true,
    } satisfies TelehealthProspectiveSafetyTriageInput
    const content = JSON.stringify(input)
    if (!pendingSafetyTriage.current || pendingSafetyTriage.current.content !== content) {
      pendingSafetyTriage.current = { content, idempotencyKey: crypto.randomUUID() }
    }
    setSubmitting(true)
    try {
      const result = await evaluateProspectiveSafetyTriage(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingSafetyTriage.current.idempotencyKey,
      )
      setSafetyResult(result)
      setApplicant((current) => current ? {
        ...current,
        status: result.applicantStatus,
        version: result.applicantVersion,
        nextAction: result.direction,
      } : current)
      pendingSafetyTriage.current = null
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) pendingSafetyTriage.current = null
      setError(caught instanceof Error ? caught.message : 'The synthetic universal safety screen could not be recorded.')
    } finally {
      setSubmitting(false)
    }
  }

  async function recordVisitPurpose(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !purposeCategory) return
    if (!purposeSyntheticConfirmed) {
      setError('Confirm that the visit purpose is fictional synthetic demonstration data.')
      return
    }
    setError(null)
    const input = {
      expectedVersion: applicant.version,
      purposeCategory,
      syntheticDataConfirmed: true,
    } satisfies TelehealthProspectiveVisitPurposeInput
    const content = JSON.stringify(input)
    if (!pendingVisitPurpose.current || pendingVisitPurpose.current.content !== content) {
      pendingVisitPurpose.current = { content, idempotencyKey: crypto.randomUUID() }
    }
    setSubmitting(true)
    try {
      const result = await recordProspectiveVisitPurpose(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingVisitPurpose.current.idempotencyKey,
      )
      setPurposeResult(result)
      setApplicant((current) => current ? {
        ...current,
        status: result.applicantStatus,
        version: result.applicantVersion,
        nextAction: result.direction,
      } : current)
      pendingVisitPurpose.current = null
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) pendingVisitPurpose.current = null
      setError(caught instanceof Error ? caught.message : 'The synthetic visit purpose could not be recorded.')
    } finally {
      setSubmitting(false)
    }
  }

  async function recordPracticeNetworkPrecheck(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !planKey) return
    if (!networkSyntheticConfirmed) {
      setError('Confirm that the selected plan is fictional synthetic demonstration data.')
      return
    }
    setError(null)
    const input = {
      expectedVersion: applicant.version,
      planKey,
      syntheticDataConfirmed: true,
    } satisfies TelehealthProspectivePracticeNetworkPrecheckInput
    const content = JSON.stringify(input)
    if (!pendingPracticeNetworkPrecheck.current || pendingPracticeNetworkPrecheck.current.content !== content) {
      pendingPracticeNetworkPrecheck.current = { content, idempotencyKey: crypto.randomUUID() }
    }
    setSubmitting(true)
    try {
      const result = await recordProspectivePracticeNetworkPrecheck(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingPracticeNetworkPrecheck.current.idempotencyKey,
      )
      setNetworkResult(result)
      setApplicant((current) => current ? {
        ...current,
        status: result.applicantStatus,
        version: result.applicantVersion,
        nextAction: result.direction,
      } : current)
      pendingPracticeNetworkPrecheck.current = null
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) pendingPracticeNetworkPrecheck.current = null
      setError(caught instanceof Error ? caught.message : 'The synthetic practice-network precheck could not be recorded.')
    } finally {
      setSubmitting(false)
    }
  }

  function clearPendingMemberDetails() {
    pendingMemberInsuranceDetails.current = null
  }

  async function recordMemberInsuranceDetails(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !subscriberRelationship) return
    if (!memberDetailsConfirmed || !memberSyntheticConfirmed) {
      setError('Review the synthetic member details and confirm the synthetic-data boundary before continuing.')
      return
    }
    if (subscriberRelationship !== 'Self' && (!subscriberFirstName.trim() || !subscriberLastName.trim() || !subscriberDateOfBirth)) {
      setError('Enter the complete fictional non-self subscriber name and date of birth.')
      return
    }
    setError(null)
    const input = {
      expectedVersion: applicant.version,
      memberId,
      groupNumber: groupNumber.trim() || null,
      subscriberRelationship,
      subscriberFirstName: subscriberRelationship === 'Self' ? null : subscriberFirstName,
      subscriberLastName: subscriberRelationship === 'Self' ? null : subscriberLastName,
      subscriberDateOfBirth: subscriberRelationship === 'Self' ? null : subscriberDateOfBirth,
      detailsConfirmed: true,
      syntheticDataConfirmed: true,
    } satisfies TelehealthProspectiveMemberInsuranceDetailsInput
    const content = JSON.stringify(input)
    if (!pendingMemberInsuranceDetails.current || pendingMemberInsuranceDetails.current.content !== content) {
      pendingMemberInsuranceDetails.current = { content, idempotencyKey: crypto.randomUUID() }
    }
    setSubmitting(true)
    try {
      const result = await recordProspectiveMemberInsuranceDetails(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingMemberInsuranceDetails.current.idempotencyKey,
      )
      setMemberDetailsResult(result)
      setApplicant((current) => current ? {
        ...current,
        status: result.applicantStatus,
        version: result.applicantVersion,
        nextAction: result.direction,
      } : current)
      pendingMemberInsuranceDetails.current = null
      setMemberId('')
      setGroupNumber('')
      setSubscriberFirstName('')
      setSubscriberLastName('')
      setSubscriberDateOfBirth('')
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) pendingMemberInsuranceDetails.current = null
      setError(caught instanceof Error ? caught.message : 'The protected synthetic member-details receipt could not be recorded.')
    } finally {
      setSubmitting(false)
    }
  }

  async function recordEligibility(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession) return
    if (!eligibilitySyntheticConfirmed) {
      setError('Confirm the NON_PRODUCTION synthetic eligibility boundary before continuing.')
      return
    }
    setError(null)
    const input = {
      expectedVersion: applicant.version,
      syntheticDataConfirmed: true,
    } satisfies TelehealthProspectiveEligibilityInput
    const content = JSON.stringify(input)
    if (!pendingEligibility.current || pendingEligibility.current.content !== content) {
      pendingEligibility.current = { content, idempotencyKey: crypto.randomUUID() }
    }
    setSubmitting(true)
    try {
      const result = await recordProspectiveEligibility(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingEligibility.current.idempotencyKey,
      )
      setEligibilityResult(result)
      setApplicant((current) => current ? {
        ...current,
        status: result.applicantStatus,
        version: result.applicantVersion,
        nextAction: result.direction,
      } : current)
      pendingEligibility.current = null
      setEligibilitySyntheticConfirmed(false)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) pendingEligibility.current = null
      setError(caught instanceof Error ? caught.message : 'The synthetic eligibility result could not be recorded.')
    } finally {
      setSubmitting(false)
    }
  }

  async function recordPracticeNetwork(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession) return
    if (!practiceNetworkSyntheticConfirmed) {
      setError('Confirm the NON_PRODUCTION synthetic practice-network boundary before continuing.')
      return
    }
    setError(null)
    const input = {
      expectedVersion: applicant.version,
      syntheticDataConfirmed: true,
    } satisfies TelehealthProspectivePracticeNetworkInput
    const content = JSON.stringify(input)
    if (!pendingPracticeNetwork.current || pendingPracticeNetwork.current.content !== content) {
      pendingPracticeNetwork.current = { content, idempotencyKey: crypto.randomUUID() }
    }
    setSubmitting(true)
    try {
      const result = await recordProspectivePracticeNetwork(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingPracticeNetwork.current.idempotencyKey,
      )
      setPracticeNetworkResult(result)
      setApplicant((current) => current ? {
        ...current,
        status: result.applicantStatus,
        version: result.applicantVersion,
        nextAction: result.direction,
      } : current)
      pendingPracticeNetwork.current = null
      setPracticeNetworkSyntheticConfirmed(false)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) pendingPracticeNetwork.current = null
      setError(caught instanceof Error ? caught.message : 'The synthetic practice-network result could not be recorded.')
    } finally {
      setSubmitting(false)
    }
  }

  async function recordIdentityProofing(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession) return
    if (!identityPrivacyNoticeAcknowledged || !identitySyntheticConfirmed) {
      setError('Acknowledge both the synthetic privacy notice and the NON_PRODUCTION identity-proofing boundary before continuing.')
      return
    }
    setError(null)
    const input = {
      expectedVersion: applicant.version,
      privacyNoticeAcknowledged: true,
      syntheticDataConfirmed: true,
    } satisfies TelehealthProspectiveIdentityProofingInput
    const content = JSON.stringify(input)
    if (!pendingIdentityProofing.current || pendingIdentityProofing.current.content !== content) {
      pendingIdentityProofing.current = { content, idempotencyKey: crypto.randomUUID() }
    }
    setSubmitting(true)
    try {
      const result = await recordProspectiveIdentityProofing(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingIdentityProofing.current.idempotencyKey,
      )
      setIdentityProofingResult(result)
      setApplicant((current) => current ? {
        ...current,
        status: result.applicantStatus,
        version: result.applicantVersion,
        nextAction: result.direction,
      } : current)
      pendingIdentityProofing.current = null
      setIdentityPrivacyNoticeAcknowledged(false)
      setIdentitySyntheticConfirmed(false)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) pendingIdentityProofing.current = null
      setError(caught instanceof Error ? caught.message : 'The synthetic identity-proofing process fixture could not be recorded.')
    } finally {
      setSubmitting(false)
    }
  }

  function updateNoticeAcknowledgment(key: keyof typeof initialNoticeAcknowledgments, checked: boolean) {
    pendingNoticeAcknowledgment.current = null
    setNoticeAcknowledgments((current) => ({ ...current, [key]: checked }))
  }

  async function acknowledgeTelehealthNotice(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !notice) return
    const allAcknowledged = Object.values(noticeAcknowledgments).every(Boolean)
    if (!allAcknowledged) {
      setError('Acknowledge every state-specific notice item before continuing.')
      return
    }
    setError(null)
    const input = {
      expectedVersion: applicant.version,
      noticeKey: notice.noticeKey,
      noticeVersion: notice.noticeVersion,
      currentLocationStateCode: notice.currentLocationStateCode,
      currentLocationConfirmed: true,
      modeOfCareAcknowledged: true,
      privacyLimitationsAcknowledged: true,
      emergencyInstructionsAcknowledged: true,
      inPersonOptionAcknowledged: true,
      clinicianReconfirmationRequiredAcknowledged: true,
      syntheticDataConfirmed: true,
    } satisfies TelehealthApplicantNoticeInput
    const content = JSON.stringify(input)
    if (!pendingNoticeAcknowledgment.current || pendingNoticeAcknowledgment.current.content !== content) {
      pendingNoticeAcknowledgment.current = { content, idempotencyKey: crypto.randomUUID() }
    }
    setSubmitting(true)
    try {
      const result = await acknowledgeApplicantTelehealthNotice(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingNoticeAcknowledgment.current.idempotencyKey,
      )
      setNotice(result)
      setApplicant((current) => current ? {
        ...current,
        status: result.applicantStatus,
        version: result.applicantVersion,
        nextAction: result.direction,
      } : current)
      pendingNoticeAcknowledgment.current = null
      setNoticeAcknowledgments(initialNoticeAcknowledgments)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) pendingNoticeAcknowledgment.current = null
      setError(caught instanceof Error ? caught.message : 'The state-specific telehealth notice acknowledgment could not be recorded.')
    } finally {
      setSubmitting(false)
    }
  }

  function updateRegistrationDetailsConfirmation(
    key: keyof typeof initialRegistrationDetailsConfirmations,
    checked: boolean,
  ) {
    pendingRegistrationDetailsConfirmation.current = null
    setRegistrationDetailsConfirmations((current) => ({ ...current, [key]: checked }))
  }

  async function confirmRegistrationDetails(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !registrationDetails) return
    const allConfirmed = Object.values(registrationDetailsConfirmations).every(Boolean)
    if (!allConfirmed) {
      setError('Confirm every minimum registration-details statement before continuing.')
      return
    }
    setError(null)
    const input = {
      expectedVersion: applicant.version,
      detailsFingerprint: registrationDetails.detailsFingerprint,
      legalNameAndBirthDateConfirmed: true,
      contactChannelsConfirmed: true,
      residenceRegionConfirmed: true,
      noCorrectionsNeededConfirmed: true,
      syntheticDataConfirmed: true,
    } satisfies TelehealthApplicantRegistrationDetailsInput
    const content = JSON.stringify(input)
    if (!pendingRegistrationDetailsConfirmation.current
      || pendingRegistrationDetailsConfirmation.current.content !== content) {
      pendingRegistrationDetailsConfirmation.current = { content, idempotencyKey: crypto.randomUUID() }
    }
    setSubmitting(true)
    try {
      const result = await confirmApplicantRegistrationDetails(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingRegistrationDetailsConfirmation.current.idempotencyKey,
      )
      setRegistrationDetails(result)
      setApplicant((current) => current ? {
        ...current,
        status: result.applicantStatus,
        version: result.applicantVersion,
        nextAction: result.direction,
      } : current)
      pendingRegistrationDetailsConfirmation.current = null
      setRegistrationDetailsConfirmations(initialRegistrationDetailsConfirmations)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) {
        pendingRegistrationDetailsConfirmation.current = null
      }
      setError(caught instanceof Error
        ? caught.message
        : 'The minimum registration-details confirmation could not be recorded.')
    } finally {
      setSubmitting(false)
    }
  }

  function updateInsuranceHandoffConfirmation(
    key: keyof typeof initialInsuranceHandoffConfirmations,
    checked: boolean,
  ) {
    pendingInsuranceHandoffConfirmation.current = null
    setInsuranceHandoffConfirmations((current) => ({ ...current, [key]: checked }))
  }

  async function confirmInsuranceHandoff(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !insuranceHandoff) return
    const allConfirmed = Object.values(insuranceHandoffConfirmations).every(Boolean)
    if (!allConfirmed) {
      setError('Confirm every synthetic insurance handoff statement before continuing.')
      return
    }
    setError(null)
    const input = {
      expectedVersion: applicant.version,
      insuranceSnapshotFingerprint: insuranceHandoff.insuranceSnapshotFingerprint,
      payerAndProductConfirmed: true,
      maskedMemberDetailsConfirmed: true,
      subscriberRelationshipConfirmed: true,
      evidenceLimitationsAcknowledged: true,
      syntheticDataConfirmed: true,
    } satisfies TelehealthApplicantInsuranceHandoffInput
    const content = JSON.stringify(input)
    if (!pendingInsuranceHandoffConfirmation.current
      || pendingInsuranceHandoffConfirmation.current.content !== content) {
      pendingInsuranceHandoffConfirmation.current = { content, idempotencyKey: crypto.randomUUID() }
    }
    setSubmitting(true)
    try {
      const result = await confirmApplicantInsuranceHandoff(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingInsuranceHandoffConfirmation.current.idempotencyKey,
      )
      setInsuranceHandoff(result)
      setApplicant((current) => current ? {
        ...current,
        status: result.applicantStatus,
        version: result.applicantVersion,
        nextAction: result.direction,
      } : current)
      pendingInsuranceHandoffConfirmation.current = null
      setInsuranceHandoffConfirmations(initialInsuranceHandoffConfirmations)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) {
        pendingInsuranceHandoffConfirmation.current = null
      }
      setError(caught instanceof Error
        ? caught.message
        : 'The synthetic insurance handoff confirmation could not be recorded.')
    } finally {
      setSubmitting(false)
    }
  }

  function resetPendingCommunicationAccessReadiness() {
    pendingCommunicationAccessReadiness.current = null
  }

  function updateCommunicationAccessConfirmation(
    key: keyof typeof initialCommunicationAccessConfirmations,
    checked: boolean,
  ) {
    resetPendingCommunicationAccessReadiness()
    setCommunicationAccessConfirmations((current) => ({ ...current, [key]: checked }))
  }

  async function recordCommunicationAccessReadiness(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !communicationAccessReadiness) return
    if (!preferredSpokenLanguage) {
      setError('Select a supported spoken language before continuing.')
      return
    }
    if (!Object.values(communicationAccessConfirmations).every(Boolean)) {
      setError('Confirm every communication and access readiness statement before continuing.')
      return
    }
    setError(null)
    const input = {
      expectedVersion: applicant.version,
      contextSnapshotFingerprint: communicationAccessReadiness.contextSnapshotFingerprint,
      preferredSpokenLanguage,
      interpreterRequested,
      accessibilitySupportRequested,
      currentLocationConfirmed: true,
      callbackNumberConfirmed: true,
      safeAndPrivateToCommunicateConfirmed: true,
      disconnectionAndEmergencyPlanAcknowledged: true,
      syntheticDataConfirmed: true,
    } satisfies TelehealthApplicantCommunicationAccessReadinessInput
    const content = JSON.stringify(input)
    if (!pendingCommunicationAccessReadiness.current
      || pendingCommunicationAccessReadiness.current.content !== content) {
      pendingCommunicationAccessReadiness.current = { content, idempotencyKey: crypto.randomUUID() }
    }
    setSubmitting(true)
    try {
      const result = await recordApplicantCommunicationAccessReadiness(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingCommunicationAccessReadiness.current.idempotencyKey,
      )
      setCommunicationAccessReadiness(result)
      setApplicant((current) => current ? {
        ...current,
        status: result.applicantStatus,
        version: result.applicantVersion,
        nextAction: result.direction,
      } : current)
      pendingCommunicationAccessReadiness.current = null
      setCommunicationAccessConfirmations(initialCommunicationAccessConfirmations)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) {
        pendingCommunicationAccessReadiness.current = null
      }
      setError(caught instanceof Error
        ? caught.message
        : 'The synthetic communication and access readiness receipt could not be recorded.')
    } finally {
      setSubmitting(false)
    }
  }

  function resetPendingDevicePreparation() {
    pendingDevicePreparation.current = null
  }

  function updateDevicePreparationAcknowledgment(
    key: keyof typeof initialDevicePreparationAcknowledgments,
    checked: boolean,
  ) {
    resetPendingDevicePreparation()
    setDevicePreparationAcknowledgments((current) => ({ ...current, [key]: checked }))
  }

  async function runDeviceCheck() {
    resetPendingDevicePreparation()
    setDevicePreparationAcknowledgments(initialDevicePreparationAcknowledgments)
    setDeviceCheckResult(null)
    setError(null)
    setDeviceCheckRunning(true)
    try {
      setDeviceCheckResult(await runTelehealthDevicePreflight())
    } finally {
      setDeviceCheckRunning(false)
    }
  }

  async function recordDevicePreparation(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !devicePreparation) return
    if (!deviceCheckResult || deviceCheckResult.status !== 'passed') {
      setError('Run and pass the local device check before continuing.')
      return
    }
    if (deviceCheckResult.evidence.networkQuality === 'limited') {
      setError('This connection is limited. Try a stronger connection and run the local device check again.')
      return
    }
    if (!Object.values(devicePreparationAcknowledgments).every(Boolean)) {
      setError('Confirm every device-preparation statement before continuing.')
      return
    }
    setError(null)
    const input = {
      expectedVersion: applicant.version,
      preparationSnapshotFingerprint: devicePreparation.preparationSnapshotFingerprint,
      browserSupported: true,
      cameraAvailable: true,
      microphoneAvailable: true,
      speakerAvailable: true,
      networkQuality: deviceCheckResult.evidence.networkQuality === 'good' ? 'Good' : 'Unknown',
      clientReportedResultAcknowledged: true,
      noReadinessGuaranteeAcknowledged: true,
      recheckBeforeConsultationAcknowledged: true,
    } satisfies TelehealthApplicantDevicePreparationInput
    const content = JSON.stringify(input)
    if (!pendingDevicePreparation.current || pendingDevicePreparation.current.content !== content) {
      pendingDevicePreparation.current = { content, idempotencyKey: crypto.randomUUID() }
    }
    setSubmitting(true)
    try {
      const result = await recordApplicantDevicePreparation(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingDevicePreparation.current.idempotencyKey,
      )
      setDevicePreparation(result)
      setApplicant((current) => current ? {
        ...current,
        status: result.applicantStatus,
        version: result.applicantVersion,
        nextAction: result.direction,
      } : current)
      pendingDevicePreparation.current = null
      setDeviceCheckResult(null)
      setDevicePreparationAcknowledgments(initialDevicePreparationAcknowledgments)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) {
        pendingDevicePreparation.current = null
      }
      setError(caught instanceof Error
        ? caught.message
        : 'The synthetic device-preparation receipt could not be recorded.')
    } finally {
      setSubmitting(false)
    }
  }

  function resetPendingClinicalInformationInventory() {
    pendingClinicalInformationInventory.current = null
  }

  function updateClinicalInformationInventoryStatus(
    category: 'medications' | 'allergies' | 'history',
    status: TelehealthApplicantClinicalInformationCategoryStatus,
  ) {
    resetPendingClinicalInformationInventory()
    if (category === 'medications') setMedicationsInventoryStatus(status)
    if (category === 'allergies') setAllergiesInventoryStatus(status)
    if (category === 'history') setOtherHealthHistoryInventoryStatus(status)
  }

  function updateClinicalInformationInventoryAcknowledgment(
    key: keyof typeof initialClinicalInformationInventoryAcknowledgments,
    checked: boolean,
  ) {
    resetPendingClinicalInformationInventory()
    setClinicalInformationInventoryAcknowledgments((current) => ({ ...current, [key]: checked }))
  }

  async function recordClinicalInformationInventory(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !clinicalInformationInventory) return
    if (!medicationsInventoryStatus || !allergiesInventoryStatus || !otherHealthHistoryInventoryStatus) {
      setError('Choose one patient-reported status for every clinical-information category.')
      return
    }
    if (!Object.values(clinicalInformationInventoryAcknowledgments).every(Boolean)) {
      setError('Confirm every clinical-information inventory limitation before continuing.')
      return
    }

    setError(null)
    const input = {
      expectedVersion: applicant.version,
      inventorySnapshotFingerprint: clinicalInformationInventory.inventorySnapshotFingerprint,
      medicationsStatus: medicationsInventoryStatus,
      allergiesOrIntolerancesStatus: allergiesInventoryStatus,
      otherHealthHistoryStatus: otherHealthHistoryInventoryStatus,
      patientReportedMayBeIncompleteAcknowledged: true,
      noClinicalDetailsCapturedAcknowledged: true,
      clinicianReconciliationRequiredAcknowledged: true,
    } satisfies TelehealthApplicantClinicalInformationInventoryInput
    const content = JSON.stringify(input)
    if (!pendingClinicalInformationInventory.current
      || pendingClinicalInformationInventory.current.content !== content) {
      pendingClinicalInformationInventory.current = {
        content,
        idempotencyKey: crypto.randomUUID(),
      }
    }

    setSubmitting(true)
    try {
      const result = await recordApplicantClinicalInformationInventory(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingClinicalInformationInventory.current.idempotencyKey,
      )
      setClinicalInformationInventory(result)
      setApplicant((current) => current ? {
        ...current,
        status: result.applicantStatus,
        version: result.applicantVersion,
        nextAction: result.direction,
      } : current)
      pendingClinicalInformationInventory.current = null
      setMedicationsInventoryStatus('')
      setAllergiesInventoryStatus('')
      setOtherHealthHistoryInventoryStatus('')
      setClinicalInformationInventoryAcknowledgments(initialClinicalInformationInventoryAcknowledgments)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) {
        pendingClinicalInformationInventory.current = null
      }
      setError(caught instanceof Error
        ? caught.message
        : 'The synthetic clinical-information inventory could not be recorded.')
    } finally {
      setSubmitting(false)
    }
  }

  function updateReportedMedicationUseStatus(
    catalogKey: string,
    status: '' | TelehealthApplicantReportedMedicationUseStatus,
  ) {
    pendingMedicationInformation.current = null
    setReportedMedicationUseStatuses((current) => ({ ...current, [catalogKey]: status }))
  }

  function updateMedicationInformationAcknowledgment(
    key: keyof typeof initialMedicationInformationAcknowledgments,
    checked: boolean,
  ) {
    pendingMedicationInformation.current = null
    setMedicationInformationAcknowledgments((current) => ({ ...current, [key]: checked }))
  }

  async function recordMedicationInformation(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !medicationInformation) return
    const medicationItems = medicationInformation.catalogItems
      .filter((item) => reportedMedicationUseStatuses[item.catalogKey])
      .map((item) => ({
        catalogKey: item.catalogKey,
        reportedUseStatus: reportedMedicationUseStatuses[item.catalogKey] as TelehealthApplicantReportedMedicationUseStatus,
      }))
    if (medicationInformation.inventoryMedicationsStatus === 'ItemsToReview'
      && medicationItems.length === 0
      && !additionalOrUnlistedMedicationReported) {
      setError('Select at least one synthetic catalog ingredient or indicate that additional or unlisted items exist.')
      return
    }
    if (!Object.values(medicationInformationAcknowledgments).every(Boolean)) {
      setError('Confirm every medication-information limitation before continuing.')
      return
    }

    setError(null)
    const input = {
      expectedVersion: applicant.version,
      medicationInformationSnapshotFingerprint: medicationInformation.medicationInformationSnapshotFingerprint,
      medicationItems,
      additionalOrUnlistedItemsReported: additionalOrUnlistedMedicationReported,
      patientReportedMayBeIncompleteAcknowledged: true,
      syntheticCatalogIncompleteAcknowledged: true,
      noDoseOrDirectionsCapturedAcknowledged: true,
      clinicianReconciliationRequiredAcknowledged: true,
    } satisfies TelehealthApplicantMedicationInformationInput
    const content = JSON.stringify(input)
    if (!pendingMedicationInformation.current
      || pendingMedicationInformation.current.content !== content) {
      pendingMedicationInformation.current = {
        content,
        idempotencyKey: crypto.randomUUID(),
      }
    }

    setSubmitting(true)
    try {
      const result = await recordApplicantMedicationInformation(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingMedicationInformation.current.idempotencyKey,
      )
      setMedicationInformation(result)
      setApplicant((current) => current ? {
        ...current,
        status: result.applicantStatus,
        version: result.applicantVersion,
        nextAction: result.direction,
      } : current)
      pendingMedicationInformation.current = null
      setReportedMedicationUseStatuses({})
      setAdditionalOrUnlistedMedicationReported(false)
      setMedicationInformationAcknowledgments(initialMedicationInformationAcknowledgments)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) {
        pendingMedicationInformation.current = null
      }
      setError(caught instanceof Error
        ? caught.message
        : 'The synthetic medication-information receipt could not be recorded.')
    } finally {
      setSubmitting(false)
    }
  }

  function updateReportedAllergyItem(catalogKey: string, checked: boolean) {
    pendingAllergyInformation.current = null
    setReportedAllergyItems((current) => ({ ...current, [catalogKey]: checked }))
  }

  function updateAllergyInformationAcknowledgment(
    key: keyof typeof initialAllergyInformationAcknowledgments,
    checked: boolean,
  ) {
    pendingAllergyInformation.current = null
    setAllergyInformationAcknowledgments((current) => ({ ...current, [key]: checked }))
  }

  async function recordAllergyInformation(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !allergyInformation) return
    const allergyItems = allergyInformation.catalogItems
      .filter((item) => reportedAllergyItems[item.catalogKey])
      .map((item) => ({ catalogKey: item.catalogKey }))
    if (allergyInformation.inventoryAllergiesOrIntolerancesStatus === 'ItemsToReview'
      && allergyItems.length === 0
      && !additionalOrUnlistedAllergyReported) {
      setError('Select at least one synthetic catalog substance or indicate that additional or unlisted substances exist.')
      return
    }
    if (!Object.values(allergyInformationAcknowledgments).every(Boolean)) {
      setError('Confirm every allergy-information limitation before continuing.')
      return
    }

    setError(null)
    const input = {
      expectedVersion: applicant.version,
      allergyInformationSnapshotFingerprint: allergyInformation.allergyInformationSnapshotFingerprint,
      allergyItems,
      additionalOrUnlistedItemsReported: additionalOrUnlistedAllergyReported,
      patientReportedMayBeIncompleteAcknowledged: true,
      syntheticCatalogIncompleteAcknowledged: true,
      noReactionOrCriticalityCapturedAcknowledged: true,
      clinicianVerificationRequiredAcknowledged: true,
    } satisfies TelehealthApplicantAllergyInformationInput
    const content = JSON.stringify(input)
    if (!pendingAllergyInformation.current
      || pendingAllergyInformation.current.content !== content) {
      pendingAllergyInformation.current = {
        content,
        idempotencyKey: crypto.randomUUID(),
      }
    }

    setSubmitting(true)
    try {
      const result = await recordApplicantAllergyInformation(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingAllergyInformation.current.idempotencyKey,
      )
      setAllergyInformation(result)
      setApplicant((current) => current ? {
        ...current,
        status: result.applicantStatus,
        version: result.applicantVersion,
        nextAction: result.direction,
      } : current)
      pendingAllergyInformation.current = null
      setReportedAllergyItems({})
      setAdditionalOrUnlistedAllergyReported(false)
      setAllergyInformationAcknowledgments(initialAllergyInformationAcknowledgments)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) {
        pendingAllergyInformation.current = null
      }
      setError(caught instanceof Error
        ? caught.message
        : 'The synthetic allergy-information receipt could not be recorded.')
    } finally {
      setSubmitting(false)
    }
  }

  function updateReportedHealthHistoryTopic(catalogKey: string, checked: boolean) {
    pendingHealthHistoryInformation.current = null
    setReportedHealthHistoryTopics((current) => ({ ...current, [catalogKey]: checked }))
  }

  function updateHealthHistoryInformationAcknowledgment(
    key: keyof typeof initialHealthHistoryInformationAcknowledgments,
    checked: boolean,
  ) {
    pendingHealthHistoryInformation.current = null
    setHealthHistoryInformationAcknowledgments((current) => ({ ...current, [key]: checked }))
  }

  async function recordHealthHistoryInformation(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !healthHistoryInformation) return
    const healthHistoryTopics = healthHistoryInformation.catalogTopics
      .filter((topic) => reportedHealthHistoryTopics[topic.catalogKey])
      .map((topic) => ({ catalogKey: topic.catalogKey }))
    if (healthHistoryInformation.inventoryOtherHealthHistoryStatus === 'ItemsToReview'
      && healthHistoryTopics.length === 0
      && !additionalOrUnlistedHealthHistoryReported) {
      setError('Select at least one synthetic review topic or indicate that additional or unlisted topics exist.')
      return
    }
    if (!Object.values(healthHistoryInformationAcknowledgments).every(Boolean)) {
      setError('Confirm every health-history information limitation before continuing.')
      return
    }

    setError(null)
    const input = {
      expectedVersion: applicant.version,
      healthHistoryInformationSnapshotFingerprint: healthHistoryInformation.healthHistoryInformationSnapshotFingerprint,
      healthHistoryTopics,
      additionalOrUnlistedTopicsReported: additionalOrUnlistedHealthHistoryReported,
      patientReportedMayBeIncompleteAcknowledged: true,
      topicSelectionIsNotDiagnosisAcknowledged: true,
      noStatusOrTimingCapturedAcknowledged: true,
      clinicianVerificationRequiredAcknowledged: true,
    } satisfies TelehealthApplicantHealthHistoryInformationInput
    const content = JSON.stringify(input)
    if (!pendingHealthHistoryInformation.current
      || pendingHealthHistoryInformation.current.content !== content) {
      pendingHealthHistoryInformation.current = {
        content,
        idempotencyKey: crypto.randomUUID(),
      }
    }

    setSubmitting(true)
    try {
      const result = await recordApplicantHealthHistoryInformation(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingHealthHistoryInformation.current.idempotencyKey,
      )
      setHealthHistoryInformation(result)
      setApplicant((current) => current ? {
        ...current,
        status: result.applicantStatus,
        version: result.applicantVersion,
        nextAction: result.direction,
      } : current)
      pendingHealthHistoryInformation.current = null
      setReportedHealthHistoryTopics({})
      setAdditionalOrUnlistedHealthHistoryReported(false)
      setHealthHistoryInformationAcknowledgments(initialHealthHistoryInformationAcknowledgments)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) {
        pendingHealthHistoryInformation.current = null
      }
      setError(caught instanceof Error
        ? caught.message
        : 'The synthetic health-history information receipt could not be recorded.')
    } finally {
      setSubmitting(false)
    }
  }

  function updateClinicalInformationSummaryAcknowledgment(
    key: keyof typeof initialClinicalInformationSummaryAcknowledgments,
    checked: boolean,
  ) {
    pendingClinicalInformationSummary.current = null
    setClinicalInformationSummaryAcknowledgments((current) => ({ ...current, [key]: checked }))
  }

  async function confirmClinicalInformationSummary(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !clinicalInformationSummary) return
    if (!Object.values(clinicalInformationSummaryAcknowledgments).every(Boolean)) {
      setError('Confirm every clinical-information summary limitation before continuing.')
      return
    }

    setError(null)
    const input = {
      expectedVersion: applicant.version,
      clinicalInformationSummarySnapshotFingerprint: clinicalInformationSummary.clinicalInformationSummarySnapshotFingerprint,
      patientReportedMayBeIncompleteAcknowledged: true,
      notClinicallyVerifiedOrReconciledAcknowledged: true,
      noIntakeCompletionOrEligibilityAcknowledged: true,
      correctionRequiresSeparateWorkflowAcknowledged: true,
    } satisfies TelehealthApplicantClinicalInformationSummaryInput
    const content = JSON.stringify(input)
    if (!pendingClinicalInformationSummary.current
      || pendingClinicalInformationSummary.current.content !== content) {
      pendingClinicalInformationSummary.current = {
        content,
        idempotencyKey: crypto.randomUUID(),
      }
    }

    setSubmitting(true)
    try {
      const result = await confirmApplicantClinicalInformationSummary(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingClinicalInformationSummary.current.idempotencyKey,
      )
      setClinicalInformationSummary(result)
      setApplicant((current) => current ? {
        ...current,
        status: result.applicantStatus,
        version: result.applicantVersion,
        nextAction: result.direction,
      } : current)
      pendingClinicalInformationSummary.current = null
      setClinicalInformationSummaryAcknowledgments(initialClinicalInformationSummaryAcknowledgments)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) {
        pendingClinicalInformationSummary.current = null
      }
      setError(caught instanceof Error
        ? caught.message
        : 'The synthetic clinical-information summary could not be confirmed.')
    } finally {
      setSubmitting(false)
    }
  }

  function updatePreRequestReadinessAcknowledgment(
    key: keyof typeof initialPreRequestReadinessAcknowledgments,
    checked: boolean,
  ) {
    pendingPreRequestReadiness.current = null
    setPreRequestReadinessAcknowledgments((current) => ({ ...current, [key]: checked }))
  }

  async function acknowledgePreRequestReadiness(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !preRequestReadiness) return
    if (!Object.values(preRequestReadinessAcknowledgments).every(Boolean)) {
      setError('Confirm every pre-request readiness limitation before continuing.')
      return
    }

    setError(null)
    const input = {
      expectedVersion: applicant.version,
      preRequestReadinessSnapshotFingerprint: preRequestReadiness.preRequestReadinessSnapshotFingerprint,
      priorSectionsReviewedAcknowledged: true,
      outstandingStepsRemainAcknowledged: true,
      noRequestOrQueueCreatedAcknowledged: true,
      correctionRequiresSeparateWorkflowAcknowledged: true,
    } satisfies TelehealthApplicantPreRequestReadinessInput
    const content = JSON.stringify(input)
    if (!pendingPreRequestReadiness.current
      || pendingPreRequestReadiness.current.content !== content) {
      pendingPreRequestReadiness.current = {
        content,
        idempotencyKey: crypto.randomUUID(),
      }
    }

    setSubmitting(true)
    try {
      const result = await acknowledgeApplicantPreRequestReadiness(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingPreRequestReadiness.current.idempotencyKey,
      )
      setPreRequestReadiness(result)
      setApplicant((current) => current ? {
        ...current,
        status: result.applicantStatus,
        version: result.applicantVersion,
        nextAction: result.direction,
      } : current)
      pendingPreRequestReadiness.current = null
      setPreRequestReadinessAcknowledgments(initialPreRequestReadinessAcknowledgments)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) {
        pendingPreRequestReadiness.current = null
      }
      setError(caught instanceof Error
        ? caught.message
        : 'The synthetic pre-request readiness review could not be acknowledged.')
    } finally {
      setSubmitting(false)
    }
  }

  function updatePracticeReviewAcknowledgment(
    key: keyof typeof initialPracticeReviewAcknowledgments,
    checked: boolean,
  ) {
    pendingPracticeReview.current = null
    setPracticeReviewAcknowledgments((current) => ({ ...current, [key]: checked }))
  }

  async function submitPracticeReview(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !practiceReview) return
    if (!Object.values(practiceReviewAcknowledgments).every(Boolean)) {
      setError('Confirm every practice review submission limitation before continuing.')
      return
    }

    setError(null)
    const input = {
      expectedVersion: applicant.version,
      practiceReviewSnapshotFingerprint: practiceReview.practiceReviewSnapshotFingerprint,
      patientReportedInformationAcknowledged: true,
      practiceMayRequestInformationOrDeclineAcknowledged: true,
      noTelehealthRequestOrCareQueueAcknowledged: true,
      worseningSymptomsRequireImmediateActionAcknowledged: true,
    } satisfies TelehealthApplicantPracticeReviewInput
    const content = JSON.stringify(input)
    if (!pendingPracticeReview.current || pendingPracticeReview.current.content !== content) {
      pendingPracticeReview.current = { content, idempotencyKey: crypto.randomUUID() }
    }

    setSubmitting(true)
    try {
      const result = await submitApplicantPracticeReview(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingPracticeReview.current.idempotencyKey,
      )
      setPracticeReview(result)
      setApplicant((current) => current ? {
        ...current,
        status: result.applicantStatus,
        version: result.applicantVersion,
        nextAction: result.direction,
      } : current)
      pendingPracticeReview.current = null
      setPracticeReviewAcknowledgments(initialPracticeReviewAcknowledgments)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) {
        pendingPracticeReview.current = null
      }
      setError(caught instanceof Error
        ? caught.message
        : 'The synthetic practice review submission could not be completed.')
    } finally {
      setSubmitting(false)
    }
  }

  function updateRequestCreationAcknowledgment(
    key: keyof typeof initialRequestCreationAcknowledgments,
    checked: boolean,
  ) {
    pendingRequestCreation.current = null
    setRequestCreationAcknowledgments((current) => ({ ...current, [key]: checked }))
  }

  async function createAuthorizedRequest(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !requestCreation) return
    if (!Object.values(requestCreationAcknowledgments).every(Boolean)) {
      setError('Confirm every request-creation and safety boundary before continuing.')
      return
    }

    setError(null)
    const input = {
      expectedApplicantVersion: applicant.version,
      authorizationPolicyVersion: requestCreation.authorizationPolicyVersion,
      requestCreationConfirmed: true,
      noQueueOrCareAcknowledged: true,
      urgentOrWorseningSymptomsRequireImmediateActionAcknowledged: true,
    } satisfies TelehealthApplicantRequestCreationInput
    const content = JSON.stringify(input)
    if (!pendingRequestCreation.current || pendingRequestCreation.current.content !== content) {
      pendingRequestCreation.current = { content, idempotencyKey: crypto.randomUUID() }
    }

    setSubmitting(true)
    try {
      const result = await createApplicantTelehealthRequest(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingRequestCreation.current.idempotencyKey,
      )
      setRequestCreation(result)
      setApplicant((current) => current ? {
        ...current,
        status: result.applicantStatus,
        version: result.applicantVersion,
        nextAction: result.direction,
      } : current)
      pendingRequestCreation.current = null
      setRequestCreationAcknowledgments(initialRequestCreationAcknowledgments)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) {
        pendingRequestCreation.current = null
      }
      setError(caught instanceof Error
        ? caught.message
        : 'The synthetic telehealth request could not be created.')
    } finally {
      setSubmitting(false)
    }
  }

  function updateRequestLocationAcknowledgment(
    key: keyof typeof initialRequestLocationAcknowledgments,
    checked: boolean,
  ) {
    pendingRequestLocation.current = null
    setRequestLocationAcknowledgments((current) => ({ ...current, [key]: checked }))
  }

  async function confirmRequestLocation(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !requestLocation || !requestLocationStateCode) return
    if (!Object.values(requestLocationAcknowledgments).every(Boolean)) {
      setError('Confirm every location, callback, restart, and safety boundary before continuing.')
      return
    }

    setError(null)
    const input = {
      expectedRequestVersion: requestLocation.requestVersion,
      contextSnapshotFingerprint: requestLocation.contextSnapshotFingerprint,
      currentLocationStateCode: requestLocationStateCode,
      currentLocationConfirmed: true,
      callbackNumberConfirmed: true,
      changedLocationRequiresRestartAcknowledged: true,
      urgentOrWorseningSymptomsRequireImmediateActionAcknowledged: true,
    } satisfies TelehealthApplicantRequestLocationInput
    const content = JSON.stringify(input)
    if (!pendingRequestLocation.current || pendingRequestLocation.current.content !== content) {
      pendingRequestLocation.current = { content, idempotencyKey: crypto.randomUUID() }
    }

    setSubmitting(true)
    try {
      const result = await confirmApplicantTelehealthRequestLocation(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingRequestLocation.current.idempotencyKey,
      )
      setRequestLocation(result)
      pendingRequestLocation.current = null
      setRequestLocationAcknowledgments(initialRequestLocationAcknowledgments)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) {
        pendingRequestLocation.current = null
      }
      setError(caught instanceof Error
        ? caught.message
        : 'The request location and callback could not be confirmed.')
    } finally {
      setSubmitting(false)
    }
  }

  function updateRequestSafetyAnswer(
    key: keyof typeof initialRequestSafetyAnswers,
    answer: YesNoAnswer,
  ) {
    pendingRequestSafety.current = null
    setRequestSafetyAnswers((current) => ({ ...current, [key]: answer }))
  }

  function updateRequestSafetyConfirmation(
    key: keyof typeof initialRequestSafetyConfirmations,
    checked: boolean,
  ) {
    pendingRequestSafety.current = null
    setRequestSafetyConfirmations((current) => ({ ...current, [key]: checked }))
  }

  async function assessRequestSafety(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !requestSafety) return
    if (Object.values(requestSafetyAnswers).some((answer) => answer === '')) {
      setError('Answer every universal safety question before continuing.')
      return
    }
    if (!Object.values(requestSafetyConfirmations).every(Boolean)) {
      setError('Confirm the location, callback route, and synthetic-data boundary before continuing.')
      return
    }

    setError(null)
    const input = {
      expectedRequestVersion: requestSafety.requestVersion,
      contextSnapshotFingerprint: requestSafety.contextSnapshotFingerprint,
      currentLocationStateCode: requestSafety.currentLocationStateCode,
      currentLocationConfirmed: true,
      callbackNumberConfirmed: true,
      syntheticDataConfirmed: true,
      hasEmergencyWarning: requestSafetyAnswers.emergency === 'yes',
      severeOrWorsening: requestSafetyAnswers.severe === 'yes',
      requiresHandsOnExam: requestSafetyAnswers.handsOn === 'yes',
      unsure: requestSafetyAnswers.unsure === 'yes',
    } satisfies TelehealthApplicantRequestUniversalSafetyInput
    const content = JSON.stringify(input)
    if (!pendingRequestSafety.current || pendingRequestSafety.current.content !== content) {
      pendingRequestSafety.current = { content, idempotencyKey: crypto.randomUUID() }
    }

    setSubmitting(true)
    try {
      const result = await assessApplicantTelehealthRequestUniversalSafety(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingRequestSafety.current.idempotencyKey,
      )
      setRequestSafety(result)
      pendingRequestSafety.current = null
      setRequestSafetyAnswers(initialRequestSafetyAnswers)
      setRequestSafetyConfirmations(initialRequestSafetyConfirmations)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) {
        pendingRequestSafety.current = null
      }
      setError(caught instanceof Error
        ? caught.message
        : 'The request universal safety screen could not be evaluated.')
    } finally {
      setSubmitting(false)
    }
  }

  function updateMigraineComplaintAnswer(
    key: keyof typeof initialMigraineComplaintAnswers,
    answer: TelehealthSyntheticComplaintAnswer,
  ) {
    pendingRequestComplaintTriage.current = null
    setMigraineComplaintAnswers((current) => ({ ...current, [key]: answer }))
  }

  function updateSleepComplaintAnswer(
    key: keyof typeof initialSleepComplaintAnswers,
    answer: TelehealthSyntheticComplaintAnswer,
  ) {
    pendingRequestComplaintTriage.current = null
    setSleepComplaintAnswers((current) => ({ ...current, [key]: answer }))
  }

  function updateRequestComplaintTriageConfirmation(
    key: keyof typeof initialRequestComplaintTriageConfirmations,
    checked: boolean,
  ) {
    pendingRequestComplaintTriage.current = null
    setRequestComplaintTriageConfirmations((current) => ({ ...current, [key]: checked }))
  }

  async function assessRequestComplaintTriage(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !requestComplaintTriage) return
    const activeAnswers = requestComplaintTriage.complaintCategory === 'migraine'
      ? migraineComplaintAnswers
      : sleepComplaintAnswers
    if (Object.values(activeAnswers).some((answer) => answer === '')) {
      setError('Answer every complaint-specific question before continuing. Use “Not sure” when uncertain.')
      return
    }
    if (!Object.values(requestComplaintTriageConfirmations).every(Boolean)) {
      setError('Confirm the location, callback route, and synthetic-data boundary before continuing.')
      return
    }

    const migraine = requestComplaintTriage.complaintCategory === 'migraine'
      ? {
          suddenOrWorstOnset: requireComplaintAnswer(migraineComplaintAnswers.suddenOrWorstOnset),
          newNeurologicOrVisionChange: requireComplaintAnswer(migraineComplaintAnswers.newNeurologicOrVisionChange),
          feverOrStiffNeck: requireComplaintAnswer(migraineComplaintAnswers.feverOrStiffNeck),
          recentHeadInjury: requireComplaintAnswer(migraineComplaintAnswers.recentHeadInjury),
          pregnantOrPostpartum: requireComplaintAnswer(migraineComplaintAnswers.pregnantOrPostpartum),
          cancerOrImmunocompromised: requireComplaintAnswer(migraineComplaintAnswers.cancerOrImmunocompromised),
          knownSimilarPattern: requireComplaintAnswer(migraineComplaintAnswers.knownSimilarPattern),
          persistentVomiting: requireComplaintAnswer(migraineComplaintAnswers.persistentVomiting),
        }
      : null
    const sleep = requestComplaintTriage.complaintCategory === 'sleep'
      ? {
          selfHarmThoughts: requireComplaintAnswer(sleepComplaintAnswers.selfHarmThoughts),
          maniaOrPsychosis: requireComplaintAnswer(sleepComplaintAnswers.maniaOrPsychosis),
          dangerousSomnolence: requireComplaintAnswer(sleepComplaintAnswers.dangerousSomnolence),
          withdrawalConcern: requireComplaintAnswer(sleepComplaintAnswers.withdrawalConcern),
          breathingPausesOrSevereSnoring: requireComplaintAnswer(sleepComplaintAnswers.breathingPausesOrSevereSnoring),
          pregnantOrComplexMedicationConcern: requireComplaintAnswer(sleepComplaintAnswers.pregnantOrComplexMedicationConcern),
          controlledSedativeRequest: requireComplaintAnswer(sleepComplaintAnswers.controlledSedativeRequest),
          uncomplicatedSleepDifficulty: requireComplaintAnswer(sleepComplaintAnswers.uncomplicatedSleepDifficulty),
        }
      : null
    const input = {
      expectedRequestVersion: requestComplaintTriage.requestVersion,
      contextSnapshotFingerprint: requestComplaintTriage.contextSnapshotFingerprint,
      currentLocationStateCode: requestComplaintTriage.currentLocationStateCode,
      currentLocationConfirmed: true,
      callbackNumberConfirmed: true,
      syntheticDataConfirmed: true,
      migraine,
      sleep,
    } satisfies TelehealthApplicantRequestComplaintTriageInput
    const content = JSON.stringify(input)
    if (!pendingRequestComplaintTriage.current
      || pendingRequestComplaintTriage.current.content !== content) {
      pendingRequestComplaintTriage.current = { content, idempotencyKey: crypto.randomUUID() }
    }

    setError(null)
    setSubmitting(true)
    try {
      const result = await assessApplicantTelehealthRequestComplaintTriage(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingRequestComplaintTriage.current.idempotencyKey,
      )
      setRequestComplaintTriage(result)
      pendingRequestComplaintTriage.current = null
      setMigraineComplaintAnswers(initialMigraineComplaintAnswers)
      setSleepComplaintAnswers(initialSleepComplaintAnswers)
      setRequestComplaintTriageConfirmations(initialRequestComplaintTriageConfirmations)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) {
        pendingRequestComplaintTriage.current = null
      }
      setError(caught instanceof Error
        ? caught.message
        : 'Complaint-specific triage could not be evaluated.')
    } finally {
      setSubmitting(false)
    }
  }

  function updateRequestIntakeDuration(
    duration: '' | TelehealthApplicantRequestIntakeInput['symptomDuration'],
  ) {
    pendingRequestIntake.current = null
    setRequestIntakeSymptomDuration(duration)
  }

  function updateRequestIntakeConfirmation(
    key: keyof typeof initialRequestIntakeConfirmations,
    checked: boolean,
  ) {
    pendingRequestIntake.current = null
    setRequestIntakeConfirmations((current) => ({ ...current, [key]: checked }))
  }

  async function confirmRequestIntake(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !requestIntake) return
    if (!requestIntakeSymptomDuration) {
      setError('Choose one symptom-duration range before continuing.')
      return
    }
    if (!Object.values(requestIntakeConfirmations).every(Boolean)) {
      setError('Review and accept all eight intake confirmations before continuing.')
      return
    }

    const input = {
      expectedRequestVersion: requestIntake.requestVersion,
      contextSnapshotFingerprint: requestIntake.contextSnapshotFingerprint,
      currentLocationStateCode: requestIntake.currentLocationStateCode,
      symptomDuration: requestIntakeSymptomDuration,
      currentLocationConfirmed: true,
      callbackNumberConfirmed: true,
      priorInformationReviewed: true,
      insuranceLimitationsAcknowledged: true,
      pendingConsentAcknowledged: true,
      pendingVerificationAcknowledged: true,
      complaintResultAcknowledged: true,
      syntheticDataConfirmed: true,
    } satisfies TelehealthApplicantRequestIntakeInput
    const content = JSON.stringify(input)
    if (!pendingRequestIntake.current || pendingRequestIntake.current.content !== content) {
      pendingRequestIntake.current = { content, idempotencyKey: crypto.randomUUID() }
    }

    setError(null)
    setSubmitting(true)
    try {
      const result = await confirmApplicantTelehealthRequestIntake(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingRequestIntake.current.idempotencyKey,
      )
      setRequestIntake(result)
      pendingRequestIntake.current = null
      setRequestIntakeSymptomDuration('')
      setRequestIntakeConfirmations(initialRequestIntakeConfirmations)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) {
        pendingRequestIntake.current = null
      }
      setError(caught instanceof Error
        ? caught.message
        : 'The request intake snapshot could not be confirmed.')
    } finally {
      setSubmitting(false)
    }
  }

  function updateRequestInsuranceSourceConfirmation(
    key: keyof typeof initialRequestInsuranceSourceConfirmations,
    checked: boolean,
  ) {
    pendingRequestInsuranceSource.current = null
    setRequestInsuranceSourceConfirmations((current) => ({ ...current, [key]: checked }))
  }

  async function confirmRequestInsuranceSource(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !requestInsuranceSource) return
    if (!Object.values(requestInsuranceSourceConfirmations).every(Boolean)) {
      setError('Review and accept all seven insurance-source confirmations before continuing.')
      return
    }

    const input = {
      expectedRequestVersion: requestInsuranceSource.requestVersion,
      insuranceSourceSnapshotFingerprint: requestInsuranceSource.insuranceSourceSnapshotFingerprint,
      payerProductConfirmed: true,
      maskedMemberDetailsConfirmed: true,
      subscriberRelationshipConfirmed: true,
      primaryCoverageSourceConfirmed: true,
      freshVerificationRequested: true,
      evidenceLimitationsAcknowledged: true,
      syntheticDataConfirmed: true,
    } satisfies TelehealthApplicantRequestInsuranceSourceInput
    const content = JSON.stringify(input)
    if (!pendingRequestInsuranceSource.current
      || pendingRequestInsuranceSource.current.content !== content) {
      pendingRequestInsuranceSource.current = { content, idempotencyKey: crypto.randomUUID() }
    }

    setError(null)
    setSubmitting(true)
    try {
      const result = await confirmApplicantTelehealthRequestInsuranceSource(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingRequestInsuranceSource.current.idempotencyKey,
      )
      setRequestInsuranceSource(result)
      pendingRequestInsuranceSource.current = null
      setRequestInsuranceSourceConfirmations(initialRequestInsuranceSourceConfirmations)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) {
        pendingRequestInsuranceSource.current = null
      }
      setError(caught instanceof Error
        ? caught.message
        : 'The request insurance source could not be confirmed.')
    } finally {
      setSubmitting(false)
    }
  }

  function updateRequestEligibilityAcknowledgment(
    key: keyof typeof initialRequestEligibilityAcknowledgments,
    checked: boolean,
  ) {
    pendingRequestEligibility.current = null
    setRequestEligibilityAcknowledgments((current) => ({ ...current, [key]: checked }))
  }

  async function runRequestEligibility(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !requestEligibility) return
    if (!Object.values(requestEligibilityAcknowledgments).every(Boolean)) {
      setError('Accept both synthetic eligibility acknowledgments before continuing.')
      return
    }
    const input = {
      expectedRequestVersion: requestEligibility.requestVersion,
      eligibilitySnapshotFingerprint: requestEligibility.eligibilitySnapshotFingerprint,
      syntheticDataConfirmed: true,
      noGuaranteeAcknowledged: true,
    } satisfies TelehealthApplicantRequestEligibilityInput
    const content = JSON.stringify(input)
    if (!pendingRequestEligibility.current || pendingRequestEligibility.current.content !== content) {
      pendingRequestEligibility.current = { content, idempotencyKey: crypto.randomUUID() }
    }

    setError(null)
    setSubmitting(true)
    try {
      const result = await runApplicantTelehealthRequestEligibility(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingRequestEligibility.current.idempotencyKey,
      )
      setRequestEligibility(result)
      pendingRequestEligibility.current = null
      setRequestEligibilityAcknowledgments(initialRequestEligibilityAcknowledgments)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) {
        pendingRequestEligibility.current = null
      }
      setError(caught instanceof Error
        ? caught.message
        : 'Fresh request eligibility could not be completed.')
    } finally {
      setSubmitting(false)
    }
  }

  function updateRequestPracticeNetworkAcknowledgment(
    key: keyof typeof initialRequestPracticeNetworkAcknowledgments,
    checked: boolean,
  ) {
    pendingRequestPracticeNetwork.current = null
    setRequestPracticeNetworkAcknowledgments((current) => ({ ...current, [key]: checked }))
  }

  async function runRequestPracticeNetwork(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !requestPracticeNetwork) return
    if (!Object.values(requestPracticeNetworkAcknowledgments).every(Boolean)) {
      setError('Accept all three synthetic practice-network acknowledgments before continuing.')
      return
    }
    const input = {
      expectedRequestVersion: requestPracticeNetwork.requestVersion,
      networkSnapshotFingerprint: requestPracticeNetwork.networkSnapshotFingerprint,
      syntheticDataConfirmed: true,
      practiceOnlyScopeAcknowledged: true,
      noGuaranteeAcknowledged: true,
    } satisfies TelehealthApplicantRequestPracticeNetworkInput
    const content = JSON.stringify(input)
    if (!pendingRequestPracticeNetwork.current
      || pendingRequestPracticeNetwork.current.content !== content) {
      pendingRequestPracticeNetwork.current = { content, idempotencyKey: crypto.randomUUID() }
    }

    setError(null)
    setSubmitting(true)
    try {
      const result = await runApplicantTelehealthRequestPracticeNetwork(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingRequestPracticeNetwork.current.idempotencyKey,
      )
      setRequestPracticeNetwork(result)
      pendingRequestPracticeNetwork.current = null
      setRequestPracticeNetworkAcknowledgments(initialRequestPracticeNetworkAcknowledgments)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) {
        pendingRequestPracticeNetwork.current = null
      }
      setError(caught instanceof Error
        ? caught.message
        : 'Fresh request practice-network verification could not be completed.')
    } finally {
      setSubmitting(false)
    }
  }

  function updateRequestRenderingCandidateAcknowledgment(
    key: keyof typeof initialRequestRenderingCandidateAcknowledgments,
    checked: boolean,
  ) {
    pendingRequestRenderingCandidate.current = null
    setRequestRenderingCandidateAcknowledgments((current) => ({ ...current, [key]: checked }))
  }

  async function selectRequestRenderingCandidate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!applicant || !applicantSession || !requestRenderingCandidate) return
    if (!Object.values(requestRenderingCandidateAcknowledgments).every(Boolean)) {
      setError('Accept all four rendering-candidate acknowledgments before continuing.')
      return
    }
    const input = {
      expectedRequestVersion: requestRenderingCandidate.requestVersion,
      candidateSnapshotFingerprint: requestRenderingCandidate.candidateSnapshotFingerprint,
      syntheticDataConfirmed: true,
      candidateOnlyScopeAcknowledged: true,
      noAssignmentAcknowledged: true,
      networkCheckStillRequiredAcknowledged: true,
    } satisfies TelehealthApplicantRequestRenderingCandidateInput
    const content = JSON.stringify(input)
    if (!pendingRequestRenderingCandidate.current
      || pendingRequestRenderingCandidate.current.content !== content) {
      pendingRequestRenderingCandidate.current = { content, idempotencyKey: crypto.randomUUID() }
    }

    setError(null)
    setSubmitting(true)
    try {
      const result = await selectApplicantTelehealthRequestRenderingCandidate(
        applicant.applicantId,
        applicantSession.applicantAccessKey,
        input,
        pendingRequestRenderingCandidate.current.idempotencyKey,
      )
      setRequestRenderingCandidate(result)
      pendingRequestRenderingCandidate.current = null
      setRequestRenderingCandidateAcknowledgments(initialRequestRenderingCandidateAcknowledgments)
    } catch (caught: unknown) {
      if (caught instanceof ApiRequestError && caught.status && caught.status < 500) {
        pendingRequestRenderingCandidate.current = null
      }
      setError(caught instanceof Error
        ? caught.message
        : 'The rendering candidate could not be selected for network evaluation.')
    } finally {
      setSubmitting(false)
    }
  }

  function restart() {
    clearApplicantSession()
    pendingCreate.current = null
    pendingVerification.current = null
    pendingSafetyTriage.current = null
    pendingVisitPurpose.current = null
    pendingPracticeNetworkPrecheck.current = null
    pendingMemberInsuranceDetails.current = null
    pendingEligibility.current = null
    pendingPracticeNetwork.current = null
    pendingIdentityProofing.current = null
    pendingNoticeAcknowledgment.current = null
    pendingRegistrationDetailsConfirmation.current = null
    pendingInsuranceHandoffConfirmation.current = null
    pendingCommunicationAccessReadiness.current = null
    pendingDevicePreparation.current = null
    pendingClinicalInformationInventory.current = null
    pendingMedicationInformation.current = null
    pendingAllergyInformation.current = null
    pendingHealthHistoryInformation.current = null
    pendingClinicalInformationSummary.current = null
    pendingPreRequestReadiness.current = null
    pendingPracticeReview.current = null
    pendingRequestCreation.current = null
    pendingRequestLocation.current = null
    pendingRequestSafety.current = null
    pendingRequestComplaintTriage.current = null
    pendingRequestIntake.current = null
    pendingRequestInsuranceSource.current = null
    pendingRequestEligibility.current = null
    pendingRequestPracticeNetwork.current = null
    pendingRequestRenderingCandidate.current = null
    setApplicantSession(null)
    setApplicant(null)
    setValues(initialValues)
    setVerificationCode('')
    setCurrentLocationStateCode('')
    setLocationConfirmed(false)
    setSafetyAnswers(initialSafetyAnswers)
    setSafetySyntheticConfirmed(false)
    setSafetyResult(null)
    setPurposeCategory('')
    setPurposeSyntheticConfirmed(false)
    setPurposeResult(null)
    setNetworkOptions(null)
    setNetworkOptionsLoading(false)
    setNetworkOptionsLoadAttempt(0)
    setPlanKey('')
    setNetworkSyntheticConfirmed(false)
    setNetworkResult(null)
    setMemberId('')
    setGroupNumber('')
    setSubscriberRelationship('')
    setSubscriberFirstName('')
    setSubscriberLastName('')
    setSubscriberDateOfBirth('')
    setMemberDetailsConfirmed(false)
    setMemberSyntheticConfirmed(false)
    setMemberDetailsResult(null)
    setEligibilitySyntheticConfirmed(false)
    setEligibilityResult(null)
    setPracticeNetworkSyntheticConfirmed(false)
    setPracticeNetworkResult(null)
    setIdentityPrivacyNoticeAcknowledged(false)
    setIdentitySyntheticConfirmed(false)
    setIdentityProofingResult(null)
    setNotice(null)
    setNoticeLoading(false)
    setNoticeLoadAttempt(0)
    setNoticeAcknowledgments(initialNoticeAcknowledgments)
    setRegistrationDetails(null)
    setRegistrationDetailsLoading(false)
    setRegistrationDetailsLoadAttempt(0)
    setRegistrationDetailsConfirmations(initialRegistrationDetailsConfirmations)
    setInsuranceHandoff(null)
    setInsuranceHandoffLoading(false)
    setInsuranceHandoffLoadAttempt(0)
    setInsuranceHandoffConfirmations(initialInsuranceHandoffConfirmations)
    setCommunicationAccessReadiness(null)
    setCommunicationAccessLoading(false)
    setCommunicationAccessLoadAttempt(0)
    setPreferredSpokenLanguage('')
    setInterpreterRequested(false)
    setAccessibilitySupportRequested(false)
    setCommunicationAccessConfirmations(initialCommunicationAccessConfirmations)
    setDevicePreparation(null)
    setDevicePreparationLoading(false)
    setDevicePreparationLoadAttempt(0)
    setDeviceCheckResult(null)
    setDeviceCheckRunning(false)
    setDevicePreparationAcknowledgments(initialDevicePreparationAcknowledgments)
    setClinicalInformationInventory(null)
    setClinicalInformationInventoryLoading(false)
    setClinicalInformationInventoryLoadAttempt(0)
    setMedicationsInventoryStatus('')
    setAllergiesInventoryStatus('')
    setOtherHealthHistoryInventoryStatus('')
    setClinicalInformationInventoryAcknowledgments(initialClinicalInformationInventoryAcknowledgments)
    setMedicationInformation(null)
    setMedicationInformationLoading(false)
    setMedicationInformationLoadAttempt(0)
    setReportedMedicationUseStatuses({})
    setAdditionalOrUnlistedMedicationReported(false)
    setMedicationInformationAcknowledgments(initialMedicationInformationAcknowledgments)
    setAllergyInformation(null)
    setAllergyInformationLoading(false)
    setAllergyInformationLoadAttempt(0)
    setReportedAllergyItems({})
    setAdditionalOrUnlistedAllergyReported(false)
    setAllergyInformationAcknowledgments(initialAllergyInformationAcknowledgments)
    setHealthHistoryInformation(null)
    setHealthHistoryInformationLoading(false)
    setHealthHistoryInformationLoadAttempt(0)
    setReportedHealthHistoryTopics({})
    setAdditionalOrUnlistedHealthHistoryReported(false)
    setHealthHistoryInformationAcknowledgments(initialHealthHistoryInformationAcknowledgments)
    setClinicalInformationSummary(null)
    setClinicalInformationSummaryLoading(false)
    setClinicalInformationSummaryLoadAttempt(0)
    setClinicalInformationSummaryAcknowledgments(initialClinicalInformationSummaryAcknowledgments)
    setPreRequestReadiness(null)
    setPreRequestReadinessLoading(false)
    setPreRequestReadinessLoadAttempt(0)
    setPreRequestReadinessAcknowledgments(initialPreRequestReadinessAcknowledgments)
    setPracticeReview(null)
    setPracticeReviewLoading(false)
    setPracticeReviewLoadAttempt(0)
    setPracticeReviewAcknowledgments(initialPracticeReviewAcknowledgments)
    setRequestCreation(null)
    setRequestCreationLoading(false)
    setRequestCreationLoadAttempt(0)
    setRequestCreationAcknowledgments(initialRequestCreationAcknowledgments)
    setRequestLocation(null)
    setRequestLocationLoading(false)
    setRequestLocationLoadAttempt(0)
    setRequestLocationStateCode('')
    setRequestLocationAcknowledgments(initialRequestLocationAcknowledgments)
    setRequestSafety(null)
    setRequestSafetyLoading(false)
    setRequestSafetyLoadAttempt(0)
    setRequestSafetyAnswers(initialRequestSafetyAnswers)
    setRequestSafetyConfirmations(initialRequestSafetyConfirmations)
    setRequestComplaintTriage(null)
    setRequestComplaintTriageLoading(false)
    setRequestComplaintTriageLoadAttempt(0)
    setMigraineComplaintAnswers(initialMigraineComplaintAnswers)
    setSleepComplaintAnswers(initialSleepComplaintAnswers)
    setRequestComplaintTriageConfirmations(initialRequestComplaintTriageConfirmations)
    setRequestIntake(null)
    setRequestIntakeLoading(false)
    setRequestIntakeLoadAttempt(0)
    setRequestIntakeSymptomDuration('')
    setRequestIntakeConfirmations(initialRequestIntakeConfirmations)
    setRequestInsuranceSource(null)
    setRequestInsuranceSourceLoading(false)
    setRequestInsuranceSourceLoadAttempt(0)
    setRequestInsuranceSourceConfirmations(initialRequestInsuranceSourceConfirmations)
    setRequestEligibility(null)
    setRequestEligibilityLoading(false)
    setRequestEligibilityLoadAttempt(0)
    setRequestEligibilityAcknowledgments(initialRequestEligibilityAcknowledgments)
    setRequestPracticeNetwork(null)
    setRequestPracticeNetworkLoading(false)
    setRequestPracticeNetworkLoadAttempt(0)
    setRequestPracticeNetworkAcknowledgments(initialRequestPracticeNetworkAcknowledgments)
    setRequestRenderingCandidate(null)
    setRequestRenderingCandidateLoading(false)
    setRequestRenderingCandidateLoadAttempt(0)
    setRequestRenderingCandidateAcknowledgments(initialRequestRenderingCandidateAcknowledgments)
    setError(null)
  }

  return (
    <main className="telehealth-page telehealth-landing" id="main-content">
      <section className="telehealth-hero" aria-labelledby="prospective-telehealth-title">
        <p className="telehealth-kicker">Practice-branded new-patient entry</p>
        <h1 id="prospective-telehealth-title">Start as a new patient</h1>
        <p>Create a short-lived prospective applicant for this practice.</p>
        <div className="telehealth-synthetic" role="note">
          Synthetic demonstration only. Do not enter real personal, insurance, or health information.
        </div>
      </section>

      <section className="telehealth-emergency" aria-labelledby="prospective-emergency-title">
        <h2 id="prospective-emergency-title">If this may be an emergency</h2>
        <p>Call 911 now or go to the nearest emergency department. This form does not request care.</p>
        <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
      </section>

      <section className="telehealth-card" aria-live="polite" aria-busy={loading || submitting}>
        <div className="telehealth-heading">
          <div>
            <p className="telehealth-kicker">Identity step</p>
            <h2>{applicant ? 'Applicant status' : 'Minimum applicant details'}</h2>
          </div>
          <Link className="telehealth-button telehealth-button-secondary" to="/telehealth">Back to telehealth</Link>
        </div>

        {loading ? <p>Loading the synthetic applicant session…</p> : null}
        {error ? (
          <div className="telehealth-error" role="alert" tabIndex={-1} ref={errorRef}>
            <p>{error}</p>
            {applicantSession && !applicant ? (
              <button className="telehealth-button" type="button" onClick={() => setResumeAttempt((value) => value + 1)}>Try loading again</button>
            ) : null}
          </div>
        ) : null}

        {!loading && !applicantSession ? (
          <form onSubmit={createApplicant}>
            <p id="minimum-data-help">Only the fields needed for the synthetic identity shell are collected. Insurance and symptoms come later, after approved identity review.</p>
            <div className="telehealth-form-grid" aria-describedby="minimum-data-help">
              <label>Legal first name
                <input required maxLength={100} autoComplete="off" value={values.legalFirstName} onChange={(event) => updateValue('legalFirstName', event.target.value)} />
              </label>
              <label>Legal last name
                <input required maxLength={100} autoComplete="off" value={values.legalLastName} onChange={(event) => updateValue('legalLastName', event.target.value)} />
              </label>
              <label>Date of birth
                <input required type="date" autoComplete="off" value={values.dateOfBirth} onChange={(event) => updateValue('dateOfBirth', event.target.value)} />
              </label>
              <label>Synthetic email
                <input required type="email" maxLength={254} autoComplete="off" value={values.email} onChange={(event) => updateValue('email', event.target.value)} />
              </label>
              <label>Synthetic phone
                <input required type="tel" inputMode="tel" autoComplete="off" value={values.phone} onChange={(event) => updateValue('phone', event.target.value)} />
              </label>
              <label>State of residence
                <select required value={values.residenceStateCode} onChange={(event) => updateValue('residenceStateCode', event.target.value)}>
                  <option value="">Choose a state</option>
                  <option value="GA">Georgia</option>
                  <option value="CA">California</option>
                  <option value="FL">Florida</option>
                </select>
              </label>
              <label>Postal code
                <input required inputMode="numeric" pattern="[0-9]{5}" maxLength={5} autoComplete="off" value={values.postalCode} onChange={(event) => updateValue('postalCode', event.target.value)} />
              </label>
            </div>
            <label className="telehealth-check">
              <input required type="checkbox" checked={values.syntheticDataConfirmed} onChange={(event) => updateValue('syntheticDataConfirmed', event.target.checked)} />
              <span>I confirm every value above is fictional synthetic demonstration data.</span>
            </label>
            <p className="telehealth-inline-warning">The access key is kept only in this browser tab session. Closing the tab may require starting again.</p>
            <button className="telehealth-button" type="submit" disabled={submitting || !values.syntheticDataConfirmed}>
              {submitting ? 'Creating applicant…' : 'Create synthetic applicant'}
            </button>
          </form>
        ) : null}

        {applicant ? (
          <div className="telehealth-applicant-status">
            <dl className="telehealth-details">
              <div><dt>Practice</dt><dd>{applicant.practiceDisplayName}</dd></div>
              <div><dt>Status</dt><dd>{applicant.status}</dd></div>
              <div><dt>Email</dt><dd>{applicant.maskedEmail}</dd></div>
              <div><dt>Phone</dt><dd>{applicant.maskedPhone}</dd></div>
              <div><dt>Identity assurance</dt><dd>{applicant.identityAssurance === 'ContactControlOnly' ? 'Synthetic contact control only' : 'Contact not verified'}</dd></div>
              <div><dt>Patient record</dt><dd>{applicant.canonicalPatientCreated ? 'Synthetic portal-disabled shell created' : 'Not created'}</dd></div>
            </dl>
            <p className="telehealth-position"><strong>{applicant.nextAction}</strong></p>
            {applicant.status === 'ContactVerificationPending' ? (
              <form onSubmit={verifyContact}>
                <div className="telehealth-synthetic" role="note">
                  No email was sent. Demonstration code: <code>{applicant.demonstrationVerificationCode}</code>
                </div>
                <label htmlFor="telehealth-verification-code">Six-digit demonstration code</label>
                <input id="telehealth-verification-code" required inputMode="numeric" pattern="[0-9]{6}" maxLength={6} autoComplete="one-time-code" value={verificationCode} onChange={(event) => { pendingVerification.current = null; setVerificationCode(event.target.value) }} />
                <p>{applicant.verificationAttemptsRemaining} attempts remain.</p>
                <button className="telehealth-button" type="submit" disabled={submitting}>{submitting ? 'Verifying…' : 'Verify synthetic contact'}</button>
              </form>
            ) : null}
            {applicant.status === 'IdentityReviewPending' ? (
              <div className="telehealth-coverage-result" role="status">
                <h3>Stopped safely at identity review</h3>
                <p>Contact control was demonstrated, but identity was not proven.</p>
                <p>Duplicate result: {applicant.duplicateDisposition === 'PossibleMatchManualReview' ? 'Possible existing record—manual review required' : 'No exact candidate found—identity review still required'}.</p>
                <p><strong>No canonical patient, chart, portal account, visit request, or queue entry was created.</strong></p>
              </div>
            ) : null}
            {applicant.status === 'ManualReviewRequired' ? (
              <div className="telehealth-coverage-result" role="status">
                <h3>Manual matching review required</h3>
                <p>{applicant.nextAction}</p>
                <p><strong>No possible matching patient is disclosed and no patient or request was created.</strong></p>
              </div>
            ) : null}
            {applicant.status === 'IdentityReviewApproved' ? (
              <form className="telehealth-review-form" onSubmit={evaluateSafetyTriage}>
                <div className="telehealth-emergency" role="note">
                  <h3>Emergency direction stays available</h3>
                  <p>If any symptom may be an emergency, call 911 now or go to the nearest emergency department. You do not need to submit this form first.</p>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
                <p id="prospective-safety-help">This is one synthetic universal safety screen, not diagnosis or complete telehealth eligibility. Choose an explicit answer for every question.</p>
                <label>Current physical location
                  <select required value={currentLocationStateCode} onChange={(event) => { pendingSafetyTriage.current = null; setCurrentLocationStateCode(event.target.value as typeof currentLocationStateCode) }}>
                    <option value="">Choose a state</option>
                    <option value="GA">Georgia</option>
                    <option value="CA">California</option>
                    <option value="FL">Florida</option>
                  </select>
                </label>
                <label className="telehealth-check"><input required type="checkbox" checked={locationConfirmed} onChange={(event) => { pendingSafetyTriage.current = null; setLocationConfirmed(event.target.checked) }} /><span>I confirm this is the synthetic applicant's current physical location, not merely a residence address.</span></label>
                <fieldset aria-describedby="prospective-safety-help"><legend>Could any current symptom be an emergency?</legend><label className="telehealth-check"><input required type="radio" name="prospective-emergency" value="yes" checked={safetyAnswers.emergency === 'yes'} onChange={() => updateSafetyAnswer('emergency', 'yes')} />Yes</label><label className="telehealth-check"><input required type="radio" name="prospective-emergency" value="no" checked={safetyAnswers.emergency === 'no'} onChange={() => updateSafetyAnswer('emergency', 'no')} />No</label></fieldset>
                {safetyAnswers.emergency === 'yes' ? <p className="telehealth-inline-warning" role="alert"><strong>Call 911 now or go to the nearest emergency department.</strong> This form has not contacted a clinician.</p> : null}
                <fieldset><legend>Are symptoms severe or getting worse quickly?</legend><label className="telehealth-check"><input required type="radio" name="prospective-severe" value="yes" checked={safetyAnswers.severe === 'yes'} onChange={() => updateSafetyAnswer('severe', 'yes')} />Yes</label><label className="telehealth-check"><input required type="radio" name="prospective-severe" value="no" checked={safetyAnswers.severe === 'no'} onChange={() => updateSafetyAnswer('severe', 'no')} />No</label></fieldset>
                <fieldset><legend>Does this seem to require a hands-on examination or procedure?</legend><label className="telehealth-check"><input required type="radio" name="prospective-hands-on" value="yes" checked={safetyAnswers.handsOn === 'yes'} onChange={() => updateSafetyAnswer('handsOn', 'yes')} />Yes</label><label className="telehealth-check"><input required type="radio" name="prospective-hands-on" value="no" checked={safetyAnswers.handsOn === 'no'} onChange={() => updateSafetyAnswer('handsOn', 'no')} />No</label></fieldset>
                <fieldset><legend>Are you unsure about any answer above?</legend><label className="telehealth-check"><input required type="radio" name="prospective-unsure" value="yes" checked={safetyAnswers.unsure === 'yes'} onChange={() => updateSafetyAnswer('unsure', 'yes')} />Yes</label><label className="telehealth-check"><input required type="radio" name="prospective-unsure" value="no" checked={safetyAnswers.unsure === 'no'} onChange={() => updateSafetyAnswer('unsure', 'no')} />No</label></fieldset>
                <label className="telehealth-check"><input required type="checkbox" checked={safetySyntheticConfirmed} onChange={(event) => { pendingSafetyTriage.current = null; setSafetySyntheticConfirmed(event.target.checked) }} /><span>I confirm the location and every safety answer are fictional synthetic demonstration data.</span></label>
                <button className="telehealth-button" type="submit" disabled={submitting || !locationConfirmed || !safetySyntheticConfirmed}>{submitting ? 'Evaluating safety screen…' : 'Evaluate universal safety screen'}</button>
              </form>
            ) : null}
            {applicant.status.startsWith('Safety') ? (
              <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={safetyResultRef}>
                <h3>{applicant.status === 'SafetyScreenPassed' ? 'Universal safety screen recorded' : 'Universal safety screen stopped progression'}</h3>
                <p><strong>{applicant.nextAction}</strong></p>
                <p>No clinician reviewed these answers. No patient, chart, complete intake, coverage check, request, appointment, or queue entry was created.</p>
                {applicant.status === 'SafetyEmergencyRedirect' ? <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a> : null}
                {safetyResult ? <ul>{safetyResult.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul> : null}
              </div>
            ) : null}
            {applicant.status === 'SafetyScreenPassed' ? (
              <form className="telehealth-review-form" onSubmit={recordVisitPurpose}>
                <div className="telehealth-emergency" role="note">
                  <h3>Emergency direction remains available</h3>
                  <p>If symptoms change or may be an emergency, call 911 or go to the nearest emergency department. Selecting a category does not contact a clinician.</p>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
                <fieldset aria-describedby="prospective-purpose-help">
                  <legend>What is the synthetic visit about?</legend>
                  <p id="prospective-purpose-help">Choose one navigation category. This is not a diagnosis, complaint-specific clinical triage, or a decision that telehealth can treat the concern.</p>
                  <label className="telehealth-check"><input required type="radio" name="prospective-purpose" value="migraine" checked={purposeCategory === 'migraine'} onChange={() => { pendingVisitPurpose.current = null; setPurposeCategory('migraine') }} /><span>Headache or known migraine pattern</span></label>
                  <label className="telehealth-check"><input required type="radio" name="prospective-purpose" value="sleep" checked={purposeCategory === 'sleep'} onChange={() => { pendingVisitPurpose.current = null; setPurposeCategory('sleep') }} /><span>Sleep difficulty</span></label>
                </fieldset>
                <label className="telehealth-check"><input required type="checkbox" checked={purposeSyntheticConfirmed} onChange={(event) => { pendingVisitPurpose.current = null; setPurposeSyntheticConfirmed(event.target.checked) }} /><span>I confirm this visit-purpose selection is fictional synthetic demonstration data.</span></label>
                <button className="telehealth-button" type="submit" disabled={submitting || !purposeCategory || !purposeSyntheticConfirmed}>{submitting ? 'Recording visit purpose…' : 'Record synthetic visit purpose'}</button>
              </form>
            ) : null}
            {applicant.status === 'VisitPurposeRecorded' ? (
              <>
                <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={purposeResultRef}>
                  <h3>Synthetic visit purpose recorded</h3>
                  <p><strong>{purposeResult?.purposeDisplayLabel ?? 'The controlled visit-purpose category is on file.'}</strong></p>
                  <p>No complaint-specific clinical protocol ran. No clinician reviewed the selection, and no patient, request, appointment, queue entry, or care capability was created.</p>
                  {purposeResult ? <ul>{purposeResult.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul> : null}
                </div>
                <div className="telehealth-emergency" role="note">
                  <h3>Emergency direction remains available</h3>
                  <p>If symptoms change or may be an emergency, call 911 or go to the nearest emergency department. Choosing a fictional plan does not contact a clinician.</p>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
                <div className="telehealth-synthetic" role="note">
                  <strong>NON_PRODUCTION practice-level plan discovery only.</strong> Do not enter real insurance information. This step collects no member ID, group number, policy number, card, physician, price, or payment details.
                </div>
                {networkOptionsLoading ? <p role="status">Loading the synthetic plan catalog…</p> : null}
                {!networkOptionsLoading && !networkOptions ? (
                  <button className="telehealth-button" type="button" onClick={() => setNetworkOptionsLoadAttempt((value) => value + 1)}>Try loading plan options again</button>
                ) : null}
                {networkOptions ? (
                  <form className="telehealth-review-form" onSubmit={recordPracticeNetworkPrecheck}>
                    <fieldset aria-describedby="prospective-network-help">
                      <legend>Choose one fictional plan</legend>
                      <p id="prospective-network-help">These versioned fixtures describe only a practice-plan association. They do not check you, your benefits, an eventual physician, exact network status, coverage, cost, or payment.</p>
                      {networkOptions.plans.map((plan) => (
                        <label className="telehealth-check" key={plan.planKey}>
                          <input
                            required
                            type="radio"
                            name="prospective-network-plan"
                            value={plan.planKey}
                            checked={planKey === plan.planKey}
                            onChange={() => {
                              pendingPracticeNetworkPrecheck.current = null
                              setPlanKey(plan.planKey)
                            }}
                          />
                          <span>
                            <strong>{plan.payerDisplayName} — {plan.productDisplayName}</strong><br />
                            {plan.practiceNetworkStatus === 'PracticeNetworkConfirmedFixture' ? 'Practice participation fixture' : plan.practiceNetworkStatus === 'PracticeOutOfNetworkFixture' ? 'Practice out-of-network fixture' : 'Practice network unknown'}<br />
                            <small>{plan.meaning}</small>
                          </span>
                        </label>
                      ))}
                    </fieldset>
                    <p><strong>No result on this screen is an insurance eligibility, benefits, coverage, network, price, or payment guarantee.</strong></p>
                    <label className="telehealth-check">
                      <input required type="checkbox" checked={networkSyntheticConfirmed} onChange={(event) => { pendingPracticeNetworkPrecheck.current = null; setNetworkSyntheticConfirmed(event.target.checked) }} />
                      <span>I confirm the selected plan is fictional synthetic demonstration data and understand this is not an insurance verification.</span>
                    </label>
                    <button className="telehealth-button" type="submit" disabled={submitting || !planKey || !networkSyntheticConfirmed}>{submitting ? 'Recording practice precheck…' : 'Record synthetic practice precheck'}</button>
                    <p className="telehealth-inline-warning">Catalog version {networkOptions.catalogVersion}; effective through {new Date(networkOptions.catalogEffectiveThrough).toLocaleDateString()}.</p>
                  </form>
                ) : null}
              </>
            ) : null}
            {applicant.status === 'PracticeNetworkPrecheckRecorded' ? (
              <>
                <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={networkResultRef}>
                  <h3>Synthetic practice-level precheck recorded</h3>
                  <p><strong>{networkResult ? `${networkResult.payerDisplayName} — ${networkResult.productDisplayName}` : 'The fictional plan fixture is on file.'}</strong></p>
                  <p>{networkResult?.practiceNetworkStatus === 'PracticeNetworkConfirmedFixture' ? 'Practice participation fixture—not individual coverage or exact network.' : networkResult?.practiceNetworkStatus === 'PracticeOutOfNetworkFixture' ? 'Practice out-of-network fixture—no self-pay choice or estimate was created.' : 'Practice network status remains unknown.'}</p>
                  <p><strong>No member eligibility, benefits, rendering-physician participation, exact network confirmation, coverage, estimate, payment guarantee, patient, request, appointment, queue, or care capability was created.</strong></p>
                  {networkResult ? <ul>{networkResult.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul> : null}
                </div>
                <div className="telehealth-emergency" role="note">
                  <h3>Emergency direction remains available</h3>
                  <p>If symptoms may be an emergency, call 911 now or go to the nearest emergency department.</p>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
                <div className="telehealth-synthetic" role="note">
                  <strong>Protected synthetic receipt only.</strong> Enter only fictional identifiers beginning with <code>SYN-</code>. This form does not contact an insurer or create coverage.
                </div>
                <form className="telehealth-review-form" onSubmit={recordMemberInsuranceDetails}>
                  <p id="prospective-member-details-help">The server reuses the selected fictional plan. Raw normalized member, group, and subscriber values are protected and are not returned; only last-four masks appear in the receipt.</p>
                  <div className="telehealth-form-grid" aria-describedby="prospective-member-details-help">
                    <label>Fictional member ID
                      <input
                        required
                        autoComplete="off"
                        spellCheck={false}
                        minLength={6}
                        maxLength={32}
                        pattern="SYN-[A-Za-z0-9][A-Za-z0-9-]{0,26}[A-Za-z0-9]"
                        placeholder="SYN-HM-1001"
                        value={memberId}
                        onChange={(event) => { clearPendingMemberDetails(); setMemberId(event.target.value) }}
                      />
                    </label>
                    <label>Fictional group number (optional)
                      <input
                        autoComplete="off"
                        spellCheck={false}
                        minLength={6}
                        maxLength={32}
                        pattern="SYN-[A-Za-z0-9][A-Za-z0-9-]{0,26}[A-Za-z0-9]"
                        placeholder="SYN-GROUP-01"
                        value={groupNumber}
                        onChange={(event) => { clearPendingMemberDetails(); setGroupNumber(event.target.value) }}
                      />
                    </label>
                    <label>Subscriber relationship
                      <select
                        required
                        value={subscriberRelationship}
                        onChange={(event) => {
                          clearPendingMemberDetails()
                          const relationship = event.target.value as typeof subscriberRelationship
                          setSubscriberRelationship(relationship)
                          if (relationship === 'Self') {
                            setSubscriberFirstName('')
                            setSubscriberLastName('')
                            setSubscriberDateOfBirth('')
                          }
                        }}
                      >
                        <option value="">Choose a relationship</option>
                        <option value="Self">Self</option>
                        <option value="Spouse">Spouse</option>
                        <option value="Parent">Parent</option>
                        <option value="Other">Other</option>
                      </select>
                    </label>
                  </div>
                  {subscriberRelationship && subscriberRelationship !== 'Self' ? (
                    <fieldset>
                      <legend>Fictional subscriber identity</legend>
                      <p>Required only because the subscriber is not the applicant. These values will not be returned.</p>
                      <div className="telehealth-form-grid">
                        <label>Subscriber first name
                          <input required maxLength={100} autoComplete="off" value={subscriberFirstName} onChange={(event) => { clearPendingMemberDetails(); setSubscriberFirstName(event.target.value) }} />
                        </label>
                        <label>Subscriber last name
                          <input required maxLength={100} autoComplete="off" value={subscriberLastName} onChange={(event) => { clearPendingMemberDetails(); setSubscriberLastName(event.target.value) }} />
                        </label>
                        <label>Subscriber date of birth
                          <input required type="date" autoComplete="off" value={subscriberDateOfBirth} onChange={(event) => { clearPendingMemberDetails(); setSubscriberDateOfBirth(event.target.value) }} />
                        </label>
                      </div>
                    </fieldset>
                  ) : null}
                  <p><strong>This is not member matching, eligibility, benefits, exact network confirmation, coverage, a price estimate, or a payment guarantee.</strong></p>
                  <label className="telehealth-check">
                    <input required type="checkbox" checked={memberDetailsConfirmed} onChange={(event) => { clearPendingMemberDetails(); setMemberDetailsConfirmed(event.target.checked) }} />
                    <span>I reviewed this minimum fictional member, group, and subscriber information.</span>
                  </label>
                  <label className="telehealth-check">
                    <input required type="checkbox" checked={memberSyntheticConfirmed} onChange={(event) => { clearPendingMemberDetails(); setMemberSyntheticConfirmed(event.target.checked) }} />
                    <span>I confirm every insurance and subscriber value is fictional synthetic demonstration data.</span>
                  </label>
                  <button className="telehealth-button" type="submit" disabled={submitting || !memberId || !subscriberRelationship || !memberDetailsConfirmed || !memberSyntheticConfirmed}>{submitting ? 'Protecting member details…' : 'Record protected synthetic receipt'}</button>
                </form>
              </>
            ) : null}
            {applicant.status === 'MemberInsuranceDetailsRecorded' ? (
              <>
                <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={memberDetailsResultRef}>
                  <h3>Protected synthetic member-details receipt recorded</h3>
                  <p><strong>{memberDetailsResult ? `${memberDetailsResult.payerDisplayName} — ${memberDetailsResult.productDisplayName}` : 'The selected fictional plan remains on file.'}</strong></p>
                  {memberDetailsResult ? (
                    <dl className="telehealth-details">
                      <div><dt>Member ID</dt><dd>{memberDetailsResult.memberIdMask}</dd></div>
                      <div><dt>Group number</dt><dd>{memberDetailsResult.groupNumberMask ?? 'Not supplied'}</dd></div>
                      <div><dt>Subscriber relationship</dt><dd>{memberDetailsResult.subscriberRelationship}</dd></div>
                      <div><dt>Coverage priority</dt><dd>{memberDetailsResult.coveragePriority}</dd></div>
                    </dl>
                  ) : null}
                  <p>{applicant.nextAction}</p>
                  <p><strong>No raw values are returned. No member match, eligibility, benefits, exact network confirmation, canonical insurance/coverage, price, payment, patient, request, queue, appointment, encounter, or care capability was created.</strong></p>
                  {memberDetailsResult ? <ul>{memberDetailsResult.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul> : null}
                </div>
                <form className="telehealth-form" onSubmit={recordEligibility}>
                  <h3>Run a bounded synthetic eligibility check</h3>
                  <p>The server will open the protected receipt in memory and use a fixed NON_PRODUCTION fixture shaped around a normalized ASC X12N 270/271 eligibility inquiry and response. It will not create or store an X12 transaction and will not contact a payer or clearinghouse.</p>
                  <p><strong>An active result is not exact practice-and-physician network confirmation, a coverage guarantee, a price, or a promise of payment.</strong></p>
                  <label className="telehealth-check">
                    <input
                      required
                      type="checkbox"
                      checked={eligibilitySyntheticConfirmed}
                      onChange={(event) => {
                        pendingEligibility.current = null
                        setEligibilitySyntheticConfirmed(event.target.checked)
                      }}
                    />
                    <span>I understand this check is synthetic, NON_PRODUCTION, and creates no payer transaction or coverage guarantee.</span>
                  </label>
                  <button className="telehealth-button" type="submit" disabled={submitting || !eligibilitySyntheticConfirmed}>
                    {submitting ? 'Checking synthetic eligibility…' : 'Record synthetic eligibility result'}
                  </button>
                </form>
                <div className="telehealth-emergency" role="note">
                  <h3>Emergency direction remains available</h3>
                  <p>If symptoms may be an emergency, call 911 now or go to the nearest emergency department.</p>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </>
            ) : null}
            {applicant.status === 'SyntheticEligibilityRecorded' ? (
              <>
                <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={eligibilityResultRef}>
                  <h3>Synthetic eligibility result recorded</h3>
                  {eligibilityResult ? (
                    <>
                      <p><strong>{eligibilityResult.payerDisplayName} — {eligibilityResult.productDisplayName}</strong></p>
                      <dl className="telehealth-details">
                        <div><dt>Member ID</dt><dd>{eligibilityResult.memberIdMask}</dd></div>
                        <div><dt>Eligibility</dt><dd>{eligibilityResult.eligibilityStatus}</dd></div>
                        <div><dt>Member match</dt><dd>{eligibilityResult.memberMatchStatus}</dd></div>
                        <div><dt>Benefit information</dt><dd>{eligibilityResult.benefitInformationStatus}</dd></div>
                        <div><dt>Business outcome</dt><dd>{eligibilityResult.businessOutcome}</dd></div>
                        <div><dt>Practice precheck</dt><dd>{eligibilityResult.practiceNetworkStatus}</dd></div>
                        <div><dt>Date of service</dt><dd>{eligibilityResult.dateOfService}</dd></div>
                        <div><dt>Result expires</dt><dd>{new Date(eligibilityResult.expiresAt).toLocaleString()}</dd></div>
                      </dl>
                    </>
                  ) : (
                    <p>The normalized result is on the server. This resumed screen intentionally exposes only the coarse applicant state.</p>
                  )}
                  <p>{applicant.nextAction}</p>
                  <p><strong>Eligibility and benefits remain separate from exact practice-and-rendering-physician network status. This result is never a guarantee of coverage or payment.</strong></p>
                  {eligibilityResult ? <ul>{eligibilityResult.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul> : null}
                </div>
                <form className="telehealth-form" onSubmit={recordPracticeNetwork}>
                  <h3>Check the synthetic practice network</h3>
                  <p>The server will use the selected plan plus the configured practice, facility, state, date, and telehealth service in a fixed NON_PRODUCTION provider-directory fixture shaped around HL7 FHIR R4 Da Vinci Plan-Net concepts. No member information is sent to this adapter.</p>
                  <p><strong>No FHIR resource or external directory call is created. A practice result does not check the eventual rendering physician and does not guarantee coverage, payment, capacity, or an appointment.</strong></p>
                  <label className="telehealth-check">
                    <input
                      required
                      type="checkbox"
                      checked={practiceNetworkSyntheticConfirmed}
                      onChange={(event) => {
                        pendingPracticeNetwork.current = null
                        setPracticeNetworkSyntheticConfirmed(event.target.checked)
                      }}
                    />
                    <span>I understand this practice-network check is synthetic, NON_PRODUCTION, and does not check a rendering physician or contact a directory.</span>
                  </label>
                  <button className="telehealth-button" type="submit" disabled={submitting || !practiceNetworkSyntheticConfirmed}>
                    {submitting ? 'Checking synthetic practice network…' : 'Record synthetic practice-network result'}
                  </button>
                </form>
                <div className="telehealth-emergency" role="note">
                  <h3>Emergency direction remains available</h3>
                  <p>If symptoms may be an emergency, call 911 now or go to the nearest emergency department.</p>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </>
            ) : null}
            {applicant.status === 'SyntheticPracticeNetworkRecorded' ? (
              <>
                <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={practiceNetworkResultRef}>
                  <h3>Synthetic practice-network result recorded</h3>
                  {practiceNetworkResult ? (
                    <>
                      <p><strong>{practiceNetworkResult.practiceDisplayName}</strong></p>
                      <p>{practiceNetworkResult.payerDisplayName} — {practiceNetworkResult.productDisplayName}</p>
                      <dl className="telehealth-details">
                        <div><dt>Prior eligibility</dt><dd>{practiceNetworkResult.eligibilityStatus}</dd></div>
                        <div><dt>Directory transport</dt><dd>{practiceNetworkResult.transportOutcome}</dd></div>
                        <div><dt>Practice affiliation</dt><dd>{practiceNetworkResult.practiceAffiliationStatus}</dd></div>
                        <div><dt>Telehealth service</dt><dd>{practiceNetworkResult.serviceAvailabilityStatus}</dd></div>
                        <div><dt>New-patient directory status</dt><dd>{practiceNetworkResult.newPatientAcceptanceStatus}</dd></div>
                        <div><dt>Business outcome</dt><dd>{practiceNetworkResult.businessOutcome}</dd></div>
                        <div><dt>Result expires</dt><dd>{new Date(practiceNetworkResult.expiresAt).toLocaleString()}</dd></div>
                      </dl>
                    </>
                  ) : (
                    <p>The normalized result is on the server. This resumed screen intentionally exposes only the coarse applicant state. Restart this bounded demonstration rather than inferring eligibility for identity proofing.</p>
                  )}
                  <p>{applicant.nextAction}</p>
                  <p><strong>Rendering-physician participation is still unchecked. No exact end-to-end network confirmation, canonical coverage, estimate, payment, patient, request, queue, appointment, encounter, or care capability was created.</strong></p>
                  {practiceNetworkResult ? <ul>{practiceNetworkResult.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul> : null}
                </div>
                {practiceNetworkResult?.eligibilityStatus === 'Active'
                  && practiceNetworkResult.businessOutcome === 'PracticeInNetworkAcceptingNewPatients' ? (
                  <form className="telehealth-form" onSubmit={recordIdentityProofing}>
                    <h3>Exercise the synthetic identity-proofing process</h3>
                    <p>This step records process-shaped fixture statuses for evidence collection, evidence validation, attribute validation, applicant verification, and fraud review. It sends only opaque synthetic references to the internal adapter.</p>
                    <p><strong>It does not collect an ID document, government identifier, image, video, biometric, or raw evidence; contact an authoritative source; establish NIST IAL1/IAL2/IAL3; or create a patient or login.</strong></p>
                    <label className="telehealth-check">
                      <input
                        required
                        type="checkbox"
                        checked={identityPrivacyNoticeAcknowledged}
                        onChange={(event) => {
                          pendingIdentityProofing.current = null
                          setIdentityPrivacyNoticeAcknowledged(event.target.checked)
                        }}
                      />
                      <span>I acknowledge the synthetic privacy notice: only opaque fixture references and normalized process statuses are retained; no real identity evidence is collected.</span>
                    </label>
                    <label className="telehealth-check">
                      <input
                        required
                        type="checkbox"
                        checked={identitySyntheticConfirmed}
                        onChange={(event) => {
                          pendingIdentityProofing.current = null
                          setIdentitySyntheticConfirmed(event.target.checked)
                        }}
                      />
                      <span>I understand this is a NON_PRODUCTION process exercise and does not verify my identity or establish an identity assurance level.</span>
                    </label>
                    <button className="telehealth-button" type="submit" disabled={submitting || !identityPrivacyNoticeAcknowledged || !identitySyntheticConfirmed}>
                      {submitting ? 'Recording synthetic identity process…' : 'Record synthetic identity-proofing fixture'}
                    </button>
                  </form>
                ) : null}
                <div className="telehealth-emergency" role="note">
                  <h3>Emergency direction remains available</h3>
                  <p>If symptoms may be an emergency, call 911 now or go to the nearest emergency department.</p>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </>
            ) : null}
            {applicant.status === 'SyntheticIdentityProofingRecorded' ? (
              <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={identityProofingResultRef}>
                <h3>Synthetic identity-proofing process recorded</h3>
                {identityProofingResult ? (
                  <dl className="telehealth-details">
                    <div><dt>Adapter transport</dt><dd>{identityProofingResult.transportOutcome}</dd></div>
                    <div><dt>Evidence reference</dt><dd>{identityProofingResult.evidenceCollectionStatus}</dd></div>
                    <div><dt>Evidence validation</dt><dd>{identityProofingResult.evidenceValidationStatus}</dd></div>
                    <div><dt>Attribute validation</dt><dd>{identityProofingResult.attributeValidationStatus}</dd></div>
                    <div><dt>Applicant verification</dt><dd>{identityProofingResult.applicantVerificationStatus}</dd></div>
                    <div><dt>Fraud review</dt><dd>{identityProofingResult.fraudCheckStatus}</dd></div>
                    <div><dt>Fixture outcome</dt><dd>{identityProofingResult.businessOutcome}</dd></div>
                    <div><dt>Identity assurance achieved</dt><dd>{identityProofingResult.assuranceLevelAchieved}</dd></div>
                  </dl>
                ) : (
                  <p>The normalized process result is on the server. This resumed screen intentionally exposes only the coarse applicant state.</p>
                )}
                <p>{applicant.nextAction}</p>
                <p><strong>No real identity was proofed. No document, biometric, authoritative-source response, proofing notification, authenticator, patient, chart, account, consent, request, queue entry, appointment, encounter, or care capability was created.</strong></p>
                {identityProofingResult ? <ul>{identityProofingResult.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul> : null}
                <div className="telehealth-emergency" role="note">
                  <h3>Emergency direction remains available</h3>
                  <p>If symptoms may be an emergency, call 911 now or go to the nearest emergency department.</p>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </div>
            ) : null}
            {applicant.status === 'SyntheticPromotionAuthorized' || applicant.status === 'SyntheticPromotionDenied' ? (
              <div className="telehealth-coverage-result" role="status" tabIndex={-1}>
                <h3>Staff synthetic governance decision recorded</h3>
                <p>{applicant.nextAction}</p>
                <p>No patient, chart, portal account, telehealth request, queue entry, or care capability exists.</p>
                <div className="telehealth-emergency-action">
                  <strong>Emergency symptoms now?</strong>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </div>
            ) : null}
            {applicant.status === 'SyntheticPromotionBlockedPossibleMatch' ? (
              <div className="telehealth-coverage-result" role="status" tabIndex={-1}>
                <h3>Synthetic promotion safely blocked</h3>
                <p>{applicant.nextAction}</p>
                <p>No patient was created or linked, and no possible-match details are available in this experience.</p>
              </div>
            ) : null}
            {applicant.status === 'SyntheticPatientPromoted' ? (
              <>
                <div className="telehealth-coverage-result" role="status" tabIndex={-1}>
                  <h3>Synthetic patient shell created</h3>
                  <p>{applicant.nextAction}</p>
                  <p>The shell has no portal, completed intake, consent, coverage, request, queue entry, or care capability.</p>
                </div>
                {noticeLoading ? <p role="status">Loading the state-specific telehealth notice…</p> : null}
                {!noticeLoading && !notice ? (
                  <button className="telehealth-button" type="button" onClick={() => setNoticeLoadAttempt((value) => value + 1)}>
                    Try loading the notice again
                  </button>
                ) : null}
                {notice && !notice.acknowledged ? (
                  <form className="telehealth-form" onSubmit={acknowledgeTelehealthNotice}>
                    <p className="telehealth-kicker">{notice.currentLocationStateCode} notice</p>
                    <h3>{notice.title}</h3>
                    <p>{notice.summary}</p>
                    <p><strong>This acknowledgment is not final legal consent to treatment and does not document the clinician's required consent conversation.</strong></p>
                    <p><a href={notice.sourceUrl} target="_blank" rel="noreferrer">Read the official source: {notice.sourceTitle}</a></p>
                    <h4>What this notice covers</h4>
                    <ul>{notice.disclosures.map((disclosure) => <li key={disclosure}>{disclosure}</li>)}</ul>
                    <h4>What must still happen later</h4>
                    <ul>{notice.deferredRequirements.map((requirement) => <li key={requirement}>{requirement}</li>)}</ul>
                    <fieldset>
                      <legend>Required acknowledgments</legend>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={noticeAcknowledgments.location} onChange={(event) => updateNoticeAcknowledgment('location', event.target.checked)} />
                        <span>I confirm I am currently located in {notice.currentLocationStateCode}.</span>
                      </label>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={noticeAcknowledgments.mode} onChange={(event) => updateNoticeAcknowledgment('mode', event.target.checked)} />
                        <span>I understand telehealth uses remote technology and can have limits compared with an in-person examination.</span>
                      </label>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={noticeAcknowledgments.privacy} onChange={(event) => updateNoticeAcknowledgment('privacy', event.target.checked)} />
                        <span>I understand privacy and technology limitations may apply to a remote visit.</span>
                      </label>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={noticeAcknowledgments.emergency} onChange={(event) => updateNoticeAcknowledgment('emergency', event.target.checked)} />
                        <span>I understand this is not emergency care and I should call 911 for emergency symptoms.</span>
                      </label>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={noticeAcknowledgments.inPerson} onChange={(event) => updateNoticeAcknowledgment('inPerson', event.target.checked)} />
                        <span>I understand an in-person evaluation may be available or clinically required.</span>
                      </label>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={noticeAcknowledgments.clinicianReconfirmation} onChange={(event) => updateNoticeAcknowledgment('clinicianReconfirmation', event.target.checked)} />
                        <span>I understand a licensed clinician must later reconfirm the notice and complete any legally required consent before care begins.</span>
                      </label>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={noticeAcknowledgments.synthetic} onChange={(event) => updateNoticeAcknowledgment('synthetic', event.target.checked)} />
                        <span>I confirm this is a synthetic NON_PRODUCTION demonstration and does not request or authorize care.</span>
                      </label>
                    </fieldset>
                    <button
                      className="telehealth-button"
                      type="submit"
                      disabled={submitting || !Object.values(noticeAcknowledgments).every(Boolean)}
                    >
                      {submitting ? 'Recording acknowledgment…' : 'Acknowledge state-specific notice'}
                    </button>
                  </form>
                ) : null}
                <div className="telehealth-emergency" role="note">
                  <h3>Emergency direction remains available</h3>
                  <p>If symptoms may be an emergency, call 911 now or go to the nearest emergency department.</p>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </>
            ) : null}
            {applicant.status === 'SyntheticTelehealthNoticeAcknowledged' ? (
              <>
                <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={noticeResultRef}>
                  <h3>State-specific telehealth notice acknowledged</h3>
                  {notice ? (
                    <>
                      <p><strong>{notice.title}</strong></p>
                      <dl className="telehealth-details">
                        <div><dt>Current location</dt><dd>{notice.currentLocationStateCode}</dd></div>
                        <div><dt>Notice version</dt><dd>{notice.noticeVersion}</dd></div>
                        <div><dt>Legal review</dt><dd>{notice.legalReviewStatus}</dd></div>
                        <div><dt>Acknowledged</dt><dd>{notice.acknowledgedAt ? new Date(notice.acknowledgedAt).toLocaleString() : 'Recorded'}</dd></div>
                        <div><dt>Final legal consent</dt><dd>Not established</dd></div>
                        <div><dt>Clinician consent documentation</dt><dd>Not completed</dd></div>
                      </dl>
                      <p><a href={notice.sourceUrl} target="_blank" rel="noreferrer">Official source: {notice.sourceTitle}</a></p>
                      <p>{notice.direction}</p>
                      <p><strong>This created no portal access, completed intake, practice acceptance, insurance record, telehealth request, queue entry, or care capability.</strong></p>
                      <ul>{notice.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                    </>
                  ) : <p>Loading the recorded state-specific notice acknowledgment…</p>}
                  <div className="telehealth-emergency-action">
                    <strong>Emergency symptoms now?</strong>
                    <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                  </div>
                </div>
                {registrationDetailsLoading ? <p role="status">Loading the copied minimum registration details…</p> : null}
                {!registrationDetailsLoading && !registrationDetails ? (
                  <button className="telehealth-button" type="button" onClick={() => setRegistrationDetailsLoadAttempt((value) => value + 1)}>
                    Try loading the registration details again
                  </button>
                ) : null}
                {registrationDetails && !registrationDetails.confirmed ? (
                  <form className="telehealth-form" onSubmit={confirmRegistrationDetails}>
                    <p className="telehealth-kicker">No-edit registration review</p>
                    <h3>Confirm the minimum copied details</h3>
                    <p>These fields were copied from the synthetic applicant into the portal-disabled patient shell. Email and phone stay masked.</p>
                    <dl className="telehealth-details">
                      <div><dt>Legal name</dt><dd>{registrationDetails.legalFirstName} {registrationDetails.legalLastName}</dd></div>
                      <div><dt>Date of birth</dt><dd>{registrationDetails.dateOfBirth}</dd></div>
                      <div><dt>Email</dt><dd>{registrationDetails.maskedEmail}</dd></div>
                      <div><dt>Callback phone</dt><dd>{registrationDetails.maskedPhone}</dd></div>
                      <div><dt>Residence region</dt><dd>{registrationDetails.residenceStateCode} {registrationDetails.postalCode}</dd></div>
                    </dl>
                    <div className="telehealth-inline-warning" role="note">
                      <strong>Something is wrong?</strong> Do not confirm. This step cannot edit records or complete a correction. Start again or contact the practice through a separately approved channel.
                    </div>
                    <p><strong>This is not a complete demographic, address, medical-history, consent, or insurance review.</strong></p>
                    <fieldset>
                      <legend>Required confirmations</legend>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={registrationDetailsConfirmations.nameAndBirthDate} onChange={(event) => updateRegistrationDetailsConfirmation('nameAndBirthDate', event.target.checked)} />
                        <span>I confirm the legal name and date of birth shown above are current and correct for this synthetic applicant.</span>
                      </label>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={registrationDetailsConfirmations.contacts} onChange={(event) => updateRegistrationDetailsConfirmation('contacts', event.target.checked)} />
                        <span>I recognize the masked verified email and callback phone as the synthetic contact channels I entered.</span>
                      </label>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={registrationDetailsConfirmations.residence} onChange={(event) => updateRegistrationDetailsConfirmation('residence', event.target.checked)} />
                        <span>I confirm the residence state and postal code shown above are current. I understand this is not my visit-time physical location.</span>
                      </label>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={registrationDetailsConfirmations.noCorrections} onChange={(event) => updateRegistrationDetailsConfirmation('noCorrections', event.target.checked)} />
                        <span>I do not need a correction to any displayed minimum registration detail.</span>
                      </label>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={registrationDetailsConfirmations.synthetic} onChange={(event) => updateRegistrationDetailsConfirmation('synthetic', event.target.checked)} />
                        <span>I confirm this is a synthetic NON_PRODUCTION demonstration and does not establish identity or request care.</span>
                      </label>
                    </fieldset>
                    <button
                      className="telehealth-button"
                      type="submit"
                      disabled={submitting || !Object.values(registrationDetailsConfirmations).every(Boolean)}
                    >
                      {submitting ? 'Recording confirmation…' : 'Confirm minimum registration details'}
                    </button>
                  </form>
                ) : null}
              </>
            ) : null}
            {applicant.status === 'SyntheticMinimumRegistrationDetailsConfirmed' ? (
              <>
                <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={registrationDetailsResultRef}>
                  <h3>Minimum registration details confirmed</h3>
                  {registrationDetails ? (
                    <>
                      <dl className="telehealth-details">
                        <div><dt>Legal name</dt><dd>{registrationDetails.legalFirstName} {registrationDetails.legalLastName}</dd></div>
                        <div><dt>Date of birth</dt><dd>{registrationDetails.dateOfBirth}</dd></div>
                        <div><dt>Verified contacts</dt><dd>{registrationDetails.maskedEmail}; {registrationDetails.maskedPhone}</dd></div>
                        <div><dt>Residence region</dt><dd>{registrationDetails.residenceStateCode} {registrationDetails.postalCode}</dd></div>
                        <div><dt>Patient record changed</dt><dd>No</dd></div>
                        <div><dt>Identity assurance established</dt><dd>No</dd></div>
                        <div><dt>Canonical insurance created</dt><dd>No</dd></div>
                        <div><dt>Confirmed</dt><dd>{registrationDetails.confirmedAt ? new Date(registrationDetails.confirmedAt).toLocaleString() : 'Recorded'}</dd></div>
                      </dl>
                      <p>{registrationDetails.direction}</p>
                      <p><strong>No portal, correction, completed intake, legal consent, practice acceptance, coverage, request, queue entry, appointment, encounter, or care capability was created.</strong></p>
                    </>
                  ) : <p>Loading the recorded minimum registration-details confirmation…</p>}
                </div>
                {insuranceHandoffLoading ? <p role="status">Loading the masked synthetic insurance handoff…</p> : null}
                {!insuranceHandoffLoading && !insuranceHandoff ? (
                  <button className="telehealth-button" type="button" onClick={() => setInsuranceHandoffLoadAttempt((value) => value + 1)}>
                    Try loading the insurance handoff again
                  </button>
                ) : null}
                {insuranceHandoff && !insuranceHandoff.insuranceDetailsConfirmed ? (
                  <form className="telehealth-form" onSubmit={confirmInsuranceHandoff}>
                    <p className="telehealth-kicker">No-edit insurance handoff</p>
                    <h3>Confirm the masked synthetic insurance details</h3>
                    <p>This review is copied from the earlier protected synthetic submission. Raw member and group values remain hidden.</p>
                    <dl className="telehealth-details">
                      <div><dt>Payer</dt><dd>{insuranceHandoff.payerDisplayName}</dd></div>
                      <div><dt>Product</dt><dd>{insuranceHandoff.productDisplayName}</dd></div>
                      <div><dt>Member ID</dt><dd>{insuranceHandoff.memberIdMask}</dd></div>
                      <div><dt>Group number</dt><dd>{insuranceHandoff.groupNumberMask ?? 'Not supplied'}</dd></div>
                      <div><dt>Subscriber relationship</dt><dd>{insuranceHandoff.subscriberRelationship}</dd></div>
                      <div><dt>Coverage priority</dt><dd>{insuranceHandoff.coveragePriority}</dd></div>
                      <div><dt>Synthetic eligibility fixture</dt><dd>{insuranceHandoff.eligibilityBusinessOutcome}</dd></div>
                      <div><dt>Eligibility evidence</dt><dd>{insuranceHandoff.eligibilityEvidenceCurrent ? 'Current for this confirmation' : 'Expired — cannot confirm'}</dd></div>
                      <div><dt>Practice-level network fixture</dt><dd>{insuranceHandoff.practiceNetworkBusinessOutcome}</dd></div>
                      <div><dt>Practice-network evidence</dt><dd>{insuranceHandoff.practiceNetworkEvidenceCurrent ? 'Current for this confirmation' : 'Expired — cannot confirm'}</dd></div>
                      <div><dt>Rendering physician checked</dt><dd>No</dd></div>
                      <div><dt>Coverage verified</dt><dd>No</dd></div>
                    </dl>
                    <div className="telehealth-inline-warning" role="note">
                      <strong>Something is wrong?</strong> Do not confirm. This step cannot edit insurance data or create coverage. Start again or contact the practice through a separately approved channel.
                    </div>
                    <p><strong>The fixture results are not guarantees of coverage, benefits, payment, cost, or rendering-physician participation.</strong></p>
                    <fieldset>
                      <legend>Required confirmations</legend>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={insuranceHandoffConfirmations.payerAndProduct} onChange={(event) => updateInsuranceHandoffConfirmation('payerAndProduct', event.target.checked)} />
                        <span>I confirm the synthetic payer and product shown above are the ones I selected.</span>
                      </label>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={insuranceHandoffConfirmations.memberDetails} onChange={(event) => updateInsuranceHandoffConfirmation('memberDetails', event.target.checked)} />
                        <span>I recognize the masked synthetic member ID and group number as the details I entered.</span>
                      </label>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={insuranceHandoffConfirmations.subscriberRelationship} onChange={(event) => updateInsuranceHandoffConfirmation('subscriberRelationship', event.target.checked)} />
                        <span>I confirm the subscriber relationship and coverage priority shown above.</span>
                      </label>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={insuranceHandoffConfirmations.evidenceLimitations} onChange={(event) => updateInsuranceHandoffConfirmation('evidenceLimitations', event.target.checked)} />
                        <span>I understand the eligibility and practice-level network fixtures are not coverage or payment guarantees and did not check a rendering physician.</span>
                      </label>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={insuranceHandoffConfirmations.synthetic} onChange={(event) => updateInsuranceHandoffConfirmation('synthetic', event.target.checked)} />
                        <span>I confirm this is a synthetic NON_PRODUCTION demonstration and does not create insurance coverage or request care.</span>
                      </label>
                    </fieldset>
                    <button
                      className="telehealth-button"
                      type="submit"
                      disabled={submitting
                        || !insuranceHandoff.eligibilityEvidenceCurrent
                        || !insuranceHandoff.practiceNetworkEvidenceCurrent
                        || !Object.values(insuranceHandoffConfirmations).every(Boolean)}
                    >
                      {submitting ? 'Recording confirmation…' : 'Confirm synthetic insurance details'}
                    </button>
                  </form>
                ) : null}
                <div className="telehealth-emergency-action">
                  <strong>Emergency symptoms now?</strong>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </>
            ) : null}
            {applicant.status === 'SyntheticInsuranceDetailsConfirmed' ? (
              <>
                <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={insuranceHandoffResultRef}>
                  <h3>Synthetic insurance details confirmed</h3>
                  {insuranceHandoff ? (
                    <>
                      <dl className="telehealth-details">
                        <div><dt>Payer and product</dt><dd>{insuranceHandoff.payerDisplayName} — {insuranceHandoff.productDisplayName}</dd></div>
                        <div><dt>Member ID</dt><dd>{insuranceHandoff.memberIdMask}</dd></div>
                        <div><dt>Group number</dt><dd>{insuranceHandoff.groupNumberMask ?? 'Not supplied'}</dd></div>
                        <div><dt>Subscriber relationship</dt><dd>{insuranceHandoff.subscriberRelationship}</dd></div>
                        <div><dt>Rendering physician checked</dt><dd>No</dd></div>
                        <div><dt>Coverage verified</dt><dd>No</dd></div>
                        <div><dt>Exact network confirmed</dt><dd>No</dd></div>
                        <div><dt>Canonical coverage created</dt><dd>No</dd></div>
                        <div><dt>Confirmed</dt><dd>{insuranceHandoff.confirmedAt ? new Date(insuranceHandoff.confirmedAt).toLocaleString() : 'Recorded'}</dd></div>
                      </dl>
                      <p>{insuranceHandoff.direction}</p>
                      <p><strong>No patient change, portal, completed intake, legal consent, practice acceptance, financial record, request, queue entry, appointment, encounter, claim, or care capability was created.</strong></p>
                      <ul>{insuranceHandoff.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                    </>
                  ) : <p>Loading the recorded synthetic insurance-details confirmation…</p>}
                </div>
                {communicationAccessLoading ? <p role="status">Loading communication and access readiness…</p> : null}
                {!communicationAccessLoading && !communicationAccessReadiness ? (
                  <button className="telehealth-button" type="button" onClick={() => setCommunicationAccessLoadAttempt((value) => value + 1)}>
                    Try loading communication readiness again
                  </button>
                ) : null}
                {communicationAccessReadiness && !communicationAccessReadiness.readinessRecorded ? (
                  <form className="telehealth-form" onSubmit={recordCommunicationAccessReadiness}>
                    <p className="telehealth-kicker">Communication and access readiness</p>
                    <h3>Confirm how this synthetic visit could communicate</h3>
                    <p>This records preferences and acknowledgments only. It does not arrange an interpreter, accessibility accommodation, or connection.</p>
                    <dl className="telehealth-details">
                      <div><dt>Current location</dt><dd>{communicationAccessReadiness.currentLocationStateCode}</dd></div>
                      <div><dt>Callback number</dt><dd>{communicationAccessReadiness.maskedCallbackPhone}</dd></div>
                      <div><dt>Interpreter assigned</dt><dd>No</dd></div>
                      <div><dt>Accessibility accommodation arranged</dt><dd>No</dd></div>
                      <div><dt>Technology readiness completed</dt><dd>No</dd></div>
                    </dl>
                    <div className="telehealth-inline-warning" role="note">
                      <strong>Location or callback is wrong, or you cannot speak safely and privately?</strong> Do not continue. This step cannot correct patient details or contact the practice.
                    </div>
                    <label htmlFor="communication-language">Preferred spoken language</label>
                    <select
                      id="communication-language"
                      required
                      value={preferredSpokenLanguage}
                      onChange={(event) => {
                        resetPendingCommunicationAccessReadiness()
                        setPreferredSpokenLanguage(event.target.value as '' | 'English' | 'Spanish')
                      }}
                    >
                      <option value="">Select a supported language</option>
                      {communicationAccessReadiness.supportedSpokenLanguages.map((language) => (
                        <option key={language} value={language}>{language}</option>
                      ))}
                    </select>
                    <fieldset>
                      <legend>Support preferences</legend>
                      <label className="telehealth-check">
                        <input type="checkbox" checked={interpreterRequested} onChange={(event) => {
                          resetPendingCommunicationAccessReadiness()
                          setInterpreterRequested(event.target.checked)
                        }} />
                        <span>I would request an interpreter. This does not assign or arrange one.</span>
                      </label>
                      <label className="telehealth-check">
                        <input type="checkbox" checked={accessibilitySupportRequested} onChange={(event) => {
                          resetPendingCommunicationAccessReadiness()
                          setAccessibilitySupportRequested(event.target.checked)
                        }} />
                        <span>I would request accessibility support. This does not arrange an accommodation.</span>
                      </label>
                    </fieldset>
                    <fieldset>
                      <legend>Required confirmations</legend>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={communicationAccessConfirmations.currentLocation} onChange={(event) => updateCommunicationAccessConfirmation('currentLocation', event.target.checked)} />
                        <span>I confirm I am currently in {communicationAccessReadiness.currentLocationStateCode}.</span>
                      </label>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={communicationAccessConfirmations.callbackNumber} onChange={(event) => updateCommunicationAccessConfirmation('callbackNumber', event.target.checked)} />
                        <span>I confirm {communicationAccessReadiness.maskedCallbackPhone} is the callback number I expect this demonstration to use.</span>
                      </label>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={communicationAccessConfirmations.safeAndPrivate} onChange={(event) => updateCommunicationAccessConfirmation('safeAndPrivate', event.target.checked)} />
                        <span>I can communicate safely and privately at this time.</span>
                      </label>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={communicationAccessConfirmations.disconnectionAndEmergencyPlan} onChange={(event) => updateCommunicationAccessConfirmation('disconnectionAndEmergencyPlan', event.target.checked)} />
                        <span>I understand a real visit needs a disconnection plan and that I should call 911 for an emergency.</span>
                      </label>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={communicationAccessConfirmations.synthetic} onChange={(event) => updateCommunicationAccessConfirmation('synthetic', event.target.checked)} />
                        <span>I confirm this is a synthetic NON_PRODUCTION receipt and does not arrange support or request care.</span>
                      </label>
                    </fieldset>
                    <button
                      className="telehealth-button"
                      type="submit"
                      disabled={submitting || !preferredSpokenLanguage || !Object.values(communicationAccessConfirmations).every(Boolean)}
                    >
                      {submitting ? 'Recording readiness…' : 'Record communication readiness'}
                    </button>
                  </form>
                ) : null}
                <div className="telehealth-emergency-action">
                  <strong>Emergency symptoms now?</strong>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </>
            ) : null}
            {applicant.status === 'SyntheticCommunicationAccessReadinessRecorded' ? (
              <>
                <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={communicationAccessResultRef}>
                  <h3>Communication and access readiness recorded</h3>
                  {communicationAccessReadiness ? (
                    <>
                      <dl className="telehealth-details">
                        <div><dt>Current location</dt><dd>{communicationAccessReadiness.currentLocationStateCode}</dd></div>
                        <div><dt>Callback number</dt><dd>{communicationAccessReadiness.maskedCallbackPhone}</dd></div>
                        <div><dt>Preferred spoken language</dt><dd>{communicationAccessReadiness.preferredSpokenLanguage}</dd></div>
                        <div><dt>Interpreter requested</dt><dd>{communicationAccessReadiness.interpreterRequested ? 'Yes — preference only' : 'No'}</dd></div>
                        <div><dt>Interpreter assigned</dt><dd>No</dd></div>
                        <div><dt>Accessibility support requested</dt><dd>{communicationAccessReadiness.accessibilitySupportRequested ? 'Yes — preference only' : 'No'}</dd></div>
                        <div><dt>Accommodation arranged</dt><dd>No</dd></div>
                        <div><dt>Technology readiness completed</dt><dd>No</dd></div>
                        <div><dt>Recorded</dt><dd>{communicationAccessReadiness.recordedAt ? new Date(communicationAccessReadiness.recordedAt).toLocaleString() : 'Recorded'}</dd></div>
                      </dl>
                      <p>{communicationAccessReadiness.direction}</p>
                      <p><strong>No interpreter, accommodation, communication session, support request, patient change, completed intake, legal consent, practice acceptance, request, queue entry, appointment, encounter, or care capability was created.</strong></p>
                      <ul>{communicationAccessReadiness.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                    </>
                  ) : <p>Loading the recorded communication and access readiness receipt…</p>}
                </div>
                {devicePreparationLoading ? <p role="status">Loading device preparation…</p> : null}
                {!devicePreparationLoading && !devicePreparation ? (
                  <button className="telehealth-button" type="button" onClick={() => setDevicePreparationLoadAttempt((value) => value + 1)}>
                    Try loading device preparation again
                  </button>
                ) : null}
                {devicePreparation && !devicePreparation.preparationRecorded ? (
                  <form className="telehealth-form" onSubmit={recordDevicePreparation}>
                    <p className="telehealth-kicker">Local device preparation</p>
                    <h3>Check this browser, camera, microphone, speaker, and connection</h3>
                    <p>The check briefly requests camera and microphone access. Every temporary test track is stopped immediately. No image, audio, recording, device name, device ID, browser details, IP address, or connection diagnostic is sent or stored.</p>
                    <button className="telehealth-button telehealth-button-secondary" type="button" disabled={deviceCheckRunning || submitting} onClick={runDeviceCheck}>
                      {deviceCheckRunning ? 'Running local device check…' : 'Run local device check'}
                    </button>
                    {deviceCheckResult?.status === 'failed' ? (
                      <div className="telehealth-inline-warning" role="alert">
                        <strong>Device check did not pass.</strong> {deviceCheckResult.message}
                      </div>
                    ) : null}
                    {deviceCheckResult?.status === 'passed' ? (
                      <div className="telehealth-coverage-result" role="status">
                        <h4>Local check complete</h4>
                        <dl className="telehealth-details">
                          <div><dt>Supported browser</dt><dd>Yes</dd></div>
                          <div><dt>Camera available</dt><dd>Yes</dd></div>
                          <div><dt>Microphone available</dt><dd>Yes</dd></div>
                          <div><dt>Speaker available</dt><dd>Yes</dd></div>
                          <div><dt>Connection indication</dt><dd>{deviceCheckResult.evidence.networkQuality === 'limited' ? 'Limited' : deviceCheckResult.evidence.networkQuality === 'good' ? 'Good' : 'Unknown'}</dd></div>
                          <div><dt>Temporary media tracks</dt><dd>Stopped</dd></div>
                        </dl>
                        {deviceCheckResult.evidence.networkQuality === 'limited' ? (
                          <p className="telehealth-inline-warning"><strong>A stronger connection is required.</strong> Try a different connection and run the check again. Do not continue with this result.</p>
                        ) : (
                          <p>This point-in-time check passed. It is preparation only and does not guarantee that a future call will work.</p>
                        )}
                      </div>
                    ) : null}
                    <fieldset>
                      <legend>Required acknowledgments</legend>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={devicePreparationAcknowledgments.clientReported} onChange={(event) => updateDevicePreparationAcknowledgment('clientReported', event.target.checked)} />
                        <span>I understand this is a client-reported, point-in-time browser result.</span>
                      </label>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={devicePreparationAcknowledgments.noReadinessGuarantee} onChange={(event) => updateDevicePreparationAcknowledgment('noReadinessGuarantee', event.target.checked)} />
                        <span>I understand this does not certify technology readiness or guarantee that a future call will work.</span>
                      </label>
                      <label className="telehealth-check">
                        <input required type="checkbox" checked={devicePreparationAcknowledgments.recheckBeforeConsultation} onChange={(event) => updateDevicePreparationAcknowledgment('recheckBeforeConsultation', event.target.checked)} />
                        <span>I understand device and connection readiness must be checked again before any consultation.</span>
                      </label>
                    </fieldset>
                    <button
                      className="telehealth-button"
                      type="submit"
                      disabled={submitting
                        || deviceCheckResult?.status !== 'passed'
                        || deviceCheckResult.evidence.networkQuality === 'limited'
                        || !Object.values(devicePreparationAcknowledgments).every(Boolean)}
                    >
                      {submitting ? 'Recording device preparation…' : 'Record device preparation'}
                    </button>
                  </form>
                ) : null}
                <div className="telehealth-emergency-action">
                  <strong>Emergency symptoms now?</strong>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </>
            ) : null}
            {applicant.status === 'SyntheticDevicePreparationRecorded' ? (
              <>
                <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={devicePreparationResultRef}>
                  <h3>Device preparation recorded</h3>
                  {devicePreparation ? (
                    <>
                      <dl className="telehealth-details">
                        <div><dt>Supported browser reported</dt><dd>{devicePreparation.browserSupported ? 'Yes' : 'No'}</dd></div>
                        <div><dt>Camera reported</dt><dd>{devicePreparation.cameraAvailable ? 'Yes' : 'No'}</dd></div>
                        <div><dt>Microphone reported</dt><dd>{devicePreparation.microphoneAvailable ? 'Yes' : 'No'}</dd></div>
                        <div><dt>Speaker reported</dt><dd>{devicePreparation.speakerAvailable ? 'Yes' : 'No'}</dd></div>
                        <div><dt>Connection indication</dt><dd>{devicePreparation.networkQuality ?? 'Not recorded'}</dd></div>
                        <div><dt>Technology ready</dt><dd>No</dd></div>
                        <div><dt>Waiting room created</dt><dd>No</dd></div>
                        <div><dt>Media session created</dt><dd>No</dd></div>
                        <div><dt>Recorded</dt><dd>{devicePreparation.recordedAt ? new Date(devicePreparation.recordedAt).toLocaleString() : 'Recorded'}</dd></div>
                      </dl>
                      <p>{devicePreparation.direction}</p>
                      <p><strong>No technology-ready status, waiting room, media session, communication, support arrangement, patient change, completed intake, legal consent, practice acceptance, request, queue entry, appointment, encounter, or care capability was created.</strong></p>
                      <ul>{devicePreparation.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                    </>
                  ) : <p>Loading the recorded synthetic device-preparation receipt…</p>}
                </div>
                {clinicalInformationInventoryLoading ? <p role="status">Loading the clinical-information inventory…</p> : null}
                {!clinicalInformationInventoryLoading && !clinicalInformationInventory ? (
                  <button className="telehealth-button" type="button" onClick={() => setClinicalInformationInventoryLoadAttempt((value) => value + 1)}>
                    Try loading the clinical-information inventory again
                  </button>
                ) : null}
                {clinicalInformationInventory && !clinicalInformationInventory.inventoryRecorded ? (
                  <form className="telehealth-form" onSubmit={recordClinicalInformationInventory}>
                    <p className="telehealth-kicker">Coarse clinical-information inventory</p>
                    <h3>Tell us whether each category has items that need review</h3>
                    <div className="telehealth-synthetic" role="note">
                      Do not enter any medication, substance, reaction, dose, diagnosis, symptom, procedure, narrative, date, identifier, or other clinical detail. This step collects category signals only.
                    </div>
                    <fieldset>
                      <legend>Medications</legend>
                      <label className="telehealth-check"><input required type="radio" name="medications-inventory" value="PatientReportsNone" checked={medicationsInventoryStatus === 'PatientReportsNone'} onChange={() => updateClinicalInformationInventoryStatus('medications', 'PatientReportsNone')} /><span>I report no medications. This is not a clinically reconciled “no known medications” finding.</span></label>
                      <label className="telehealth-check"><input required type="radio" name="medications-inventory" value="ItemsToReview" checked={medicationsInventoryStatus === 'ItemsToReview'} onChange={() => updateClinicalInformationInventoryStatus('medications', 'ItemsToReview')} /><span>I have medication items that need detailed review.</span></label>
                      <label className="telehealth-check"><input required type="radio" name="medications-inventory" value="Unsure" checked={medicationsInventoryStatus === 'Unsure'} onChange={() => updateClinicalInformationInventoryStatus('medications', 'Unsure')} /><span>I am unsure.</span></label>
                    </fieldset>
                    <fieldset>
                      <legend>Allergies or intolerances</legend>
                      <label className="telehealth-check"><input required type="radio" name="allergies-inventory" value="PatientReportsNone" checked={allergiesInventoryStatus === 'PatientReportsNone'} onChange={() => updateClinicalInformationInventoryStatus('allergies', 'PatientReportsNone')} /><span>I report no allergies or intolerances. This is not a clinically reconciled “no known allergies” finding.</span></label>
                      <label className="telehealth-check"><input required type="radio" name="allergies-inventory" value="ItemsToReview" checked={allergiesInventoryStatus === 'ItemsToReview'} onChange={() => updateClinicalInformationInventoryStatus('allergies', 'ItemsToReview')} /><span>I have allergy or intolerance items that need detailed review.</span></label>
                      <label className="telehealth-check"><input required type="radio" name="allergies-inventory" value="Unsure" checked={allergiesInventoryStatus === 'Unsure'} onChange={() => updateClinicalInformationInventoryStatus('allergies', 'Unsure')} /><span>I am unsure.</span></label>
                    </fieldset>
                    <fieldset>
                      <legend>Other health history</legend>
                      <label className="telehealth-check"><input required type="radio" name="history-inventory" value="PatientReportsNone" checked={otherHealthHistoryInventoryStatus === 'PatientReportsNone'} onChange={() => updateClinicalInformationInventoryStatus('history', 'PatientReportsNone')} /><span>I report no other health-history items. This is not a clinically reconciled finding.</span></label>
                      <label className="telehealth-check"><input required type="radio" name="history-inventory" value="ItemsToReview" checked={otherHealthHistoryInventoryStatus === 'ItemsToReview'} onChange={() => updateClinicalInformationInventoryStatus('history', 'ItemsToReview')} /><span>I have other health-history items that need detailed review.</span></label>
                      <label className="telehealth-check"><input required type="radio" name="history-inventory" value="Unsure" checked={otherHealthHistoryInventoryStatus === 'Unsure'} onChange={() => updateClinicalInformationInventoryStatus('history', 'Unsure')} /><span>I am unsure.</span></label>
                    </fieldset>
                    <fieldset>
                      <legend>Required acknowledgments</legend>
                      <label className="telehealth-check"><input required type="checkbox" checked={clinicalInformationInventoryAcknowledgments.patientReportedMayBeIncomplete} onChange={(event) => updateClinicalInformationInventoryAcknowledgment('patientReportedMayBeIncomplete', event.target.checked)} /><span>I understand these are patient-reported signals and may be incomplete.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={clinicalInformationInventoryAcknowledgments.noClinicalDetailsCaptured} onChange={(event) => updateClinicalInformationInventoryAcknowledgment('noClinicalDetailsCaptured', event.target.checked)} /><span>I understand this step captures no detailed clinical information.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={clinicalInformationInventoryAcknowledgments.clinicianReconciliationRequired} onChange={(event) => updateClinicalInformationInventoryAcknowledgment('clinicianReconciliationRequired', event.target.checked)} /><span>I understand a clinician must reconcile medications, allergies or intolerances, and health history before care or prescribing.</span></label>
                    </fieldset>
                    <button
                      className="telehealth-button"
                      type="submit"
                      disabled={submitting
                        || !medicationsInventoryStatus
                        || !allergiesInventoryStatus
                        || !otherHealthHistoryInventoryStatus
                        || !Object.values(clinicalInformationInventoryAcknowledgments).every(Boolean)}
                    >
                      {submitting ? 'Recording clinical-information inventory…' : 'Record clinical-information inventory'}
                    </button>
                  </form>
                ) : null}
                <div className="telehealth-emergency-action">
                  <strong>Emergency symptoms now?</strong>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </>
            ) : null}
            {applicant.status === 'SyntheticClinicalInformationInventoryRecorded' ? (
              <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={clinicalInformationInventoryResultRef}>
                <h3>Clinical-information inventory recorded</h3>
                {clinicalInformationInventory ? (
                  <>
                    <dl className="telehealth-details">
                      <div><dt>Medications</dt><dd>{clinicalInformationStatusLabel(clinicalInformationInventory.medicationsStatus)}</dd></div>
                      <div><dt>Allergies or intolerances</dt><dd>{clinicalInformationStatusLabel(clinicalInformationInventory.allergiesOrIntolerancesStatus)}</dd></div>
                      <div><dt>Other health history</dt><dd>{clinicalInformationStatusLabel(clinicalInformationInventory.otherHealthHistoryStatus)}</dd></div>
                      <div><dt>Server review route</dt><dd>{clinicalInformationInventory.reviewRoute ?? 'Not recorded'}</dd></div>
                      <div><dt>Medication list reconciled</dt><dd>No</dd></div>
                      <div><dt>Allergy list reconciled</dt><dd>No</dd></div>
                      <div><dt>Health history reconciled</dt><dd>No</dd></div>
                      <div><dt>Clinician review created</dt><dd>No</dd></div>
                      <div><dt>Clinical intake completed</dt><dd>No</dd></div>
                      <div><dt>Clinical eligibility established</dt><dd>No</dd></div>
                      <div><dt>Prescribing enabled</dt><dd>No</dd></div>
                      <div><dt>Recorded</dt><dd>{clinicalInformationInventory.recordedAt ? new Date(clinicalInformationInventory.recordedAt).toLocaleString() : 'Recorded'}</dd></div>
                    </dl>
                    <p>{clinicalInformationInventory.direction}</p>
                    <p><strong>No clinical details, reconciled chart list, clinician review task, patient-record change, completed intake, eligibility decision, request, queue entry, prescribing, or care capability was created.</strong></p>
                    <ul>{clinicalInformationInventory.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                  </>
                ) : <p>Loading the recorded synthetic clinical-information inventory…</p>}
                <div className="telehealth-emergency-action">
                  <strong>Emergency symptoms now?</strong>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </div>
            ) : null}
            {applicant.status === 'SyntheticClinicalInformationInventoryRecorded' ? (
              <>
                {medicationInformationLoading ? <p role="status">Loading the medication-information step…</p> : null}
                {!medicationInformationLoading && !medicationInformation ? (
                  <button className="telehealth-button" type="button" onClick={() => setMedicationInformationLoadAttempt((value) => value + 1)}>
                    Try loading medication information again
                  </button>
                ) : null}
                {medicationInformation && !medicationInformation.medicationInformationRecorded ? (
                  <form className="telehealth-form" onSubmit={recordMedicationInformation}>
                    <p className="telehealth-kicker">Patient-reported medication information</p>
                    <h3>Confirm the medication category</h3>
                    <div className="telehealth-synthetic" role="note">
                      This fixed local synthetic ingredient catalog is incomplete. Do not enter real health information. This step has no dose, directions, route, frequency, indication, prescriber, pharmacy, dates, notes, attachments, or free text.
                    </div>
                    {medicationInformation.inventoryMedicationsStatus === 'ItemsToReview' ? (
                      <fieldset>
                        <legend>Select each listed ingredient that applies and its patient-reported use status</legend>
                        {medicationInformation.catalogItems.map((item) => {
                          const selectedStatus = reportedMedicationUseStatuses[item.catalogKey] ?? ''
                          return (
                            <div className="telehealth-check" key={item.catalogKey}>
                              <label>
                                <input
                                  type="checkbox"
                                  checked={Boolean(selectedStatus)}
                                  onChange={(event) => updateReportedMedicationUseStatus(item.catalogKey, event.target.checked ? 'Taking' : '')}
                                />
                                <span>{item.displayName}</span>
                              </label>
                              {selectedStatus ? (
                                <label>
                                  <span>Reported use status for {item.displayName}</span>
                                  <select
                                    required
                                    value={selectedStatus}
                                    onChange={(event) => updateReportedMedicationUseStatus(item.catalogKey, event.target.value as TelehealthApplicantReportedMedicationUseStatus)}
                                  >
                                    <option value="Taking">Taking</option>
                                    <option value="NotTaking">Not taking</option>
                                    <option value="Unsure">Unsure</option>
                                  </select>
                                </label>
                              ) : null}
                            </div>
                          )
                        })}
                        <label className="telehealth-check">
                          <input
                            type="checkbox"
                            checked={additionalOrUnlistedMedicationReported}
                            onChange={(event) => {
                              pendingMedicationInformation.current = null
                              setAdditionalOrUnlistedMedicationReported(event.target.checked)
                            }}
                          />
                          <span>I have additional or unlisted medication items that this incomplete catalog cannot capture.</span>
                        </label>
                      </fieldset>
                    ) : (
                      <p>
                        {medicationInformation.inventoryMedicationsStatus === 'Unsure'
                          ? 'Your prior patient-reported medication category remains “unsure.” No ingredient selection is accepted in this branch.'
                          : 'Your prior patient report was “none.” This only records that provisional report and is not a clinician-reconciled no-known-medications finding.'}
                      </p>
                    )}
                    <fieldset>
                      <legend>Required acknowledgments</legend>
                      <label className="telehealth-check"><input required type="checkbox" checked={medicationInformationAcknowledgments.patientReportedMayBeIncomplete} onChange={(event) => updateMedicationInformationAcknowledgment('patientReportedMayBeIncomplete', event.target.checked)} /><span>I understand patient-reported medication information may be incomplete.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={medicationInformationAcknowledgments.syntheticCatalogIncomplete} onChange={(event) => updateMedicationInformationAcknowledgment('syntheticCatalogIncomplete', event.target.checked)} /><span>I understand this synthetic local ingredient catalog is incomplete and has no RxNorm, NDC, or SNOMED CT mapping claim.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={medicationInformationAcknowledgments.noDoseOrDirectionsCaptured} onChange={(event) => updateMedicationInformationAcknowledgment('noDoseOrDirectionsCaptured', event.target.checked)} /><span>I understand no dose, directions, or other medication detail is collected.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={medicationInformationAcknowledgments.clinicianReconciliationRequired} onChange={(event) => updateMedicationInformationAcknowledgment('clinicianReconciliationRequired', event.target.checked)} /><span>I understand a clinician must reconcile medication information before care or prescribing.</span></label>
                    </fieldset>
                    <button
                      className="telehealth-button"
                      type="submit"
                      disabled={submitting
                        || !Object.values(medicationInformationAcknowledgments).every(Boolean)
                        || (medicationInformation.inventoryMedicationsStatus === 'ItemsToReview'
                          && !Object.values(reportedMedicationUseStatuses).some(Boolean)
                          && !additionalOrUnlistedMedicationReported)}
                    >
                      {submitting ? 'Recording medication information…' : 'Record medication information'}
                    </button>
                  </form>
                ) : null}
                <div className="telehealth-emergency-action">
                  <strong>Emergency symptoms now?</strong>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </>
            ) : null}
            {applicant.status === 'SyntheticMedicationInformationRecorded' ? (
              <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={medicationInformationResultRef}>
                <h3>Medication information recorded</h3>
                {medicationInformation ? (
                  <>
                    <dl className="telehealth-details">
                      <div><dt>Prior medication category</dt><dd>{clinicalInformationStatusLabel(medicationInformation.inventoryMedicationsStatus)}</dd></div>
                      <div><dt>Selected synthetic ingredients</dt><dd>{medicationInformation.medicationItems.length}</dd></div>
                      <div><dt>Additional or unlisted items reported</dt><dd>{medicationInformation.additionalOrUnlistedItemsReported ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Server review route</dt><dd>{medicationInformation.reviewRoute ?? 'Not recorded'}</dd></div>
                      <div><dt>MedicationStatement created</dt><dd>No</dd></div>
                      <div><dt>MedicationRequest created</dt><dd>No</dd></div>
                      <div><dt>Medication list reconciled</dt><dd>No</dd></div>
                      <div><dt>Interaction check performed</dt><dd>No</dd></div>
                      <div><dt>Clinician review task created</dt><dd>No</dd></div>
                      <div><dt>Prescribing enabled</dt><dd>No</dd></div>
                      <div><dt>Recorded</dt><dd>{medicationInformation.recordedAt ? new Date(medicationInformation.recordedAt).toLocaleString() : 'Recorded'}</dd></div>
                    </dl>
                    {medicationInformation.medicationItems.length ? (
                      <ul>{medicationInformation.medicationItems.map((item) => <li key={item.catalogKey}>{item.displayName}: {item.reportedUseStatus}</li>)}</ul>
                    ) : <p>No catalog ingredients were recorded.</p>}
                    <p>{medicationInformation.direction}</p>
                    <p><strong>No canonical medication resource or list, reconciliation, interaction check, clinician task, patient-record change, completed intake, eligibility decision, request, queue entry, prescribing, or care capability was created.</strong></p>
                    <ul>{medicationInformation.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                  </>
                ) : <p>Loading the recorded synthetic medication-information receipt…</p>}
                <div className="telehealth-emergency-action">
                  <strong>Emergency symptoms now?</strong>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </div>
            ) : null}
            {applicant.status === 'SyntheticMedicationInformationRecorded' ? (
              <>
                {allergyInformationLoading ? <p role="status">Loading the allergy-information step…</p> : null}
                {!allergyInformationLoading && !allergyInformation ? (
                  <button className="telehealth-button" type="button" onClick={() => setAllergyInformationLoadAttempt((value) => value + 1)}>
                    Try loading allergy information again
                  </button>
                ) : null}
                {allergyInformation && !allergyInformation.allergyInformationRecorded ? (
                  <form className="telehealth-form" onSubmit={recordAllergyInformation}>
                    <p className="telehealth-kicker">Patient-reported allergy or intolerance information</p>
                    <h3>Confirm the allergy or intolerance category</h3>
                    <div className="telehealth-synthetic" role="note">
                      This fixed local synthetic substance catalog is incomplete. Do not enter real health information. This step has no reaction, manifestation, type, clinical or verification status, severity, criticality, onset, dates, notes, attachments, or free text.
                    </div>
                    {allergyInformation.inventoryAllergiesOrIntolerancesStatus === 'ItemsToReview' ? (
                      <fieldset>
                        <legend>Select each listed substance that applies to the prior patient report</legend>
                        {allergyInformation.catalogItems.map((item) => (
                          <label className="telehealth-check" key={item.catalogKey}>
                            <input
                              type="checkbox"
                              checked={Boolean(reportedAllergyItems[item.catalogKey])}
                              onChange={(event) => updateReportedAllergyItem(item.catalogKey, event.target.checked)}
                            />
                            <span>{item.displayName} — local category: {item.category}</span>
                          </label>
                        ))}
                        <label className="telehealth-check">
                          <input
                            type="checkbox"
                            checked={additionalOrUnlistedAllergyReported}
                            onChange={(event) => {
                              pendingAllergyInformation.current = null
                              setAdditionalOrUnlistedAllergyReported(event.target.checked)
                            }}
                          />
                          <span>I have additional or unlisted allergy or intolerance substances that this incomplete catalog cannot capture.</span>
                        </label>
                      </fieldset>
                    ) : (
                      <p>
                        {allergyInformation.inventoryAllergiesOrIntolerancesStatus === 'Unsure'
                          ? 'Your prior patient-reported allergy or intolerance category remains “unsure.” No substance selection is accepted in this branch.'
                          : 'Your prior patient report was “none.” This only records that provisional report and is not a confirmed no-known-allergy assertion.'}
                      </p>
                    )}
                    <fieldset>
                      <legend>Required acknowledgments</legend>
                      <label className="telehealth-check"><input required type="checkbox" checked={allergyInformationAcknowledgments.patientReportedMayBeIncomplete} onChange={(event) => updateAllergyInformationAcknowledgment('patientReportedMayBeIncomplete', event.target.checked)} /><span>I understand patient-reported allergy or intolerance information may be incomplete.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={allergyInformationAcknowledgments.syntheticCatalogIncomplete} onChange={(event) => updateAllergyInformationAcknowledgment('syntheticCatalogIncomplete', event.target.checked)} /><span>I understand this synthetic local substance catalog is incomplete and has no SNOMED CT, RxNorm, NDC, UNII, or other external terminology mapping claim.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={allergyInformationAcknowledgments.noReactionOrCriticalityCaptured} onChange={(event) => updateAllergyInformationAcknowledgment('noReactionOrCriticalityCaptured', event.target.checked)} /><span>I understand no reaction, severity, criticality, type, status, timing, or other allergy detail is collected.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={allergyInformationAcknowledgments.clinicianVerificationRequired} onChange={(event) => updateAllergyInformationAcknowledgment('clinicianVerificationRequired', event.target.checked)} /><span>I understand a clinician must verify and reconcile allergy or intolerance information before care or prescribing.</span></label>
                    </fieldset>
                    <button
                      className="telehealth-button"
                      type="submit"
                      disabled={submitting
                        || !Object.values(allergyInformationAcknowledgments).every(Boolean)
                        || (allergyInformation.inventoryAllergiesOrIntolerancesStatus === 'ItemsToReview'
                          && !Object.values(reportedAllergyItems).some(Boolean)
                          && !additionalOrUnlistedAllergyReported)}
                    >
                      {submitting ? 'Recording allergy information…' : 'Record allergy information'}
                    </button>
                  </form>
                ) : null}
                <div className="telehealth-emergency-action">
                  <strong>Emergency symptoms now?</strong>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </>
            ) : null}
            {applicant.status === 'SyntheticAllergyInformationRecorded' ? (
              <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={allergyInformationResultRef}>
                <h3>Allergy or intolerance information recorded</h3>
                {allergyInformation ? (
                  <>
                    <dl className="telehealth-details">
                      <div><dt>Prior allergy or intolerance category</dt><dd>{clinicalInformationStatusLabel(allergyInformation.inventoryAllergiesOrIntolerancesStatus)}</dd></div>
                      <div><dt>Selected synthetic substances</dt><dd>{allergyInformation.allergyItems.length}</dd></div>
                      <div><dt>Additional or unlisted substances reported</dt><dd>{allergyInformation.additionalOrUnlistedItemsReported ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Server review route</dt><dd>{allergyInformation.reviewRoute ?? 'Not recorded'}</dd></div>
                      <div><dt>AllergyIntolerance created</dt><dd>No</dd></div>
                      <div><dt>Allergy list reconciled</dt><dd>No</dd></div>
                      <div><dt>Reaction assessed</dt><dd>No</dd></div>
                      <div><dt>Criticality assessed</dt><dd>No</dd></div>
                      <div><dt>Contraindication check performed</dt><dd>No</dd></div>
                      <div><dt>Alert or clinician review task created</dt><dd>No</dd></div>
                      <div><dt>Prescribing enabled</dt><dd>No</dd></div>
                      <div><dt>Recorded</dt><dd>{allergyInformation.recordedAt ? new Date(allergyInformation.recordedAt).toLocaleString() : 'Recorded'}</dd></div>
                    </dl>
                    {allergyInformation.allergyItems.length ? (
                      <ul>{allergyInformation.allergyItems.map((item) => <li key={item.catalogKey}>{item.displayName}: {item.category}</li>)}</ul>
                    ) : <p>No catalog substances were recorded.</p>}
                    <p>{allergyInformation.direction}</p>
                    <p><strong>No canonical allergy resource or confirmed negation, reconciliation, reaction or criticality assessment, contraindication check, alert, clinician task, patient-record change, completed intake, eligibility decision, request, queue entry, prescribing, or care capability was created.</strong></p>
                    <ul>{allergyInformation.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                  </>
                ) : <p>Loading the recorded synthetic allergy-information receipt…</p>}
                <div className="telehealth-emergency-action">
                  <strong>Emergency symptoms now?</strong>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </div>
            ) : null}
            {applicant.status === 'SyntheticAllergyInformationRecorded' ? (
              <>
                {healthHistoryInformationLoading ? <p role="status">Loading the health-history information step…</p> : null}
                {!healthHistoryInformationLoading && !healthHistoryInformation ? (
                  <button className="telehealth-button" type="button" onClick={() => setHealthHistoryInformationLoadAttempt((value) => value + 1)}>
                    Try loading health-history information again
                  </button>
                ) : null}
                {healthHistoryInformation && !healthHistoryInformation.healthHistoryInformationRecorded ? (
                  <form className="telehealth-form" onSubmit={recordHealthHistoryInformation}>
                    <p className="telehealth-kicker">Patient-reported health-history topics</p>
                    <h3>Identify broad topics for later review</h3>
                    <div className="telehealth-synthetic" role="note">
                      This fixed local synthetic topic catalog is incomplete. A selection is only a prompt for later review—not a diagnosis, finding, assessment, problem-list item, or risk evaluation. Do not enter real health information.
                    </div>
                    {healthHistoryInformation.inventoryOtherHealthHistoryStatus === 'ItemsToReview' ? (
                      <fieldset>
                        <legend>Select each broad topic that applies to the prior patient report</legend>
                        {healthHistoryInformation.catalogTopics.map((topic) => (
                          <label className="telehealth-check" key={topic.catalogKey}>
                            <input
                              type="checkbox"
                              checked={Boolean(reportedHealthHistoryTopics[topic.catalogKey])}
                              onChange={(event) => updateReportedHealthHistoryTopic(topic.catalogKey, event.target.checked)}
                            />
                            <span>{topic.displayName} — local category: {topic.category}</span>
                          </label>
                        ))}
                        <label className="telehealth-check">
                          <input
                            type="checkbox"
                            checked={additionalOrUnlistedHealthHistoryReported}
                            onChange={(event) => {
                              pendingHealthHistoryInformation.current = null
                              setAdditionalOrUnlistedHealthHistoryReported(event.target.checked)
                            }}
                          />
                          <span>I have additional or unlisted health-history topics that this incomplete catalog cannot capture.</span>
                        </label>
                      </fieldset>
                    ) : (
                      <p>
                        {healthHistoryInformation.inventoryOtherHealthHistoryStatus === 'Unsure'
                          ? 'Your prior patient-reported other-health-history category remains “unsure.” No topic selection is accepted in this branch.'
                          : 'Your prior patient report was “none.” This only records that provisional report and is not a confirmed no-history assertion.'}
                      </p>
                    )}
                    <fieldset>
                      <legend>Required acknowledgments</legend>
                      <label className="telehealth-check"><input required type="checkbox" checked={healthHistoryInformationAcknowledgments.patientReportedMayBeIncomplete} onChange={(event) => updateHealthHistoryInformationAcknowledgment('patientReportedMayBeIncomplete', event.target.checked)} /><span>I understand patient-reported health-history information may be incomplete.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={healthHistoryInformationAcknowledgments.topicSelectionIsNotDiagnosis} onChange={(event) => updateHealthHistoryInformationAcknowledgment('topicSelectionIsNotDiagnosis', event.target.checked)} /><span>I understand a topic selection is not a diagnosis, finding, assessment, or problem-list entry.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={healthHistoryInformationAcknowledgments.noStatusOrTimingCaptured} onChange={(event) => updateHealthHistoryInformationAcknowledgment('noStatusOrTimingCaptured', event.target.checked)} /><span>I understand no clinical or verification status, timing, severity, diagnosis, procedure, assessment result, family-history detail, note, attachment, or free text is collected.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={healthHistoryInformationAcknowledgments.clinicianVerificationRequired} onChange={(event) => updateHealthHistoryInformationAcknowledgment('clinicianVerificationRequired', event.target.checked)} /><span>I understand detailed collection and clinician verification are required before these topics can affect triage, care, or prescribing.</span></label>
                    </fieldset>
                    <button
                      className="telehealth-button"
                      type="submit"
                      disabled={submitting
                        || !Object.values(healthHistoryInformationAcknowledgments).every(Boolean)
                        || (healthHistoryInformation.inventoryOtherHealthHistoryStatus === 'ItemsToReview'
                          && !Object.values(reportedHealthHistoryTopics).some(Boolean)
                          && !additionalOrUnlistedHealthHistoryReported)}
                    >
                      {submitting ? 'Recording health-history topics…' : 'Record health-history topics'}
                    </button>
                  </form>
                ) : null}
                <div className="telehealth-emergency-action">
                  <strong>Emergency symptoms now?</strong>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </>
            ) : null}
            {applicant.status === 'SyntheticHealthHistoryInformationRecorded' ? (
              <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={healthHistoryInformationResultRef}>
                <h3>Health-history topics recorded</h3>
                {healthHistoryInformation ? (
                  <>
                    <dl className="telehealth-details">
                      <div><dt>Prior other-health-history category</dt><dd>{clinicalInformationStatusLabel(healthHistoryInformation.inventoryOtherHealthHistoryStatus)}</dd></div>
                      <div><dt>Selected synthetic review topics</dt><dd>{healthHistoryInformation.healthHistoryTopics.length}</dd></div>
                      <div><dt>Additional or unlisted topics reported</dt><dd>{healthHistoryInformation.additionalOrUnlistedTopicsReported ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Server review route</dt><dd>{healthHistoryInformation.reviewRoute ?? 'Not recorded'}</dd></div>
                      <div><dt>Condition or problem created</dt><dd>No</dd></div>
                      <div><dt>Procedure or observation created</dt><dd>No</dd></div>
                      <div><dt>FamilyMemberHistory or QuestionnaireResponse created</dt><dd>No</dd></div>
                      <div><dt>Health history reconciled</dt><dd>No</dd></div>
                      <div><dt>Risk modifier evaluated</dt><dd>No</dd></div>
                      <div><dt>Clinical triage changed</dt><dd>No</dd></div>
                      <div><dt>Clinician review task created</dt><dd>No</dd></div>
                      <div><dt>Prescribing enabled</dt><dd>No</dd></div>
                      <div><dt>Recorded</dt><dd>{healthHistoryInformation.recordedAt ? new Date(healthHistoryInformation.recordedAt).toLocaleString() : 'Recorded'}</dd></div>
                    </dl>
                    {healthHistoryInformation.healthHistoryTopics.length ? (
                      <ul>{healthHistoryInformation.healthHistoryTopics.map((topic) => <li key={topic.catalogKey}>{topic.displayName}: {topic.category}</li>)}</ul>
                    ) : <p>No catalog topics were recorded.</p>}
                    <p>{healthHistoryInformation.direction}</p>
                    <p><strong>No canonical problem or clinical resource, confirmed negation, reconciliation, assessment, risk evaluation, triage change, clinician task, patient-record change, completed intake, eligibility decision, request, queue entry, prescribing, or care capability was created.</strong></p>
                    <ul>{healthHistoryInformation.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                  </>
                ) : <p>Loading the recorded synthetic health-history information receipt…</p>}
                <div className="telehealth-emergency-action">
                  <strong>Emergency symptoms now?</strong>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </div>
            ) : null}
            {applicant.status === 'SyntheticHealthHistoryInformationRecorded' ? (
              <div className="telehealth-coverage-result" aria-labelledby="clinical-information-summary-title">
                <h3 id="clinical-information-summary-title">Review your clinical-information summary</h3>
                <p>This is a no-edit review of the coarse synthetic information already recorded. It is not a clinical chart summary.</p>
                {clinicalInformationSummaryLoading ? <p role="status">Loading the clinical-information summary…</p> : null}
                {!clinicalInformationSummaryLoading && !clinicalInformationSummary ? (
                  <button className="telehealth-button" type="button" onClick={() => setClinicalInformationSummaryLoadAttempt((value) => value + 1)}>
                    Retry summary load
                  </button>
                ) : null}
                {clinicalInformationSummary && !clinicalInformationSummary.summaryConfirmed ? (
                  <form onSubmit={confirmClinicalInformationSummary}>
                    <dl className="telehealth-details">
                      {clinicalInformationSummary.categories.map((category) => (
                        <div key={category.categoryKey}>
                          <dt>{clinicalInformationSummaryCategoryLabel(category.categoryKey)}</dt>
                          <dd>
                            {clinicalInformationStatusLabel(category.inventoryStatus)}; {category.selectedItemCount} selected; additional or unlisted: {category.additionalOrUnlistedItemsReported ? 'Yes' : 'No'}; route: {category.reviewRoute}
                          </dd>
                        </div>
                      ))}
                      <div><dt>Server summary route</dt><dd>{clinicalInformationSummary.summaryRoute}</dd></div>
                    </dl>
                    <p><strong>If anything is wrong or missing, do not confirm this step. A separately governed correction or collection workflow is required.</strong></p>
                    <fieldset className="telehealth-fieldset">
                      <legend>Required summary confirmations</legend>
                      <label className="telehealth-check"><input required type="checkbox" checked={clinicalInformationSummaryAcknowledgments.patientReportedMayBeIncomplete} onChange={(event) => updateClinicalInformationSummaryAcknowledgment('patientReportedMayBeIncomplete', event.target.checked)} /><span>I understand this summary is based on patient-reported information and may be incomplete.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={clinicalInformationSummaryAcknowledgments.notClinicallyVerifiedOrReconciled} onChange={(event) => updateClinicalInformationSummaryAcknowledgment('notClinicallyVerifiedOrReconciled', event.target.checked)} /><span>I understand none of these categories has been clinically verified or reconciled.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={clinicalInformationSummaryAcknowledgments.noIntakeCompletionOrEligibility} onChange={(event) => updateClinicalInformationSummaryAcknowledgment('noIntakeCompletionOrEligibility', event.target.checked)} /><span>I understand confirmation does not complete clinical intake or establish telehealth eligibility.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={clinicalInformationSummaryAcknowledgments.correctionRequiresSeparateWorkflow} onChange={(event) => updateClinicalInformationSummaryAcknowledgment('correctionRequiresSeparateWorkflow', event.target.checked)} /><span>I understand corrections or omissions require a separately authorized workflow.</span></label>
                    </fieldset>
                    <button
                      className="telehealth-button"
                      type="submit"
                      disabled={submitting || !Object.values(clinicalInformationSummaryAcknowledgments).every(Boolean)}
                    >
                      {submitting ? 'Confirming summary…' : 'Confirm no-edit summary'}
                    </button>
                  </form>
                ) : null}
                <div className="telehealth-emergency-action">
                  <strong>Emergency symptoms now?</strong>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </div>
            ) : null}
            {applicant.status === 'SyntheticClinicalInformationSummaryConfirmed' ? (
              <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={clinicalInformationSummaryResultRef}>
                <h3>Clinical-information summary confirmed</h3>
                {clinicalInformationSummary ? (
                  <>
                    <dl className="telehealth-details">
                      {clinicalInformationSummary.categories.map((category) => (
                        <div key={category.categoryKey}>
                          <dt>{clinicalInformationSummaryCategoryLabel(category.categoryKey)}</dt>
                          <dd>{clinicalInformationStatusLabel(category.inventoryStatus)}; selected: {category.selectedItemCount}; additional or unlisted: {category.additionalOrUnlistedItemsReported ? 'Yes' : 'No'}</dd>
                        </div>
                      ))}
                      <div><dt>Server summary route</dt><dd>{clinicalInformationSummary.summaryRoute}</dd></div>
                      <div><dt>QuestionnaireResponse created</dt><dd>No</dd></div>
                      <div><dt>Medication, allergy, or history reconciled</dt><dd>No</dd></div>
                      <div><dt>Confirmed negative established</dt><dd>No</dd></div>
                      <div><dt>Clinical intake completed</dt><dd>No</dd></div>
                      <div><dt>Clinical eligibility established</dt><dd>No</dd></div>
                      <div><dt>Clinician review task created</dt><dd>No</dd></div>
                      <div><dt>Practice accepted or queue entered</dt><dd>No</dd></div>
                      <div><dt>Confirmed</dt><dd>{clinicalInformationSummary.confirmedAt ? new Date(clinicalInformationSummary.confirmedAt).toLocaleString() : 'Confirmed'}</dd></div>
                    </dl>
                    <p>{clinicalInformationSummary.direction}</p>
                    <p><strong>No canonical clinical record, confirmed negative, verification, reconciliation, clinical intake completion, eligibility decision, clinician task, practice acceptance, request, queue entry, prescribing, or care capability was created.</strong></p>
                    <ul>{clinicalInformationSummary.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                  </>
                ) : <p>Loading the confirmed synthetic clinical-information summary…</p>}
                <div className="telehealth-emergency-action">
                  <strong>Emergency symptoms now?</strong>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </div>
            ) : null}
            {applicant.status === 'SyntheticClinicalInformationSummaryConfirmed' ? (
              <div className="telehealth-coverage-result" aria-labelledby="pre-request-readiness-title">
                <h3 id="pre-request-readiness-title">Review pre-request readiness</h3>
                <p>This is a no-edit review of five coarse synthetic receipt sections. It does not submit a telehealth request.</p>
                {preRequestReadinessLoading ? <p role="status">Loading the pre-request readiness review…</p> : null}
                {!preRequestReadinessLoading && !preRequestReadiness ? (
                  <button className="telehealth-button" type="button" onClick={() => setPreRequestReadinessLoadAttempt((value) => value + 1)}>
                    Retry readiness load
                  </button>
                ) : null}
                {preRequestReadiness && !preRequestReadiness.readinessAcknowledged ? (
                  <form onSubmit={acknowledgePreRequestReadiness}>
                    <dl className="telehealth-details">
                      {preRequestReadiness.sections.map((section) => (
                        <div key={section.sectionKey}>
                          <dt>{preRequestReadinessSectionLabel(section.sectionKey)}</dt>
                          <dd>{section.receiptState}; unresolved route: {section.outstandingRoute}</dd>
                        </div>
                      ))}
                      <div><dt>Overall server route</dt><dd>{preRequestReadiness.overallRoute}</dd></div>
                    </dl>
                    <p><strong>If any earlier information is wrong or missing, do not acknowledge this step. Use a separately governed correction or collection workflow.</strong></p>
                    <fieldset className="telehealth-fieldset">
                      <legend>Required readiness acknowledgments</legend>
                      <label className="telehealth-check"><input required type="checkbox" checked={preRequestReadinessAcknowledgments.priorSectionsReviewed} onChange={(event) => updatePreRequestReadinessAcknowledgment('priorSectionsReviewed', event.target.checked)} /><span>I reviewed the five coarse sections shown above.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={preRequestReadinessAcknowledgments.outstandingStepsRemain} onChange={(event) => updatePreRequestReadinessAcknowledgment('outstandingStepsRemain', event.target.checked)} /><span>I understand identity, coverage, support, technology, reconciliation, consent, eligibility, and practice-review steps remain unresolved.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={preRequestReadinessAcknowledgments.noRequestOrQueueCreated} onChange={(event) => updatePreRequestReadinessAcknowledgment('noRequestOrQueueCreated', event.target.checked)} /><span>I understand this acknowledgment does not create a telehealth request or queue entry.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={preRequestReadinessAcknowledgments.correctionRequiresSeparateWorkflow} onChange={(event) => updatePreRequestReadinessAcknowledgment('correctionRequiresSeparateWorkflow', event.target.checked)} /><span>I understand corrections require a separately authorized workflow.</span></label>
                    </fieldset>
                    <button
                      className="telehealth-button"
                      type="submit"
                      disabled={submitting || !Object.values(preRequestReadinessAcknowledgments).every(Boolean)}
                    >
                      {submitting ? 'Recording acknowledgment…' : 'Acknowledge readiness boundaries'}
                    </button>
                  </form>
                ) : null}
                <div className="telehealth-emergency-action">
                  <strong>Emergency symptoms now?</strong>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </div>
            ) : null}
            {applicant.status === 'SyntheticPreRequestReadinessAcknowledged' ? (
              <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={preRequestReadinessResultRef}>
                <h3>Pre-request readiness boundaries acknowledged</h3>
                {preRequestReadiness ? (
                  <>
                    <dl className="telehealth-details">
                      {preRequestReadiness.sections.map((section) => (
                        <div key={section.sectionKey}>
                          <dt>{preRequestReadinessSectionLabel(section.sectionKey)}</dt>
                          <dd>{section.receiptState}; unresolved route: {section.outstandingRoute}</dd>
                        </div>
                      ))}
                      <div><dt>Overall server route</dt><dd>{preRequestReadiness.overallRoute}</dd></div>
                      <div><dt>Identity assurance established</dt><dd>No</dd></div>
                      <div><dt>Coverage or rendering-clinician network guaranteed</dt><dd>No</dd></div>
                      <div><dt>Interpreter or accommodation arranged</dt><dd>No</dd></div>
                      <div><dt>Technology readiness established</dt><dd>No</dd></div>
                      <div><dt>Clinical information reconciled or intake completed</dt><dd>No</dd></div>
                      <div><dt>Legal consent or practice acceptance established</dt><dd>No</dd></div>
                      <div><dt>Staff or clinician review task created</dt><dd>No</dd></div>
                      <div><dt>Request, queue, appointment, or encounter created</dt><dd>No</dd></div>
                      <div><dt>Acknowledged</dt><dd>{preRequestReadiness.acknowledgedAt ? new Date(preRequestReadiness.acknowledgedAt).toLocaleString() : 'Acknowledged'}</dd></div>
                    </dl>
                    <p>{preRequestReadiness.direction}</p>
                    <p><strong>No completion, eligibility, consent, practice acceptance, request, queue, appointment, encounter, care, prescribing, billing, claim, integration, or external action was created.</strong></p>
                    <ul>{preRequestReadiness.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                  </>
                ) : <p>Loading the acknowledged synthetic pre-request readiness receipt…</p>}
                <div className="telehealth-emergency-action">
                  <strong>Emergency symptoms now?</strong>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </div>
            ) : null}
            {applicant.status === 'SyntheticPreRequestReadinessAcknowledged' ? (
              <div className="telehealth-coverage-result" aria-labelledby="practice-review-submission-title">
                <h3 id="practice-review-submission-title">Submit for practice review</h3>
                <p>This creates one synthetic practice-intake review work item. It does not create a telehealth request or put you in a patient or clinician queue.</p>
                {practiceReviewLoading ? <p role="status">Loading the practice review submission…</p> : null}
                {!practiceReviewLoading && !practiceReview ? (
                  <button className="telehealth-button" type="button" onClick={() => setPracticeReviewLoadAttempt((value) => value + 1)}>
                    Retry practice review load
                  </button>
                ) : null}
                {practiceReview && !practiceReview.submitted ? (
                  <form onSubmit={submitPracticeReview}>
                    <dl className="telehealth-details">
                      <div><dt>Server review route</dt><dd>{practiceReview.reviewRoute}</dd></div>
                      <div><dt>Practice acceptance</dt><dd>Not established</dd></div>
                      <div><dt>Telehealth request</dt><dd>Not created</dd></div>
                      <div><dt>Patient or clinician queue</dt><dd>Not entered</dd></div>
                      <div><dt>Appointment, encounter, or care</dt><dd>Not created or authorized</dd></div>
                    </dl>
                    <fieldset className="telehealth-fieldset">
                      <legend>Required practice review acknowledgments</legend>
                      <label className="telehealth-check"><input required type="checkbox" checked={practiceReviewAcknowledgments.patientReported} onChange={(event) => updatePracticeReviewAcknowledgment('patientReported', event.target.checked)} /><span>I understand the submitted information remains patient reported and has not been clinically verified or reconciled.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={practiceReviewAcknowledgments.practiceMayRequestInformationOrDecline} onChange={(event) => updatePracticeReviewAcknowledgment('practiceMayRequestInformationOrDecline', event.target.checked)} /><span>I understand the practice may request more information or decline this review.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={practiceReviewAcknowledgments.noTelehealthRequestOrCareQueue} onChange={(event) => updatePracticeReviewAcknowledgment('noTelehealthRequestOrCareQueue', event.target.checked)} /><span>I understand this creates no telehealth request, doctor search, or patient or clinician queue entry.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={practiceReviewAcknowledgments.worseningSymptomsRequireImmediateAction} onChange={(event) => updatePracticeReviewAcknowledgment('worseningSymptomsRequireImmediateAction', event.target.checked)} /><span>I will seek appropriate immediate care instead of waiting if symptoms are urgent or worsening.</span></label>
                    </fieldset>
                    <button
                      className="telehealth-button"
                      type="submit"
                      disabled={submitting || !Object.values(practiceReviewAcknowledgments).every(Boolean)}
                    >
                      {submitting ? 'Submitting for review…' : 'Submit for practice review'}
                    </button>
                  </form>
                ) : null}
                <div className="telehealth-emergency-action">
                  <strong>Emergency symptoms now?</strong>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </div>
            ) : null}
            {applicant.status === 'SyntheticPracticeReviewSubmitted' ? (
              <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={practiceReviewResultRef}>
                <h3>Submitted for practice review</h3>
                {practiceReview ? (
                  <>
                    <dl className="telehealth-details">
                      <div><dt>Review status</dt><dd>{practiceReview.reviewStatus === 'PendingPracticeReview' ? 'Pending practice review' : 'Pending'}</dd></div>
                      <div><dt>Server review route</dt><dd>{practiceReview.reviewRoute}</dd></div>
                      <div><dt>Practice staff review work item created</dt><dd>{practiceReview.staffReviewCreated ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Practice accepted</dt><dd>No</dd></div>
                      <div><dt>Telehealth request created</dt><dd>No</dd></div>
                      <div><dt>Patient or clinician queue entered</dt><dd>No</dd></div>
                      <div><dt>Appointment, encounter, or care created</dt><dd>No</dd></div>
                      <div><dt>Submitted</dt><dd>{practiceReview.submittedAt ? new Date(practiceReview.submittedAt).toLocaleString() : 'Submitted'}</dd></div>
                    </dl>
                    <p>{practiceReview.direction}</p>
                    <p><strong>The practice has a synthetic review work item. No doctor search, queue position, response-time promise, acceptance, telehealth request, appointment, encounter, prescribing, billing, claim, integration, or care capability was created.</strong></p>
                    <ul>{practiceReview.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                  </>
                ) : <p>Loading the submitted synthetic practice review receipt…</p>}
                <div className="telehealth-emergency-action">
                  <strong>Emergency symptoms now?</strong>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </div>
            ) : null}
            {applicant.status === 'SyntheticPracticeReviewAuthorized' ? (
              <div className="telehealth-coverage-result" aria-labelledby="authorized-request-creation-title">
                <h3 id="authorized-request-creation-title">Create the authorized Draft request</h3>
                <p>The practice authorized this one synthetic request-creation step. You must still confirm it. Creating the Draft does not start a doctor search or place you in any queue.</p>
                {requestCreationLoading ? <p role="status">Loading the authorized request-creation step…</p> : null}
                {!requestCreationLoading && !requestCreation ? (
                  <button className="telehealth-button" type="button" onClick={() => setRequestCreationLoadAttempt((value) => value + 1)}>
                    Retry request-creation load
                  </button>
                ) : null}
                {requestCreation?.requestCreationReady ? (
                  <form onSubmit={createAuthorizedRequest}>
                    <dl className="telehealth-details">
                      <div><dt>Visit category</dt><dd>{requestCreation.complaintCategory === 'migraine' ? 'Migraine or recurring headache' : 'Sleep difficulty'}</dd></div>
                      <div><dt>Practice authorization policy</dt><dd>Version {requestCreation.authorizationPolicyVersion}</dd></div>
                      <div><dt>Request status after creation</dt><dd>Draft</dd></div>
                      <div><dt>Doctor search or queue</dt><dd>Not started</dd></div>
                      <div><dt>Appointment, encounter, consent, or care</dt><dd>Not created or authorized</dd></div>
                    </dl>
                    <ul>{requestCreation.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                    <fieldset className="telehealth-fieldset">
                      <legend>Required request-creation confirmations</legend>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestCreationAcknowledgments.createRequest} onChange={(event) => updateRequestCreationAcknowledgment('createRequest', event.target.checked)} /><span>I want to create this one synthetic Draft telehealth request now.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestCreationAcknowledgments.noQueueOrCare} onChange={(event) => updateRequestCreationAcknowledgment('noQueueOrCare', event.target.checked)} /><span>I understand this does not start a doctor search or create a queue entry, queue position, appointment, encounter, consent, or care.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestCreationAcknowledgments.urgentOrWorseningSymptomsRequireImmediateAction} onChange={(event) => updateRequestCreationAcknowledgment('urgentOrWorseningSymptomsRequireImmediateAction', event.target.checked)} /><span>I will seek appropriate immediate care instead of waiting if symptoms are urgent or worsening.</span></label>
                    </fieldset>
                    <button
                      className="telehealth-button"
                      type="submit"
                      disabled={submitting || !Object.values(requestCreationAcknowledgments).every(Boolean)}
                    >
                      {submitting ? 'Creating Draft request…' : 'Create Draft telehealth request'}
                    </button>
                  </form>
                ) : null}
                <div className="telehealth-emergency-action">
                  <strong>Emergency symptoms now?</strong>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
              </div>
            ) : null}
            {applicant.status === 'SyntheticRequestCreated' ? (
              <>
                <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={requestCreationResultRef}>
                <h3>Draft telehealth request created</h3>
                {requestCreation?.requestCreated ? (
                  <>
                    <dl className="telehealth-details">
                      <div><dt>Request reference</dt><dd>{requestCreation.requestId}</dd></div>
                      <div><dt>Visit category</dt><dd>{requestCreation.complaintCategory === 'migraine' ? 'Migraine or recurring headache' : 'Sleep difficulty'}</dd></div>
                      <div><dt>Request status</dt><dd>{requestCreation.requestStatus}</dd></div>
                      <div><dt>Doctor search started</dt><dd>{requestCreation.doctorSearchStarted ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Patient or clinician queue entered</dt><dd>{requestCreation.patientCareQueueEntered || requestCreation.clinicianQueueEntered ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Queue position assigned</dt><dd>{requestCreation.queuePositionAssigned ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Appointment, encounter, consent, or care created</dt><dd>No</dd></div>
                      <div><dt>Created</dt><dd>{requestCreation.createdAt ? new Date(requestCreation.createdAt).toLocaleString() : 'Created'}</dd></div>
                    </dl>
                    <p>{requestCreation.direction}</p>
                    <p><strong>The request is only a Draft. No doctor search, queue, queue position, appointment, encounter, consent, media, care, prescribing, billing, claim, integration, or external action exists.</strong></p>
                    <ul>{requestCreation.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                  </>
                ) : <p>Loading the synthetic Draft request receipt…</p>}
                <div className="telehealth-emergency-action">
                  <strong>Emergency symptoms now?</strong>
                  <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                </div>
                </div>
                {requestLocationLoading ? <p role="status">Loading location and callback confirmation…</p> : null}
                {!requestLocationLoading && !requestLocation ? (
                  <button className="telehealth-button" type="button" onClick={() => setRequestLocationLoadAttempt((value) => value + 1)}>
                    Retry location-confirmation load
                  </button>
                ) : null}
                {requestLocation?.confirmationReady ? (
                  <div className="telehealth-coverage-result" aria-labelledby="request-location-title">
                    <h3 id="request-location-title">Confirm where you are now</h3>
                    <p>Your current physical location controls which state-specific telehealth rules apply. It must match the state already used for this request.</p>
                    <form onSubmit={confirmRequestLocation}>
                      <label htmlFor="request-current-location-state">Current physical location</label>
                      <select
                        id="request-current-location-state"
                        required
                        value={requestLocationStateCode}
                        onChange={(event) => {
                          pendingRequestLocation.current = null
                          setRequestLocationStateCode(event.target.value as '' | 'GA' | 'CA' | 'FL')
                        }}
                      >
                        <option value="">Select your current state</option>
                        <option value="GA">Georgia</option>
                        <option value="CA">California</option>
                        <option value="FL">Florida</option>
                      </select>
                      <dl className="telehealth-details">
                        <div><dt>State previously established</dt><dd>{requestLocation.currentLocationStateCode}</dd></div>
                        <div><dt>Callback number</dt><dd>{requestLocation.maskedCallbackPhone}</dd></div>
                        <div><dt>Request status before confirmation</dt><dd>{requestLocation.requestStatus}</dd></div>
                        <div><dt>Doctor search or queue</dt><dd>Not started</dd></div>
                      </dl>
                      <p className="telehealth-inline-warning"><strong>If your state or callback route changed, stop here.</strong> Start again or ask the practice to review the change so state-specific evidence can be rebuilt.</p>
                      <ul>{requestLocation.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                      <fieldset className="telehealth-fieldset">
                        <legend>Required location and safety confirmations</legend>
                        <label className="telehealth-check"><input required type="checkbox" checked={requestLocationAcknowledgments.currentLocation} onChange={(event) => updateRequestLocationAcknowledgment('currentLocation', event.target.checked)} /><span>The state I selected is my current physical location.</span></label>
                        <label className="telehealth-check"><input required type="checkbox" checked={requestLocationAcknowledgments.callbackNumber} onChange={(event) => updateRequestLocationAcknowledgment('callbackNumber', event.target.checked)} /><span>The displayed masked callback number remains correct.</span></label>
                        <label className="telehealth-check"><input required type="checkbox" checked={requestLocationAcknowledgments.changedLocationRequiresRestart} onChange={(event) => updateRequestLocationAcknowledgment('changedLocationRequiresRestart', event.target.checked)} /><span>I understand that a changed state or callback route requires a restart or practice review.</span></label>
                        <label className="telehealth-check"><input required type="checkbox" checked={requestLocationAcknowledgments.urgentOrWorseningSymptomsRequireImmediateAction} onChange={(event) => updateRequestLocationAcknowledgment('urgentOrWorseningSymptomsRequireImmediateAction', event.target.checked)} /><span>I will seek appropriate immediate care instead of waiting if symptoms are urgent or worsening.</span></label>
                      </fieldset>
                      <button
                        className="telehealth-button"
                        type="submit"
                        disabled={submitting || !requestLocationStateCode || !Object.values(requestLocationAcknowledgments).every(Boolean)}
                      >
                        {submitting ? 'Confirming location…' : 'Confirm location and callback'}
                      </button>
                    </form>
                    <div className="telehealth-emergency-action">
                      <strong>Emergency symptoms now?</strong>
                      <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                    </div>
                  </div>
                ) : null}
                {requestLocation?.locationConfirmed ? (
                  <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={requestLocationResultRef}>
                    <h3>Location and callback confirmed</h3>
                    <dl className="telehealth-details">
                      <div><dt>Request reference</dt><dd>{requestLocation.requestId}</dd></div>
                      <div><dt>Request status</dt><dd>{requestLocation.requestStatus}</dd></div>
                      <div><dt>Request version</dt><dd>{requestLocation.requestVersion}</dd></div>
                      <div><dt>Current location state</dt><dd>{requestLocation.currentLocationStateCode}</dd></div>
                      <div><dt>Callback number</dt><dd>{requestLocation.maskedCallbackPhone}</dd></div>
                      <div><dt>Triage assessment created</dt><dd>{requestLocation.triageAssessmentCreated ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Doctor search started</dt><dd>{requestLocation.doctorSearchStarted ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Patient or clinician queue entered</dt><dd>{requestLocation.patientCareQueueEntered || requestLocation.clinicianQueueEntered ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Appointment, encounter, consent, or care created</dt><dd>No</dd></div>
                      <div><dt>Confirmed</dt><dd>{requestLocation.confirmedAt ? new Date(requestLocation.confirmedAt).toLocaleString() : 'Confirmed'}</dd></div>
                    </dl>
                    <p>{requestLocation.direction}</p>
                    <p><strong>No triage result, doctor search, queue, queue position, appointment, encounter, consent, care, prescribing, billing, claim, integration, or external action was created.</strong></p>
                    <ul>{requestLocation.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                    <div className="telehealth-emergency-action">
                      <strong>Emergency symptoms now?</strong>
                      <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                    </div>
                  </div>
                ) : null}
                {requestLocation?.locationConfirmed && requestSafetyLoading ? (
                  <p role="status">Loading the request universal safety screen…</p>
                ) : null}
                {requestLocation?.locationConfirmed && !requestSafetyLoading && !requestSafety ? (
                  <button className="telehealth-button" type="button" onClick={() => setRequestSafetyLoadAttempt((value) => value + 1)}>
                    Retry universal safety-screen load
                  </button>
                ) : null}
                {requestSafety?.assessmentReady ? (
                  <form className="telehealth-review-form" onSubmit={assessRequestSafety}>
                    <div className="telehealth-emergency" role="note">
                      <h3>Emergency direction is immediate</h3>
                      <p>If any current symptom may be an emergency, call 911 now or go to the nearest emergency department. Do not wait to submit this form.</p>
                      <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                    </div>
                    <h3>Request universal safety screen</h3>
                    <p id="request-universal-safety-help">Choose an explicit answer for every question. This immutable synthetic fixture is not approved clinical content, diagnosis, or complete telehealth eligibility.</p>
                    <dl className="telehealth-details">
                      <div><dt>Current location state</dt><dd>{requestSafety.currentLocationStateCode}</dd></div>
                      <div><dt>Callback number</dt><dd>{requestSafety.maskedCallbackPhone}</dd></div>
                      <div><dt>Request status</dt><dd>{requestSafety.requestStatus}</dd></div>
                      <div><dt>Context expires</dt><dd>{new Date(requestSafety.contextExpiresAt).toLocaleString()}</dd></div>
                    </dl>
                    <fieldset aria-describedby="request-universal-safety-help"><legend>Could any current symptom be an emergency?</legend><label className="telehealth-check"><input required type="radio" name="request-emergency" value="yes" checked={requestSafetyAnswers.emergency === 'yes'} onChange={() => updateRequestSafetyAnswer('emergency', 'yes')} />Yes</label><label className="telehealth-check"><input required type="radio" name="request-emergency" value="no" checked={requestSafetyAnswers.emergency === 'no'} onChange={() => updateRequestSafetyAnswer('emergency', 'no')} />No</label></fieldset>
                    {requestSafetyAnswers.emergency === 'yes' ? <p className="telehealth-inline-warning" role="alert"><strong>Call 911 now or go to the nearest emergency department.</strong> This application has not contacted or dispatched emergency services.</p> : null}
                    <fieldset><legend>Are symptoms severe or getting worse quickly?</legend><label className="telehealth-check"><input required type="radio" name="request-severe" value="yes" checked={requestSafetyAnswers.severe === 'yes'} onChange={() => updateRequestSafetyAnswer('severe', 'yes')} />Yes</label><label className="telehealth-check"><input required type="radio" name="request-severe" value="no" checked={requestSafetyAnswers.severe === 'no'} onChange={() => updateRequestSafetyAnswer('severe', 'no')} />No</label></fieldset>
                    <fieldset><legend>Does this seem to require a hands-on examination or procedure?</legend><label className="telehealth-check"><input required type="radio" name="request-hands-on" value="yes" checked={requestSafetyAnswers.handsOn === 'yes'} onChange={() => updateRequestSafetyAnswer('handsOn', 'yes')} />Yes</label><label className="telehealth-check"><input required type="radio" name="request-hands-on" value="no" checked={requestSafetyAnswers.handsOn === 'no'} onChange={() => updateRequestSafetyAnswer('handsOn', 'no')} />No</label></fieldset>
                    <fieldset><legend>Are you unsure about any answer above?</legend><label className="telehealth-check"><input required type="radio" name="request-unsure" value="yes" checked={requestSafetyAnswers.unsure === 'yes'} onChange={() => updateRequestSafetyAnswer('unsure', 'yes')} />Yes</label><label className="telehealth-check"><input required type="radio" name="request-unsure" value="no" checked={requestSafetyAnswers.unsure === 'no'} onChange={() => updateRequestSafetyAnswer('unsure', 'no')} />No</label></fieldset>
                    <fieldset className="telehealth-fieldset">
                      <legend>Required context confirmations</legend>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestSafetyConfirmations.currentLocation} onChange={(event) => updateRequestSafetyConfirmation('currentLocation', event.target.checked)} /><span>I confirm the displayed state is my current physical location.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestSafetyConfirmations.callbackNumber} onChange={(event) => updateRequestSafetyConfirmation('callbackNumber', event.target.checked)} /><span>I confirm the displayed masked callback number remains correct.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestSafetyConfirmations.syntheticData} onChange={(event) => updateRequestSafetyConfirmation('syntheticData', event.target.checked)} /><span>I confirm every answer is fictional synthetic demonstration data.</span></label>
                    </fieldset>
                    <ul>{requestSafety.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                    <button
                      className="telehealth-button"
                      type="submit"
                      disabled={submitting || Object.values(requestSafetyAnswers).some((answer) => answer === '') || !Object.values(requestSafetyConfirmations).every(Boolean)}
                    >
                      {submitting ? 'Evaluating request safety screen…' : 'Evaluate request universal safety screen'}
                    </button>
                  </form>
                ) : null}
                {requestSafety?.assessmentCreated ? (
                  <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={requestSafetyResultRef}>
                    <h3>{requestSafety.publicDisposition === 'UniversalSafetyPassed' ? 'Universal safety screen passed' : 'Universal safety screen stopped progression'}</h3>
                    <dl className="telehealth-details">
                      <div><dt>Request reference</dt><dd>{requestSafety.requestId}</dd></div>
                      <div><dt>Request status</dt><dd>{requestSafety.requestStatus}</dd></div>
                      <div><dt>Public disposition</dt><dd>{requestSafety.publicDisposition}</dd></div>
                      <div><dt>Universal safety passed</dt><dd>{requestSafety.universalSafetyPassed ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Complaint-specific triage required</dt><dd>{requestSafety.complaintSpecificTriageRequired ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Complaint-specific triage created</dt><dd>{requestSafety.complaintSpecificTriageCreated ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Clinical review required</dt><dd>{requestSafety.clinicalReviewRequired ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Clinical-review work item created</dt><dd>{requestSafety.clinicalReviewCreated ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Doctor search or queue</dt><dd>{requestSafety.doctorSearchStarted || requestSafety.patientCareQueueEntered || requestSafety.clinicianQueueEntered ? 'Started' : 'Not started'}</dd></div>
                      <div><dt>Evaluated</dt><dd>{requestSafety.evaluatedAt ? new Date(requestSafety.evaluatedAt).toLocaleString() : 'Recorded'}</dd></div>
                    </dl>
                    <p><strong>{requestSafety.direction}</strong></p>
                    {requestSafety.outcome === 'Emergency' ? <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a> : null}
                    <p>No submitted safety answer or answer fingerprint is returned. No doctor search, queue, appointment, encounter, consent, care, prescribing, financial, integration, or external action was created.</p>
                    <ul>{requestSafety.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                  </div>
                ) : null}
                {requestSafety?.complaintSpecificTriageRequired && requestComplaintTriageLoading ? (
                  <p role="status">Loading complaint-specific synthetic triage…</p>
                ) : null}
                {requestSafety?.complaintSpecificTriageRequired
                  && !requestComplaintTriageLoading
                  && !requestComplaintTriage ? (
                    <button
                      className="telehealth-button"
                      type="button"
                      onClick={() => setRequestComplaintTriageLoadAttempt((value) => value + 1)}
                    >
                      Retry complaint-triage load
                    </button>
                  ) : null}
                {requestComplaintTriage?.assessmentReady ? (
                  <form className="telehealth-review-form" onSubmit={assessRequestComplaintTriage}>
                    <div className="telehealth-emergency" role="note">
                      <h3>Emergency and crisis help is immediate</h3>
                      <p>Call 911 now for an emergency. Call or text 988 for a mental-health or suicide crisis. Do not wait to submit this demonstration form.</p>
                      <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                      <a className="telehealth-button telehealth-button-secondary" href="tel:988">Call 988</a>
                    </div>
                    <h3>{requestComplaintTriage.complaintCategory === 'migraine' ? 'Headache or known migraine pattern' : 'Sleep difficulty'} synthetic triage</h3>
                    <p id="request-complaint-triage-help">Choose Yes, No, or Not sure for every question. “Not sure” routes to review; it never defaults to eligible. These questions are unapproved synthetic engineering content, not medical advice.</p>
                    <dl className="telehealth-details">
                      <div><dt>Server-owned category</dt><dd>{requestComplaintTriage.complaintCategory}</dd></div>
                      <div><dt>Current location state</dt><dd>{requestComplaintTriage.currentLocationStateCode}</dd></div>
                      <div><dt>Callback number</dt><dd>{requestComplaintTriage.maskedCallbackPhone}</dd></div>
                      <div><dt>Clinical content status</dt><dd>{requestComplaintTriage.clinicalContentStatus}</dd></div>
                      <div><dt>Context expires</dt><dd>{new Date(requestComplaintTriage.contextExpiresAt).toLocaleString()}</dd></div>
                    </dl>
                    {requestComplaintTriage.complaintCategory === 'migraine' ? (
                      <div aria-describedby="request-complaint-triage-help">
                        <ComplaintAnswerField legend="Did this headache begin suddenly, or is it the worst headache in this fictional scenario?" name="complaint-migraine-sudden" answer={migraineComplaintAnswers.suddenOrWorstOnset} onChange={(answer) => updateMigraineComplaintAnswer('suddenOrWorstOnset', answer)} />
                        <ComplaintAnswerField legend="Is there a new neurologic or vision change in this fictional scenario?" name="complaint-migraine-neurologic" answer={migraineComplaintAnswers.newNeurologicOrVisionChange} onChange={(answer) => updateMigraineComplaintAnswer('newNeurologicOrVisionChange', answer)} />
                        {(migraineComplaintAnswers.suddenOrWorstOnset === 'Yes' || migraineComplaintAnswers.newNeurologicOrVisionChange === 'Yes') ? <p className="telehealth-inline-warning" role="alert"><strong>Call 911 now.</strong> This application has not contacted or dispatched emergency services.</p> : null}
                        <ComplaintAnswerField legend="Is there fever or a stiff neck in this fictional scenario?" name="complaint-migraine-fever" answer={migraineComplaintAnswers.feverOrStiffNeck} onChange={(answer) => updateMigraineComplaintAnswer('feverOrStiffNeck', answer)} />
                        <ComplaintAnswerField legend="Was there a recent head injury in this fictional scenario?" name="complaint-migraine-injury" answer={migraineComplaintAnswers.recentHeadInjury} onChange={(answer) => updateMigraineComplaintAnswer('recentHeadInjury', answer)} />
                        <ComplaintAnswerField legend="Is the fictional patient pregnant or recently postpartum?" name="complaint-migraine-pregnancy" answer={migraineComplaintAnswers.pregnantOrPostpartum} onChange={(answer) => updateMigraineComplaintAnswer('pregnantOrPostpartum', answer)} />
                        <ComplaintAnswerField legend="Does the fictional scenario include cancer or immune compromise?" name="complaint-migraine-cancer" answer={migraineComplaintAnswers.cancerOrImmunocompromised} onChange={(answer) => updateMigraineComplaintAnswer('cancerOrImmunocompromised', answer)} />
                        <ComplaintAnswerField legend="Is this similar to a previously known headache or migraine pattern in the fictional scenario?" name="complaint-migraine-pattern" answer={migraineComplaintAnswers.knownSimilarPattern} onChange={(answer) => updateMigraineComplaintAnswer('knownSimilarPattern', answer)} />
                        <ComplaintAnswerField legend="Is there persistent vomiting in this fictional scenario?" name="complaint-migraine-vomiting" answer={migraineComplaintAnswers.persistentVomiting} onChange={(answer) => updateMigraineComplaintAnswer('persistentVomiting', answer)} />
                      </div>
                    ) : (
                      <div aria-describedby="request-complaint-triage-help">
                        <ComplaintAnswerField legend="Does the fictional scenario include thoughts of self-harm or suicide?" name="complaint-sleep-self-harm" answer={sleepComplaintAnswers.selfHarmThoughts} onChange={(answer) => updateSleepComplaintAnswer('selfHarmThoughts', answer)} />
                        {sleepComplaintAnswers.selfHarmThoughts === 'Yes' ? <p className="telehealth-inline-warning" role="alert"><strong>Call 911 for immediate danger, or call or text 988 for crisis support.</strong> This application has not contacted emergency or crisis services.</p> : null}
                        <ComplaintAnswerField legend="Does the fictional scenario include possible mania or psychosis?" name="complaint-sleep-mania" answer={sleepComplaintAnswers.maniaOrPsychosis} onChange={(answer) => updateSleepComplaintAnswer('maniaOrPsychosis', answer)} />
                        <ComplaintAnswerField legend="Could sleepiness make driving, work, or another activity unsafe in this fictional scenario?" name="complaint-sleep-somnolence" answer={sleepComplaintAnswers.dangerousSomnolence} onChange={(answer) => updateSleepComplaintAnswer('dangerousSomnolence', answer)} />
                        <ComplaintAnswerField legend="Is withdrawal from alcohol, medication, or another substance a concern in this fictional scenario?" name="complaint-sleep-withdrawal" answer={sleepComplaintAnswers.withdrawalConcern} onChange={(answer) => updateSleepComplaintAnswer('withdrawalConcern', answer)} />
                        <ComplaintAnswerField legend="Are breathing pauses or severe snoring reported in this fictional scenario?" name="complaint-sleep-breathing" answer={sleepComplaintAnswers.breathingPausesOrSevereSnoring} onChange={(answer) => updateSleepComplaintAnswer('breathingPausesOrSevereSnoring', answer)} />
                        <ComplaintAnswerField legend="Does the fictional scenario include pregnancy or a complex medication concern?" name="complaint-sleep-pregnancy" answer={sleepComplaintAnswers.pregnantOrComplexMedicationConcern} onChange={(answer) => updateSleepComplaintAnswer('pregnantOrComplexMedicationConcern', answer)} />
                        <ComplaintAnswerField legend="Is the fictional request specifically for a controlled sedative?" name="complaint-sleep-controlled" answer={sleepComplaintAnswers.controlledSedativeRequest} onChange={(answer) => updateSleepComplaintAnswer('controlledSedativeRequest', answer)} />
                        <ComplaintAnswerField legend="Is uncomplicated sleep difficulty the only concern in this fictional scenario?" name="complaint-sleep-uncomplicated" answer={sleepComplaintAnswers.uncomplicatedSleepDifficulty} onChange={(answer) => updateSleepComplaintAnswer('uncomplicatedSleepDifficulty', answer)} />
                      </div>
                    )}
                    <fieldset className="telehealth-fieldset">
                      <legend>Required context confirmations</legend>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestComplaintTriageConfirmations.currentLocation} onChange={(event) => updateRequestComplaintTriageConfirmation('currentLocation', event.target.checked)} /><span>I confirm the displayed state remains the current physical location.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestComplaintTriageConfirmations.callbackNumber} onChange={(event) => updateRequestComplaintTriageConfirmation('callbackNumber', event.target.checked)} /><span>I confirm the displayed masked callback number remains correct.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestComplaintTriageConfirmations.syntheticData} onChange={(event) => updateRequestComplaintTriageConfirmation('syntheticData', event.target.checked)} /><span>I confirm every answer is fictional synthetic demonstration data.</span></label>
                    </fieldset>
                    <ul>{requestComplaintTriage.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                    <button
                      className="telehealth-button"
                      type="submit"
                      disabled={submitting
                        || Object.values(requestComplaintTriage.complaintCategory === 'migraine' ? migraineComplaintAnswers : sleepComplaintAnswers).some((answer) => answer === '')
                        || !Object.values(requestComplaintTriageConfirmations).every(Boolean)}
                    >
                      {submitting ? 'Evaluating complaint-specific triage…' : 'Evaluate synthetic complaint triage'}
                    </button>
                  </form>
                ) : null}
                {requestComplaintTriage?.assessmentCreated ? (
                  <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={requestComplaintTriageResultRef}>
                    <h3>{requestComplaintTriage.syntheticVideoEvaluationCandidate ? 'Synthetic fixture can demonstrate intake progression' : 'Complaint-specific triage stopped progression'}</h3>
                    <dl className="telehealth-details">
                      <div><dt>Request reference</dt><dd>{requestComplaintTriage.requestId}</dd></div>
                      <div><dt>Request status</dt><dd>{requestComplaintTriage.requestStatus}</dd></div>
                      <div><dt>Complaint category</dt><dd>{requestComplaintTriage.complaintCategory}</dd></div>
                      <div><dt>Public disposition</dt><dd>{requestComplaintTriage.publicDisposition}</dd></div>
                      <div><dt>Clinical content status</dt><dd>{requestComplaintTriage.clinicalContentStatus}</dd></div>
                      <div><dt>Medical-director approval recorded</dt><dd>{requestComplaintTriage.medicalDirectorApprovalRecorded ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Clinical golden cases approved</dt><dd>{requestComplaintTriage.clinicalGoldenCasePackApproved ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Production publication allowed</dt><dd>{requestComplaintTriage.productionPublicationAllowed ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Clinical-review work item created</dt><dd>{requestComplaintTriage.clinicalReviewCreated ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Intake snapshot created</dt><dd>{requestComplaintTriage.intakeSnapshotCreated ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Doctor search or queue</dt><dd>{requestComplaintTriage.doctorSearchStarted || requestComplaintTriage.patientCareQueueEntered || requestComplaintTriage.clinicianQueueEntered ? 'Started' : 'Not started'}</dd></div>
                      <div><dt>Evaluated</dt><dd>{requestComplaintTriage.evaluatedAt ? new Date(requestComplaintTriage.evaluatedAt).toLocaleString() : 'Recorded'}</dd></div>
                    </dl>
                    <p><strong>{requestComplaintTriage.direction}</strong></p>
                    {requestComplaintTriage.outcome === 'Emergency' ? <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a> : null}
                    {requestComplaintTriage.complaintCategory === 'sleep' && requestComplaintTriage.outcome === 'Emergency' ? <a className="telehealth-button telehealth-button-secondary" href="tel:988">Call 988</a> : null}
                    <p>No submitted answer, answer fingerprint, fired rule, or reason code is returned. No clinical-review work item, intake snapshot, contact, doctor search, queue, appointment, encounter, consent, care, prescribing, financial, integration, or external action was created.</p>
                    <ul>{requestComplaintTriage.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                  </div>
                ) : null}
                {requestComplaintTriage?.assessmentCreated
                  && requestComplaintTriage.syntheticVideoEvaluationCandidate
                  && requestIntakeLoading ? (
                    <p role="status">Loading the request intake confirmation…</p>
                  ) : null}
                {requestComplaintTriage?.assessmentCreated
                  && requestComplaintTriage.syntheticVideoEvaluationCandidate
                  && !requestIntakeLoading
                  && !requestIntake ? (
                    <button
                      className="telehealth-button"
                      type="button"
                      onClick={() => setRequestIntakeLoadAttempt((value) => value + 1)}
                    >
                      Retry request intake load
                    </button>
                  ) : null}
                {requestIntake?.snapshotReady ? (
                  <form className="telehealth-review-form" onSubmit={confirmRequestIntake}>
                    <div className="telehealth-emergency" role="note">
                      <h3>Changed or worsening symptoms need action</h3>
                      <p>Call 911 now for an emergency. Do not use this synthetic intake step when the location, callback route, or prior information has changed; restart or contact the practice.</p>
                      <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
                    </div>
                    <h3>Confirm request intake snapshot</h3>
                    <p id="request-intake-help">Choose one controlled duration range. This form has no complaint narrative or free-text field, and it cannot change the server-owned category or triage result.</p>
                    <dl className="telehealth-details">
                      <div><dt>Request reference</dt><dd>{requestIntake.requestId}</dd></div>
                      <div><dt>Server-owned purpose</dt><dd>{requestIntake.complaintDisplayLabel}</dd></div>
                      <div><dt>Current location state</dt><dd>{requestIntake.currentLocationStateCode}</dd></div>
                      <div><dt>Callback number</dt><dd>{requestIntake.maskedCallbackPhone}</dd></div>
                      <div><dt>Clinical content status</dt><dd>{requestIntake.clinicalContentStatus}</dd></div>
                      <div><dt>Context expires</dt><dd>{new Date(requestIntake.contextExpiresAt).toLocaleString()}</dd></div>
                    </dl>
                    <h4>Previously collected source states</h4>
                    <dl className="telehealth-details">
                      {requestIntake.sections.map((section) => (
                        <div key={section.sectionKey}>
                          <dt>{section.sectionKey}</dt>
                          <dd>{section.receiptState}. {section.outstandingRoute}</dd>
                        </div>
                      ))}
                    </dl>
                    <label>How long has the fictional concern been present?
                      <select
                        required
                        aria-describedby="request-intake-help"
                        value={requestIntakeSymptomDuration}
                        onChange={(event) => updateRequestIntakeDuration(event.target.value as '' | TelehealthApplicantRequestIntakeInput['symptomDuration'])}
                      >
                        <option value="">Choose a duration</option>
                        {requestIntake.supportedSymptomDurations.map((duration) => (
                          <option key={duration} value={duration}>
                            {duration === 'less-than-day' ? 'Less than one day'
                              : duration === '1-3-days' ? '1–3 days'
                                : duration === '4-14-days' ? '4–14 days'
                                  : 'More than 14 days'}
                          </option>
                        ))}
                      </select>
                    </label>
                    <fieldset className="telehealth-fieldset">
                      <legend>Eight required confirmations</legend>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestIntakeConfirmations.currentLocation} onChange={(event) => updateRequestIntakeConfirmation('currentLocation', event.target.checked)} /><span>I confirm the displayed state remains the current physical location.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestIntakeConfirmations.callbackNumber} onChange={(event) => updateRequestIntakeConfirmation('callbackNumber', event.target.checked)} /><span>I confirm the displayed masked callback route remains correct.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestIntakeConfirmations.priorInformation} onChange={(event) => updateRequestIntakeConfirmation('priorInformation', event.target.checked)} /><span>I reviewed the displayed prior-information receipt states and will stop if a correction is needed.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestIntakeConfirmations.insuranceLimitations} onChange={(event) => updateRequestIntakeConfirmation('insuranceLimitations', event.target.checked)} /><span>I understand no canonical coverage, current eligibility, benefits, or exact network result exists.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestIntakeConfirmations.pendingConsent} onChange={(event) => updateRequestIntakeConfirmation('pendingConsent', event.target.checked)} /><span>I understand legal and clinician consent remain pending and unavailable here.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestIntakeConfirmations.pendingVerification} onChange={(event) => updateRequestIntakeConfirmation('pendingVerification', event.target.checked)} /><span>I understand advancing to Verification only records a pending workflow state.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestIntakeConfirmations.complaintResult} onChange={(event) => updateRequestIntakeConfirmation('complaintResult', event.target.checked)} /><span>I understand the synthetic candidate result is not diagnosis, treatment, acceptance, or guaranteed care.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestIntakeConfirmations.syntheticData} onChange={(event) => updateRequestIntakeConfirmation('syntheticData', event.target.checked)} /><span>I confirm the duration is fictional synthetic demonstration data.</span></label>
                    </fieldset>
                    <ul>{requestIntake.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                    <button
                      className="telehealth-button"
                      type="submit"
                      disabled={submitting
                        || !requestIntakeSymptomDuration
                        || !Object.values(requestIntakeConfirmations).every(Boolean)}
                    >
                      {submitting ? 'Recording request intake…' : 'Record synthetic intake snapshot'}
                    </button>
                  </form>
                ) : null}
                {requestIntake?.snapshotCreated ? (
                  <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={requestIntakeResultRef}>
                    <h3>Request intake snapshot recorded</h3>
                    <dl className="telehealth-details">
                      <div><dt>Request reference</dt><dd>{requestIntake.requestId}</dd></div>
                      <div><dt>Request status</dt><dd>{requestIntake.requestStatus}</dd></div>
                      <div><dt>Request version</dt><dd>{requestIntake.requestVersion}</dd></div>
                      <div><dt>Purpose</dt><dd>{requestIntake.complaintDisplayLabel}</dd></div>
                      <div><dt>Duration</dt><dd>{requestIntake.symptomDuration}</dd></div>
                      <div><dt>Verification pending</dt><dd>{requestIntake.verificationPending ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Consent pending</dt><dd>{requestIntake.consentPending ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Coverage or exact network confirmed</dt><dd>{requestIntake.coverageVerified || requestIntake.exactNetworkConfirmed ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Operational review or practice acceptance</dt><dd>{requestIntake.operationalReviewCreated || requestIntake.practiceAccepted ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Doctor search or queue</dt><dd>{requestIntake.doctorSearchStarted || requestIntake.patientCareQueueEntered || requestIntake.clinicianQueueEntered ? 'Started' : 'Not started'}</dd></div>
                      <div><dt>Captured</dt><dd>{requestIntake.capturedAt ? new Date(requestIntake.capturedAt).toLocaleString() : 'Recorded'}</dd></div>
                    </dl>
                    <p><strong>{requestIntake.direction}</strong></p>
                    <p>No patient or applicant record was changed. No coverage, consent, operational review, contact, queue, appointment, encounter, media, care, prescription, financial, integration, or external action was created.</p>
                    <ul>{requestIntake.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                  </div>
                ) : null}
                {requestIntake?.snapshotCreated && requestInsuranceSourceLoading ? (
                  <p role="status">Loading the request insurance source…</p>
                ) : null}
                {requestIntake?.snapshotCreated
                  && !requestInsuranceSourceLoading
                  && !requestInsuranceSource ? (
                    <button
                      className="telehealth-button"
                      type="button"
                      onClick={() => setRequestInsuranceSourceLoadAttempt((value) => value + 1)}
                    >
                      Retry insurance-source load
                    </button>
                  ) : null}
                {requestInsuranceSource?.sourceReady ? (
                  <form className="telehealth-review-form" onSubmit={confirmRequestInsuranceSource}>
                    <div className="telehealth-synthetic" role="note">
                      Historical source only. These details do not show current eligibility, benefits, practice-network status, or participation by the eventual treating physician.
                    </div>
                    <h3>Confirm insurance source for this request</h3>
                    <p id="request-insurance-source-help">Review only the masked, previously collected source. There are no member-ID, group-number, payer, or plan edit fields in this step.</p>
                    <dl className="telehealth-details" aria-describedby="request-insurance-source-help">
                      <div><dt>Payer</dt><dd>{requestInsuranceSource.payerDisplayName}</dd></div>
                      <div><dt>Product</dt><dd>{requestInsuranceSource.productDisplayName}</dd></div>
                      <div><dt>Member ID</dt><dd>{requestInsuranceSource.maskedMemberId}</dd></div>
                      <div><dt>Group number</dt><dd>{requestInsuranceSource.maskedGroupNumber ?? 'Not supplied'}</dd></div>
                      <div><dt>Subscriber relationship</dt><dd>{requestInsuranceSource.subscriberRelationship}</dd></div>
                      <div><dt>Coverage source</dt><dd>{requestInsuranceSource.coveragePriority}</dd></div>
                      <div><dt>Earlier eligibility result — historical only</dt><dd>{requestInsuranceSource.previousEligibilityBusinessOutcome}</dd></div>
                      <div><dt>Earlier eligibility checked</dt><dd>{new Date(requestInsuranceSource.previousEligibilityCheckedAt).toLocaleString()}</dd></div>
                      <div><dt>Earlier eligibility evidence expired</dt><dd>{requestInsuranceSource.previousEligibilityEvidenceExpired ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Earlier practice-network result — historical only</dt><dd>{requestInsuranceSource.previousPracticeNetworkBusinessOutcome}</dd></div>
                      <div><dt>Earlier practice-network checked</dt><dd>{new Date(requestInsuranceSource.previousPracticeNetworkCheckedAt).toLocaleString()}</dd></div>
                      <div><dt>Earlier practice-network evidence expired</dt><dd>{requestInsuranceSource.previousPracticeNetworkEvidenceExpired ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Earlier treating-physician network check</dt><dd>{requestInsuranceSource.previousRenderingPhysicianNetworkChecked ? 'Performed' : 'Not performed'}</dd></div>
                      <div><dt>Earlier result reusable</dt><dd>{requestInsuranceSource.previousResultReusable ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Confirmation expires</dt><dd>{new Date(requestInsuranceSource.contextExpiresAt).toLocaleString()}</dd></div>
                    </dl>
                    <fieldset className="telehealth-fieldset">
                      <legend>Seven required confirmations</legend>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestInsuranceSourceConfirmations.payerProduct} onChange={(event) => updateRequestInsuranceSourceConfirmation('payerProduct', event.target.checked)} /><span>I confirm the displayed payer and product match the synthetic source I previously supplied.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestInsuranceSourceConfirmations.maskedMemberDetails} onChange={(event) => updateRequestInsuranceSourceConfirmation('maskedMemberDetails', event.target.checked)} /><span>I confirm the displayed masked member and optional group details match that source.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestInsuranceSourceConfirmations.subscriberRelationship} onChange={(event) => updateRequestInsuranceSourceConfirmation('subscriberRelationship', event.target.checked)} /><span>I confirm the displayed subscriber relationship.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestInsuranceSourceConfirmations.primaryCoverageSource} onChange={(event) => updateRequestInsuranceSourceConfirmation('primaryCoverageSource', event.target.checked)} /><span>I confirm this is the primary synthetic coverage source to carry into a future verification step.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestInsuranceSourceConfirmations.freshVerification} onChange={(event) => updateRequestInsuranceSourceConfirmation('freshVerification', event.target.checked)} /><span>I request a future fresh eligibility and network verification and understand this step performs none.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestInsuranceSourceConfirmations.evidenceLimitations} onChange={(event) => updateRequestInsuranceSourceConfirmation('evidenceLimitations', event.target.checked)} /><span>I understand every earlier eligibility and practice-network result is historical only and cannot be reused.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestInsuranceSourceConfirmations.syntheticData} onChange={(event) => updateRequestInsuranceSourceConfirmation('syntheticData', event.target.checked)} /><span>I confirm these are fictional synthetic demonstration details.</span></label>
                    </fieldset>
                    <ul>{requestInsuranceSource.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                    <button
                      className="telehealth-button"
                      type="submit"
                      disabled={submitting || !Object.values(requestInsuranceSourceConfirmations).every(Boolean)}
                    >
                      {submitting ? 'Confirming insurance source…' : 'Confirm synthetic insurance source'}
                    </button>
                  </form>
                ) : null}
                {requestInsuranceSource?.sourceConfirmed ? (
                  <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={requestInsuranceSourceResultRef}>
                    <h3>Request insurance source confirmed</h3>
                    <dl className="telehealth-details">
                      <div><dt>Request reference</dt><dd>{requestInsuranceSource.requestId}</dd></div>
                      <div><dt>Request status</dt><dd>{requestInsuranceSource.requestStatus}</dd></div>
                      <div><dt>Request version</dt><dd>{requestInsuranceSource.requestVersion}</dd></div>
                      <div><dt>Source</dt><dd>{requestInsuranceSource.payerDisplayName} — {requestInsuranceSource.productDisplayName}</dd></div>
                      <div><dt>Member ID</dt><dd>{requestInsuranceSource.maskedMemberId}</dd></div>
                      <div><dt>Fresh verification requested</dt><dd>{requestInsuranceSource.freshVerificationRequested ? 'Intent recorded; not performed' : 'No'}</dd></div>
                      <div><dt>Current coverage verified</dt><dd>{requestInsuranceSource.coverageVerified ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Exact network confirmed</dt><dd>{requestInsuranceSource.exactNetworkConfirmed ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Doctor search or queue</dt><dd>{requestInsuranceSource.doctorSearchStarted || requestInsuranceSource.patientCareQueueEntered || requestInsuranceSource.clinicianQueueEntered ? 'Started' : 'Not started'}</dd></div>
                      <div><dt>Confirmed</dt><dd>{requestInsuranceSource.confirmedAt ? new Date(requestInsuranceSource.confirmedAt).toLocaleString() : 'Recorded'}</dd></div>
                    </dl>
                    <p><strong>{requestInsuranceSource.direction}</strong></p>
                    <p>The protected member payload was referenced but was not copied or decrypted. No canonical coverage, selection, verification, financial route, operational review, contact, queue, appointment, encounter, consent, care, integration, or external action was created.</p>
                    <ul>{requestInsuranceSource.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                  </div>
                ) : null}
                {requestInsuranceSource?.sourceConfirmed && requestEligibilityLoading ? (
                  <p role="status">Loading fresh request eligibility…</p>
                ) : null}
                {requestInsuranceSource?.sourceConfirmed
                  && !requestEligibilityLoading
                  && !requestEligibility ? (
                    <button
                      className="telehealth-button"
                      type="button"
                      onClick={() => setRequestEligibilityLoadAttempt((value) => value + 1)}
                    >
                      Retry eligibility load
                    </button>
                  ) : null}
                {requestEligibility?.verificationReady ? (
                  <form className="telehealth-review-form" onSubmit={runRequestEligibility}>
                    <div className="telehealth-synthetic" role="note">
                      NON_PRODUCTION eligibility fixture only. No payer or clearinghouse will be contacted.
                    </div>
                    <h3>Run fresh request eligibility</h3>
                    <p id="request-eligibility-help">This check validates the protected synthetic source in server memory. It does not verify whether the practice or eventual treating physician is in network.</p>
                    <dl className="telehealth-details" aria-describedby="request-eligibility-help">
                      <div><dt>Payer and product</dt><dd>{requestEligibility.payerDisplayName} — {requestEligibility.productDisplayName}</dd></div>
                      <div><dt>Member ID</dt><dd>{requestEligibility.maskedMemberId}</dd></div>
                      <div><dt>Group number</dt><dd>{requestEligibility.maskedGroupNumber ?? 'Not supplied'}</dd></div>
                      <div><dt>Subscriber relationship</dt><dd>{requestEligibility.subscriberRelationship}</dd></div>
                      <div><dt>Current state</dt><dd>{requestEligibility.currentLocationStateCode}</dd></div>
                      <div><dt>Visit purpose</dt><dd>{requestEligibility.purposeCategory}</dd></div>
                      <div><dt>Request version</dt><dd>{requestEligibility.requestVersion}</dd></div>
                      <div><dt>Run before</dt><dd>{new Date(requestEligibility.contextExpiresAt).toLocaleString()}</dd></div>
                    </dl>
                    <fieldset className="telehealth-fieldset">
                      <legend>Two required acknowledgments</legend>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestEligibilityAcknowledgments.syntheticData} onChange={(event) => updateRequestEligibilityAcknowledgment('syntheticData', event.target.checked)} /><span>I confirm this check uses only fictional synthetic demonstration data.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestEligibilityAcknowledgments.noGuarantee} onChange={(event) => updateRequestEligibilityAcknowledgment('noGuarantee', event.target.checked)} /><span>I understand eligibility or benefit information is not a guarantee of coverage, payment, cost, or network participation.</span></label>
                    </fieldset>
                    <ul>{requestEligibility.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                    <button
                      className="telehealth-button"
                      type="submit"
                      disabled={submitting || !Object.values(requestEligibilityAcknowledgments).every(Boolean)}
                    >
                      {submitting ? 'Running fresh eligibility…' : 'Run synthetic eligibility check'}
                    </button>
                  </form>
                ) : null}
                {requestEligibility?.verificationCompleted ? (
                  <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={requestEligibilityResultRef}>
                    <h3>Fresh request eligibility recorded</h3>
                    <dl className="telehealth-details">
                      <div><dt>Request reference</dt><dd>{requestEligibility.requestId}</dd></div>
                      <div><dt>Request status</dt><dd>{requestEligibility.requestStatus}</dd></div>
                      <div><dt>Request version</dt><dd>{requestEligibility.requestVersion}</dd></div>
                      <div><dt>Adapter</dt><dd>{requestEligibility.adapterMode}</dd></div>
                      <div><dt>Eligibility</dt><dd>{requestEligibility.eligibilityStatus}</dd></div>
                      <div><dt>Benefit information</dt><dd>{requestEligibility.benefitInformationStatus}</dd></div>
                      <div><dt>Member match</dt><dd>{requestEligibility.memberMatchStatus}</dd></div>
                      <div><dt>Business result</dt><dd>{requestEligibility.businessOutcome}</dd></div>
                      <div><dt>Date of service</dt><dd>{requestEligibility.dateOfService}</dd></div>
                      <div><dt>Evidence expires</dt><dd>{requestEligibility.expiresAt ? new Date(requestEligibility.expiresAt).toLocaleString() : 'Unavailable'}</dd></div>
                      <div><dt>Exact network confirmed</dt><dd>{requestEligibility.exactNetworkConfirmed ? 'Yes' : 'No — still pending'}</dd></div>
                      <div><dt>Coverage verified</dt><dd>{requestEligibility.coverageVerified ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Doctor search or queue</dt><dd>{requestEligibility.doctorSearchStarted || requestEligibility.patientCareQueueEntered || requestEligibility.clinicianQueueEntered ? 'Started' : 'Not started'}</dd></div>
                    </dl>
                    <p><strong>{requestEligibility.direction}</strong></p>
                    <p>The protected payload was decrypted only in server memory and was not copied. No raw transaction, network determination, canonical coverage, financial route, operational review, contact, queue, appointment, encounter, consent, care, integration, or external action was created.</p>
                    <ul>{requestEligibility.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                  </div>
                ) : null}
                {requestEligibility?.verificationCompleted
                  && requestEligibility.businessOutcome === 'EligibleBenefitsReported'
                  && requestPracticeNetworkLoading ? (
                    <p role="status">Loading fresh practice-network verification…</p>
                  ) : null}
                {requestEligibility?.verificationCompleted
                  && requestEligibility.businessOutcome === 'EligibleBenefitsReported'
                  && !requestPracticeNetworkLoading
                  && !requestPracticeNetwork ? (
                    <button
                      className="telehealth-button"
                      type="button"
                      onClick={() => setRequestPracticeNetworkLoadAttempt((value) => value + 1)}
                    >
                      Retry practice-network load
                    </button>
                  ) : null}
                {requestPracticeNetwork?.verificationReady ? (
                  <form className="telehealth-review-form" onSubmit={runRequestPracticeNetwork}>
                    <div className="telehealth-synthetic" role="note">
                      NON_PRODUCTION practice/facility/service fixture only. No external directory or payer will be contacted.
                    </div>
                    <h3>Verify the practice-level network fixture</h3>
                    <p id="request-practice-network-help">This checks the configured practice, facility, service, state, date, and synthetic plan. It cannot establish exact network status because no rendering physician has been selected.</p>
                    <dl className="telehealth-details" aria-describedby="request-practice-network-help">
                      <div><dt>Practice</dt><dd>{requestPracticeNetwork.practiceDisplayName}</dd></div>
                      <div><dt>Payer and product</dt><dd>{requestPracticeNetwork.payerDisplayName} — {requestPracticeNetwork.productDisplayName}</dd></div>
                      <div><dt>Current state</dt><dd>{requestPracticeNetwork.currentLocationStateCode}</dd></div>
                      <div><dt>Visit purpose</dt><dd>{requestPracticeNetwork.purposeCategory}</dd></div>
                      <div><dt>Eligibility context</dt><dd>{requestPracticeNetwork.eligibilityBusinessOutcome}</dd></div>
                      <div><dt>Request version</dt><dd>{requestPracticeNetwork.requestVersion}</dd></div>
                      <div><dt>Run before</dt><dd>{new Date(requestPracticeNetwork.contextExpiresAt).toLocaleString()}</dd></div>
                    </dl>
                    <fieldset className="telehealth-fieldset">
                      <legend>Three required acknowledgments</legend>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestPracticeNetworkAcknowledgments.syntheticData} onChange={(event) => updateRequestPracticeNetworkAcknowledgment('syntheticData', event.target.checked)} /><span>I confirm this check uses only fictional synthetic demonstration data.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestPracticeNetworkAcknowledgments.practiceOnly} onChange={(event) => updateRequestPracticeNetworkAcknowledgment('practiceOnly', event.target.checked)} /><span>I understand this result covers only the configured practice/facility/service fixture and does not select or check a rendering physician.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestPracticeNetworkAcknowledgments.noGuarantee} onChange={(event) => updateRequestPracticeNetworkAcknowledgment('noGuarantee', event.target.checked)} /><span>I understand practice-level network evidence is not a guarantee of coverage, payment, cost, physician participation, or an appointment.</span></label>
                    </fieldset>
                    <ul>{requestPracticeNetwork.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                    <button
                      className="telehealth-button"
                      type="submit"
                      disabled={submitting || !Object.values(requestPracticeNetworkAcknowledgments).every(Boolean)}
                    >
                      {submitting ? 'Running practice-network verification…' : 'Run synthetic practice-network check'}
                    </button>
                  </form>
                ) : null}
                {requestPracticeNetwork?.verificationCompleted ? (
                  <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={requestPracticeNetworkResultRef}>
                    <h3>Fresh practice-network result recorded</h3>
                    <dl className="telehealth-details">
                      <div><dt>Request reference</dt><dd>{requestPracticeNetwork.requestId}</dd></div>
                      <div><dt>Request status</dt><dd>{requestPracticeNetwork.requestStatus}</dd></div>
                      <div><dt>Request version</dt><dd>{requestPracticeNetwork.requestVersion}</dd></div>
                      <div><dt>Practice</dt><dd>{requestPracticeNetwork.practiceDisplayName}</dd></div>
                      <div><dt>Plan-network match</dt><dd>{requestPracticeNetwork.planNetworkMatchStatus}</dd></div>
                      <div><dt>Practice affiliation</dt><dd>{requestPracticeNetwork.practiceAffiliationStatus}</dd></div>
                      <div><dt>Telehealth service</dt><dd>{requestPracticeNetwork.serviceAvailabilityStatus}</dd></div>
                      <div><dt>Accepting new patients</dt><dd>{requestPracticeNetwork.newPatientAcceptanceStatus}</dd></div>
                      <div><dt>Business result</dt><dd>{requestPracticeNetwork.businessOutcome}</dd></div>
                      <div><dt>Composite evidence expires</dt><dd>{requestPracticeNetwork.evidenceExpiresAt ? new Date(requestPracticeNetwork.evidenceExpiresAt).toLocaleString() : 'Unavailable'}</dd></div>
                      <div><dt>Rendering physician selected</dt><dd>{requestPracticeNetwork.renderingPhysicianSelected ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Exact network confirmed</dt><dd>{requestPracticeNetwork.exactNetworkConfirmed ? 'Yes' : 'No — physician check still required'}</dd></div>
                      <div><dt>Doctor search or queue</dt><dd>{requestPracticeNetwork.doctorSearchStarted || requestPracticeNetwork.patientCareQueueEntered || requestPracticeNetwork.clinicianQueueEntered ? 'Started' : 'Not started'}</dd></div>
                    </dl>
                    <p><strong>{requestPracticeNetwork.direction}</strong></p>
                    <p>No rendering physician, canonical coverage, financial route, operational review, contact, queue, appointment, encounter, consent, care, integration, or external action was created.</p>
                    <ul>{requestPracticeNetwork.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                  </div>
                ) : null}
                {requestPracticeNetwork?.verificationCompleted
                  && requestPracticeNetwork.businessOutcome === 'PracticeInNetworkAcceptingNewPatients'
                  && requestRenderingCandidateLoading ? (
                    <p role="status">Loading a rendering candidate for network evaluation…</p>
                  ) : null}
                {requestPracticeNetwork?.verificationCompleted
                  && requestPracticeNetwork.businessOutcome === 'PracticeInNetworkAcceptingNewPatients'
                  && !requestRenderingCandidateLoading
                  && !requestRenderingCandidate ? (
                    <button
                      className="telehealth-button"
                      type="button"
                      onClick={() => setRequestRenderingCandidateLoadAttempt((value) => value + 1)}
                    >
                      Retry rendering-candidate load
                    </button>
                  ) : null}
                {requestRenderingCandidate?.selectionReady ? (
                  <form className="telehealth-review-form" onSubmit={selectRequestRenderingCandidate}>
                    <div className="telehealth-synthetic" role="note">
                      NON_PRODUCTION synthetic roster candidate only. No clinician, payer, directory, or credentialing source will be contacted.
                    </div>
                    <h3>Review a candidate for network evaluation</h3>
                    <p id="request-rendering-candidate-help">This binds one state-specific synthetic clinician only as the subject of a future exact participation check. It does not assign the clinician or establish network status, licensure, credentials, availability, or care.</p>
                    <dl className="telehealth-details" aria-describedby="request-rendering-candidate-help">
                      <div><dt>Candidate</dt><dd>{requestRenderingCandidate.candidateDisplayName}</dd></div>
                      <div><dt>Masked provider reference</dt><dd>{requestRenderingCandidate.maskedProviderReference}</dd></div>
                      <div><dt>Practice</dt><dd>{requestRenderingCandidate.practiceDisplayName}</dd></div>
                      <div><dt>Payer and product</dt><dd>{requestRenderingCandidate.payerDisplayName} — {requestRenderingCandidate.productDisplayName}</dd></div>
                      <div><dt>Current state</dt><dd>{requestRenderingCandidate.currentLocationStateCode}</dd></div>
                      <div><dt>Service and modality</dt><dd>{requestRenderingCandidate.serviceCategory} — {requestRenderingCandidate.modality}</dd></div>
                      <div><dt>Candidate purpose</dt><dd>{requestRenderingCandidate.candidatePurpose}</dd></div>
                      <div><dt>Request version</dt><dd>{requestRenderingCandidate.requestVersion}</dd></div>
                      <div><dt>Select before</dt><dd>{new Date(requestRenderingCandidate.contextExpiresAt).toLocaleString()}</dd></div>
                    </dl>
                    <fieldset className="telehealth-fieldset">
                      <legend>Four required acknowledgments</legend>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestRenderingCandidateAcknowledgments.syntheticData} onChange={(event) => updateRequestRenderingCandidateAcknowledgment('syntheticData', event.target.checked)} /><span>I confirm this is a fictional synthetic demonstration candidate.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestRenderingCandidateAcknowledgments.candidateOnly} onChange={(event) => updateRequestRenderingCandidateAcknowledgment('candidateOnly', event.target.checked)} /><span>I understand this selection identifies only the subject of a future network evaluation.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestRenderingCandidateAcknowledgments.noAssignment} onChange={(event) => updateRequestRenderingCandidateAcknowledgment('noAssignment', event.target.checked)} /><span>I understand this does not assign a clinician or promise availability, credentials, licensure, an appointment, or care.</span></label>
                      <label className="telehealth-check"><input required type="checkbox" checked={requestRenderingCandidateAcknowledgments.networkCheckStillRequired} onChange={(event) => updateRequestRenderingCandidateAcknowledgment('networkCheckStillRequired', event.target.checked)} /><span>I understand exact billing-entity and rendering-physician network participation is still unchecked.</span></label>
                    </fieldset>
                    <ul>{requestRenderingCandidate.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                    <button
                      className="telehealth-button"
                      type="submit"
                      disabled={submitting || !Object.values(requestRenderingCandidateAcknowledgments).every(Boolean)}
                    >
                      {submitting ? 'Selecting candidate…' : 'Select candidate for network evaluation'}
                    </button>
                  </form>
                ) : null}
                {requestRenderingCandidate?.selectionCompleted ? (
                  <div className="telehealth-coverage-result" role="status" tabIndex={-1} ref={requestRenderingCandidateResultRef}>
                    <h3>Rendering candidate selected for network evaluation</h3>
                    <dl className="telehealth-details">
                      <div><dt>Request reference</dt><dd>{requestRenderingCandidate.requestId}</dd></div>
                      <div><dt>Request status</dt><dd>{requestRenderingCandidate.requestStatus}</dd></div>
                      <div><dt>Request version</dt><dd>{requestRenderingCandidate.requestVersion}</dd></div>
                      <div><dt>Candidate</dt><dd>{requestRenderingCandidate.candidateDisplayName}</dd></div>
                      <div><dt>Masked provider reference</dt><dd>{requestRenderingCandidate.maskedProviderReference}</dd></div>
                      <div><dt>Candidate selected for network evaluation</dt><dd>{requestRenderingCandidate.candidateSelectedForNetworkEvaluation ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Clinician assigned</dt><dd>{requestRenderingCandidate.renderingPhysicianAssigned ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Physician network checked</dt><dd>{requestRenderingCandidate.renderingPhysicianNetworkChecked ? 'Yes' : 'No — still required'}</dd></div>
                      <div><dt>Exact network confirmed</dt><dd>{requestRenderingCandidate.exactNetworkConfirmed ? 'Yes' : 'No'}</dd></div>
                      <div><dt>Doctor search or queue</dt><dd>{requestRenderingCandidate.doctorSearchStarted || requestRenderingCandidate.patientCareQueueEntered || requestRenderingCandidate.clinicianQueueEntered ? 'Started' : 'Not started'}</dd></div>
                      <div><dt>Selected</dt><dd>{requestRenderingCandidate.selectedAt ? new Date(requestRenderingCandidate.selectedAt).toLocaleString() : 'Recorded'}</dd></div>
                    </dl>
                    <p><strong>{requestRenderingCandidate.direction}</strong></p>
                    <p>No clinician assignment, exact network result, canonical coverage, financial route, operational review, contact, queue, appointment, encounter, consent, care, integration, or external action was created.</p>
                    <ul>{requestRenderingCandidate.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
                  </div>
                ) : null}
              </>
            ) : null}
            {applicant.status === 'VerificationLocked' || applicant.status === 'Expired' ? (
              <p className="telehealth-inline-warning">This synthetic applicant cannot continue. Start again to obtain a new short-lived credential.</p>
            ) : null}
            <ul>{applicant.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul>
            <button className="telehealth-button telehealth-button-secondary" type="button" disabled={loading} onClick={() => setResumeAttempt((value) => value + 1)}>Reload applicant status</button>
            <button className="telehealth-button telehealth-button-secondary" type="button" onClick={restart}>Start again</button>
          </div>
        ) : null}
      </section>
    </main>
  )
}
