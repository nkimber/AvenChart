import { useCallback, useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import {
  Download,
  ExternalLink,
  FileText,
  FileUp,
  Files,
  FolderOpen,
  History,
  Link2,
  Pencil,
  Plus,
  RefreshCw,
  Save,
  StickyNote,
  X,
} from 'lucide-react'
import {
  createPatientBinaryDocument,
  createPatientDocument,
  createPatientExternalLinkDocument,
  downloadPatientDocument,
  downloadPatientDocumentVersion,
  getPatientDocumentCategoryOptions,
  getPatientDocumentMetadataHistory,
  getPatientDocumentVersionHistory,
  getPatientDocuments,
  isRequestCancellation,
  searchEncounters,
  replacePatientDocumentBinaryContent,
  replacePatientDocumentContent,
  updatePatientDocumentMetadata,
  type EncounterListItem,
  type PatientDocumentCategoryOptionsResponse,
  type PatientDocumentItem,
  type PatientDocumentMetadataHistoryItem,
  type PatientDocumentMetadataHistoryResponse,
  type PatientDocumentVersionHistoryResponse,
  type PatientDocumentsResponse,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { PatientOutletContext } from './PatientShell.tsx'

type IntakeMode = 'note' | 'file' | 'link'
type ReplacementMode = 'text' | 'file'

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

type MetadataDraft = {
  name: string
  categoryId: string
  docDate: string
  encounter: string
  notes: string
  reason: string
}

type DocumentHistoryState =
  | { documentId: number; status: 'loading' }
  | {
      documentId: number
      status: 'ready'
      data: PatientDocumentMetadataHistoryResponse
    }
  | { documentId: number; status: 'error'; message: string }

type DocumentVersionState =
  | { documentId: number; status: 'loading' }
  | {
      documentId: number
      status: 'ready'
      data: PatientDocumentVersionHistoryResponse
    }
  | { documentId: number; status: 'error'; message: string }

type ReplacementDraft = {
  fileName: string
  content: string
  reason: string
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

function metadataDraftFor(item: PatientDocumentItem): MetadataDraft {
  return {
    name: item.name,
    categoryId: String(item.categoryId),
    docDate: item.docDate,
    encounter: item.encounter ? String(item.encounter) : '',
    notes: item.notes ?? '',
    reason: '',
  }
}

function displayEncounter(value?: number | null) {
  return value ? `Encounter #${value}` : 'No encounter link'
}

function displayMetadataValue(value?: string | null) {
  return value?.trim() || 'None'
}

function shortHash(value?: string | null) {
  if (!value) return 'Hash unavailable'
  return value.length > 16 ? `${value.slice(0, 16)}…` : value
}

function formatVersionTime(value: string) {
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString()
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

function MetadataChangeSummary({
  event,
}: {
  event: PatientDocumentMetadataHistoryItem
}) {
  const changes: Array<{ field: string; from: string; to: string }> = []
  if (event.changedFields.includes('category')) {
    changes.push({
      field: 'Category',
      from: event.fromCategoryName,
      to: event.toCategoryName,
    })
  }
  if (event.changedFields.includes('name')) {
    changes.push({ field: 'Name', from: event.fromName, to: event.toName })
  }
  if (event.changedFields.includes('documentDate')) {
    changes.push({
      field: 'Document date',
      from: event.fromDocDate,
      to: event.toDocDate,
    })
  }
  if (event.changedFields.includes('encounter')) {
    changes.push({
      field: 'Encounter',
      from: displayEncounter(event.fromEncounter),
      to: displayEncounter(event.toEncounter),
    })
  }
  if (event.changedFields.includes('notes')) {
    changes.push({
      field: 'Filing notes',
      from: displayMetadataValue(event.fromNotes),
      to: displayMetadataValue(event.toNotes),
    })
  }

  return (
    <dl className="patient-document-history-changes">
      {changes.map((change) => (
        <div key={change.field}>
          <dt>{change.field}</dt>
          <dd>
            <span>{change.from}</span>
            <span aria-hidden="true">→</span>
            <strong>{change.to}</strong>
          </dd>
        </div>
      ))}
    </dl>
  )
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
  const [editingDocumentId, setEditingDocumentId] = useState<number | null>(null)
  const [metadataDraft, setMetadataDraft] = useState<MetadataDraft | null>(null)
  const [metadataStatus, setMetadataStatus] = useState<
    'idle' | 'saving' | 'error'
  >('idle')
  const [metadataError, setMetadataError] = useState('')
  const [historyState, setHistoryState] =
    useState<DocumentHistoryState | null>(null)
  const [versionState, setVersionState] =
    useState<DocumentVersionState | null>(null)
  const [replacingDocumentId, setReplacingDocumentId] = useState<number | null>(
    null,
  )
  const [replacementMode, setReplacementMode] =
    useState<ReplacementMode>('text')
  const [replacementDraft, setReplacementDraft] =
    useState<ReplacementDraft | null>(null)
  const [replacementFile, setReplacementFile] = useState<File | null>(null)
  const [replacementFileInputKey, setReplacementFileInputKey] = useState(0)
  const [replacementStatus, setReplacementStatus] = useState<
    'idle' | 'saving' | 'error'
  >('idle')
  const [replacementError, setReplacementError] = useState('')
  const [versionDownloadingKey, setVersionDownloadingKey] = useState<
    string | null
  >(null)

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

  useEffect(() => {
    setEditingDocumentId(null)
    setMetadataDraft(null)
    setMetadataStatus('idle')
    setMetadataError('')
    setHistoryState(null)
    setVersionState(null)
    setReplacingDocumentId(null)
    setReplacementDraft(null)
    setReplacementFile(null)
    setReplacementStatus('idle')
    setReplacementError('')
  }, [patientId])

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

  function beginMetadataEdit(item: PatientDocumentItem) {
    if (editingDocumentId === item.id) {
      setEditingDocumentId(null)
      setMetadataDraft(null)
      setMetadataStatus('idle')
      setMetadataError('')
      return
    }
    closeContentReplacement()
    setVersionState(null)
    setEditingDocumentId(item.id)
    setMetadataDraft(metadataDraftFor(item))
    setMetadataStatus('idle')
    setMetadataError('')
  }

  function setMetadataField<K extends keyof MetadataDraft>(
    field: K,
    value: MetadataDraft[K],
  ) {
    setMetadataDraft((current) =>
      current ? { ...current, [field]: value } : current,
    )
    if (metadataStatus === 'error') {
      setMetadataStatus('idle')
      setMetadataError('')
    }
  }

  function metadataHasChanges(
    item: PatientDocumentItem,
    value: MetadataDraft | null,
  ) {
    if (!value) return false
    const nextEncounter = value.encounter ? Number(value.encounter) : null
    return (
      Number(value.categoryId) !== item.categoryId ||
      value.name.trim() !== item.name ||
      value.docDate !== item.docDate ||
      nextEncounter !== (item.encounter ?? null) ||
      (value.notes.trim() || null) !== (item.notes?.trim() || null)
    )
  }

  async function fetchMetadataHistory(documentId: number) {
    setHistoryState({ documentId, status: 'loading' })
    try {
      const history = await getPatientDocumentMetadataHistory(
        session.sessionId,
        documentId,
      )
      setHistoryState({ documentId, status: 'ready', data: history })
    } catch (error) {
      setHistoryState({
        documentId,
        status: 'error',
        message:
          error instanceof Error
            ? error.message
            : 'Filing history could not be loaded.',
      })
    }
  }

  function toggleMetadataHistory(documentId: number) {
    if (historyState?.documentId === documentId) {
      setHistoryState(null)
      return
    }
    setVersionState(null)
    void fetchMetadataHistory(documentId)
  }

  async function handleMetadataUpdate(
    event: React.FormEvent<HTMLFormElement>,
    item: PatientDocumentItem,
  ) {
    event.preventDefault()
    if (!metadataDraft || !metadataHasChanges(item, metadataDraft)) return
    if (!metadataDraft.reason.trim()) {
      setMetadataStatus('error')
      setMetadataError('Explain why the filing metadata is changing.')
      return
    }

    setMetadataStatus('saving')
    setMetadataError('')
    try {
      const result = await updatePatientDocumentMetadata(
        session.sessionId,
        item.id,
        {
          categoryId: Number(metadataDraft.categoryId),
          name: metadataDraft.name.trim(),
          docDate: metadataDraft.docDate,
          encounter: metadataDraft.encounter
            ? Number(metadataDraft.encounter)
            : null,
          notes: metadataDraft.notes.trim() || null,
          reason: metadataDraft.reason.trim(),
        },
      )
      setState((current) =>
        current.status === 'ready'
          ? {
              status: 'ready',
              data: { ...current.data, documents: result.detail },
            }
          : current,
      )
      setRecentDocumentId(result.id)
      setEditingDocumentId(null)
      setMetadataDraft(null)
      setMetadataStatus('idle')
      showToast('Document filing metadata updated.', 'success')
      await fetchMetadataHistory(item.id)
    } catch (error) {
      setMetadataStatus('error')
      setMetadataError(
        error instanceof Error
          ? error.message
          : 'Document filing metadata could not be updated.',
      )
    }
  }

  async function fetchVersionHistory(documentId: number) {
    setVersionState({ documentId, status: 'loading' })
    try {
      const history = await getPatientDocumentVersionHistory(
        session.sessionId,
        documentId,
      )
      setVersionState({ documentId, status: 'ready', data: history })
      return history
    } catch (error) {
      setVersionState({
        documentId,
        status: 'error',
        message:
          error instanceof Error
            ? error.message
            : 'Content version history could not be loaded.',
      })
      return null
    }
  }

  function toggleVersionHistory(documentId: number) {
    if (versionState?.documentId === documentId) {
      setVersionState(null)
      return
    }
    setHistoryState(null)
    void fetchVersionHistory(documentId)
  }

  function closeContentReplacement() {
    setReplacingDocumentId(null)
    setReplacementDraft(null)
    setReplacementFile(null)
    setReplacementFileInputKey((current) => current + 1)
    setReplacementStatus('idle')
    setReplacementError('')
  }

  function beginContentReplacement(item: PatientDocumentItem) {
    if (replacingDocumentId === item.id) {
      closeContentReplacement()
      return
    }
    setEditingDocumentId(null)
    setMetadataDraft(null)
    setMetadataStatus('idle')
    setMetadataError('')
    setHistoryState(null)
    setReplacingDocumentId(item.id)
    setReplacementMode(item.mimetype === 'text/plain' ? 'text' : 'file')
    setReplacementDraft({
      fileName: item.fileName || `${item.name}.txt`,
      content: '',
      reason: '',
    })
    setReplacementFile(null)
    setReplacementFileInputKey((current) => current + 1)
    setReplacementStatus('idle')
    setReplacementError('')
    if (
      versionState?.documentId !== item.id ||
      versionState.status !== 'ready'
    ) {
      void fetchVersionHistory(item.id)
    }
  }

  function setReplacementField<K extends keyof ReplacementDraft>(
    field: K,
    value: ReplacementDraft[K],
  ) {
    setReplacementDraft((current) =>
      current ? { ...current, [field]: value } : current,
    )
    if (replacementStatus === 'error') {
      setReplacementStatus('idle')
      setReplacementError('')
    }
  }

  function chooseReplacementMode(mode: ReplacementMode) {
    setReplacementMode(mode)
    setReplacementFile(null)
    setReplacementFileInputKey((current) => current + 1)
    setReplacementStatus('idle')
    setReplacementError('')
  }

  function handleReplacementFileSelection(file: File | null) {
    if (!file || state.status !== 'ready') {
      setReplacementFile(null)
      return
    }
    if (file.size > state.data.options.maxFileSizeBytes) {
      setReplacementFile(null)
      setReplacementFileInputKey((current) => current + 1)
      setReplacementStatus('error')
      setReplacementError(
        `${file.name} is ${formatBytes(file.size)}. Choose a replacement no larger than ${formatBytes(state.data.options.maxFileSizeBytes)}.`,
      )
      return
    }
    setReplacementFile(file)
    setReplacementDraft((current) =>
      current ? { ...current, fileName: file.name } : current,
    )
    setReplacementStatus('idle')
    setReplacementError('')
  }

  async function handleContentReplacement(
    event: React.FormEvent<HTMLFormElement>,
    item: PatientDocumentItem,
  ) {
    event.preventDefault()
    if (
      !replacementDraft ||
      versionState?.documentId !== item.id ||
      versionState.status !== 'ready'
    ) {
      setReplacementStatus('error')
      setReplacementError(
        'Load the current version history before replacing content.',
      )
      return
    }
    if (!replacementDraft.reason.trim()) {
      setReplacementStatus('error')
      setReplacementError('Explain why the protected content is changing.')
      return
    }

    setReplacementStatus('saving')
    setReplacementError('')
    try {
      const expectedVersion = versionState.data.currentVersion
      const result =
        replacementMode === 'text'
          ? await replacePatientDocumentContent(
              session.sessionId,
              item.id,
              {
                fileName: replacementDraft.fileName.trim(),
                content: replacementDraft.content.trim(),
                reason: replacementDraft.reason.trim(),
                expectedVersion,
              },
            )
          : await (async () => {
              if (!replacementFile) {
                throw new Error('Choose a replacement file.')
              }
              if (
                state.status === 'ready' &&
                replacementFile.size > state.data.options.maxFileSizeBytes
              ) {
                throw new Error(
                  `Choose a replacement no larger than ${formatBytes(state.data.options.maxFileSizeBytes)}.`,
                )
              }
              return replacePatientDocumentBinaryContent(
                session.sessionId,
                item.id,
                {
                  fileName: replacementDraft.fileName.trim(),
                  mimetype:
                    replacementFile.type.trim() || 'application/octet-stream',
                  contentBase64: await readFileAsBase64(replacementFile),
                  reason: replacementDraft.reason.trim(),
                  expectedVersion,
                },
              )
            })()

      setState((current) =>
        current.status === 'ready'
          ? {
              status: 'ready',
              data: { ...current.data, documents: result.detail },
            }
          : current,
      )
      setRecentDocumentId(result.id)
      closeContentReplacement()
      showToast('A new protected document version was filed.', 'success')
      await fetchVersionHistory(item.id)
    } catch (error) {
      setReplacementStatus('error')
      setReplacementError(
        error instanceof Error
          ? error.message
          : 'Protected document content could not be replaced.',
      )
      await fetchVersionHistory(item.id)
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

  async function downloadDocumentVersion(
    item: PatientDocumentItem,
    version: number,
    fileName?: string | null,
  ) {
    const downloadKey = `${item.id}-${version}`
    setVersionDownloadingKey(downloadKey)
    try {
      const file = await downloadPatientDocumentVersion(
        session.sessionId,
        item.id,
        version,
        fileName || `${item.name}-v${version}`,
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
      showToast(`${file.fileName} version ${version} downloaded.`, 'success')
    } catch (error) {
      showToast(
        error instanceof Error
          ? error.message
          : 'The document version could not be downloaded.',
        'error',
      )
    } finally {
      setVersionDownloadingKey(null)
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
                  <button
                    className="cl-btn-secondary"
                    type="button"
                    onClick={() => beginMetadataEdit(item)}
                    aria-expanded={editingDocumentId === item.id}
                    aria-controls={`document-metadata-edit-${item.id}`}
                  >
                    {editingDocumentId === item.id ? (
                      <X size={14} />
                    ) : (
                      <Pencil size={14} />
                    )}
                    {editingDocumentId === item.id ? 'Close edit' : 'Edit filing'}
                  </button>
                  <button
                    className="cl-btn-secondary"
                    type="button"
                    onClick={() => toggleMetadataHistory(item.id)}
                    aria-expanded={historyState?.documentId === item.id}
                    aria-controls={`document-metadata-history-${item.id}`}
                  >
                    <History size={14} />
                    Filing history
                  </button>
                  {item.storageMethod !== 'web_url' && (
                    <>
                      <button
                        className="cl-btn-secondary"
                        type="button"
                        onClick={() => toggleVersionHistory(item.id)}
                        aria-expanded={versionState?.documentId === item.id}
                        aria-controls={`document-version-history-${item.id}`}
                      >
                        <Files size={14} />
                        Content versions
                      </button>
                      <button
                        className="cl-btn-secondary"
                        type="button"
                        onClick={() => beginContentReplacement(item)}
                        aria-expanded={replacingDocumentId === item.id}
                        aria-controls={`document-content-replace-${item.id}`}
                      >
                        {replacingDocumentId === item.id ? (
                          <X size={14} />
                        ) : (
                          <FileUp size={14} />
                        )}
                        {replacingDocumentId === item.id
                          ? 'Close replacement'
                          : 'Replace content'}
                      </button>
                    </>
                  )}
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
                {editingDocumentId === item.id && metadataDraft && (
                  <form
                    className="patient-document-metadata-edit"
                    id={`document-metadata-edit-${item.id}`}
                    onSubmit={(event) => void handleMetadataUpdate(event, item)}
                  >
                    <div className="patient-document-panel-heading">
                      <div>
                        <span className="document-workspace-eyebrow">
                          Correct filing
                        </span>
                        <h4>Edit metadata and chart location</h4>
                      </div>
                      <p>
                        Content is unchanged. Material changes retain before,
                        after, reason, actor, and time evidence.
                      </p>
                    </div>
                    <div className="patient-document-metadata-grid">
                      <div className="field patient-document-name-field">
                        <label
                          className="label"
                          htmlFor={`document-edit-name-${item.id}`}
                        >
                          Document name *
                        </label>
                        <input
                          id={`document-edit-name-${item.id}`}
                          className="input"
                          value={metadataDraft.name}
                          onChange={(event) =>
                            setMetadataField('name', event.target.value)
                          }
                          maxLength={255}
                          required
                        />
                      </div>
                      <div className="field">
                        <label
                          className="label"
                          htmlFor={`document-edit-category-${item.id}`}
                        >
                          Filing category *
                        </label>
                        <select
                          id={`document-edit-category-${item.id}`}
                          className="select"
                          value={metadataDraft.categoryId}
                          onChange={(event) =>
                            setMetadataField('categoryId', event.target.value)
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
                        <label
                          className="label"
                          htmlFor={`document-edit-date-${item.id}`}
                        >
                          Document date *
                        </label>
                        <input
                          id={`document-edit-date-${item.id}`}
                          className="input"
                          type="date"
                          value={metadataDraft.docDate}
                          onChange={(event) =>
                            setMetadataField('docDate', event.target.value)
                          }
                          required
                        />
                      </div>
                      <div className="field">
                        <label
                          className="label"
                          htmlFor={`document-edit-encounter-${item.id}`}
                        >
                          Related encounter
                        </label>
                        <select
                          id={`document-edit-encounter-${item.id}`}
                          className="select"
                          value={metadataDraft.encounter}
                          onChange={(event) =>
                            setMetadataField('encounter', event.target.value)
                          }
                        >
                          <option value="">No encounter link</option>
                          {encounters.map((encounter) => (
                            <option
                              key={encounter.encounter}
                              value={encounter.encounter}
                            >
                              {encounter.date} ·{' '}
                              {encounter.reason || 'Encounter'} · #
                              {encounter.encounter}
                            </option>
                          ))}
                        </select>
                      </div>
                    </div>
                    <div className="patient-document-metadata-secondary-grid">
                      <div className="field">
                        <label
                          className="label"
                          htmlFor={`document-edit-notes-${item.id}`}
                        >
                          Filing notes
                        </label>
                        <textarea
                          id={`document-edit-notes-${item.id}`}
                          className="textarea"
                          value={metadataDraft.notes}
                          onChange={(event) =>
                            setMetadataField('notes', event.target.value)
                          }
                          maxLength={2_000}
                        />
                      </div>
                      <div className="field">
                        <label
                          className="label"
                          htmlFor={`document-edit-reason-${item.id}`}
                        >
                          Change reason *
                        </label>
                        <textarea
                          id={`document-edit-reason-${item.id}`}
                          className="textarea"
                          value={metadataDraft.reason}
                          onChange={(event) =>
                            setMetadataField('reason', event.target.value)
                          }
                          maxLength={250}
                          required
                        />
                      </div>
                    </div>
                    {metadataStatus === 'error' && (
                      <div className="cl-inline-error" role="alert">
                        {metadataError}
                      </div>
                    )}
                    <div className="patient-document-intake-actions">
                      <button
                        className="cl-btn-primary"
                        type="submit"
                        disabled={
                          metadataStatus === 'saving' ||
                          !metadataDraft.reason.trim() ||
                          !metadataHasChanges(item, metadataDraft)
                        }
                      >
                        <Save size={15} />
                        {metadataStatus === 'saving'
                          ? 'Saving…'
                          : 'Save filing change'}
                      </button>
                      <button
                        className="cl-btn-secondary"
                        type="button"
                        disabled={metadataStatus === 'saving'}
                        onClick={() => beginMetadataEdit(item)}
                      >
                        Cancel
                      </button>
                    </div>
                  </form>
                )}
                {replacingDocumentId === item.id && replacementDraft && (
                  <form
                    className="patient-document-content-replace"
                    id={`document-content-replace-${item.id}`}
                    onSubmit={(event) =>
                      void handleContentReplacement(event, item)
                    }
                  >
                    <div className="patient-document-panel-heading">
                      <div>
                        <span className="document-workspace-eyebrow">
                          Protected content
                        </span>
                        <h4>Create the next immutable version</h4>
                      </div>
                      <p>
                        The current bytes remain retrievable as a prior version.
                        Filing metadata is unchanged.
                      </p>
                    </div>
                    <div
                      className="patient-document-replacement-modes"
                      role="group"
                      aria-label="Replacement content type"
                    >
                      <button
                        className={replacementMode === 'text' ? 'active' : ''}
                        type="button"
                        aria-pressed={replacementMode === 'text'}
                        onClick={() => chooseReplacementMode('text')}
                      >
                        <FileText size={15} />
                        Clinical text
                      </button>
                      <button
                        className={replacementMode === 'file' ? 'active' : ''}
                        type="button"
                        aria-pressed={replacementMode === 'file'}
                        onClick={() => chooseReplacementMode('file')}
                      >
                        <FileUp size={15} />
                        Replacement file
                      </button>
                    </div>
                    <div className="patient-document-content-replace-grid">
                      <div className="field">
                        <label
                          className="label"
                          htmlFor={`document-replace-file-name-${item.id}`}
                        >
                          Stored file name *
                        </label>
                        <input
                          id={`document-replace-file-name-${item.id}`}
                          className="input"
                          value={replacementDraft.fileName}
                          onChange={(event) =>
                            setReplacementField(
                              'fileName',
                              event.target.value,
                            )
                          }
                          maxLength={255}
                          required
                        />
                      </div>
                      <div className="patient-document-version-target">
                        <span>Loaded version</span>
                        <strong>
                          {versionState?.documentId === item.id &&
                          versionState.status === 'ready'
                            ? `Version ${versionState.data.currentVersion}`
                            : 'Loading current version…'}
                        </strong>
                        <small>
                          A stale save is rejected; reload and review before
                          retrying.
                        </small>
                      </div>
                    </div>
                    {replacementMode === 'text' ? (
                      <div className="field">
                        <label
                          className="label"
                          htmlFor={`document-replace-text-${item.id}`}
                        >
                          New document content *
                        </label>
                        <textarea
                          id={`document-replace-text-${item.id}`}
                          className="textarea patient-document-replacement-body"
                          value={replacementDraft.content}
                          onChange={(event) =>
                            setReplacementField('content', event.target.value)
                          }
                          required
                        />
                      </div>
                    ) : (
                      <div className="patient-document-replacement-file">
                        <label
                          className="patient-document-file-drop"
                          htmlFor={`document-replace-file-${item.id}`}
                        >
                          <FileUp size={24} aria-hidden="true" />
                          <span>
                            {replacementFile
                              ? replacementFile.name
                              : 'Choose the complete replacement file'}
                          </span>
                          <small>
                            {replacementFile
                              ? `${formatBytes(replacementFile.size)} · ${replacementFile.type || 'application/octet-stream'}`
                              : state.status === 'ready'
                                ? `Protected limit ${formatBytes(state.data.options.maxFileSizeBytes)}`
                                : 'Protected service limit applies'}
                          </small>
                        </label>
                        <input
                          key={replacementFileInputKey}
                          id={`document-replace-file-${item.id}`}
                          className="patient-document-file-input"
                          type="file"
                          onChange={(event) =>
                            handleReplacementFileSelection(
                              event.target.files?.[0] ?? null,
                            )
                          }
                          required
                        />
                      </div>
                    )}
                    <div className="field">
                      <label
                        className="label"
                        htmlFor={`document-replace-reason-${item.id}`}
                      >
                        Replacement reason *
                      </label>
                      <textarea
                        id={`document-replace-reason-${item.id}`}
                        className="textarea"
                        value={replacementDraft.reason}
                        onChange={(event) =>
                          setReplacementField('reason', event.target.value)
                        }
                        maxLength={250}
                        required
                      />
                    </div>
                    <p className="patient-document-content-boundary">
                      Saving creates a new current version with hash, size,
                      type, reason, actor, and time evidence. It never edits a
                      prior version in place.
                    </p>
                    {replacementStatus === 'error' && (
                      <div className="cl-inline-error" role="alert">
                        {replacementError}
                      </div>
                    )}
                    <div className="patient-document-intake-actions">
                      <button
                        className="cl-btn-primary"
                        type="submit"
                        disabled={
                          replacementStatus === 'saving' ||
                          !replacementDraft.fileName.trim() ||
                          !replacementDraft.reason.trim() ||
                          (replacementMode === 'text'
                            ? !replacementDraft.content.trim()
                            : !replacementFile) ||
                          versionState?.documentId !== item.id ||
                          versionState.status !== 'ready'
                        }
                      >
                        <Save size={15} />
                        {replacementStatus === 'saving'
                          ? 'Creating version…'
                          : 'Create next version'}
                      </button>
                      <button
                        className="cl-btn-secondary"
                        type="button"
                        disabled={replacementStatus === 'saving'}
                        onClick={closeContentReplacement}
                      >
                        Cancel
                      </button>
                    </div>
                  </form>
                )}
                {historyState?.documentId === item.id && (
                  <section
                    className="patient-document-metadata-history"
                    id={`document-metadata-history-${item.id}`}
                    aria-label={`Filing history for ${item.name}`}
                  >
                    {historyState.status === 'loading' && (
                      <div className="patient-document-panel-loading" role="status">
                        <span className="spinner" aria-hidden="true" />
                        Loading filing history…
                      </div>
                    )}
                    {historyState.status === 'error' && (
                      <div className="cl-inline-error" role="alert">
                        <span>{historyState.message}</span>
                        <button
                          className="cl-link"
                          type="button"
                          onClick={() => void fetchMetadataHistory(item.id)}
                        >
                          Retry
                        </button>
                      </div>
                    )}
                    {historyState.status === 'ready' && (
                      <>
                        <div className="patient-document-history-heading">
                          <div>
                            <span className="document-workspace-eyebrow">
                              Immutable local evidence
                            </span>
                            <h4>Filing history</h4>
                          </div>
                          <span className="cl-badge cl-badge-muted">
                            {historyState.data.eventCount}{' '}
                            {historyState.data.eventCount === 1
                              ? 'change'
                              : 'changes'}
                          </span>
                        </div>
                        {historyState.data.events.length === 0 ? (
                          <p className="patient-document-panel-empty">
                            No filing metadata changes have been retained.
                          </p>
                        ) : (
                          <ol className="patient-document-history-list">
                            {historyState.data.events.map((historyEvent) => (
                              <li key={historyEvent.eventId}>
                                <div className="patient-document-history-event-heading">
                                  <strong>{historyEvent.reason}</strong>
                                  <span>
                                    By {historyEvent.actor} ·{' '}
                                    <time dateTime={historyEvent.occurredAt}>
                                      {new Date(
                                        historyEvent.occurredAt,
                                      ).toLocaleString()}
                                    </time>
                                  </span>
                                </div>
                                <MetadataChangeSummary event={historyEvent} />
                              </li>
                            ))}
                          </ol>
                        )}
                        {historyState.data.eventCount >
                          historyState.data.returnedCount && (
                          <p className="patient-document-history-boundary">
                            Showing the newest{' '}
                            {historyState.data.returnedCount} of{' '}
                            {historyState.data.eventCount} changes.
                          </p>
                        )}
                      </>
                    )}
                  </section>
                )}
                {versionState?.documentId === item.id && (
                  <section
                    className="patient-document-version-history"
                    id={`document-version-history-${item.id}`}
                    aria-label={`Content versions for ${item.name}`}
                  >
                    {versionState.status === 'loading' && (
                      <div className="patient-document-panel-loading" role="status">
                        <span className="spinner" aria-hidden="true" />
                        Loading protected content versions…
                      </div>
                    )}
                    {versionState.status === 'error' && (
                      <div className="cl-inline-error" role="alert">
                        <span>{versionState.message}</span>
                        <button
                          className="cl-link"
                          type="button"
                          onClick={() => void fetchVersionHistory(item.id)}
                        >
                          Retry
                        </button>
                      </div>
                    )}
                    {versionState.status === 'ready' && (
                      <>
                        <div className="patient-document-history-heading">
                          <div>
                            <span className="document-workspace-eyebrow">
                              Immutable protected bytes
                            </span>
                            <h4>Content version history</h4>
                          </div>
                          <span className="cl-badge cl-badge-muted">
                            {versionState.data.versionCount}{' '}
                            {versionState.data.versionCount === 1
                              ? 'version'
                              : 'versions'}
                          </span>
                        </div>
                        <ol className="patient-document-version-list">
                          {versionState.data.versions.map((version) => {
                            const downloadKey = `${item.id}-${version.version}`
                            return (
                              <li key={version.version}>
                                <div className="patient-document-version-heading">
                                  <div>
                                    <strong>{version.versionLabel}</strong>
                                    <span
                                      className={
                                        version.version ===
                                        versionState.data.currentVersion
                                          ? 'cl-badge cl-badge-green'
                                          : 'cl-badge cl-badge-muted'
                                      }
                                    >
                                      {version.versionStatus}
                                    </span>
                                  </div>
                                  <button
                                    className="cl-btn-secondary"
                                    type="button"
                                    disabled={
                                      !version.canDownload ||
                                      versionDownloadingKey === downloadKey
                                    }
                                    onClick={() =>
                                      void downloadDocumentVersion(
                                        item,
                                        version.version,
                                        version.fileName,
                                      )
                                    }
                                    aria-label={`Download ${item.name} ${version.versionLabel}`}
                                  >
                                    <Download size={14} />
                                    {versionDownloadingKey === downloadKey
                                      ? 'Downloading…'
                                      : `Download V${version.version}`}
                                  </button>
                                </div>
                                <dl className="patient-document-version-facts">
                                  <div>
                                    <dt>File</dt>
                                    <dd>
                                      {version.fileName || 'Stored document'}
                                    </dd>
                                  </div>
                                  <div>
                                    <dt>Type and size</dt>
                                    <dd>
                                      {version.mimetype || 'Unknown type'} ·{' '}
                                      {formatBytes(version.sizeBytes)}
                                    </dd>
                                  </div>
                                  <div>
                                    <dt>Hash</dt>
                                    <dd title={version.hash || undefined}>
                                      {shortHash(version.hash)}
                                    </dd>
                                  </div>
                                  <div>
                                    <dt>Revision</dt>
                                    <dd>
                                      {version.revisionActor
                                        ? `By ${version.revisionActor}`
                                        : 'Original filing'}{' '}
                                      ·{' '}
                                      <time dateTime={version.revisionAt}>
                                        {formatVersionTime(version.revisionAt)}
                                      </time>
                                    </dd>
                                  </div>
                                </dl>
                                <p className="patient-document-version-reason">
                                  {version.revisionReason ||
                                    'Original filed content'}
                                </p>
                                {version.contentPreview && (
                                  <p className="patient-document-version-preview">
                                    {version.contentPreview}
                                  </p>
                                )}
                              </li>
                            )
                          })}
                        </ol>
                        <p className="patient-document-content-boundary">
                          Every download uses the protected version route. The
                          displayed hash, type, size, actor, reason, and time are
                          server evidence for that exact version.
                        </p>
                      </>
                    )}
                  </section>
                )}
              </article>
            ))}
          </div>
        )}
      </section>
    </div>
  )
}
