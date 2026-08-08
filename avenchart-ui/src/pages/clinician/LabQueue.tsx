// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import {
  useEffect,
  useEffectEvent,
  useMemo,
  useState,
  type FormEvent,
} from 'react'
import { useNavigate, useOutletContext, useSearchParams } from 'react-router-dom'
import { CheckCircle, History, RefreshCw, UserCheck } from 'lucide-react'
import {
  assignLabReportReviewer,
  acknowledgeCriticalLabResult,
  bulkSignLabReports,
  getCriticalLabResultQueue,
  denyLabReportReview,
  getLabReportReviewHistory,
  getProcedureOrderQueue,
  getProcedureReportQueue,
  isRequestCancellation,
  reopenLabReportReview,
  signLabReport,
  type ProcedureOrderQueueResponse,
  type ProcedureQueueFilters,
  type ProcedureReportQueueItem,
  type ProcedureReportQueueResponse,
  type ProcedureReportReviewHistoryResponse,
  type CriticalLabResultQueueResponse,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

type AsyncState<T> =
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; message: string }

type FilterDraft = {
  reportStatus: string
  orderStatus: string
  patientId: string
  providerId: string
  labId: string
  fromDate: string
  toDate: string
}

function filtersFromParams(params: URLSearchParams): FilterDraft {
  const rawReportStatus =
    params.get('reportStatus') ?? params.get('status') ?? 'unreviewed'
  const reportStatus =
    rawReportStatus === 'reviewed' || rawReportStatus === 'all'
      ? rawReportStatus
      : 'unreviewed'
  const rawOrderStatus = params.get('orderStatus') ?? 'ready-to-send'
  const orderStatus = [
    'ready-to-send',
    'transmitted-pending',
    'reported',
    'scheduled',
    'completed',
    'all',
  ].includes(rawOrderStatus)
    ? rawOrderStatus
    : 'ready-to-send'
  return {
    reportStatus,
    orderStatus,
    patientId: params.get('patientId') ?? '',
    providerId: params.get('providerId') ?? '',
    labId: params.get('labId') ?? '',
    fromDate: params.get('fromDate') ?? '',
    toDate: params.get('toDate') ?? '',
  }
}

function queueFilters(
  draft: FilterDraft,
  queue: 'reports' | 'orders',
): ProcedureQueueFilters {
  const providerId = Number(draft.providerId)
  const labId = Number(draft.labId)
  return {
    status: queue === 'reports' ? draft.reportStatus : draft.orderStatus,
    patientId: draft.patientId.trim() || undefined,
    providerId:
      draft.providerId && Number.isInteger(providerId) ? providerId : undefined,
    labId: draft.labId && Number.isInteger(labId) ? labId : undefined,
    fromDate: draft.fromDate || undefined,
    toDate: draft.toDate || undefined,
    limit: 100,
  }
}

function formatDateTime(value?: string | null) {
  if (!value) return '—'
  const parsed = new Date(value)
  return Number.isNaN(parsed.valueOf()) ? value : parsed.toLocaleString()
}

function reviewStatusClass(status?: string | null) {
  if (status?.toLowerCase() === 'reviewed') return 'cl-badge-green'
  if (status?.toLowerCase() === 'assigned') return 'cl-badge-blue'
  if (status?.toLowerCase() === 'denied') return 'cl-badge-red'
  return 'cl-badge-amber'
}

function hasArrayProperty(value: unknown, property: string) {
  return (
    typeof value === 'object' &&
    value !== null &&
    Array.isArray((value as Record<string, unknown>)[property])
  )
}

export default function LabQueue() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const queryKey = searchParams.toString()
  const activeFilters = useMemo(
    () => filtersFromParams(new URLSearchParams(queryKey)),
    [queryKey],
  )
  const [draft, setDraft] = useState<FilterDraft>(() =>
    filtersFromParams(searchParams),
  )
  const [tab, setTab] = useState<'reports' | 'orders'>(() =>
    searchParams.get('tab') === 'orders' ? 'orders' : 'reports',
  )
  const [reportState, setReportState] = useState<
    AsyncState<ProcedureReportQueueResponse>
  >({ status: 'loading' })
  const [orderState, setOrderState] = useState<
    AsyncState<ProcedureOrderQueueResponse>
  >({ status: 'loading' })
  const [reload, setReload] = useState(0)
  const [actionId, setActionId] = useState<number | null>(null)
  const [actionError, setActionError] = useState<{
    id: number
    message: string
  } | null>(null)
  const [selectedReports, setSelectedReports] = useState<Set<number>>(
    () => new Set(),
  )
  const [bulkSigning, setBulkSigning] = useState(false)
  const [criticalResults, setCriticalResults] =
    useState<CriticalLabResultQueueResponse | null>(null)
  const [criticalResultsError, setCriticalResultsError] = useState<
    string | null
  >(null)
  const [reviewHistoryByReport, setReviewHistoryByReport] = useState<
    Record<number, ProcedureReportReviewHistoryResponse>
  >({})
  const [historyLoadingId, setHistoryLoadingId] = useState<number | null>(null)

  async function acknowledgeCritical(
    result: NonNullable<typeof criticalResults>['results'][number],
  ) {
    const reason = requestReviewReason('Acknowledge this critical result', '')
    if (!reason) return
    try {
      await acknowledgeCriticalLabResult(session.sessionId, result.resultId, {
        expectedVersion: result.acknowledgementVersion,
        reason,
      })
      setReload((value) => value + 1)
      showToast('Critical result acknowledged locally.', 'success')
    } catch (error) {
      showToast(
        error instanceof Error
          ? error.message
          : 'Critical acknowledgement failed.',
        'error',
      )
    }
  }

  function requestReviewReason(action: string, suggestedReason: string) {
    const reason = window.prompt(`${action} reason (1–500 characters):`, suggestedReason)
    const normalized = reason?.trim() ?? ''
    return normalized.length > 0 && normalized.length <= 500 ? normalized : null
  }

  function loadReports(signal?: AbortSignal) {
    setReportState({ status: 'loading' })
    getProcedureReportQueue(
      session.sessionId,
      queueFilters(activeFilters, 'reports'),
      signal,
    )
      .then((data) => {
        if (!hasArrayProperty(data, 'reports')) {
          throw new Error(
            'The lab report queue response was incomplete. Retry the page.',
          )
        }
        setReportState({ status: 'ready', data })
      })
      .catch((error: unknown) => {
        if (isRequestCancellation(error)) return
        setReportState({
          status: 'error',
          message:
            error instanceof Error
              ? error.message
              : 'Could not load the report review queue.',
        })
      })
  }

  function loadOrders(signal?: AbortSignal) {
    setOrderState({ status: 'loading' })
    getProcedureOrderQueue(
      session.sessionId,
      queueFilters(activeFilters, 'orders'),
      signal,
    )
      .then((data) => {
        if (!hasArrayProperty(data, 'orders')) {
          throw new Error(
            'The procedure order queue response was incomplete. Retry the page.',
          )
        }
        setOrderState({ status: 'ready', data })
      })
      .catch((error: unknown) => {
        if (isRequestCancellation(error)) return
        setOrderState({
          status: 'error',
          message:
            error instanceof Error
              ? error.message
              : 'Could not load the procedure order queue.',
        })
      })
  }

  const loadReportsForCriteria = useEffectEvent(loadReports)
  const loadOrdersForCriteria = useEffectEvent(loadOrders)
  useEffect(() => {
    const controller = new AbortController()
    setCriticalResults(null)
    setCriticalResultsError(null)
    void loadReportsForCriteria(controller.signal)
    void loadOrdersForCriteria(controller.signal)
    void getCriticalLabResultQueue(session.sessionId, controller.signal)
      .then((data) => {
        if (!hasArrayProperty(data, 'results')) {
          throw new Error('The critical-result queue response was incomplete.')
        }
        setCriticalResults(data)
      })
      .catch((error: unknown) => {
        if (isRequestCancellation(error)) return
        setCriticalResults(null)
        setCriticalResultsError(
          'The critical-result acknowledgement queue is unavailable. Retry the page before relying on this queue.',
        )
      })
    return () => controller.abort()
  }, [queryKey, reload, session.sessionId])

  function applyFilters(event: FormEvent) {
    event.preventDefault()
    const params = new URLSearchParams()
    Object.entries(draft).forEach(([key, value]) => {
      if (!value) return
      params.set(key, value)
    })
    if (tab === 'orders') params.set('tab', 'orders')
    setSearchParams(params)
    setSelectedReports(new Set())
  }

  function clearFilters() {
    const next = filtersFromParams(new URLSearchParams())
    setDraft(next)
    setSearchParams(tab === 'orders' ? { tab: 'orders' } : {})
    setSelectedReports(new Set())
  }

  async function runReportAction(
    reportId: number,
    successMessage: string,
    action: () => Promise<unknown>,
  ) {
    setActionId(reportId)
    setActionError(null)
    try {
      await action()
      setSelectedReports((current) => {
        const next = new Set(current)
        next.delete(reportId)
        return next
      })
      setReload((value) => value + 1)
      showToast(successMessage, 'success')
    } catch (error) {
      const message =
        error instanceof Error ? error.message : 'The review action failed.'
      setActionError({ id: reportId, message })
      showToast(message, 'error')
    } finally {
      setActionId(null)
    }
  }

  function claimReport(report: ProcedureReportQueueItem) {
    const reason = requestReviewReason('Assign this report to yourself', 'Claimed for local review')
    if (!reason) return Promise.resolve()
    return runReportAction(
      report.reportId,
      'Report assigned to you.',
      () =>
        assignLabReportReviewer(session.sessionId, report.reportId, {
          assignedTo: session.username,
          expectedReviewVersion: report.reviewVersion,
          reason,
        }),
    )
  }

  function signReport(report: ProcedureReportQueueItem) {
    if (
      !window.confirm(
        `Sign ${report.procedureName ?? `report ${report.reportId}`} as reviewed?`,
      )
    ) {
      return Promise.resolve()
    }
    const reason = requestReviewReason('Sign this report as reviewed', 'Reviewed local report')
    if (!reason) return Promise.resolve()
    return runReportAction(
      report.reportId,
      'Report signed as reviewed.',
      () =>
        signLabReport(session.sessionId, report.reportId, {
          expectedReviewVersion: report.reviewVersion,
          reason,
        }),
    )
  }

  function reopenReport(report: ProcedureReportQueueItem) {
    if (
      !window.confirm(
        `Reopen review for ${report.procedureName ?? `report ${report.reportId}`}?`,
      )
    ) {
      return Promise.resolve()
    }
    const reason = requestReviewReason('Reopen this report review', 'Reopened for local review')
    if (!reason) return Promise.resolve()
    return runReportAction(
      report.reportId,
      'Report review reopened.',
      () =>
        reopenLabReportReview(
          session.sessionId,
          report.reportId,
          { expectedReviewVersion: report.reviewVersion, reason },
        ),
    )
  }

  function denyReport(report: ProcedureReportQueueItem) {
    const reason = requestReviewReason('Deny this report review', '')
    if (!reason) return Promise.resolve()
    return runReportAction(
      report.reportId,
      'Report review denied locally.',
      () =>
        denyLabReportReview(session.sessionId, report.reportId, {
          expectedReviewVersion: report.reviewVersion,
          reason,
        }),
    )
  }

  async function toggleReviewHistory(report: ProcedureReportQueueItem) {
    if (reviewHistoryByReport[report.reportId]) {
      setReviewHistoryByReport((current) => {
        const next = { ...current }
        delete next[report.reportId]
        return next
      })
      return
    }
    setHistoryLoadingId(report.reportId)
    try {
      const history = await getLabReportReviewHistory(session.sessionId, report.reportId)
      setReviewHistoryByReport((current) => ({ ...current, [report.reportId]: history }))
    } catch (error) {
      setActionError({
        id: report.reportId,
        message: error instanceof Error ? error.message : 'Could not load review history.',
      })
    } finally {
      setHistoryLoadingId(null)
    }
  }

  async function handleBulkSign() {
    const reportIds = [...selectedReports]
    if (
      reportIds.length === 0 ||
      !window.confirm(
        `Sign ${reportIds.length} selected ${reportIds.length === 1 ? 'report' : 'reports'} as reviewed?`,
      )
    ) {
      return
    }
    setBulkSigning(true)
    try {
      const reports = reportData?.reports
        .filter((report) => selectedReports.has(report.reportId))
        .map((report) => ({ reportId: report.reportId, expectedReviewVersion: report.reviewVersion })) ?? []
      const reason = requestReviewReason('Sign the selected reports as reviewed', 'Bulk local report review')
      if (!reason || reports.length === 0) return
      const result = await bulkSignLabReports(session.sessionId, {
        reports,
        reason,
      })
      setSelectedReports(new Set())
      setReload((value) => value + 1)
      showToast(
        `${result.signedCount} of ${result.requestedCount} reports signed.`,
        result.signedCount === result.requestedCount ? 'success' : 'error',
      )
    } catch (error) {
      showToast(
        error instanceof Error ? error.message : 'Bulk sign failed.',
        'error',
      )
    } finally {
      setBulkSigning(false)
    }
  }

  const reportData =
    reportState.status === 'ready' ? reportState.data : null
  const orderData = orderState.status === 'ready' ? orderState.data : null
  const eligibleReportIds =
    reportData?.reports
      .filter((report) =>
        ['received', 'assigned'].includes(
          report.reviewStatus?.toLowerCase() ?? 'received',
        ),
      )
      .map((report) => report.reportId) ?? []
  const allEligibleSelected =
    eligibleReportIds.length > 0 &&
    eligibleReportIds.every((id) => selectedReports.has(id))

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <div>
          <h1 className="clinician-page-title">Lab queue</h1>
          <p className="clinician-page-subtitle">
            {reportData
              ? reportData.unreviewedReports > 0
                ? `${reportData.unreviewedReports} reports pending review`
                : 'All reports reviewed'
              : 'Review and route local procedure work'}
            {orderData?.readyToSendOrders
              ? ` · ${orderData.readyToSendOrders} orders ready to send`
              : ''}
          </p>
        </div>
        <button
          className="cl-btn-secondary"
          type="button"
          disabled={
            reportState.status === 'loading' ||
            orderState.status === 'loading'
          }
          onClick={() => setReload((value) => value + 1)}
        >
          <RefreshCw size={15} aria-hidden="true" />
          Refresh
        </button>
      </div>
      {criticalResultsError && (
        <div className="error-banner" role="alert">
          {criticalResultsError}
        </div>
      )}
      {criticalResults && criticalResults.totalOpen > 0 && (
        <div className="error-banner" role="alert">
          {criticalResults.totalOpen} local critical{' '}
          {criticalResults.totalOpen === 1 ? 'result requires' : 'results require'}{' '}
          acknowledgement. This records local review only; it does not send an
          external notification.
          {criticalResults.results.slice(0, 3).map((result) => (
            <div key={result.resultId} className="ne-actions">
              <span>
                {result.patientDisplayName}: {result.text ?? result.code ?? 'Result'}{' '}
                {result.result ?? ''} {result.units ?? ''}
              </span>
              <button
                className="cl-btn-secondary"
                type="button"
                onClick={() => void acknowledgeCritical(result)}
              >
                Acknowledge
              </button>
            </div>
          ))}
        </div>
      )}

      <div className="cl-tab-bar">
        <button
          className={`cl-tab-btn${tab === 'reports' ? ' cl-tab-btn-active' : ''}`}
          type="button"
          onClick={() => setTab('reports')}
        >
          Report review
          {reportData && reportData.unreviewedReports > 0 && (
            <span className="cl-tab-badge">
              {reportData.unreviewedReports}
            </span>
          )}
        </button>
        <button
          className={`cl-tab-btn${tab === 'orders' ? ' cl-tab-btn-active' : ''}`}
          type="button"
          onClick={() => setTab('orders')}
        >
          Order queue
          {orderData && orderData.readyToSendOrders > 0 && (
            <span className="cl-tab-badge">
              {orderData.readyToSendOrders}
            </span>
          )}
        </button>
      </div>

      {searchParams.get('status') && (
        <div className="cl-active-filters" aria-label="Active lab filters">
          <span className="cl-filter-chip">
            Status: {searchParams.get('status')}
          </span>
          <span className="cl-filter-chip">
            Server review status: {activeFilters.reportStatus}
          </span>
        </div>
      )}

      <form className="cl-card message-inbox-filters" onSubmit={applyFilters}>
        <label className="cl-admin-field">
          <span>{tab === 'reports' ? 'Review status' : 'Queue status'}</span>
          {tab === 'reports' ? (
            <select
              className="ne-input"
              value={draft.reportStatus}
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  reportStatus: event.target.value,
                }))
              }
            >
              <option value="unreviewed">Unreviewed</option>
              <option value="reviewed">Reviewed</option>
              <option value="all">All</option>
            </select>
          ) : (
            <select
              className="ne-input"
              value={draft.orderStatus}
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  orderStatus: event.target.value,
                }))
              }
            >
              <option value="ready-to-send">Ready to send</option>
              <option value="transmitted-pending">Transmitted, pending</option>
              <option value="reported">Reported</option>
              <option value="scheduled">Scheduled</option>
              <option value="completed">Completed</option>
              <option value="all">All</option>
            </select>
          )}
        </label>
        <label className="cl-admin-field">
          <span>Patient ID</span>
          <input
            className="ne-input"
            value={draft.patientId}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                patientId: event.target.value,
              }))
            }
            placeholder="Canonical, public, or legacy ID"
          />
        </label>
        <label className="cl-admin-field">
          <span>Provider ID</span>
          <input
            className="ne-input"
            inputMode="numeric"
            value={draft.providerId}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                providerId: event.target.value,
              }))
            }
          />
        </label>
        <label className="cl-admin-field">
          <span>Lab ID</span>
          <input
            className="ne-input"
            inputMode="numeric"
            value={draft.labId}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                labId: event.target.value,
              }))
            }
          />
        </label>
        <label className="cl-admin-field">
          <span>Ordered from</span>
          <input
            className="ne-input"
            type="date"
            value={draft.fromDate}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                fromDate: event.target.value,
              }))
            }
          />
        </label>
        <label className="cl-admin-field">
          <span>Ordered through</span>
          <input
            className="ne-input"
            type="date"
            value={draft.toDate}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                toDate: event.target.value,
              }))
            }
          />
        </label>
        <div className="ne-actions">
          <button className="cl-btn-primary" type="submit">
            Apply filters
          </button>
          <button className="cl-btn-secondary" type="button" onClick={clearFilters}>
            Clear
          </button>
        </div>
      </form>

      {tab === 'reports' && (
        <>
          {reportState.status === 'loading' && (
            <div className="cl-card" aria-live="polite">
              <span className="sr-only">Loading report review queue</span>
              <div className="skeleton-list">
                {[0, 1, 2, 3].map((item) => (
                  <div
                    key={item}
                    className="skeleton-row"
                    style={{ height: 56 }}
                  />
                ))}
              </div>
            </div>
          )}
          {reportState.status === 'error' && (
            <div className="cl-card">
              <div className="error-banner" role="alert">
                {reportState.message}
              </div>
              <button
                className="cl-btn-secondary"
                type="button"
                onClick={() => setReload((value) => value + 1)}
              >
                Retry
              </button>
            </div>
          )}
          {reportData && (
            <>
              <section className="cl-card" aria-label="Report review totals">
                <div className="lab-result-summary">
                  <span>{reportData.totalReports} total</span>
                  <span>{reportData.unreviewedReports} unreviewed</span>
                  <span>{reportData.reviewedReports} reviewed</span>
                  <span>
                    Showing {reportData.reports.length} of at most{' '}
                    {reportData.limit}
                  </span>
                </div>
                <p className="cl-table-sub">
                  Dataset {reportData.datasetId} · {reportData.datasetVersion} ·
                  server status {reportData.statusFilter}
                </p>
              </section>

              {reportData.reports.length === 0 ? (
                <div className="cl-card">
                  <p className="cl-empty-text">
                    No reports match the active server filters.
                  </p>
                </div>
              ) : (
                <section className="cl-card" style={{ padding: 0 }}>
                  {eligibleReportIds.length > 0 && (
                    <div className="ne-actions" style={{ padding: 16 }}>
                      <label className="cl-checkbox-label">
                        <input
                          type="checkbox"
                          checked={allEligibleSelected}
                          onChange={(event) =>
                            setSelectedReports(
                              event.target.checked
                                ? new Set(eligibleReportIds)
                                : new Set(),
                            )
                          }
                        />
                        Select eligible returned reports
                      </label>
                      <button
                        className="cl-btn-primary"
                        type="button"
                        disabled={
                          bulkSigning || selectedReports.size === 0
                        }
                        onClick={() => void handleBulkSign()}
                      >
                        <CheckCircle size={15} aria-hidden="true" />
                        {bulkSigning
                          ? 'Signing…'
                          : `Sign selected (${selectedReports.size})`}
                      </button>
                    </div>
                  )}
                  <div
                    className="cl-table-wrap"
                    role="region"
                    aria-label="Filtered report review queue"
                    tabIndex={0}
                  >
                    <table className="cl-table">
                      <thead>
                        <tr>
                          <th scope="col">Select</th>
                          <th scope="col">Report / patient</th>
                          <th scope="col">Order context</th>
                          <th scope="col">Lab / provider</th>
                          <th scope="col">Review state</th>
                          <th scope="col">Actions</th>
                        </tr>
                      </thead>
                      <tbody>
                        {reportData.reports.map((report) => {
                          const isReviewed =
                            report.reviewStatus?.toLowerCase() === 'reviewed'
                          const isDenied =
                            report.reviewStatus?.toLowerCase() === 'denied'
                          const isTerminal = isReviewed || isDenied
                          const isAssignedToUser =
                            !isTerminal &&
                            report.reviewedBy === session.username
                          const busy = actionId === report.reportId
                          return (
                            <tr key={report.reportId}>
                              <td>
                                {!isTerminal ? (
                                  <input
                                    type="checkbox"
                                    aria-label={`Select report ${report.reportId} for ${report.patientDisplayName}`}
                                    checked={selectedReports.has(
                                      report.reportId,
                                    )}
                                    onChange={(event) =>
                                      setSelectedReports((current) => {
                                        const next = new Set(current)
                                        if (event.target.checked) {
                                          next.add(report.reportId)
                                        } else {
                                          next.delete(report.reportId)
                                        }
                                        return next
                                      })
                                    }
                                  />
                                ) : (
                                  <span aria-hidden="true">—</span>
                                )}
                              </td>
                              <td>
                                <strong>
                                  {report.procedureName ??
                                    report.procedureCode ??
                                    `Report ${report.reportId}`}
                                </strong>
                                <button
                                  className="cl-table-link"
                                  type="button"
                                  onClick={() =>
                                    navigate(
                                      `/clinician/patients/${report.patientId}/labs`,
                                    )
                                  }
                                >
                                  {report.patientDisplayName}
                                </button>
                                <p className="cl-table-sub">
                                  {report.pubpid} · report {report.reportId}
                                </p>
                              </td>
                              <td>
                                <span>Reported {report.reportDate}</span>
                                <p className="cl-table-sub">
                                  Ordered {report.orderDate} · order{' '}
                                  {report.orderId}
                                  {report.specimenNumber
                                    ? ` · specimen ${report.specimenNumber}`
                                    : ''}
                                </p>
                              </td>
                              <td>
                                <span>{report.labName ?? 'Lab unavailable'}</span>
                                <p className="cl-table-sub">
                                  {report.providerName ??
                                    'Provider unavailable'}
                                  {report.labId ? ` · lab ${report.labId}` : ''}
                                </p>
                              </td>
                              <td>
                                <span
                                  className={`cl-badge ${reviewStatusClass(report.reviewStatus)}`}
                                >
                                  {report.reviewStatus ?? 'unreviewed'}
                                </span>
                                <p className="cl-table-sub">
                                  {report.reviewedBy
                                    ? `${isReviewed ? 'Reviewed' : isDenied ? 'Denied' : 'Assigned'} by ${report.reviewedBy}`
                                    : 'No reviewer assigned'}
                                  {report.reviewedAt
                                    ? ` · ${formatDateTime(report.reviewedAt)}`
                                    : ''}
                                </p>
                              </td>
                              <td>
                                <div className="ne-actions">
                                  {!isTerminal && !isAssignedToUser && (
                                    <button
                                      className="cl-btn-secondary"
                                      type="button"
                                      disabled={actionId !== null}
                                      onClick={() => void claimReport(report)}
                                    >
                                      <UserCheck
                                        size={14}
                                        aria-hidden="true"
                                      />
                                      {busy
                                        ? 'Assigning…'
                                        : report.reviewedBy
                                          ? 'Reassign to me'
                                          : 'Claim'}
                                    </button>
                                  )}
                                  {isAssignedToUser && (
                                    <span className="cl-badge cl-badge-blue">
                                      Assigned to you
                                    </span>
                                  )}
                                  {!isTerminal && (
                                    <button
                                      className="cl-btn-primary"
                                      type="button"
                                      disabled={actionId !== null}
                                      onClick={() => void signReport(report)}
                                    >
                                      <CheckCircle
                                        size={14}
                                        aria-hidden="true"
                                      />
                                      {busy ? 'Signing…' : 'Sign reviewed'}
                                    </button>
                                  )}
                                  {!isTerminal && (
                                    <button
                                      className="cl-btn-secondary"
                                      type="button"
                                      disabled={actionId !== null}
                                      onClick={() => void denyReport(report)}
                                    >
                                      {busy ? 'Saving…' : 'Deny review'}
                                    </button>
                                  )}
                                  {isTerminal && (
                                    <button
                                      className="cl-btn-secondary"
                                      type="button"
                                      disabled={actionId !== null}
                                      onClick={() => void reopenReport(report)}
                                    >
                                      {busy ? 'Reopening…' : 'Reopen review'}
                                    </button>
                                  )}
                                  <button
                                    className="cl-btn-secondary"
                                    type="button"
                                    disabled={historyLoadingId === report.reportId}
                                    onClick={() => void toggleReviewHistory(report)}
                                  >
                                    <History size={14} aria-hidden="true" />
                                    {historyLoadingId === report.reportId
                                      ? 'Loading history…'
                                      : reviewHistoryByReport[report.reportId]
                                        ? 'Hide history'
                                        : `History (${report.reviewHistoryCount})`}
                                  </button>
                                </div>
                                {reviewHistoryByReport[report.reportId] && (
                                  <ol className="cl-table-sub" aria-label={`Review history for report ${report.reportId}`}>
                                    {reviewHistoryByReport[report.reportId].events.map((event) => (
                                      <li key={event.eventId}>
                                        {event.action} · {event.previousStatus ?? 'none'} → {event.currentStatus} · {event.actor} · {formatDateTime(event.occurredAt)}
                                        {event.reason ? ` · ${event.reason}` : ''}
                                      </li>
                                    ))}
                                  </ol>
                                )}
                                {actionError?.id === report.reportId && (
                                  <p className="field-error" role="alert">
                                    {actionError.message}
                                  </p>
                                )}
                              </td>
                            </tr>
                          )
                        })}
                      </tbody>
                    </table>
                  </div>
                </section>
              )}
            </>
          )}
        </>
      )}

      {tab === 'orders' && (
        <>
          {orderState.status === 'loading' && (
            <div className="cl-card" aria-live="polite">
              <span className="sr-only">Loading procedure order queue</span>
              <div className="skeleton-list">
                {[0, 1, 2, 3].map((item) => (
                  <div
                    key={item}
                    className="skeleton-row"
                    style={{ height: 56 }}
                  />
                ))}
              </div>
            </div>
          )}
          {orderState.status === 'error' && (
            <div className="cl-card">
              <div className="error-banner" role="alert">
                {orderState.message}
              </div>
              <button
                className="cl-btn-secondary"
                type="button"
                onClick={() => setReload((value) => value + 1)}
              >
                Retry
              </button>
            </div>
          )}
          {orderData && (
            <>
              <section className="cl-card" aria-label="Procedure order totals">
                <div className="lab-result-summary">
                  <span>{orderData.totalOrders} total</span>
                  <span>{orderData.readyToSendOrders} ready to send</span>
                  <span>
                    {orderData.transmittedPendingOrders} transmitted, pending
                  </span>
                  <span>{orderData.reportedOrders} reported</span>
                  <span>{orderData.completedOrders} completed</span>
                </div>
                <p className="cl-table-sub">
                  Dataset {orderData.datasetId} · {orderData.datasetVersion} ·
                  server status {orderData.statusFilter} · showing{' '}
                  {orderData.orders.length} of at most {orderData.limit}
                </p>
              </section>

              {orderData.orders.length === 0 ? (
                <div className="cl-card">
                  <p className="cl-empty-text">
                    No orders match the active server filters.
                  </p>
                </div>
              ) : (
                <section className="cl-card" style={{ padding: 0 }}>
                  <div
                    className="cl-table-wrap"
                    role="region"
                    aria-label="Filtered procedure order queue"
                    tabIndex={0}
                  >
                    <table className="cl-table">
                      <thead>
                        <tr>
                          <th scope="col">Order / patient</th>
                          <th scope="col">Test</th>
                          <th scope="col">Lab / provider</th>
                          <th scope="col">Queue state</th>
                          <th scope="col">Clinical evidence</th>
                        </tr>
                      </thead>
                      <tbody>
                        {orderData.orders.map((order) => (
                          <tr key={order.orderId}>
                            <td>
                              <button
                                className="cl-table-link"
                                type="button"
                                onClick={() =>
                                  navigate(
                                    `/clinician/patients/${order.patientId}/labs`,
                                  )
                                }
                              >
                                {order.patientDisplayName}
                              </button>
                              <p className="cl-table-sub">
                                {order.pubpid} · order {order.orderId} ·{' '}
                                {order.orderDate}
                              </p>
                            </td>
                            <td>
                              <strong>
                                {order.procedureName ??
                                  order.procedureCode ??
                                  'Procedure unavailable'}
                              </strong>
                              <p className="cl-table-sub">
                                {order.procedureCode ?? 'No code'} ·{' '}
                                {order.orderPriority ?? 'Priority unavailable'}
                              </p>
                            </td>
                            <td>
                              <span>{order.labName ?? 'Lab unavailable'}</span>
                              <p className="cl-table-sub">
                                {order.providerName ?? 'Provider unavailable'}
                              </p>
                            </td>
                            <td>
                              <span
                                className={`cl-badge ${
                                  order.queueState === 'reported'
                                    ? 'cl-badge-green'
                                    : order.queueState ===
                                        'transmitted-pending'
                                      ? 'cl-badge-blue'
                                      : 'cl-badge-amber'
                                }`}
                              >
                                {order.queueState}
                              </span>
                              <p className="cl-table-sub">
                                Order status {order.orderStatus ?? 'unavailable'}
                                {order.dateTransmitted
                                  ? ` · sent ${formatDateTime(order.dateTransmitted)}`
                                  : ' · not transmitted'}
                              </p>
                            </td>
                            <td>
                              <span>
                                {order.specimenCount} specimens ·{' '}
                                {order.reportCount} reports · {order.resultCount}{' '}
                                results
                              </span>
                              <p className="cl-table-sub">
                                {order.canTransmit
                                  ? 'Eligible for local transmit workflow'
                                  : 'Not eligible for local transmit workflow'}
                              </p>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </section>
              )}
            </>
          )}
        </>
      )}
    </div>
  )
}
