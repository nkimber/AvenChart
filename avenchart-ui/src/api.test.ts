import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  ApiRequestError,
  downloadPatientDocument,
  endPatientPortalSession,
  getCurrentSession,
  getPatientPortalHome,
  getStaffMessageInbox,
  logout,
  SESSION_INVALID_EVENT,
} from './api.ts'

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/problem+json' },
  })
}

describe('authenticated API transport', () => {
  const fetchMock = vi.fn<typeof fetch>()

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('ends a clinician session using the backend logout contract', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({ authenticated: false }))

    await logout('staff-session')

    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5001/api/auth/logout',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ sessionId: 'staff-session' }),
      }),
    )
  })

  it('ends a portal session using its authenticated DELETE contract', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({ authenticated: false }))

    await endPatientPortalSession('portal-session')

    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5001/api/patient-portal/session',
      expect.objectContaining({
        method: 'DELETE',
        headers: { 'X-Legacy EHR-Patient-Portal-Session': 'portal-session' },
      }),
    )
  })

  it('downloads a document from the protected document endpoint', async () => {
    fetchMock.mockResolvedValueOnce(new Response('clinical document', {
      status: 200,
      headers: {
        'content-type': 'application/pdf',
        'content-disposition': "attachment; filename*=UTF-8''visit%20summary.pdf",
      },
    }))

    const result = await downloadPatientDocument(
      'staff-session',
      42,
      'fallback.pdf',
    )

    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5001/api/documents/42/download',
      expect.objectContaining({
        headers: { 'X-Legacy EHR-Session': 'staff-session' },
      }),
    )
    expect(result.fileName).toBe('visit summary.pdf')
    expect(result.contentType).toBe('application/pdf')
    expect(await result.blob.text()).toBe('clinical document')
  })

  it('rejects an HTML response masquerading as a document', async () => {
    fetchMock.mockResolvedValueOnce(new Response('<html>login</html>', {
      status: 200,
      headers: { 'content-type': 'text/html' },
    }))

    await expect(
      downloadPatientDocument('staff-session', 42, 'fallback.pdf'),
    ).rejects.toThrow('returned a web page')
  })

  it('announces a rejected clinician session and preserves Problem Details', async () => {
    const invalidSession = vi.fn()
    window.addEventListener(SESSION_INVALID_EVENT, invalidSession)
    fetchMock.mockResolvedValueOnce(jsonResponse({
      title: 'Unauthorized',
      detail: 'This session has expired.',
      status: 401,
      traceId: 'trace-1',
    }, 401))

    const error = await getCurrentSession('expired-session').catch((caught) => caught)

    expect(error).toBeInstanceOf(ApiRequestError)
    expect(error).toMatchObject({
      message: 'This session has expired.',
      status: 401,
      problem: expect.objectContaining({ traceId: 'trace-1' }),
    })
    expect(invalidSession).toHaveBeenCalledOnce()
    window.removeEventListener(SESSION_INVALID_EVENT, invalidSession)
  })

  it('builds the staff inbox query without sending inactive filters', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({
      datasetId: 'legacy-ehr-shared-synthetic-v1',
      datasetVersion: 'v1',
      total: 0,
      offset: 20,
      limit: 20,
      counts: { total: 0, unread: 0, assignedToMe: 0, unassigned: 0 },
      items: [],
    }))

    await getStaffMessageInbox('staff-session', {
      status: 'new',
      assignment: 'mine',
      priority: 'all',
      minimumAgeDays: 7,
      offset: 20,
      limit: 20,
    })

    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5001/api/messages/inbox?status=new&assignment=mine&minimumAgeDays=7&offset=20&limit=20',
      expect.objectContaining({
        headers: { 'X-Legacy EHR-Session': 'staff-session' },
      }),
    )
  })

  it('normalizes network failures without treating the session as invalid', async () => {
    const invalidSession = vi.fn()
    window.addEventListener(SESSION_INVALID_EVENT, invalidSession)
    fetchMock.mockRejectedValueOnce(new TypeError('fetch failed'))

    const error = await getCurrentSession('staff-session').catch((caught) => caught)

    expect(error).toBeInstanceOf(ApiRequestError)
    expect(error).toMatchObject({ kind: 'network' })
    expect(error.message).toContain('could not reach the server')
    expect(invalidSession).not.toHaveBeenCalled()
    window.removeEventListener(SESSION_INVALID_EVENT, invalidSession)
  })

  it('invalidates the portal once when any protected portal call returns 401', async () => {
    const invalidSession = vi.fn()
    window.addEventListener(SESSION_INVALID_EVENT, invalidSession)
    fetchMock.mockResolvedValueOnce(
      jsonResponse({ title: 'Unauthorized', detail: 'Portal session expired.' }, 401),
    )

    await expect(getPatientPortalHome('portal-session')).rejects.toMatchObject({
      kind: 'http',
      status: 401,
      message: 'Portal session expired.',
    })
    expect(invalidSession).toHaveBeenCalledOnce()
    expect(invalidSession.mock.calls[0][0]).toMatchObject({
      detail: { scope: 'portal' },
    })
    window.removeEventListener(SESSION_INVALID_EVENT, invalidSession)
  })
})
