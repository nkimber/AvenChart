// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'

test('failed administrator refresh clears stale telehealth authorization actions', async ({ page }) => {
  await page.route('**/api/messages/inbox**', (route) => route.fulfill({ json: { messages: [], totalMatches: 0 } }))
  await page.route('**/api/procedures/report-queue**', (route) => route.fulfill({ json: { items: [], totalMatches: 0 } }))

  let failQueue = false
  await page.route('**/api/telehealth/v1/admin/applicant-identity-review', (route) => route.fulfill({ json: {
    practiceDisplayName: 'AvenChart Synthetic Practice', serverTime: '2026-08-27T04:29:00Z', applicants: [],
    limitations: ['The applicant remains prospective.'],
  } }))
  await page.route('**/api/telehealth/v1/admin/applicant-promotion-authorization', (route) => route.fulfill({ json: {
    practiceDisplayName: 'AvenChart Synthetic Practice', serverTime: '2026-08-27T04:29:00Z', applicants: [],
    limitations: ['Assurance remains None.'],
  } }))
  await page.route('**/api/telehealth/v1/admin/applicant-synthetic-promotion', (route) => route.fulfill({ json: {
    practiceDisplayName: 'AvenChart Synthetic Practice', serverTime: '2026-08-27T04:29:00Z', applicants: [],
    limitations: ['No portal or care capability is created.'],
  } }))
  await page.route('**/api/telehealth/v1/admin/operational-review', async (route) => {
    if (!failQueue) {
      await route.fulfill({ json: { requests: [{
        requestId: '10000000-0000-4000-8000-000000000001',
        status: 'OperationalReview',
        complaintCategory: 'migraine',
        triageOutcome: 'TelehealthEligible',
        version: 3,
        createdAt: '2026-08-26T12:00:00Z',
        applicantOriginated: false,
      }] } })
      return
    }
    await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Synthetic queue unavailable.' }) })
  })

  await page.goto('/login')
  await page.getByLabel('Username').fill(process.env.MODERN_UI_STAFF_USERNAME ?? 'admin')
  await page.getByLabel('Password').fill(process.env.MODERN_UI_STAFF_PASSWORD ?? 'pass')
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page).toHaveURL(/\/clinician\/dashboard$/, { timeout: 20_000 })

  await page.goto('/clinician/telehealth/admin')
  await expect(page.getByRole('button', { name: 'Authorize to clinician queue' })).toBeVisible()
  failQueue = true
  await page.getByRole('button', { name: 'Refresh all' }).click()
  await expect(
    page.getByRole('alert').filter({ hasText: 'Synthetic queue unavailable.' }),
  ).toBeVisible()
  await expect(page.getByRole('button', { name: 'Authorize to clinician queue' })).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'Try again' })).toBeEnabled()
})

test('applicant queue authorization preserves an unchanged retry and accessible recovery', async ({ page }) => {
  await page.route('**/api/messages/inbox**', (route) => route.fulfill({ json: { messages: [], totalMatches: 0 } }))
  await page.route('**/api/procedures/report-queue**', (route) => route.fulfill({ json: { items: [], totalMatches: 0 } }))
  await page.route('**/api/telehealth/v1/admin/applicant-identity-review', (route) => route.fulfill({ json: {
    practiceDisplayName: 'AvenChart Synthetic Practice', serverTime: '2026-08-29T14:00:00Z', applicants: [], limitations: [],
  } }))
  await page.route('**/api/telehealth/v1/admin/applicant-promotion-authorization', (route) => route.fulfill({ json: {
    practiceDisplayName: 'AvenChart Synthetic Practice', serverTime: '2026-08-29T14:00:00Z', applicants: [], limitations: [],
  } }))
  await page.route('**/api/telehealth/v1/admin/applicant-synthetic-promotion', (route) => route.fulfill({ json: {
    practiceDisplayName: 'AvenChart Synthetic Practice', serverTime: '2026-08-29T14:00:00Z', applicants: [], limitations: [],
  } }))
  await page.route('**/api/telehealth/v1/admin/applicant-practice-review', (route) => route.fulfill({ json: {
    policyKey: 'SYNTHETIC_ADMIN_PRACTICE_REVIEW_INBOX', policyVersion: 1,
    practiceDisplayName: 'AvenChart Synthetic Practice', serverTime: '2026-08-29T14:00:00Z', items: [], limitations: [],
  } }))

  const requestId = '52000000-0000-4000-8000-000000000052'
  let authorized = false
  const posts: Array<{ body: Record<string, unknown>, idempotency: string | undefined }> = []
  await page.route('**/api/telehealth/v1/admin/operational-review', (route) => route.fulfill({ json: { requests: authorized ? [] : [{
    requestId,
    status: 'OperationalReview',
    complaintCategory: 'migraine',
    triageOutcome: 'TelehealthEligible',
    version: 12,
    createdAt: '2026-08-29T13:55:00Z',
    applicantOriginated: true,
  }] } }))
  await page.route(`**/api/telehealth/v1/admin/applicant-requests/${requestId}/queue-authorization`, async (route) => {
    if (route.request().method() === 'GET') {
      return route.fulfill({ json: {
        requestId, requestVersion: 12, requestStatus: 'OperationalReview',
        policyKey: 'SYNTHETIC_APPLICANT_REQUEST_QUEUE_AUTHORIZATION', policyVersion: 1,
        sourceMode: 'NON_PRODUCTION', compatibilityTarget: 'AVENCHART_SYNTHETIC_QUEUE_AUTHORIZATION_V1',
        authorizationSnapshotFingerprint: 'a'.repeat(64), resultValidThrough: '2026-08-29T15:00:00Z',
        practiceDisplayName: 'AvenChart Synthetic Practice', payerDisplayName: 'Harbor Mutual',
        productDisplayName: 'Synthetic Choice', currentLocationStateCode: 'GA', purposeCategory: 'migraine',
        dateOfService: '2026-08-29', candidateDisplayName: 'Dr. Synthetic',
        maskedProviderReference: 'Synthetic provider ••••1234', maskedBillingProviderReference: 'Synthetic billing provider ••••8800',
        serviceCategory: 'ProfessionalTelehealthConsultation', modality: 'RealTimeAudioVideo',
        authorizationReady: true, authorizationCompleted: false, authorizedAt: null, businessOutcome: null,
        syntheticEvidenceReviewed: false, practiceAccepted: false, patientCareQueueEntered: false,
        clinicianQueueEntered: false, doctorSearchStarted: false, appointmentCreated: false,
        realStateAuthorityVerified: false, realCredentialingVerified: false, renderingPhysicianAssigned: false,
        renderingPhysicianNetworkChecked: false, exactNetworkConfirmed: false, canonicalCoverageCreated: false,
        coverageSelected: false, coverageVerified: false, financialRouteCreated: false, patientContacted: false,
        queuePositionAssigned: false, encounterCreated: false, consentCreated: false, careAuthorized: false,
        prescribingEnabled: false, billingEnabled: false, claimCreated: false, integrationEnabled: false,
        externalCallPerformed: false, direction: 'Review the bounded synthetic evidence before deciding.',
        limitations: ['No real coverage, assignment, encounter, consent, or care authority is created.'],
      } })
    }
    posts.push({
      body: route.request().postDataJSON() as Record<string, unknown>,
      idempotency: route.request().headers()['x-idempotency-key'],
    })
    if (posts.length === 1) {
      return route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Authorization result unknown; retry unchanged.' }) })
    }
    authorized = true
    return route.fulfill({ json: { requestId, requestVersion: 13, requestStatus: 'Queued', authorizationCompleted: true } })
  })

  await page.goto('/login')
  await page.getByLabel('Username').fill(process.env.MODERN_UI_STAFF_USERNAME ?? 'admin')
  await page.getByLabel('Password').fill(process.env.MODERN_UI_STAFF_PASSWORD ?? 'pass')
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page).toHaveURL(/\/clinician\/dashboard$/, { timeout: 20_000 })
  await page.goto('/clinician/telehealth/admin')

  await expect(page.getByText('New-patient applicant request', { exact: false })).toBeVisible()
  await page.getByRole('button', { name: 'Review applicant queue authorization' }).click()
  const heading = page.getByRole('heading', { name: 'Applicant request queue authorization' })
  await expect(heading).toBeFocused()
  const submit = page.getByRole('button', { name: 'Accept into synthetic clinician queue' })
  await expect(submit).toBeDisabled()
  await page.getByLabel(/reviewed the bounded synthetic evidence/i).check()
  await page.getByLabel(/does not verify real insurance/i).check()
  await page.getByLabel(/accept this request into this practice's synthetic clinician work queue/i).check()
  await page.getByLabel(/queue entry is not clinician assignment/i).check()
  const accessibility = await new AxeBuilder({ page }).analyze()
  expect(accessibility.violations.filter((violation) => violation.impact === 'serious' || violation.impact === 'critical')).toEqual([])
  await page.setViewportSize({ width: 320, height: 720 })
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1)).toBe(true)
  await submit.click()
  const retry = page.getByRole('button', { name: 'Retry unchanged authorization' })
  await expect(page.getByRole('alert')).toContainText('Authorization result unknown')
  await expect(retry).toBeFocused()
  await retry.click()
  await expect(page.getByText('No requests are awaiting operational review.')).toBeVisible()
  expect(posts).toHaveLength(2)
  expect(posts[0].body).toEqual({
    expectedRequestVersion: 12,
    authorizationSnapshotFingerprint: 'a'.repeat(64),
    syntheticEvidenceReviewed: true,
    noCoverageGuaranteeAcknowledged: true,
    practiceAcceptsForQueueAcknowledged: true,
    queueNotCareAcknowledged: true,
  })
  expect(posts[1].body).toEqual(posts[0].body)
  expect(posts[0].idempotency).toBeTruthy()
  expect(posts[1].idempotency).toBe(posts[0].idempotency)
  expect(await page.evaluate(() => JSON.stringify(localStorage))).not.toContain('a'.repeat(64))
  expect(await page.evaluate(() => JSON.stringify(sessionStorage))).not.toContain('a'.repeat(64))
})

test('applicant queue status preserves the last confirmed state and recovers without disclosing an exact position', async ({ page }) => {
  const applicantId = '53000000-0000-4000-8000-000000000053'
  const applicantKey = 'q'.repeat(64)
  const requestId = '53000000-0000-4000-8000-000000000054'
  await page.addInitScript((session) => {
    sessionStorage.setItem('avenchart-ui.telehealthProspectiveApplicant', JSON.stringify(session))
  }, { applicantId, applicantAccessKey: applicantKey })
  await page.route('**/api/telehealth/v1/context', (route) => route.fulfill({ json: {
    available: true,
    practiceDisplayName: 'AvenChart Synthetic Practice',
    supportedStates: ['GA', 'CA', 'FL'],
    syntheticOnly: true,
    entryMessage: 'Synthetic demonstration only. This service is not available for patient care.',
  } }))

  let queueStatusMode: 'reviewing' | 'fail' | 'queued' = 'reviewing'
  await page.route('**/api/telehealth/v1/applicants/**', async (route) => {
    const request = route.request()
    const path = new URL(request.url()).pathname
    expect(request.headers()['x-avenchart-telehealth-applicant-key']).toBe(applicantKey)
    if (path.endsWith('/telehealth-request/queue-status')) {
      if (queueStatusMode === 'fail') {
        queueStatusMode = 'queued'
        await route.fulfill({
          status: 503,
          contentType: 'application/problem+json',
          body: JSON.stringify({ detail: 'Synthetic queue status temporarily unavailable.' }),
        })
        return
      }
      const queued = queueStatusMode === 'queued'
      await route.fulfill({ json: {
        requestId,
        requestStatus: queued ? 'Queued' : 'OperationalReview',
        requestVersion: queued ? 13 : 12,
        policyKey: 'SYNTHETIC_APPLICANT_REQUEST_QUEUE_STATUS',
        policyVersion: 1,
        sourceMode: 'NON_PRODUCTION',
        phase: queued ? 'InQueue' : 'Reviewing',
        headline: queued ? "You're in line" : 'Reviewing your request',
        detail: queued
          ? 'Approximately 2 requests are ahead. This can change for safety or operational reasons.'
          : 'Your practice has not placed this request in the physician queue yet.',
        approximateRequestsAhead: queued ? 2 : null,
        positionIsApproximate: queued,
        exactQueuePositionAssigned: false,
        waitEstimateAvailable: false,
        waitEstimateMessage: 'A wait-time estimate is not available in this synthetic demonstration.',
        requestUpdatedAt: '2026-08-29T14:00:00Z',
        snapshotAt: '2026-08-29T14:00:01Z',
        refreshAfterSeconds: 5,
        realtimeAvailable: false,
        practiceAccepted: queued,
        doctorSearchStarted: queued,
        renderingPhysicianAssigned: false,
        renderingPhysicianIdentityDisclosed: false,
        coverageVerified: false,
        consentCreated: false,
        careAuthorized: false,
        integrationEnabled: false,
        externalCallPerformed: false,
        safetyActions: ['If symptoms worsen or you are unsure it is safe to wait, seek in-person care.', 'Call 911 now for an emergency.'],
        limitations: ['Approximate synthetic status only; no clinician is assigned.'],
      } })
      return
    }
    if (path.endsWith(`/applicants/${applicantId}`)) {
      await route.fulfill({ json: {
        applicantId,
        status: 'SyntheticRequestCreated',
        version: 26,
        practiceDisplayName: 'AvenChart Synthetic Practice',
        residenceStateCode: 'GA',
        maskedEmail: 'q•••@example.test',
        maskedPhone: '(***) ***-0153',
        contactVerified: true,
        identityAssurance: 'ContactControlOnly',
        duplicateDisposition: 'NoCandidate',
        canonicalPatientCreated: true,
        verificationAttemptsRemaining: 0,
        expiresAt: '2026-10-31T23:59:59Z',
        demonstrationVerificationCode: null,
        nextAction: 'Wait for the synthetic practice queue status.',
        limitations: ['Synthetic demonstration only.'],
      } })
      return
    }
    await route.fulfill({
      status: 409,
      contentType: 'application/problem+json',
      body: JSON.stringify({ detail: 'This completed prerequisite is not reloaded in the queue-status journey.' }),
    })
  })

  await page.goto('/telehealth/new')
  await expect(page.getByRole('heading', { name: 'Reviewing your request' })).toBeVisible()
  await expect(page.getByText(/Practice accepted for synthetic queue/).locator('..')).toContainText('Not yet')
  const refresh = page.getByRole('button', { name: 'Refresh queue status now' })
  queueStatusMode = 'fail'
  await refresh.focus()
  await refresh.press('Enter')
  await expect(page.getByRole('alert').filter({ hasText: 'last confirmed status remains shown' })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Reviewing your request' })).toBeVisible()
  await expect(refresh).toBeFocused()
  const retry = page.getByRole('button', { name: 'Retry queue status' })
  await retry.focus()
  await retry.press('Enter')
  await expect(page.getByRole('heading', { name: "You're in line" })).toBeVisible()
  await expect(page.getByText(/Approximate requests ahead:/).locator('..')).toContainText('2')
  await expect(page.getByText(/Exact queue position assigned/).locator('..')).toContainText('No')
  await expect(page.getByText(/Wait estimate available/).locator('..')).toContainText('No')
  await expect(page.getByText(/Physician assigned/).locator('..')).toContainText('No')
  const accessibility = await new AxeBuilder({ page }).analyze()
  expect(accessibility.violations.filter((violation) => violation.impact === 'serious' || violation.impact === 'critical')).toEqual([])
  await page.setViewportSize({ width: 320, height: 720 })
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1)).toBe(true)
  const stored = await page.evaluate(() => JSON.stringify({ session: sessionStorage, local: localStorage }))
  expect(stored).not.toMatch(/requestId|queueStatus|approximateRequestsAhead|OperationalReview|Queued|physician/i)
})
