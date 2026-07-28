import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  allocateInventoryPatientSale,
  ApiRequestError,
  approvePrescriptionRefillRequest,
  archivePatientDocument,
  assignLabReportReviewer,
  bulkSignLabReports,
  completePatientDocumentRouting,
  createInventoryPatientSale,
  createInventoryCountReconciliation,
  createInventoryExpiryDisposition,
  createInventoryLotDestruction,
  createInventoryPurchaseReceipt,
  createInventoryPurchaseRequisition,
  createInventoryTransaction,
  createInventoryTransfer,
  createPatientBinaryDocument,
  createPatientDocument,
  createPatientExternalLinkDocument,
  createPatientMessage,
  createPrescription,
  deleteAllergy,
  deleteImmunization,
  deleteMedication,
  deletePatientDocument,
  deleteProblem,
  decidePrescriptionRefillRequest,
  decideInventoryPurchaseRequisition,
  dispenseInventoryPrescription,
  downloadInventoryActivityCsv,
  downloadPatientDocument,
  downloadPatientDocumentVersion,
  endPatientPortalSession,
  findPatientDuplicateCandidates,
  getCurrentSession,
  getInventoryActivityReport,
  getInventoryMedicationCatalog,
  getInventoryLotMetadataHistory,
  getInventoryPurchaseRequisitions,
  getClinicalPharmacyDirectory,
  getPatientBilling,
  getPatientCareTeamOptions,
  getPatientAdministrationHistory,
  getPatientDocumentArchiveHistory,
  getPatientDocumentCategoryOptions,
  getPatientDocumentMetadataHistory,
  getPatientDocumentReviewHistory,
  getPatientDocumentRoutingAssignees,
  getPatientDocumentRoutingHistory,
  getPatientDocumentRoutingQueue,
  getPatientDocumentVersionHistory,
  getPatientDocuments,
  getPatientPortalAppointments,
  getPatientPortalHome,
  getPatientPortalMessages,
  getPatientPortalPrescriptionRefillHistory,
  getPatientProviderAssignmentHistory,
  getPatientProviderAssignmentOptions,
  getPrescriptionAuditHistory,
  getPrescriptionRefillQueue,
  getProcedureOrderQueue,
  getProcedureReportQueue,
  getStaffMessageInbox,
  logout,
  markImmunizationEnteredInError,
  reopenLabReportReview,
  replacePatientDocumentBinaryContent,
  replacePatientDocumentContent,
  restorePatientDocument,
  reviewPatientDocument,
  refillPrescription,
  replyToPatientMessage,
  routePrescriptionToPharmacy,
  routePatientDocument,
  searchEncounters,
  searchClinicalMedicationVocabulary,
  SESSION_INVALID_EVENT,
  signLabReport,
  submitInventoryPurchaseRequisition,
  updatePatientCareTeam,
  updatePatientEmployer,
  updatePatientGuardianContact,
  updatePatientMessageAssignment,
  updatePatientMessageStatus,
  updatePatientDocumentMetadata,
  updatePatientProviderAssignment,
  updatePrescription,
  updateInventoryMedicationLink,
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

  it('retains the portal sent-mail projection used for refill history', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse({
        authenticated: true,
        messageCount: 0,
        messages: [],
        sentMessageCount: 1,
        sentMessages: [
          {
            id: '901',
            date: '2026-07-28',
            title: 'Prescription refill request - Metformin',
            body: 'Prescription: Metformin\nPrescription ID: RX-901',
            status: 'Done',
            senderName: 'Portal Patient',
            recipientName: 'Care Team',
            portalRelation: 'portal:prescription-refill-request',
          },
        ],
        allMessageCount: 1,
        allMessages: [],
        deletedMessageCount: 0,
        deletedMessages: [],
      }),
    )

    const result = await getPatientPortalMessages('portal-session')

    expect(result.sentMessageCount).toBe(1)
    expect(result.sentMessages?.[0]).toMatchObject({
      status: 'Done',
      portalRelation: 'portal:prescription-refill-request',
    })
    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5001/api/patient-portal/messages',
      expect.objectContaining({
        headers: {
          'X-Legacy EHR-Patient-Portal-Session': 'portal-session',
        },
      }),
    )
  })

  it('reads the portal refill lifecycle instead of inferring state from sent mail', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse({
        authenticated: true,
        requestCount: 1,
        requests: [
          {
            messageId: 901,
            threadId: 900,
            prescriptionId: 'RX-901',
            drug: 'Metformin',
            requestDate: '2026-07-28',
            status: 'clarification-requested',
            patientNote: 'Please review',
            staffResponse: 'Which pharmacy should we use?',
            updatedAt: '2026-07-28T14:00:00Z',
            updatedBy: 'clinician',
          },
        ],
      }),
    )

    const result =
      await getPatientPortalPrescriptionRefillHistory('portal-session')

    expect(result.requests[0]).toMatchObject({
      status: 'clarification-requested',
      staffResponse: 'Which pharmacy should we use?',
    })
    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5001/api/patient-portal/prescription-refill-requests',
      expect.objectContaining({
        headers: {
          'X-Legacy EHR-Patient-Portal-Session': 'portal-session',
        },
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

  it('uses protected document intake and metadata-history contracts', async () => {
    const detail = {
      datasetId: 'legacy-ehr-shared-synthetic-v1',
      datasetVersion: '2026.07',
      patientId: 'MOD-PAT-0001',
      legacyPid: 1,
      pubpid: 'MOD-PAT-0001',
      patientDisplayName: 'Stone, Avery',
      count: 1,
      documents: [],
    }
    fetchMock
      .mockResolvedValueOnce(
        jsonResponse({
          datasetId: detail.datasetId,
          datasetVersion: detail.datasetVersion,
          maxFileSizeBytes: 26_214_400,
          categories: [{ id: 3, name: 'Medical Record' }],
        }),
      )
      .mockResolvedValueOnce(jsonResponse({ id: 91, detail }, 201))
      .mockResolvedValueOnce(jsonResponse({ id: 92, detail }, 201))
      .mockResolvedValueOnce(jsonResponse({ id: 93, detail }, 201))
      .mockResolvedValueOnce(
        jsonResponse({
          datasetId: detail.datasetId,
          datasetVersion: detail.datasetVersion,
          documentId: 91,
          documentKey: 'DOC-91',
          patientId: detail.patientId,
          legacyPid: detail.legacyPid,
          currentCategoryId: 3,
          currentCategoryName: 'Medical Record',
          currentName: 'Care note',
          currentDocDate: '2026-07-28',
          currentEncounter: 1000013,
          currentNotes: 'Created in chart.',
          eventCount: 0,
          returnedCount: 0,
          resultLimit: 100,
          events: [],
        }),
      )
      .mockResolvedValueOnce(jsonResponse({ id: 91, detail }))
      .mockResolvedValueOnce(
        jsonResponse({
          datasetId: detail.datasetId,
          datasetVersion: detail.datasetVersion,
          documentId: 91,
          documentKey: 'DOC-91',
          patientId: detail.patientId,
          legacyPid: detail.legacyPid,
          name: 'Updated care note',
          currentVersion: 1,
          versionCount: 1,
          versions: [
            {
              version: 1,
              versionLabel: 'Version 1',
              versionStatus: 'Current version',
              capturedAt: '2026-07-28 12:00:00',
              revisionAt: '2026-07-28 12:00:00',
              fileName: 'care-note.txt',
              mimetype: 'text/plain',
              sizeBytes: 20,
              hash: 'abc123',
              contentPreview: 'Documented care instructions.',
              canDownload: true,
            },
          ],
        }),
      )
      .mockResolvedValueOnce(jsonResponse({ id: 91, detail }))
      .mockResolvedValueOnce(jsonResponse({ id: 91, detail }))
      .mockResolvedValueOnce(
        new Response('original version', {
          status: 200,
          headers: {
            'content-type': 'text/plain',
            'content-disposition': 'attachment; filename="care-note-v1.txt"',
          },
        }),
      )
      .mockResolvedValueOnce(new Response(null, { status: 204 }))

    const categories = await getPatientDocumentCategoryOptions('staff-session')
    await createPatientDocument('staff-session', {
      patientId: 'MOD-PAT-0001',
      categoryId: 3,
      name: 'Care note',
      docDate: '2026-07-28',
      encounter: 1000013,
      content: 'Documented care instructions.',
      notes: 'Created in chart.',
    })
    await createPatientBinaryDocument('staff-session', {
      patientId: 'MOD-PAT-0001',
      categoryId: 3,
      name: 'Referral PDF',
      docDate: '2026-07-28',
      encounter: null,
      fileName: 'referral.pdf',
      mimetype: 'application/pdf',
      contentBase64: 'JVBERi0xLjQK',
      notes: null,
    })
    await createPatientExternalLinkDocument('staff-session', {
      patientId: 'MOD-PAT-0001',
      categoryId: 3,
      name: 'External image',
      docDate: '2026-07-28',
      encounter: null,
      url: 'https://example.test/image',
      notes: 'External source.',
    })
    const history = await getPatientDocumentMetadataHistory('staff-session', 91)
    await updatePatientDocumentMetadata('staff-session', 91, {
      categoryId: 2,
      name: 'Updated care note',
      docDate: '2026-07-29',
      encounter: 1000011,
      notes: 'Refiled in chart.',
      reason: 'Correct filing metadata.',
    })
    const versions = await getPatientDocumentVersionHistory(
      'staff-session',
      91,
    )
    await replacePatientDocumentContent('staff-session', 91, {
      fileName: 'care-note-v2.txt',
      content: 'Corrected clinical content.',
      reason: 'Correct transcription.',
      expectedVersion: 1,
    })
    await replacePatientDocumentBinaryContent('staff-session', 91, {
      fileName: 'care-note-v3.pdf',
      mimetype: 'application/pdf',
      contentBase64: 'JVBERi0xLjQK',
      reason: 'Attach signed source.',
      expectedVersion: 2,
    })
    const priorVersion = await downloadPatientDocumentVersion(
      'staff-session',
      91,
      1,
      'fallback.txt',
    )
    await deletePatientDocument('staff-session', 93)

    expect(categories.maxFileSizeBytes).toBe(26_214_400)
    expect(history.resultLimit).toBe(100)
    expect(versions.currentVersion).toBe(1)
    expect(priorVersion.fileName).toBe('care-note-v1.txt')
    expect(await priorVersion.blob.text()).toBe('original version')
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      'http://localhost:5001/api/documents/category-options',
      'http://localhost:5001/api/documents',
      'http://localhost:5001/api/documents/binary',
      'http://localhost:5001/api/documents/external-link',
      'http://localhost:5001/api/documents/91/metadata-history',
      'http://localhost:5001/api/documents/91/metadata',
      'http://localhost:5001/api/documents/91/versions',
      'http://localhost:5001/api/documents/91/content',
      'http://localhost:5001/api/documents/91/content/binary',
      'http://localhost:5001/api/documents/91/versions/1/download',
      'http://localhost:5001/api/documents/93',
    ])
    expect(fetchMock.mock.calls.map(([, options]) => options?.method)).toEqual([
      undefined,
      'POST',
      'POST',
      'POST',
      undefined,
      'PUT',
      undefined,
      'PUT',
      'PUT',
      undefined,
      'DELETE',
    ])
    expect(fetchMock.mock.calls[2]?.[1]).toMatchObject({
      headers: {
        'X-Legacy EHR-Session': 'staff-session',
        'content-type': 'application/json',
      },
      body: JSON.stringify({
        patientId: 'MOD-PAT-0001',
        categoryId: 3,
        name: 'Referral PDF',
        docDate: '2026-07-28',
        encounter: null,
        fileName: 'referral.pdf',
        mimetype: 'application/pdf',
        contentBase64: 'JVBERi0xLjQK',
        notes: null,
      }),
    })
    expect(fetchMock.mock.calls[5]?.[1]).toMatchObject({
      headers: {
        'X-Legacy EHR-Session': 'staff-session',
        'content-type': 'application/json',
      },
      body: JSON.stringify({
        categoryId: 2,
        name: 'Updated care note',
        docDate: '2026-07-29',
        encounter: 1000011,
        notes: 'Refiled in chart.',
        reason: 'Correct filing metadata.',
      }),
    })
    expect(fetchMock.mock.calls[7]?.[1]).toMatchObject({
      headers: {
        'X-Legacy EHR-Session': 'staff-session',
        'content-type': 'application/json',
      },
      body: JSON.stringify({
        fileName: 'care-note-v2.txt',
        content: 'Corrected clinical content.',
        reason: 'Correct transcription.',
        expectedVersion: 1,
      }),
    })
  })

  it('uses authenticated stale-safe patient document review contracts', async () => {
    fetchMock
      .mockResolvedValueOnce(
        jsonResponse({
          datasetId: 'legacy-ehr-shared-synthetic-v1',
          datasetVersion: 'v1',
          documentId: 91,
          documentKey: 'DOC-91',
          patientId: 'PAT-0001',
          legacyPid: 1,
          name: 'Care note',
          currentStatus: 'pending',
          currentReviewer: null,
          currentReviewedAt: null,
          eventCount: 0,
          returnedCount: 0,
          resultLimit: 100,
          events: [],
        }),
      )
      .mockResolvedValueOnce(
        jsonResponse({
          id: 91,
          detail: {
            datasetId: 'legacy-ehr-shared-synthetic-v1',
            datasetVersion: 'v1',
            patientId: 'PAT-0001',
            documents: [],
          },
        }),
      )

    const history = await getPatientDocumentReviewHistory(
      'staff-session',
      91,
    )
    await reviewPatientDocument('staff-session', 91, {
      reviewStatus: 'denied',
      reason: 'The source is incomplete.',
      expectedReviewStatus: 'pending',
    })

    expect(history.currentStatus).toBe('pending')
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      'http://localhost:5001/api/documents/91/review-history',
      'http://localhost:5001/api/documents/91/sign',
    ])
    expect(fetchMock.mock.calls[1]?.[1]).toMatchObject({
      method: 'PUT',
      headers: {
        'X-Legacy EHR-Session': 'staff-session',
        'content-type': 'application/json',
      },
      body: JSON.stringify({
        reviewStatus: 'denied',
        reason: 'The source is incomplete.',
        expectedReviewStatus: 'pending',
      }),
    })
  })

  it('discovers archived patient documents and sends reasoned stale-safe lifecycle transitions', async () => {
    const detail = {
      datasetId: 'legacy-ehr-shared-synthetic-v1',
      datasetVersion: 'v1',
      patientId: 'PAT-0001',
      legacyPid: 1,
      pubpid: 'MOD-PAT-0001',
      patientDisplayName: 'Stone, Avery',
      count: 2,
      activeCount: 1,
      archivedCount: 1,
      includesArchived: true,
      documents: [],
    }
    fetchMock
      .mockResolvedValueOnce(jsonResponse(detail))
      .mockResolvedValueOnce(
        jsonResponse({
          datasetId: detail.datasetId,
          datasetVersion: detail.datasetVersion,
          documentId: 91,
          documentKey: 'DOC-91',
          patientId: detail.patientId,
          legacyPid: detail.legacyPid,
          name: 'Care note',
          currentArchived: false,
          currentStateActor: null,
          currentStateAt: null,
          eventCount: 0,
          returnedCount: 0,
          resultLimit: 100,
          events: [],
        }),
      )
      .mockResolvedValueOnce(jsonResponse({ id: 91, detail }))
      .mockResolvedValueOnce(jsonResponse({ id: 91, detail }))

    const register = await getPatientDocuments(
      'staff-session',
      'PAT-0001',
      undefined,
      true,
    )
    const history = await getPatientDocumentArchiveHistory(
      'staff-session',
      91,
    )
    await archivePatientDocument('staff-session', 91, {
      reason: 'Move superseded copy out of the active register.',
      expectedArchived: false,
    })
    await restorePatientDocument('staff-session', 91, {
      reason: 'Returned after chart reconciliation.',
      expectedArchived: true,
    })

    expect(register.includesArchived).toBe(true)
    expect(history.resultLimit).toBe(100)
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      'http://localhost:5001/api/documents/PAT-0001?includeArchived=true',
      'http://localhost:5001/api/documents/91/archive-history',
      'http://localhost:5001/api/documents/91/soft-delete',
      'http://localhost:5001/api/documents/91/restore',
    ])
    expect(fetchMock.mock.calls[2]?.[1]).toMatchObject({
      method: 'PUT',
      headers: {
        'X-Legacy EHR-Session': 'staff-session',
        'content-type': 'application/json',
      },
      body: JSON.stringify({
        reason: 'Move superseded copy out of the active register.',
        expectedArchived: false,
      }),
    })
    expect(fetchMock.mock.calls[3]?.[1]).toMatchObject({
      method: 'PUT',
      body: JSON.stringify({
        reason: 'Returned after chart reconciliation.',
        expectedArchived: true,
      }),
    })
  })

  it('uses the filtered, assigned, stale-safe patient document routing lifecycle', async () => {
    const queue = {
      datasetId: 'legacy-ehr-shared-synthetic-v1',
      datasetVersion: 'v1',
      count: 1,
      totalCount: 1,
      returnedCount: 1,
      offset: 0,
      limit: 10,
      statusFilter: 'active',
      counts: {
        active: 1,
        pending: 1,
        inProgress: 0,
        unassigned: 1,
        highPriority: 1,
        overdue: 0,
        completed: 0,
      },
      items: [{ id: 91, taskVersion: 0, queueStatus: 'Awaiting review' }],
    }
    const mutation = {
      documentId: 91,
      taskVersion: 1,
      status: 'in_progress',
      assignedTo: 'admin',
      destination: 'Clinical review',
      priority: 'High',
      dueAt: '2026-07-30T16:00:00Z',
    }
    fetchMock
      .mockResolvedValueOnce(jsonResponse(queue))
      .mockResolvedValueOnce(
        jsonResponse({
          datasetId: queue.datasetId,
          datasetVersion: queue.datasetVersion,
          count: 1,
          assignees: [
            {
              staffId: 1,
              username: 'admin',
              displayName: 'Administrator, System',
              role: 'administrator',
            },
          ],
        }),
      )
      .mockResolvedValueOnce(
        jsonResponse({
          datasetId: queue.datasetId,
          datasetVersion: queue.datasetVersion,
          documentId: 91,
          documentKey: 'DOC-91',
          patientId: 'MOD-PAT-0001',
          legacyPid: 1,
          name: 'Advance directive',
          currentTaskVersion: 0,
          currentStatus: 'pending',
          eventCount: 0,
          returnedCount: 0,
          resultLimit: 100,
          events: [],
        }),
      )
      .mockResolvedValueOnce(jsonResponse(mutation))
      .mockResolvedValueOnce(
        jsonResponse({ ...mutation, taskVersion: 2, status: 'completed' }),
      )

    const loaded = await getPatientDocumentRoutingQueue('staff-session', {
      patientId: ' MOD-PAT-0001 ',
      status: 'active',
      priority: 'High',
      assignedTo: 'unassigned',
      minimumAgeHours: 24,
      query: ' directive ',
      offset: 0,
      limit: 10,
    })
    const assignees = await getPatientDocumentRoutingAssignees('staff-session')
    const history = await getPatientDocumentRoutingHistory('staff-session', 91)
    await routePatientDocument('staff-session', 91, {
      destination: 'Clinical review',
      priority: 'High',
      assignedTo: 'admin',
      reason: 'Route advance directive for review.',
      dueAt: '2026-07-30T16:00:00Z',
      expectedTaskVersion: 0,
    })
    await completePatientDocumentRouting('staff-session', 91, {
      reason: 'Clinical review handoff completed.',
      expectedTaskVersion: 1,
    })

    expect(loaded.counts.active).toBe(1)
    expect(assignees.assignees[0]?.username).toBe('admin')
    expect(history.currentTaskVersion).toBe(0)
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      'http://localhost:5001/api/documents/routing-queue?patientId=MOD-PAT-0001&status=active&priority=High&assignedTo=unassigned&minimumAgeHours=24&query=directive&offset=0&limit=10',
      'http://localhost:5001/api/documents/routing-assignees',
      'http://localhost:5001/api/documents/91/routing-history',
      'http://localhost:5001/api/documents/91/routing',
      'http://localhost:5001/api/documents/91/routing/complete',
    ])
    expect(fetchMock.mock.calls[3]?.[1]).toMatchObject({
      method: 'PUT',
      body: JSON.stringify({
        destination: 'Clinical review',
        priority: 'High',
        assignedTo: 'admin',
        reason: 'Route advance directive for review.',
        dueAt: '2026-07-30T16:00:00Z',
        expectedTaskVersion: 0,
      }),
    })
    expect(fetchMock.mock.calls[4]?.[1]).toMatchObject({
      method: 'POST',
      body: JSON.stringify({
        reason: 'Clinical review handoff completed.',
        expectedTaskVersion: 1,
      }),
    })
  })

  it('uses the backend from parameter for longitudinal encounter searches', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse({
        totalMatches: 1,
        encounters: [],
      }),
    )

    await searchEncounters('staff-session', {
      patientId: 'MOD-PAT-0901',
      fromDate: '1900-01-01',
      limit: 50,
    })

    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5001/api/encounters/?patientId=MOD-PAT-0901&from=1900-01-01&limit=50',
      expect.objectContaining({
        headers: { 'X-Legacy EHR-Session': 'staff-session' },
      }),
    )
  })

  it('adopts clinical-list delete and entered-in-error contracts', async () => {
    fetchMock
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(
        jsonResponse({ id: '41', detail: { immunizations: [] } }),
      )
      .mockResolvedValueOnce(new Response(null, { status: 204 }))

    await deleteProblem('staff-session', 'PROB-41')
    await deleteAllergy('staff-session', 'ALG-41')
    await deleteMedication('staff-session', 'MED-41')
    await markImmunizationEnteredInError(
      'staff-session',
      41,
      'Duplicate administration record',
    )
    await deleteImmunization('staff-session', 41)

    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      'http://localhost:5001/api/clinical-lists/problems/PROB-41',
      'http://localhost:5001/api/clinical-lists/allergies/ALG-41',
      'http://localhost:5001/api/clinical-lists/medications/MED-41',
      'http://localhost:5001/api/clinical-lists/immunizations/41/entered-in-error',
      'http://localhost:5001/api/clinical-lists/immunizations/41',
    ])
    expect(fetchMock.mock.calls.map(([, options]) => options?.method)).toEqual([
      'DELETE',
      'DELETE',
      'DELETE',
      'PUT',
      'DELETE',
    ])
    expect(fetchMock.mock.calls[3]?.[1]).toMatchObject({
      headers: { 'X-Legacy EHR-Session': 'staff-session' },
      body: JSON.stringify({ note: 'Duplicate administration record' }),
    })
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

  it('uses the PUT reply contract and unwraps the refreshed patient thread', async () => {
    const detail = {
      patientId: 'MOD-PAT-0004',
      patientDisplayName: 'Alex Morgan',
      portalEnabled: true,
      messages: [
        {
          id: 'MSG-1',
          title: 'Medication question',
          body: 'Original message\n\nReply from admin: Take with food.',
          status: 'new',
          assignedTo: 'admin',
          deleted: 0,
        },
      ],
    }
    fetchMock.mockResolvedValueOnce(jsonResponse({ id: 'MSG-1', detail }))

    const result = await replyToPatientMessage(
      'staff-session',
      'MSG-1',
      { body: 'Take with food.', assignedTo: 'admin' },
    )

    expect(result).toEqual(detail)
    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5001/api/messages/MSG-1/reply',
      expect.objectContaining({
        method: 'PUT',
        headers: {
          'X-Legacy EHR-Session': 'staff-session',
          'content-type': 'application/json',
        },
        body: JSON.stringify({
          body: 'Take with food.',
          assignedTo: 'admin',
        }),
      }),
    )
  })

  it('assigns a message through the mutation envelope and returns its refreshed thread', async () => {
    const detail = {
      patientId: 'MOD-PAT-0004',
      patientDisplayName: 'Alex Morgan',
      portalEnabled: true,
      messages: [
        {
          id: 'MSG-1',
          title: 'Medication question',
          status: 'new',
          assignedTo: 'admin',
          deleted: 0,
        },
      ],
    }
    fetchMock.mockResolvedValueOnce(jsonResponse({ id: 'MSG-1', detail }))

    const result = await updatePatientMessageAssignment(
      'staff-session',
      'MSG-1',
      'admin',
    )

    expect(result).toEqual(detail)
    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5001/api/messages/MSG-1/assignment',
      expect.objectContaining({
        method: 'PUT',
        body: JSON.stringify({ assignedTo: 'admin' }),
      }),
    )
  })

  it('unwraps create and status mutation envelopes consistently', async () => {
    const detail = {
      patientId: 'MOD-PAT-0004',
      patientDisplayName: 'Alex Morgan',
      portalEnabled: true,
      messages: [
        {
          id: 'MSG-2',
          title: 'Follow-up',
          status: 'new',
          assignedTo: 'admin',
          deleted: 0,
        },
      ],
    }
    fetchMock
      .mockResolvedValueOnce(jsonResponse({ id: 'MSG-2', detail }))
      .mockResolvedValueOnce(jsonResponse({ id: 'MSG-2', detail }))

    const created = await createPatientMessage('staff-session', {
      patientId: 'MOD-PAT-0004',
      title: 'Follow-up',
      body: 'Please review.',
      assignedTo: 'admin',
    })
    const updated = await updatePatientMessageStatus(
      'staff-session',
      'MSG-2',
      { status: 'done', body: 'Reviewed.' },
    )

    expect(created).toEqual(detail)
    expect(updated).toEqual(detail)
    expect(fetchMock.mock.calls).toEqual([
      [
        'http://localhost:5001/api/messages',
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({
            patientId: 'MOD-PAT-0004',
            title: 'Follow-up',
            body: 'Please review.',
            assignedTo: 'admin',
          }),
        }),
      ],
      [
        'http://localhost:5001/api/messages/MSG-2/status',
        expect.objectContaining({
          method: 'PUT',
          body: JSON.stringify({ status: 'done', body: 'Reviewed.' }),
        }),
      ],
    ])
  })

  it('sends the complete protected procedure queue filter contract', async () => {
    fetchMock
      .mockResolvedValueOnce(
        jsonResponse({
          datasetId: 'test',
          datasetVersion: 'v1',
          statusFilter: 'all',
          limit: 100,
          totalReports: 1,
          reviewedReports: 0,
          unreviewedReports: 1,
          reports: [{ reportId: 6001 }],
        }),
      )
      .mockResolvedValueOnce(
        jsonResponse({
          datasetId: 'test',
          datasetVersion: 'v1',
          statusFilter: 'ready-to-send',
          limit: 100,
          totalOrders: 1,
          readyToSendOrders: 1,
          transmittedPendingOrders: 0,
          reportedOrders: 0,
          scheduledOrders: 1,
          completedOrders: 0,
          orders: [{ orderId: 5001, queueState: 'ready-to-send' }],
        }),
      )

    const filters = {
      status: 'all',
      patientId: 'MOD-PAT-0004',
      providerId: 101,
      labId: 501,
      fromDate: '2026-07-01',
      toDate: '2026-07-27',
      limit: 100,
    }
    const reports = await getProcedureReportQueue(
      'staff-session',
      filters,
    )
    const orders = await getProcedureOrderQueue('staff-session', {
      ...filters,
      status: 'ready-to-send',
    })

    expect(reports.reports[0]).toMatchObject({ reportId: 6001 })
    expect(orders.orders[0]).toMatchObject({
      orderId: 5001,
      queueState: 'ready-to-send',
    })
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      'http://localhost:5001/api/procedures/report-review-queue?status=all&patientId=MOD-PAT-0004&providerId=101&labId=501&fromDate=2026-07-01&toDate=2026-07-27&limit=100',
      'http://localhost:5001/api/procedures/order-queue?status=ready-to-send&patientId=MOD-PAT-0004&providerId=101&labId=501&fromDate=2026-07-01&toDate=2026-07-27&limit=100',
    ])
  })

  it('uses the protected assign, sign, reopen, and bulk-sign report contracts', async () => {
    const detail = {
      patientId: 'MOD-PAT-0004',
      patientDisplayName: 'Alex Morgan',
      counts: { orders: 1, reports: 1, results: 0, finalResults: 0 },
      orders: [],
    }
    fetchMock
      .mockResolvedValueOnce(jsonResponse({ id: 6001, detail }))
      .mockResolvedValueOnce(jsonResponse({ id: 6001, detail }))
      .mockResolvedValueOnce(jsonResponse({ id: 6001, detail }))
      .mockResolvedValueOnce(
        jsonResponse({
          requestedCount: 2,
          signedCount: 2,
          signedReportIds: [6001, 6002],
          reviewedBy: 'admin',
          reviewedAt: '2026-07-27 23:45',
        }),
      )

    await assignLabReportReviewer('staff-session', 6001, {
      assignedTo: 'admin',
      assignedAt: '2026-07-27T23:44:00Z',
    })
    await signLabReport('staff-session', 6001, {
      reviewedBy: 'admin',
      reviewedAt: '2026-07-27T23:45:00Z',
    })
    await reopenLabReportReview('staff-session', 6001)
    const bulk = await bulkSignLabReports('staff-session', {
      reportIds: [6001, 6002],
      reviewedBy: 'admin',
      reviewedAt: '2026-07-27T23:45:00Z',
    })

    expect(bulk).toMatchObject({ requestedCount: 2, signedCount: 2 })
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      'http://localhost:5001/api/procedures/reports/6001/review-assignment',
      'http://localhost:5001/api/procedures/reports/6001/sign',
      'http://localhost:5001/api/procedures/reports/6001/reopen-review',
      'http://localhost:5001/api/procedures/reports/bulk-sign',
    ])
    expect(fetchMock.mock.calls.map(([, options]) => options?.method)).toEqual([
      'PUT',
      'PUT',
      'PUT',
      'PUT',
    ])
    expect(fetchMock.mock.calls[2]?.[1]).toMatchObject({
      body: undefined,
    })
  })

  it('uses the protected patient relationship and care-team contracts', async () => {
    const responseBody = { canonicalId: 'MOD-PAT-0004' }
    fetchMock
      .mockResolvedValueOnce(jsonResponse({ providers: [] }))
      .mockResolvedValueOnce(
        jsonResponse({ patientId: 'MOD-PAT-0004', eventCount: 0, events: [] }),
      )
      .mockResolvedValueOnce(
        jsonResponse({ patientId: 'MOD-PAT-0004', eventCount: 0, events: [] }),
      )
      .mockResolvedValueOnce(jsonResponse({ providers: [], contacts: [] }))
      .mockResolvedValueOnce(jsonResponse(responseBody))
      .mockResolvedValueOnce(jsonResponse(responseBody))
      .mockResolvedValueOnce(jsonResponse(responseBody))
      .mockResolvedValueOnce(jsonResponse(responseBody))

    await getPatientProviderAssignmentOptions('staff-session')
    await getPatientProviderAssignmentHistory(
      'staff-session',
      'MOD-PAT-0004',
    )
    await getPatientAdministrationHistory('staff-session', 'MOD-PAT-0004')
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
      reason: 'Care team reassignment',
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
      'http://localhost:5001/api/patients/MOD-PAT-0004/provider-assignment-history',
      'http://localhost:5001/api/patients/MOD-PAT-0004/administration-history',
      'http://localhost:5001/api/patients/MOD-PAT-0004/care-team-options',
      'http://localhost:5001/api/patients/MOD-PAT-0004/guardian-contact',
      'http://localhost:5001/api/patients/MOD-PAT-0004/employer',
      'http://localhost:5001/api/patients/MOD-PAT-0004/provider-assignment',
      'http://localhost:5001/api/patients/MOD-PAT-0004/care-team',
    ])
    for (const call of fetchMock.mock.calls.slice(4)) {
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
    expect(fetchMock.mock.calls[6]?.[1]).toMatchObject({
      body: JSON.stringify({
        providerId: 7,
        reason: 'Care team reassignment',
      }),
    })
  })

  it('encodes the complete protected patient duplicate search contract', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse({
        datasetId: 'legacy-ehr-shared-synthetic-v1',
        datasetVersion: '2026.07',
        limit: 10,
        totalCandidates: 1,
        candidates: [{ canonicalId: 'MOD-PAT-0004', matchScore: 100 }],
      }),
    )

    const result = await findPatientDuplicateCandidates('staff-session', {
      firstName: ' Nora ',
      lastName: ' Kim ',
      dateOfBirth: '2002-05-05',
      phone: ' (619) 555-1004 ',
      email: ' mod-pat-0004@example.test ',
      excludePatientId: ' MOD-PAT-9999 ',
      limit: 10,
    })

    expect(result.candidates[0]).toMatchObject({
      canonicalId: 'MOD-PAT-0004',
      matchScore: 100,
    })
    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5001/api/patients/duplicates?firstName=Nora&lastName=Kim&dateOfBirth=2002-05-05&phone=%28619%29+555-1004&email=mod-pat-0004%40example.test&excludePatientId=MOD-PAT-9999&limit=10',
      expect.objectContaining({
        headers: { 'X-Legacy EHR-Session': 'staff-session' },
      }),
    )
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

  it('limits the generic stock mutation contract to named consumption', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(
        {
          transaction: { transactionId: 'transaction-1' },
          lot: { lotId: 20001, quantityOnHand: 4 },
        },
        201,
      ),
    )

    const result = await createInventoryTransaction('staff-session', {
      lotId: 20001,
      transactionType: 'consumption',
      quantity: 2,
      reason: 'Used by clinic operations',
    })

    expect(result.transaction.transactionId).toBe('transaction-1')
    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5001/api/inventory/transactions',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({
          lotId: 20001,
          transactionType: 'consumption',
          quantity: 2,
          reason: 'Used by clinic operations',
        }),
      }),
    )
  })

  it('records a protected facility transfer with an explicit reason', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(
        {
          transferId: 'transfer-1',
          transaction: { transactionId: 'transaction-1' },
        },
        201,
      ),
    )

    const result = await createInventoryTransfer('staff-session', {
      sourceLotId: 20001,
      destinationFacilityId: 13,
      quantity: 2,
      reason: 'Move stock to the north clinic',
    })

    expect(result.transferId).toBe('transfer-1')
    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5001/api/inventory/transfers',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({
          sourceLotId: 20001,
          destinationFacilityId: 13,
          quantity: 2,
          reason: 'Move stock to the north clinic',
        }),
      }),
    )
  })

  it('records a protected physical-count reconciliation', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(
        {
          reconciliationId: 'reconciliation-1',
          expectedQuantity: 8,
          countedQuantity: 7,
          quantityDelta: -1,
        },
        201,
      ),
    )

    const result = await createInventoryCountReconciliation('staff-session', {
      lotId: 20001,
      countedQuantity: 7,
      notes: 'Two-person shelf count',
    })

    expect(result.quantityDelta).toBe(-1)
    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5001/api/inventory/count-reconciliations',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({
          lotId: 20001,
          countedQuantity: 7,
          notes: 'Two-person shelf count',
        }),
      }),
    )
  })

  it('records witnessed destruction and expired-lot dispositions through distinct contracts', async () => {
    fetchMock
      .mockResolvedValueOnce(
        jsonResponse(
          {
            destructionId: 'destruction-1',
            quantityAffected: 5,
            transaction: { transactionId: 'transaction-1' },
          },
          201,
        ),
      )
      .mockResolvedValueOnce(
        jsonResponse(
          {
            dispositionId: 'disposition-1',
            disposition: 'destroy',
            quantityAffected: 3,
            destructionId: 'destruction-2',
            transaction: { transactionId: 'transaction-2' },
          },
          201,
        ),
      )

    const destruction = await createInventoryLotDestruction(
      'staff-session',
      20001,
      {
        destructionDate: '2026-07-27',
        method: 'Approved waste service',
        witness: 'Second staff member',
        notes: 'Damaged stock',
      },
    )
    const expiry = await createInventoryExpiryDisposition(
      'staff-session',
      20002,
      {
        disposition: 'destroy',
        method: 'Approved waste service',
        witness: 'Second staff member',
        notes: 'Expired stock',
      },
    )

    expect(destruction.transaction.transactionId).toBe('transaction-1')
    expect(expiry.destructionId).toBe('destruction-2')
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      'http://localhost:5001/api/inventory/lots/20001/destructions',
      'http://localhost:5001/api/inventory/lots/20002/expiry-dispositions',
    ])
    expect(fetchMock.mock.calls[0]?.[1]?.body).toBe(
      JSON.stringify({
        destructionDate: '2026-07-27',
        method: 'Approved waste service',
        witness: 'Second staff member',
        notes: 'Damaged stock',
      }),
    )
    expect(fetchMock.mock.calls[1]?.[1]?.body).toBe(
      JSON.stringify({
        disposition: 'destroy',
        method: 'Approved waste service',
        witness: 'Second staff member',
        notes: 'Expired stock',
      }),
    )
  })

  it('records direct, FEFO, and prescription-linked patient dispensing through distinct contracts', async () => {
    fetchMock
      .mockResolvedValueOnce(
        jsonResponse({ saleId: 'sale-1', quantity: 2 }, 201),
      )
      .mockResolvedValueOnce(
        jsonResponse({ saleBatchId: 'batch-1', quantity: 5 }, 201),
      )
      .mockResolvedValueOnce(
        jsonResponse({ prescriptionId: 'rx-1', rxNormCode: '860975' }, 201),
      )

    const direct = await createInventoryPatientSale('staff-session', {
      lotId: 20001,
      patientId: 'MOD-PAT-0001',
      encounter: 90001,
      saleDate: '2026-07-27',
      quantity: 2,
      fee: 12.5,
      notes: 'Dispensed after visit',
    })
    const allocated = await allocateInventoryPatientSale('staff-session', {
      itemId: 10001,
      patientId: 'MOD-PAT-0001',
      encounter: 90001,
      saleDate: '2026-07-27',
      quantity: 5,
      fee: 20,
      notes: 'Allocate earliest-expiring lots first',
    })
    const prescription = await dispenseInventoryPrescription('staff-session', {
      prescriptionId: 'rx-1',
      saleDate: '2026-07-27',
      quantity: 1,
      fee: 8.25,
      notes: 'Prescription-linked dispense',
    })

    expect(direct.saleId).toBe('sale-1')
    expect(allocated.saleBatchId).toBe('batch-1')
    expect(prescription.rxNormCode).toBe('860975')
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      'http://localhost:5001/api/inventory/patient-sales',
      'http://localhost:5001/api/inventory/patient-sales/allocate',
      'http://localhost:5001/api/inventory/prescription-dispensations',
    ])
    expect(fetchMock.mock.calls.map(([, init]) => init?.body)).toEqual([
      JSON.stringify({
        lotId: 20001,
        patientId: 'MOD-PAT-0001',
        encounter: 90001,
        saleDate: '2026-07-27',
        quantity: 2,
        fee: 12.5,
        notes: 'Dispensed after visit',
      }),
      JSON.stringify({
        itemId: 10001,
        patientId: 'MOD-PAT-0001',
        encounter: 90001,
        saleDate: '2026-07-27',
        quantity: 5,
        fee: 20,
        notes: 'Allocate earliest-expiring lots first',
      }),
      JSON.stringify({
        prescriptionId: 'rx-1',
        saleDate: '2026-07-27',
        quantity: 1,
        fee: 8.25,
        notes: 'Prescription-linked dispense',
      }),
    ])
  })

  it('loads and exports the same filtered inventory activity contract', async () => {
    fetchMock
      .mockResolvedValueOnce(
        jsonResponse({
          datasetId: 'legacy-ehr-modernized-gold',
          datasetVersion: '2026.07',
          fromDate: '2026-07-01',
          toDate: '2026-07-27',
          facilityId: 13,
          totalEntries: 1,
          entries: [{ transactionId: 'transaction-1' }],
        }),
      )
      .mockResolvedValueOnce(
        new Response('Occurred At,Item Code\n2026-07-27,MED-1', {
          status: 200,
          headers: { 'content-type': 'text/csv' },
        }),
      )

    const filters = {
      from: '2026-07-01',
      to: '2026-07-27',
      facilityId: 13,
    }
    const report = await getInventoryActivityReport('staff-session', filters)
    const csv = await downloadInventoryActivityCsv('staff-session', filters)

    expect(report).toMatchObject({
      datasetId: 'legacy-ehr-modernized-gold',
      datasetVersion: '2026.07',
      totalEntries: 1,
    })
    await expect(csv.text()).resolves.toContain('Occurred At,Item Code')
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      'http://localhost:5001/api/inventory/activity?from=2026-07-01&to=2026-07-27&facilityId=13',
      'http://localhost:5001/api/inventory/activity/export?from=2026-07-01&to=2026-07-27&facilityId=13',
    ])
    expect(fetchMock.mock.calls[1]?.[1]).toMatchObject({
      headers: { 'X-Legacy EHR-Session': 'staff-session' },
    })
  })

  it('loads the local medication catalog and updates an inventory RXCUI link', async () => {
    fetchMock
      .mockResolvedValueOnce(
        jsonResponse([
          {
            rxNormCode: '860975',
            drugName: 'Metformin',
            displayName: 'Metformin 500 MG Oral Tablet',
            form: 'tablet',
            strength: '500 mg',
            route: 'oral',
          },
        ]),
      )
      .mockResolvedValueOnce(
        jsonResponse({
          itemId: 10004,
          rxNormCode: '860975',
          drugName: 'Metformin',
          displayName: 'Metformin 500 MG Oral Tablet',
          linkedBy: 'admin',
          linkedAt: '2026-07-27T22:15:00Z',
        }),
      )

    const catalog = await getInventoryMedicationCatalog('staff-session')
    const link = await updateInventoryMedicationLink(
      'staff-session',
      10004,
      '860975',
    )

    expect(catalog[0]).toMatchObject({
      rxNormCode: '860975',
      strength: '500 mg',
    })
    expect(link).toMatchObject({
      itemId: 10004,
      rxNormCode: '860975',
      linkedBy: 'admin',
    })
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      'http://localhost:5001/api/inventory/medication-catalog',
      'http://localhost:5001/api/inventory/items/10004/medication-link',
    ])
    expect(fetchMock.mock.calls[1]?.[1]).toMatchObject({
      method: 'PUT',
      body: JSON.stringify({ rxNormCode: '860975' }),
    })
  })

  it('uses the protected prescription refill, request approval, and audit contracts', async () => {
    fetchMock
      .mockResolvedValueOnce(
        jsonResponse({ id: 'rx-1', detail: { prescriptions: [] } }),
      )
      .mockResolvedValueOnce(
        jsonResponse({ id: 'rx-1', detail: { prescriptions: [] } }),
      )
      .mockResolvedValueOnce(
        jsonResponse({
          prescriptionId: 'rx-1',
          eventCount: 2,
          events: [
            { eventId: 'event-1', action: 'create' },
            { eventId: 'event-2', action: 'refill-request-approved' },
          ],
        }),
      )

    const directRefill = {
      refillDate: '2026-07-28',
      additionalRefills: 2,
      note: 'Authorized after chart review',
    }
    const requestApproval = {
      refillDate: '2026-07-28',
      additionalRefills: 1,
      note: 'Portal request approved',
    }

    await refillPrescription(
      'staff-session',
      'rx with spaces',
      directRefill,
    )
    await approvePrescriptionRefillRequest(
      'staff-session',
      445,
      requestApproval,
    )
    const audit = await getPrescriptionAuditHistory(
      'staff-session',
      'rx with spaces',
    )

    expect(audit.events.map((event) => event.action)).toEqual([
      'create',
      'refill-request-approved',
    ])
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      'http://localhost:5001/api/clinical-lists/prescriptions/rx%20with%20spaces/refill',
      'http://localhost:5001/api/clinical-lists/prescription-refill-requests/445/approve',
      'http://localhost:5001/api/clinical-lists/prescriptions/rx%20with%20spaces/audit-history',
    ])
    expect(fetchMock.mock.calls[0]?.[1]).toMatchObject({
      method: 'PUT',
      headers: {
        'X-Legacy EHR-Session': 'staff-session',
        'content-type': 'application/json',
      },
      body: JSON.stringify(directRefill),
    })
    expect(fetchMock.mock.calls[1]?.[1]).toMatchObject({
      method: 'PUT',
      body: JSON.stringify(requestApproval),
    })
  })

  it('filters the global refill queue and sends explicit lifecycle decisions', async () => {
    fetchMock
      .mockResolvedValueOnce(
        jsonResponse({
          statusFilter: 'clarification-requested',
          totalMatches: 1,
          counts: {
            pending: 0,
            clarificationRequested: 1,
            approved: 0,
            denied: 0,
            completed: 0,
            total: 1,
          },
          requests: [
            {
              messageId: 445,
              status: 'clarification-requested',
              staffResponse: 'Please confirm the pharmacy.',
            },
          ],
        }),
      )
      .mockResolvedValueOnce(
        jsonResponse({
          messageId: 445,
          prescriptionId: 'RX-445',
          status: 'denied',
          staffResponse: 'A visit is required first.',
        }),
      )

    const queue = await getPrescriptionRefillQueue('staff-session', {
      status: 'clarification-requested',
      patient: 'Ada Lovelace',
      limit: 25,
      offset: 50,
    })
    const decision = await decidePrescriptionRefillRequest(
      'staff-session',
      445,
      {
        action: 'deny',
        response: 'A visit is required first.',
      },
    )

    expect(queue.counts.clarificationRequested).toBe(1)
    expect(decision.status).toBe('denied')
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      'http://localhost:5001/api/clinical-lists/prescription-refill-requests?status=clarification-requested&patient=Ada+Lovelace&limit=25&offset=50',
      'http://localhost:5001/api/clinical-lists/prescription-refill-requests/445/decision',
    ])
    expect(fetchMock.mock.calls[0]?.[1]).toMatchObject({
      headers: { 'X-Legacy EHR-Session': 'staff-session' },
    })
    expect(fetchMock.mock.calls[1]?.[1]).toMatchObject({
      method: 'PUT',
      body: JSON.stringify({
        action: 'deny',
        response: 'A visit is required first.',
      }),
    })
  })

  it('reads the local pharmacy directory and records local prescription route evidence', async () => {
    fetchMock
      .mockResolvedValueOnce(
        jsonResponse({
          datasetId: 'legacy-ehr-modernization-gold',
          datasetVersion: 'v1',
          pharmacyCount: 1,
          pharmacies: [
            {
              id: 9001,
              name: 'Northstar Community Pharmacy',
              transmitMethod: 1,
              ncpdp: 1234567,
            },
          ],
        }),
      )
      .mockResolvedValueOnce(
        jsonResponse({
          id: 'rx id',
          routed: true,
          failureReason: null,
          detail: { prescriptions: [] },
        }),
      )

    const directory = await getClinicalPharmacyDirectory('staff-session')
    const route = {
      pharmacyId: directory.pharmacies[0]!.id,
      sentAt: '2026-07-28T12:30:00.000Z',
      note: 'Browser-verified local route',
    }
    const result = await routePrescriptionToPharmacy(
      'staff-session',
      'rx id',
      route,
    )

    expect(directory).toMatchObject({
      pharmacyCount: 1,
      pharmacies: [{ name: 'Northstar Community Pharmacy' }],
    })
    expect(result.routed).toBe(true)
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      'http://localhost:5001/api/clinical-lists/pharmacies',
      'http://localhost:5001/api/clinical-lists/prescriptions/rx%20id/route-pharmacy',
    ])
    expect(fetchMock.mock.calls[0]?.[1]).toMatchObject({
      headers: { 'X-Legacy EHR-Session': 'staff-session' },
    })
    expect(fetchMock.mock.calls[1]?.[1]).toMatchObject({
      method: 'PUT',
      headers: {
        'X-Legacy EHR-Session': 'staff-session',
        'content-type': 'application/json',
      },
      body: JSON.stringify(route),
    })
  })

  it('sends the loaded prescription version with structured edit fields', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse({ id: 'rx id', detail: { prescriptions: [] } }),
    )
    const update = {
      expectedVersion: '48190',
      startDate: '2026-07-28',
      dosage: '1 tablet twice daily',
      quantity: '60',
      doseAmount: 1,
      doseUnit: 'tablet',
      frequency: 'twice daily',
      durationDays: 30,
      route: 'oral',
      refills: 2,
      diagnosis: 'E11.9',
      note: 'Take with food',
      editReason: 'Dose instructions clarified after chart review',
    }

    await updatePrescription('staff-session', 'rx id', update)

    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5001/api/clinical-lists/prescriptions/rx%20id',
      expect.objectContaining({
        method: 'PUT',
        headers: {
          'X-Legacy EHR-Session': 'staff-session',
          'content-type': 'application/json',
        },
        body: JSON.stringify(update),
      }),
    )
  })

  it('searches the local medication vocabulary and creates a structured prescription', async () => {
    fetchMock
      .mockResolvedValueOnce(
        jsonResponse([
          {
            rxNormCode: '860975',
            drugName: 'Metformin',
            displayName: 'Metformin 500 MG Oral Tablet',
            form: 'tablet',
            strength: '500 mg',
            route: 'oral',
            doseAmount: 1,
            doseUnit: 'tablet',
            frequency: 'twice daily',
            durationDays: 30,
          },
        ]),
      )
      .mockResolvedValueOnce(
        jsonResponse({ id: 'rx-1', detail: { prescriptions: [] } }, 201),
      )

    const catalog = await searchClinicalMedicationVocabulary(
      'staff-session',
      'metformin 500',
    )
    const prescription = {
      patientId: 'MOD-PAT-0004',
      startDate: '2026-07-28',
      drug: catalog[0]!.displayName,
      rxNormCode: catalog[0]!.rxNormCode,
      dosage: '1 tablet twice daily',
      quantity: '60',
      doseAmount: 1,
      doseUnit: 'tablet',
      frequency: 'twice daily',
      durationDays: 30,
      route: 'oral',
      refills: 1,
      note: 'Catalog-backed local prescription',
      diagnosis: 'E11.9',
    }
    await createPrescription('staff-session', prescription)

    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      'http://localhost:5001/api/clinical-lists/medication-vocabulary?query=metformin+500',
      'http://localhost:5001/api/clinical-lists/prescriptions',
    ])
    expect(fetchMock.mock.calls[1]?.[1]).toMatchObject({
      method: 'POST',
      headers: {
        'X-Legacy EHR-Session': 'staff-session',
        'content-type': 'application/json',
      },
      body: JSON.stringify(prescription),
    })
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
