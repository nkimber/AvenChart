// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { expect, test, type BrowserContext, type Page } from '@playwright/test'
import { execFileSync } from 'node:child_process'
import { randomBytes } from 'node:crypto'

const azure = process.env.TELEHEALTH_AZURE_DEMO_E2E === '1'
test.skip(process.env.TELEHEALTH_DEMO_E2E !== '1' && !azure, 'Requires an explicitly selected isolated synthetic demo')
test.use({ trace: 'off', video: 'off', launchOptions: { args: ['--use-fake-device-for-media-stream'] } })

declare global {
  interface Window {
    __telehealthAcceptanceTracks: MediaStreamTrack[]
    __telehealthAcceptancePeers: RTCPeerConnection[]
  }
}

async function instrument(context: BrowserContext) {
  await context.addInitScript(() => {
    window.__telehealthAcceptanceTracks = []
    window.__telehealthAcceptancePeers = []
    const capture = navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices)
    navigator.mediaDevices.getUserMedia = async constraints => {
      const stream = await capture(constraints)
      window.__telehealthAcceptanceTracks.push(...stream.getTracks())
      return stream
    }
    const NativePeer = window.RTCPeerConnection
    window.RTCPeerConnection = class extends NativePeer {
      constructor(configuration?: RTCConfiguration) { super(configuration); window.__telehealthAcceptancePeers.push(this) }
    }
  })
}

async function prepare(page: Page) {
  await page.bringToFront()
  await page.getByRole('button', { name: 'Check devices and enter room' }).click()
  await expect(page.getByRole('button', { name: azure ? 'Join video visit' : /^(Start|Join) browser media POC$/ })).toBeVisible({ timeout: 40_000 })
}
async function join(page: Page) { await page.bringToFront(); await page.getByRole('button', { name: azure ? 'Join video visit' : /^(Start|Join) browser media POC$/ }).click() }
async function connected(pages: Page[]) {
  for (const page of pages) {
    await expect(page.getByText(azure ? 'Patient and physician are connected.' : 'Local browser-to-browser media connected. AvenChart does not receive or store media.', { exact: true })).toBeVisible({ timeout: 45_000 })
    await expect.poll(() => page.locator('video[aria-label="Other participant video"], [aria-label="Other participant video"] video').evaluateAll(videos => videos.some(video => (video as HTMLVideoElement).videoWidth > 0 && !(video as HTMLVideoElement).paused)), { timeout: 30_000 }).toBe(true)
    if (!azure) {
      // Real RTP from Chromium's synthetic microphone, not physical audibility.
      await expect.poll(() => page.evaluate(async () => {
        const peer = window.__telehealthAcceptancePeers.findLast(item => item.connectionState === 'connected')
        if (!peer) return false
        const stats = await peer.getStats()
        return Array.from(stats.values()).some(item => item.type === 'inbound-rtp' && item.kind === 'audio' && item.packetsReceived > 0)
      }), { timeout: 30_000 }).toBe(true)
    }
  }
}

test('isolated patient and physician connect, recover, reject cross-room access, and end without clinical state', async ({ browser, page: launcher, request, baseURL }) => {
  expect(azure ? ['avenchart.kimber.dev'] : ['127.0.0.1', 'localhost']).toContain(new URL(baseURL!).hostname)
  const api = process.env.TELEHEALTH_API_BASE_URL ?? baseURL!
  expect(azure ? ['avenchart.kimber.dev'] : ['127.0.0.1', 'localhost']).toContain(new URL(api).hostname)
  if (azure) expect(new URL(api).protocol).toBe('https:')
  const contexts = await Promise.all([browser.newContext({ permissions: ['camera', 'microphone'] }), browser.newContext({ permissions: ['camera', 'microphone'] })])
  for (const context of contexts) await instrument(context)
  const [patient, physician] = await Promise.all(contexts.map(context => context.newPage()))
  const errors: string[] = []
  for (const page of [launcher, patient, physician]) page.on('pageerror', error => errors.push(error.message))
  let runId = '', observer = ''
  try {
    await launcher.goto('/telehealth/demo')
    await launcher.getByRole('checkbox').check()
    const started = Date.now()
    await launcher.getByRole('button', { name: 'Create synthetic demo', exact: true }).click()
    const patientUrl = await launcher.getByRole('link', { name: 'Open patient session' }).getAttribute('href')
    const physicianUrl = await launcher.getByRole('link', { name: 'Open physician session' }).getAttribute('href')
    expect(new URL(patientUrl!).origin).toBe(new URL(baseURL!).origin)
    expect(new URL(physicianUrl!).origin).toBe(new URL(baseURL!).origin)
    runId = new URL(patientUrl!).pathname.split('/')[3]
    observer = await launcher.evaluate(() => JSON.parse(sessionStorage.getItem('avenchart-ui.telehealth-demo.launcher')!).key as string)
    await patient.goto(patientUrl!)
    await physician.goto(physicianUrl!)
    await prepare(patient)
    await prepare(physician)
    expect(new URL(patient.url()).hash).toBe('')
    expect(new URL(physician.url()).hash).toBe('')
    // Either party may enter first; these are independent browser sessions.
    await join(patient); await join(physician)
    await connected([patient, physician])
    const elapsed = Date.now() - started
    expect(elapsed, 'Warm synthetic connection should take less than two minutes').toBeLessThan(120_000)
    console.info(`Synthetic two-session connection: ${elapsed} ms`)

    const patientKey = await patient.evaluate(({ runId }) => JSON.parse(sessionStorage.getItem(`avenchart-ui.telehealth-demo.${runId}.patient`)!).key as string, { runId })
    const headers = { 'X-AvenChart-Telehealth-Demo-Key': patientKey }
    const outsiderKey = randomBytes(32).toString('hex')
    expect((await request.get(`${api}/api/telehealth/v1/demo/runs/${runId}`, { headers: { 'X-AvenChart-Telehealth-Demo-Key': outsiderKey } })).status()).toBe(404)
    expect((await request.post(`${api}/api/telehealth/v1/demo/runs/${runId}/physician/heartbeat`, { headers, data: { stage: 'Connected' } })).status()).toBe(404)
    const anotherKey = randomBytes(32).toString('hex')
    const another = await request.post(`${api}/api/telehealth/v1/demo/runs`, { headers: { 'X-Idempotency-Key': crypto.randomUUID() }, data: { syntheticDataConfirmed: true, observerKey: anotherKey } })
    expect(another.ok()).toBe(true)
    const anotherRun = await another.json() as { runId: string }
    expect((await request.get(`${api}/api/telehealth/v1/demo/runs/${anotherRun.runId}`, { headers })).status()).toBe(404)
    await request.post(`${api}/api/telehealth/v1/demo/runs/${anotherRun.runId}/end`, { headers: { 'X-AvenChart-Telehealth-Demo-Key': anotherKey }, data: {} })
    const invite = new URLSearchParams(new URL(patientUrl!).hash.slice(1)).get('invite')!
    expect((await request.post(`${api}/api/telehealth/v1/demo/runs/${runId}/patient/redeem`, { data: { invitation: invite, sessionKey: outsiderKey } })).status()).toBe(404)

    for (const page of [patient, physician]) {
      await page.reload()
      await prepare(page); await join(page)
      await connected([patient, physician])
    }
    await contexts[0].setOffline(true)
    await expect(patient.getByRole('alert')).toContainText('Live room status is temporarily unavailable', { timeout: 15_000 })
    await contexts[0].setOffline(false)
    await patient.reload(); await prepare(patient); await join(patient)
    await connected([patient, physician])

    if (!azure) {
      // Fault injection for a disconnected physical track; actual USB hardware
      // and drivers still need a manual device-matrix acceptance run.
      await patient.evaluate(() => window.__telehealthAcceptanceTracks.findLast(track => track.kind === 'video' && track.readyState === 'live')!.dispatchEvent(new Event('ended')))
      await expect(patient.getByText(/camera or microphone was disconnected/i)).toBeVisible()
      await join(patient)
      await connected([patient, physician])
    }

    const restart = process.env.TELEHEALTH_QA_API_CONTAINER
    if (restart) {
      expect(azure).toBe(false)
      const inspection = JSON.parse(execFileSync('docker', ['inspect', restart], { encoding: 'utf8' })) as Array<{ Config: { Labels: Record<string, string> } }>
      expect(inspection[0].Config.Labels['com.docker.compose.project']).toMatch(/^avenchart-telehealth-.*(?:qa|ci)$/)
      expect(inspection[0].Config.Labels['com.docker.compose.service']).toBe('api')
      execFileSync('docker', ['restart', restart], { stdio: 'pipe' })
      await expect.poll(async () => {
        try {
          const response = await request.get(`${api}/health/ready`, { timeout: 5000 })
          return response.ok() && (await response.json() as { status?: string }).status === 'healthy'
        } catch { return false }
      }, { timeout: 120_000, intervals: [1000, 2000, 5000] }).toBe(true)
      // The control-plane outage can end either panel's signaling attempt.
      // Recover both durable role sessions after the transient relay restarts.
      await Promise.all([patient.reload(), physician.reload()])
      await prepare(patient); await prepare(physician)
      await join(patient); await join(physician)
      await connected([patient, physician])
    } else test.info().annotations.push({ type: 'not-executed', description: 'API restart: set TELEHEALTH_QA_API_CONTAINER to an isolated QA/CI API container.' })

    await physician.getByRole('button', { name: 'End this demo', exact: true }).click()
    for (const page of [patient, physician]) {
      await expect(page.getByText(/This synthetic demo has ended/)).toBeVisible({ timeout: 15_000 })
      await expect.poll(() => page.evaluate(() => window.__telehealthAcceptanceTracks.every(track => track.readyState === 'ended'))).toBe(true)
    }
    expect((await request.post(`${api}/api/telehealth/v1/demo/runs/${runId}/patient/${azure ? 'calling' : 'signals/read'}`, { headers, data: azure ? {} : { afterSequence: 0 } })).status()).toBe(404)
    await launcher.reload()
    await expect(launcher.getByRole('button', { name: 'Create another synthetic demo' })).toBeVisible()
    expect(errors).toEqual([])
  } finally {
    if (runId && observer) await request.post(`${api}/api/telehealth/v1/demo/runs/${runId}/end`, { headers: { 'X-AvenChart-Telehealth-Demo-Key': observer }, data: {} }).catch(() => undefined)
    await Promise.all(contexts.map(context => context.close()))
  }
})

test('camera denial can be corrected without creating another room or consuming the invitation again', async ({ browser, page: launcher, request, baseURL }) => {
  test.skip(azure, 'Local deterministic permission test; no live Azure mutation')
  expect(['127.0.0.1', 'localhost']).toContain(new URL(baseURL!).hostname)
  const api = process.env.TELEHEALTH_API_BASE_URL ?? baseURL!
  const context = await browser.newContext()
  await context.grantPermissions(['microphone'])
  const patient = await context.newPage()
  let runId = '', observer = ''
  try {
    await launcher.goto('/telehealth/demo')
    await launcher.getByRole('checkbox').check()
    await launcher.getByRole('button', { name: 'Create synthetic demo', exact: true }).click()
    const invitation = await launcher.getByRole('link', { name: 'Open patient session' }).getAttribute('href')
    runId = new URL(invitation!).pathname.split('/')[3]
    observer = await launcher.evaluate(() => JSON.parse(sessionStorage.getItem('avenchart-ui.telehealth-demo.launcher')!).key as string)
    await patient.goto(invitation!)
    expect(await patient.evaluate(async () => (await navigator.permissions.query({ name: 'camera' as PermissionName })).state)).toBe('denied')
    await patient.getByRole('button', { name: 'Check devices and enter room' }).click()
    await expect(patient.getByRole('alert')).toContainText('Camera or microphone permission was unavailable', { timeout: 10_000 })
    await context.grantPermissions(['camera', 'microphone'])
    await prepare(patient)
    expect(new URL(patient.url()).pathname).toContain(runId)
    expect(new URL(patient.url()).hash).toBe('')
  } finally {
    if (runId && observer) await request.post(`${api}/api/telehealth/v1/demo/runs/${runId}/end`, { headers: { 'X-AvenChart-Telehealth-Demo-Key': observer }, data: {} }).catch(() => undefined)
    await context.close()
  }
})
