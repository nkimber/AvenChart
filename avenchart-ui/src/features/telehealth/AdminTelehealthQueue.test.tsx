// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import AdminTelehealthQueue from './AdminTelehealthQueue.tsx'
import {
  authorizeApplicantRequestToQueue,
  authorizeApplicantPracticeReview,
  authorizeRequest,
  claimApplicantPracticeReview,
  executeApplicantSyntheticPromotion,
  getApplicantRequestQueueAuthorization,
  getApplicantPracticeReviewPacket,
  listApplicantIdentityReview,
  listApplicantPracticeReviewInbox,
  listApplicantPromotionAuthorization,
  listApplicantSyntheticPromotion,
  listOperationalReview,
  recordApplicantIdentityReview,
  recordApplicantPromotionAuthorization,
  type TelehealthApplicantPromotionAuthorizationItem,
  type TelehealthApplicantSyntheticPromotionItem,
} from './api.ts'

vi.mock('./api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('./api.ts')>()
  return {
    ...original,
    authorizeApplicantRequestToQueue: vi.fn(),
    authorizeApplicantPracticeReview: vi.fn(),
    authorizeRequest: vi.fn(),
    claimApplicantPracticeReview: vi.fn(),
    executeApplicantSyntheticPromotion: vi.fn(),
    getApplicantRequestQueueAuthorization: vi.fn(),
    getApplicantPracticeReviewPacket: vi.fn(),
    listApplicantIdentityReview: vi.fn(),
    listApplicantPracticeReviewInbox: vi.fn(),
    listApplicantPromotionAuthorization: vi.fn(),
    listApplicantSyntheticPromotion: vi.fn(),
    listOperationalReview: vi.fn(),
    recordApplicantIdentityReview: vi.fn(),
    recordApplicantPromotionAuthorization: vi.fn(),
  }
})

const promotionApplicant: TelehealthApplicantPromotionAuthorizationItem = {
  applicantId: '60000000-0000-4000-8000-000000000006',
  version: 10,
  status: 'SyntheticIdentityProofingRecorded',
  legalFirstName: 'Riley',
  legalLastName: 'Synthetic',
  dateOfBirth: '1992-04-12',
  maskedEmail: 'r***@example.test',
  maskedPhone: '*******0166',
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
  proofingCheckedAt: '2026-08-27T12:00:00Z',
  proofingExpiresAt: '2026-08-27T12:15:00Z',
  createdAt: '2026-08-27T11:40:00Z',
  applicantExpiresAt: '2026-08-27T13:40:00Z',
  allowedDecisions: ['AuthorizedForSyntheticPromotion', 'DeniedForSyntheticPromotion'],
}

const syntheticPromotionApplicant: TelehealthApplicantSyntheticPromotionItem = {
  applicantId: '90000000-0000-4000-8000-000000000009',
  version: 11,
  status: 'SyntheticPromotionAuthorized',
  legalFirstName: 'Jordan',
  legalLastName: 'Atomic',
  dateOfBirth: '1991-05-20',
  maskedEmail: 'j***@example.test',
  maskedPhone: '*******0177',
  residenceStateCode: 'GA',
  postalCode: '30301',
  authorizationDecision: 'AuthorizedForSyntheticPromotion',
  authorizedAt: '2026-08-27T12:01:00Z',
  assuranceLevelAchieved: 'None',
  identityProofed: false,
  applicantExpiresAt: '2026-08-27T13:40:00Z',
  proofingExpiresAt: '2026-08-27T12:15:00Z',
  allowedCommand: 'PromoteAuthorizedSyntheticApplicant',
}

describe('AdminTelehealthQueue', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(listApplicantIdentityReview).mockResolvedValue({
      practiceDisplayName: 'Synthetic Practice',
      serverTime: '2026-08-27T12:00:00Z',
      applicants: [],
      limitations: ['Contact control is not identity proofing.'],
    })
    vi.mocked(listApplicantPracticeReviewInbox).mockResolvedValue({
      policyKey: 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_INBOX',
      policyVersion: 1,
      practiceDisplayName: 'Synthetic Practice',
      serverTime: '2026-08-28T12:00:00Z',
      items: [],
      limitations: ['Read-only operational awareness.'],
    })
    vi.mocked(listApplicantPromotionAuthorization).mockResolvedValue({
      practiceDisplayName: 'Synthetic Practice',
      serverTime: '2026-08-27T12:00:00Z',
      applicants: [],
      limitations: ['Assurance remains None.'],
    })
    vi.mocked(listApplicantSyntheticPromotion).mockResolvedValue({
      practiceDisplayName: 'Synthetic Practice',
      serverTime: '2026-08-27T12:00:00Z',
      applicants: [],
      limitations: ['No portal or care capability is created.'],
    })
  })

  it('removes stale authorization actions after a failed refresh', async () => {
    vi.mocked(listOperationalReview)
      .mockResolvedValueOnce([{
        requestId: '10000000-0000-4000-8000-000000000001',
        status: 'OperationalReview',
        complaintCategory: 'migraine',
        triageOutcome: 'TelehealthEligible',
        version: 3,
        createdAt: '2026-08-26T12:00:00Z',
        applicantOriginated: false,
      }])
      .mockRejectedValueOnce(new Error('Queue unavailable'))
    vi.mocked(authorizeRequest).mockResolvedValue({} as never)

    render(<AdminTelehealthQueue />)
    expect(await screen.findByRole('button', { name: 'Authorize to clinician queue' })).toBeEnabled()

    fireEvent.click(screen.getByRole('button', { name: 'Refresh all' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Queue unavailable')
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Authorize to clinician queue' })).not.toBeInTheDocument())
    expect(screen.getByRole('button', { name: 'Try again' })).toBeEnabled()
  })

  it('uses the dedicated evidence-bound authorization for an applicant-originated request', async () => {
    const item = {
      requestId: '12000000-0000-4000-8000-000000000012',
      status: 'OperationalReview' as const,
      complaintCategory: 'migraine' as const,
      triageOutcome: 'TelehealthEligible',
      version: 12,
      createdAt: '2026-08-29T12:00:00Z',
      applicantOriginated: true,
    }
    const packet = {
      requestId: item.requestId,
      requestVersion: 12,
      requestStatus: 'OperationalReview',
      policyKey: 'SYNTHETIC_APPLICANT_REQUEST_QUEUE_AUTHORIZATION',
      policyVersion: 1,
      sourceMode: 'NON_PRODUCTION',
      compatibilityTarget: 'AVENCHART_SYNTHETIC_QUEUE_AUTHORIZATION_V1',
      authorizationSnapshotFingerprint: 'a'.repeat(64),
      resultValidThrough: '2026-08-29T13:00:00Z',
      practiceDisplayName: 'AvenChart Synthetic Practice',
      payerDisplayName: 'Harbor Mutual',
      productDisplayName: 'Synthetic Choice',
      currentLocationStateCode: 'GA',
      purposeCategory: 'migraine',
      dateOfService: '2026-08-29',
      candidateDisplayName: 'Dr. Synthetic',
      maskedProviderReference: 'Synthetic provider ••••1234',
      maskedBillingProviderReference: 'Synthetic billing provider ••••8800',
      serviceCategory: 'ProfessionalTelehealthConsultation',
      modality: 'RealTimeAudioVideo',
      authorizationReady: true,
      authorizationCompleted: false,
      authorizedAt: null,
      businessOutcome: null,
      direction: 'Review the bounded evidence.',
      limitations: ['No real coverage verification.'],
    }
    vi.mocked(listOperationalReview).mockResolvedValueOnce([item]).mockResolvedValue([])
    vi.mocked(getApplicantRequestQueueAuthorization).mockResolvedValue(packet as never)
    vi.mocked(authorizeApplicantRequestToQueue).mockResolvedValue({ ...packet, requestVersion: 13, requestStatus: 'Queued' } as never)

    render(<AdminTelehealthQueue />)

    expect(await screen.findByText('New-patient applicant request', { exact: false })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Authorize to clinician queue' })).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Review applicant queue authorization' }))
    expect(await screen.findByRole('heading', { name: 'Applicant request queue authorization' })).toHaveFocus()
    const acknowledgments = screen.getAllByRole('checkbox')
    expect(acknowledgments).toHaveLength(4)
    acknowledgments.forEach((acknowledgment) => expect(acknowledgment).not.toBeChecked())
    const authorize = screen.getByRole('button', { name: 'Accept into synthetic clinician queue' })
    expect(authorize).toBeDisabled()
    acknowledgments.forEach((acknowledgment) => fireEvent.click(acknowledgment))
    expect(authorize).toBeEnabled()
    fireEvent.click(authorize)

    await waitFor(() => expect(authorizeApplicantRequestToQueue).toHaveBeenCalledWith(
      item.requestId,
      {
        expectedRequestVersion: 12,
        authorizationSnapshotFingerprint: 'a'.repeat(64),
        syntheticEvidenceReviewed: true,
        noCoverageGuaranteeAcknowledged: true,
        practiceAcceptsForQueueAcknowledged: true,
        queueNotCareAcknowledged: true,
      },
      expect.any(String),
    ))
    expect(await screen.findByText('No requests are awaiting operational review.')).toBeInTheDocument()
  })

  it('renders a minimized pending practice-review item with only a bounded claim action', async () => {
    vi.mocked(listOperationalReview).mockResolvedValue([])
    vi.mocked(listApplicantPracticeReviewInbox).mockResolvedValue({
      policyKey: 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_INBOX',
      policyVersion: 1,
      practiceDisplayName: 'Synthetic Practice',
      serverTime: '2026-08-28T12:00:00Z',
      limitations: ['No review action, assignment, priority, or response-time promise is available.'],
      items: [{
        practiceReviewCaseId: '35000000-0000-4000-8000-000000000035',
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
        submittedAt: '2026-08-28T11:59:00Z',
        staffReviewWorkItemExists: true,
        staffActionTaken: false,
        assigned: false,
        assignedToCurrentUser: false,
        assignmentExpiresAt: null,
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
    })

    render(<AdminTelehealthQueue />)

    expect(await screen.findByText('Avery Pending')).toBeInTheDocument()
    expect(screen.getByText('Headache or known migraine pattern · universal safety screen passed')).toBeInTheDocument()
    expect(screen.getByText(/These are not telehealth requests or patient or clinician care-queue entries/i)).toBeInTheDocument()
    expect(screen.getByText(/no priority, accept, decline, contact, request, queue, appointment/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Claim for review' })).toBeDisabled()
    expect(screen.queryByRole('button', { name: /accept|decline|assign|authorize/i })).not.toBeInTheDocument()
    expect(document.body.textContent).not.toContain('35000000-0000-4000-8000-000000000035')
  })

  it('claims a pending review only after all three limitations are acknowledged', async () => {
    vi.mocked(listOperationalReview).mockResolvedValue([])
    const item = {
      practiceReviewCaseId: '37000000-0000-4000-8000-000000000037', applicantVersion: 24,
      applicantStatus: 'SyntheticPracticeReviewSubmitted' as const, reviewStatus: 'PendingPracticeReview' as const,
      legalFirstName: 'Taylor', legalLastName: 'Claim', dateOfBirth: '1987-05-06',
      maskedEmail: 't•••@example.test', maskedPhone: '(***) ***-0177', residenceStateCode: 'CA' as const, postalCode: '94105',
      purposeCategory: 'sleep' as const, purposeDisplayLabel: 'Sleep difficulty', safetyOutcome: 'TelehealthEligible' as const,
      reviewRoute: 'PendingPracticePreRequestReview' as const, sections: [], submittedAt: '2026-08-28T11:59:00Z',
      staffReviewWorkItemExists: true as const, staffActionTaken: false, assigned: false,
      assignedToCurrentUser: false, assignmentExpiresAt: null,
      priorityAssigned: false as const, practiceAccepted: false as const, practiceDeclined: false as const,
      patientContacted: false as const, clinicianReviewCreated: false as const, telehealthRequestCreated: false as const,
      patientCareQueueEntered: false as const, clinicianQueueEntered: false as const, appointmentCreated: false as const,
      encounterCreated: false as const, careAuthorized: false as const, prescribingEnabled: false as const,
      billingEnabled: false as const, claimCreated: false as const, integrationEnabled: false as const,
      externalCallPerformed: false as const,
    }
    vi.mocked(listApplicantPracticeReviewInbox).mockResolvedValue({
      policyKey: 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_INBOX', policyVersion: 1,
      practiceDisplayName: 'Synthetic Practice', serverTime: '2026-08-28T12:00:00Z',
      items: [item], limitations: [],
    })
    vi.mocked(claimApplicantPracticeReview).mockResolvedValue({} as never)

    render(<AdminTelehealthQueue />)
    const claimButton = await screen.findByRole('button', { name: 'Claim for review' })
    fireEvent.click(screen.getByLabelText(/not an accept, decline, or clinical decision/i))
    fireEvent.click(screen.getByLabelText(/does not contact the patient/i))
    fireEvent.click(screen.getByLabelText(/creates no telehealth request or care queue/i))
    expect(claimButton).toBeEnabled()
    fireEvent.click(claimButton)

    await waitFor(() => expect(claimApplicantPracticeReview).toHaveBeenCalledOnce())
    expect(vi.mocked(claimApplicantPracticeReview).mock.calls[0]?.[0]).toBe(item.practiceReviewCaseId)
    expect(vi.mocked(claimApplicantPracticeReview).mock.calls[0]?.[1]).toEqual({
      expectedApplicantVersion: 24,
      inboxPolicyVersion: 1,
      noDecisionAcknowledged: true,
      noPatientContactAcknowledged: true,
      noRequestOrCareQueueAcknowledged: true,
    })
  })

  it('opens a minimized packet only for the claimant and records only the acknowledged bounded authorization', async () => {
    vi.mocked(listOperationalReview).mockResolvedValue([])
    const item = {
      practiceReviewCaseId: '38000000-0000-4000-8000-000000000038', applicantVersion: 24,
      applicantStatus: 'SyntheticPracticeReviewSubmitted' as const, reviewStatus: 'PendingPracticeReview' as const,
      legalFirstName: 'Casey', legalLastName: 'Packet', dateOfBirth: '1989-03-04',
      maskedEmail: 'c•••@example.test', maskedPhone: '(***) ***-0199', residenceStateCode: 'GA' as const, postalCode: '30303',
      purposeCategory: 'migraine' as const, purposeDisplayLabel: 'Migraine or recurring headache', safetyOutcome: 'TelehealthEligible' as const,
      reviewRoute: 'PendingPracticePreRequestReview' as const, sections: [], submittedAt: '2026-08-28T11:59:00Z',
      staffReviewWorkItemExists: true as const, staffActionTaken: true as const, assigned: true as const,
      assignedToCurrentUser: true as const, assignmentExpiresAt: '2026-08-28T12:02:00Z',
      priorityAssigned: false as const, practiceAccepted: false as const, practiceDeclined: false as const,
      patientContacted: false as const, clinicianReviewCreated: false as const, telehealthRequestCreated: false as const,
      patientCareQueueEntered: false as const, clinicianQueueEntered: false as const, appointmentCreated: false as const,
      encounterCreated: false as const, careAuthorized: false as const, prescribingEnabled: false as const,
      billingEnabled: false as const, claimCreated: false as const, integrationEnabled: false as const,
      externalCallPerformed: false as const,
    }
    vi.mocked(listApplicantPracticeReviewInbox).mockResolvedValueOnce({
      policyKey: 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_INBOX', policyVersion: 1,
      practiceDisplayName: 'Synthetic Practice', serverTime: '2026-08-28T12:00:00Z',
      items: [item], limitations: [],
    }).mockResolvedValue({
      policyKey: 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_INBOX', policyVersion: 1,
      practiceDisplayName: 'Synthetic Practice', serverTime: '2026-08-28T12:01:00Z',
      items: [], limitations: [],
    })
    vi.mocked(getApplicantPracticeReviewPacket).mockResolvedValue({
      ...item,
      policyKey: 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_PACKET', policyVersion: 1,
      practiceDisplayName: 'Synthetic Practice', serverTime: '2026-08-28T12:00:00Z',
      assignmentExpiresAt: '2026-08-28T12:02:00Z',
      registration: { receiptRecorded: true, confirmedAt: '2026-08-28T11:00:00Z', identityAssuranceEstablished: false, patientRecordChanged: false },
      insurance: {
        payerDisplayName: 'Harbor Mutual', productDisplayName: 'High Deductible',
        memberIdMask: '••••A123', groupNumberMask: '••••B456', subscriberRelationship: 'Self', coveragePriority: 'Primary',
        eligibilityBusinessOutcome: 'EligibleBenefitsReported', eligibilityCheckedAt: '2026-08-28T11:00:00Z', eligibilityExpiresAt: '2026-08-28T13:00:00Z', eligibilityEvidenceCurrent: true,
        practiceNetworkBusinessOutcome: 'PracticeInNetworkAcceptingNewPatients', practiceNetworkCheckedAt: '2026-08-28T11:01:00Z', practiceNetworkExpiresAt: '2026-08-28T13:00:00Z', practiceNetworkEvidenceCurrent: true,
        renderingPhysicianNetworkChecked: false, confirmedAt: '2026-08-28T11:02:00Z', coverageVerified: false, exactNetworkConfirmed: false, canonicalCoverageCreated: false,
      },
      communicationAccess: {
        preferredSpokenLanguage: 'English', interpreterRequested: false, accessibilitySupportRequested: true,
        safePrivateCommunicationConfirmed: true, recordedAt: '2026-08-28T11:03:00Z',
        interpreterAssigned: false, accessibilityAccommodationArranged: false, communicationArrangementCompleted: false,
      },
      devicePreparation: {
        browserSupported: true, cameraAvailable: true, microphoneAvailable: true, speakerAvailable: true,
        networkQuality: 'Good', recordedAt: '2026-08-28T11:04:00Z', technologyReady: false, waitingRoomCreated: false, mediaSessionCreated: false,
      },
      clinicalInformationSummaryRoute: 'ClinicianClinicalInformationReviewRequired',
      clinicalInformationSummaryConfirmedAt: '2026-08-28T11:55:00Z',
      limitations: ['Synthetic operational evidence only.'],
    })
    vi.mocked(authorizeApplicantPracticeReview).mockResolvedValue({} as never)
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('41000000-0000-4000-8000-000000000041')

    render(<AdminTelehealthQueue />)
    const openButton = await screen.findByRole('button', { name: 'Open operational review packet' })
    fireEvent.click(openButton)

    expect(await screen.findByRole('heading', { name: 'Claimant-only operational review packet' })).toBeInTheDocument()
    expect(getApplicantPracticeReviewPacket).toHaveBeenCalledWith(item.practiceReviewCaseId)
    expect(screen.getByText(/Harbor Mutual · High Deductible/)).toBeInTheDocument()
    expect(screen.getByText(/rendering physician not checked/i)).toBeInTheDocument()
    expect(screen.getByText(/no clinical selections or patient chart shown/i)).toBeInTheDocument()
    const authorizeButton = screen.getByRole('button', { name: 'Authorize later request creation' })
    expect(authorizeButton).toBeDisabled()
    expect(screen.queryByRole('button', { name: /accept|decline|contact patient|enter queue/i })).not.toBeInTheDocument()
    expect(document.body.textContent).not.toMatch(/A123456|B456789|patientId|applicantId/)

    fireEvent.click(screen.getByLabelText(/not a clinical eligibility decision/i))
    fireEvent.click(screen.getByLabelText(/not a coverage guarantee/i))
    fireEvent.click(screen.getByLabelText(/creates no request, queue, appointment/i))
    expect(authorizeButton).toBeEnabled()
    fireEvent.click(authorizeButton)

    await waitFor(() => expect(authorizeApplicantPracticeReview).toHaveBeenCalledOnce())
    expect(authorizeApplicantPracticeReview).toHaveBeenCalledWith(item.practiceReviewCaseId, {
      expectedApplicantVersion: 24,
      packetPolicyVersion: 1,
      decision: 'AuthorizedForSyntheticRequestCreation',
      rationaleCode: 'OperationalPrerequisitesReviewed',
      noClinicalEligibilityAcknowledged: true,
      noCoverageGuaranteeAcknowledged: true,
      noRequestOrQueueAcknowledged: true,
    }, '41000000-0000-4000-8000-000000000041')
    expect(await screen.findByText('No synthetic work items are pending practice review.')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Claimant-only operational review packet' })).not.toBeInTheDocument()
  })

  it('clears a stale practice-review item and exposes an independent retry after refresh failure', async () => {
    vi.mocked(listOperationalReview).mockResolvedValue([])
    const item = {
      practiceReviewCaseId: '36000000-0000-4000-8000-000000000036', applicantVersion: 24,
      applicantStatus: 'SyntheticPracticeReviewSubmitted' as const, reviewStatus: 'PendingPracticeReview' as const,
      legalFirstName: 'Morgan', legalLastName: 'Refresh', dateOfBirth: '1988-01-02',
      maskedEmail: 'm•••@example.test', maskedPhone: '(***) ***-0188', residenceStateCode: 'FL' as const, postalCode: '33101',
      purposeCategory: 'sleep' as const, purposeDisplayLabel: 'Sleep difficulty', safetyOutcome: 'TelehealthEligible' as const,
      reviewRoute: 'PendingPracticePreRequestReview' as const, sections: [], submittedAt: '2026-08-28T11:59:00Z',
      staffReviewWorkItemExists: true as const, staffActionTaken: false as const, assigned: false as const,
      assignedToCurrentUser: false, assignmentExpiresAt: null,
      priorityAssigned: false as const, practiceAccepted: false as const, practiceDeclined: false as const,
      patientContacted: false as const, clinicianReviewCreated: false as const, telehealthRequestCreated: false as const,
      patientCareQueueEntered: false as const, clinicianQueueEntered: false as const, appointmentCreated: false as const,
      encounterCreated: false as const, careAuthorized: false as const, prescribingEnabled: false as const,
      billingEnabled: false as const, claimCreated: false as const, integrationEnabled: false as const,
      externalCallPerformed: false as const,
    }
    vi.mocked(listApplicantPracticeReviewInbox)
      .mockResolvedValueOnce({ policyKey: 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_INBOX', policyVersion: 1, practiceDisplayName: 'Synthetic Practice', serverTime: '2026-08-28T12:00:00Z', items: [item], limitations: [] })
      .mockRejectedValueOnce(new Error('Practice review unavailable'))
      .mockResolvedValue({ policyKey: 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_INBOX', policyVersion: 1, practiceDisplayName: 'Synthetic Practice', serverTime: '2026-08-28T12:01:00Z', items: [], limitations: [] })

    render(<AdminTelehealthQueue />)
    await screen.findByText('Morgan Refresh')
    fireEvent.click(screen.getByRole('button', { name: 'Refresh all' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Practice review unavailable')
    expect(screen.queryByText('Morgan Refresh')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Reload practice-review inbox' }))
    expect(await screen.findByText('No synthetic work items are pending practice review.')).toBeInTheDocument()
  })

  it('records only the server-derived bounded applicant decision and keeps the applicant prospective', async () => {
    vi.mocked(listOperationalReview).mockResolvedValue([])
    vi.mocked(listApplicantIdentityReview)
      .mockResolvedValueOnce({
        practiceDisplayName: 'Synthetic Practice',
        serverTime: '2026-08-27T12:00:00Z',
        limitations: ['The applicant remains prospective.'],
        applicants: [{
          applicantId: '20000000-0000-4000-8000-000000000002',
          version: 2,
          status: 'IdentityReviewPending',
          legalFirstName: 'Avery',
          legalLastName: 'Example',
          dateOfBirth: '1990-03-14',
          maskedEmail: 'a***@example.test',
          maskedPhone: '*******0199',
          residenceStateCode: 'GA',
          postalCode: '30301',
          contactVerifiedAt: '2026-08-27T11:59:00Z',
          duplicateDisposition: 'NoCandidate',
          allowedDecision: 'ApprovedForProspectiveIntake',
          createdAt: '2026-08-27T11:55:00Z',
          expiresAt: '2026-08-27T13:55:00Z',
        }],
      })
      .mockResolvedValue({
        practiceDisplayName: 'Synthetic Practice',
        serverTime: '2026-08-27T12:01:00Z',
        limitations: ['The applicant remains prospective.'],
        applicants: [],
      })
    vi.mocked(recordApplicantIdentityReview).mockResolvedValue({
      decisionId: '30000000-0000-4000-8000-000000000003',
      applicantId: '20000000-0000-4000-8000-000000000002',
      applicantVersion: 3,
      applicantStatus: 'IdentityReviewApproved',
      decision: 'ApprovedForProspectiveIntake',
      reason: 'Synthetic evidence reviewed by authorized staff.',
      policyKey: 'SYNTHETIC_STAFF_IDENTITY_REVIEW',
      policyVersion: 1,
      evidenceType: 'CONTACT_CONTROL_AND_DUPLICATE_DISPOSITION_ONLY',
      decidedAt: '2026-08-27T12:00:30Z',
      identityProofed: false,
      canonicalPatientCreated: false,
      chartLinked: false,
      prospectiveIntakeCompleted: false,
      requestCreated: false,
      queueEnabled: false,
      limitations: ['The applicant remains prospective.'],
    })

    render(<AdminTelehealthQueue />)
    expect(await screen.findByText('Avery Example')).toBeInTheDocument()
    expect(screen.getByText(/Possible matching patient information is never shown/i)).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText('Review reason'), { target: { value: 'Synthetic evidence reviewed by authorized staff.' } })
    fireEvent.click(screen.getByLabelText(/I confirm this uses synthetic data/i))
    fireEvent.click(screen.getByRole('button', { name: 'Approve for later prospective intake' }))

    await waitFor(() => expect(recordApplicantIdentityReview).toHaveBeenCalledOnce())
    expect(vi.mocked(recordApplicantIdentityReview).mock.calls[0][1]).toEqual({
      expectedVersion: 2,
      decision: 'ApprovedForProspectiveIntake',
      reason: 'Synthetic evidence reviewed by authorized staff.',
      syntheticDataConfirmed: true,
    })
    expect(await screen.findByText('No applicants are awaiting bounded identity review.')).toBeInTheDocument()
  })

  it('retains the unchanged command and retry identity after an ambiguous review failure', async () => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('40000000-0000-4000-8000-000000000004')
    vi.mocked(listOperationalReview).mockResolvedValue([])
    vi.mocked(listApplicantIdentityReview).mockResolvedValue({
      practiceDisplayName: 'Synthetic Practice', serverTime: '2026-08-27T12:00:00Z', limitations: [],
      applicants: [{
        applicantId: '50000000-0000-4000-8000-000000000005', version: 2, status: 'IdentityReviewPending',
        legalFirstName: 'Morgan', legalLastName: 'Retry', dateOfBirth: '1988-01-02',
        maskedEmail: 'm***@example.test', maskedPhone: '*******0188', residenceStateCode: 'FL', postalCode: '33101',
        contactVerifiedAt: '2026-08-27T11:59:00Z', duplicateDisposition: 'PossibleMatchManualReview',
        allowedDecision: 'ManualReviewRequired', createdAt: '2026-08-27T11:55:00Z', expiresAt: '2026-08-27T13:55:00Z',
      }],
    })
    vi.mocked(recordApplicantIdentityReview)
      .mockRejectedValueOnce(new Error('Result unknown; retry unchanged.'))
      .mockResolvedValue({} as never)

    render(<AdminTelehealthQueue />)
    await screen.findByText('Morgan Retry')
    fireEvent.change(screen.getByLabelText('Review reason'), { target: { value: 'Synthetic possible match requires separate review.' } })
    fireEvent.click(screen.getByLabelText(/I confirm this uses synthetic data/i))
    const button = screen.getByRole('button', { name: 'Require separate manual review' })
    fireEvent.click(button)

    expect(await screen.findByRole('alert')).toHaveTextContent('Result unknown; retry unchanged.')
    expect(screen.getByLabelText('Review reason')).toHaveValue('Synthetic possible match requires separate review.')
    fireEvent.click(screen.getByRole('button', { name: 'Require separate manual review' }))

    await waitFor(() => expect(recordApplicantIdentityReview).toHaveBeenCalledTimes(2))
    expect(vi.mocked(recordApplicantIdentityReview).mock.calls[0][2]).toBe('40000000-0000-4000-8000-000000000004')
    expect(vi.mocked(recordApplicantIdentityReview).mock.calls[1][2]).toBe('40000000-0000-4000-8000-000000000004')
  })

  it('records only a synthetic promotion authorization with both explicit acknowledgments', async () => {
    vi.mocked(listOperationalReview).mockResolvedValue([])
    vi.mocked(listApplicantPromotionAuthorization)
      .mockResolvedValueOnce({
        practiceDisplayName: 'Synthetic Practice', serverTime: '2026-08-27T12:00:00Z',
        applicants: [promotionApplicant], limitations: ['No patient is created.'],
      })
      .mockResolvedValue({
        practiceDisplayName: 'Synthetic Practice', serverTime: '2026-08-27T12:01:00Z',
        applicants: [], limitations: ['No patient is created.'],
      })
    vi.mocked(recordApplicantPromotionAuthorization).mockResolvedValue({
      decisionId: '70000000-0000-4000-8000-000000000007',
      applicantId: promotionApplicant.applicantId,
      applicantVersion: 11,
      applicantStatus: 'SyntheticPromotionAuthorized',
      decision: 'AuthorizedForSyntheticPromotion',
      reason: 'Complete synthetic chain reviewed by authorized staff.',
      policyKey: 'SYNTHETIC_PROSPECTIVE_PROMOTION_AUTHORIZATION',
      policyVersion: 1,
      evidenceType: 'COMPLETE_SYNTHETIC_INTAKE_AND_PROCESS_STATUS_ONLY',
      decidedAt: '2026-08-27T12:01:00Z',
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
    })

    render(<AdminTelehealthQueue />)
    await screen.findByText('Riley Synthetic')
    expect(screen.getByText(/assurance None · identity proved: no/i)).toBeInTheDocument()
    const submit = screen.getByRole('button', { name: 'Record promotion decision' })
    expect(submit).toBeDisabled()
    fireEvent.change(screen.getByLabelText('Promotion decision reason'), {
      target: { value: 'Complete synthetic chain reviewed by authorized staff.' },
    })
    fireEvent.click(screen.getByLabelText(/I acknowledge assurance is None/i))
    fireEvent.click(screen.getByLabelText(/I confirm this uses synthetic data and creates no patient/i))
    fireEvent.click(submit)

    await waitFor(() => expect(recordApplicantPromotionAuthorization).toHaveBeenCalledOnce())
    expect(vi.mocked(recordApplicantPromotionAuthorization).mock.calls[0][1]).toEqual({
      expectedVersion: 10,
      decision: 'AuthorizedForSyntheticPromotion',
      reason: 'Complete synthetic chain reviewed by authorized staff.',
      noneAssuranceAcknowledged: true,
      syntheticDataConfirmed: true,
    })
    expect(await screen.findByText('No applicants are awaiting synthetic promotion authorization.')).toBeInTheDocument()
  })

  it('preserves a denied promotion command and retry key after an ambiguous failure', async () => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('80000000-0000-4000-8000-000000000008')
    vi.mocked(listOperationalReview).mockResolvedValue([])
    vi.mocked(listApplicantPromotionAuthorization).mockResolvedValue({
      practiceDisplayName: 'Synthetic Practice', serverTime: '2026-08-27T12:00:00Z',
      applicants: [promotionApplicant], limitations: [],
    })
    vi.mocked(recordApplicantPromotionAuthorization)
      .mockRejectedValueOnce(new Error('Promotion result unknown; retry unchanged.'))
      .mockResolvedValue({} as never)

    render(<AdminTelehealthQueue />)
    await screen.findByText('Riley Synthetic')
    fireEvent.click(screen.getByLabelText('Deny synthetic promotion'))
    fireEvent.change(screen.getByLabelText('Promotion decision reason'), {
      target: { value: 'Synthetic promotion is not authorized at this time.' },
    })
    fireEvent.click(screen.getByLabelText(/I acknowledge assurance is None/i))
    fireEvent.click(screen.getByLabelText(/I confirm this uses synthetic data and creates no patient/i))
    fireEvent.click(screen.getByRole('button', { name: 'Record promotion decision' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Promotion result unknown; retry unchanged.')
    expect(screen.getByLabelText('Deny synthetic promotion')).toBeChecked()
    expect(screen.getByLabelText('Promotion decision reason')).toHaveValue('Synthetic promotion is not authorized at this time.')
    fireEvent.click(screen.getByRole('button', { name: 'Record promotion decision' }))

    await waitFor(() => expect(recordApplicantPromotionAuthorization).toHaveBeenCalledTimes(2))
    expect(vi.mocked(recordApplicantPromotionAuthorization).mock.calls[0][1].decision).toBe('DeniedForSyntheticPromotion')
    expect(vi.mocked(recordApplicantPromotionAuthorization).mock.calls[0][2]).toBe('80000000-0000-4000-8000-000000000008')
    expect(vi.mocked(recordApplicantPromotionAuthorization).mock.calls[1][2]).toBe('80000000-0000-4000-8000-000000000008')
  })

  it('executes only the acknowledged atomic synthetic patient-shell command', async () => {
    vi.mocked(listOperationalReview).mockResolvedValue([])
    vi.mocked(listApplicantSyntheticPromotion)
      .mockResolvedValueOnce({
        practiceDisplayName: 'Synthetic Practice', serverTime: '2026-08-27T12:00:00Z',
        applicants: [syntheticPromotionApplicant], limitations: ['A current duplicate recheck is mandatory.'],
      })
      .mockResolvedValue({
        practiceDisplayName: 'Synthetic Practice', serverTime: '2026-08-27T12:01:00Z',
        applicants: [], limitations: [],
      })
    vi.mocked(executeApplicantSyntheticPromotion).mockResolvedValue({
      promotionId: 'a0000000-0000-4000-8000-00000000000a',
      applicantId: syntheticPromotionApplicant.applicantId,
      applicantVersion: 12,
      applicantStatus: 'SyntheticPatientPromoted',
      outcome: 'SyntheticPatientCreated',
      possibleMatchDetected: false,
      canonicalPatientCreated: true,
      policyKey: 'SYNTHETIC_PROSPECTIVE_PATIENT_PROMOTION',
      policyVersion: 1,
      evidenceType: 'AUTHORIZED_SYNTHETIC_APPLICANT_AND_CURRENT_DUPLICATE_RECHECK',
      executedAt: '2026-08-27T12:02:00Z',
      portalAccountCreated: false,
      prospectiveIntakeCompleted: false,
      consentCreated: false,
      practiceAccepted: false,
      insuranceCreated: false,
      requestCreated: false,
      queueEnabled: false,
      careEnabled: false,
      limitations: ['No portal or care capability is created.'],
    })

    render(<AdminTelehealthQueue />)
    await screen.findByText('Jordan Atomic')
    const submit = screen.getByRole('button', { name: 'Run duplicate check and promote' })
    expect(submit).toBeDisabled()
    fireEvent.change(screen.getByLabelText('Atomic promotion reason'), {
      target: { value: 'Authorized synthetic patient shell transaction reviewed.' },
    })
    fireEvent.click(screen.getByLabelText(/a no-match result creates one minimal canonical synthetic patient shell/i))
    fireEvent.click(screen.getByLabelText(/creates no portal, completed intake, consent, coverage/i))
    fireEvent.click(submit)

    await waitFor(() => expect(executeApplicantSyntheticPromotion).toHaveBeenCalledOnce())
    expect(vi.mocked(executeApplicantSyntheticPromotion).mock.calls[0][1]).toEqual({
      expectedVersion: 11,
      command: 'PromoteAuthorizedSyntheticApplicant',
      reason: 'Authorized synthetic patient shell transaction reviewed.',
      canonicalPatientCreationAcknowledged: true,
      noPortalNoCareAcknowledged: true,
    })
    expect(await screen.findByText('No applicants are authorized for atomic synthetic promotion.')).toBeInTheDocument()
  })

  it('preserves the exact atomic promotion command and retry key after ambiguity', async () => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('b0000000-0000-4000-8000-00000000000b')
    vi.mocked(listOperationalReview).mockResolvedValue([])
    vi.mocked(listApplicantSyntheticPromotion).mockResolvedValue({
      practiceDisplayName: 'Synthetic Practice', serverTime: '2026-08-27T12:00:00Z',
      applicants: [syntheticPromotionApplicant], limitations: [],
    })
    vi.mocked(executeApplicantSyntheticPromotion)
      .mockRejectedValueOnce(new Error('Atomic result unknown; retry unchanged.'))
      .mockResolvedValue({} as never)

    render(<AdminTelehealthQueue />)
    await screen.findByText('Jordan Atomic')
    fireEvent.change(screen.getByLabelText('Atomic promotion reason'), {
      target: { value: 'Authorized synthetic patient shell transaction reviewed.' },
    })
    fireEvent.click(screen.getByLabelText(/a no-match result creates one minimal canonical synthetic patient shell/i))
    fireEvent.click(screen.getByLabelText(/creates no portal, completed intake, consent, coverage/i))
    fireEvent.click(screen.getByRole('button', { name: 'Run duplicate check and promote' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Atomic result unknown; retry unchanged.')
    expect(screen.getByLabelText('Atomic promotion reason')).toHaveValue('Authorized synthetic patient shell transaction reviewed.')
    fireEvent.click(screen.getByRole('button', { name: 'Run duplicate check and promote' }))

    await waitFor(() => expect(executeApplicantSyntheticPromotion).toHaveBeenCalledTimes(2))
    expect(vi.mocked(executeApplicantSyntheticPromotion).mock.calls[0][2]).toBe('b0000000-0000-4000-8000-00000000000b')
    expect(vi.mocked(executeApplicantSyntheticPromotion).mock.calls[1][2]).toBe('b0000000-0000-4000-8000-00000000000b')
  })
})
