// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiRequestError } from '../../api/transport.ts'
import ApplicantTelehealthQueueStatus from './ApplicantTelehealthQueueStatus.tsx'
import { getApplicantTelehealthRequestQueueStatus, prepareApplicantConnection, type TelehealthApplicantRequestQueueStatus } from './api.ts'
import { runTelehealthDevicePreflight } from './devicePreflight.ts'

vi.mock('./api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('./api.ts')>()
  return { ...original, getApplicantTelehealthRequestQueueStatus: vi.fn(), prepareApplicantConnection: vi.fn() }
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

describe('ApplicantTelehealthQueueStatus', () => {
  beforeEach(() => vi.clearAllMocks())

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
