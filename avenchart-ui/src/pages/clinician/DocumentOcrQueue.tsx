// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Link, useOutletContext, useSearchParams } from 'react-router-dom'
import {
  ArrowRight,
  Check,
  ChevronLeft,
  ChevronRight,
  Clock,
  History,
  PencilLine,
  Play,
  RefreshCw,
  ScanText,
  Search,
  TriangleAlert,
  X,
} from 'lucide-react'
import {
  completePatientDocumentOcr,
  correctPatientDocumentOcr,
  failPatientDocumentOcr,
  getPatientDocumentOcrHistory,
  getPatientDocumentOcrQueue,
  startPatientDocumentOcr,
  type PatientDocumentOcrHistoryResponse,
  type PatientDocumentOcrQueueFilters,
  type PatientDocumentOcrQueueItem,
  type PatientDocumentOcrQueueResponse,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

type QueueState =
  | { status: 'loading' }
  | { status: 'ready'; data: PatientDocumentOcrQueueResponse }
  | { status: 'error'; message: string; data?: PatientDocumentOcrQueueResponse }

type HistoryState =
  | { status: 'loading'; documentId: number }
  | { status: 'ready'; documentId: number; data: PatientDocumentOcrHistoryResponse }
  | { status: 'error'; documentId: number; message: string }
  | null

type OcrAction = 'start' | 'fail' | 'complete' | 'correct'

type ActionEditor = {
  action: OcrAction
  item: PatientDocumentOcrQueueItem
  reason: string
  extractedText: string
}

const PAGE_SIZE = 10

function statusFromSearch(value: string | null): PatientDocumentOcrQueueFilters['status'] {
  return value === 'queued' ||
    value === 'running' ||
    value === 'failed' ||
    value === 'completed' ||
    value === 'all'
    ? value
    : 'active'
}

function formatDateTime(value?: string | null) {
  if (!value) return 'Not recorded'
  const parsed = new Date(value)
  return Number.isNaN(parsed.valueOf()) ? value : parsed.toLocaleString()
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

function editorTitle(action: OcrAction) {
  switch (action) {
    case 'start':
      return 'Start OCR work'
    case 'fail':
      return 'Record OCR failure'
    case 'complete':
      return 'Complete OCR extraction'
    case 'correct':
      return 'Correct extracted text'
  }
}

function editorSubmitLabel(action: OcrAction, saving: boolean) {
  if (saving) return 'Saving…'
  switch (action) {
    case 'start':
      return 'Start OCR'
    case 'fail':
      return 'Record failure'
    case 'complete':
      return 'Complete OCR'
    case 'correct':
      return 'Save correction'
  }
}

export default function DocumentOcrQueue() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const [searchParams] = useSearchParams()
  const initialFilters: PatientDocumentOcrQueueFilters = {
    patientId: searchParams.get('patientId')?.trim() || undefined,
    status: statusFromSearch(searchParams.get('status')),
    limit: PAGE_SIZE,
    offset: 0,
  }
  const [draftFilters, setDraftFilters] =
    useState<PatientDocumentOcrQueueFilters>(initialFilters)
  const [filters, setFilters] =
    useState<PatientDocumentOcrQueueFilters>(initialFilters)
  const [queueState, setQueueState] = useState<QueueState>({ status: 'loading' })
  const [editor, setEditor] = useState<ActionEditor | null>(null)
  const [historyState, setHistoryState] = useState<HistoryState>(null)
  const [saving, setSaving] = useState(false)

  const loadQueue = useCallback(
    async (signal?: AbortSignal) => {
      setQueueState({ status: 'loading' })
      try {
        const data = await getPatientDocumentOcrQueue(
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
              : 'The document OCR queue could not be loaded.',
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

  const queue =
    queueState.status === 'ready'
      ? queueState.data
      : queueState.status === 'error'
        ? queueState.data
        : undefined
  const offset = filters.offset ?? 0
  const canGoBack = offset > 0
  const canGoForward = Boolean(queue && offset + queue.returnedCount < queue.totalCount)

  function closePanels() {
    setEditor(null)
    setHistoryState(null)
  }

  function applyFilters(event: FormEvent) {
    event.preventDefault()
    setFilters({ ...draftFilters, offset: 0, limit: PAGE_SIZE })
    closePanels()
  }

  function resetFilters() {
    const reset: PatientDocumentOcrQueueFilters = {
      status: 'active',
      limit: PAGE_SIZE,
      offset: 0,
    }
    setDraftFilters(reset)
    setFilters(reset)
    closePanels()
  }

  async function openEditor(action: OcrAction, item: PatientDocumentOcrQueueItem) {
    setHistoryState(null)
    if (action !== 'correct') {
      setEditor({
        action,
        item,
        reason: '',
        extractedText: '',
      })
      return
    }

    setHistoryState({ status: 'loading', documentId: item.id })
    try {
      const history = await getPatientDocumentOcrHistory(
        session.sessionId,
        item.id,
      )
      setHistoryState(null)
      setEditor({
        action,
        item: { ...item, taskVersion: history.currentTaskVersion },
        reason: '',
        extractedText: history.currentExtractedText ?? '',
      })
    } catch (error) {
      setHistoryState({
        status: 'error',
        documentId: item.id,
        message:
          error instanceof Error
            ? error.message
            : 'Current OCR text could not be loaded.',
      })
    }
  }

  async function openHistory(item: PatientDocumentOcrQueueItem) {
    setEditor(null)
    setHistoryState({ status: 'loading', documentId: item.id })
    try {
      const data = await getPatientDocumentOcrHistory(
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
            : 'OCR history could not be loaded.',
      })
    }
  }

  async function submitAction(event: FormEvent) {
    event.preventDefault()
    if (!editor || saving) return

    setSaving(true)
    try {
      const reason = editor.reason.trim()
      const expectedTaskVersion = editor.item.taskVersion
      if (editor.action === 'start') {
        await startPatientDocumentOcr(session.sessionId, editor.item.id, {
          expectedTaskVersion,
          reason,
        })
      } else if (editor.action === 'fail') {
        await failPatientDocumentOcr(session.sessionId, editor.item.id, {
          expectedTaskVersion,
          reason,
        })
      } else if (editor.action === 'complete') {
        await completePatientDocumentOcr(session.sessionId, editor.item.id, {
          expectedTaskVersion,
          extractedText: editor.extractedText.trim(),
          reason,
        })
      } else {
        await correctPatientDocumentOcr(session.sessionId, editor.item.id, {
          expectedTaskVersion,
          extractedText: editor.extractedText.trim(),
          reason,
        })
      }

      showToast(
        editor.action === 'start'
          ? 'OCR work started.'
          : editor.action === 'fail'
            ? 'OCR failure recorded.'
            : editor.action === 'complete'
              ? 'OCR extraction completed.'
              : 'OCR correction retained.',
        'success',
      )
      setEditor(null)
      await loadQueue()
    } catch (error) {
      showToast(
        error instanceof Error ? error.message : 'The OCR change could not be saved.',
        'error',
      )
      await loadQueue()
    } finally {
      setSaving(false)
    }
  }

  const editorIsValid = Boolean(
    editor &&
      editor.reason.trim().length >= 3 &&
      (editor.action === 'start' ||
        editor.action === 'fail' ||
        editor.extractedText.trim().length > 0),
  )

  return (
    <div className="clinician-page document-routing-page document-ocr-page">
      <div className="clinician-page-header">
        <div>
          <p className="clinician-page-eyebrow">Document operations</p>
          <h1 className="clinician-page-title">OCR work queue</h1>
          <p className="clinician-page-subtitle">
            Control local extraction work, failures, verified text, and corrections
            without changing the protected source attachment.
          </p>
        </div>
        <button
          className="cl-btn-secondary"
          type="button"
          disabled={queueState.status === 'loading'}
          onClick={() => void loadQueue()}
        >
          <RefreshCw size={15} aria-hidden="true" />
          Refresh
        </button>
      </div>

      <div className="document-ocr-boundary" role="note">
        <ScanText size={18} aria-hidden="true" />
        <div>
          <strong>Local workflow boundary</strong>
          <p>
            This queue retains staff-entered or locally produced OCR evidence. It does
            not send patient documents to an external OCR vendor.
          </p>
        </div>
      </div>

      <section className="document-routing-metrics" aria-label="Document OCR counts">
        {[
          ['Active', queue?.counts.active],
          ['Ready', queue?.counts.queued],
          ['Running', queue?.counts.running],
          ['Failed', queue?.counts.failed],
          ['Completed', queue?.counts.completed],
        ].map(([label, value]) => (
          <div className="document-routing-metric" key={String(label)}>
            <span>{label}</span>
            <strong>{value ?? '—'}</strong>
          </div>
        ))}
      </section>

      <form className="document-routing-filters" onSubmit={applyFilters}>
        <label className="document-routing-search">
          Search documents
          <span>
            <Search size={15} aria-hidden="true" />
            <input
              type="search"
              value={draftFilters.query ?? ''}
              placeholder="Patient, document, source, or extracted text"
              onChange={(event) =>
                setDraftFilters((current) => ({
                  ...current,
                  query: event.target.value || undefined,
                }))
              }
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
                status: event.target.value as PatientDocumentOcrQueueFilters['status'],
              }))
            }
          >
            <option value="active">Active</option>
            <option value="queued">Ready</option>
            <option value="running">Running</option>
            <option value="failed">Failed</option>
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
                  (event.target.value || undefined) as
                    | PatientDocumentOcrQueueFilters['priority']
                    | undefined,
              }))
            }
          >
            <option value="">All priorities</option>
            <option value="High">High</option>
            <option value="Standard">Standard</option>
          </select>
        </label>
        <label>
          Patient ID
          <input
            value={draftFilters.patientId ?? ''}
            placeholder="Optional patient scope"
            onChange={(event) =>
              setDraftFilters((current) => ({
                ...current,
                patientId: event.target.value || undefined,
              }))
            }
          />
        </label>
        <div className="document-routing-filter-actions">
          <button className="cl-btn-secondary" type="button" onClick={resetFilters}>
            Reset
          </button>
          <button className="cl-btn-primary" type="submit">
            Apply filters
          </button>
        </div>
      </form>

      {queueState.status === 'error' && (
        <div className="error-banner" role="alert">
          <span>{queueState.message}</span>
          <button className="link-button" type="button" onClick={() => void loadQueue()}>
            Retry
          </button>
        </div>
      )}

      {queueState.status === 'loading' && !queue && (
        <div className="skeleton-list" aria-live="polite">
          {[0, 1, 2].map((item) => (
            <div className="skeleton-row" style={{ height: 180 }} key={item} />
          ))}
        </div>
      )}

      {queue && (
        <>
          <div className="document-routing-result-summary" aria-live="polite">
            <span>
              {queue.totalCount} matching document
              {queue.totalCount === 1 ? '' : 's'}
            </span>
            <span>
              {queue.counts.highPriority} active high priority / source{' '}
              {queue.datasetId} {queue.datasetVersion}
            </span>
          </div>

          {queue.items.length === 0 ? (
            <div className="cl-empty-state">
              <ScanText size={30} aria-hidden="true" />
              <h2>No OCR work matches these filters</h2>
              <p>Try another status, patient scope, or search phrase.</p>
            </div>
          ) : (
            <section className="document-routing-list" aria-label="Document OCR queue">
              {queue.items.map((item) => (
                <article
                  className={`document-routing-card document-ocr-card document-ocr-card-${item.queueStatus.toLowerCase().replaceAll(' ', '-')}`}
                  key={item.id}
                >
                  <div className="document-routing-card-heading">
                    <div>
                      <div className="document-routing-title-row">
                        <h2>{item.name}</h2>
                        <span
                          className={`document-routing-priority document-routing-priority-${item.priority.toLowerCase()}`}
                        >
                          {item.priority}
                        </span>
                        <span className={`document-ocr-status document-ocr-status-${item.queueStatus.toLowerCase().replaceAll(' ', '-')}`}>
                          {item.queueStatus}
                        </span>
                      </div>
                      <p>
                        <Link to={`/clinician/patients/${item.patientId}/documents`}>
                          {item.patientDisplayName}
                        </Link>{' '}
                        / {item.pubpid} / {item.categoryName}
                      </p>
                    </div>
                    <span className="document-routing-age">
                      <Clock size={14} aria-hidden="true" />
                      {formatAge(item.ageHours)}
                    </span>
                  </div>

                  <dl className="document-routing-facts">
                    <div>
                      <dt>Capture</dt>
                      <dd>
                        {item.captureSource} / {item.scanPageCount} page
                        {item.scanPageCount === 1 ? '' : 's'}
                      </dd>
                    </div>
                    <div>
                      <dt>OCR evidence</dt>
                      <dd>
                        {item.extractedTextLength > 0
                          ? `${item.extractedTextLength} retained characters`
                          : 'No retained text'}
                      </dd>
                    </div>
                    <div>
                      <dt>Versions</dt>
                      <dd>
                        Task v{item.taskVersion} / document v{item.documentVersion}
                      </dd>
                    </div>
                    <div>
                      <dt>Review</dt>
                      <dd>{stateLabel(item.reviewStatus)}</dd>
                    </div>
                    <div>
                      <dt>Last updated</dt>
                      <dd>{formatDateTime(item.lastUpdatedAt)}</dd>
                    </div>
                    <div>
                      <dt>Source file</dt>
                      <dd>{item.fileName ?? item.mimetype ?? 'Stored attachment'}</dd>
                    </div>
                  </dl>

                  {item.failureReason && (
                    <p className="document-ocr-failure">
                      <TriangleAlert size={14} aria-hidden="true" />
                      {item.failureReason}
                    </p>
                  )}
                  {item.extractedTextPreview && (
                    <blockquote className="document-ocr-preview">
                      {item.extractedTextPreview}
                    </blockquote>
                  )}

                  <div className="patient-document-actions">
                    {(item.queueStatus === 'Ready for OCR' ||
                      item.queueStatus === 'OCR failed') && (
                      <button
                        className="cl-btn-primary"
                        type="button"
                        onClick={() => void openEditor('start', item)}
                      >
                        <Play size={14} aria-hidden="true" />
                        {item.queueStatus === 'OCR failed' ? 'Retry OCR' : 'Start OCR'}
                      </button>
                    )}
                    {item.queueStatus === 'OCR running' && (
                      <>
                        <button
                          className="cl-btn-primary"
                          type="button"
                          onClick={() => void openEditor('complete', item)}
                        >
                          <Check size={14} aria-hidden="true" />
                          Complete OCR
                        </button>
                        <button
                          className="cl-btn-secondary"
                          type="button"
                          onClick={() => void openEditor('fail', item)}
                        >
                          <TriangleAlert size={14} aria-hidden="true" />
                          Record failure
                        </button>
                      </>
                    )}
                    {item.queueStatus === 'OCR complete' && (
                      <button
                        className="cl-btn-secondary"
                        type="button"
                        onClick={() => void openEditor('correct', item)}
                      >
                        <PencilLine size={14} aria-hidden="true" />
                        Correct extracted text
                      </button>
                    )}
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      onClick={() => void openHistory(item)}
                    >
                      <History size={14} aria-hidden="true" />
                      OCR history
                    </button>
                  </div>

                  {editor?.item.id === item.id && (
                    <form
                      className="patient-document-panel document-ocr-editor"
                      aria-labelledby={`ocr-editor-${item.id}`}
                      onSubmit={submitAction}
                    >
                      <div className="patient-document-panel-heading">
                        <div>
                          <h3 id={`ocr-editor-${item.id}`}>
                            {editorTitle(editor.action)}
                          </h3>
                          <p>
                            Task version {editor.item.taskVersion}. The authenticated
                            staff account is recorded automatically.
                          </p>
                        </div>
                        <button
                          className="cl-icon-button"
                          type="button"
                          aria-label="Close OCR form"
                          onClick={() => setEditor(null)}
                        >
                          <X size={16} />
                        </button>
                      </div>
                      {(editor.action === 'complete' || editor.action === 'correct') && (
                        <label>
                          Extracted text *
                          <textarea
                            className="document-ocr-textarea"
                            value={editor.extractedText}
                            maxLength={262_144}
                            required
                            onChange={(event) =>
                              setEditor((current) =>
                                current
                                  ? { ...current, extractedText: event.target.value }
                                  : current,
                              )
                            }
                          />
                        </label>
                      )}
                      <label>
                        {editor.action === 'fail'
                          ? 'Failure reason *'
                          : editor.action === 'correct'
                            ? 'Correction reason *'
                            : 'Work note *'}
                        <textarea
                          value={editor.reason}
                          maxLength={500}
                          required
                          onChange={(event) =>
                            setEditor((current) =>
                              current ? { ...current, reason: event.target.value } : current,
                            )
                          }
                        />
                      </label>
                      <div className="patient-document-panel-actions">
                        <button
                          className="cl-btn-primary"
                          type="submit"
                          disabled={saving || !editorIsValid}
                        >
                          {editorSubmitLabel(editor.action, saving)}
                        </button>
                        <button
                          className="cl-btn-secondary"
                          type="button"
                          disabled={saving}
                          onClick={() => setEditor(null)}
                        >
                          Cancel
                        </button>
                      </div>
                    </form>
                  )}

                  {historyState?.documentId === item.id && (
                    <section
                      className="patient-document-panel"
                      aria-labelledby={`ocr-history-${item.id}`}
                    >
                      <div className="patient-document-panel-heading">
                        <div>
                          <h3 id={`ocr-history-${item.id}`}>OCR history</h3>
                          <p>Newest retained lifecycle event first.</p>
                        </div>
                        <button
                          className="cl-icon-button"
                          type="button"
                          aria-label="Close OCR history"
                          onClick={() => setHistoryState(null)}
                        >
                          <X size={16} />
                        </button>
                      </div>
                      {historyState.status === 'loading' && (
                        <p className="cl-empty-text" aria-live="polite">
                          Loading OCR history…
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
                          {historyState.data.currentExtractedText && (
                            <details className="document-ocr-current-text">
                              <summary>
                                Current extracted text (
                                {historyState.data.currentExtractedText.length} characters)
                              </summary>
                              <pre>{historyState.data.currentExtractedText}</pre>
                            </details>
                          )}
                          {historyState.data.events.length === 0 ? (
                            <p className="cl-empty-text">
                              No explicit OCR events yet. This state is inferred from the
                              scanned document.
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
                                    {stateLabel(event.fromStatus)}{' '}
                                    <ArrowRight size={12} aria-hidden="true" />{' '}
                                    {stateLabel(event.toStatus)} / task v{event.taskVersion}
                                  </p>
                                  <p>
                                    Text {event.fromExtractedTextLength}{' '}
                                    <ArrowRight size={12} aria-hidden="true" />{' '}
                                    {event.toExtractedTextLength} characters / document v
                                    {event.documentVersion} / review{' '}
                                    {stateLabel(event.reviewStatus)}
                                  </p>
                                  {event.toExtractedTextPreview && (
                                    <p>{event.toExtractedTextPreview}</p>
                                  )}
                                  {event.toExtractedTextHash && (
                                    <p className="document-ocr-hash">
                                      SHA-256 {event.toExtractedTextHash}
                                    </p>
                                  )}
                                  {event.failureReason && (
                                    <p>Failure: {event.failureReason}</p>
                                  )}
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

          <nav className="document-routing-pagination" aria-label="Document OCR pages">
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
