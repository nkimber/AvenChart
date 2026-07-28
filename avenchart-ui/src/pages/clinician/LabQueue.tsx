import {
  useEffect,
  useEffectEvent,
  useMemo,
  useState,
  type FormEvent,
} from 'react'
import { useNavigate, useOutletContext, useSearchParams } from 'react-router-dom'
import { CheckCircle, RefreshCw, UserCheck } from 'lucide-react'
import {
  assignLabReportReviewer,
  bulkSignLabReports,
  getProcedureOrderQueue,
  getProcedureReportQueue,
  isRequestCancellation,
  reopenLabReportReview,
  signLabReport,
  type ProcedureOrderQueueResponse,
  type ProcedureQueueFilters,
  type ProcedureReportQueueItem,
  type ProcedureReportQueueResponse,
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
  return 'cl-badge-amber'
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

  function loadReports(signal?: AbortSignal) {
    setReportState({ status: 'loading' })
    getProcedureReportQueue(
      session.sessionId,
      queueFilters(activeFilters, 'reports'),
      signal,
    )
      .then((data) => setReportState({ status: 'ready', data }))
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
      .then((data) => setOrderState({ status: 'ready', data }))
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
    void loadReportsForCriteria(controller.signal)
    void loadOrdersForCriteria(controller.signal)
    return () => controller.abort()
  }, [queryKey, reload, session.sessionId])

  function applyFilters(event: FormEvent) {
    event.preventDefault()
    const params = new URLSearchParams()
    Object.entries(draft).forEach(([key, value]) => {
      if (!value || value === 'all') return
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
    return runReportAction(
      report.reportId,
      'Report assigned to you.',
      () =>
        assignLabReportReviewer(session.sessionId, report.reportId, {
          assignedTo: session.username,
          assignedAt: new Date().toISOString(),
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
    return runReportAction(
      report.reportId,
      'Report signed as reviewed.',
      () =>
        signLabReport(session.sessionId, report.reportId, {
          reviewedBy: session.username,
          reviewedAt: new Date().toISOString(),
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
    return runReportAction(
      report.reportId,
      'Report review reopened.',
      () =>
        reopenLabReportReview(
          session.sessionId,
          report.reportId,
        ),
    )
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
      const result = await bulkSignLabReports(session.sessionId, {
        reportIds,
        reviewedBy: session.username,
        reviewedAt: new Date().toISOString(),
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
      .filter((report) => report.reviewStatus?.toLowerCase() !== 'reviewed')
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
                          const isAssignedToUser =
                            !isReviewed &&
                            report.reviewedBy === session.username
                          const busy = actionId === report.reportId
                          return (
                            <tr key={report.reportId}>
                              <td>
                                {!isReviewed ? (
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
                                    ? `${isReviewed ? 'Reviewed' : 'Assigned'} by ${report.reviewedBy}`
                                    : 'No reviewer assigned'}
                                  {report.reviewedAt
                                    ? ` · ${formatDateTime(report.reviewedAt)}`
                                    : ''}
                                </p>
                              </td>
                              <td>
                                <div className="ne-actions">
                                  {!isReviewed && !isAssignedToUser && (
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
                                  {!isReviewed && (
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
                                  {isReviewed && (
                                    <button
                                      className="cl-btn-secondary"
                                      type="button"
                                      disabled={actionId !== null}
                                      onClick={() => void reopenReport(report)}
                                    >
                                      {busy ? 'Reopening…' : 'Reopen review'}
                                    </button>
                                  )}
                                </div>
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
