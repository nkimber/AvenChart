import { useCallback, useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import {
  Download,
  ExternalLink,
  FileText,
  FileUp,
  FolderOpen,
  Link2,
  Plus,
  RefreshCw,
  StickyNote,
  X,
} from 'lucide-react'
import {
  createPatientBinaryDocument,
  createPatientDocument,
  createPatientExternalLinkDocument,
  downloadPatientDocument,
  getPatientDocumentCategoryOptions,
  getPatientDocuments,
  isRequestCancellation,
  searchEncounters,
  type EncounterListItem,
  type PatientDocumentCategoryOptionsResponse,
  type PatientDocumentItem,
  type PatientDocumentsResponse,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { PatientOutletContext } from './PatientShell.tsx'

type IntakeMode = 'note' | 'file' | 'link'

type WorkspaceData = {
  documents: PatientDocumentsResponse
  options: PatientDocumentCategoryOptionsResponse
  encounters: EncounterListItem[]
}

type AsyncState<T> =
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; message: string }

type IntakeDraft = {
  name: string
  categoryId: string
  docDate: string
  encounter: string
  notes: string
  content: string
  url: string
}

const TODAY = new Date().toISOString().slice(0, 10)

function blankDraft(): IntakeDraft {
  return {
    name: '',
    categoryId: '3',
    docDate: TODAY,
    encounter: '',
    notes: '',
    content: '',
    url: '',
  }
}

function formatBytes(value?: number | null) {
  if (value === null || value === undefined) return 'Size unavailable'
  if (value < 1024) return `${value} B`
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`
  return `${(value / (1024 * 1024)).toFixed(1)} MB`
}

function readFileAsBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.addEventListener('load', () => {
      const value = typeof reader.result === 'string' ? reader.result : ''
      const separator = value.indexOf(',')
      if (separator < 0) {
        reject(new Error('The selected file could not be encoded.'))
        return
      }
      resolve(value.slice(separator + 1))
    })
    reader.addEventListener('error', () => {
      reject(new Error('The selected file could not be read.'))
    })
    reader.readAsDataURL(file)
  })
}

function documentKind(item: PatientDocumentItem) {
  if (item.storageMethod === 'web_url') return 'External link'
  if (item.contentPreview?.startsWith('Binary document:')) {
    return item.mimetype || 'Stored file'
  }
  if (item.mimetype === 'text/plain') return 'Clinical note'
  return item.mimetype || 'Stored file'
}

export default function PatientDocuments() {
  const { session, patientId } = useOutletContext<PatientOutletContext>()
  const [state, setState] = useState<AsyncState<WorkspaceData>>({
    status: 'loading',
  })
  const [showIntake, setShowIntake] = useState(false)
  const [mode, setMode] = useState<IntakeMode>('note')
  const [draft, setDraft] = useState<IntakeDraft>(blankDraft)
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [fileInputKey, setFileInputKey] = useState(0)
  const [mutationStatus, setMutationStatus] = useState<
    'idle' | 'saving' | 'error'
  >('idle')
  const [mutationError, setMutationError] = useState('')
  const [downloadingId, setDownloadingId] = useState<number | null>(null)
  const [recentDocumentId, setRecentDocumentId] = useState<number | null>(null)

  const loadWorkspace = useCallback(
    async (signal?: AbortSignal) => {
      setState({ status: 'loading' })
      try {
        const [documents, options, encounters] = await Promise.all([
          getPatientDocuments(session.sessionId, patientId, signal),
          getPatientDocumentCategoryOptions(session.sessionId, signal),
          searchEncounters(
            session.sessionId,
            {
              patientId,
              fromDate: '1900-01-01',
              limit: 100,
            },
            signal,
          ),
        ])
        setState({
          status: 'ready',
          data: {
            documents,
            options,
            encounters: encounters.encounters,
          },
        })
        setDraft((current) => ({
          ...current,
          categoryId:
            options.categories.some(
              (category) => String(category.id) === current.categoryId,
            )
              ? current.categoryId
              : String(options.categories[0]?.id ?? ''),
        }))
      } catch (error) {
        if (isRequestCancellation(error)) return
        setState({
          status: 'error',
          message:
            error instanceof Error
              ? error.message
              : 'Patient documents could not be loaded.',
        })
      }
    },
    [patientId, session.sessionId],
  )

  useEffect(() => {
    const controller = new AbortController()
    void loadWorkspace(controller.signal)
    return () => controller.abort()
  }, [loadWorkspace])

  function setDraftField<K extends keyof IntakeDraft>(
    field: K,
    value: IntakeDraft[K],
  ) {
    setDraft((current) => ({ ...current, [field]: value }))
    if (mutationStatus === 'error') {
      setMutationStatus('idle')
      setMutationError('')
    }
  }

  function resetIntake(close = false) {
    setDraft(blankDraft())
    setSelectedFile(null)
    setFileInputKey((current) => current + 1)
    setMutationStatus('idle')
    setMutationError('')
    if (close) setShowIntake(false)
  }

  function chooseMode(nextMode: IntakeMode) {
    setMode(nextMode)
    setMutationStatus('idle')
    setMutationError('')
  }

  function handleFileSelection(file: File | null) {
    if (!file || state.status !== 'ready') {
      setSelectedFile(null)
      return
    }
    if (file.size > state.data.options.maxFileSizeBytes) {
      setSelectedFile(null)
      setFileInputKey((current) => current + 1)
      setMutationStatus('error')
      setMutationError(
        `${file.name} is ${formatBytes(file.size)}. The protected document service accepts files up to ${formatBytes(state.data.options.maxFileSizeBytes)}.`,
      )
      return
    }
    setSelectedFile(file)
    setDraft((current) => ({
      ...current,
      name: current.name.trim() ? current.name : file.name,
    }))
    setMutationStatus('idle')
    setMutationError('')
  }

  async function handleCreate(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (state.status !== 'ready') return

    const categoryId = Number(draft.categoryId)
    const encounter = draft.encounter ? Number(draft.encounter) : null
    if (!Number.isInteger(categoryId) || categoryId <= 0) {
      setMutationStatus('error')
      setMutationError('Choose a filing category.')
      return
    }

    setMutationStatus('saving')
    setMutationError('')
    try {
      const shared = {
        patientId,
        categoryId,
        name: draft.name.trim(),
        docDate: draft.docDate,
        encounter,
        notes: draft.notes.trim() || null,
      }
      const result =
        mode === 'note'
          ? await createPatientDocument(session.sessionId, {
              ...shared,
              content: draft.content.trim(),
            })
          : mode === 'file'
            ? await (async () => {
                if (!selectedFile) {
                  throw new Error('Choose a file to upload.')
                }
                if (selectedFile.size > state.data.options.maxFileSizeBytes) {
                  throw new Error(
                    `Choose a file no larger than ${formatBytes(state.data.options.maxFileSizeBytes)}.`,
                  )
                }
                return createPatientBinaryDocument(session.sessionId, {
                  ...shared,
                  fileName: selectedFile.name,
                  mimetype:
                    selectedFile.type.trim() || 'application/octet-stream',
                  contentBase64: await readFileAsBase64(selectedFile),
                })
              })()
            : await (async () => {
                let link: URL
                try {
                  link = new URL(draft.url.trim())
                } catch {
                  throw new Error('Enter a complete http or https URL.')
                }
                if (!['http:', 'https:'].includes(link.protocol)) {
                  throw new Error('External document links must use http or https.')
                }
                return createPatientExternalLinkDocument(session.sessionId, {
                  ...shared,
                  url: link.toString(),
                })
              })()

      setState({
        status: 'ready',
        data: { ...state.data, documents: result.detail },
      })
      setRecentDocumentId(result.id)
      resetIntake(true)
      showToast(
        mode === 'note'
          ? 'Clinical note filed.'
          : mode === 'file'
            ? 'Document uploaded.'
            : 'External document link filed.',
        'success',
      )
    } catch (error) {
      setMutationStatus('error')
      setMutationError(
        error instanceof Error
          ? error.message
          : 'The document could not be filed.',
      )
    }
  }

  async function downloadDocument(item: PatientDocumentItem) {
    setDownloadingId(item.id)
    try {
      const file = await downloadPatientDocument(
        session.sessionId,
        item.id,
        item.fileName || item.name,
      )
      const objectUrl = URL.createObjectURL(file.blob)
      const link = document.createElement('a')
      link.href = objectUrl
      link.download = file.fileName
      link.style.display = 'none'
      document.body.append(link)
      link.click()
      link.remove()
      window.setTimeout(() => URL.revokeObjectURL(objectUrl), 0)
      showToast(`${file.fileName} downloaded.`, 'success')
    } catch (error) {
      showToast(
        error instanceof Error
          ? error.message
          : 'The document could not be downloaded.',
        'error',
      )
    } finally {
      setDownloadingId(null)
    }
  }

  if (state.status === 'loading') {
    return (
      <div className="clinician-page">
        <div className="cl-card">
          <div className="skeleton-list">
            {[0, 1, 2].map((item) => (
              <div
                key={item}
                className="skeleton-row"
                style={{ height: 72 }}
              />
            ))}
          </div>
        </div>
      </div>
    )
  }

  if (state.status === 'error') {
    return (
      <div className="clinician-page">
        <div className="error-banner" role="alert">
          <span>{state.message}</span>
          <button
            className="cl-link"
            type="button"
            onClick={() => void loadWorkspace()}
          >
            Retry
          </button>
        </div>
      </div>
    )
  }

  const { documents, options, encounters } = state.data

  return (
    <div className="clinician-page patient-documents-workspace">
      <section className="document-workspace-brief" aria-labelledby="documents-heading">
        <div>
          <span className="document-workspace-eyebrow">Protected chart files</span>
          <h2 id="documents-heading">Document register</h2>
          <p>
            File notes, local uploads, and external web links in this patient
            chart. Every new item remains pending review.
          </p>
        </div>
        <div className="document-workspace-brief-actions">
          <span className="document-workspace-count">
            <strong>{documents.count}</strong>
            active {documents.count === 1 ? 'document' : 'documents'}
          </span>
          <button
            className="cl-btn-primary"
            type="button"
            onClick={() => setShowIntake((current) => !current)}
            aria-expanded={showIntake}
            aria-controls="patient-document-intake"
          >
            {showIntake ? <X size={16} /> : <Plus size={16} />}
            {showIntake ? 'Close intake' : 'Add document'}
          </button>
        </div>
      </section>

      {showIntake && (
        <section
          className="cl-card patient-document-intake"
          id="patient-document-intake"
          aria-labelledby="patient-document-intake-heading"
        >
          <div className="patient-document-intake-heading">
            <div>
              <span className="document-workspace-eyebrow">New chart item</span>
              <h2 id="patient-document-intake-heading">Choose how to file it</h2>
            </div>
            <p>
              Files are stored by the local modernized service. Malware
              scanning and production object-storage controls are not part of
              this environment.
            </p>
          </div>

          <div className="patient-document-mode-switch" aria-label="Document type">
            <button
              type="button"
              aria-pressed={mode === 'note'}
              onClick={() => chooseMode('note')}
            >
              <StickyNote size={17} />
              <span>
                <strong>Clinical note</strong>
                <small>Store text in the chart</small>
              </span>
            </button>
            <button
              type="button"
              aria-pressed={mode === 'file'}
              onClick={() => chooseMode('file')}
            >
              <FileUp size={17} />
              <span>
                <strong>Upload file</strong>
                <small>Up to {formatBytes(options.maxFileSizeBytes)}</small>
              </span>
            </button>
            <button
              type="button"
              aria-pressed={mode === 'link'}
              onClick={() => chooseMode('link')}
            >
              <Link2 size={17} />
              <span>
                <strong>External link</strong>
                <small>HTTP or HTTPS only</small>
              </span>
            </button>
          </div>

          <form className="patient-document-intake-form" onSubmit={handleCreate}>
            <div className="patient-document-intake-grid">
              <div className="field patient-document-name-field">
                <label className="label" htmlFor="document-name">
                  Document name *
                </label>
                <input
                  id="document-name"
                  className="input"
                  value={draft.name}
                  onChange={(event) =>
                    setDraftField('name', event.target.value)
                  }
                  maxLength={255}
                  required
                />
              </div>
              <div className="field">
                <label className="label" htmlFor="document-category">
                  Filing category *
                </label>
                <select
                  id="document-category"
                  className="select"
                  value={draft.categoryId}
                  onChange={(event) =>
                    setDraftField('categoryId', event.target.value)
                  }
                  required
                >
                  {options.categories.map((category) => (
                    <option key={category.id} value={category.id}>
                      {category.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="field">
                <label className="label" htmlFor="document-date">
                  Document date *
                </label>
                <input
                  id="document-date"
                  className="input"
                  type="date"
                  value={draft.docDate}
                  onChange={(event) =>
                    setDraftField('docDate', event.target.value)
                  }
                  required
                />
              </div>
              <div className="field">
                <label className="label" htmlFor="document-encounter">
                  Related encounter
                </label>
                <select
                  id="document-encounter"
                  className="select"
                  value={draft.encounter}
                  onChange={(event) =>
                    setDraftField('encounter', event.target.value)
                  }
                >
                  <option value="">No encounter link</option>
                  {encounters.map((encounter) => (
                    <option key={encounter.encounter} value={encounter.encounter}>
                      {encounter.date} · {encounter.reason || 'Encounter'} · #
                      {encounter.encounter}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            {mode === 'note' && (
              <div className="field">
                <label className="label" htmlFor="document-content">
                  Note content *
                </label>
                <textarea
                  id="document-content"
                  className="textarea patient-document-content-input"
                  value={draft.content}
                  onChange={(event) =>
                    setDraftField('content', event.target.value)
                  }
                  maxLength={200_000}
                  required
                />
              </div>
            )}

            {mode === 'file' && (
              <div className="field">
                <label className="label" htmlFor="document-file">
                  Document file *
                </label>
                <input
                  key={fileInputKey}
                  id="document-file"
                  className="patient-document-file-input"
                  type="file"
                  onChange={(event) =>
                    handleFileSelection(event.target.files?.[0] ?? null)
                  }
                  required
                />
                <label className="patient-document-file-picker" htmlFor="document-file">
                  <FileUp size={22} aria-hidden="true" />
                  <span>
                    <strong>
                      {selectedFile ? selectedFile.name : 'Choose a local file'}
                    </strong>
                    <small>
                      {selectedFile
                        ? `${formatBytes(selectedFile.size)} · ${selectedFile.type || 'application/octet-stream'}`
                        : `Maximum ${formatBytes(options.maxFileSizeBytes)}`}
                    </small>
                  </span>
                </label>
              </div>
            )}

            {mode === 'link' && (
              <div className="field">
                <label className="label" htmlFor="document-url">
                  External document URL *
                </label>
                <input
                  id="document-url"
                  className="input"
                  type="url"
                  value={draft.url}
                  onChange={(event) =>
                    setDraftField('url', event.target.value)
                  }
                  placeholder="https://"
                  maxLength={2_000}
                  required
                />
                <span className="field-hint">
                  This records a link; it does not copy or verify the external
                  content.
                </span>
              </div>
            )}

            <div className="field">
              <label className="label" htmlFor="document-notes">
                Filing notes
              </label>
              <textarea
                id="document-notes"
                className="textarea"
                value={draft.notes}
                onChange={(event) =>
                  setDraftField('notes', event.target.value)
                }
                maxLength={2_000}
              />
            </div>

            {mutationStatus === 'error' && (
              <div className="cl-inline-error" role="alert">
                {mutationError}
              </div>
            )}

            <div className="patient-document-intake-actions">
              <button
                className="cl-btn-primary"
                type="submit"
                disabled={
                  mutationStatus === 'saving' ||
                  (mode === 'file' && !selectedFile)
                }
              >
                {mode === 'file' ? <FileUp size={16} /> : <Plus size={16} />}
                {mutationStatus === 'saving'
                  ? 'Filing…'
                  : mode === 'note'
                    ? 'File clinical note'
                    : mode === 'file'
                      ? 'Upload document'
                      : 'File external link'}
              </button>
              <button
                className="cl-btn-secondary"
                type="button"
                disabled={mutationStatus === 'saving'}
                onClick={() => resetIntake(true)}
              >
                Cancel
              </button>
            </div>
          </form>
        </section>
      )}

      <section className="cl-card patient-document-register" aria-label="Filed documents">
        <div className="cl-card-header">
          <div>
            <h2 className="cl-card-title">Filed documents</h2>
            <p className="cl-empty-text">
              Dataset {documents.datasetId} · version {documents.datasetVersion}
            </p>
          </div>
          <button
            className="cl-btn-secondary"
            type="button"
            onClick={() => void loadWorkspace()}
          >
            <RefreshCw size={14} />
            Refresh
          </button>
        </div>

        {documents.documents.length === 0 ? (
          <div className="cl-search-empty-state">
            <FolderOpen size={40} aria-hidden="true" />
            <p>No active documents are filed for this patient.</p>
            <button
              className="cl-link"
              type="button"
              onClick={() => setShowIntake(true)}
            >
              Add the first document
            </button>
          </div>
        ) : (
          <div className="patient-document-register-list">
            {documents.documents.map((item) => (
              <article
                className={`patient-document-register-item${recentDocumentId === item.id ? ' is-recent' : ''}`}
                key={item.id}
              >
                <div className="patient-document-register-icon" aria-hidden="true">
                  {item.storageMethod === 'web_url' ? (
                    <Link2 size={20} />
                  ) : (
                    <FileText size={20} />
                  )}
                </div>
                <div className="patient-document-register-main">
                  <div className="patient-document-register-title">
                    <div>
                      <h3>{item.name}</h3>
                      <p>
                        {item.categoryName} · {documentKind(item)}
                      </p>
                    </div>
                    <div className="patient-document-register-tags">
                      <span className="cl-badge cl-badge-muted">
                        {item.reviewStatus}
                      </span>
                      <span className="cl-badge cl-badge-muted">
                        {item.versionLabel}
                      </span>
                      {recentDocumentId === item.id && (
                        <span className="cl-badge cl-badge-green">Just filed</span>
                      )}
                    </div>
                  </div>
                  <dl className="patient-document-register-facts">
                    <div>
                      <dt>Document date</dt>
                      <dd>{item.docDate}</dd>
                    </div>
                    <div>
                      <dt>Encounter</dt>
                      <dd>{item.encounter ? `#${item.encounter}` : 'Not linked'}</dd>
                    </div>
                    <div>
                      <dt>File</dt>
                      <dd>
                        {item.fileName || 'Link record'} · {formatBytes(item.sizeBytes)}
                      </dd>
                    </div>
                  </dl>
                  {item.contentPreview && (
                    <p className="patient-document-register-preview">
                      {item.contentPreview}
                    </p>
                  )}
                  {item.notes && (
                    <p className="patient-document-register-note">
                      Filing note: {item.notes}
                    </p>
                  )}
                </div>
                <div className="patient-document-register-actions">
                  {item.storageMethod === 'web_url' && item.url && (
                    <a
                      className="cl-btn-secondary"
                      href={item.url}
                      target="_blank"
                      rel="noreferrer"
                    >
                      <ExternalLink size={14} />
                      Open link
                    </a>
                  )}
                  {item.canDownload && (
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      disabled={downloadingId === item.id}
                      onClick={() => void downloadDocument(item)}
                      aria-label={`Download ${item.name}`}
                    >
                      <Download size={14} />
                      {downloadingId === item.id ? 'Downloading…' : 'Download'}
                    </button>
                  )}
                </div>
              </article>
            ))}
          </div>
        )}
      </section>
    </div>
  )
}
