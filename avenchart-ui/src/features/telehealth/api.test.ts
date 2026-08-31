// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { acknowledgeApplicantTelehealthNotice, assessApplicantTelehealthRequestComplaintTriage, assessApplicantTelehealthRequestUniversalSafety, authorizeApplicantPracticeReview, authorizeApplicantRequestToQueue, authorizeRequest, cancelPatientTelehealthRequest, claimApplicantPracticeReview, completePatientReadiness, confirmApplicantInsuranceHandoff, confirmApplicantRegistrationDetails, confirmApplicantTelehealthRequestInsuranceSource, confirmApplicantTelehealthRequestIntake, confirmApplicantTelehealthRequestLocation, confirmApplicantTelehealthRequestParticipationContext, createApplicantTelehealthRequest, createPatientRequest, createProspectiveApplicant, endIdleClinicianShift, enterTelehealthConsultationWrapUp, evaluateApplicantTelehealthRequestParticipation, evaluateProspectiveSafetyTriage, executeApplicantSyntheticPromotion, getApplicantInsuranceHandoff, getApplicantPracticeReviewPacket, getApplicantRegistrationDetails, getApplicantRequestQueueAuthorization, getApplicantSyntheticPostVisitReceipt, getApplicantTelehealthNotice, getApplicantTelehealthRequest, getApplicantTelehealthRequestComplaintTriage, getApplicantTelehealthRequestEligibility, getApplicantTelehealthRequestInsuranceSource, getApplicantTelehealthRequestIntake, getApplicantTelehealthRequestLocation, getApplicantTelehealthRequestOperationalReviewSubmission, getApplicantTelehealthRequestParticipationContext, getApplicantTelehealthRequestParticipationEvaluation, getApplicantTelehealthRequestPracticeNetwork, getApplicantTelehealthRequestQueueStatus, getApplicantTelehealthRequestRenderingCandidate, getApplicantTelehealthRequestUniversalSafety, getPatientQueueStatus, getPatientRequestHistory, getPatientSyntheticPostVisitReceipt, getProspectivePracticeNetworkOptions, getTelehealthCompletionPrerequisites, getTelehealthConsultationWorkspace, getTelehealthPharmacyChoices, getTelehealthPrescriptionPreparationDraft, getTelehealthSafetyDispositionDraft, listApplicantIdentityReview, listApplicantPracticeReviewInbox, listApplicantPromotionAuthorization, listApplicantSyntheticPromotion, listClinicianQueue, prepareApplicantConnection, preparePatientConnection, preparePhysicianConnection, prepareTelehealthProfessionalClaim, recordApplicantIdentityReview, recordApplicantPromotionAuthorization, recordProspectiveEligibility, recordProspectiveIdentityProofing, recordProspectiveMemberInsuranceDetails, recordProspectivePracticeNetwork, recordProspectivePracticeNetworkPrecheck, recordProspectiveVisitPurpose, recordTelehealthPharmacyChoice, recordTelehealthPrescriptionPreparationDraft, recordTelehealthSafetyDispositionDraft, reserveNextRequest, runApplicantTelehealthRequestEligibility, runApplicantTelehealthRequestPracticeNetwork, saveTelehealthConsultationDocumentationDraft, selectApplicantTelehealthRequestRenderingCandidate, startTelehealthConsultation, submitApplicantTelehealthRequestForOperationalReview, verifyPatientCoverage, verifyProspectiveApplicantContact, type TelehealthDevicePreflight, type TelehealthReadiness } from './api.ts'
import { getApplicantSyntheticAfterVisitPlanPreview, getPatientSyntheticAfterVisitPlanPreview } from './api.ts'

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } })
}

describe('telehealth transport boundaries', () => {
  const fetchMock = vi.fn<typeof fetch>()

  beforeEach(() => {
    sessionStorage.clear()
    fetchMock.mockReset()
    vi.stubGlobal('fetch', fetchMock)
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('00000000-0000-4000-8000-000000000001')
  })

  afterEach(() => vi.restoreAllMocks())

  it('binds patient creation to the stored portal session and a semantic idempotency key', async () => {
    sessionStorage.setItem('avenchart-ui.portalSession', JSON.stringify({ sessionId: 'portal-session', username: 'patient', portalUsername: 'patient', displayName: 'Synthetic Patient' }))
    fetchMock.mockResolvedValue(jsonResponse({ requestId: 'request-1', status: 'Draft' }, 201))

    await createPatientRequest('migraine')

    const [, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(headers.get('X-AvenChart-Patient-Portal-Session')).toBe('portal-session')
    expect(headers.get('X-Idempotency-Key')).toBe('00000000-0000-4000-8000-000000000001')
    expect(JSON.parse(String(init?.body))).toEqual({ complaintCategory: 'migraine' })
  })

  it('submits patient cancellation with the current version, explicit synthetic confirmation, and portal session', async () => {
    sessionStorage.setItem('avenchart-ui.portalSession', JSON.stringify({ sessionId: 'portal-session', username: 'patient', portalUsername: 'patient', displayName: 'Synthetic Patient' }))
    fetchMock.mockResolvedValue(jsonResponse({ requestId: 'request-1', status: 'Cancelled' }))

    await cancelPatientTelehealthRequest('request/1', 7)

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/patient/requests/request%2F1/cancel')
    expect(init?.method).toBe('POST')
    expect(headers.get('X-AvenChart-Patient-Portal-Session')).toBe('portal-session')
    expect(headers.get('X-Idempotency-Key')).toBe('00000000-0000-4000-8000-000000000001')
    expect(JSON.parse(String(init?.body))).toEqual({ expectedVersion: 7, syntheticCancellationConfirmed: true })
  })

  it('reads patient request history with only the portal session and no mutation identity', async () => {
    sessionStorage.setItem('avenchart-ui.portalSession', JSON.stringify({ sessionId: 'portal-session', username: 'patient', portalUsername: 'patient', displayName: 'Synthetic Patient' }))
    fetchMock.mockResolvedValue(jsonResponse({ requestId: 'request-1', entries: [] }))

    await getPatientRequestHistory('request/1')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/patient/requests/request%2F1/history')
    expect(init?.method).toBeUndefined()
    expect(headers.get('X-AvenChart-Patient-Portal-Session')).toBe('portal-session')
    expect(headers.get('X-Idempotency-Key')).toBeNull()
  })

  it('reads a post-visit receipt with only the portal session and no mutation identity', async () => {
    sessionStorage.setItem('avenchart-ui.portalSession', JSON.stringify({ sessionId: 'portal-session', username: 'patient', portalUsername: 'patient', displayName: 'Synthetic Patient' }))
    fetchMock.mockResolvedValue(jsonResponse({ receiptId: 'receipt-1', requestId: 'request-1', receiptState: 'AvailableInPortal' }))

    await getPatientSyntheticPostVisitReceipt('request/1')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/patient/requests/request%2F1/post-visit-receipt')
    expect(init?.method).toBeUndefined()
    expect(init?.body).toBeUndefined()
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Patient-Portal-Session')).toBe('portal-session')
    expect(headers.get('X-Idempotency-Key')).toBeNull()
  })

  it('reads an applicant post-visit receipt only with the applicant access key and no mutation identity', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ receiptId: 'receipt-1', requestId: 'request-1', receiptState: 'AvailableInPortal' }))

    await getApplicantSyntheticPostVisitReceipt('applicant/1', 'access-secret', 'request/1')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/applicants/applicant%2F1/telehealth-request/request%2F1/post-visit-receipt')
    expect(init?.method).toBeUndefined()
    expect(init?.body).toBeUndefined()
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(headers.get('X-Idempotency-Key')).toBeNull()
    expect(headers.get('X-AvenChart-Patient-Portal-Session')).toBeNull()
  })

  it('reads patient and applicant after-visit plan previews with only their respective read identities', async () => {
    sessionStorage.setItem('avenchart-ui.portalSession', JSON.stringify({ sessionId: 'portal-session', username: 'patient', portalUsername: 'patient', displayName: 'Synthetic Patient' }))
    fetchMock.mockImplementation(async () => jsonResponse({ previewId: 'preview-1', requestId: 'request-1', previewState: 'AvailableInPortal' }))

    await getPatientSyntheticAfterVisitPlanPreview('request/1')
    await getApplicantSyntheticAfterVisitPlanPreview('applicant/1', 'access-secret', 'request/1')

    const [patientUrl, patientInit] = fetchMock.mock.calls[0]
    const patientHeaders = new Headers(patientInit?.headers)
    expect(String(patientUrl)).toContain('/patient/requests/request%2F1/after-visit-plan-preview')
    expect(patientInit?.method).toBeUndefined()
    expect(patientInit?.body).toBeUndefined()
    expect(patientHeaders.get('X-AvenChart-Patient-Portal-Session')).toBe('portal-session')
    expect(patientHeaders.get('X-Idempotency-Key')).toBeNull()

    const [applicantUrl, applicantInit] = fetchMock.mock.calls[1]
    const applicantHeaders = new Headers(applicantInit?.headers)
    expect(String(applicantUrl)).toContain('/applicants/applicant%2F1/telehealth-request/request%2F1/after-visit-plan-preview')
    expect(applicantInit?.method).toBeUndefined()
    expect(applicantInit?.body).toBeUndefined()
    expect(applicantHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(applicantHeaders.get('X-AvenChart-Patient-Portal-Session')).toBeNull()
    expect(applicantHeaders.get('X-Idempotency-Key')).toBeNull()
  })

  it('keeps applicant Draft-request reads private and writes access-key bound with caller retry identity', async () => {
    fetchMock.mockImplementation(async () => jsonResponse({ requestCreated: false }))
    const input = {
      expectedApplicantVersion: 25,
      authorizationPolicyVersion: 1 as const,
      requestCreationConfirmed: true as const,
      noQueueOrCareAcknowledged: true as const,
      urgentOrWorseningSymptomsRequireImmediateActionAcknowledged: true as const,
    }

    await getApplicantTelehealthRequest('applicant/1', 'access-secret')
    await createApplicantTelehealthRequest(
      'applicant/1',
      'access-secret',
      input,
      'request-creation-retry-key',
    )

    const [getUrl, getInit] = fetchMock.mock.calls[0]
    const getHeaders = new Headers(getInit?.headers)
    expect(String(getUrl)).toContain('/applicants/applicant%2F1/telehealth-request')
    expect(getInit?.method).toBeUndefined()
    expect(getInit?.cache).toBe('no-store')
    expect(getInit?.body).toBeUndefined()
    expect(getHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(getHeaders.has('X-Idempotency-Key')).toBe(false)

    const [postUrl, postInit] = fetchMock.mock.calls[1]
    const postHeaders = new Headers(postInit?.headers)
    expect(String(postUrl)).toContain('/applicants/applicant%2F1/telehealth-request')
    expect(postInit?.method).toBe('POST')
    expect(postInit?.cache).toBe('no-store')
    expect(postHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(postHeaders.get('X-Idempotency-Key')).toBe('request-creation-retry-key')
    expect(JSON.parse(String(postInit?.body))).toEqual(input)
    expect(String(postInit?.body)).not.toMatch(/patientId|promotionId|caseId|authorizationId|complaint|priority|note/i)
  })

  it('keeps applicant request-location reads private and submits only bounded confirmation data with retry identity', async () => {
    fetchMock.mockImplementation(async () => jsonResponse({ locationConfirmed: false }))
    const input = {
      expectedRequestVersion: 1,
      contextSnapshotFingerprint: 'a'.repeat(64),
      currentLocationStateCode: 'GA' as const,
      currentLocationConfirmed: true as const,
      callbackNumberConfirmed: true as const,
      changedLocationRequiresRestartAcknowledged: true as const,
      urgentOrWorseningSymptomsRequireImmediateActionAcknowledged: true as const,
    }

    await getApplicantTelehealthRequestLocation('applicant/1', 'access-secret')
    await confirmApplicantTelehealthRequestLocation(
      'applicant/1',
      'access-secret',
      input,
      'request-location-retry-key',
    )

    const [getUrl, getInit] = fetchMock.mock.calls[0]
    const getHeaders = new Headers(getInit?.headers)
    expect(String(getUrl)).toContain('/applicants/applicant%2F1/telehealth-request/location')
    expect(getInit?.method).toBeUndefined()
    expect(getInit?.cache).toBe('no-store')
    expect(getInit?.body).toBeUndefined()
    expect(getHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(getHeaders.has('X-Idempotency-Key')).toBe(false)

    const [postUrl, postInit] = fetchMock.mock.calls[1]
    const postHeaders = new Headers(postInit?.headers)
    expect(String(postUrl)).toContain('/applicants/applicant%2F1/telehealth-request/location')
    expect(postInit?.method).toBe('POST')
    expect(postInit?.cache).toBe('no-store')
    expect(postHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(postHeaders.get('X-Idempotency-Key')).toBe('request-location-retry-key')
    expect(JSON.parse(String(postInit?.body))).toEqual(input)
    expect(String(postInit?.body)).not.toMatch(/patientId|callbackPhone|phoneNumber|clinical|complaint|priority|note/i)
  })

  it('keeps applicant request-safety reads private and submits only explicit bounded answers with retry identity', async () => {
    fetchMock.mockImplementation(async () => jsonResponse({ assessmentReady: true }))
    const input = {
      expectedRequestVersion: 2,
      contextSnapshotFingerprint: 'b'.repeat(64),
      currentLocationStateCode: 'CA' as const,
      currentLocationConfirmed: true as const,
      callbackNumberConfirmed: true as const,
      syntheticDataConfirmed: true as const,
      hasEmergencyWarning: false,
      severeOrWorsening: false,
      requiresHandsOnExam: false,
      unsure: true,
    }

    await getApplicantTelehealthRequestUniversalSafety('applicant/1', 'access-secret')
    await assessApplicantTelehealthRequestUniversalSafety(
      'applicant/1',
      'access-secret',
      input,
      'request-safety-retry-key',
    )

    const [getUrl, getInit] = fetchMock.mock.calls[0]
    const getHeaders = new Headers(getInit?.headers)
    expect(String(getUrl)).toContain('/applicants/applicant%2F1/telehealth-request/safety')
    expect(getInit?.method).toBeUndefined()
    expect(getInit?.cache).toBe('no-store')
    expect(getInit?.body).toBeUndefined()
    expect(getHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(getHeaders.has('X-Idempotency-Key')).toBe(false)

    const [postUrl, postInit] = fetchMock.mock.calls[1]
    const postHeaders = new Headers(postInit?.headers)
    expect(String(postUrl)).toContain('/applicants/applicant%2F1/telehealth-request/safety')
    expect(postInit?.method).toBe('POST')
    expect(postInit?.cache).toBe('no-store')
    expect(postHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(postHeaders.get('X-Idempotency-Key')).toBe('request-safety-retry-key')
    expect(JSON.parse(String(postInit?.body))).toEqual(input)
    expect(String(postInit?.body)).not.toMatch(/patientId|phoneNumber|freeText|complaint|priority|note/i)
  })

  it('keeps complaint-triage reads private and submits only one coded category answer set with retry identity', async () => {
    fetchMock.mockImplementation(async () => jsonResponse({ assessmentReady: true }))
    const input = {
      expectedRequestVersion: 3,
      contextSnapshotFingerprint: 'c'.repeat(64),
      currentLocationStateCode: 'FL' as const,
      currentLocationConfirmed: true as const,
      callbackNumberConfirmed: true as const,
      syntheticDataConfirmed: true as const,
      migraine: {
        suddenOrWorstOnset: 'No' as const,
        newNeurologicOrVisionChange: 'No' as const,
        feverOrStiffNeck: 'No' as const,
        recentHeadInjury: 'NotSure' as const,
        pregnantOrPostpartum: 'No' as const,
        cancerOrImmunocompromised: 'No' as const,
        knownSimilarPattern: 'Yes' as const,
        persistentVomiting: 'No' as const,
      },
      sleep: null,
    }

    await getApplicantTelehealthRequestComplaintTriage('applicant/1', 'access-secret')
    await assessApplicantTelehealthRequestComplaintTriage(
      'applicant/1',
      'access-secret',
      input,
      'complaint-triage-retry-key',
    )

    const [getUrl, getInit] = fetchMock.mock.calls[0]
    const getHeaders = new Headers(getInit?.headers)
    expect(String(getUrl)).toContain('/applicants/applicant%2F1/telehealth-request/complaint-triage')
    expect(getInit?.method).toBeUndefined()
    expect(getInit?.cache).toBe('no-store')
    expect(getInit?.body).toBeUndefined()
    expect(getHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(getHeaders.has('X-Idempotency-Key')).toBe(false)

    const [postUrl, postInit] = fetchMock.mock.calls[1]
    const postHeaders = new Headers(postInit?.headers)
    expect(String(postUrl)).toContain('/applicants/applicant%2F1/telehealth-request/complaint-triage')
    expect(postInit?.method).toBe('POST')
    expect(postInit?.cache).toBe('no-store')
    expect(postHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(postHeaders.get('X-Idempotency-Key')).toBe('complaint-triage-retry-key')
    expect(JSON.parse(String(postInit?.body))).toEqual(input)
    expect(String(postInit?.body)).not.toMatch(/patientId|phoneNumber|freeText|diagnosis|outcome|rule|reason|priority|note/i)
  })

  it('keeps applicant intake private and submits only duration plus eight confirmations with retry identity', async () => {
    fetchMock.mockImplementation(async () => jsonResponse({ snapshotReady: true }))
    const input = {
      expectedRequestVersion: 4,
      contextSnapshotFingerprint: 'd'.repeat(64),
      currentLocationStateCode: 'CA' as const,
      symptomDuration: '1-3-days' as const,
      currentLocationConfirmed: true as const,
      callbackNumberConfirmed: true as const,
      priorInformationReviewed: true as const,
      insuranceLimitationsAcknowledged: true as const,
      pendingConsentAcknowledged: true as const,
      pendingVerificationAcknowledged: true as const,
      complaintResultAcknowledged: true as const,
      syntheticDataConfirmed: true as const,
    }

    await getApplicantTelehealthRequestIntake('applicant/1', 'access-secret')
    await confirmApplicantTelehealthRequestIntake(
      'applicant/1',
      'access-secret',
      input,
      'intake-retry-key',
    )

    const [getUrl, getInit] = fetchMock.mock.calls[0]
    const getHeaders = new Headers(getInit?.headers)
    expect(String(getUrl)).toContain('/applicants/applicant%2F1/telehealth-request/intake')
    expect(getInit?.method).toBeUndefined()
    expect(getInit?.cache).toBe('no-store')
    expect(getInit?.body).toBeUndefined()
    expect(getHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(getHeaders.has('X-Idempotency-Key')).toBe(false)

    const [postUrl, postInit] = fetchMock.mock.calls[1]
    const postHeaders = new Headers(postInit?.headers)
    expect(String(postUrl)).toContain('/applicants/applicant%2F1/telehealth-request/intake')
    expect(postInit?.method).toBe('POST')
    expect(postInit?.cache).toBe('no-store')
    expect(postHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(postHeaders.get('X-Idempotency-Key')).toBe('intake-retry-key')
    expect(JSON.parse(String(postInit?.body))).toEqual(input)
    expect(String(postInit?.body)).not.toMatch(/patientId|phoneNumber|freeText|complaintCategory|diagnosis|treatment|outcome|rule|reason|priority|coverageResult|networkResult|note/i)
  })

  it('keeps request insurance source private and submits only the snapshot plus seven confirmations', async () => {
    fetchMock.mockImplementation(async () => jsonResponse({ sourceReady: true }))
    const input = {
      expectedRequestVersion: 5,
      insuranceSourceSnapshotFingerprint: 'e'.repeat(64),
      payerProductConfirmed: true as const,
      maskedMemberDetailsConfirmed: true as const,
      subscriberRelationshipConfirmed: true as const,
      primaryCoverageSourceConfirmed: true as const,
      freshVerificationRequested: true as const,
      evidenceLimitationsAcknowledged: true as const,
      syntheticDataConfirmed: true as const,
    }

    await getApplicantTelehealthRequestInsuranceSource('applicant/1', 'access-secret')
    await confirmApplicantTelehealthRequestInsuranceSource(
      'applicant/1',
      'access-secret',
      input,
      'insurance-source-retry-key',
    )

    const [getUrl, getInit] = fetchMock.mock.calls[0]
    const getHeaders = new Headers(getInit?.headers)
    expect(String(getUrl)).toContain('/applicants/applicant%2F1/telehealth-request/insurance-source')
    expect(getInit?.method).toBeUndefined()
    expect(getInit?.cache).toBe('no-store')
    expect(getInit?.body).toBeUndefined()
    expect(getHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(getHeaders.has('X-Idempotency-Key')).toBe(false)

    const [postUrl, postInit] = fetchMock.mock.calls[1]
    const postHeaders = new Headers(postInit?.headers)
    expect(String(postUrl)).toContain('/applicants/applicant%2F1/telehealth-request/insurance-source')
    expect(postInit?.method).toBe('POST')
    expect(postInit?.cache).toBe('no-store')
    expect(postHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(postHeaders.get('X-Idempotency-Key')).toBe('insurance-source-retry-key')
    expect(JSON.parse(String(postInit?.body))).toEqual(input)
    expect(String(postInit?.body)).not.toMatch(/"(?:memberId|groupNumber|payerDisplayName|productDisplayName|patientId|insuranceRecord|eligibilityOutcome|networkOutcome|freeText|note)"\s*:/i)
  })

  it('keeps fresh request eligibility private and submits only snapshot plus two acknowledgments', async () => {
    fetchMock.mockImplementation(async () => jsonResponse({ verificationReady: true }))
    const input = {
      expectedRequestVersion: 6,
      eligibilitySnapshotFingerprint: 'f'.repeat(64),
      syntheticDataConfirmed: true as const,
      noGuaranteeAcknowledged: true as const,
    }

    await getApplicantTelehealthRequestEligibility('applicant/1', 'access-secret')
    await runApplicantTelehealthRequestEligibility(
      'applicant/1',
      'access-secret',
      input,
      'request-eligibility-retry-key',
    )

    const [getUrl, getInit] = fetchMock.mock.calls[0]
    const getHeaders = new Headers(getInit?.headers)
    expect(String(getUrl)).toContain('/applicants/applicant%2F1/telehealth-request/eligibility')
    expect(getInit?.method).toBeUndefined()
    expect(getInit?.cache).toBe('no-store')
    expect(getInit?.body).toBeUndefined()
    expect(getHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(getHeaders.has('X-Idempotency-Key')).toBe(false)

    const [postUrl, postInit] = fetchMock.mock.calls[1]
    const postHeaders = new Headers(postInit?.headers)
    expect(String(postUrl)).toContain('/applicants/applicant%2F1/telehealth-request/eligibility')
    expect(postInit?.method).toBe('POST')
    expect(postInit?.cache).toBe('no-store')
    expect(postHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(postHeaders.get('X-Idempotency-Key')).toBe('request-eligibility-retry-key')
    expect(JSON.parse(String(postInit?.body))).toEqual(input)
    expect(String(postInit?.body)).not.toMatch(/"(?:memberId|groupNumber|subscriber|payer|product|patientId|protectedPayload|rawTransaction|network|benefit|freeText|note)"\s*:/i)
  })

  it('keeps request practice-network verification private and submits only snapshot plus three acknowledgments', async () => {
    fetchMock.mockImplementation(async () => jsonResponse({ verificationReady: true }))
    const input = {
      expectedRequestVersion: 7,
      networkSnapshotFingerprint: 'e'.repeat(64),
      syntheticDataConfirmed: true as const,
      practiceOnlyScopeAcknowledged: true as const,
      noGuaranteeAcknowledged: true as const,
    }

    await getApplicantTelehealthRequestPracticeNetwork('applicant/1', 'access-secret')
    await runApplicantTelehealthRequestPracticeNetwork(
      'applicant/1',
      'access-secret',
      input,
      'request-practice-network-retry-key',
    )

    const [getUrl, getInit] = fetchMock.mock.calls[0]
    const getHeaders = new Headers(getInit?.headers)
    expect(String(getUrl)).toContain('/applicants/applicant%2F1/telehealth-request/practice-network')
    expect(getInit?.method).toBeUndefined()
    expect(getInit?.cache).toBe('no-store')
    expect(getInit?.body).toBeUndefined()
    expect(getHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(getHeaders.has('X-Idempotency-Key')).toBe(false)

    const [postUrl, postInit] = fetchMock.mock.calls[1]
    const postHeaders = new Headers(postInit?.headers)
    expect(String(postUrl)).toContain('/applicants/applicant%2F1/telehealth-request/practice-network')
    expect(postInit?.method).toBe('POST')
    expect(postInit?.cache).toBe('no-store')
    expect(postHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(postHeaders.get('X-Idempotency-Key')).toBe('request-practice-network-retry-key')
    expect(JSON.parse(String(postInit?.body))).toEqual(input)
    expect(String(postInit?.body)).not.toMatch(/"(?:memberId|groupNumber|patientId|physician|npi|tin|price|queue|freeText|note)"\s*:/i)
  })

  it('selects a rendering candidate for network evaluation using only snapshot and four acknowledgments', async () => {
    fetchMock.mockImplementation(async () => jsonResponse({ selectionReady: true }))
    const input = {
      expectedRequestVersion: 8,
      candidateSnapshotFingerprint: 'f'.repeat(64),
      syntheticDataConfirmed: true as const,
      candidateOnlyScopeAcknowledged: true as const,
      noAssignmentAcknowledged: true as const,
      networkCheckStillRequiredAcknowledged: true as const,
    }

    await getApplicantTelehealthRequestRenderingCandidate('applicant/1', 'access-secret')
    await selectApplicantTelehealthRequestRenderingCandidate(
      'applicant/1',
      'access-secret',
      input,
      'rendering-candidate-retry-key',
    )

    const [getUrl, getInit] = fetchMock.mock.calls[0]
    const getHeaders = new Headers(getInit?.headers)
    expect(String(getUrl)).toContain('/applicants/applicant%2F1/telehealth-request/rendering-candidate')
    expect(getInit?.method).toBeUndefined()
    expect(getInit?.cache).toBe('no-store')
    expect(getInit?.body).toBeUndefined()
    expect(getHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(getHeaders.has('X-Idempotency-Key')).toBe(false)

    const [postUrl, postInit] = fetchMock.mock.calls[1]
    const postHeaders = new Headers(postInit?.headers)
    expect(String(postUrl)).toContain('/applicants/applicant%2F1/telehealth-request/rendering-candidate')
    expect(postInit?.method).toBe('POST')
    expect(postInit?.cache).toBe('no-store')
    expect(postHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(postHeaders.get('X-Idempotency-Key')).toBe('rendering-candidate-retry-key')
    expect(JSON.parse(String(postInit?.body))).toEqual(input)
    expect(String(postInit?.body)).not.toMatch(/"(?:staffId|provider|physician|npi|tin|payer|product|patientId|price|queue|freeText|note)"\s*:/i)
  })

  it('confirms participation prerequisites using only snapshot and four acknowledgments', async () => {
    fetchMock.mockImplementation(async () => jsonResponse({ confirmationReady: true }))
    const input = {
      expectedRequestVersion: 9,
      contextSnapshotFingerprint: 'e'.repeat(64),
      syntheticDataConfirmed: true as const,
      npiNotCredentialAcknowledged: true as const,
      realAuthorityNotVerifiedAcknowledged: true as const,
      exactParticipationStillRequiredAcknowledged: true as const,
    }

    await getApplicantTelehealthRequestParticipationContext('applicant/1', 'access-secret')
    await confirmApplicantTelehealthRequestParticipationContext(
      'applicant/1',
      'access-secret',
      input,
      'participation-context-retry-key',
    )

    const [getUrl, getInit] = fetchMock.mock.calls[0]
    const getHeaders = new Headers(getInit?.headers)
    expect(String(getUrl)).toContain('/applicants/applicant%2F1/telehealth-request/participation-context')
    expect(getInit?.method).toBeUndefined()
    expect(getInit?.cache).toBe('no-store')
    expect(getInit?.body).toBeUndefined()
    expect(getHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(getHeaders.has('X-Idempotency-Key')).toBe(false)

    const [postUrl, postInit] = fetchMock.mock.calls[1]
    const postHeaders = new Headers(postInit?.headers)
    expect(String(postUrl)).toContain('/applicants/applicant%2F1/telehealth-request/participation-context')
    expect(postInit?.method).toBe('POST')
    expect(postInit?.cache).toBe('no-store')
    expect(postHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(postHeaders.get('X-Idempotency-Key')).toBe('participation-context-retry-key')
    expect(JSON.parse(String(postInit?.body))).toEqual(input)
    expect(String(postInit?.body)).not.toMatch(/"(?:staffId|provider|physician|npi|tin|license|authority|contract|payer|product|patientId|price|queue|freeText|note)"\s*:/i)
  })

  it('requests exact synthetic participation evaluation using only snapshot and four acknowledgments', async () => {
    fetchMock.mockImplementation(async () => jsonResponse({ evaluationReady: true }))
    const input = {
      expectedRequestVersion: 10,
      evaluationSnapshotFingerprint: 'd'.repeat(64),
      syntheticDataConfirmed: true as const,
      exactTupleScopeAcknowledged: true as const,
      noCoverageGuaranteeAcknowledged: true as const,
      realVerificationStillRequiredAcknowledged: true as const,
    }

    await getApplicantTelehealthRequestParticipationEvaluation('applicant/1', 'access-secret')
    await evaluateApplicantTelehealthRequestParticipation(
      'applicant/1',
      'access-secret',
      input,
      'participation-evaluation-retry-key',
    )

    const [getUrl, getInit] = fetchMock.mock.calls[0]
    const getHeaders = new Headers(getInit?.headers)
    expect(String(getUrl)).toContain('/applicants/applicant%2F1/telehealth-request/participation-evaluation')
    expect(getInit?.method).toBeUndefined()
    expect(getInit?.cache).toBe('no-store')
    expect(getInit?.body).toBeUndefined()
    expect(getHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(getHeaders.has('X-Idempotency-Key')).toBe(false)

    const [postUrl, postInit] = fetchMock.mock.calls[1]
    const postHeaders = new Headers(postInit?.headers)
    expect(String(postUrl)).toContain('/applicants/applicant%2F1/telehealth-request/participation-evaluation')
    expect(postInit?.method).toBe('POST')
    expect(postInit?.cache).toBe('no-store')
    expect(postHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(postHeaders.get('X-Idempotency-Key')).toBe('participation-evaluation-retry-key')
    expect(JSON.parse(String(postInit?.body))).toEqual(input)
    expect(String(postInit?.body)).not.toMatch(/"(?:staffId|provider|physician|npi|tin|license|authority|contract|payer|product|network|location|service|modality|outcome|patientId|price|queue|freeText|note)"\s*:/i)
  })

  it('submits the applicant request for operational review using only snapshot and four acknowledgments', async () => {
    fetchMock.mockImplementation(async () => jsonResponse({ submissionReady: true }))
    const input = {
      expectedRequestVersion: 11,
      submissionSnapshotFingerprint: 'f'.repeat(64),
      syntheticEvidenceAcknowledged: true as const,
      noCoverageGuaranteeAcknowledged: true as const,
      practiceReviewPendingAcknowledged: true as const,
      noCareRelationshipAcknowledged: true as const,
    }

    await getApplicantTelehealthRequestOperationalReviewSubmission('applicant/1', 'access-secret')
    await submitApplicantTelehealthRequestForOperationalReview(
      'applicant/1',
      'access-secret',
      input,
      'operational-review-submission-retry-key',
    )

    const [getUrl, getInit] = fetchMock.mock.calls[0]
    const getHeaders = new Headers(getInit?.headers)
    expect(String(getUrl)).toContain('/applicants/applicant%2F1/telehealth-request/operational-review-submission')
    expect(getInit?.method).toBeUndefined()
    expect(getInit?.cache).toBe('no-store')
    expect(getInit?.body).toBeUndefined()
    expect(getHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(getHeaders.has('X-Idempotency-Key')).toBe(false)

    const [postUrl, postInit] = fetchMock.mock.calls[1]
    const postHeaders = new Headers(postInit?.headers)
    expect(String(postUrl)).toContain('/applicants/applicant%2F1/telehealth-request/operational-review-submission')
    expect(postInit?.method).toBe('POST')
    expect(postInit?.cache).toBe('no-store')
    expect(postHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(postHeaders.get('X-Idempotency-Key')).toBe('operational-review-submission-retry-key')
    expect(JSON.parse(String(postInit?.body))).toEqual(input)
    expect(String(postInit?.body)).not.toMatch(/"(?:staffId|provider|physician|npi|tin|license|authority|contract|payer|product|network|location|service|modality|outcome|patientId|price|queue|freeText|note)"\s*:/i)
  })

  it('reads applicant queue status with only the applicant access key and no mutation identity', async () => {
    fetchMock.mockImplementation(async () => jsonResponse({
      requestId: 'request/53',
      requestStatus: 'Queued',
      approximateRequestsAhead: 2,
    }))

    await getApplicantTelehealthRequestQueueStatus('applicant/53', 'access-secret')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/applicants/applicant%2F53/telehealth-request/queue-status')
    expect(init?.method).toBeUndefined()
    expect(init?.cache).toBe('no-store')
    expect(init?.body).toBeUndefined()
    expect(headers.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('access-secret')
    expect(headers.has('X-Idempotency-Key')).toBe(false)
    expect(headers.has('X-AvenChart-Session')).toBe(false)
    expect(headers.has('X-AvenChart-Patient-Portal-Session')).toBe(false)
  })

  it('binds operational authorization to staff facility and purpose context', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'staff-session', username: 'admin', displayName: 'Admin', role: 'frontdesk', facilityId: 10, purposeOfUse: 'healthcare-operations' }))
    fetchMock.mockResolvedValue(jsonResponse({ requestId: 'request-1', status: 'Queued' }))

    await authorizeRequest('request-1', 3)

    const [, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(headers.get('X-AvenChart-Session')).toBe('staff-session')
    expect(headers.get('X-AvenChart-Facility-Id')).toBe('10')
    expect(headers.get('X-AvenChart-Purpose-Of-Use')).toBe('healthcare-operations')
    expect(JSON.parse(String(init?.body))).toEqual({ expectedVersion: 3 })
  })

  it('binds applicant-originated clinician queue reads and reservations to the treatment session', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'physician-session', username: 'doctor', displayName: 'Doctor', role: 'provider', facilityId: 10, purposeOfUse: 'treatment' }))
    fetchMock
      .mockResolvedValueOnce(jsonResponse({ requests: [{ requestId: 'request-54', status: 'Queued', applicantOriginated: true }] }))
      .mockResolvedValueOnce(jsonResponse({ reservationId: 'reservation-54', requestId: 'request-54', applicantOriginated: true }))

    const queue = await listClinicianQueue()
    const reservation = await reserveNextRequest()

    expect(queue[0]?.applicantOriginated).toBe(true)
    expect(reservation?.applicantOriginated).toBe(true)
    const [queueUrl, queueInit] = fetchMock.mock.calls[0]
    const queueHeaders = new Headers(queueInit?.headers)
    expect(String(queueUrl)).toContain('/clinician/queue')
    expect(queueInit?.method).toBeUndefined()
    expect(queueHeaders.get('X-AvenChart-Session')).toBe('physician-session')
    expect(queueHeaders.get('X-AvenChart-Facility-Id')).toBe('10')
    expect(queueHeaders.get('X-AvenChart-Purpose-Of-Use')).toBe('treatment')
    expect(queueHeaders.has('X-Idempotency-Key')).toBe(false)
    const [reserveUrl, reserveInit] = fetchMock.mock.calls[1]
    const reserveHeaders = new Headers(reserveInit?.headers)
    expect(String(reserveUrl)).toContain('/clinician/reservations/reserve-next')
    expect(reserveInit?.method).toBe('POST')
    expect(reserveHeaders.get('X-AvenChart-Session')).toBe('physician-session')
    expect(reserveHeaders.get('X-AvenChart-Purpose-Of-Use')).toBe('treatment')
    expect(reserveHeaders.get('X-Idempotency-Key')).toBe('00000000-0000-4000-8000-000000000001')
  })

  it('ends only an idle clinician shift with confirmations, version, and idempotency context', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'physician-session', username: 'doctor', displayName: 'Doctor', role: 'provider', facilityId: 10, purposeOfUse: 'treatment' }))
    fetchMock.mockResolvedValue(jsonResponse({ shiftId: 'shift-63', status: 'Ended', version: 3 }))
    await endIdleClinicianShift('shift-63', 2, true, true)
    const [url, init] = fetchMock.mock.calls[0]
    expect(String(url)).toContain('/clinician/shifts/shift-63/end')
    expect(init?.method).toBe('POST')
    expect(JSON.parse(String(init?.body))).toEqual({ expectedVersion: 2, noActiveWorkConfirmed: true, syntheticEndConfirmed: true })
    expect(new Headers(init?.headers).get('X-Idempotency-Key')).toBe('00000000-0000-4000-8000-000000000001')
  })

  it('uses the dedicated staff route and only snapshot plus four acknowledgments for applicant queue authorization', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'staff-session', username: 'admin', displayName: 'Admin', role: 'frontdesk', facilityId: 10, purposeOfUse: 'healthcare-operations' }))
    fetchMock.mockImplementation(async () => jsonResponse({ requestId: 'request/1', requestStatus: 'OperationalReview' }))
    const input = {
      expectedRequestVersion: 12,
      authorizationSnapshotFingerprint: 'a'.repeat(64),
      syntheticEvidenceReviewed: true as const,
      noCoverageGuaranteeAcknowledged: true as const,
      practiceAcceptsForQueueAcknowledged: true as const,
      queueNotCareAcknowledged: true as const,
    }

    await getApplicantRequestQueueAuthorization('request/1')
    await authorizeApplicantRequestToQueue('request/1', input, 'queue-authorization-retry-key')

    const [getUrl, getInit] = fetchMock.mock.calls[0]
    const getHeaders = new Headers(getInit?.headers)
    expect(String(getUrl)).toContain('/admin/applicant-requests/request%2F1/queue-authorization')
    expect(getInit?.method).toBeUndefined()
    expect(getInit?.cache).toBe('no-store')
    expect(getInit?.body).toBeUndefined()
    expect(getHeaders.get('X-AvenChart-Session')).toBe('staff-session')
    expect(getHeaders.get('X-AvenChart-Facility-Id')).toBe('10')
    expect(getHeaders.get('X-AvenChart-Purpose-Of-Use')).toBe('healthcare-operations')
    expect(getHeaders.has('X-Idempotency-Key')).toBe(false)

    const [postUrl, postInit] = fetchMock.mock.calls[1]
    const postHeaders = new Headers(postInit?.headers)
    expect(String(postUrl)).toContain('/admin/applicant-requests/request%2F1/queue-authorization')
    expect(postInit?.method).toBe('POST')
    expect(postInit?.cache).toBe('no-store')
    expect(postHeaders.get('X-Idempotency-Key')).toBe('queue-authorization-retry-key')
    expect(JSON.parse(String(postInit?.body))).toEqual(input)
    expect(String(postInit?.body)).not.toMatch(/"(?:staffId|provider|physician|npi|tin|license|authority|contract|payer|product|network|location|service|modality|outcome|patientId|price|queuePosition|freeText|note)"\s*:/i)
  })

  it('binds applicant review to staff context, an opaque applicant route, and explicit retry identity', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'staff-session', username: 'admin', displayName: 'Admin', role: 'frontdesk', facilityId: 10, purposeOfUse: 'treatment' }))
    fetchMock.mockImplementation(async () => jsonResponse({ applicants: [] }))

    await listApplicantIdentityReview()
    await recordApplicantIdentityReview('applicant/1', {
      expectedVersion: 2,
      decision: 'ManualReviewRequired',
      reason: 'Possible deterministic match requires separate review.',
      syntheticDataConfirmed: true,
    }, 'identity-review-retry-key')

    const [listUrl, listInit] = fetchMock.mock.calls[0]
    expect(String(listUrl)).toContain('/admin/applicant-identity-review')
    expect(listInit?.method).toBeUndefined()
    expect(listInit?.cache).toBe('no-store')
    expect(new Headers(listInit?.headers).has('X-Idempotency-Key')).toBe(false)

    const [writeUrl, writeInit] = fetchMock.mock.calls[1]
    const headers = new Headers(writeInit?.headers)
    expect(String(writeUrl)).toContain('/admin/applicants/applicant%2F1/identity-review-decision')
    expect(writeInit?.method).toBe('PUT')
    expect(writeInit?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Session')).toBe('staff-session')
    expect(headers.get('X-AvenChart-Facility-Id')).toBe('10')
    expect(headers.get('X-AvenChart-Purpose-Of-Use')).toBe('treatment')
    expect(headers.get('X-Idempotency-Key')).toBe('identity-review-retry-key')
    expect(JSON.parse(String(writeInit?.body))).toEqual({
      expectedVersion: 2,
      decision: 'ManualReviewRequired',
      reason: 'Possible deterministic match requires separate review.',
      syntheticDataConfirmed: true,
    })
    expect(String(writeInit?.body)).not.toMatch(/patientId|candidate|accessKey|evidenceFingerprint/i)
  })

  it('uses a GET-only no-store staff request for the practice-review inbox', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'staff-session', username: 'admin', displayName: 'Admin', role: 'frontdesk', facilityId: 10, purposeOfUse: 'healthcare-operations' }))
    fetchMock.mockResolvedValue(jsonResponse({ items: [], limitations: [] }))

    await listApplicantPracticeReviewInbox()

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/admin/applicant-practice-review')
    expect(init?.method).toBeUndefined()
    expect(init?.body).toBeUndefined()
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Session')).toBe('staff-session')
    expect(headers.get('X-AvenChart-Facility-Id')).toBe('10')
    expect(headers.get('X-AvenChart-Purpose-Of-Use')).toBe('healthcare-operations')
    expect(headers.has('X-Idempotency-Key')).toBe(false)
    expect(headers.has('X-Telehealth-Applicant-Access-Key')).toBe(false)
  })

  it('binds a practice-review claim to staff scope, exact acknowledgments, and caller-owned retry identity', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'staff-session', username: 'admin', displayName: 'Admin', role: 'frontdesk', facilityId: 10, purposeOfUse: 'healthcare-operations' }))
    fetchMock.mockResolvedValue(jsonResponse({ claimId: 'claim-1', assigned: true }))
    const input = {
      expectedApplicantVersion: 24,
      inboxPolicyVersion: 1 as const,
      noDecisionAcknowledged: true as const,
      noPatientContactAcknowledged: true as const,
      noRequestOrCareQueueAcknowledged: true as const,
    }

    await claimApplicantPracticeReview('case/1', input, 'practice-review-claim-retry-key')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/admin/applicant-practice-review/case%2F1/claim')
    expect(init?.method).toBe('POST')
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Session')).toBe('staff-session')
    expect(headers.get('X-AvenChart-Facility-Id')).toBe('10')
    expect(headers.get('X-AvenChart-Purpose-Of-Use')).toBe('healthcare-operations')
    expect(headers.get('X-Idempotency-Key')).toBe('practice-review-claim-retry-key')
    expect(JSON.parse(String(init?.body))).toEqual(input)
    expect(String(init?.body)).not.toMatch(/actor|staffId|patientId|applicantId|priority|decisionCode|contactInstruction/i)
  })

  it('reads one claimant-bound practice-review packet without a body or retry identity', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'staff-session', username: 'admin', displayName: 'Admin', role: 'frontdesk', facilityId: 10, purposeOfUse: 'healthcare-operations' }))
    fetchMock.mockResolvedValue(jsonResponse({ practiceReviewCaseId: 'case/1' }))

    await getApplicantPracticeReviewPacket('case/1')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/admin/applicant-practice-review/case%2F1')
    expect(String(url)).not.toContain('/claim')
    expect(init?.method).toBeUndefined()
    expect(init?.body).toBeUndefined()
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Session')).toBe('staff-session')
    expect(headers.get('X-AvenChart-Facility-Id')).toBe('10')
    expect(headers.get('X-AvenChart-Purpose-Of-Use')).toBe('healthcare-operations')
    expect(headers.has('X-Idempotency-Key')).toBe(false)
    expect(headers.has('X-Telehealth-Applicant-Access-Key')).toBe(false)
  })

  it('binds practice-review authorization to the exact controlled command and caller-owned retry identity', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'staff-session', username: 'admin', displayName: 'Admin', role: 'frontdesk', facilityId: 10, purposeOfUse: 'healthcare-operations' }))
    fetchMock.mockResolvedValue(jsonResponse({ decision: 'AuthorizedForSyntheticRequestCreation' }))
    const input = {
      expectedApplicantVersion: 24,
      packetPolicyVersion: 1 as const,
      decision: 'AuthorizedForSyntheticRequestCreation' as const,
      rationaleCode: 'OperationalPrerequisitesReviewed' as const,
      noClinicalEligibilityAcknowledged: true as const,
      noCoverageGuaranteeAcknowledged: true as const,
      noRequestOrQueueAcknowledged: true as const,
    }

    await authorizeApplicantPracticeReview('case/1', input, 'practice-review-authorization-retry-key')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/admin/applicant-practice-review/case%2F1/authorization')
    expect(init?.method).toBe('POST')
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Session')).toBe('staff-session')
    expect(headers.get('X-AvenChart-Facility-Id')).toBe('10')
    expect(headers.get('X-AvenChart-Purpose-Of-Use')).toBe('healthcare-operations')
    expect(headers.get('X-Idempotency-Key')).toBe('practice-review-authorization-retry-key')
    expect(JSON.parse(String(init?.body))).toEqual(input)
    expect(String(init?.body)).not.toMatch(/actor|staffId|patientId|applicantId|claimId|freeText|note/i)
  })

  it('binds synthetic promotion authorization to staff context and only the governed command', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'staff-session', username: 'admin', displayName: 'Admin', role: 'frontdesk', facilityId: 10, purposeOfUse: 'healthcare-operations' }))
    fetchMock.mockImplementation(async () => jsonResponse({ applicants: [] }))

    await listApplicantPromotionAuthorization()
    await recordApplicantPromotionAuthorization('applicant/2', {
      expectedVersion: 10,
      decision: 'DeniedForSyntheticPromotion',
      reason: 'Synthetic promotion is not authorized at this time.',
      noneAssuranceAcknowledged: true,
      syntheticDataConfirmed: true,
    }, 'promotion-authorization-retry-key')

    const [listUrl, listInit] = fetchMock.mock.calls[0]
    expect(String(listUrl)).toContain('/admin/applicant-promotion-authorization')
    expect(listInit?.method).toBeUndefined()
    expect(listInit?.cache).toBe('no-store')
    expect(new Headers(listInit?.headers).has('X-Idempotency-Key')).toBe(false)

    const [writeUrl, writeInit] = fetchMock.mock.calls[1]
    const headers = new Headers(writeInit?.headers)
    expect(String(writeUrl)).toContain('/admin/applicants/applicant%2F2/promotion-authorization-decision')
    expect(writeInit?.method).toBe('PUT')
    expect(writeInit?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Session')).toBe('staff-session')
    expect(headers.get('X-AvenChart-Facility-Id')).toBe('10')
    expect(headers.get('X-AvenChart-Purpose-Of-Use')).toBe('healthcare-operations')
    expect(headers.get('X-Idempotency-Key')).toBe('promotion-authorization-retry-key')
    expect(JSON.parse(String(writeInit?.body))).toEqual({
      expectedVersion: 10,
      decision: 'DeniedForSyntheticPromotion',
      reason: 'Synthetic promotion is not authorized at this time.',
      noneAssuranceAcknowledged: true,
      syntheticDataConfirmed: true,
    })
    expect(String(writeInit?.body)).not.toMatch(/patientId|memberId|government|biometric|proofingResult/i)
  })

  it('binds atomic synthetic promotion to administrator context and no client-authored patient facts', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'staff-session', username: 'admin', displayName: 'Admin', role: 'administrator', facilityId: 10, purposeOfUse: 'healthcare-operations' }))
    fetchMock.mockImplementation(async () => jsonResponse({ applicants: [] }))

    await listApplicantSyntheticPromotion()
    await executeApplicantSyntheticPromotion('applicant/3', {
      expectedVersion: 11,
      command: 'PromoteAuthorizedSyntheticApplicant',
      reason: 'Authorized synthetic patient shell transaction reviewed.',
      canonicalPatientCreationAcknowledged: true,
      noPortalNoCareAcknowledged: true,
    }, 'synthetic-promotion-retry-key')

    const [listUrl, listInit] = fetchMock.mock.calls[0]
    expect(String(listUrl)).toContain('/admin/applicant-synthetic-promotion')
    expect(listInit?.method).toBeUndefined()
    expect(listInit?.cache).toBe('no-store')
    expect(new Headers(listInit?.headers).has('X-Idempotency-Key')).toBe(false)

    const [writeUrl, writeInit] = fetchMock.mock.calls[1]
    const headers = new Headers(writeInit?.headers)
    expect(String(writeUrl)).toContain('/admin/applicants/applicant%2F3/synthetic-promotion')
    expect(writeInit?.method).toBe('PUT')
    expect(writeInit?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Session')).toBe('staff-session')
    expect(headers.get('X-AvenChart-Facility-Id')).toBe('10')
    expect(headers.get('X-AvenChart-Purpose-Of-Use')).toBe('healthcare-operations')
    expect(headers.get('X-Idempotency-Key')).toBe('synthetic-promotion-retry-key')
    expect(JSON.parse(String(writeInit?.body))).toEqual({
      expectedVersion: 11,
      command: 'PromoteAuthorizedSyntheticApplicant',
      reason: 'Authorized synthetic patient shell transaction reviewed.',
      canonicalPatientCreationAcknowledged: true,
      noPortalNoCareAcknowledged: true,
    })
    expect(String(writeInit?.body)).not.toMatch(/patientId|pubpid|legacyPid|candidate|firstName|dateOfBirth|email|phone|assurance/i)
  })

  it('binds exact readiness fingerprints and separate verification to the patient session', async () => {
    sessionStorage.setItem('avenchart-ui.portalSession', JSON.stringify({ sessionId: 'portal-session', username: 'patient', portalUsername: 'patient', displayName: 'Synthetic Patient' }))
    fetchMock.mockImplementation(async () => jsonResponse({ requestId: 'request-1', status: 'Verification' }))
    const readiness = {
      requestId: 'request-1', requestVersion: 3, status: 'Intake',
      patientDetails: { displayName: 'Patient', dateOfBirth: '1990-01-01', email: 'patient@example.test', phone: '555', address: 'Synthetic address', fingerprint: 'a'.repeat(64), missingFields: [] },
      clinicalSummary: { activeMedicationCount: 0, activeAllergyCount: 0, historyAvailable: true, fingerprint: 'b'.repeat(64) },
      coverageOptions: [{ coverageToken: 'c'.repeat(64), coverageType: 'primary', provider: 'Harbor Mutual', planName: 'High Deductible', maskedPolicyNumber: '••••0012', maskedGroupNumber: '••••P111', subscriberRelationship: 'self', fingerprint: 'e'.repeat(64) }],
      acknowledgment: { kind: 'SyntheticDemonstrationAcknowledgment', packageKey: 'ack', packageVersion: 1, contentHash: 'd'.repeat(64), title: 'Acknowledgment', statements: ['Synthetic only'], legalEffect: false },
      blockingReasons: [],
    } satisfies TelehealthReadiness

    await completePatientReadiness('request-1', readiness, { complaintSummary: 'Synthetic migraine details', symptomDuration: '1-3-days', coverageToken: 'c'.repeat(64) })
    await verifyPatientCoverage('request-1', 4)

    const readinessBody = JSON.parse(String(fetchMock.mock.calls[0][1]?.body))
    expect(readinessBody).toMatchObject({
      expectedVersion: 3,
      demographicsFingerprint: 'a'.repeat(64),
      clinicalSummaryFingerprint: 'b'.repeat(64),
      coverageToken: 'c'.repeat(64),
      coverageFingerprint: 'e'.repeat(64),
      coverageConfirmed: true,
      acknowledgmentAccepted: true,
    })
    expect(readinessBody).not.toHaveProperty('policyNumber')
    expect(JSON.parse(String(fetchMock.mock.calls[1][1]?.body))).toEqual({ expectedVersion: 4 })
  })

  it('reads authoritative patient queue status without a mutation or idempotency header', async () => {
    sessionStorage.setItem('avenchart-ui.portalSession', JSON.stringify({ sessionId: 'portal-session', username: 'patient', portalUsername: 'patient', displayName: 'Synthetic Patient' }))
    fetchMock.mockResolvedValue(jsonResponse({ requestId: 'request/one', requestStatus: 'Queued' }))

    await getPatientQueueStatus('request/one')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/patient/requests/request%2Fone/status')
    expect(init?.method).toBeUndefined()
    expect(headers.get('X-AvenChart-Patient-Portal-Session')).toBe('portal-session')
    expect(headers.has('X-Idempotency-Key')).toBe(false)
  })

  it('binds prospective creation to a browser credential without a patient session', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ applicantId: 'applicant-1', status: 'ContactVerificationPending' }, 201))

    await createProspectiveApplicant({
      legalFirstName: 'Avery', legalLastName: 'Example', dateOfBirth: '1990-03-14',
      email: 'avery@example.test', phone: '404-555-0199', residenceStateCode: 'GA',
      postalCode: '30301', syntheticDataConfirmed: true,
    }, 'a'.repeat(64), 'create-idempotency-1')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/api/telehealth/v1/applicants')
    expect(headers.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('a'.repeat(64))
    expect(headers.get('X-Idempotency-Key')).toBe('create-idempotency-1')
    expect(headers.has('X-AvenChart-Patient-Portal-Session')).toBe(false)
    expect(JSON.parse(String(init?.body))).not.toHaveProperty('applicantAccessKey')
  })

  it('uses the same applicant credential and explicit idempotency for contact verification', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ applicantId: 'applicant/1', status: 'IdentityReviewPending' }))

    await verifyProspectiveApplicantContact('applicant/1', 'b'.repeat(64), 1, '246810', 'verify-idempotency-1')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/applicants/applicant%2F1/contact-verification')
    expect(headers.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('b'.repeat(64))
    expect(headers.get('X-Idempotency-Key')).toBe('verify-idempotency-1')
    expect(JSON.parse(String(init?.body))).toEqual({ expectedVersion: 1, verificationCode: '246810' })
  })

  it('keeps prospective safety triage applicant-owned, private, explicit, and retry-stable', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ applicantId: 'applicant/1', applicantStatus: 'SafetyScreenPassed' }))
    const input = {
      expectedVersion: 3,
      currentLocationStateCode: 'FL' as const,
      currentLocationConfirmed: true as const,
      hasEmergencyWarning: false,
      severeOrWorsening: false,
      requiresHandsOnExam: false,
      unsure: false,
      syntheticDataConfirmed: true as const,
    }

    await evaluateProspectiveSafetyTriage('applicant/1', 'c'.repeat(64), input, 'safety-triage-retry-key')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/applicants/applicant%2F1/safety-triage')
    expect(init?.method).toBe('POST')
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('c'.repeat(64))
    expect(headers.get('X-Idempotency-Key')).toBe('safety-triage-retry-key')
    expect(headers.has('X-AvenChart-Session')).toBe(false)
    expect(headers.has('X-AvenChart-Patient-Portal-Session')).toBe(false)
    expect(JSON.parse(String(init?.body))).toEqual(input)
    expect(String(init?.body)).not.toMatch(/patientId|requestId|complaint|insurance|accessKey/i)
  })

  it('records only a controlled prospective visit-purpose category at the applicant boundary', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ applicantId: 'applicant/1', applicantStatus: 'VisitPurposeRecorded' }))
    const input = {
      expectedVersion: 4,
      purposeCategory: 'sleep' as const,
      syntheticDataConfirmed: true as const,
    }

    await recordProspectiveVisitPurpose('applicant/1', 'd'.repeat(64), input, 'visit-purpose-retry-key')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/applicants/applicant%2F1/visit-purpose')
    expect(init?.method).toBe('POST')
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('d'.repeat(64))
    expect(headers.get('X-Idempotency-Key')).toBe('visit-purpose-retry-key')
    expect(headers.has('X-AvenChart-Session')).toBe(false)
    expect(headers.has('X-AvenChart-Patient-Portal-Session')).toBe(false)
    expect(JSON.parse(String(init?.body))).toEqual(input)
    expect(String(init?.body)).not.toMatch(/freeText|complaintSummary|symptom|patientId|requestId|insurance|accessKey/i)
  })

  it('loads only applicant-owned private practice-network catalog options without an idempotency header', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ applicantId: 'applicant/1', applicantStatus: 'VisitPurposeRecorded', plans: [] }))

    await getProspectivePracticeNetworkOptions('applicant/1', 'e'.repeat(64))

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/applicants/applicant%2F1/practice-network-precheck/options')
    expect(init?.method).toBeUndefined()
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('e'.repeat(64))
    expect(headers.has('X-Idempotency-Key')).toBe(false)
    expect(headers.has('X-AvenChart-Session')).toBe(false)
    expect(headers.has('X-AvenChart-Patient-Portal-Session')).toBe(false)
  })

  it('records only expected version, opaque plan key, and synthetic confirmation', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ applicantId: 'applicant/1', applicantStatus: 'PracticeNetworkPrecheckRecorded' }))
    const input = {
      expectedVersion: 5,
      planKey: 'blue-valley-standard' as const,
      syntheticDataConfirmed: true as const,
    }

    await recordProspectivePracticeNetworkPrecheck(
      'applicant/1',
      'f'.repeat(64),
      input,
      'practice-network-retry-key',
    )

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/applicants/applicant%2F1/practice-network-precheck')
    expect(init?.method).toBe('POST')
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('f'.repeat(64))
    expect(headers.get('X-Idempotency-Key')).toBe('practice-network-retry-key')
    expect(headers.has('X-AvenChart-Session')).toBe(false)
    expect(headers.has('X-AvenChart-Patient-Portal-Session')).toBe(false)
    expect(JSON.parse(String(init?.body))).toEqual(input)
    expect(String(init?.body)).not.toMatch(/member|subscriber|policy|group|card|physician|status|result|price|payment|patientId|requestId|accessKey/i)
  })

  it('posts minimum synthetic member details only to the applicant-owned protected receipt route', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ applicantId: 'applicant/1', applicantStatus: 'MemberInsuranceDetailsRecorded' }))
    const input = {
      expectedVersion: 6,
      memberId: 'SYN-HM-1001',
      groupNumber: 'SYN-GROUP-01',
      subscriberRelationship: 'Self' as const,
      subscriberFirstName: null,
      subscriberLastName: null,
      subscriberDateOfBirth: null,
      detailsConfirmed: true as const,
      syntheticDataConfirmed: true as const,
    }

    await recordProspectiveMemberInsuranceDetails(
      'applicant/1',
      'g'.repeat(64),
      input,
      'member-details-retry-key',
    )

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/applicants/applicant%2F1/member-insurance-details')
    expect(init?.method).toBe('POST')
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('g'.repeat(64))
    expect(headers.get('X-Idempotency-Key')).toBe('member-details-retry-key')
    expect(headers.has('X-AvenChart-Session')).toBe(false)
    expect(headers.has('X-AvenChart-Patient-Portal-Session')).toBe(false)
    expect(JSON.parse(String(init?.body))).toEqual(input)
    expect(String(init?.body)).not.toMatch(/payer|product|planKey|network|coverageStatus|price|payment|patientId|requestId|accessKey|card/i)
  })

  it('posts only version and synthetic confirmation to the applicant-owned eligibility route', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ applicantId: 'applicant/1', applicantStatus: 'SyntheticEligibilityRecorded' }))
    const input = {
      expectedVersion: 7,
      syntheticDataConfirmed: true as const,
    }

    await recordProspectiveEligibility(
      'applicant/1',
      'h'.repeat(64),
      input,
      'eligibility-retry-key',
    )

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/applicants/applicant%2F1/eligibility')
    expect(init?.method).toBe('POST')
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('h'.repeat(64))
    expect(headers.get('X-Idempotency-Key')).toBe('eligibility-retry-key')
    expect(headers.has('X-AvenChart-Session')).toBe(false)
    expect(headers.has('X-AvenChart-Patient-Portal-Session')).toBe(false)
    expect(JSON.parse(String(init?.body))).toEqual(input)
    expect(String(init?.body)).not.toMatch(/member|subscriber|payer|plan|network|benefit|x12|price|payment|patientId|requestId|accessKey/i)
  })

  it('posts only version and synthetic confirmation to the applicant-owned practice-network route', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ applicantId: 'applicant/1', applicantStatus: 'SyntheticPracticeNetworkRecorded' }))
    const input = {
      expectedVersion: 8,
      syntheticDataConfirmed: true as const,
    }

    await recordProspectivePracticeNetwork(
      'applicant/1',
      'i'.repeat(64),
      input,
      'exact-practice-network-retry-key',
    )

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/applicants/applicant%2F1/practice-network-determination')
    expect(init?.method).toBe('POST')
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('i'.repeat(64))
    expect(headers.get('X-Idempotency-Key')).toBe('exact-practice-network-retry-key')
    expect(headers.has('X-AvenChart-Session')).toBe(false)
    expect(headers.has('X-AvenChart-Patient-Portal-Session')).toBe(false)
    expect(JSON.parse(String(init?.body))).toEqual(input)
    expect(String(init?.body)).not.toMatch(/member|subscriber|payer|plan|network|benefit|eligibility|physician|fhir|price|payment|patientId|requestId|accessKey/i)
  })

  it('posts only version and acknowledgments to the applicant-owned identity-proofing route', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ applicantId: 'applicant/1', applicantStatus: 'SyntheticIdentityProofingRecorded' }))
    const input = {
      expectedVersion: 9,
      privacyNoticeAcknowledged: true as const,
      syntheticDataConfirmed: true as const,
    }

    await recordProspectiveIdentityProofing(
      'applicant/1',
      'j'.repeat(64),
      input,
      'identity-proofing-retry-key',
    )

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/applicants/applicant%2F1/identity-proofing')
    expect(init?.method).toBe('POST')
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('j'.repeat(64))
    expect(headers.get('X-Idempotency-Key')).toBe('identity-proofing-retry-key')
    expect(headers.has('X-AvenChart-Session')).toBe(false)
    expect(headers.has('X-AvenChart-Patient-Portal-Session')).toBe(false)
    expect(JSON.parse(String(init?.body))).toEqual(input)
    expect(String(init?.body)).not.toMatch(/name|birth|email|phone|address|member|subscriber|payer|plan|network|document|government|biometric|evidence|result|assurance|patientId|requestId|accessKey/i)
  })

  it('loads and acknowledges only the applicant-owned state notice with explicit retry identity', async () => {
    fetchMock.mockImplementation(async () => jsonResponse({ applicantId: 'applicant/1', applicantStatus: 'SyntheticPatientPromoted' }))
    const input = {
      expectedVersion: 12,
      noticeKey: 'GA_TELEHEALTH_NOTICE_V1' as const,
      noticeVersion: 1 as const,
      currentLocationStateCode: 'GA' as const,
      currentLocationConfirmed: true as const,
      modeOfCareAcknowledged: true as const,
      privacyLimitationsAcknowledged: true as const,
      emergencyInstructionsAcknowledged: true as const,
      inPersonOptionAcknowledged: true as const,
      clinicianReconfirmationRequiredAcknowledged: true as const,
      syntheticDataConfirmed: true as const,
    }

    await getApplicantTelehealthNotice('applicant/1', 'k'.repeat(64))
    await acknowledgeApplicantTelehealthNotice('applicant/1', 'k'.repeat(64), input, 'notice-retry-key')

    const [getUrl, getInit] = fetchMock.mock.calls[0]
    const getHeaders = new Headers(getInit?.headers)
    expect(String(getUrl)).toContain('/applicants/applicant%2F1/telehealth-notice')
    expect(getInit?.method).toBeUndefined()
    expect(getInit?.body).toBeUndefined()
    expect(getInit?.cache).toBe('no-store')
    expect(getHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('k'.repeat(64))
    expect(getHeaders.has('X-Idempotency-Key')).toBe(false)

    const [postUrl, postInit] = fetchMock.mock.calls[1]
    const postHeaders = new Headers(postInit?.headers)
    expect(String(postUrl)).toContain('/applicants/applicant%2F1/telehealth-notice/acknowledgment')
    expect(postInit?.method).toBe('POST')
    expect(postInit?.cache).toBe('no-store')
    expect(postHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('k'.repeat(64))
    expect(postHeaders.get('X-Idempotency-Key')).toBe('notice-retry-key')
    expect(postHeaders.has('X-AvenChart-Session')).toBe(false)
    expect(postHeaders.has('X-AvenChart-Patient-Portal-Session')).toBe(false)
    expect(JSON.parse(String(postInit?.body))).toEqual(input)
    expect(String(postInit?.body)).not.toMatch(/"patientId"|"portalAccountId"|"insuranceId"|"requestId"|"queueEntryId"|"consentDocumented"|"actor"|"timestamp"|"accessKey"/i)
  })

  it('loads and confirms only the bounded applicant-owned registration-details snapshot', async () => {
    fetchMock.mockImplementation(async () => jsonResponse({ applicantId: 'applicant/1', applicantStatus: 'SyntheticTelehealthNoticeAcknowledged' }))
    const input = {
      expectedVersion: 14,
      detailsFingerprint: 'f'.repeat(64),
      legalNameAndBirthDateConfirmed: true as const,
      contactChannelsConfirmed: true as const,
      residenceRegionConfirmed: true as const,
      noCorrectionsNeededConfirmed: true as const,
      syntheticDataConfirmed: true as const,
    }

    await getApplicantRegistrationDetails('applicant/1', 'l'.repeat(64))
    await confirmApplicantRegistrationDetails('applicant/1', 'l'.repeat(64), input, 'registration-details-retry-key')

    const [getUrl, getInit] = fetchMock.mock.calls[0]
    const getHeaders = new Headers(getInit?.headers)
    expect(String(getUrl)).toContain('/applicants/applicant%2F1/registration-details')
    expect(getInit?.method).toBeUndefined()
    expect(getInit?.body).toBeUndefined()
    expect(getInit?.cache).toBe('no-store')
    expect(getHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('l'.repeat(64))
    expect(getHeaders.has('X-Idempotency-Key')).toBe(false)

    const [postUrl, postInit] = fetchMock.mock.calls[1]
    const postHeaders = new Headers(postInit?.headers)
    const body = JSON.parse(String(postInit?.body)) as Record<string, unknown>
    expect(String(postUrl)).toContain('/applicants/applicant%2F1/registration-details/confirmation')
    expect(postInit?.method).toBe('POST')
    expect(postInit?.cache).toBe('no-store')
    expect(postHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('l'.repeat(64))
    expect(postHeaders.get('X-Idempotency-Key')).toBe('registration-details-retry-key')
    expect(postHeaders.has('X-AvenChart-Session')).toBe(false)
    expect(postHeaders.has('X-AvenChart-Patient-Portal-Session')).toBe(false)
    expect(body).toEqual(input)
    expect(Object.keys(body).sort()).toEqual([
      'contactChannelsConfirmed',
      'detailsFingerprint',
      'expectedVersion',
      'legalNameAndBirthDateConfirmed',
      'noCorrectionsNeededConfirmed',
      'residenceRegionConfirmed',
      'syntheticDataConfirmed',
    ])
    expect(String(postInit?.body)).not.toMatch(/"legalFirstName"|"legalLastName"|"dateOfBirth"|"email"|"phone"|"postalCode"|"address"|"patientId"|"memberId"|"insuranceId"|"accessKey"/i)
  })

  it('loads and confirms only the masked applicant-owned insurance handoff snapshot', async () => {
    fetchMock.mockImplementation(async () => jsonResponse({ applicantId: 'applicant/1', applicantStatus: 'SyntheticMinimumRegistrationDetailsConfirmed' }))
    const input = {
      expectedVersion: 15,
      insuranceSnapshotFingerprint: 'e'.repeat(64),
      payerAndProductConfirmed: true as const,
      maskedMemberDetailsConfirmed: true as const,
      subscriberRelationshipConfirmed: true as const,
      evidenceLimitationsAcknowledged: true as const,
      syntheticDataConfirmed: true as const,
    }

    await getApplicantInsuranceHandoff('applicant/1', 'm'.repeat(64))
    await confirmApplicantInsuranceHandoff('applicant/1', 'm'.repeat(64), input, 'insurance-handoff-retry-key')

    const [getUrl, getInit] = fetchMock.mock.calls[0]
    const getHeaders = new Headers(getInit?.headers)
    expect(String(getUrl)).toContain('/applicants/applicant%2F1/insurance-handoff')
    expect(getInit?.method).toBeUndefined()
    expect(getInit?.body).toBeUndefined()
    expect(getInit?.cache).toBe('no-store')
    expect(getHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('m'.repeat(64))
    expect(getHeaders.has('X-Idempotency-Key')).toBe(false)

    const [postUrl, postInit] = fetchMock.mock.calls[1]
    const postHeaders = new Headers(postInit?.headers)
    const body = JSON.parse(String(postInit?.body)) as Record<string, unknown>
    expect(String(postUrl)).toContain('/applicants/applicant%2F1/insurance-handoff/confirmation')
    expect(postInit?.method).toBe('POST')
    expect(postInit?.cache).toBe('no-store')
    expect(postHeaders.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('m'.repeat(64))
    expect(postHeaders.get('X-Idempotency-Key')).toBe('insurance-handoff-retry-key')
    expect(postHeaders.has('X-AvenChart-Session')).toBe(false)
    expect(postHeaders.has('X-AvenChart-Patient-Portal-Session')).toBe(false)
    expect(body).toEqual(input)
    expect(Object.keys(body).sort()).toEqual([
      'evidenceLimitationsAcknowledged',
      'expectedVersion',
      'insuranceSnapshotFingerprint',
      'maskedMemberDetailsConfirmed',
      'payerAndProductConfirmed',
      'subscriberRelationshipConfirmed',
      'syntheticDataConfirmed',
    ])
    expect(String(postInit?.body)).not.toMatch(/"payerDisplayName"|"productDisplayName"|"memberId"|"groupNumber"|"subscriberName"|"patientId"|"insuranceId"|"accessKey"/i)
  })

  it('issues a patient connection command with only coarse preflight evidence and explicit retry identity', async () => {
    sessionStorage.setItem('avenchart-ui.portalSession', JSON.stringify({ sessionId: 'portal-session', username: 'patient', portalUsername: 'patient', displayName: 'Synthetic Patient' }))
    fetchMock.mockResolvedValue(jsonResponse({ requestId: 'request-1', requestStatus: 'Connecting' }))
    const evidence: TelehealthDevicePreflight = {
      browserSupported: true, cameraAvailable: true, microphoneAvailable: true,
      speakerAvailable: true, networkQuality: 'unknown', syntheticDataConfirmed: true,
    }

    await preparePatientConnection('request-1', 8, evidence, 'connection-retry-key')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    const body = JSON.parse(String(init?.body))
    expect(String(url)).toContain('/patient/requests/request-1/connection-grants')
    expect(headers.get('X-AvenChart-Patient-Portal-Session')).toBe('portal-session')
    expect(headers.get('X-Idempotency-Key')).toBe('connection-retry-key')
    expect(body).toEqual({ expectedVersion: 8, ...evidence })
    expect(JSON.stringify(body)).not.toMatch(/deviceId|label|credential/i)
  })

  it('issues an applicant-owned connection command without portal or staff credentials', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ requestId: 'request-55', requestStatus: 'Connecting' }))
    const evidence: TelehealthDevicePreflight = {
      browserSupported: true, cameraAvailable: true, microphoneAvailable: true,
      speakerAvailable: true, networkQuality: 'good', syntheticDataConfirmed: true,
    }

    await prepareApplicantConnection(
      'applicant/55',
      'a'.repeat(64),
      'request/55',
      14,
      evidence,
      'applicant-connection-retry-key',
    )

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    const body = JSON.parse(String(init?.body))
    expect(String(url)).toContain('/applicants/applicant%2F55/telehealth-request/request%2F55/connection-grants')
    expect(init?.method).toBe('POST')
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Telehealth-Applicant-Key')).toBe('a'.repeat(64))
    expect(headers.get('X-Idempotency-Key')).toBe('applicant-connection-retry-key')
    expect(headers.has('X-AvenChart-Session')).toBe(false)
    expect(headers.has('X-AvenChart-Patient-Portal-Session')).toBe(false)
    expect(body).toEqual({ expectedVersion: 14, ...evidence })
    expect(JSON.stringify(body)).not.toMatch(/deviceId|label|credential|patientId|providerId|applicantId/i)
  })

  it('binds physician connection grants to the staff facility and reservation', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'physician-session', username: 'doctor', displayName: 'Doctor', role: 'provider', facilityId: 10, purposeOfUse: 'treatment' }))
    fetchMock.mockResolvedValue(jsonResponse({ requestId: 'request-1', requestStatus: 'Connecting' }))
    const evidence: TelehealthDevicePreflight = {
      browserSupported: true, cameraAvailable: true, microphoneAvailable: true,
      speakerAvailable: true, networkQuality: 'good', syntheticDataConfirmed: true,
    }

    await preparePhysicianConnection('reservation/1', 8, evidence, 'physician-retry-key')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/clinician/reservations/reservation%2F1/connection-grants')
    expect(headers.get('X-AvenChart-Session')).toBe('physician-session')
    expect(headers.get('X-AvenChart-Facility-Id')).toBe('10')
    expect(headers.get('X-AvenChart-Purpose-Of-Use')).toBe('treatment')
    expect(headers.get('X-Idempotency-Key')).toBe('physician-retry-key')
  })

  it('starts a consultation with a retry-stable command and coarse affirmative evidence only', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'physician-session', username: 'doctor', displayName: 'Doctor', role: 'provider', facilityId: 10, purposeOfUse: 'treatment' }))
    fetchMock.mockResolvedValue(jsonResponse({ consultationId: 'opaque-consultation', requestStatus: 'InConsultation', legalEffect: false }))
    const input = {
      expectedVersion: 9,
      patientLocationState: 'GA' as const,
      patientIdentityDiscussed: true,
      callbackConfirmed: true,
      privacyConfirmed: true,
      consentDiscussed: true,
      noConcerningSymptomChange: true,
      emergencyPlanConfirmed: true,
      communicationSufficient: true,
      syntheticDataConfirmed: true,
    } satisfies import('./api.ts').TelehealthConsultationStartInput

    await startTelehealthConsultation('reservation/1', input, 'consultation-retry-key')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    const body = JSON.parse(String(init?.body))
    expect(String(url)).toContain('/clinician/reservations/reservation%2F1/consultations/start')
    expect(headers.get('X-AvenChart-Session')).toBe('physician-session')
    expect(headers.get('X-AvenChart-Facility-Id')).toBe('10')
    expect(headers.get('X-AvenChart-Purpose-Of-Use')).toBe('treatment')
    expect(headers.get('X-Idempotency-Key')).toBe('consultation-retry-key')
    expect(body).toEqual(input)
    expect(JSON.stringify(body)).not.toMatch(/encounterId|credential|deviceId|policyNumber/i)
  })

  it('reads the bounded consultation workspace without a mutation, body, or idempotency header', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'physician-session', username: 'doctor', displayName: 'Doctor', role: 'provider', facilityId: 10, purposeOfUse: 'treatment' }))
    fetchMock.mockResolvedValue(jsonResponse({ consultationId: 'consultation/1', consultationStatus: 'InConsultation', readOnly: true }))

    await getTelehealthConsultationWorkspace('consultation/1')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/clinician/consultations/consultation%2F1/workspace')
    expect(init?.method).toBeUndefined()
    expect(init?.body).toBeUndefined()
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Session')).toBe('physician-session')
    expect(headers.get('X-AvenChart-Facility-Id')).toBe('10')
    expect(headers.get('X-AvenChart-Purpose-Of-Use')).toBe('treatment')
    expect(headers.has('X-Idempotency-Key')).toBe(false)
  })

  it('saves only a versioned SOAP draft through the opaque consultation route', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'physician-session', username: 'doctor', displayName: 'Doctor', role: 'provider', facilityId: 10, purposeOfUse: 'treatment' }))
    fetchMock.mockResolvedValue(jsonResponse({ version: 3, isSigned: false, isFinal: false }))
    const input = {
      expectedVersion: 2,
      subjective: 'Synthetic clinician-entered history.',
      objective: null,
      assessment: null,
      plan: null,
    }

    await saveTelehealthConsultationDocumentationDraft('consultation/1', input)

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/clinician/consultations/consultation%2F1/documentation/draft')
    expect(init?.method).toBe('PUT')
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Session')).toBe('physician-session')
    expect(headers.get('X-AvenChart-Facility-Id')).toBe('10')
    expect(headers.get('X-AvenChart-Purpose-Of-Use')).toBe('treatment')
    expect(headers.has('X-Idempotency-Key')).toBe(false)
    expect(JSON.parse(String(init?.body))).toEqual(input)
    expect(JSON.stringify(init?.body)).not.toMatch(/patientId|encounterId|author|savedAt|diagnos|prescri|claim/i)
  })

  it('enters unfinished wrap-up with explicit acknowledgments and retry identity only', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'physician-session', username: 'doctor', displayName: 'Doctor', role: 'provider', facilityId: 10, purposeOfUse: 'treatment' }))
    fetchMock.mockResolvedValue(jsonResponse({ consultationId: 'consultation/1', version: 2, consultationStatus: 'MediaEnded', requestStatus: 'WrapUp', completionEnabled: false }))
    const input = {
      expectedVersion: 1,
      syntheticSessionEndedConfirmed: true,
      documentationStillIncompleteAcknowledged: true,
      wrapUpResponsibilityAcknowledged: true,
    } as const

    await enterTelehealthConsultationWrapUp('consultation/1', input, 'wrap-up-retry-key')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/clinician/consultations/consultation%2F1/wrap-up')
    expect(init?.method).toBe('POST')
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Session')).toBe('physician-session')
    expect(headers.get('X-AvenChart-Facility-Id')).toBe('10')
    expect(headers.get('X-AvenChart-Purpose-Of-Use')).toBe('treatment')
    expect(headers.get('X-Idempotency-Key')).toBe('wrap-up-retry-key')
    expect(JSON.parse(String(init?.body))).toEqual(input)
    expect(JSON.stringify(init?.body)).not.toMatch(/patientId|requestId|shiftId|appointmentId|encounterId|actor|timestamp|disposition|completionEnabled/i)
  })

  it('searches scoped pharmacy choices without mutation or hidden resource identifiers', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'physician-session', username: 'doctor', displayName: 'Doctor', role: 'provider', facilityId: 10, purposeOfUse: 'treatment' }))
    fetchMock.mockResolvedValue(jsonResponse({ consultationId: 'consultation/1', adapterMode: 'NON_PRODUCTION', pharmacies: [] }))

    await getTelehealthPharmacyChoices('consultation/1', {
      query: 'community', state: 'GA', postalCode: '303', originPostalCode: '30303', locationSearchAcknowledged: true, limit: 25,
    })

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/clinician/consultations/consultation%2F1/pharmacy-choices?')
    expect(String(url)).toContain('query=community')
    expect(String(url)).toContain('originPostalCode=30303')
    expect(String(url)).toContain('locationSearchAcknowledged=true')
    expect(init?.method).toBeUndefined()
    expect(init?.body).toBeUndefined()
    expect(init?.cache).toBe('no-store')
    expect(headers.has('X-Idempotency-Key')).toBe(false)
    expect(String(url)).not.toMatch(/patientId|encounterId|requestId|latitude|longitude/i)
  })

  it('records only a patient-confirmed unsigned destination draft with retry identity', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'physician-session', username: 'doctor', displayName: 'Doctor', role: 'provider', facilityId: 10, purposeOfUse: 'treatment' }))
    fetchMock.mockResolvedValue(jsonResponse({ version: 2, prescriptionCreated: false, transmitted: false }))

    await recordTelehealthPharmacyChoice('consultation/1', 1, 'directory-entry-1', 'pharmacy-choice-key')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    const body = JSON.parse(String(init?.body))
    expect(String(url)).toContain('/clinician/consultations/consultation%2F1/pharmacy-choice')
    expect(init?.method).toBe('PUT')
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-Idempotency-Key')).toBe('pharmacy-choice-key')
    expect(body).toEqual({ expectedVersion: 1, directoryEntryId: 'directory-entry-1', patientChoiceConfirmed: true, syntheticDataConfirmed: true })
    expect(JSON.stringify(body)).not.toMatch(/patientId|encounterId|requestId|medication|drug|prescri|claim|address|ncpdp|npi|actor|timestamp/i)
  })

  it('loads only the physician-scoped safety-disposition workspace without mutation identity', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'physician-session', username: 'doctor', displayName: 'Doctor', role: 'provider', facilityId: 10, purposeOfUse: 'treatment' }))
    fetchMock.mockResolvedValue(jsonResponse({ consultationId: 'consultation/1', currentDraft: null }))

    await getTelehealthSafetyDispositionDraft('consultation/1')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/clinician/consultations/consultation%2F1/safety-disposition-draft')
    expect(init?.method).toBeUndefined()
    expect(init?.body).toBeUndefined()
    expect(init?.cache).toBe('no-store')
    expect(headers.has('X-Idempotency-Key')).toBe(false)
  })

  it('loads a minimized completion-prerequisites review without a mutation, body, or idempotency identity', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'physician-session', username: 'doctor', displayName: 'Doctor', role: 'provider', facilityId: 10, purposeOfUse: 'treatment' }))
    fetchMock.mockResolvedValue(jsonResponse({ consultationId: 'consultation/1', signingEnabled: false, completionEnabled: false }))

    await getTelehealthCompletionPrerequisites('consultation/1')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/clinician/consultations/consultation%2F1/completion-prerequisites')
    expect(init?.method).toBeUndefined()
    expect(init?.body).toBeUndefined()
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-AvenChart-Session')).toBe('physician-session')
    expect(headers.get('X-AvenChart-Facility-Id')).toBe('10')
    expect(headers.get('X-AvenChart-Purpose-Of-Use')).toBe('treatment')
    expect(headers.has('X-Idempotency-Key')).toBe(false)
  })

  it('searches only the scoped synthetic medication catalog without a mutation identity', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'physician-session', username: 'doctor', displayName: 'Doctor', role: 'provider', facilityId: 10, purposeOfUse: 'treatment' }))
    fetchMock.mockResolvedValue(jsonResponse({ consultationId: 'consultation/1', catalogResults: [] }))

    await getTelehealthPrescriptionPreparationDraft('consultation/1', 'metformin')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/clinician/consultations/consultation%2F1/prescription-preparation-draft?query=metformin')
    expect(init?.method).toBeUndefined()
    expect(init?.body).toBeUndefined()
    expect(init?.cache).toBe('no-store')
    expect(headers.has('X-Idempotency-Key')).toBe(false)
    expect(String(url)).not.toMatch(/patientId|encounterId|requestId|appointmentId|pharmacyId/i)
  })

  it('records only bounded preparation content with stable retry identity', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'physician-session', username: 'doctor', displayName: 'Doctor', role: 'provider', facilityId: 10, purposeOfUse: 'treatment' }))
    fetchMock.mockResolvedValue(jsonResponse({ version: 1, legalEffect: false, safetyChecked: false, signed: false, transmitted: false }))
    const input = {
      expectedVersion: 0,
      rxNormCode: '860975',
      doseAmount: 500,
      doseUnit: 'mg',
      frequency: 'twice daily',
      quantityValue: 60,
      quantityUnit: 'tablets',
      durationDays: 30,
      refills: 0,
      indication: 'Physician-entered synthetic indication.',
      directions: 'Physician-entered synthetic directions.',
      medicationListReviewed: true as const,
      allergyListReviewed: true as const,
      adequateEvaluationCompleted: true as const,
      syntheticDataConfirmed: true as const,
    }

    await recordTelehealthPrescriptionPreparationDraft('consultation/1', input, 'prescription-preparation-key')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    const body = JSON.parse(String(init?.body))
    expect(String(url)).toContain('/clinician/consultations/consultation%2F1/prescription-preparation-draft')
    expect(init?.method).toBe('PUT')
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-Idempotency-Key')).toBe('prescription-preparation-key')
    expect(body).toEqual(input)
    expect(JSON.stringify(body)).not.toMatch(/patientId|encounterId|requestId|appointmentId|pharmacyId|actor|timestamp|signature|claim|transmissionToken/i)
  })

  it('records only bounded physician-authored safety-draft input with retry identity', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'physician-session', username: 'doctor', displayName: 'Doctor', role: 'provider', facilityId: 10, purposeOfUse: 'treatment' }))
    fetchMock.mockResolvedValue(jsonResponse({ version: 1, legalEffect: false, signed: false, finalized: false, patientDelivered: false }))
    const input = {
      expectedVersion: 0,
      dispositionCode: 'TreatedTelehealth',
      adequateEvaluationCompleted: true,
      followUpOwner: 'Patient',
      followUpTimeframe: 'within two synthetic days',
      nextStepInstructions: 'Physician-authored synthetic next step.',
      warningEscalationInstructions: 'Physician-authored synthetic warning.',
      communicationMethod: 'DiscussedDuringSyntheticConsultation',
      communicationCompleted: true,
      locationCallbackReconfirmed: false,
      emergencyInstructionProvided: false,
      emergencyHandoffStatus: null,
      contactAttemptSummary: null,
      syntheticDataConfirmed: true as const,
    }

    await recordTelehealthSafetyDispositionDraft('consultation/1', input, 'disposition-draft-key')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    const body = JSON.parse(String(init?.body))
    expect(String(url)).toContain('/clinician/consultations/consultation%2F1/safety-disposition-draft')
    expect(init?.method).toBe('PUT')
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-Idempotency-Key')).toBe('disposition-draft-key')
    expect(body).toEqual(input)
    expect(JSON.stringify(body)).not.toMatch(/patientId|encounterId|requestId|appointmentId|actor|timestamp|signature|delivery|medication|prescription|claim/i)
  })

  it('records only source versions and explicit POC boundaries for a PreparedOnly claim receipt', async () => {
    sessionStorage.setItem('avenchart-ui.clinicianSession', JSON.stringify({ sessionId: 'physician-session', username: 'doctor', displayName: 'Doctor', role: 'provider', facilityId: 10, purposeOfUse: 'treatment' }))
    fetchMock.mockResolvedValue(jsonResponse({ claimPreparationId: 'receipt-1', claimState: 'PreparedOnly', transactionCreated: false, externalDestinationContacted: false, submissionAccepted: false }))
    const input = { expectedDocumentationVersion: 2, expectedDispositionVersion: 3, expectedFinalClinicalReviewVersion: 4, sourceEvidenceReviewed: true, syntheticOnlyConfirmed: true, noSubmissionConfirmed: true }

    await prepareTelehealthProfessionalClaim('consultation/1', input, 'claim-preparation-key')

    const [url, init] = fetchMock.mock.calls[0]
    const headers = new Headers(init?.headers)
    expect(String(url)).toContain('/clinician/consultations/consultation%2F1/professional-claim-preparation')
    expect(init?.method).toBe('POST')
    expect(init?.cache).toBe('no-store')
    expect(headers.get('X-Idempotency-Key')).toBe('claim-preparation-key')
    expect(JSON.parse(String(init?.body))).toEqual(input)
    expect(String(init?.body)).not.toMatch(/patientId|encounterId|requestId|appointmentId|payer|diagnosis|procedure|fee|payload|destination/i)
  })
})
