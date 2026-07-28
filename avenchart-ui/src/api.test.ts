import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  ApiRequestError,
  createInventoryPurchaseReceipt,
  createInventoryPurchaseRequisition,
  decideInventoryPurchaseRequisition,
  downloadPatientDocument,
  endPatientPortalSession,
  getCurrentSession,
  getInventoryLotMetadataHistory,
  getInventoryPurchaseRequisitions,
  getPatientBilling,
  getPatientCareTeamOptions,
  getPatientPortalAppointments,
  getPatientPortalHome,
  getPatientProviderAssignmentOptions,
  getStaffMessageInbox,
  logout,
  SESSION_INVALID_EVENT,
  submitInventoryPurchaseRequisition,
  updatePatientCareTeam,
  updatePatientEmployer,
  updatePatientGuardianContact,
  updatePatientProviderAssignment,
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
    fetchMock.mockReset()
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
    fetchMock.mockResolvedValueOnce(
      new Response('clinical document', {
        status: 200,
        headers: {
          'content-type': 'application/pdf',
          'content-disposition':
            "attachment; filename*=UTF-8''visit%20summary.pdf",
        },
      }),
    )

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
    fetchMock.mockResolvedValueOnce(
      new Response('<html>login</html>', {
        status: 200,
        headers: { 'content-type': 'text/html' },
      }),
    )

    await expect(
      downloadPatientDocument('staff-session', 42, 'fallback.pdf'),
    ).rejects.toThrow('returned a web page')
  })

  it('announces a rejected clinician session and preserves Problem Details', async () => {
    const invalidSession = vi.fn()
    window.addEventListener(SESSION_INVALID_EVENT, invalidSession)
    fetchMock.mockResolvedValueOnce(
      jsonResponse(
        {
          title: 'Unauthorized',
          detail: 'This session has expired.',
          status: 401,
          traceId: 'trace-1',
        },
        401,
      ),
    )

    const error = await getCurrentSession('expired-session').catch(
      (caught) => caught,
    )

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
    fetchMock.mockResolvedValueOnce(
      jsonResponse({
        datasetId: 'legacy-ehr-shared-synthetic-v1',
        datasetVersion: 'v1',
        total: 0,
        offset: 20,
        limit: 20,
        counts: { total: 0, unread: 0, assignedToMe: 0, unassigned: 0 },
        items: [],
      }),
    )

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

  it('uses the protected patient relationship and care-team contracts', async () => {
    const responseBody = { canonicalId: 'MOD-PAT-0004' }
    fetchMock
      .mockResolvedValueOnce(jsonResponse({ providers: [] }))
      .mockResolvedValueOnce(jsonResponse({ providers: [], contacts: [] }))
      .mockResolvedValueOnce(jsonResponse(responseBody))
      .mockResolvedValueOnce(jsonResponse(responseBody))
      .mockResolvedValueOnce(jsonResponse(responseBody))
      .mockResolvedValueOnce(jsonResponse(responseBody))

    await getPatientProviderAssignmentOptions('staff-session')
    await getPatientCareTeamOptions('staff-session', 'MOD-PAT-0004')
    await updatePatientGuardianContact('staff-session', 'MOD-PAT-0004', {
      motherName: 'Maria Kim',
      guardianName: 'Jordan Morris',
      guardianRelationship: 'guardian',
      guardianPhone: '555-0100',
      guardianEmail: 'guardian@example.test',
      guardianSex: 'UNK',
      guardianAddress: '100 Main Street',
      guardianCity: 'Boston',
      guardianState: 'MA',
      guardianPostalCode: '02108',
      guardianCountry: 'USA',
      guardianWorkPhone: '555-0101',
    })
    await updatePatientEmployer('staff-session', 'MOD-PAT-0004', {
      employerName: 'Northwind Health',
      employerStreet: '200 State Street',
      employerCity: 'Boston',
      employerState: 'MA',
      employerPostalCode: '02109',
      employerCountry: 'USA',
    })
    await updatePatientProviderAssignment('staff-session', 'MOD-PAT-0004', {
      providerId: 7,
    })
    await updatePatientCareTeam('staff-session', 'MOD-PAT-0004', {
      teamName: 'Primary care team',
      teamStatus: 'active',
      members: [
        {
          userId: 7,
          contactId: null,
          role: 'primary_care_provider',
          facilityId: 3,
          providerSince: '2025-01-01',
          status: 'active',
          note: 'Lead',
        },
      ],
    })

    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      'http://localhost:5001/api/patients/provider-options',
      'http://localhost:5001/api/patients/MOD-PAT-0004/care-team-options',
      'http://localhost:5001/api/patients/MOD-PAT-0004/guardian-contact',
      'http://localhost:5001/api/patients/MOD-PAT-0004/employer',
      'http://localhost:5001/api/patients/MOD-PAT-0004/provider-assignment',
      'http://localhost:5001/api/patients/MOD-PAT-0004/care-team',
    ])
    for (const call of fetchMock.mock.calls.slice(2)) {
      expect(call[1]).toEqual(
        expect.objectContaining({
          method: 'PUT',
          headers: {
            'X-Legacy EHR-Session': 'staff-session',
            'content-type': 'application/json',
          },
        }),
      )
    }
  })

  it('loads a patient account through the protected billing route', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse({
        patientId: 'MOD-PAT-0004',
        accountSummary: { balanceAmount: 125 },
        agingSummary: { totalBalanceAmount: 125 },
        ledgerSummary: { entryCount: 2 },
        statementSummary: { statementStatus: 'Ready' },
        ledgerEntries: [],
        encounters: [],
      }),
    )

    await getPatientBilling('staff-session', ' MOD-PAT-0004 ')

    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5001/api/billing/MOD-PAT-0004',
      expect.objectContaining({
        headers: { 'X-Legacy EHR-Session': 'staff-session' },
      }),
    )
  })

  it('loads upcoming and past appointments through the protected portal route', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse({
        authenticated: true,
        upcomingAppointmentCount: 1,
        upcomingAppointments: [{ id: 'APPT-PORTAL-1' }],
        pastAppointmentCount: 1,
        pastAppointments: [{ id: 'APPT-1' }],
      }),
    )

    const result = await getPatientPortalAppointments('portal-session')

    expect(result.pastAppointmentCount).toBe(1)
    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5001/api/patient-portal/appointments',
      expect.objectContaining({
        headers: {
          'X-Legacy EHR-Patient-Portal-Session': 'portal-session',
        },
      }),
    )
  })

  it('loads immutable inventory lot metadata history through the protected route', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse([
        {
          auditId: 'audit-1',
          priorLotNumber: 'LOT-A',
          newLotNumber: 'LOT-B',
          changedBy: 'admin',
          changedAt: '2026-07-27T12:00:00Z',
        },
      ]),
    )

    const result = await getInventoryLotMetadataHistory('staff-session', 20001)

    expect(result[0]?.newLotNumber).toBe('LOT-B')
    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5001/api/inventory/lots/20001/metadata-history',
      expect.objectContaining({
        headers: { 'X-Legacy EHR-Session': 'staff-session' },
      }),
    )
  })

  it('uses protected inventory requisition lifecycle contracts', async () => {
    const draft = {
      requisitionId: 'req-1',
      status: 'draft',
      lines: [],
      events: [],
    }
    fetchMock
      .mockResolvedValueOnce(jsonResponse([draft]))
      .mockResolvedValueOnce(jsonResponse(draft, 201))
      .mockResolvedValueOnce(jsonResponse({ ...draft, status: 'submitted' }))
      .mockResolvedValueOnce(jsonResponse({ ...draft, status: 'approved' }))

    await getInventoryPurchaseRequisitions('staff-session')
    await createInventoryPurchaseRequisition('staff-session', {
      facilityId: 12,
      vendorId: null,
      notes: 'Restock',
      lines: [{ itemId: 10001, quantity: 4 }],
    })
    await submitInventoryPurchaseRequisition('staff-session', 'req-1')
    await decideInventoryPurchaseRequisition(
      'staff-session',
      'req-1',
      'approve',
      'Approved for restock',
    )

    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      'http://localhost:5001/api/inventory/purchase-requisitions',
      'http://localhost:5001/api/inventory/purchase-requisitions',
      'http://localhost:5001/api/inventory/purchase-requisitions/req-1/submit',
      'http://localhost:5001/api/inventory/purchase-requisitions/req-1/decisions/approve',
    ])
    expect(fetchMock.mock.calls[1]?.[1]).toEqual(
      expect.objectContaining({
        method: 'POST',
        headers: {
          'X-Legacy EHR-Session': 'staff-session',
          'content-type': 'application/json',
        },
      }),
    )
    expect(fetchMock.mock.calls[3]?.[1]?.body).toBe(
      JSON.stringify({ notes: 'Approved for restock' }),
    )
  })

  it('records a protected inventory receipt with requisition reconciliation input', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(
        {
          receiptId: 'receipt-1',
          requisitionReconciliation: {
            requisitionId: 'req-1',
            receivedQuantity: 2,
          },
        },
        201,
      ),
    )

    const result = await createInventoryPurchaseReceipt('staff-session', {
      vendorId: 'vendor-1',
      facilityId: 12,
      itemId: 10001,
      lotNumber: 'LOT-NEW',
      expirationDate: '2027-12-31',
      quantity: 2,
      unitCost: 8.75,
      referenceNumber: 'REF-1',
      notes: 'Partial receipt',
      requisitionId: 'req-1',
    })

    expect(result.receiptId).toBe('receipt-1')
    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5001/api/inventory/purchase-receipts',
      expect.objectContaining({
        method: 'POST',
        headers: {
          'X-Legacy EHR-Session': 'staff-session',
          'content-type': 'application/json',
        },
        body: JSON.stringify({
          vendorId: 'vendor-1',
          facilityId: 12,
          itemId: 10001,
          lotNumber: 'LOT-NEW',
          expirationDate: '2027-12-31',
          quantity: 2,
          unitCost: 8.75,
          referenceNumber: 'REF-1',
          notes: 'Partial receipt',
          requisitionId: 'req-1',
        }),
      }),
    )
  })

  it('normalizes network failures without treating the session as invalid', async () => {
    const invalidSession = vi.fn()
    window.addEventListener(SESSION_INVALID_EVENT, invalidSession)
    fetchMock.mockRejectedValueOnce(new TypeError('fetch failed'))

    const error = await getCurrentSession('staff-session').catch(
      (caught) => caught,
    )

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
      jsonResponse(
        { title: 'Unauthorized', detail: 'Portal session expired.' },
        401,
      ),
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
