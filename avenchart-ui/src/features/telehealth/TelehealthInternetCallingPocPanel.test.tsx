// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest'
import TelehealthInternetCallingPocPanel from './TelehealthInternetCallingPocPanel.tsx'
import type { TelehealthConnectionGrant, TelehealthInternetCallingConfiguration } from './api.ts'

const sdk = vi.hoisted(() => {
  const handlers = new Map<string, (...args: unknown[]) => void>()
  const call = {
    state: 'Connected', isMuted: false, remoteParticipants: [] as unknown[], localVideoStreams: [] as unknown[],
    on: vi.fn((name: string, handler: (...args: unknown[]) => void) => handlers.set(name, handler)),
    off: vi.fn((name: string) => handlers.delete(name)),
    hangUp: vi.fn().mockResolvedValue(undefined), muteIncomingAudio: vi.fn().mockResolvedValue(undefined),
    mute: vi.fn(async () => { call.isMuted = true }), unmute: vi.fn(async () => { call.isMuted = false }),
    startVideo: vi.fn().mockResolvedValue(undefined), stopVideo: vi.fn().mockResolvedValue(undefined),
  }
  return {
    handlers, call,
    camera1: { id: 'acs-camera-1', name: 'Laptop camera' }, camera2: { id: 'acs-camera-2', name: 'USB camera' },
    manager: {
      isSpeakerSelectionAvailable: true,
      getCameras: vi.fn(), getMicrophones: vi.fn(), getSpeakers: vi.fn(),
      askDevicePermission: vi.fn(), selectMicrophone: vi.fn(), selectSpeaker: vi.fn(),
    },
    join: vi.fn(), agentDispose: vi.fn().mockResolvedValue(undefined),
    clientDispose: vi.fn().mockResolvedValue(undefined), tokenDispose: vi.fn(),
    makeStream: vi.fn(), streamDispose: vi.fn(), createAgent: vi.fn(),
    rendererSources: [] as unknown[], createView: vi.fn(), rendererDispose: vi.fn(), viewDispose: vi.fn(), lifecycle: [] as string[],
  }
})

vi.mock('@azure/communication-calling', () => ({
  CallClient: class {
    getDeviceManager = async () => sdk.manager
    createCallAgent = sdk.createAgent
    dispose = sdk.clientDispose
  },
  LocalVideoStream: class {
    constructor(device: unknown) { sdk.makeStream(device) }
    dispose = sdk.streamDispose
  },
  VideoStreamRenderer: class {
    constructor(stream: unknown) { sdk.rendererSources.push(stream) }
    createView = async (options?: unknown) => {
      sdk.lifecycle.push('createView')
      sdk.createView(options)
      return { target: document.createElement('video'), dispose: sdk.viewDispose }
    }
    dispose = sdk.rendererDispose
  },
}))
vi.mock('@azure/communication-common', () => ({
  AzureCommunicationTokenCredential: class { dispose = sdk.tokenDispose },
}))

const grant = { grantId: 'demo-grant' } as TelehealthConnectionGrant
const config = { accessToken: 'unit-test-token', groupId: 'unit-test-group' } as TelehealthInternetCallingConfiguration

async function join() {
  await screen.findByRole('option', { name: 'USB camera' })
  fireEvent.click(screen.getByRole('button', { name: 'Join video visit' }))
  await waitFor(() => expect(sdk.join).toHaveBeenCalled())
}

describe('internet video visit', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.stubGlobal('isSecureContext', true)
    sdk.handlers.clear()
    sdk.rendererSources.length = 0; sdk.lifecycle.length = 0
    sdk.call.state = 'Connected'; sdk.call.isMuted = false; sdk.call.remoteParticipants = []; sdk.call.localVideoStreams = []
    sdk.manager.isSpeakerSelectionAvailable = true
    sdk.manager.getCameras.mockResolvedValue([sdk.camera1, sdk.camera2])
    sdk.manager.getMicrophones.mockResolvedValue([{ id: 'acs-microphone', name: 'USB microphone' }])
    sdk.manager.getSpeakers.mockResolvedValue([{ id: 'acs-speaker', name: 'Headphones' }])
    sdk.manager.askDevicePermission.mockResolvedValue({ audio: true, video: true })
    sdk.join.mockImplementation((_locator, options) => {
      sdk.lifecycle.push('join')
      sdk.call.localVideoStreams = options.videoOptions.localVideoStreams
      return sdk.call
    })
    sdk.createAgent.mockResolvedValue({ join: sdk.join, dispose: sdk.agentDispose })
  })
  afterEach(() => vi.unstubAllGlobals())

  it('captures the second SDK camera selected by the user and identifies it in the preview', async () => {
    render(<TelehealthInternetCallingPocPanel grant={grant} role="patient" getCallingConfiguration={async () => config} />)
    await screen.findByRole('option', { name: 'USB camera' })
    fireEvent.change(screen.getByRole('combobox', { name: 'Camera for synthetic internet call' }), { target: { value: 'acs-camera-2' } })
    await join()
    expect(sdk.makeStream).toHaveBeenCalledWith(sdk.camera2)
    expect(screen.getByText('Your camera: USB camera')).toBeVisible()
    expect(sdk.manager.selectMicrophone).toHaveBeenCalledWith({ id: 'acs-microphone', name: 'USB microphone' })
  })

  it('renders a mirrored self-preview from the stream adopted by the active call', async () => {
    render(<TelehealthInternetCallingPocPanel grant={grant} role="patient" getCallingConfiguration={async () => config} />)
    await join()
    const localPreview = screen.getByLabelText('Local camera preview')
    await waitFor(() => expect(localPreview.querySelector('video')).not.toBeNull())
    expect(sdk.rendererSources).toContain(sdk.call.localVideoStreams[0])
    expect(sdk.createView).toHaveBeenCalledWith({ isMirrored: true, scalingMode: 'Crop' })
    expect(sdk.lifecycle.indexOf('join')).toBeLessThan(sdk.lifecycle.indexOf('createView'))
  })

  it('keeps an unplugged camera selection and refuses to substitute the first camera', async () => {
    const configuration = vi.fn(async () => config)
    render(<TelehealthInternetCallingPocPanel grant={grant} role="patient" getCallingConfiguration={configuration} />)
    await screen.findByRole('option', { name: 'USB camera' })
    fireEvent.change(screen.getByRole('combobox', { name: 'Camera for synthetic internet call' }), { target: { value: 'acs-camera-2' } })
    sdk.manager.getCameras.mockResolvedValue([sdk.camera1])
    fireEvent.click(screen.getByRole('button', { name: 'Refresh available devices' }))
    await screen.findByRole('option', { name: 'Selected camera disconnected' })
    fireEvent.click(screen.getByRole('button', { name: 'Join video visit' }))
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Your selected camera is unavailable'))
    expect(configuration).not.toHaveBeenCalled()
    expect(sdk.makeStream).not.toHaveBeenCalled()
    expect(sdk.join).not.toHaveBeenCalled()
  })

  it('reports waiting while alone and requires an actually connected remote participant', async () => {
    const onConnection = vi.fn()
    render(<TelehealthInternetCallingPocPanel grant={grant} role="physician" getCallingConfiguration={async () => config} onConnectionStateChange={onConnection} />)
    await join()
    expect(onConnection).not.toHaveBeenCalledWith(true)
    expect(screen.getByRole('status')).toHaveTextContent('Waiting for the other participant')
    const participant = { state: 'Connected', videoStreams: [], on: vi.fn(), off: vi.fn() }
    act(() => {
      sdk.call.remoteParticipants = [participant]
      sdk.handlers.get('remoteParticipantsUpdated')?.({ added: [participant], removed: [] })
    })
    expect(onConnection).toHaveBeenLastCalledWith(true)
    expect(screen.getByRole('status')).toHaveTextContent('Patient and physician are connected')
    act(() => {
      sdk.call.remoteParticipants = []
      sdk.handlers.get('remoteParticipantsUpdated')?.({ added: [], removed: [participant] })
    })
    expect(onConnection).toHaveBeenLastCalledWith(false)
    expect(screen.getByRole('status')).toHaveTextContent('Waiting for the other participant')
  })

  it('silences microphone and incoming audio in the same-laptop tab', async () => {
    render(<TelehealthInternetCallingPocPanel grant={grant} role="patient" getCallingConfiguration={async () => config} />)
    fireEvent.click(screen.getByRole('checkbox', { name: /Same-laptop demo/ }))
    await join()
    expect(sdk.join).toHaveBeenCalledWith({ groupId: config.groupId }, expect.objectContaining({ audioOptions: { muted: true } }))
    expect(sdk.call.muteIncomingAudio).toHaveBeenCalledOnce()
    expect(screen.getByRole('button', { name: 'Unmute microphone' })).toBeDisabled()
  })

  it('supports a complete microphone and camera toggle cycle without ending the visit', async () => {
    render(<TelehealthInternetCallingPocPanel grant={grant} role="physician" getCallingConfiguration={async () => config} />)
    await join()
    fireEvent.click(screen.getByRole('button', { name: 'Mute microphone' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Unmute microphone' }))
    await screen.findByRole('button', { name: 'Mute microphone' })
    expect(sdk.call.mute).toHaveBeenCalledOnce(); expect(sdk.call.unmute).toHaveBeenCalledOnce()
    fireEvent.click(screen.getByRole('button', { name: 'Turn camera off' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Turn camera on' }))
    await screen.findByRole('button', { name: 'Turn camera off' })
    expect(sdk.call.stopVideo).toHaveBeenCalledOnce(); expect(sdk.call.startVideo).toHaveBeenCalledOnce()
    expect(sdk.call.hangUp).not.toHaveBeenCalled()
  })

  it('does not create a call after the user leaves during credential preparation', async () => {
    let resolveConfiguration!: (value: TelehealthInternetCallingConfiguration) => void
    const configuration = vi.fn(() => new Promise<TelehealthInternetCallingConfiguration>((resolve) => { resolveConfiguration = resolve }))
    render(<TelehealthInternetCallingPocPanel grant={grant} role="patient" getCallingConfiguration={configuration} />)
    await screen.findByRole('option', { name: 'USB camera' })
    fireEvent.click(screen.getByRole('button', { name: 'Join video visit' }))
    await waitFor(() => expect(configuration).toHaveBeenCalledOnce())
    fireEvent.click(screen.getByRole('button', { name: 'Leave video visit' }))
    await screen.findByRole('button', { name: 'Rejoin video visit' })
    await act(async () => { resolveConfiguration(config) })
    expect(sdk.createAgent).not.toHaveBeenCalled(); expect(sdk.join).not.toHaveBeenCalled()
  })

  it('releases disconnected calls and permits rejoining', async () => {
    render(<TelehealthInternetCallingPocPanel grant={grant} role="patient" getCallingConfiguration={async () => config} />)
    await join()
    await act(async () => { sdk.call.state = 'Disconnected'; sdk.handlers.get('stateChanged')?.() })
    expect(sdk.agentDispose).toHaveBeenCalled(); expect(sdk.streamDispose).toHaveBeenCalled()
    sdk.call.state = 'Connected'
    fireEvent.click(screen.getByRole('button', { name: 'Rejoin video visit' }))
    await waitFor(() => expect(sdk.join).toHaveBeenCalledTimes(2))
  })

  it('works with default output when speaker selection is unsupported', async () => {
    sdk.manager.isSpeakerSelectionAvailable = false
    render(<TelehealthInternetCallingPocPanel grant={grant} role="patient" getCallingConfiguration={async () => config} />)
    await join()
    expect(screen.getByRole('combobox', { name: 'Speaker for synthetic internet call' })).toBeDisabled()
    expect(sdk.manager.selectSpeaker).not.toHaveBeenCalled()
  })

  it('recovers from denied permission without requesting a calling credential', async () => {
    sdk.manager.askDevicePermission.mockResolvedValue({ audio: false, video: false })
    const configuration = vi.fn(async () => config)
    render(<TelehealthInternetCallingPocPanel grant={grant} role="patient" getCallingConfiguration={configuration} />)
    await screen.findByRole('option', { name: 'USB camera' })
    fireEvent.click(screen.getByRole('button', { name: 'Join video visit' }))
    await screen.findByRole('alert')
    expect(configuration).not.toHaveBeenCalled()
    sdk.manager.askDevicePermission.mockResolvedValue({ audio: true, video: true })
    fireEvent.click(screen.getByRole('button', { name: 'Rejoin video visit' }))
    await waitFor(() => expect(sdk.join).toHaveBeenCalledOnce())
  })
})
