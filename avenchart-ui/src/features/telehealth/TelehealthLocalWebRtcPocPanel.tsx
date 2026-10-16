// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useEffect, useRef, useState } from 'react'
import type { TelehealthConnectionGrant, TelehealthLocalWebRtcSignalKind, TelehealthLocalWebRtcSignalRead } from './api.ts'
import { ApiRequestError } from '../../api/transport.ts'

type Props = {
  grant: TelehealthConnectionGrant
  role: 'patient' | 'physician'
  writeSignal: (kind: TelehealthLocalWebRtcSignalKind, payload: string) => Promise<unknown>
  readSignals: (afterSequence: number, signal?: AbortSignal) => Promise<TelehealthLocalWebRtcSignalRead>
  onConnectionStateChange?: (connected: boolean) => void
}

type LocalDevice = {
  deviceId: string
  label: string
}

type SinkSelectableVideo = HTMLVideoElement & {
  setSinkId?: (sinkId: string) => Promise<void>
}

function deviceLabel(device: MediaDeviceInfo, index: number, kind: 'camera' | 'microphone' | 'speaker') {
  return device.label || `${kind[0].toUpperCase()}${kind.slice(1)} ${index + 1}`
}

function retainDeviceSelection(current: string, devices: LocalDevice[]) {
  return current && devices.some((device) => device.deviceId === current) ? current : ''
}

function deviceConstraint(deviceId: string): boolean | MediaTrackConstraints {
  return deviceId ? { deviceId: { exact: deviceId } } : true
}

export default function TelehealthLocalWebRtcPocPanel({ grant, role, writeSignal, readSignals, onConnectionStateChange }: Props) {
  const localVideo = useRef<HTMLVideoElement>(null)
  const remoteVideo = useRef<HTMLVideoElement>(null)
  const peer = useRef<RTCPeerConnection | null>(null)
  const localStream = useRef<MediaStream | null>(null)
  const pendingRemoteCandidates = useRef<Array<RTCIceCandidateInit & { negotiationId?: string }>>([])
  const sequence = useRef(0)
  const polling = useRef(false)
  const negotiation = useRef<string | null>(null)
  const requestedOffer = useRef<string | null>(null)
  const operation = useRef(0)
  const authorizationChangedAt = useRef<number | null>(null)
  const connectionStateChange = useRef(onConnectionStateChange)
  const selectedSpeaker = useRef('')
  const [state, setState] = useState<'idle' | 'joining' | 'waiting' | 'connected' | 'ended' | 'error'>('idle')
  const [message, setMessage] = useState('Join only when both participants are in the synthetic waiting room.')
  const [cameras, setCameras] = useState<LocalDevice[]>([])
  const [microphones, setMicrophones] = useState<LocalDevice[]>([])
  const [speakers, setSpeakers] = useState<LocalDevice[]>([])
  const [selectedCamera, setSelectedCamera] = useState('')
  const [selectedMicrophone, setSelectedMicrophone] = useState('')
  const [selectedSpeakerId, setSelectedSpeakerId] = useState('')
  const [deviceStatus, setDeviceStatus] = useState('Loading the cameras, microphones, and speakers available to this browser…')
  const [loadingDevices, setLoadingDevices] = useState(true)
  const [sameLaptop, setSameLaptop] = useState(false)
  const speakerSelectionSupported = typeof HTMLMediaElement !== 'undefined' && 'setSinkId' in HTMLMediaElement.prototype

  useEffect(() => {
    connectionStateChange.current = onConnectionStateChange
  }, [onConnectionStateChange])

  useEffect(() => {
    selectedSpeaker.current = selectedSpeakerId
  }, [selectedSpeakerId])

  useEffect(() => () => {
    operation.current++
    connectionStateChange.current?.(false)
    peer.current?.close()
    localStream.current?.getTracks().forEach((track) => track.stop())
  }, [])

  const stop = useCallback(() => {
    operation.current++
    connectionStateChange.current?.(false)
    peer.current?.close()
    peer.current = null
    pendingRemoteCandidates.current = []
    sequence.current = 0
    negotiation.current = null
    requestedOffer.current = null
    localStream.current?.getTracks().forEach((track) => track.stop())
    localStream.current = null
    if (localVideo.current) localVideo.current.srcObject = null
    if (remoteVideo.current) remoteVideo.current.srcObject = null
  }, [])

  const fail = useCallback((error: unknown) => {
    connectionStateChange.current?.(false)
    setState('error')
    setMessage(error instanceof Error ? error.message : 'The browser media POC could not connect. End and retry both participants.')
  }, [])

  const applySpeaker = useCallback(async (video: HTMLVideoElement | null, deviceId: string) => {
    if (!video || !speakerSelectionSupported) return
    const selectableVideo = video as SinkSelectableVideo
    if (!selectableVideo.setSinkId) return
    try {
      await selectableVideo.setSinkId(deviceId || 'default')
    } catch {
      setDeviceStatus('The browser could not switch the speaker. Keep the browser default speaker or check its site permissions.')
    }
  }, [speakerSelectionSupported])

  const refreshDevices = useCallback(async () => {
    if (!navigator.mediaDevices?.enumerateDevices) {
      setLoadingDevices(false)
      setDeviceStatus('This browser does not expose local device selection. Use its camera and microphone settings before joining.')
      return
    }

    setLoadingDevices(true)
    try {
      const devices = await navigator.mediaDevices.enumerateDevices()
      const availableCameras = devices
        .filter((device) => device.kind === 'videoinput')
        .map((device, index) => ({ deviceId: device.deviceId, label: deviceLabel(device, index, 'camera') }))
      const availableMicrophones = devices
        .filter((device) => device.kind === 'audioinput')
        .map((device, index) => ({ deviceId: device.deviceId, label: deviceLabel(device, index, 'microphone') }))
      const availableSpeakers = devices
        .filter((device) => device.kind === 'audiooutput')
        .map((device, index) => ({ deviceId: device.deviceId, label: deviceLabel(device, index, 'speaker') }))

      setCameras(availableCameras)
      setMicrophones(availableMicrophones)
      setSpeakers(availableSpeakers)
      setSelectedCamera((current) => retainDeviceSelection(current, availableCameras))
      setSelectedMicrophone((current) => retainDeviceSelection(current, availableMicrophones))
      setSelectedSpeakerId((current) => retainDeviceSelection(current, availableSpeakers))
      setDeviceStatus('Choose the local camera, microphone, and speaker to use when the media POC starts.')
    } catch {
      setDeviceStatus('Available local devices could not be read. Confirm browser camera and microphone permission, then refresh this list.')
    } finally {
      setLoadingDevices(false)
    }
  }, [])

  useEffect(() => {
    void refreshDevices()
    const mediaDevices = navigator.mediaDevices
    if (!mediaDevices?.addEventListener) return
    const handleDeviceChange = () => void refreshDevices()
    mediaDevices.addEventListener('devicechange', handleDeviceChange)
    return () => mediaDevices.removeEventListener('devicechange', handleDeviceChange)
  }, [refreshDevices])

  const receive = useCallback(async (kind: TelehealthLocalWebRtcSignalKind, payload: string) => {
    const connection = peer.current
    if (!connection) return
    try {
      const data = JSON.parse(payload) as (RTCSessionDescriptionInit & RTCIceCandidateInit & { negotiationId?: string; requestId?: string }) | null
      if (kind === 'request-offer' && role === 'physician') {
        negotiation.current = crypto.randomUUID()
        const offer = await connection.createOffer({ iceRestart: true })
        await connection.setLocalDescription(offer)
        await writeSignal('offer', JSON.stringify({ ...offer, negotiationId: negotiation.current, requestId: data?.requestId }))
      } else if (kind === 'offer' && role === 'patient' && data) {
        if (requestedOffer.current && data.requestId !== requestedOffer.current) return
        negotiation.current = data.negotiationId ?? null
        requestedOffer.current = null
        pendingRemoteCandidates.current = pendingRemoteCandidates.current.filter(candidate => candidate.negotiationId === data.negotiationId)
        const offer = { type: data.type, sdp: data.sdp }
        await connection.setRemoteDescription(offer)
        await flushRemoteCandidates(connection)
        const answer = await connection.createAnswer()
        await connection.setLocalDescription(answer)
        await writeSignal('answer', JSON.stringify({ ...answer, negotiationId: negotiation.current }))
      } else if (kind === 'answer' && data && data.negotiationId === negotiation.current && connection.signalingState === 'have-local-offer') {
        await connection.setRemoteDescription({ type: data.type, sdp: data.sdp })
        await flushRemoteCandidates(connection)
      } else if (kind === 'candidate' && data) {
        const candidate = data
        if (connection.remoteDescription && data.negotiationId === negotiation.current) {
          await connection.addIceCandidate(candidate)
        } else if (pendingRemoteCandidates.current.length < 100) {
          pendingRemoteCandidates.current.push(candidate)
        }
      }
    } catch (error) {
      fail(error)
    }
  }, [fail, role, writeSignal])

  async function flushRemoteCandidates(connection: RTCPeerConnection) {
    const candidates = pendingRemoteCandidates.current
    pendingRemoteCandidates.current = []
    for (const candidate of candidates) if (candidate.negotiationId === negotiation.current) await connection.addIceCandidate(candidate)
  }

  async function join() {
    if (state === 'joining' || peer.current) return
    if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia || typeof RTCPeerConnection !== 'function') {
      setState('error')
      setMessage('This synthetic browser media POC requires a secure browser context with camera, microphone, and WebRTC support.')
      return
    }

    setState('joining')
    const current = ++operation.current
    try {
      setMessage('Requesting camera and microphone permission…')
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: deviceConstraint(selectedMicrophone),
        video: deviceConstraint(selectedCamera),
      })
      if (current !== operation.current) { stream.getTracks().forEach(track => track.stop()); return }
      localStream.current = stream
      if (sameLaptop) stream.getAudioTracks().forEach(track => { track.enabled = false })
      if (remoteVideo.current) remoteVideo.current.muted = sameLaptop
      if (localVideo.current) localVideo.current.srcObject = stream
      const connection = new RTCPeerConnection()
      peer.current = connection
      pendingRemoteCandidates.current = []
      stream.getTracks().forEach((track) => connection.addTrack(track, stream))
      connection.ontrack = (event) => {
        if (remoteVideo.current) {
          remoteVideo.current.srcObject = event.streams[0] ?? new MediaStream([event.track])
          void applySpeaker(remoteVideo.current, selectedSpeaker.current)
        }
      }
      connection.onicecandidate = (event) => {
        if (event.candidate) void writeSignal('candidate', JSON.stringify({ ...event.candidate.toJSON(), negotiationId: negotiation.current })).catch(fail)
      }
      connection.onconnectionstatechange = () => {
        if (connection.connectionState === 'connected') {
          setState('connected')
          setMessage('Local browser-to-browser media connected. AvenChart does not receive or store media.')
          connectionStateChange.current?.(true)
        } else if (connection.connectionState === 'failed') {
          connectionStateChange.current?.(false)
          fail(new Error('The browser media connection failed. End and reconnect both participants.'))
        } else if (connection.connectionState === 'disconnected') {
          connectionStateChange.current?.(false)
          setMessage('The connection was interrupted. Reconnect if it does not recover.')
        } else if (connection.connectionState === 'closed') {
          connectionStateChange.current?.(false)
          setState('ended')
        }
      }
      if (role === 'physician') {
        negotiation.current = crypto.randomUUID()
        const offer = await connection.createOffer()
        await connection.setLocalDescription(offer)
        await writeSignal('offer', JSON.stringify({ ...offer, negotiationId: negotiation.current }))
        setMessage('Browser offer sent. Waiting for the patient browser to join.')
      } else {
        requestedOffer.current = crypto.randomUUID()
        await writeSignal('request-offer', JSON.stringify({ requestId: requestedOffer.current }))
        setMessage('Waiting for the physician browser to begin the connection.')
      }
      setState('waiting')
    } catch (error) {
      fail(error)
    }
  }

  const mediaActive = state === 'joining' || state === 'waiting' || state === 'connected'

  async function changeSpeaker(deviceId: string) {
    selectedSpeaker.current = deviceId
    setSelectedSpeakerId(deviceId)
    await applySpeaker(remoteVideo.current, deviceId)
  }

  useEffect(() => {
    if (state !== 'waiting' && state !== 'connected') return
    const controller = new AbortController()
    let timer: number | undefined
    const poll = async () => {
      if (document.visibilityState === 'hidden' || polling.current || !peer.current) return
      polling.current = true
      try {
        const result = await readSignals(sequence.current, controller.signal)
        authorizationChangedAt.current = null
        // A local API restart resets only the transient relay. Permit the
        // next poll to read its new sequence without recreating the visit.
        sequence.current = result.latestSequence
        for (const signal of result.signals) await receive(signal.kind, signal.payload)
      } catch (error) {
        if (!controller.signal.aborted) {
          // Consultation start revokes the waiting-room grant before the
          // independent patient status poll can learn the new lifecycle.
          // Give that reconciliation a bounded window; never revive a grant.
          if (error instanceof ApiRequestError && error.status === 404) {
            authorizationChangedAt.current ??= Date.now()
            if (Date.now() - authorizationChangedAt.current < 15_000) return
          }
          stop()
          fail(error)
        }
      } finally {
        polling.current = false
      }
    }
    const schedule = () => {
      window.clearTimeout(timer)
      if (document.visibilityState !== 'hidden') {
        void poll()
        timer = window.setTimeout(schedule, 2500)
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
  }, [fail, readSignals, receive, state, stop])

  return (
    <section className="telehealth-local-webrtc-poc" aria-labelledby={`local-webrtc-${grant.grantId}`}>
      <h4 id={`local-webrtc-${grant.grantId}`}>Synthetic browser media POC</h4>
      <p role="note">NON_PRODUCTION local-only synthetic demonstration. Browser media is protected by WebRTC and does not enter AvenChart. No external media service, recording, transcription, media storage, patient delivery, clinical completion, or emergency support is provided.</p>
      <fieldset className="telehealth-local-device-picker" disabled={mediaActive}>
        <legend>Choose your local devices</legend>
        <label className="telehealth-check"><input type="checkbox" checked={sameLaptop} onChange={event => setSameLaptop(event.target.checked)} />Same-laptop demo: silence this tab’s microphone and incoming audio to prevent echo</label>
        <p aria-live="polite">{deviceStatus}</p>
        <div className="telehealth-local-device-grid">
          <label>
            Camera
            <select aria-label="Camera for browser media POC" value={selectedCamera} onChange={(event) => setSelectedCamera(event.target.value)}>
              <option value="">Browser default camera</option>
              {cameras.map((device) => <option key={device.deviceId} value={device.deviceId}>{device.label}</option>)}
            </select>
          </label>
          <label>
            Microphone
            <select aria-label="Microphone for browser media POC" value={selectedMicrophone} onChange={(event) => setSelectedMicrophone(event.target.value)}>
              <option value="">Browser default microphone</option>
              {microphones.map((device) => <option key={device.deviceId} value={device.deviceId}>{device.label}</option>)}
            </select>
          </label>
          <label>
            Speaker
            <select aria-label="Speaker for browser media POC" value={selectedSpeakerId} disabled={!speakerSelectionSupported} onChange={(event) => void changeSpeaker(event.target.value)}>
              <option value="">Browser default speaker</option>
              {speakers.map((device) => <option key={device.deviceId} value={device.deviceId}>{device.label}</option>)}
            </select>
          </label>
        </div>
        {!speakerSelectionSupported ? <p>This browser controls speaker selection outside AvenChart. Use its audio settings before joining.</p> : null}
        <button className="telehealth-button telehealth-button-secondary" type="button" onClick={() => void refreshDevices()} disabled={loadingDevices}>{loadingDevices ? 'Loading local devices…' : 'Refresh available devices'}</button>
      </fieldset>
      <div className="telehealth-local-webrtc-videos">
        <figure><video ref={localVideo} muted autoPlay playsInline aria-label="Local camera preview" /><figcaption>Your camera preview</figcaption></figure>
        <figure><video ref={remoteVideo} autoPlay playsInline aria-label="Other participant video" /><figcaption>Other participant</figcaption></figure>
      </div>
      <p role="status">{message}</p>
      <div className="telehealth-actions">
        <button className="telehealth-button" type="button" disabled={mediaActive} onClick={() => void join()}>{role === 'physician' ? 'Start browser media POC' : 'Join browser media POC'}</button>
        <button className="telehealth-button telehealth-button-secondary" type="button" disabled={state === 'idle' || state === 'ended'} onClick={() => { stop(); setState('ended'); setMessage('Local camera, microphone, and browser connection ended.') }}>End browser media POC</button>
      </div>
    </section>
  )
}
