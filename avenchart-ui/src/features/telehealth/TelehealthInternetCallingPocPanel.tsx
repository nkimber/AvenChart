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

function selectedDevice<T extends { id: string }>(devices: T[], selection: string, kind: string) {
  if (!selection) return devices[0]
  const device = devices.find((item) => item.id === selection)
  if (!device) throw new Error(`Your selected ${kind} is unavailable. Reconnect it or select another ${kind}, then join again.`)
  return device
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
  const clientRef = useRef<CallClient | null>(null)
  const credentialRef = useRef<AzureCommunicationTokenCredential | null>(null)
  const operation = useRef(0)
  const joining = useRef(false)
  const stopping = useRef(false)
  const subscriptions = useRef<Array<() => void>>([])
  const callAgentRef = useRef<CallAgent | null>(null)
  const localRendererRef = useRef<VideoStreamRenderer | null>(null)
  const remoteRendererRef = useRef<VideoStreamRenderer | null>(null)
  const localViewRef = useRef<VideoStreamRendererView | null>(null)
  const remoteViewRef = useRef<VideoStreamRendererView | null>(null)
  const localStreamRef = useRef<LocalVideoStream | null>(null)
  const connectionStateChange = useRef(onConnectionStateChange)
  const [state, setState] = useState<'idle' | 'joining' | 'waiting' | 'connected' | 'ended' | 'error'>('idle')
  const [message, setMessage] = useState('Choose your camera, then join. Either participant can join first.')
  const [cameras, setCameras] = useState<LocalDevice[]>([])
  const [microphones, setMicrophones] = useState<LocalDevice[]>([])
  const [speakers, setSpeakers] = useState<LocalDevice[]>([])
  const [selectedCamera, setSelectedCamera] = useState('')
  const [selectedMicrophone, setSelectedMicrophone] = useState('')
  const [selectedSpeaker, setSelectedSpeaker] = useState('')
  const [deviceStatus, setDeviceStatus] = useState('Loading the cameras, microphones, and speakers available to this browser…')
  const [loadingDevices, setLoadingDevices] = useState(true)
  const [speakerSupported, setSpeakerSupported] = useState(false)
  const [muted, setMuted] = useState(false)
  const [cameraOn, setCameraOn] = useState(true)
  const [sameLaptop, setSameLaptop] = useState(false)
  const [controlBusy, setControlBusy] = useState(false)
  const [activeCamera, setActiveCamera] = useState('')
  const [serviceConnected, setServiceConnected] = useState(false)

  const getDeviceManager = useCallback(async () => {
    clientRef.current ??= new CallClient()
    return clientRef.current.getDeviceManager()
  }, [])

  useEffect(() => {
    connectionStateChange.current = onConnectionStateChange
  }, [onConnectionStateChange])

  const clearView = useCallback((container: HTMLDivElement | null, view: VideoStreamRendererView | null, renderer: VideoStreamRenderer | null) => {
    view?.dispose()
    renderer?.dispose()
    container?.replaceChildren()
  }, [])

  const stop = useCallback(async () => {
    stopping.current = true
    operation.current++
    setServiceConnected(false)
    connectionStateChange.current?.(false)
    subscriptions.current.splice(0).forEach((unsubscribe) => unsubscribe())
    const call = callRef.current
    const agent = callAgentRef.current
    const credential = credentialRef.current
    callRef.current = null
    callAgentRef.current = null
    credentialRef.current = null
    clearView(localVideoContainer.current, localViewRef.current, localRendererRef.current)
    clearView(remoteVideoContainer.current, remoteViewRef.current, remoteRendererRef.current)
    localViewRef.current = null
    remoteViewRef.current = null
    localRendererRef.current = null
    remoteRendererRef.current = null
    localStreamRef.current?.dispose()
    localStreamRef.current = null
    if (call && call.state !== 'Disconnected') {
      try {
        await call.hangUp({ forEveryone: false })
      } catch {
        // The user-facing state is still ended when a stale ACS call cannot be hung up.
      }
    }
    await agent?.dispose().catch(() => undefined)
    credential?.dispose()
    stopping.current = false
  }, [clearView])

  useEffect(() => () => {
    const client = clientRef.current
    clientRef.current = null
    void stop().then(() => client?.dispose()).catch(() => undefined)
  }, [stop])

  const fail = useCallback((error: unknown) => {
    connectionStateChange.current?.(false)
    setState('error')
    setMessage(error instanceof Error ? error.message : 'The synthetic internet call could not connect. End and retry both participants.')
  }, [])

  const refreshDevices = useCallback(async () => {
    setLoadingDevices(true)
    try {
      // Use the same SDK device namespace for display and capture. Browser
      // MediaDeviceInfo.deviceId is not interchangeable with an ACS device ID.
      const manager = await getDeviceManager()
      const [video, audio, output] = await Promise.all([manager.getCameras(), manager.getMicrophones(), manager.getSpeakers()])
      const options = (devices: { id: string; name: string }[]) => devices.map((device) => ({ deviceId: device.id, label: device.name }))
      setCameras(options(video)); setMicrophones(options(audio)); setSpeakers(options(output))
      setSpeakerSupported(manager.isSpeakerSelectionAvailable)
      setDeviceStatus('Choose your camera and audio. These choices apply only to this tab.')
    } catch {
      setDeviceStatus('Available local devices could not be read. Confirm browser camera and microphone permission, then refresh this list.')
    } finally {
      setLoadingDevices(false)
    }
  }, [getDeviceManager])

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
    const current = operation.current
    const renderer = new VideoStreamRenderer(stream)
    remoteRendererRef.current = renderer
    const view = await renderer.createView()
    if (current !== operation.current || remoteRendererRef.current !== renderer || !stream.isAvailable) { view.dispose(); return }
    remoteViewRef.current = view
    remoteVideoContainer.current?.append(view.target)
  }, [])

  async function join() {
    if (joining.current || stopping.current || callRef.current) return
    if (!window.isSecureContext) {
      setState('error')
      setMessage('This synthetic internet calling POC requires HTTPS and a browser with camera and microphone support.')
      return
    }

    joining.current = true
    const current = ++operation.current
    const isCurrent = () => operation.current === current
    setState('joining')
    setMessage('Requesting camera and microphone permission…')
    try {
      const deviceManager = await getDeviceManager()
      if (!isCurrent()) return
      const permission = await deviceManager.askDevicePermission({ audio: true, video: true })
      if (!isCurrent()) return
      if (!permission.audio || !permission.video) {
        throw new Error('Camera and microphone access is required for this synthetic video-call demonstration.')
      }

      const availableCameras = await deviceManager.getCameras()
      const availableMicrophones = await deviceManager.getMicrophones()
      const availableSpeakers = await deviceManager.getSpeakers()
      if (!isCurrent()) return
      const camera = selectedDevice(availableCameras, selectedCamera, 'camera')
      const microphone = selectedDevice(availableMicrophones, selectedMicrophone, 'microphone')
      const speaker = deviceManager.isSpeakerSelectionAvailable ? selectedDevice(availableSpeakers, selectedSpeaker, 'speaker') : undefined
      if (!camera || !microphone) throw new Error('A camera and microphone are required for this synthetic video-call demonstration.')
      await deviceManager.selectMicrophone(microphone)
      if (speaker) await deviceManager.selectSpeaker(speaker)
      if (!isCurrent()) return

      setMessage('Getting an authorized calling credential…')
      const configuration = await getCallingConfiguration()
      if (!isCurrent()) return
      const credential = new AzureCommunicationTokenCredential(configuration.accessToken)
      credentialRef.current = credential
      const callAgent = await clientRef.current!.createCallAgent(credential, { displayName: role === 'physician' ? 'Demo physician' : 'Demo patient' })
      if (!isCurrent()) { await callAgent.dispose(); return }
      callAgentRef.current = callAgent
      const localStream = new LocalVideoStream(camera)
      localStreamRef.current = localStream
      const localRenderer = new VideoStreamRenderer(localStream)
      localRendererRef.current = localRenderer
      const localView = await localRenderer.createView()
      if (!isCurrent()) { localView.dispose(); return }
      localRendererRef.current = localRenderer
      localViewRef.current = localView
      localVideoContainer.current?.append(localView.target)
      setActiveCamera(camera.name)
      setCameraOn(true)
      setMuted(sameLaptop)

      const call = callAgent.join(
        { groupId: configuration.groupId },
        { audioOptions: { muted: sameLaptop }, videoOptions: { localVideoStreams: [localStream] } },
      )
      callRef.current = call
      const syncState = () => {
        if (!isCurrent()) return
        const bothConnected = call.state === 'Connected' && call.remoteParticipants.some((participant) => participant.state === 'Connected')
        setServiceConnected(call.state === 'Connected')
        connectionStateChange.current?.(bothConnected)
        if (call.state === 'Connected') {
          setState(bothConnected ? 'connected' : 'waiting')
          setMessage(bothConnected ? 'Patient and physician are connected.' : 'You are in the room. Waiting for the other participant to join…')
        } else if (call.state === 'Disconnected') {
          setState('ended')
          setMessage('The call ended. You can join again while this visit is active.')
          void stop()
        } else {
          setState('waiting')
          setMessage('Connecting to the video service…')
        }
      }
      call.on('stateChanged', syncState)
      subscriptions.current.push(() => call.off('stateChanged', syncState))
      const attachRemoteStream = (stream: RemoteVideoStream) => {
        const updateView = () => {
          if (stream.isAvailable) {
            void renderRemoteStream(stream).catch(() => { if (isCurrent()) setMessage('The remote camera could not be displayed. Ask the other participant to turn their camera off and on.') })
          } else {
            clearView(remoteVideoContainer.current, remoteViewRef.current, remoteRendererRef.current)
            remoteViewRef.current = null
            remoteRendererRef.current = null
          }
        }
        stream.on('isAvailableChanged', updateView)
        subscriptions.current.push(() => stream.off('isAvailableChanged', updateView))
        updateView()
      }
      const attachParticipant = (participant: RemoteParticipant) => {
        participant.videoStreams.forEach(attachRemoteStream)
        const videosUpdated = (event: { added: RemoteVideoStream[]; removed: RemoteVideoStream[] }) => {
          if (event.removed.length) {
            clearView(remoteVideoContainer.current, remoteViewRef.current, remoteRendererRef.current)
            remoteViewRef.current = null; remoteRendererRef.current = null
          }
          event.added.forEach(attachRemoteStream)
        }
        participant.on('videoStreamsUpdated', videosUpdated)
        participant.on('stateChanged', syncState)
        subscriptions.current.push(() => { participant.off('videoStreamsUpdated', videosUpdated); participant.off('stateChanged', syncState) })
      }
      call.remoteParticipants.forEach(attachParticipant)
      const participantsUpdated = (event: { added: RemoteParticipant[]; removed: RemoteParticipant[] }) => {
        if (!isCurrent()) return
        if (event.removed.length) {
          clearView(remoteVideoContainer.current, remoteViewRef.current, remoteRendererRef.current)
          remoteViewRef.current = null; remoteRendererRef.current = null
        }
        event.added.forEach(attachParticipant)
        syncState()
      }
      call.on('remoteParticipantsUpdated', participantsUpdated)
      subscriptions.current.push(() => call.off('remoteParticipantsUpdated', participantsUpdated))
      if (sameLaptop) await call.muteIncomingAudio()
      syncState()
    } catch (error) {
      if (!isCurrent()) return
      await stop()
      fail(error)
    } finally { joining.current = false }
  }

  async function changeMedia(kind: 'microphone' | 'camera') {
    const activeCall = callRef.current
    const stream = localStreamRef.current
    if (!activeCall || !stream || controlBusy) return
    setControlBusy(true)
    try {
      if (kind === 'microphone') {
        if (muted) await activeCall.unmute(); else await activeCall.mute()
        if (callRef.current === activeCall) setMuted(activeCall.isMuted)
      } else {
        if (cameraOn) await activeCall.stopVideo(stream); else await activeCall.startVideo(stream)
        if (callRef.current === activeCall) setCameraOn(!cameraOn)
      }
    } catch { setMessage('The media control could not be changed. Try again or leave and rejoin the call.') }
    finally { setControlBusy(false) }
  }

  const mediaActive = state === 'joining' || state === 'waiting' || state === 'connected'
  const controlsEnabled = (state === 'waiting' || state === 'connected') && serviceConnected

  return (
    <section className="telehealth-local-webrtc-poc" aria-labelledby={`internet-calling-${grant.grantId}`}>
      <h4 id={`internet-calling-${grant.grantId}`}>Video visit</h4>
      <p role="note">Synthetic demonstration only. Audio and video use Azure Communication Services and are not recorded by AvenChart.</p>
      <fieldset className="telehealth-local-device-picker" disabled={mediaActive}>
        <legend>Camera and audio</legend>
        <p aria-live="polite">{deviceStatus}</p>
        <div className="telehealth-local-device-grid">
          <label>Camera<select aria-label="Camera for synthetic internet call" value={selectedCamera} onChange={(event) => setSelectedCamera(event.target.value)}><option value="">Default camera</option>{selectedCamera && !cameras.some((device) => device.deviceId === selectedCamera) ? <option value={selectedCamera}>Selected camera disconnected</option> : null}{cameras.map((device) => <option key={device.deviceId} value={device.deviceId}>{device.label}</option>)}</select></label>
          <label>Microphone<select aria-label="Microphone for synthetic internet call" value={selectedMicrophone} onChange={(event) => setSelectedMicrophone(event.target.value)}><option value="">Browser default microphone</option>{microphones.map((device) => <option key={device.deviceId} value={device.deviceId}>{device.label}</option>)}</select></label>
          <label>Speaker<select aria-label="Speaker for synthetic internet call" value={selectedSpeaker} disabled={!speakerSupported} onChange={(event) => setSelectedSpeaker(event.target.value)}><option value="">Default speaker</option>{speakers.map((device) => <option key={device.deviceId} value={device.deviceId}>{device.label}</option>)}</select></label>
        </div>
        {!speakerSupported ? <p>Choose your speaker in your browser or system audio settings.</p> : null}
        <label className="telehealth-check"><input type="checkbox" checked={sameLaptop} onChange={(event) => setSameLaptop(event.target.checked)} />Same-laptop demo: silence this tab’s microphone and incoming audio to prevent echo</label>
        <button className="telehealth-button telehealth-button-secondary" type="button" onClick={() => void refreshDevices()} disabled={loadingDevices}>{loadingDevices ? 'Loading local devices…' : 'Refresh available devices'}</button>
      </fieldset>
      <div className="telehealth-local-webrtc-videos">
        <figure><div ref={localVideoContainer} className="telehealth-video-surface" aria-label="Local camera preview" hidden={!cameraOn} />{!cameraOn ? <div className="telehealth-video-placeholder">Your camera is off</div> : null}<figcaption>{activeCamera ? `Your camera: ${activeCamera}` : 'Your camera preview'}</figcaption></figure>
        <figure><div ref={remoteVideoContainer} className="telehealth-video-surface" aria-label="Other participant video" /><figcaption>{role === 'physician' ? 'Patient' : 'Physician'}</figcaption></figure>
      </div>
      <p role={state === 'error' ? 'alert' : 'status'}>{message}</p>
      {mediaActive && sameLaptop ? <p>This tab is silenced for the same-laptop demo. Use the other tab for audio.</p> : null}
      <div className="telehealth-actions">
        {!mediaActive ? <button className="telehealth-button" type="button" disabled={loadingDevices} onClick={() => void join()}>{state === 'ended' || state === 'error' ? 'Rejoin video visit' : 'Join video visit'}</button> : null}
        {mediaActive ? <>
          <button className="telehealth-button telehealth-button-secondary" type="button" disabled={!controlsEnabled || controlBusy || sameLaptop} onClick={() => void changeMedia('microphone')}>{muted ? 'Unmute microphone' : 'Mute microphone'}</button>
          <button className="telehealth-button telehealth-button-secondary" type="button" disabled={!controlsEnabled || controlBusy} onClick={() => void changeMedia('camera')}>{cameraOn ? 'Turn camera off' : 'Turn camera on'}</button>
          <button className="telehealth-button telehealth-button-secondary" type="button" onClick={() => void stop().then(() => { setState('ended'); setMessage('You left the call. Your camera and microphone have been released.') })}>Leave video visit</button>
        </> : null}
      </div>
    </section>
  )
}
