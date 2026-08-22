// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  getTherapyGroupMembers,
  getTherapyGroups,
  getTherapyGroupSessionAttendance,
  getTherapyGroupSessions,
  recordTherapyGroupSessionAttendance,
  type TherapyGroup,
  type TherapyGroupSession,
  type TherapyGroupSessionAttendance,
} from '../../api.ts'
import TherapyGroups from './TherapyGroups.tsx'

vi.mock('../../api.ts', async (importOriginal) => {
  const original = await importOriginal<typeof import('../../api.ts')>()
  return {
    ...original,
    getTherapyGroupMembers: vi.fn(),
    getTherapyGroups: vi.fn(),
    getTherapyGroupSessionAttendance: vi.fn(),
    getTherapyGroupSessions: vi.fn(),
    recordTherapyGroupSessionAttendance: vi.fn(),
  }
})

const group: TherapyGroup = {
  id: 'group-1',
  name: 'Coping skills',
  status: 'active',
  capacity: 12,
  createdAt: '2030-04-17T09:00:00Z',
}

const groupSession: TherapyGroupSession = {
  id: 'group-session-1',
  groupId: group.id,
  startsAt: '2030-04-17T09:00:00Z',
  durationMinutes: 60,
  topic: 'Grounding techniques',
  status: 'scheduled',
  createdAt: '2030-04-17T08:00:00Z',
}

const unrecordedAttendance: TherapyGroupSessionAttendance = {
  sessionId: groupSession.id,
  patientId: 'patient-1',
  legacyPid: 1001,
  displayName: 'Alex Rivera',
  status: 'unrecorded',
  note: null,
  recordedAt: null,
}

function TestOutlet() {
  return (
    <Outlet
      context={{
        session: {
          sessionId: 'staff-session',
          username: 'clinician',
          displayName: 'Clinician',
          role: 'staff',
        },
        signOut: vi.fn(),
      }}
    />
  )
}

function renderTherapyGroups() {
  return render(
    <MemoryRouter initialEntries={['/clinician/therapy-groups']}>
      <Routes>
        <Route path="/clinician" element={<TestOutlet />}>
          <Route path="therapy-groups" element={<TherapyGroups />} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

describe('TherapyGroups attendance', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getTherapyGroups).mockResolvedValue({ groups: [group] })
    vi.mocked(getTherapyGroupMembers).mockResolvedValue([])
    vi.mocked(getTherapyGroupSessions).mockResolvedValue([groupSession])
    vi.mocked(getTherapyGroupSessionAttendance).mockResolvedValue({
      sessionId: groupSession.id,
      attendance: [unrecordedAttendance],
    })
    vi.mocked(recordTherapyGroupSessionAttendance).mockResolvedValue({
      ...unrecordedAttendance,
      status: 'present',
      recordedAt: '2030-04-17T10:00:00Z',
    })
  })

  it('records a participant attendance before session completion', async () => {
    const user = userEvent.setup()
    renderTherapyGroups()

    await user.click(await screen.findByRole('button', { name: 'Manage' }))
    await user.click(await screen.findByRole('button', { name: 'Record attendance' }))
    const attendance = await screen.findByLabelText('Attendance for Alex Rivera')

    expect(getTherapyGroupSessionAttendance).toHaveBeenCalledWith(
      'staff-session',
      group.id,
      groupSession.id,
    )
    await user.selectOptions(attendance, 'present')

    await waitFor(() => expect(recordTherapyGroupSessionAttendance).toHaveBeenCalledWith(
      'staff-session',
      group.id,
      groupSession.id,
      'patient-1',
      { status: 'present', note: null },
    ))
    expect(attendance).toHaveValue('present')
  })
})
