import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useNavigate, useOutletContext, useSearchParams } from 'react-router-dom'
import {
  getClinicalWorkflowAssignees,
  getReferralWorkQueue,
  isRequestCancellation,
  type ReferralWorkQueueFilters,
  type ReferralWorkQueueResponse,
} from '../../api.ts'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

type QueueState =
  | { status: 'loading'; data?: ReferralWorkQueueResponse }
  | { status: 'ready'; data: ReferralWorkQueueResponse }
  | { status: 'error'; message: string; data?: ReferralWorkQueueResponse }

type FilterDraft = {
  status: NonNullable<ReferralWorkQueueFilters['status']>
  assignedTo: string
  overdueOnly: boolean
  query: string
}

function filtersFromParams(params: URLSearchParams): FilterDraft {
  const rawStatus = params.get('status')
  const status =
    rawStatus === 'draft' ||
    rawStatus === 'sent' ||
    rawStatus === 'received' ||
    rawStatus === 'closed' ||
    rawStatus === 'cancelled'
      ? rawStatus
      : 'all'
  return {
    status,
    assignedTo: params.get('assignedTo') ?? '',
    overdueOnly: params.get('overdueOnly') === 'true',
    query: params.get('query') ?? '',
  }
}

function formatDate(value?: string | null) {
  if (!value) return 'Not set'
  const date = new Date(value)
  return Number.isNaN(date.valueOf())
    ? value
    : date.toLocaleDateString(undefined, { timeZone: 'UTC' })
}

function statusClass(status: string) {
  if (status === 'closed') return 'cl-badge-green'
  if (status === 'cancelled') return 'cl-badge-muted'
  if (status === 'received') return 'cl-badge-blue'
  return 'cl-badge-amber'
}

export default function ReferralWorkQueue() {
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
  const [queueState, setQueueState] = useState<QueueState>({ status: 'loading' })
  const [assignees, setAssignees] = useState<
    Awaited<ReturnType<typeof getClinicalWorkflowAssignees>>['assignees']
  >([])
  const [refresh, setRefresh] = useState(0)

  useEffect(() => {
    setDraft(filtersFromParams(new URLSearchParams(queryKey)))
    const controller = new AbortController()
    setQueueState((current) => ({ status: 'loading', data: current.data }))
    Promise.all([
      getReferralWorkQueue(session.sessionId, { ...activeFilters, limit: 100 }, controller.signal),
      getClinicalWorkflowAssignees(session.sessionId, controller.signal),
    ])
      .then(([queue, roster]) => {
        setQueueState({ status: 'ready', data: queue })
        setAssignees(roster.assignees)
      })
      .catch((error: unknown) => {
        if (isRequestCancellation(error)) return
        setQueueState((current) => ({
          status: 'error',
          data: current.data,
          message:
            error instanceof Error
              ? error.message
              : 'The referral work queue could not be loaded.',
        }))
      })
    return () => controller.abort()
  }, [activeFilters, queryKey, refresh, session.sessionId])

  const queue = queueState.data

  function applyFilters(event: FormEvent) {
    event.preventDefault()
    const next = new URLSearchParams()
    if (draft.status !== 'all') next.set('status', draft.status)
    if (draft.assignedTo) next.set('assignedTo', draft.assignedTo)
    if (draft.overdueOnly) next.set('overdueOnly', 'true')
    if (draft.query.trim()) next.set('query', draft.query.trim())
    setSearchParams(next)
  }

  function resetFilters() {
    setDraft(filtersFromParams(new URLSearchParams()))
    setSearchParams({})
  }

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <h1 className="clinician-page-title">Referral work queue</h1>
        <p className="clinician-page-subtitle">
          Cross-patient view of locally tracked referrals. Open a patient workflow to
          assign responsibility or record a protected transition.
        </p>
      </div>

      <section className="cl-card" aria-label="Referral queue summary">
        <div className="cl-inline-form">
          <p><strong>{queue?.activeCount ?? 0}</strong> active referrals</p>
          <p><strong>{queue?.overdueCount ?? 0}</strong> overdue referrals</p>
          <p><strong>{queue?.total ?? 0}</strong> matching referrals</p>
          <div className="cl-inline-form-actions">
            <button
              className="cl-btn-secondary"
              type="button"
              onClick={() => setRefresh((value) => value + 1)}
              disabled={queueState.status === 'loading'}
            >
              Refresh
            </button>
          </div>
        </div>
      </section>

      <section className="cl-card">
        <form className="cl-inline-form" onSubmit={applyFilters}>
          <label className="cl-admin-field">
            <span>Status</span>
            <select
              className="ne-input"
              value={draft.status}
              onChange={(event) => setDraft((current) => ({ ...current, status: event.target.value as FilterDraft['status'] }))}
            >
              <option value="all">All statuses</option>
              <option value="draft">Draft</option>
              <option value="sent">Sent</option>
              <option value="received">Received</option>
              <option value="closed">Closed</option>
              <option value="cancelled">Cancelled</option>
            </select>
          </label>
          <label className="cl-admin-field">
            <span>Responsible staff</span>
            <select
              className="ne-input"
              value={draft.assignedTo}
              onChange={(event) => setDraft((current) => ({ ...current, assignedTo: event.target.value }))}
            >
              <option value="">Any staff member</option>
              {assignees.map((assignee) => (
                <option key={assignee.username} value={assignee.username}>
                  {assignee.displayName}
                </option>
              ))}
            </select>
          </label>
          <label className="cl-admin-field">
            <span>Search</span>
            <input
              className="ne-input"
              value={draft.query}
              onChange={(event) => setDraft((current) => ({ ...current, query: event.target.value }))}
              placeholder="Patient, destination, or reason"
            />
          </label>
          <label className="cl-admin-field">
            <span>Due state</span>
            <span>
              <input
                type="checkbox"
                checked={draft.overdueOnly}
                onChange={(event) => setDraft((current) => ({ ...current, overdueOnly: event.target.checked }))}
              />{' '}
              Overdue only
            </span>
          </label>
          <div className="cl-inline-form-actions">
            <button className="cl-btn-primary" type="submit">Apply filters</button>
            <button className="cl-btn-secondary" type="button" onClick={resetFilters}>Clear</button>
          </div>
        </form>
      </section>

      {queueState.status === 'error' && (
        <section className="cl-card" role="alert">
          <p className="cl-error-text">{queueState.message}</p>
          <button className="cl-btn-secondary" type="button" onClick={() => setRefresh((value) => value + 1)}>Try again</button>
        </section>
      )}

      <section className="cl-card" aria-busy={queueState.status === 'loading'}>
        <h2 className="cl-card-title">Matching referrals</h2>
        {queueState.status === 'loading' && !queue && <p>Loading referral queue…</p>}
        {queue && queue.items.length > 0 && (
          <table className="cl-table">
            <thead>
              <tr>
                <th>Patient</th>
                <th>Referral</th>
                <th>Owner / due</th>
                <th>Status</th>
                <th><span className="sr-only">Open workflow</span></th>
              </tr>
            </thead>
            <tbody>
              {queue.items.map((item) => (
                <tr key={item.referral.id}>
                  <td>
                    <strong>{item.patientDisplayName}</strong><br />
                    <small>{item.pubpid}</small>
                  </td>
                  <td>
                    <strong>{item.referral.destination}</strong><br />
                    <small>{item.referral.reason}</small>
                  </td>
                  <td>
                    {item.referral.assignedDisplayName}<br />
                    <small className={item.isOverdue ? 'cl-error-text' : undefined}>
                      {item.isOverdue ? 'Overdue: ' : 'Due: '}{formatDate(item.referral.dueAt)}
                    </small>
                  </td>
                  <td>
                    <span className={`cl-badge ${statusClass(item.referral.status)}`}>
                      {item.referral.status}
                    </span><br />
                    <small>v{item.referral.workflowVersion}</small>
                  </td>
                  <td>
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      onClick={() => navigate(`/clinician/patients/${encodeURIComponent(item.referral.patientId)}/referrals`)}
                    >
                      Open workflow
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        {queueState.status !== 'loading' && queue && queue.items.length === 0 && (
          <p className="cl-empty-text">No referrals match these filters.</p>
        )}
      </section>
    </div>
  )
}
