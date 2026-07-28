import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Link, useOutletContext } from 'react-router-dom'
import {
  ArrowRight,
  Check,
  ChevronLeft,
  ChevronRight,
  Clock,
  FileCheck,
  History,
  RefreshCw,
  Route,
  Search,
  UserRoundCheck,
  X,
} from 'lucide-react'
import {
  completePatientDocumentRouting,
  getPatientDocumentRoutingAssignees,
  getPatientDocumentRoutingHistory,
  getPatientDocumentRoutingQueue,
  routePatientDocument,
  type PatientDocumentRoutingAssignee,
  type PatientDocumentRoutingHistoryResponse,
  type PatientDocumentRoutingQueueFilters,
  type PatientDocumentRoutingQueueItem,
  type PatientDocumentRoutingQueueResponse,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

type QueueState =
  | { status: 'loading' }
  | { status: 'ready'; data: PatientDocumentRoutingQueueResponse }
  | { status: 'error'; message: string; data?: PatientDocumentRoutingQueueResponse }

type HistoryState =
  | { status: 'loading'; documentId: number }
  | { status: 'ready'; documentId: number; data: PatientDocumentRoutingHistoryResponse }
  | { status: 'error'; documentId: number; message: string }
  | null

type RouteEditor = {
  item: PatientDocumentRoutingQueueItem
  destination: string
  priority: 'High' | 'Standard'
  assignedTo: string
  dueAt: string
  reason: string
}

type CompletionEditor = {
  item: PatientDocumentRoutingQueueItem
  reason: string
}

const PAGE_SIZE = 10

const initialFilters: PatientDocumentRoutingQueueFilters = {
  status: 'active',
  limit: PAGE_SIZE,
  offset: 0,
}

function formatDateTime(value?: string | null) {
  if (!value) return 'Not recorded'
  const parsed = new Date(value)
  return Number.isNaN(parsed.valueOf()) ? value : parsed.toLocaleString()
}

function toLocalDateTimeInput(value: string) {
  const parsed = new Date(value)
  if (Number.isNaN(parsed.valueOf())) return ''
  const offset = parsed.getTimezoneOffset() * 60_000
  return new Date(parsed.valueOf() - offset).toISOString().slice(0, 16)
}

function editableDueAt(item: PatientDocumentRoutingQueueItem) {
  const currentDueAt = new Date(item.dueAt)
  const now = Date.now()
  if (!Number.isNaN(currentDueAt.valueOf()) && currentDueAt.valueOf() > now) {
    return toLocalDateTimeInput(item.dueAt)
  }

  const defaultHours = item.priority === 'High' ? 24 : 72
  return toLocalDateTimeInput(new Date(now + defaultHours * 60 * 60 * 1000).toISOString())
}

function formatAge(hours: number) {
  if (hours < 24) return `${hours} hour${hours === 1 ? '' : 's'}`
  const days = Math.floor(hours / 24)
  const remainingHours = hours % 24
  return remainingHours === 0
    ? `${days} day${days === 1 ? '' : 's'}`
    : `${days}d ${remainingHours}h`
}

function stateLabel(value: string) {
  return value.replaceAll('_', ' ').replace(/\b\w/g, (letter) => letter.toUpperCase())
}

export default function DocumentRoutingQueue() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const [draftFilters, setDraftFilters] =
    useState<PatientDocumentRoutingQueueFilters>(initialFilters)
  const [filters, setFilters] =
    useState<PatientDocumentRoutingQueueFilters>(initialFilters)
  const [queueState, setQueueState] = useState<QueueState>({ status: 'loading' })
  const [assignees, setAssignees] = useState<PatientDocumentRoutingAssignee[]>([])
  const [assigneeError, setAssigneeError] = useState<string | null>(null)
  const [routeEditor, setRouteEditor] = useState<RouteEditor | null>(null)
  const [completionEditor, setCompletionEditor] =
    useState<CompletionEditor | null>(null)
  const [historyState, setHistoryState] = useState<HistoryState>(null)
  const [saving, setSaving] = useState(false)

  const loadQueue = useCallback(
    async (signal?: AbortSignal) => {
      setQueueState((current) =>
        current.status === 'ready'
          ? { status: 'loading' }
          : current.status === 'error' && current.data
            ? { status: 'loading' }
            : { status: 'loading' },
      )
      try {
        const data = await getPatientDocumentRoutingQueue(
          session.sessionId,
          filters,
          signal,
        )
        setQueueState({ status: 'ready', data })
      } catch (error) {
        if (signal?.aborted) return
        setQueueState((current) => ({
          status: 'error',
          message:
            error instanceof Error
              ? error.message
              : 'The document routing queue could not be loaded.',
          data: current.status === 'ready' ? current.data : undefined,
        }))
      }
    },
    [filters, session.sessionId],
  )

  useEffect(() => {
    const controller = new AbortController()
    void loadQueue(controller.signal)
    return () => controller.abort()
  }, [loadQueue])

  useEffect(() => {
    const controller = new AbortController()
    getPatientDocumentRoutingAssignees(session.sessionId, controller.signal)
      .then((response) => {
        setAssignees(response.assignees)
        setAssigneeError(null)
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return
        setAssigneeError(
          error instanceof Error
            ? error.message
            : 'Routing assignees could not be loaded.',
        )
      })
    return () => controller.abort()
  }, [session.sessionId])

  const queue =
    queueState.status === 'ready'
      ? queueState.data
      : queueState.status === 'error'
        ? queueState.data
        : undefined
  const offset = filters.offset ?? 0
  const canGoBack = offset > 0
  const canGoForward = Boolean(queue && offset + queue.returnedCount < queue.totalCount)

  function applyFilters(event: FormEvent) {
    event.preventDefault()
    setFilters({ ...draftFilters, offset: 0, limit: PAGE_SIZE })
    closePanels()
  }

  function resetFilters() {
    setDraftFilters(initialFilters)
    setFilters(initialFilters)
    closePanels()
  }

  function closePanels() {
    setRouteEditor(null)
    setCompletionEditor(null)
    setHistoryState(null)
  }

  function openRoute(item: PatientDocumentRoutingQueueItem) {
    setCompletionEditor(null)
    setHistoryState(null)
    setRouteEditor({
      item,
      destination: item.routeDestination,
      priority: item.priority === 'High' ? 'High' : 'Standard',
      assignedTo: item.assignedTo ?? '',
      dueAt: editableDueAt(item),
      reason: '',
    })
  }

  function openCompletion(item: PatientDocumentRoutingQueueItem) {
    setRouteEditor(null)
    setHistoryState(null)
    setCompletionEditor({ item, reason: '' })
  }

  async function openHistory(item: PatientDocumentRoutingQueueItem) {
    setRouteEditor(null)
    setCompletionEditor(null)
    setHistoryState({ status: 'loading', documentId: item.id })
    try {
      const data = await getPatientDocumentRoutingHistory(
        session.sessionId,
        item.id,
      )
      setHistoryState({ status: 'ready', documentId: item.id, data })
    } catch (error) {
      setHistoryState({
        status: 'error',
        documentId: item.id,
        message:
          error instanceof Error
            ? error.message
            : 'Routing history could not be loaded.',
      })
    }
  }

  async function saveRoute(event: FormEvent) {
    event.preventDefault()
    if (!routeEditor || saving) return
    const due = new Date(routeEditor.dueAt)
    if (
      routeEditor.destination.trim().length < 3 ||
      routeEditor.reason.trim().length < 3 ||
      Number.isNaN(due.valueOf())
    ) {
      return
    }

    setSaving(true)
    try {
      await routePatientDocument(session.sessionId, routeEditor.item.id, {
        destination: routeEditor.destination.trim(),
        priority: routeEditor.priority,
        assignedTo: routeEditor.assignedTo || null,
        reason: routeEditor.reason.trim(),
        dueAt: due.toISOString(),
        expectedTaskVersion: routeEditor.item.taskVersion,
      })
      showToast(
        routeEditor.item.queueStatus === 'Completed'
          ? 'Document routing reopened.'
          : 'Document routing saved.',
      )
      setRouteEditor(null)
      await loadQueue()
    } catch (error) {
      showToast(
        error instanceof Error ? error.message : 'Document routing could not be saved.',
        'error',
      )
    } finally {
      setSaving(false)
    }
  }

  async function completeRoute(event: FormEvent) {
    event.preventDefault()
    if (!completionEditor || saving || completionEditor.reason.trim().length < 3) {
      return
    }

    setSaving(true)
    try {
      await completePatientDocumentRouting(
        session.sessionId,
        completionEditor.item.id,
        {
          reason: completionEditor.reason.trim(),
          expectedTaskVersion: completionEditor.item.taskVersion,
        },
      )
      showToast('Routing work completed.')
      setCompletionEditor(null)
      await loadQueue()
    } catch (error) {
      showToast(
        error instanceof Error
          ? error.message
          : 'Routing work could not be completed.',
        'error',
      )
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="clinician-page document-routing-page">
      <header className="clinician-page-header">
        <div>
          <h1 className="clinician-page-title">Document routing</h1>
          <p className="clinician-page-subtitle">
            Assign, prioritize, age, and complete local document handoffs with retained history.
          </p>
        </div>
        <button
          className="cl-btn-secondary"
          type="button"
          disabled={queueState.status === 'loading'}
          onClick={() => void loadQueue()}
        >
          <RefreshCw size={15} aria-hidden="true" />
          Refresh queue
        </button>
      </header>

      <div className="hint-banner">
        <strong>Local workflow boundary:</strong>{' '}
        <span>
          Routing records an internal work assignment only. Completion does not
          approve, sign, send, release, fax, or deliver the protected document.
        </span>
      </div>

      <section className="document-routing-metrics" aria-label="Document routing counts">
        {[
          ['Active', queue?.counts.active],
          ['Unassigned', queue?.counts.unassigned],
          ['High priority', queue?.counts.highPriority],
          ['Overdue', queue?.counts.overdue],
          ['Completed', queue?.counts.completed],
        ].map(([label, value]) => (
          <div className="document-routing-metric" key={label}>
            <span>{label}</span>
            <strong>{value ?? '—'}</strong>
          </div>
        ))}
      </section>

      <form className="document-routing-filters" onSubmit={applyFilters}>
        <label className="document-routing-search">
          Patient, chart, document, category, destination, or assignee
          <span>
            <Search size={15} aria-hidden="true" />
            <input
              value={draftFilters.query ?? ''}
              onChange={(event) =>
                setDraftFilters((current) => ({
                  ...current,
                  query: event.target.value,
                }))
              }
              placeholder="Search the queue"
            />
          </span>
        </label>
        <label>
          Status
          <select
            value={draftFilters.status ?? 'active'}
            onChange={(event) =>
              setDraftFilters((current) => ({
                ...current,
                status: event.target.value as PatientDocumentRoutingQueueFilters['status'],
              }))
            }
          >
            <option value="active">Active</option>
            <option value="pending">Pending assignment</option>
            <option value="in_progress">In progress</option>
            <option value="completed">Completed</option>
            <option value="all">All</option>
          </select>
        </label>
        <label>
          Priority
          <select
            value={draftFilters.priority ?? ''}
            onChange={(event) =>
              setDraftFilters((current) => ({
                ...current,
                priority:
                  (event.target.value || undefined) as PatientDocumentRoutingQueueFilters['priority'],
              }))
            }
          >
            <option value="">Any priority</option>
            <option value="High">High</option>
            <option value="Standard">Standard</option>
          </select>
        </label>
        <label>
          Assignment
          <select
            value={draftFilters.assignedTo ?? ''}
            onChange={(event) =>
              setDraftFilters((current) => ({
                ...current,
                assignedTo: event.target.value || undefined,
              }))
            }
          >
            <option value="">Any assignment</option>
            <option value="unassigned">Unassigned</option>
            <option value={session.username}>Assigned to me</option>
            {assignees
              .filter((assignee) => assignee.username !== session.username)
              .map((assignee) => (
                <option key={assignee.username} value={assignee.username}>
                  {assignee.displayName}
                </option>
              ))}
          </select>
        </label>
        <label>
          Minimum age
          <select
            value={draftFilters.minimumAgeHours ?? 0}
            onChange={(event) =>
              setDraftFilters((current) => ({
                ...current,
                minimumAgeHours: Number(event.target.value),
              }))
            }
          >
            <option value={0}>Any age</option>
            <option value={24}>24 hours</option>
            <option value={72}>3 days</option>
            <option value={168}>7 days</option>
          </select>
        </label>
        <div className="document-routing-filter-actions">
          <button className="cl-btn-primary" type="submit">
            Apply filters
          </button>
          <button className="cl-btn-secondary" type="button" onClick={resetFilters}>
            Clear
          </button>
        </div>
      </form>

      {assigneeError && (
        <div className="hint-banner" role="status">
          Assignee directory unavailable. Existing assignments remain visible; retry by
          refreshing this page.
        </div>
      )}

      {queueState.status === 'loading' && !queue && (
        <section className="cl-card cl-inline-state" aria-live="polite">
          <p>Loading document routing work…</p>
        </section>
      )}
      {queueState.status === 'error' && (
        <section className="error-banner" role="alert">
          <span>{queueState.message}</span>
          <button className="link-button" type="button" onClick={() => void loadQueue()}>
            Retry queue
          </button>
        </section>
      )}

      {queue && (
        <>
          <div className="document-routing-result-summary" role="status">
            <span>
              Showing {queue.returnedCount} of {queue.totalCount} matching tasks
            </span>
            <span>
              Dataset {queue.datasetId} / version {queue.datasetVersion}
            </span>
          </div>

          {queue.items.length === 0 ? (
            <section className="cl-card cl-empty-state-sm">
              <FileCheck size={28} aria-hidden="true" />
              <p>No document routing tasks match these filters.</p>
              <button className="cl-link" type="button" onClick={resetFilters}>
                Clear filters
              </button>
            </section>
          ) : (
            <section className="document-routing-list" aria-label="Document routing queue">
              {queue.items.map((item) => (
                <article
                  className={`document-routing-card${item.isOverdue ? ' document-routing-card-overdue' : ''}`}
                  key={item.id}
                >
                  <div className="document-routing-card-heading">
                    <div>
                      <div className="document-routing-title-row">
                        <h2>{item.name}</h2>
                        <span className={`document-routing-priority document-routing-priority-${item.priority.toLowerCase()}`}>
                          {item.priority}
                        </span>
                        <span className="patient-document-review-badge">
                          {item.queueStatus}
                        </span>
                        {item.isOverdue && (
                          <span className="document-routing-overdue">Overdue</span>
                        )}
                      </div>
                      <p>
                        <Link
                          className="cl-link"
                          to={`/clinician/patients/${encodeURIComponent(item.patientId)}/documents`}
                        >
                          {item.patientDisplayName}
                        </Link>{' '}
                        / {item.pubpid} / {item.categoryName}
                      </p>
                    </div>
                    <div className="document-routing-age">
                      <Clock size={15} aria-hidden="true" />
                      <span>{formatAge(item.ageHours)} old</span>
                    </div>
                  </div>

                  <dl className="document-routing-facts">
                    <div>
                      <dt>Destination</dt>
                      <dd>{item.routeDestination}</dd>
                    </div>
                    <div>
                      <dt>Assigned to</dt>
                      <dd>{item.assignedDisplayName ?? 'Unassigned'}</dd>
                    </div>
                    <div>
                      <dt>Due</dt>
                      <dd>{formatDateTime(item.dueAt)}</dd>
                    </div>
                    <div>
                      <dt>Review state</dt>
                      <dd>{stateLabel(item.reviewStatus)}</dd>
                    </div>
                    <div>
                      <dt>Task version</dt>
                      <dd>
                        {item.inferred ? 'Inferred / not yet routed' : `v${item.taskVersion}`}
                      </dd>
                    </div>
                    <div>
                      <dt>Document date</dt>
                      <dd>{item.docDate}</dd>
                    </div>
                  </dl>
                  <p className="document-routing-reason">{item.routingReason}</p>

                  <div className="patient-document-actions">
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      onClick={() => openRoute(item)}
                    >
                      <Route size={14} aria-hidden="true" />
                      {item.queueStatus === 'Completed'
                        ? 'Reopen route'
                        : item.inferred
                          ? 'Route document'
                          : 'Update route'}
                    </button>
                    {item.queueStatus !== 'Completed' && (
                      <button
                        className="cl-btn-primary"
                        type="button"
                        onClick={() => openCompletion(item)}
                      >
                        <Check size={14} aria-hidden="true" />
                        Complete work
                      </button>
                    )}
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      onClick={() => void openHistory(item)}
                    >
                      <History size={14} aria-hidden="true" />
                      Routing history
                    </button>
                  </div>

                  {routeEditor?.item.id === item.id && (
                    <form
                      className="patient-document-panel document-routing-editor"
                      aria-labelledby={`route-document-${item.id}`}
                      onSubmit={saveRoute}
                    >
                      <div className="patient-document-panel-heading">
                        <div>
                          <h3 id={`route-document-${item.id}`}>
                            {item.queueStatus === 'Completed'
                              ? 'Reopen document routing'
                              : 'Route document'}
                          </h3>
                          <p>
                            Saving checks task version {item.taskVersion}. A concurrent
                            change is rejected and must be reloaded.
                          </p>
                        </div>
                        <button
                          className="cl-icon-button"
                          type="button"
                          aria-label="Close routing form"
                          onClick={() => setRouteEditor(null)}
                        >
                          <X size={16} />
                        </button>
                      </div>
                      <div className="document-routing-editor-grid">
                        <label>
                          Destination *
                          <input
                            value={routeEditor.destination}
                            maxLength={100}
                            required
                            onChange={(event) =>
                              setRouteEditor((current) =>
                                current
                                  ? { ...current, destination: event.target.value }
                                  : current,
                              )
                            }
                          />
                        </label>
                        <label>
                          Priority *
                          <select
                            value={routeEditor.priority}
                            onChange={(event) =>
                              setRouteEditor((current) =>
                                current
                                  ? {
                                      ...current,
                                      priority: event.target.value as 'High' | 'Standard',
                                    }
                                  : current,
                              )
                            }
                          >
                            <option value="Standard">Standard</option>
                            <option value="High">High</option>
                          </select>
                        </label>
                        <label>
                          Assign to
                          <select
                            value={routeEditor.assignedTo}
                            onChange={(event) =>
                              setRouteEditor((current) =>
                                current
                                  ? { ...current, assignedTo: event.target.value }
                                  : current,
                              )
                            }
                          >
                            <option value="">Unassigned</option>
                            {assignees.map((assignee) => (
                              <option key={assignee.username} value={assignee.username}>
                                {assignee.displayName} / {assignee.role}
                              </option>
                            ))}
                          </select>
                        </label>
                        <label>
                          Due *
                          <input
                            type="datetime-local"
                            value={routeEditor.dueAt}
                            required
                            onChange={(event) =>
                              setRouteEditor((current) =>
                                current ? { ...current, dueAt: event.target.value } : current,
                              )
                            }
                          />
                        </label>
                        <label className="document-routing-editor-reason">
                          Routing reason *
                          <textarea
                            value={routeEditor.reason}
                            maxLength={250}
                            required
                            onChange={(event) =>
                              setRouteEditor((current) =>
                                current ? { ...current, reason: event.target.value } : current,
                              )
                            }
                          />
                        </label>
                      </div>
                      <div className="patient-document-panel-actions">
                        <button
                          className="cl-btn-primary"
                          type="submit"
                          disabled={
                            saving ||
                            routeEditor.destination.trim().length < 3 ||
                            routeEditor.reason.trim().length < 3 ||
                            !routeEditor.dueAt
                          }
                        >
                          <UserRoundCheck size={14} aria-hidden="true" />
                          {saving ? 'Saving…' : 'Save routing'}
                        </button>
                        <button
                          className="cl-btn-secondary"
                          type="button"
                          disabled={saving}
                          onClick={() => setRouteEditor(null)}
                        >
                          Cancel
                        </button>
                      </div>
                    </form>
                  )}

                  {completionEditor?.item.id === item.id && (
                    <form
                      className="patient-document-panel"
                      aria-labelledby={`complete-route-${item.id}`}
                      onSubmit={completeRoute}
                    >
                      <div className="patient-document-panel-heading">
                        <div>
                          <h3 id={`complete-route-${item.id}`}>
                            Complete routing work
                          </h3>
                          <p>
                            This closes only the internal handoff. The document review
                            state remains {stateLabel(item.reviewStatus)}.
                          </p>
                        </div>
                        <button
                          className="cl-icon-button"
                          type="button"
                          aria-label="Close completion form"
                          onClick={() => setCompletionEditor(null)}
                        >
                          <X size={16} />
                        </button>
                      </div>
                      <label>
                        Completion note *
                        <textarea
                          value={completionEditor.reason}
                          maxLength={250}
                          required
                          onChange={(event) =>
                            setCompletionEditor((current) =>
                              current ? { ...current, reason: event.target.value } : current,
                            )
                          }
                        />
                      </label>
                      <div className="patient-document-panel-actions">
                        <button
                          className="cl-btn-primary"
                          type="submit"
                          disabled={saving || completionEditor.reason.trim().length < 3}
                        >
                          <Check size={14} aria-hidden="true" />
                          {saving ? 'Completing…' : 'Complete routing work'}
                        </button>
                        <button
                          className="cl-btn-secondary"
                          type="button"
                          disabled={saving}
                          onClick={() => setCompletionEditor(null)}
                        >
                          Cancel
                        </button>
                      </div>
                    </form>
                  )}

                  {historyState?.documentId === item.id && (
                    <section
                      className="patient-document-panel"
                      aria-labelledby={`routing-history-${item.id}`}
                    >
                      <div className="patient-document-panel-heading">
                        <div>
                          <h3 id={`routing-history-${item.id}`}>Routing history</h3>
                          <p>Newest retained task event first.</p>
                        </div>
                        <button
                          className="cl-icon-button"
                          type="button"
                          aria-label="Close routing history"
                          onClick={() => setHistoryState(null)}
                        >
                          <X size={16} />
                        </button>
                      </div>
                      {historyState.status === 'loading' && (
                        <p className="cl-empty-text" aria-live="polite">
                          Loading routing history…
                        </p>
                      )}
                      {historyState.status === 'error' && (
                        <div className="error-banner" role="alert">
                          <span>{historyState.message}</span>
                          <button
                            className="link-button"
                            type="button"
                            onClick={() => void openHistory(item)}
                          >
                            Retry history
                          </button>
                        </div>
                      )}
                      {historyState.status === 'ready' && (
                        <>
                          <p className="patient-document-history-summary">
                            {historyState.data.returnedCount} of{' '}
                            {historyState.data.eventCount} events / current task v
                            {historyState.data.currentTaskVersion} /{' '}
                            {stateLabel(historyState.data.currentStatus)}
                          </p>
                          {historyState.data.events.length === 0 ? (
                            <p className="cl-empty-text">
                              No explicit routing events yet. This task is inferred from
                              the pending document.
                            </p>
                          ) : (
                            <ol className="patient-document-history-list">
                              {historyState.data.events.map((event) => (
                                <li key={event.eventId}>
                                  <div className="patient-document-history-title">
                                    <strong>{stateLabel(event.action)}</strong>
                                    <span>
                                      {event.actor} / {formatDateTime(event.occurredAt)}
                                    </span>
                                  </div>
                                  <p>{event.reason}</p>
                                  <p>
                                    {stateLabel(event.fromStatus)} <ArrowRight size={12} />{' '}
                                    {stateLabel(event.toStatus)} / task v{event.taskVersion}
                                  </p>
                                  <p>
                                    {event.fromDestination ?? 'Inferred'} <ArrowRight size={12} />{' '}
                                    {event.toDestination} / {event.toPriority} /{' '}
                                    {event.toAssignedTo ?? 'Unassigned'}
                                  </p>
                                  <p>
                                    Due {formatDateTime(event.dueAt)} / document v
                                    {event.documentVersion} / review{' '}
                                    {stateLabel(event.reviewStatus)}
                                    {event.contentHash
                                      ? ` / hash ${event.contentHash}`
                                      : ''}
                                  </p>
                                </li>
                              ))}
                            </ol>
                          )}
                        </>
                      )}
                    </section>
                  )}
                </article>
              ))}
            </section>
          )}

          <nav className="document-routing-pagination" aria-label="Document routing pages">
            <button
              className="cl-btn-secondary"
              type="button"
              disabled={!canGoBack}
              onClick={() =>
                setFilters((current) => ({
                  ...current,
                  offset: Math.max(0, offset - PAGE_SIZE),
                }))
              }
            >
              <ChevronLeft size={14} aria-hidden="true" />
              Previous
            </button>
            <span>
              {queue.totalCount === 0
                ? 'No results'
                : `${offset + 1}–${offset + queue.returnedCount} of ${queue.totalCount}`}
            </span>
            <button
              className="cl-btn-secondary"
              type="button"
              disabled={!canGoForward}
              onClick={() =>
                setFilters((current) => ({
                  ...current,
                  offset: offset + PAGE_SIZE,
                }))
              }
            >
              Next
              <ChevronRight size={14} aria-hidden="true" />
            </button>
          </nav>
        </>
      )}
    </div>
  )
}
