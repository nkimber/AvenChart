// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiRequestError } from '../../api/transport.ts'
import ApplicantTelehealthQueueStatus from './ApplicantTelehealthQueueStatus.tsx'
import { getApplicantSyntheticPostVisitReceipt, getApplicantTelehealthRequestQueueStatus, prepareApplicantConnection, type TelehealthApplicantRequestQueueStatus } from './api.ts'
import { getApplicantSyntheticAfterVisitPlanPreview } from './api.ts'
import { runTelehealthDevicePreflight } from './devicePreflight.ts'

vi.mock('./api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('./api.ts')>()
  return { ...original, getApplicantSyntheticAfterVisitPlanPreview: vi.fn(), getApplicantSyntheticPostVisitReceipt: vi.fn(), getApplicantTelehealthRequestQueueStatus: vi.fn(), prepareApplicantConnection: vi.fn() }
})

vi.mock('./devicePreflight.ts', () => ({ runTelehealthDevicePreflight: vi.fn() }))

const queuedStatus: TelehealthApplicantRequestQueueStatus = {
  requestId: '53000000-0000-4000-8000-000000000053',
  requestStatus: 'Queued',
  requestVersion: 13,
  policyKey: 'SYNTHETIC_APPLICANT_REQUEST_QUEUE_STATUS',
  policyVersion: 1,
  sourceMode: 'NON_PRODUCTION',
  phase: 'InQueue',
  headline: "You're in line",
  detail: 'Approximately 2 requests are ahead. This can change for safety or operational reasons.',
  approximateRequestsAhead: 2,
  positionIsApproximate: true,
  exactQueuePositionAssigned: false,
  waitEstimateAvailable: false,
  waitEstimateMessage: 'A wait-time estimate is not available in this synthetic demonstration.',
  requestUpdatedAt: '2026-08-29T14:00:00Z',
  snapshotAt: '2026-08-29T14:00:01Z',
  refreshAfterSeconds: 5,
  realtimeAvailable: false,
  practiceAccepted: true,
  doctorSearchStarted: true,
  renderingPhysicianAssigned: false,
  renderingPhysicianIdentityDisclosed: false,
  syntheticRenderingCandidateMatched: false,
  realRenderingPhysicianNetworkConfirmed: false,
  connectionRoomCreated: false,
  patientWaitingRoomEntered: false,
  mediaSessionCreated: false,
  communicationStarted: false,
  coverageVerified: false,
  consentCreated: false,
  careAuthorized: false,
  integrationEnabled: false,
  externalCallPerformed: false,
  safetyActions: ['Call 911 now for an emergency.'],
  limitations: ['Approximate status only.'],
}

const reviewingStatus: TelehealthApplicantRequestQueueStatus = {
  ...queuedStatus,
  requestStatus: 'OperationalReview',
  requestVersion: 12,
  phase: 'Reviewing',
  headline: 'Reviewing your request',
  detail: 'Your practice has not placed this request in the physician queue yet.',
  approximateRequestsAhead: null,
  positionIsApproximate: false,
  practiceAccepted: false,
  doctorSearchStarted: false,
}

const reservedStatus: TelehealthApplicantRequestQueueStatus = {
  ...queuedStatus,
  requestStatus: 'Reserved',
  requestVersion: 14,
  phase: 'PhysicianPreparing',
  headline: 'A physician is getting ready',
  detail: 'Keep this page open. You can run the synthetic device check when the connection-room action appears.',
  approximateRequestsAhead: null,
  positionIsApproximate: false,
  renderingPhysicianAssigned: true,
  syntheticRenderingCandidateMatched: true,
}

const connectingStatus: TelehealthApplicantRequestQueueStatus = {
  ...reservedStatus,
  requestStatus: 'Connecting',
  requestVersion: 15,
  phase: 'ConnectionRoom',
  headline: 'Your private connection room is ready',
  detail: 'This synthetic room transports no media and does not start a consultation.',
  connectionRoomCreated: true,
  patientWaitingRoomEntered: true,
}

const inConsultationStatus: TelehealthApplicantRequestQueueStatus = {
  ...reservedStatus,
  requestStatus: 'InConsultation',
  requestVersion: 16,
  phase: 'Consultation',
  headline: 'Your synthetic consultation has started',
  detail: 'This is lifecycle demonstration data only.',
}

const wrapUpStatus: TelehealthApplicantRequestQueueStatus = {
  ...inConsultationStatus,
  requestStatus: 'WrapUp',
  requestVersion: 17,
  phase: 'WrapUp',
  headline: 'Your physician is finishing the synthetic visit record',
  detail: 'This visit is not complete. No signed record, after-visit summary, prescription, or claim is available.',
}

const closedStatus: TelehealthApplicantRequestQueueStatus = {
  ...wrapUpStatus,
  requestStatus: 'Closed',
  requestVersion: 18,
  phase: 'SyntheticLifecycleClosed',
  headline: 'The synthetic visit lifecycle has closed',
  detail: 'The appointment and encounter remain incomplete. No patient delivery, billing, claim, integration, or external action was created.',
  renderingPhysicianAssigned: false,
  syntheticRenderingCandidateMatched: false,
}

describe('ApplicantTelehealthQueueStatus', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getApplicantSyntheticAfterVisitPlanPreview).mockRejectedValue(new ApiRequestError('Not available.', 404))
  })

  it('shows only approximate applicant-owned queue status without a wait promise or clinician identity', async () => {
    vi.mocked(getApplicantTelehealthRequestQueueStatus).mockResolvedValue(queuedStatus)

    render(<ApplicantTelehealthQueueStatus applicantId="applicant/53" applicantAccessKey="secret-key" enabled />)

    expect(await screen.findByRole('heading', { name: "You're in line" })).toBeVisible()
    expect(screen.getByText(/Approximate requests ahead:/).parentElement).toHaveTextContent('2')
    expect(screen.getByText(/Exact queue position assigned/).parentElement).toHaveTextContent('No')
    expect(screen.getByText(/Wait estimate available/).parentElement).toHaveTextContent('No')
    expect(screen.getByText(/Physician assigned/).parentElement).toHaveTextContent('No')
    expect(screen.getByText(/Authoritative HTTP polling/)).toBeVisible()
    expect(getApplicantTelehealthRequestQueueStatus).toHaveBeenCalledWith(
      'applicant/53',
      'secret-key',
      expect.any(AbortSignal),
    )
  })

  it('refreshes from practice review to the approximate queue without expanding the status scope', async () => {
    vi.mocked(getApplicantTelehealthRequestQueueStatus)
      .mockResolvedValueOnce(reviewingStatus)
      .mockResolvedValueOnce(queuedStatus)

    render(<ApplicantTelehealthQueueStatus applicantId="applicant-53" applicantAccessKey="secret-key" enabled />)
    const refresh = await screen.findByRole('button', { name: 'Refresh queue status now' })
    fireEvent.click(refresh)

    expect(await screen.findByRole('heading', { name: "You're in line" })).toBeVisible()
    expect(screen.getByText(/Physician assigned/).parentElement).toHaveTextContent('No')
    expect(screen.getByText(/Approximate requests ahead:/).parentElement).toHaveTextContent('2')
  })

  it('keeps the last confirmed status and offers keyboard-operable recovery after a transient failure', async () => {
    vi.mocked(getApplicantTelehealthRequestQueueStatus)
      .mockResolvedValueOnce(queuedStatus)
      .mockRejectedValueOnce(new ApiRequestError('Network unavailable.', 503))
      .mockResolvedValueOnce(queuedStatus)

    render(<ApplicantTelehealthQueueStatus applicantId="applicant-53" applicantAccessKey="secret-key" enabled />)
    const refresh = await screen.findByRole('button', { name: 'Refresh queue status now' })
    fireEvent.click(refresh)

    expect(await screen.findByRole('alert')).toHaveTextContent('last confirmed status remains shown')
    expect(screen.getByRole('heading', { name: "You're in line" })).toBeVisible()
    const retry = screen.getByRole('button', { name: 'Retry queue status' })
    retry.focus()
    fireEvent.keyDown(retry, { key: 'Enter' })
    fireEvent.click(retry)

    await waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument())
  })

  it('shows physician preparation without disclosing identity or claiming real network confirmation', async () => {
    vi.mocked(getApplicantTelehealthRequestQueueStatus).mockResolvedValue(reservedStatus)

    render(<ApplicantTelehealthQueueStatus applicantId="applicant-53" applicantAccessKey="secret-key" enabled />)

    expect(await screen.findByRole('heading', { name: 'A physician is getting ready' })).toBeVisible()
    expect(screen.getByText(/Physician assigned/).parentElement).toHaveTextContent('Yes — identity not disclosed here')
    expect(screen.getByText(/Exact synthetic candidate matched/).parentElement).toHaveTextContent('Yes')
    expect(screen.getByText(/Real physician network confirmed/).parentElement).toHaveTextContent('No')
    expect(screen.queryByText(/provider|NPI/i)).not.toBeInTheDocument()
  })

  it('runs a local track-safe preflight and enters only the private synthetic waiting room', async () => {
    const joinCredential = 'secret-join-credential-that-must-not-render'
    vi.mocked(getApplicantTelehealthRequestQueueStatus)
      .mockResolvedValueOnce(reservedStatus)
      .mockResolvedValue(connectingStatus)
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
    vi.mocked(prepareApplicantConnection).mockResolvedValue({
      sessionId: 'session-55',
      grantId: 'grant-55',
      requestId: reservedStatus.requestId,
      requestVersion: 15,
      requestStatus: 'Connecting',
      participantRole: 'patient',
      adapterMode: 'NON_PRODUCTION',
      joinCredential,
      expiresAt: '2026-08-29T14:05:00Z',
      recordingEnabled: false,
      transcriptionEnabled: false,
      mediaTransportEnabled: false,
      waitingRoomMessage: 'Your private synthetic waiting room is ready. No media is connected in this demonstration.',
      limitations: ['No media or communication is connected.'],
    })

    render(<ApplicantTelehealthQueueStatus applicantId="applicant-55" applicantAccessKey="secret-key" enabled />)

    await screen.findByRole('heading', { name: 'A physician is getting ready' })
    fireEvent.click(screen.getByRole('button', { name: 'Check this device' }))
    expect(await screen.findByText(/Device check passed/)).toBeVisible()
    fireEvent.click(screen.getByRole('button', { name: 'Enter private synthetic waiting room' }))

    expect(await screen.findByRole('heading', { name: 'Waiting room ready' })).toBeVisible()
    expect(prepareApplicantConnection).toHaveBeenCalledWith(
      'applicant-55',
      'secret-key',
      reservedStatus.requestId,
      14,
      expect.objectContaining({ syntheticDataConfirmed: true }),
      expect.any(String),
    )
    expect(screen.queryByText(joinCredential)).not.toBeInTheDocument()
    expect(JSON.stringify(sessionStorage)).not.toContain(joinCredential)
    expect(await screen.findByRole('heading', { name: 'Your private connection room is ready' })).toBeVisible()
    expect(screen.getByText(/Private synthetic waiting room entered/).parentElement).toHaveTextContent('Yes')
    expect(screen.getByText(/Media session created/).parentElement).toHaveTextContent('No')
    expect(screen.getByText(/Communication started/).parentElement).toHaveTextContent('No')
  })

  it('continues polling during consultation and stops at minimized wrap-up status', async () => {
    vi.mocked(getApplicantTelehealthRequestQueueStatus)
      .mockResolvedValueOnce(inConsultationStatus)
      .mockResolvedValueOnce(wrapUpStatus)

    render(<ApplicantTelehealthQueueStatus applicantId="applicant-57" applicantAccessKey="secret-key" enabled />)

    await screen.findByRole('heading', { name: 'Your synthetic consultation has started' })
    fireEvent.click(screen.getByRole('button', { name: 'Refresh queue status now' }))

    expect(await screen.findByRole('heading', { name: 'Your physician is finishing the synthetic visit record' })).toBeVisible()
    expect(screen.getByText(/No signed record, after-visit summary, prescription, or claim/)).toBeVisible()
    expect(screen.queryByRole('button', { name: 'Refresh queue status now' })).not.toBeInTheDocument()
    expect(screen.queryByText(/provider|NPI|prescription ID/i)).not.toBeInTheDocument()
  })

  it('shows synthetic lifecycle closure without claiming an appointment or encounter completion', async () => {
    vi.mocked(getApplicantTelehealthRequestQueueStatus).mockResolvedValue(closedStatus)
    vi.mocked(getApplicantSyntheticPostVisitReceipt).mockResolvedValue({
      receiptId: 'receipt-61', requestId: closedStatus.requestId, createdAt: '2026-08-29T14:01:00Z', receiptVersion: 1,
      consultationVersion: 18, requestVersion: 18, receiptState: 'AvailableInPortal', sourceMode: 'NON_PRODUCTION', syntheticDataConfirmed: true,
      appointmentCompleted: false, encounterCompleted: false, clinicalRecordDelivered: false, prescriptionDelivered: false,
      billingCreated: false, claimCreated: false, notificationSent: false, externalDestinationContacted: false,
      limitations: ['This is an immutable NON_PRODUCTION synthetic lifecycle receipt, not an after-visit summary.'],
    })
    vi.mocked(getApplicantSyntheticAfterVisitPlanPreview).mockResolvedValue({
      previewId: 'preview-61', requestId: closedStatus.requestId, createdAt: '2026-08-29T14:01:00Z', previewVersion: 1,
      consultationVersion: 18, requestVersion: 18, dispositionVersion: 1, finalClinicalReviewVersion: 1,
      previewState: 'AvailableInPortal', sourceMode: 'NON_PRODUCTION', syntheticDataConfirmed: true,
      dispositionCode: 'TreatedTelehealth', followUpOwner: 'Practice', followUpTimeframe: 'Synthetic timeframe',
      nextStepInstructions: 'Synthetic next-step preview only.', warningEscalationInstructions: 'Synthetic warning preview only.',
      communicationMethod: 'DiscussedDuringSyntheticConsultation', communicationCompleted: true,
      appointmentCompleted: false, encounterCompleted: false, avsDelivered: false, notificationSent: false, externalDestinationContacted: false,
      limitations: ['This is an immutable NON_PRODUCTION synthetic plan preview, not medical advice or a delivered after-visit summary.'],
    })

    render(<ApplicantTelehealthQueueStatus applicantId="applicant-61" applicantAccessKey="secret-key" enabled />)

    expect(await screen.findByRole('heading', { name: 'The synthetic visit lifecycle has closed' })).toBeVisible()
    expect(screen.getByText(/appointment and encounter remain incomplete/i)).toBeVisible()
    expect(await screen.findByRole('heading', { name: 'Synthetic post-visit receipt' })).toBeVisible()
    expect(screen.getByText(/minimized lifecycle receipt, not an after-visit summary/i)).toBeVisible()
    expect(getApplicantSyntheticPostVisitReceipt).toHaveBeenCalledWith('applicant-61', 'secret-key', closedStatus.requestId, expect.any(AbortSignal))
    expect(await screen.findByRole('heading', { name: 'Synthetic after-visit plan preview' })).toBeVisible()
    expect(screen.getByText(/Synthetic next-step preview only/)).toBeVisible()
    expect(getApplicantSyntheticAfterVisitPlanPreview).toHaveBeenCalledWith('applicant-61', 'secret-key', closedStatus.requestId, expect.any(AbortSignal))
    expect(screen.getByText(/Physician assigned/).parentElement).toHaveTextContent('No')
    expect(screen.queryByRole('button', { name: 'Refresh queue status now' })).not.toBeInTheDocument()
    expect(screen.queryByText(/provider|NPI|prescription ID/i)).not.toBeInTheDocument()
  })

  it('stays silent while the owned request has not reached operational review', async () => {
    vi.mocked(getApplicantTelehealthRequestQueueStatus).mockRejectedValue(
      new ApiRequestError('Not available yet.', 409),
    )

    const { container } = render(
      <ApplicantTelehealthQueueStatus applicantId="applicant-53" applicantAccessKey="secret-key" enabled />,
    )

    await waitFor(() => expect(getApplicantTelehealthRequestQueueStatus).toHaveBeenCalledTimes(1))
    expect(container).toBeEmptyDOMElement()
  })
})
