// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { describe, expect, it, vi } from 'vitest'
import { runTelehealthDevicePreflight, type TelehealthPreflightEnvironment } from './devicePreflight.ts'

describe('telehealth device preflight', () => {
  it('returns only coarse evidence and immediately stops test tracks', async () => {
    const stopCamera = vi.fn()
    const stopMicrophone = vi.fn()
    const environment: TelehealthPreflightEnvironment = {
      secureContext: true,
      peerConnectionAvailable: true,
      speakerOutputAvailable: true,
      effectiveConnectionType: '4g',
      getUserMedia: vi.fn(async () => ({
        getTracks: () => [
          { kind: 'video', stop: stopCamera },
          { kind: 'audio', stop: stopMicrophone },
        ],
      })),
    }

    const result = await runTelehealthDevicePreflight(environment)

    expect(result).toEqual({
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
    expect(stopCamera).toHaveBeenCalledOnce()
    expect(stopMicrophone).toHaveBeenCalledOnce()
    expect(JSON.stringify(result)).not.toMatch(/label|deviceId|groupId/i)
  })

  it('stops any acquired track when a required capability is missing', async () => {
    const stop = vi.fn()
    const result = await runTelehealthDevicePreflight({
      secureContext: true,
      peerConnectionAvailable: true,
      speakerOutputAvailable: true,
      getUserMedia: vi.fn(async () => ({ getTracks: () => [{ kind: 'audio', stop }] })),
    })

    expect(result.status).toBe('failed')
    expect(stop).toHaveBeenCalledOnce()
  })

  it('fails plainly without requesting media in an unsupported context', async () => {
    const getUserMedia = vi.fn()
    const result = await runTelehealthDevicePreflight({
      secureContext: false,
      peerConnectionAvailable: true,
      speakerOutputAvailable: true,
      getUserMedia,
    })

    expect(result).toEqual({ status: 'failed', message: 'This browser cannot run the secure telehealth device check.' })
    expect(getUserMedia).not.toHaveBeenCalled()
  })
})
