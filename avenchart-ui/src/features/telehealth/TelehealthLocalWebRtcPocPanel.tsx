// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useEffect, useRef, useState } from 'react'
import type { TelehealthConnectionGrant, TelehealthLocalWebRtcSignalKind, TelehealthLocalWebRtcSignalRead } from './api.ts'

type Props = {
  grant: TelehealthConnectionGrant
  role: 'patient' | 'physician'
  writeSignal: (kind: TelehealthLocalWebRtcSignalKind, payload: string) => Promise<unknown>
  readSignals: (afterSequence: number, signal?: AbortSignal) => Promise<TelehealthLocalWebRtcSignalRead>
  onConnectionStateChange?: (connected: boolean) => void
}

export default function TelehealthLocalWebRtcPocPanel({ grant, role, writeSignal, readSignals, onConnectionStateChange }: Props) {
  const localVideo = useRef<HTMLVideoElement>(null)
  const remoteVideo = useRef<HTMLVideoElement>(null)
  const peer = useRef<RTCPeerConnection | null>(null)
  const localStream = useRef<MediaStream | null>(null)
  const pendingRemoteCandidates = useRef<RTCIceCandidateInit[]>([])
  const sequence = useRef(0)
  const polling = useRef(false)
  const connectionStateChange = useRef(onConnectionStateChange)
  const [state, setState] = useState<'idle' | 'joining' | 'waiting' | 'connected' | 'ended' | 'error'>('idle')
  const [message, setMessage] = useState('Join only when both participants are in the local POC waiting room.')

  useEffect(() => {
    connectionStateChange.current = onConnectionStateChange
  }, [onConnectionStateChange])

  useEffect(() => () => {
    connectionStateChange.current?.(false)
    peer.current?.close()
    localStream.current?.getTracks().forEach((track) => track.stop())
  }, [])

  function stop() {
    connectionStateChange.current?.(false)
    peer.current?.close()
    peer.current = null
    pendingRemoteCandidates.current = []
    localStream.current?.getTracks().forEach((track) => track.stop())
    localStream.current = null
    if (localVideo.current) localVideo.current.srcObject = null
    if (remoteVideo.current) remoteVideo.current.srcObject = null
  }

  const fail = useCallback((error: unknown) => {
    connectionStateChange.current?.(false)
    setState('error')
    setMessage(error instanceof Error ? error.message : 'The local media POC could not connect. End and retry both participants.')
  }, [])

  const receive = useCallback(async (kind: TelehealthLocalWebRtcSignalKind, payload: string) => {
    const connection = peer.current
    if (!connection) return
    try {
      if (kind === 'offer') {
        const offer = JSON.parse(payload) as RTCSessionDescriptionInit
        await connection.setRemoteDescription(offer)
        await flushRemoteCandidates(connection)
        const answer = await connection.createAnswer()
        await connection.setLocalDescription(answer)
        await writeSignal('answer', JSON.stringify(answer))
      } else if (kind === 'answer') {
        await connection.setRemoteDescription(JSON.parse(payload) as RTCSessionDescriptionInit)
        await flushRemoteCandidates(connection)
      } else if (payload !== 'null') {
        const candidate = JSON.parse(payload) as RTCIceCandidateInit
        if (connection.remoteDescription) {
          await connection.addIceCandidate(candidate)
        } else {
          pendingRemoteCandidates.current.push(candidate)
        }
      }
    } catch (error) {
      fail(error)
    }
  }, [fail, writeSignal])

  async function flushRemoteCandidates(connection: RTCPeerConnection) {
    const candidates = pendingRemoteCandidates.current
    pendingRemoteCandidates.current = []
    for (const candidate of candidates) await connection.addIceCandidate(candidate)
  }

  async function join() {
    if (state === 'joining' || peer.current) return
    if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia || typeof RTCPeerConnection !== 'function') {
      setState('error')
      setMessage('This local media POC requires a secure browser context with camera, microphone, and WebRTC support.')
      return
    }

    setState('joining')
    setMessage('Requesting camera and microphone permission…')
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true, video: true })
      localStream.current = stream
      if (localVideo.current) localVideo.current.srcObject = stream
      const connection = new RTCPeerConnection({ iceServers: [] })
      peer.current = connection
      pendingRemoteCandidates.current = []
      stream.getTracks().forEach((track) => connection.addTrack(track, stream))
      connection.ontrack = (event) => {
        if (remoteVideo.current) remoteVideo.current.srcObject = event.streams[0] ?? new MediaStream([event.track])
      }
      connection.onicecandidate = (event) => {
        if (event.candidate) void writeSignal('candidate', JSON.stringify(event.candidate.toJSON())).catch(fail)
      }
      connection.onconnectionstatechange = () => {
        if (connection.connectionState === 'connected') {
          setState('connected')
          setMessage('Local peer-to-peer media connected. No media is sent to AvenChart or a vendor.')
          connectionStateChange.current?.(true)
        } else if (connection.connectionState === 'failed') {
          fail(new Error('The local peer-to-peer media connection failed. End and reconnect both participants.'))
        } else if (connection.connectionState === 'closed') {
          connectionStateChange.current?.(false)
          setState('ended')
        }
      }
      if (role === 'physician') {
        const offer = await connection.createOffer()
        await connection.setLocalDescription(offer)
        await writeSignal('offer', JSON.stringify(offer))
        setMessage('Local offer sent. Waiting for the patient browser to join.')
      } else {
        setMessage('Waiting for the physician browser to begin the local connection.')
      }
      setState('waiting')
    } catch (error) {
      fail(error)
    }
  }

  useEffect(() => {
    if (state !== 'waiting') return
    const controller = new AbortController()
    let timer: number | undefined
    const poll = async () => {
      if (document.visibilityState === 'hidden' || polling.current || !peer.current) return
      polling.current = true
      try {
        const result = await readSignals(sequence.current, controller.signal)
        sequence.current = Math.max(sequence.current, result.latestSequence)
        for (const signal of result.signals) await receive(signal.kind, signal.payload)
      } catch (error) {
        if (!controller.signal.aborted) fail(error)
      } finally {
        polling.current = false
      }
    }
    const schedule = () => {
      window.clearTimeout(timer)
      if (document.visibilityState !== 'hidden') {
        void poll()
        timer = window.setTimeout(schedule, 900)
      }
    }
    const visible = () => schedule()
    document.addEventListener('visibilitychange', visible)
    schedule()
    return () => {
      controller.abort()
      window.clearTimeout(timer)
      document.removeEventListener('visibilitychange', visible)
    }
  }, [fail, readSignals, receive, state])

  return (
    <section className="telehealth-local-webrtc-poc" aria-labelledby={`local-webrtc-${grant.grantId}`}>
      <h4 id={`local-webrtc-${grant.grantId}`}>Local browser media POC</h4>
      <p role="note">NON_PRODUCTION local-only demonstration. Camera and microphone stay in the browsers and are peer-to-peer only. No recording, transcription, media storage, TURN service, vendor service, patient delivery, clinical completion, or emergency support is provided.</p>
      <div className="telehealth-local-webrtc-videos">
        <figure><video ref={localVideo} muted autoPlay playsInline aria-label="Local camera preview" /><figcaption>Your camera preview</figcaption></figure>
        <figure><video ref={remoteVideo} autoPlay playsInline aria-label="Other participant video" /><figcaption>Other participant</figcaption></figure>
      </div>
      <p role="status">{message}</p>
      <div className="telehealth-actions">
        <button className="telehealth-button" type="button" disabled={state === 'joining' || state === 'waiting' || state === 'connected'} onClick={() => void join()}>{role === 'physician' ? 'Start local media POC' : 'Join local media POC'}</button>
        <button className="telehealth-button telehealth-button-secondary" type="button" disabled={state === 'idle' || state === 'ended'} onClick={() => { stop(); setState('ended'); setMessage('Local camera, microphone, and peer connection ended.') }}>End local media POC</button>
      </div>
    </section>
  )
}
