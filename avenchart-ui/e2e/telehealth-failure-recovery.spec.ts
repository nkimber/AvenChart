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
