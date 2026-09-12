// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import type { TelehealthDevicePreflight } from './api.ts'

type MediaTrackLike = { kind: string; stop(): void }
type MediaStreamLike = { getTracks(): MediaTrackLike[] }

export type TelehealthPreflightEnvironment = {
  secureContext: boolean
  peerConnectionAvailable: boolean
  speakerOutputAvailable: boolean
  getUserMedia?: (constraints: MediaStreamConstraints) => Promise<MediaStreamLike>
  effectiveConnectionType?: string
}

export type TelehealthPreflightResult =
  | { status: 'passed'; evidence: TelehealthDevicePreflight }
  | { status: 'failed'; message: string }

export async function runTelehealthDevicePreflight(
  environment: TelehealthPreflightEnvironment = browserEnvironment(),
): Promise<TelehealthPreflightResult> {
  if (!environment.secureContext || !environment.peerConnectionAvailable || !environment.getUserMedia) {
    return { status: 'failed', message: 'This browser cannot run the secure telehealth device check.' }
  }

  let stream: MediaStreamLike | null = null
  let expired = false
  let timeout: ReturnType<typeof setTimeout> | undefined
  try {
    const request = environment.getUserMedia({ audio: true, video: true }).then((result) => {
      // getUserMedia cannot be cancelled: release any late permission grant.
      if (expired) result.getTracks().forEach((track) => track.stop())
      return result
    })
    stream = await Promise.race([
      request,
      new Promise<never>((_, reject) => {
        timeout = setTimeout(() => { expired = true; reject(new Error('permission-timeout')) }, 30_000)
      }),
    ])
    const tracks = stream.getTracks()
    const cameraAvailable = tracks.some((track) => track.kind === 'video')
    const microphoneAvailable = tracks.some((track) => track.kind === 'audio')
    if (!cameraAvailable || !microphoneAvailable || !environment.speakerOutputAvailable) {
      return { status: 'failed', message: 'Camera, microphone, and speaker access are all required for this device check.' }
    }
    return {
      status: 'passed',
      evidence: {
        browserSupported: true,
        cameraAvailable,
        microphoneAvailable,
        speakerAvailable: true,
        networkQuality: normalizeNetworkQuality(environment.effectiveConnectionType),
        syntheticDataConfirmed: true,
      },
    }
  } catch {
    return { status: 'failed', message: expired
      ? 'The device check timed out. Allow camera and microphone access in your browser, then try again. If a permission prompt is still open, dismiss it before retrying.'
      : 'Camera or microphone permission was unavailable. Check browser permissions and try again.' }
  } finally {
    clearTimeout(timeout)
    stream?.getTracks().forEach((track) => track.stop())
  }
}

function normalizeNetworkQuality(effectiveType?: string): 'unknown' | 'limited' | 'good' {
  if (!effectiveType) return 'unknown'
  return ['slow-2g', '2g', '3g'].includes(effectiveType.toLowerCase()) ? 'limited' : 'good'
}

function browserEnvironment(): TelehealthPreflightEnvironment {
  const connection = (navigator as Navigator & { connection?: { effectiveType?: string } }).connection
  return {
    secureContext: window.isSecureContext,
    peerConnectionAvailable: typeof RTCPeerConnection === 'function',
    speakerOutputAvailable: typeof HTMLAudioElement === 'function',
    getUserMedia: navigator.mediaDevices?.getUserMedia
      ? (constraints) => navigator.mediaDevices.getUserMedia(constraints)
      : undefined,
    effectiveConnectionType: connection?.effectiveType,
  }
}
