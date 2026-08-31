// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import TelehealthLocalWebRtcPocPanel from './TelehealthLocalWebRtcPocPanel.tsx'
import type { TelehealthConnectionGrant } from './api.ts'

const grant = {
  sessionId: '00000000-0000-4000-8000-000000000001',
  grantId: '00000000-0000-4000-8000-000000000002',
  joinCredential: 'local-poc-credential',
  expiresAt: '2026-08-31T12:00:00Z',
  mediaTransportEnabled: true,
} as TelehealthConnectionGrant

const originalMediaDevices = Object.getOwnPropertyDescriptor(navigator, 'mediaDevices')
describe('TelehealthLocalWebRtcPocPanel', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    if (originalMediaDevices) Object.defineProperty(navigator, 'mediaDevices', originalMediaDevices)
    else Reflect.deleteProperty(navigator, 'mediaDevices')
  })

  it('fails closed before any signaling when browser media prerequisites are unavailable', () => {
    const writeSignal = vi.fn()
    const readSignals = vi.fn()

    render(<TelehealthLocalWebRtcPocPanel grant={grant} role="patient" writeSignal={writeSignal} readSignals={readSignals} />)

    expect(screen.getByText(/NON_PRODUCTION local-only synthetic demonstration/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'End browser media POC' })).toBeDisabled()
    fireEvent.click(screen.getByRole('button', { name: 'Join browser media POC' }))

    expect(screen.getByRole('status')).toHaveTextContent(/requires a secure browser context/i)
    expect(writeSignal).not.toHaveBeenCalled()
    expect(readSignals).not.toHaveBeenCalled()
  })

  it('queues a remote ICE candidate until the offer establishes a remote description', async () => {
    const peer = {
      remoteDescription: null as RTCSessionDescriptionInit | null,
      connectionState: 'new',
      ontrack: null as RTCPeerConnection['ontrack'],
      onicecandidate: null as RTCPeerConnection['onicecandidate'],
      onconnectionstatechange: null as RTCPeerConnection['onconnectionstatechange'],
      addTrack: vi.fn(),
      addIceCandidate: vi.fn().mockResolvedValue(undefined),
      close: vi.fn(),
      createAnswer: vi.fn().mockResolvedValue({ type: 'answer', sdp: 'opaque-answer' }),
      setLocalDescription: vi.fn().mockResolvedValue(undefined),
      setRemoteDescription: vi.fn(async (description: RTCSessionDescriptionInit) => { peer.remoteDescription = description }),
    }
    vi.stubGlobal('isSecureContext', true)
    const createPeerConnection = vi.fn(function FakePeerConnection() { return peer })
    vi.stubGlobal('RTCPeerConnection', createPeerConnection)
    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: { getUserMedia: vi.fn().mockResolvedValue({ getTracks: () => [] }) },
    })
    const writeSignal = vi.fn().mockResolvedValue(undefined)
    const readSignals = vi.fn()
      .mockResolvedValueOnce({
        latestSequence: 2,
        expiresAt: grant.expiresAt,
        signals: [
          { sequence: 1, kind: 'candidate', payload: '{"candidate":"candidate:opaque"}' },
          { sequence: 2, kind: 'offer', payload: '{"type":"offer","sdp":"opaque-offer"}' },
        ],
      })
      .mockResolvedValue({ latestSequence: 2, expiresAt: grant.expiresAt, signals: [] })

    render(<TelehealthLocalWebRtcPocPanel grant={grant} role="patient" writeSignal={writeSignal} readSignals={readSignals} />)
    fireEvent.click(screen.getByRole('button', { name: 'Join browser media POC' }))

    await waitFor(() => expect(peer.addIceCandidate).toHaveBeenCalledWith({ candidate: 'candidate:opaque' }))
    expect(createPeerConnection).toHaveBeenCalledWith()
    expect(peer.setRemoteDescription.mock.invocationCallOrder[0]).toBeLessThan(peer.addIceCandidate.mock.invocationCallOrder[0])
    expect(writeSignal).toHaveBeenCalledWith('answer', JSON.stringify({ type: 'answer', sdp: 'opaque-answer' }))
  })

  it('uses the selected local camera and microphone when joining', async () => {
    const peer = {
      connectionState: 'new',
      ontrack: null as RTCPeerConnection['ontrack'],
      onicecandidate: null as RTCPeerConnection['onicecandidate'],
      onconnectionstatechange: null as RTCPeerConnection['onconnectionstatechange'],
      addTrack: vi.fn(),
      close: vi.fn(),
    }
    const getUserMedia = vi.fn().mockResolvedValue({ getTracks: () => [] })
    vi.stubGlobal('isSecureContext', true)
    vi.stubGlobal('RTCPeerConnection', vi.fn(function FakePeerConnection() { return peer }))
    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: {
        enumerateDevices: vi.fn().mockResolvedValue([
          { kind: 'videoinput', deviceId: 'camera-usb', label: 'USB camera' },
          { kind: 'audioinput', deviceId: 'microphone-usb', label: 'USB microphone' },
          { kind: 'audiooutput', deviceId: 'speaker-usb', label: 'USB speaker' },
        ]),
        getUserMedia,
      },
    })
    const writeSignal = vi.fn().mockResolvedValue(undefined)
    const readSignals = vi.fn().mockResolvedValue({ latestSequence: 0, expiresAt: grant.expiresAt, signals: [] })

    render(<TelehealthLocalWebRtcPocPanel grant={grant} role="patient" writeSignal={writeSignal} readSignals={readSignals} />)

    await screen.findByRole('option', { name: 'USB camera' })
    fireEvent.change(screen.getByRole('combobox', { name: 'Camera for browser media POC' }), { target: { value: 'camera-usb' } })
    fireEvent.change(screen.getByRole('combobox', { name: 'Microphone for browser media POC' }), { target: { value: 'microphone-usb' } })
    fireEvent.click(screen.getByRole('button', { name: 'Join browser media POC' }))

    await waitFor(() => expect(getUserMedia).toHaveBeenCalledWith({
      audio: { deviceId: { exact: 'microphone-usb' } },
      video: { deviceId: { exact: 'camera-usb' } },
    }))
  })
})
