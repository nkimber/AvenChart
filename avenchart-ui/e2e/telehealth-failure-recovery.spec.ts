// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

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
  await expect(page.getByRole('alert')).toContainText('Synthetic queue unavailable.')
  await expect(page.getByRole('button', { name: 'Authorize to clinician queue' })).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'Try again' })).toBeEnabled()
})
