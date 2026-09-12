// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { expect as baseExpect, test, type Page } from '@playwright/test'
import AxeBuilder from '@axe-core/playwright'
const expect = baseExpect.configure({ timeout: 20_000 })

// Explicitly opt in against the isolated synthetic staging stack. No API
// responses are mocked. Chromium supplies synthetic camera/microphone tracks;
// this is NOT evidence for physical cameras or the Azure ACS transport.
test.use({ actionTimeout: 15_000, navigationTimeout: 30_000, permissions: ['camera', 'microphone'], launchOptions: { args: ['--use-fake-ui-for-media-stream', '--use-fake-device-for-media-stream'] } })
test.skip(process.env.TELEHEALTH_DEMO_E2E !== '1', 'Requires isolated synthetic staging data and explicit opt-in')

async function checkLabels(page: Page, labels: RegExp[]) {
  for (const label of labels) await page.getByLabel(label).check()
}

test('two tabs complete the patient and physician office demonstration through real APIs', async ({ page: patient, context, baseURL }) => {
  expect(['127.0.0.1', 'localhost']).toContain(new URL(baseURL!).hostname)
  const doctor = await context.newPage()
  const errors: string[] = []
  let wrapUpRequested = false
  patient.on('pageerror', error => errors.push(`Patient: ${error.message}`))
  doctor.on('pageerror', error => errors.push(`Physician: ${error.message}`))
  for (const tab of [patient, doctor]) tab.on('response', response => {
    // The transcript is intentionally unavailable once the server enters
    // wrap-up; its poll may beat the patient's independent status poll.
    if (wrapUpRequested && response.status() === 404 && /\/patient\/requests\/[^/]+\/conversation$/.test(new URL(response.url()).pathname)) return
    if (response.status() >= 400 && response.url().includes('/api/')) errors.push(`HTTP ${response.status()} ${new URL(response.url()).pathname}`)
  })
  await patient.goto('/telehealth')
  await expect(patient.getByRole('link', { name: 'Open physician workspace in a new tab' })).toHaveAttribute('href', '/login?telehealth=1')
  await patient.getByRole('link', { name: 'Sign in as an existing patient' }).click()
  await expect(patient.getByLabel('Email or username')).toHaveValue('mod-pat-0012@example.test')
  await patient.getByRole('button', { name: 'Sign in', exact: true }).click()
  await expect(patient).toHaveURL(/\/portal\/telehealth$/)
  await expect(patient.getByRole('heading', { name: 'Immediate telehealth request' })).toBeVisible()
  if (process.env.TELEHEALTH_DEMO_RESUME !== '1') {
    await patient.getByRole('button', { name: 'Start sleep demo' }).click()
    await patient.getByRole('button', { name: 'Confirm current location' }).click()
    await patient.getByRole('button', { name: 'Evaluate synthetic triage' }).click()
    await checkLabels(patient, [/confirm these current demographic/i, /reviewed this synthetic clinical-list summary/i, /entered synthetic demonstration data only/i, /selected and confirmed this existing synthetic coverage/i, /affirmatively accept this exact synthetic acknowledgment/i])
    await patient.getByRole('button', { name: 'Confirm details and join demo queue' }).click()
    await expect(patient.getByRole('heading', { name: "You're in line" })).toBeVisible({ timeout: 20_000 })
    await expect(patient.getByRole('button', { name: 'Confirm details and join demo queue' })).toHaveCount(0)
  }
  await doctor.goto('/login?telehealth=1')
  await expect(doctor.getByLabel('Username', { exact: true })).toHaveValue('gold-provider-01')
  await doctor.getByRole('button', { name: 'Sign in', exact: true }).click()
  await expect(doctor).toHaveURL(/\/clinician\/telehealth\/physician$/)
  await expect(doctor.getByRole('heading', { name: 'Telehealth shift', exact: true })).toBeVisible()
  const note = 'Synthetic office demonstration: reviewed the supplied fixture, not a real clinical assessment.'
  if (process.env.TELEHEALTH_DEMO_RESUME_CONSULTATION !== '1') {
  await doctor.getByRole('button', { name: 'See next patient', exact: true }).click()
  await expect(patient.getByRole('button', { name: 'Check camera and microphone' })).toBeVisible({ timeout: 25_000 })
  for (const tab of [patient, doctor]) {
    await tab.getByRole('button', { name: 'Check camera and microphone' }).click()
    await expect(tab.getByText(/Device check passed/)).toBeVisible({ timeout: 40_000 })
  }
  await patient.getByRole('button', { name: 'Enter synthetic waiting room' }).click()
  await doctor.getByRole('button', { name: 'Enter physician waiting room' }).click()
  await doctor.getByRole('button', { name: 'Start browser media POC' }).click()
  await patient.getByRole('button', { name: 'Join browser media POC' }).click()
  for (const tab of [patient, doctor]) await expect(tab.getByText('Local browser-to-browser media connected. AvenChart does not receive or store media.')).toBeVisible({ timeout: 30_000 })
  await checkLabels(doctor, [/Patient identity discussion completed/, /Callback number reconfirmed/, /Privacy and other participants discussed/, /Telehealth consent discussion completed/, /No concerning symptom change/, /Emergency plan reviewed/, /Synthetic communication check is sufficient/])
  await doctor.getByRole('button', { name: 'Start synthetic lifecycle', exact: true }).click()
  await expect(doctor.getByRole('heading', { name: 'Consultation workspace' })).toBeVisible()
  await expect(patient.getByRole('heading', { name: 'Synthetic consultation lifecycle started' })).toBeVisible({ timeout: 25_000 })
  for (const label of ['Subjective', 'Objective', 'Assessment', 'Plan']) await doctor.getByRole('textbox', { name: label, exact: true }).fill(note)
  await doctor.getByRole('button', { name: 'Save unsigned draft' }).click()
  await expect(doctor.getByText(/Unsigned synthetic draft version 1 saved/)).toBeVisible()
  }
  await doctor.reload()
  await expect(doctor.getByRole('textbox', { name: 'Subjective', exact: true })).toHaveValue(note)
  if (process.env.TELEHEALTH_DEMO_RESUME_WRAPUP !== '1') {
  await patient.getByLabel('Demonstration message').fill('Synthetic patient hello from a separate tab.')
  await patient.getByLabel(/I confirm this contains synthetic demonstration data only/).check()
  await patient.getByRole('button', { name: 'Add synthetic message' }).click()
  await expect(doctor.getByText('Synthetic patient hello from a separate tab.')).toBeVisible()
  await doctor.getByLabel('Demonstration message').fill('Synthetic physician reply received.')
  await doctor.getByLabel(/I confirm this contains synthetic demonstration data only/).check()
  await doctor.getByRole('button', { name: 'Add synthetic message' }).click()
  await expect(patient.getByText('Synthetic physician reply received.')).toBeVisible()
  // The real ten-second active-work poll must retain the restored visit.
  await expect.poll(async () => {
    await doctor.getByRole('button', { name: 'Refresh', exact: true }).click()
    return doctor.getByRole('textbox', { name: 'Subjective', exact: true }).inputValue()
  }).toBe(note)
  await checkLabels(doctor, [/The synthetic session is ended/, /Documentation and any future safety disposition remain incomplete/, /I remain responsible for this unfinished synthetic visit/])
  wrapUpRequested = true
  await doctor.getByRole('button', { name: 'End synthetic session and enter wrap-up' }).click()
  await expect(doctor.getByRole('heading', { name: 'Wrap-up is active' })).toBeVisible()
  }
  await doctor.reload()
  await expect(doctor.getByRole('heading', { name: 'Wrap-up is active' })).toBeVisible()
  await expect(doctor.getByRole('textbox', { name: 'Subjective', exact: true })).toHaveValue(note)
  // Subsequent steps are deliberately normal UI operations, preserving every
  // authored field and explicit confirmation rather than bypassing safeguards.
  const disposition = doctor.locator('.telehealth-disposition-draft')
  await disposition.getByRole('combobox', { name: 'Disposition', exact: true }).selectOption('TreatedTelehealth')
  await disposition.getByLabel('The available evaluation was adequate for this selected disposition.').check()
  await disposition.getByRole('combobox', { name: 'Follow-up owner', exact: true }).selectOption('TreatingPhysician')
  await disposition.getByLabel('Physician-authored follow-up timeframe').fill('Synthetic demonstration follow-up only')
  await disposition.getByLabel('Physician-authored next-step instructions').fill('Demo next step: review this synthetic plan with the presenter.')
  await disposition.getByLabel('Physician-authored warning signs and escalation instructions').fill('Demo only: no real assessment or advice was provided. Seek appropriate care for real symptoms.')
  await disposition.getByLabel('Communication method').selectOption('NotYetCommunicated')
  await disposition.getByLabel(/I confirm this draft contains synthetic demonstration data only/).check()
  await disposition.getByRole('button', { name: /^Record (revised )?safety draft$/ }).click()
  await expect(disposition.getByText(/Unsigned safety-disposition draft version \d+ recorded/)).toBeVisible()
  await checkLabels(doctor, [/I reviewed the current synthetic SOAP and safety-disposition draft versions/, /I retain responsibility for clinical review/, /I understand this does not create an automatic patient delivery/, /I confirm this is synthetic demonstration evidence with no legal effect/])
  await doctor.getByRole('button', { name: 'Record final clinical-review evidence' }).click()
  await expect(doctor.getByText(/Final clinical-review evidence version \d+ was recorded/)).toBeVisible()
  await checkLabels(doctor, [/I reviewed the exact current SOAP/, /I confirm this is synthetic-only and has no legal/])
  await doctor.getByRole('button', { name: 'Record synthetic encounter lock' }).click()
  await expect(doctor.getByRole('heading', { name: 'Return to availability' })).toBeVisible()
  await doctor.reload()
  await expect(doctor.getByRole('heading', { name: 'Return to availability' })).toBeVisible()
  await expect(doctor.getByRole('textbox', { name: 'Subjective', exact: true })).toBeDisabled()
  await checkLabels(doctor, [/I reviewed the governed encounter lock/, /I confirm this synthetic-only closure has no billing/])
  await doctor.getByRole('button', { name: 'Close synthetic visit and return to availability' }).click()
  await expect(doctor.getByText(/Synthetic visit lifecycle closed/)).toBeVisible()
  await expect(patient.getByRole('heading', { name: 'Synthetic post-visit receipt' })).toBeVisible({ timeout: 25_000 })
  await expect(patient.getByText('Demo next step: review this synthetic plan with the presenter.')).toBeVisible()
  await doctor.getByText('End availability', { exact: true }).click()
  await checkLabels(doctor, [/I confirm I hold no active telehealth reservation/, /I understand this ends only my synthetic shift/])
  await doctor.getByRole('button', { name: 'End idle telehealth shift' }).click()
  await expect(doctor.getByRole('button', { name: 'Start telehealth shift', exact: true })).toBeEnabled()
  for (const tab of [patient, doctor]) {
    const results = await new AxeBuilder({ page: tab }).analyze()
    expect(results.violations.filter(v => v.impact === 'serious' || v.impact === 'critical')).toEqual([])
    await tab.setViewportSize({ width: 1280, height: 720 })
    expect(await tab.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1)).toBe(true)
  }
  expect(errors).toEqual([])
})
