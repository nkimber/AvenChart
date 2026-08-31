// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { CallClient, LocalVideoStream, VideoStreamRenderer } from '@azure/communication-calling'
import type { Call, CallAgent, RemoteParticipant, RemoteVideoStream, VideoStreamRendererView } from '@azure/communication-calling'
import { AzureCommunicationTokenCredential } from '@azure/communication-common'
import { useCallback, useEffect, useRef, useState } from 'react'
import type { TelehealthConnectionGrant, TelehealthInternetCallingConfiguration } from './api.ts'

type Props = {
  grant: TelehealthConnectionGrant
  role: 'patient' | 'physician'
  getCallingConfiguration: () => Promise<TelehealthInternetCallingConfiguration>
  onConnectionStateChange?: (connected: boolean) => void
}

type LocalDevice = {
  deviceId: string
  label: string
}

function deviceLabel(device: MediaDeviceInfo, index: number, kind: 'camera' | 'microphone' | 'speaker') {
  return device.label || `${kind[0].toUpperCase()}${kind.slice(1)} ${index + 1}`
}

function retainDeviceSelection(current: string, devices: LocalDevice[]) {
  return current && devices.some((device) => device.deviceId === current) ? current : ''
}

/**
 * Deliberately separate from the local WebRTC panel. The internet POC hands
 * browser media to Azure Communication Services, while the local POC retains
 * its process-local SDP/ICE relay for localhost-only development.
 */
export default function TelehealthInternetCallingPocPanel({ grant, role, getCallingConfiguration, onConnectionStateChange }: Props) {
  const localVideoContainer = useRef<HTMLDivElement>(null)
  const remoteVideoContainer = useRef<HTMLDivElement>(null)
  const callRef = useRef<Call | null>(null)
  const callAgentRef = useRef<CallAgent | null>(null)
  const localRendererRef = useRef<VideoStreamRenderer | null>(null)
  const remoteRendererRef = useRef<VideoStreamRenderer | null>(null)
  const localViewRef = useRef<VideoStreamRendererView | null>(null)
  const remoteViewRef = useRef<VideoStreamRendererView | null>(null)
  const localStreamRef = useRef<LocalVideoStream | null>(null)
  const connectionStateChange = useRef(onConnectionStateChange)
  const [state, setState] = useState<'idle' | 'joining' | 'waiting' | 'connected' | 'ended' | 'error'>('idle')
  const [message, setMessage] = useState('Join only when both participants are in the synthetic waiting room.')
  const [cameras, setCameras] = useState<LocalDevice[]>([])
  const [microphones, setMicrophones] = useState<LocalDevice[]>([])
  const [speakers, setSpeakers] = useState<LocalDevice[]>([])
  const [selectedCamera, setSelectedCamera] = useState('')
  const [selectedMicrophone, setSelectedMicrophone] = useState('')
  const [selectedSpeaker, setSelectedSpeaker] = useState('')
  const [deviceStatus, setDeviceStatus] = useState('Loading the cameras, microphones, and speakers available to this browser…')
  const [loadingDevices, setLoadingDevices] = useState(true)

  useEffect(() => {
    connectionStateChange.current = onConnectionStateChange
  }, [onConnectionStateChange])

  const clearView = useCallback((container: HTMLDivElement | null, view: VideoStreamRendererView | null, renderer: VideoStreamRenderer | null) => {
    view?.dispose()
    renderer?.dispose()
    container?.replaceChildren()
  }, [])

  const stop = useCallback(async () => {
    connectionStateChange.current?.(false)
    const call = callRef.current
    callRef.current = null
    if (call && call.state !== 'Disconnected') {
      try {
        await call.hangUp({ forEveryone: false })
      } catch {
        // The user-facing state is still ended when a stale ACS call cannot be hung up.
      }
    }
    callAgentRef.current?.dispose()
    callAgentRef.current = null
    clearView(localVideoContainer.current, localViewRef.current, localRendererRef.current)
    clearView(remoteVideoContainer.current, remoteViewRef.current, remoteRendererRef.current)
    localViewRef.current = null
    remoteViewRef.current = null
    localRendererRef.current = null
    remoteRendererRef.current = null
    localStreamRef.current = null
  }, [clearView])

  useEffect(() => () => {
    void stop()
  }, [stop])

  const fail = useCallback((error: unknown) => {
    connectionStateChange.current?.(false)
    setState('error')
    setMessage(error instanceof Error ? error.message : 'The synthetic internet call could not connect. End and retry both participants.')
  }, [])

  const refreshDevices = useCallback(async () => {
    if (!navigator.mediaDevices?.enumerateDevices) {
      setLoadingDevices(false)
      setDeviceStatus('This browser does not expose local device selection. Use its camera and microphone settings before joining.')
      return
    }

    setLoadingDevices(true)
    try {
      const devices = await navigator.mediaDevices.enumerateDevices()
      const availableCameras = devices.filter((device) => device.kind === 'videoinput').map((device, index) => ({ deviceId: device.deviceId, label: deviceLabel(device, index, 'camera') }))
      const availableMicrophones = devices.filter((device) => device.kind === 'audioinput').map((device, index) => ({ deviceId: device.deviceId, label: deviceLabel(device, index, 'microphone') }))
      const availableSpeakers = devices.filter((device) => device.kind === 'audiooutput').map((device, index) => ({ deviceId: device.deviceId, label: deviceLabel(device, index, 'speaker') }))
      setCameras(availableCameras)
      setMicrophones(availableMicrophones)
      setSpeakers(availableSpeakers)
      setSelectedCamera((current) => retainDeviceSelection(current, availableCameras))
      setSelectedMicrophone((current) => retainDeviceSelection(current, availableMicrophones))
      setSelectedSpeaker((current) => retainDeviceSelection(current, availableSpeakers))
      setDeviceStatus('Choose the local camera, microphone, and speaker before joining. Browser permission is requested when you join.')
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

  const renderRemoteStream = useCallback(async (stream: RemoteVideoStream) => {
    if (!stream.isAvailable || remoteRendererRef.current) return
    const renderer = new VideoStreamRenderer(stream)
    const view = await renderer.createView()
    clearView(remoteVideoContainer.current, remoteViewRef.current, remoteRendererRef.current)
    remoteRendererRef.current = renderer
    remoteViewRef.current = view
    remoteVideoContainer.current?.append(view.target)
  }, [clearView])

  async function join() {
    if (state === 'joining' || callRef.current) return
    if (!window.isSecureContext) {
      setState('error')
      setMessage('This synthetic internet calling POC requires HTTPS and a browser with camera and microphone support.')
      return
    }

    setState('joining')
    setMessage('Requesting camera and microphone permission…')
    try {
      const callClient = new CallClient()
      const deviceManager = await callClient.getDeviceManager()
      const permission = await deviceManager.askDevicePermission({ audio: true, video: true })
      if (!permission.audio || !permission.video) {
        throw new Error('Camera and microphone access is required for this synthetic video-call demonstration.')
      }

      const availableCameras = await deviceManager.getCameras()
      const availableMicrophones = await deviceManager.getMicrophones()
      const availableSpeakers = await deviceManager.getSpeakers()
      const camera = availableCameras.find((device) => device.id === selectedCamera) ?? availableCameras[0]
      const microphone = availableMicrophones.find((device) => device.id === selectedMicrophone) ?? availableMicrophones[0]
      const speaker = availableSpeakers.find((device) => device.id === selectedSpeaker) ?? availableSpeakers[0]
      if (!camera || !microphone) throw new Error('A camera and microphone are required for this synthetic video-call demonstration.')
      await deviceManager.selectMicrophone(microphone)
      if (speaker) await deviceManager.selectSpeaker(speaker)

      setMessage('Getting an authorized calling credential…')
      const configuration = await getCallingConfiguration()
      const credential = new AzureCommunicationTokenCredential(configuration.accessToken)
      const callAgent = await callClient.createCallAgent(credential, { displayName: role === 'physician' ? 'Synthetic physician' : 'Synthetic patient' })
      callAgentRef.current = callAgent
      const localStream = new LocalVideoStream(camera)
      localStreamRef.current = localStream
      const localRenderer = new VideoStreamRenderer(localStream)
      const localView = await localRenderer.createView()
      clearView(localVideoContainer.current, localViewRef.current, localRendererRef.current)
      localRendererRef.current = localRenderer
      localViewRef.current = localView
      localVideoContainer.current?.append(localView.target)

      const call = callAgent.join(
        { groupId: configuration.groupId },
        { audioOptions: { muted: false }, videoOptions: { localVideoStreams: [localStream] } },
      )
      callRef.current = call
      call.on('stateChanged', () => {
        if (call.state === 'Connected') {
          setState('connected')
          setMessage('Synthetic browser call connected through Azure Communication Services. AvenChart does not receive, record, transcribe, or store media.')
          connectionStateChange.current?.(true)
        } else if (call.state === 'Disconnected') {
          connectionStateChange.current?.(false)
          setState('ended')
          setMessage('The synthetic browser call ended.')
        }
      })
      const attachRemoteStream = (stream: RemoteVideoStream) => {
        const updateView = () => {
          if (stream.isAvailable) {
            void renderRemoteStream(stream)
          } else {
            clearView(remoteVideoContainer.current, remoteViewRef.current, remoteRendererRef.current)
            remoteViewRef.current = null
            remoteRendererRef.current = null
          }
        }
        stream.on('isAvailableChanged', updateView)
        updateView()
      }
      const attachParticipant = (participant: RemoteParticipant) => {
        participant.videoStreams.forEach(attachRemoteStream)
        participant.on('videoStreamsUpdated', (event) => event.added.forEach(attachRemoteStream))
      }
      call.remoteParticipants.forEach(attachParticipant)
      call.on('remoteParticipantsUpdated', (event) => event.added.forEach(attachParticipant))
      setState('waiting')
      setMessage('Joining the synthetic internet call. Waiting for the other participant if they have not joined yet.')
    } catch (error) {
      await stop()
      fail(error)
    }
  }

  const mediaActive = state === 'joining' || state === 'waiting' || state === 'connected'

  return (
    <section className="telehealth-local-webrtc-poc" aria-labelledby={`internet-calling-${grant.grantId}`}>
      <h4 id={`internet-calling-${grant.grantId}`}>Synthetic internet video-call POC</h4>
      <p role="note">NON_PRODUCTION synthetic demonstration. Azure Communication Services carries browser audio and video using its calling transport; AvenChart authorizes access but does not receive, record, transcribe, or store media. Do not enter real health information.</p>
      <fieldset className="telehealth-local-device-picker" disabled={mediaActive}>
        <legend>Choose your local devices</legend>
        <p aria-live="polite">{deviceStatus}</p>
        <div className="telehealth-local-device-grid">
          <label>Camera<select aria-label="Camera for synthetic internet call" value={selectedCamera} onChange={(event) => setSelectedCamera(event.target.value)}><option value="">Browser default camera</option>{cameras.map((device) => <option key={device.deviceId} value={device.deviceId}>{device.label}</option>)}</select></label>
          <label>Microphone<select aria-label="Microphone for synthetic internet call" value={selectedMicrophone} onChange={(event) => setSelectedMicrophone(event.target.value)}><option value="">Browser default microphone</option>{microphones.map((device) => <option key={device.deviceId} value={device.deviceId}>{device.label}</option>)}</select></label>
          <label>Speaker<select aria-label="Speaker for synthetic internet call" value={selectedSpeaker} onChange={(event) => setSelectedSpeaker(event.target.value)}><option value="">Browser default speaker</option>{speakers.map((device) => <option key={device.deviceId} value={device.deviceId}>{device.label}</option>)}</select></label>
        </div>
        <button className="telehealth-button telehealth-button-secondary" type="button" onClick={() => void refreshDevices()} disabled={loadingDevices}>{loadingDevices ? 'Loading local devices…' : 'Refresh available devices'}</button>
      </fieldset>
      <div className="telehealth-local-webrtc-videos">
        <figure><div ref={localVideoContainer} aria-label="Local camera preview" /><figcaption>Your camera preview</figcaption></figure>
        <figure><div ref={remoteVideoContainer} aria-label="Other participant video" /><figcaption>Other participant</figcaption></figure>
      </div>
      <p role="status">{message}</p>
      <div className="telehealth-actions">
        <button className="telehealth-button" type="button" disabled={mediaActive} onClick={() => void join()}>{role === 'physician' ? 'Start synthetic internet call' : 'Join synthetic internet call'}</button>
        <button className="telehealth-button telehealth-button-secondary" type="button" disabled={state === 'idle' || state === 'ended'} onClick={() => void stop().then(() => { setState('ended'); setMessage('Local camera, microphone, and synthetic internet call ended.') })}>End synthetic internet call</button>
      </div>
    </section>
  )
}
