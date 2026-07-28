import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useOutletContext } from 'react-router-dom'
import {
  Archive,
  Ban,
  CheckCircle2,
  Download,
  ExternalLink,
  FileText,
  FileUp,
  Files,
  FolderOpen,
  History,
  Eye,
  Link2,
  Pencil,
  Plus,
  RefreshCw,
  RotateCcw,
  ScanText,
  Save,
  StickyNote,
  X,
} from 'lucide-react'
import {
  archivePatientDocument,
  createPatientBinaryDocument,
  createPatientDocument,
  createPatientExternalLinkDocument,
  createPatientScannerCapture,
  downloadPatientDocument,
  downloadPatientDocumentVersion,
  getPatientDocumentCategoryOptions,
  getPatientDocumentArchiveHistory,
  getPatientDocumentMetadataHistory,
  getPatientDocumentReviewHistory,
  getPatientDocumentVersionHistory,
  getPatientDocuments,
  isRequestCancellation,
  searchEncounters,
  replacePatientDocumentBinaryContent,
  replacePatientDocumentContent,
  restorePatientDocument,
  reviewPatientDocument,
  updatePatientDocumentMetadata,
  type EncounterListItem,
  type PatientDocumentCategoryOptionsResponse,
  type PatientDocumentArchiveHistoryResponse,
  type PatientDocumentItem,
  type PatientDocumentMetadataHistoryItem,
  type PatientDocumentMetadataHistoryResponse,
  type PatientDocumentReviewHistoryResponse,
  type PatientDocumentVersionHistoryResponse,
  type PatientDocumentsResponse,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { PatientOutletContext } from './PatientShell.tsx'

type IntakeMode = 'note' | 'file' | 'scanner' | 'link'
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
  captureSource: string
  pageCount: string
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

type DocumentReviewState =
  | { documentId: number; status: 'loading' }
  | {
      documentId: number
      status: 'ready'
      data: PatientDocumentReviewHistoryResponse
    }
  | { documentId: number; status: 'error'; message: string }

type DocumentArchiveState =
  | { documentId: number; status: 'loading' }
  | {
      documentId: number
      status: 'ready'
      data: PatientDocumentArchiveHistoryResponse
    }
  | { documentId: number; status: 'error'; message: string }

type SupportedPreviewKind = 'text' | 'pdf' | 'image'
type ReviewAction = 'approved' | 'denied' | 'pending'

type DocumentPreviewState =
  | { documentId: number; status: 'loading' }
  | {
      documentId: number
      status: 'ready'
      kind: SupportedPreviewKind
      fileName: string
      contentType: string
      sizeBytes: number
      text?: string
      objectUrl?: string
      isTruncated: boolean
    }
  | { documentId: number; status: 'error'; message: string }

type ReplacementDraft = {
  fileName: string
  content: string
  reason: string
}

const TODAY = new Date().toISOString().slice(0, 10)
const TEXT_PREVIEW_LIMIT_BYTES = 512 * 1024
const SAFE_IMAGE_PREVIEW_TYPES = new Set([
  'image/avif',
  'image/gif',
  'image/jpeg',
  'image/png',
  'image/webp',
])

function blankDraft(): IntakeDraft {
  return {
    name: '',
    categoryId: '3',
    docDate: TODAY,
    encounter: '',
    notes: '',
    content: '',
    url: '',
    captureSource: 'Chart scanner',
    pageCount: '1',
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

function supportedPreviewKind(
  item: PatientDocumentItem,
): SupportedPreviewKind | null {
  if (!item.canPreviewInline) return null
  const mimetype = item.mimetype?.split(';', 1)[0]?.trim().toLowerCase() ?? ''
  if (item.previewKind === 'text' && mimetype.startsWith('text/')) {
    return 'text'
  }
  if (item.previewKind === 'pdf' && mimetype === 'application/pdf') {
    return 'pdf'
  }
  if (
    item.previewKind === 'image' &&
    SAFE_IMAGE_PREVIEW_TYPES.has(mimetype)
  ) {
    return 'image'
  }
  return null
}

function previewAvailability(item: PatientDocumentItem) {
  const kind = supportedPreviewKind(item)
  if (kind === 'text') return 'Inline text'
  if (kind === 'pdf') return 'Inline PDF'
  if (kind === 'image') return 'Inline image'
  return item.storageMethod === 'web_url' ? 'External link' : 'Download only'
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
  const [includeArchived, setIncludeArchived] = useState(false)
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
  const [reviewState, setReviewState] =
    useState<DocumentReviewState | null>(null)
  const [reviewingDocumentId, setReviewingDocumentId] = useState<number | null>(
    null,
  )
  const [reviewAction, setReviewAction] =
    useState<ReviewAction>('approved')
  const [reviewReason, setReviewReason] = useState('')
  const [reviewMutationStatus, setReviewMutationStatus] = useState<
    'idle' | 'saving' | 'error'
  >('idle')
  const [reviewMutationError, setReviewMutationError] = useState('')
  const [archiveState, setArchiveState] =
    useState<DocumentArchiveState | null>(null)
  const [archivingDocumentId, setArchivingDocumentId] = useState<number | null>(
    null,
  )
  const [archiveReason, setArchiveReason] = useState('')
  const [archiveMutationStatus, setArchiveMutationStatus] = useState<
    'idle' | 'saving' | 'error'
  >('idle')
  const [archiveMutationError, setArchiveMutationError] = useState('')
  const [previewState, setPreviewState] =
    useState<DocumentPreviewState | null>(null)
  const previewAbortRef = useRef<AbortController | null>(null)
  const previewObjectUrlRef = useRef<string | null>(null)
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
      previewAbortRef.current?.abort()
      previewAbortRef.current = null
      if (previewObjectUrlRef.current) {
        URL.revokeObjectURL(previewObjectUrlRef.current)
        previewObjectUrlRef.current = null
      }
      setPreviewState(null)
      setReviewState(null)
      setReviewingDocumentId(null)
      setReviewReason('')
      setReviewMutationStatus('idle')
      setReviewMutationError('')
      setArchiveState(null)
      setArchivingDocumentId(null)
      setArchiveReason('')
      setArchiveMutationStatus('idle')
      setArchiveMutationError('')
      setState({ status: 'loading' })
      try {
        const [documents, options, encounters] = await Promise.all([
          getPatientDocuments(
            session.sessionId,
            patientId,
            signal,
            includeArchived,
          ),
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
    [includeArchived, patientId, session.sessionId],
  )

  useEffect(() => {
    const controller = new AbortController()
    void loadWorkspace(controller.signal)
    return () => controller.abort()
  }, [loadWorkspace])

  useEffect(
    () => () => {
      previewAbortRef.current?.abort()
      if (previewObjectUrlRef.current) {
        URL.revokeObjectURL(previewObjectUrlRef.current)
      }
    },
    [],
  )

  useEffect(() => {
    setIncludeArchived(false)
    setEditingDocumentId(null)
    setMetadataDraft(null)
    setMetadataStatus('idle')
    setMetadataError('')
    setHistoryState(null)
    setVersionState(null)
    setReviewState(null)
    setReviewingDocumentId(null)
    setReviewAction('approved')
    setReviewReason('')
    setReviewMutationStatus('idle')
    setReviewMutationError('')
    setArchiveState(null)
    setArchivingDocumentId(null)
    setArchiveReason('')
    setArchiveMutationStatus('idle')
    setArchiveMutationError('')
    setPreviewState(null)
    setReplacingDocumentId(null)
    setReplacementDraft(null)
    setReplacementFile(null)
    setReplacementStatus('idle')
    setReplacementError('')
  }, [patientId])

  useEffect(() => {
    if (
      reviewState?.status === 'ready' &&
      reviewState.documentId === reviewingDocumentId
    ) {
      setReviewAction(
        reviewState.data.currentStatus.toLowerCase() === 'pending'
          ? 'approved'
          : 'pending',
      )
    }
  }, [reviewState, reviewingDocumentId])

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
      let result
      if (mode === 'note') {
        result = await createPatientDocument(session.sessionId, {
          ...shared,
          content: draft.content.trim(),
        })
      } else if (mode === 'file') {
        if (!selectedFile) {
          throw new Error('Choose a file to upload.')
        }
        if (selectedFile.size > state.data.options.maxFileSizeBytes) {
          throw new Error(
            `Choose a file no larger than ${formatBytes(state.data.options.maxFileSizeBytes)}.`,
          )
        }
        result = await createPatientBinaryDocument(session.sessionId, {
          ...shared,
          fileName: selectedFile.name,
          mimetype: selectedFile.type.trim() || 'application/octet-stream',
          contentBase64: await readFileAsBase64(selectedFile),
        })
      } else if (mode === 'scanner') {
        const pageCount = Number(draft.pageCount)
        if (
          !Number.isInteger(pageCount) ||
          pageCount < 1 ||
          pageCount > 100
        ) {
          throw new Error('Scanner page count must be between 1 and 100.')
        }
        if (!draft.captureSource.trim()) {
          throw new Error('Enter the scanner or capture source.')
        }
        result = await createPatientScannerCapture(session.sessionId, {
          ...shared,
          captureSource: draft.captureSource.trim(),
          pageCount,
        })
      } else {
        let link: URL
        try {
          link = new URL(draft.url.trim())
        } catch {
          throw new Error('Enter a complete http or https URL.')
        }
        if (!['http:', 'https:'].includes(link.protocol)) {
          throw new Error('External document links must use http or https.')
        }
        result = await createPatientExternalLinkDocument(session.sessionId, {
          ...shared,
          url: link.toString(),
        })
      }

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
            : mode === 'scanner'
              ? 'Scanner capture receipt filed and queued for OCR.'
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
    closeDocumentPreview()
    closeReviewWorkflow()
    closeArchiveWorkflow()
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
    closeDocumentPreview()
    closeReviewWorkflow()
    closeArchiveWorkflow()
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
    closeDocumentPreview()
    closeReviewWorkflow()
    closeArchiveWorkflow()
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
    closeDocumentPreview()
    closeReviewWorkflow()
    closeArchiveWorkflow()
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
      closeDocumentPreview()
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

  async function fetchReviewHistory(documentId: number) {
    setReviewState({ documentId, status: 'loading' })
    try {
      const history = await getPatientDocumentReviewHistory(
        session.sessionId,
        documentId,
      )
      setReviewState({ documentId, status: 'ready', data: history })
      return history
    } catch (error) {
      setReviewState({
        documentId,
        status: 'error',
        message:
          error instanceof Error
            ? error.message
            : 'Document review history could not be loaded.',
      })
      return null
    }
  }

  function closeReviewWorkflow() {
    setReviewState(null)
    setReviewingDocumentId(null)
    setReviewAction('approved')
    setReviewReason('')
    setReviewMutationStatus('idle')
    setReviewMutationError('')
  }

  function toggleReviewHistory(documentId: number) {
    if (
      reviewState?.documentId === documentId &&
      reviewingDocumentId !== documentId
    ) {
      closeReviewWorkflow()
      return
    }
    setEditingDocumentId(null)
    setMetadataDraft(null)
    setHistoryState(null)
    setVersionState(null)
    closeContentReplacement()
    closeDocumentPreview()
    closeArchiveWorkflow()
    setReviewingDocumentId(null)
    setReviewReason('')
    setReviewMutationStatus('idle')
    setReviewMutationError('')
    void fetchReviewHistory(documentId)
  }

  function beginDocumentReview(item: PatientDocumentItem) {
    if (reviewingDocumentId === item.id) {
      closeReviewWorkflow()
      return
    }
    setEditingDocumentId(null)
    setMetadataDraft(null)
    setHistoryState(null)
    setVersionState(null)
    closeContentReplacement()
    closeDocumentPreview()
    closeArchiveWorkflow()
    setReviewingDocumentId(item.id)
    setReviewAction(
      item.reviewStatus.toLowerCase() === 'pending' ? 'approved' : 'pending',
    )
    setReviewReason('')
    setReviewMutationStatus('idle')
    setReviewMutationError('')
    void fetchReviewHistory(item.id)
  }

  async function handleDocumentReview(
    event: React.FormEvent<HTMLFormElement>,
    item: PatientDocumentItem,
  ) {
    event.preventDefault()
    if (
      reviewState?.documentId !== item.id ||
      reviewState.status !== 'ready'
    ) {
      setReviewMutationStatus('error')
      setReviewMutationError(
        'Load the authoritative review state before recording this action.',
      )
      return
    }
    if (!reviewReason.trim()) {
      setReviewMutationStatus('error')
      setReviewMutationError('Explain this review decision.')
      return
    }

    const currentStatus = reviewState.data.currentStatus.toLowerCase()
    if (!['pending', 'approved', 'denied'].includes(currentStatus)) {
      setReviewMutationStatus('error')
      setReviewMutationError(
        `The current ${reviewState.data.currentStatus} state is not supported.`,
      )
      return
    }

    setReviewMutationStatus('saving')
    setReviewMutationError('')
    try {
      const result = await reviewPatientDocument(
        session.sessionId,
        item.id,
        {
          reviewStatus: reviewAction,
          reason: reviewReason.trim(),
          expectedReviewStatus: currentStatus as ReviewAction,
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
      setReviewingDocumentId(null)
      setReviewReason('')
      setReviewMutationStatus('idle')
      showToast(
        reviewAction === 'approved'
          ? 'Document approved.'
          : reviewAction === 'denied'
            ? 'Document denied.'
            : 'Document review reopened.',
        'success',
      )
      await fetchReviewHistory(item.id)
    } catch (error) {
      setReviewMutationStatus('error')
      setReviewMutationError(
        error instanceof Error
          ? error.message
          : 'The document review action could not be recorded.',
      )
      await fetchReviewHistory(item.id)
    }
  }

  async function fetchArchiveHistory(documentId: number) {
    setArchiveState({ documentId, status: 'loading' })
    try {
      const history = await getPatientDocumentArchiveHistory(
        session.sessionId,
        documentId,
      )
      setArchiveState({ documentId, status: 'ready', data: history })
      return history
    } catch (error) {
      setArchiveState({
        documentId,
        status: 'error',
        message:
          error instanceof Error
            ? error.message
            : 'Document archive history could not be loaded.',
      })
      return null
    }
  }

  function closeArchiveWorkflow() {
    setArchiveState(null)
    setArchivingDocumentId(null)
    setArchiveReason('')
    setArchiveMutationStatus('idle')
    setArchiveMutationError('')
  }

  function toggleArchiveHistory(documentId: number) {
    if (
      archiveState?.documentId === documentId &&
      archivingDocumentId !== documentId
    ) {
      closeArchiveWorkflow()
      return
    }
    setEditingDocumentId(null)
    setMetadataDraft(null)
    setHistoryState(null)
    setVersionState(null)
    closeContentReplacement()
    closeDocumentPreview()
    closeReviewWorkflow()
    setArchivingDocumentId(null)
    setArchiveReason('')
    setArchiveMutationStatus('idle')
    setArchiveMutationError('')
    void fetchArchiveHistory(documentId)
  }

  function beginArchiveChange(item: PatientDocumentItem) {
    if (archivingDocumentId === item.id) {
      closeArchiveWorkflow()
      return
    }
    setEditingDocumentId(null)
    setMetadataDraft(null)
    setHistoryState(null)
    setVersionState(null)
    closeContentReplacement()
    closeDocumentPreview()
    closeReviewWorkflow()
    setArchivingDocumentId(item.id)
    setArchiveReason('')
    setArchiveMutationStatus('idle')
    setArchiveMutationError('')
    void fetchArchiveHistory(item.id)
  }

  async function handleArchiveChange(
    event: React.FormEvent<HTMLFormElement>,
    item: PatientDocumentItem,
  ) {
    event.preventDefault()
    if (
      archiveState?.documentId !== item.id ||
      archiveState.status !== 'ready'
    ) {
      setArchiveMutationStatus('error')
      setArchiveMutationError(
        'Load the authoritative archive state before recording this action.',
      )
      return
    }
    if (!archiveReason.trim()) {
      setArchiveMutationStatus('error')
      setArchiveMutationError(
        archiveState.data.currentArchived
          ? 'Explain why this document is being restored.'
          : 'Explain why this document is being archived.',
      )
      return
    }

    const expectedArchived = archiveState.data.currentArchived
    setArchiveMutationStatus('saving')
    setArchiveMutationError('')
    try {
      const input = {
        reason: archiveReason.trim(),
        expectedArchived,
      }
      if (expectedArchived) {
        await restorePatientDocument(session.sessionId, item.id, input)
      } else {
        await archivePatientDocument(session.sessionId, item.id, input)
      }
      setRecentDocumentId(null)
      closeArchiveWorkflow()
      showToast(
        expectedArchived
          ? 'Document restored to the active register.'
          : 'Document archived and removed from the active register.',
        'success',
      )
      await loadWorkspace()
    } catch (error) {
      setArchiveMutationStatus('error')
      setArchiveMutationError(
        error instanceof Error
          ? error.message
          : 'The document archive state could not be changed.',
      )
      await fetchArchiveHistory(item.id)
    }
  }

  function closeDocumentPreview() {
    previewAbortRef.current?.abort()
    previewAbortRef.current = null
    if (previewObjectUrlRef.current) {
      URL.revokeObjectURL(previewObjectUrlRef.current)
      previewObjectUrlRef.current = null
    }
    setPreviewState(null)
  }

  async function openDocumentPreview(
    item: PatientDocumentItem,
    forceReload = false,
  ) {
    if (!forceReload && previewState?.documentId === item.id) {
      closeDocumentPreview()
      return
    }

    const expectedKind = supportedPreviewKind(item)
    if (!expectedKind) return

    closeDocumentPreview()
    setEditingDocumentId(null)
    setMetadataDraft(null)
    setMetadataStatus('idle')
    setMetadataError('')
    setHistoryState(null)
    setVersionState(null)
    closeReviewWorkflow()
    closeArchiveWorkflow()
    closeContentReplacement()

    const controller = new AbortController()
    previewAbortRef.current = controller
    setPreviewState({ documentId: item.id, status: 'loading' })

    try {
      const file = await downloadPatientDocument(
        session.sessionId,
        item.id,
        item.fileName || item.name,
        controller.signal,
      )
      if (controller.signal.aborted || previewAbortRef.current !== controller) {
        return
      }

      const contentType = file.contentType
        .split(';', 1)[0]
        .trim()
        .toLowerCase()
      const actualKind: SupportedPreviewKind | null = contentType.startsWith(
        'text/',
      )
        ? 'text'
        : contentType === 'application/pdf'
          ? 'pdf'
          : SAFE_IMAGE_PREVIEW_TYPES.has(contentType)
            ? 'image'
            : null

      if (!actualKind || actualKind !== expectedKind) {
        throw new Error(
          `${contentType || 'This file type'} is not available for inline preview. Use the protected download instead.`,
        )
      }

      if (actualKind === 'text') {
        const isTruncated = file.blob.size > TEXT_PREVIEW_LIMIT_BYTES
        const text = await file.blob
          .slice(0, TEXT_PREVIEW_LIMIT_BYTES)
          .text()
        if (
          controller.signal.aborted ||
          previewAbortRef.current !== controller
        ) {
          return
        }
        setPreviewState({
          documentId: item.id,
          status: 'ready',
          kind: actualKind,
          fileName: file.fileName,
          contentType,
          sizeBytes: file.blob.size,
          text,
          isTruncated,
        })
        return
      }

      const objectUrl = URL.createObjectURL(file.blob)
      previewObjectUrlRef.current = objectUrl
      setPreviewState({
        documentId: item.id,
        status: 'ready',
        kind: actualKind,
        fileName: file.fileName,
        contentType,
        sizeBytes: file.blob.size,
        objectUrl,
        isTruncated: false,
      })
    } catch (error) {
      if (isRequestCancellation(error) || controller.signal.aborted) return
      setPreviewState({
        documentId: item.id,
        status: 'error',
        message:
          error instanceof Error
            ? error.message
            : 'The protected inline preview could not be loaded.',
      })
    } finally {
      if (previewAbortRef.current === controller) {
        previewAbortRef.current = null
      }
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
            File notes, local uploads, scanner capture receipts, and external
            web links in this patient chart. Every new item remains pending
            review.
          </p>
        </div>
        <div className="document-workspace-brief-actions">
          <span className="document-workspace-count">
            <strong>{documents.activeCount}</strong>
            active {documents.activeCount === 1 ? 'document' : 'documents'}
          </span>
          <span className="document-workspace-count is-archived-count">
            <strong>{documents.archivedCount}</strong>
            archived {documents.archivedCount === 1 ? 'document' : 'documents'}
          </span>
          <Link
            className="cl-btn-secondary"
            to={`/clinician/document-ocr?patientId=${encodeURIComponent(patientId)}&status=all`}
          >
            <ScanText size={16} aria-hidden="true" />
            Open patient OCR
          </Link>
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
              aria-pressed={mode === 'scanner'}
              onClick={() => chooseMode('scanner')}
            >
              <ScanText size={17} />
              <span>
                <strong>Scanner capture</strong>
                <small>Local receipt, 1–100 pages</small>
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

            {mode === 'scanner' && (
              <div className="patient-document-scanner-capture">
                <div className="patient-document-scanner-grid">
                  <div className="field">
                    <label className="label" htmlFor="document-capture-source">
                      Scanner or capture source *
                    </label>
                    <input
                      id="document-capture-source"
                      className="input"
                      value={draft.captureSource}
                      onChange={(event) =>
                        setDraftField('captureSource', event.target.value)
                      }
                      maxLength={200}
                      required
                    />
                  </div>
                  <div className="field">
                    <label className="label" htmlFor="document-page-count">
                      Captured pages *
                    </label>
                    <input
                      id="document-page-count"
                      className="input"
                      type="number"
                      min={1}
                      max={100}
                      step={1}
                      value={draft.pageCount}
                      onChange={(event) =>
                        setDraftField('pageCount', event.target.value)
                      }
                      required
                    />
                  </div>
                </div>
                <p className="patient-document-scanner-boundary">
                  This records a deterministic local PDF capture receipt and
                  queues it for OCR. It does not control scanner hardware,
                  import device bytes, or perform malware/quarantine release.
                  Your authenticated account is retained as the capture actor.
                </p>
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
                {mode === 'file' ? (
                  <FileUp size={16} />
                ) : mode === 'scanner' ? (
                  <ScanText size={16} />
                ) : (
                  <Plus size={16} />
                )}
                {mutationStatus === 'saving'
                  ? 'Filing…'
                  : mode === 'note'
                    ? 'File clinical note'
                    : mode === 'file'
                      ? 'Upload document'
                      : mode === 'scanner'
                        ? 'File scanner capture'
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
          <div className="patient-document-register-controls">
            <label className="patient-document-archive-filter">
              <input
                type="checkbox"
                checked={includeArchived}
                onChange={(event) => setIncludeArchived(event.target.checked)}
              />
              <span>Show archived</span>
              <small>{documents.archivedCount}</small>
            </label>
            <button
              className="cl-btn-secondary"
              type="button"
              onClick={() => void loadWorkspace()}
            >
              <RefreshCw size={14} />
              Refresh
            </button>
          </div>
        </div>

        {documents.documents.length === 0 ? (
          <div className="cl-search-empty-state">
            <FolderOpen size={40} aria-hidden="true" />
            <p>
              {includeArchived
                ? 'No active or archived documents are filed for this patient.'
                : 'No active documents are filed for this patient.'}
            </p>
            {!includeArchived && (
              <button
                className="cl-link"
                type="button"
                onClick={() => setShowIntake(true)}
              >
                Add the first document
              </button>
            )}
          </div>
        ) : (
          <div className="patient-document-register-list">
            {documents.documents.map((item) => (
              <article
                className={`patient-document-register-item${recentDocumentId === item.id ? ' is-recent' : ''}${item.deleted !== 0 ? ' is-archived' : ''}`}
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
                      {item.deleted !== 0 && (
                        <span className="cl-badge patient-document-archived-badge">
                          Archived
                        </span>
                      )}
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
                    <div>
                      <dt>Viewing</dt>
                      <dd>{previewAvailability(item)}</dd>
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
                  {item.reviewedBy && (
                    <p className="patient-document-register-note">
                      Last review action: {item.reviewedBy}
                      {item.reviewedAt
                        ? ` · ${formatVersionTime(item.reviewedAt)}`
                        : ''}
                    </p>
                  )}
                  {item.archiveStateActor && item.archiveStateAt && (
                    <p className="patient-document-register-note">
                      {item.deleted !== 0 ? 'Archived' : 'Last restored'} by{' '}
                      {item.archiveStateActor} /{' '}
                      {formatVersionTime(item.archiveStateAt)}
                    </p>
                  )}
                </div>
                <div className="patient-document-register-actions">
                  {item.deleted === 0 && (
                    <>
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
                  <button
                    className="cl-btn-secondary"
                    type="button"
                    onClick={() => beginDocumentReview(item)}
                    aria-expanded={reviewingDocumentId === item.id}
                    aria-controls={`document-review-${item.id}`}
                  >
                    {item.reviewStatus.toLowerCase() === 'pending' ? (
                      <CheckCircle2 size={14} />
                    ) : (
                      <RotateCcw size={14} />
                    )}
                    {reviewingDocumentId === item.id
                      ? 'Close review'
                      : item.reviewStatus.toLowerCase() === 'pending'
                        ? 'Review document'
                        : 'Reopen review'}
                  </button>
                  <button
                    className="cl-btn-secondary"
                    type="button"
                    onClick={() => toggleReviewHistory(item.id)}
                    aria-expanded={
                      reviewState?.documentId === item.id &&
                      reviewingDocumentId !== item.id
                    }
                    aria-controls={`document-review-${item.id}`}
                  >
                    <History size={14} />
                    Review history
                  </button>
                  {supportedPreviewKind(item) && (
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      onClick={() => void openDocumentPreview(item)}
                      aria-expanded={previewState?.documentId === item.id}
                      aria-controls={`document-inline-preview-${item.id}`}
                    >
                      {previewState?.documentId === item.id ? (
                        <X size={14} />
                      ) : (
                        <Eye size={14} />
                      )}
                      {previewState?.documentId === item.id
                        ? 'Close preview'
                        : 'Preview'}
                    </button>
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
                    </>
                  )}
                  <button
                    className="cl-btn-secondary"
                    type="button"
                    onClick={() => beginArchiveChange(item)}
                    aria-expanded={archivingDocumentId === item.id}
                    aria-controls={`document-archive-${item.id}`}
                  >
                    {item.deleted !== 0 ? (
                      <RotateCcw size={14} />
                    ) : (
                      <Archive size={14} />
                    )}
                    {archivingDocumentId === item.id
                      ? 'Close lifecycle'
                      : item.deleted !== 0
                        ? 'Restore document'
                        : 'Archive document'}
                  </button>
                  <button
                    className="cl-btn-secondary"
                    type="button"
                    onClick={() => toggleArchiveHistory(item.id)}
                    aria-expanded={
                      archiveState?.documentId === item.id &&
                      archivingDocumentId !== item.id
                    }
                    aria-controls={`document-archive-${item.id}`}
                  >
                    <History size={14} />
                    Archive history
                  </button>
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
                {reviewState?.documentId === item.id && (
                  <section
                    className="patient-document-review"
                    id={`document-review-${item.id}`}
                    aria-label={`Review lifecycle for ${item.name}`}
                  >
                    {reviewState.status === 'loading' && (
                      <div
                        className="patient-document-panel-loading"
                        role="status"
                      >
                        <span className="spinner" aria-hidden="true" />
                        Loading authoritative review state…
                      </div>
                    )}
                    {reviewState.status === 'error' && (
                      <div className="cl-inline-error" role="alert">
                        <span>{reviewState.message}</span>
                        <button
                          className="cl-link"
                          type="button"
                          onClick={() => void fetchReviewHistory(item.id)}
                        >
                          Retry
                        </button>
                      </div>
                    )}
                    {reviewState.status === 'ready' && (
                      <>
                        <div className="patient-document-history-heading">
                          <div>
                            <span className="document-workspace-eyebrow">
                              Version-bound decisions
                            </span>
                            <h4>Review lifecycle</h4>
                          </div>
                          <span
                            className={
                              reviewState.data.currentStatus === 'approved'
                                ? 'cl-badge cl-badge-green'
                                : 'cl-badge cl-badge-muted'
                            }
                          >
                            {reviewState.data.currentStatus}
                          </span>
                        </div>
                        {reviewingDocumentId === item.id && (
                          <form
                            className="patient-document-review-form"
                            onSubmit={(event) =>
                              void handleDocumentReview(event, item)
                            }
                          >
                            {reviewState.data.currentStatus === 'pending' ? (
                              <div
                                className="patient-document-review-actions"
                                role="group"
                                aria-label="Document review decision"
                              >
                                <button
                                  className={
                                    reviewAction === 'approved'
                                      ? 'is-selected'
                                      : ''
                                  }
                                  type="button"
                                  aria-pressed={reviewAction === 'approved'}
                                  onClick={() => {
                                    setReviewAction('approved')
                                    setReviewMutationStatus('idle')
                                    setReviewMutationError('')
                                  }}
                                >
                                  <CheckCircle2 size={16} />
                                  Approve
                                </button>
                                <button
                                  className={
                                    reviewAction === 'denied'
                                      ? 'is-selected is-denial'
                                      : ''
                                  }
                                  type="button"
                                  aria-pressed={reviewAction === 'denied'}
                                  onClick={() => {
                                    setReviewAction('denied')
                                    setReviewMutationStatus('idle')
                                    setReviewMutationError('')
                                  }}
                                >
                                  <Ban size={16} />
                                  Deny
                                </button>
                              </div>
                            ) : (
                              <div className="patient-document-review-reopen">
                                <RotateCcw size={18} aria-hidden="true" />
                                <div>
                                  <strong>
                                    Reopen this{' '}
                                    {reviewState.data.currentStatus} review
                                  </strong>
                                  <span>
                                    The document returns to pending and requires
                                    a new decision.
                                  </span>
                                </div>
                              </div>
                            )}
                            <div className="field">
                              <label
                                className="label"
                                htmlFor={`document-review-reason-${item.id}`}
                              >
                                {reviewAction === 'approved'
                                  ? 'Approval rationale *'
                                  : reviewAction === 'denied'
                                    ? 'Denial reason *'
                                    : 'Reopen reason *'}
                              </label>
                              <textarea
                                id={`document-review-reason-${item.id}`}
                                className="textarea"
                                value={reviewReason}
                                onChange={(event) => {
                                  setReviewReason(event.target.value)
                                  if (reviewMutationStatus === 'error') {
                                    setReviewMutationStatus('idle')
                                    setReviewMutationError('')
                                  }
                                }}
                                maxLength={250}
                                required
                              />
                            </div>
                            <p className="patient-document-content-boundary">
                              This action is checked against the loaded{' '}
                              {reviewState.data.currentStatus} state and records
                              the authenticated actor, reason, time, document
                              version, and content hash.
                            </p>
                            {reviewMutationStatus === 'error' && (
                              <div className="cl-inline-error" role="alert">
                                {reviewMutationError}
                              </div>
                            )}
                            <div className="patient-document-intake-actions">
                              <button
                                className="cl-btn-primary"
                                type="submit"
                                disabled={
                                  reviewMutationStatus === 'saving' ||
                                  !reviewReason.trim()
                                }
                              >
                                {reviewAction === 'approved' ? (
                                  <CheckCircle2 size={15} />
                                ) : reviewAction === 'denied' ? (
                                  <Ban size={15} />
                                ) : (
                                  <RotateCcw size={15} />
                                )}
                                {reviewMutationStatus === 'saving'
                                  ? 'Recording…'
                                  : reviewAction === 'approved'
                                    ? 'Approve document'
                                    : reviewAction === 'denied'
                                      ? 'Deny document'
                                      : 'Reopen review'}
                              </button>
                              <button
                                className="cl-btn-secondary"
                                type="button"
                                disabled={reviewMutationStatus === 'saving'}
                                onClick={closeReviewWorkflow}
                              >
                                Cancel
                              </button>
                            </div>
                          </form>
                        )}
                        <div className="patient-document-review-history-heading">
                          <strong>Decision history</strong>
                          <span>
                            {reviewState.data.eventCount}{' '}
                            {reviewState.data.eventCount === 1
                              ? 'event'
                              : 'events'}
                          </span>
                        </div>
                        {reviewState.data.events.length === 0 ? (
                          <p className="patient-document-panel-empty">
                            No review decisions have been retained.
                          </p>
                        ) : (
                          <ol className="patient-document-review-history">
                            {reviewState.data.events.map((reviewEvent) => (
                              <li key={reviewEvent.eventId}>
                                <div>
                                  <strong>{reviewEvent.action}</strong>
                                  <span>
                                    {reviewEvent.fromStatus} →{' '}
                                    {reviewEvent.toStatus}
                                  </span>
                                </div>
                                <p>{reviewEvent.reason}</p>
                                <dl>
                                  <div>
                                    <dt>Actor and time</dt>
                                    <dd>
                                      {reviewEvent.actor} ·{' '}
                                      <time dateTime={reviewEvent.occurredAt}>
                                        {formatVersionTime(
                                          reviewEvent.occurredAt,
                                        )}
                                      </time>
                                    </dd>
                                  </div>
                                  <div>
                                    <dt>Reviewed bytes</dt>
                                    <dd>
                                      Version {reviewEvent.documentVersion} ·{' '}
                                      <span
                                        title={
                                          reviewEvent.contentHash || undefined
                                        }
                                      >
                                        {shortHash(reviewEvent.contentHash)}
                                      </span>
                                    </dd>
                                  </div>
                                </dl>
                              </li>
                            ))}
                          </ol>
                        )}
                        {reviewState.data.eventCount >
                          reviewState.data.returnedCount && (
                          <p className="patient-document-history-boundary">
                            Showing the newest{' '}
                            {reviewState.data.returnedCount} of{' '}
                            {reviewState.data.eventCount} review events.
                          </p>
                        )}
                      </>
                    )}
                  </section>
                )}
                {archiveState?.documentId === item.id && (
                  <section
                    className="patient-document-archive"
                    id={`document-archive-${item.id}`}
                    aria-label={`Archive lifecycle for ${item.name}`}
                  >
                    {archiveState.status === 'loading' && (
                      <div
                        className="patient-document-panel-loading"
                        role="status"
                      >
                        <span className="spinner" aria-hidden="true" />
                        Loading authoritative archive stateâ€¦
                      </div>
                    )}
                    {archiveState.status === 'error' && (
                      <div className="cl-inline-error" role="alert">
                        <span>{archiveState.message}</span>
                        <button
                          className="cl-link"
                          type="button"
                          onClick={() => void fetchArchiveHistory(item.id)}
                        >
                          Retry
                        </button>
                      </div>
                    )}
                    {archiveState.status === 'ready' && (
                      <>
                        <div className="patient-document-history-heading">
                          <div>
                            <span className="document-workspace-eyebrow">
                              Reversible chart visibility
                            </span>
                            <h4>Archive lifecycle</h4>
                          </div>
                          <span
                            className={
                              archiveState.data.currentArchived
                                ? 'cl-badge patient-document-archived-badge'
                                : 'cl-badge cl-badge-green'
                            }
                          >
                            {archiveState.data.currentArchived
                              ? 'Archived'
                              : 'Active'}
                          </span>
                        </div>
                        {archiveState.data.currentStateActor &&
                          archiveState.data.currentStateAt && (
                            <p className="patient-document-archive-current">
                              Current state recorded by{' '}
                              <strong>
                                {archiveState.data.currentStateActor}
                              </strong>{' '}
                              /{' '}
                              <time
                                dateTime={archiveState.data.currentStateAt}
                              >
                                {formatVersionTime(
                                  archiveState.data.currentStateAt,
                                )}
                              </time>
                            </p>
                          )}
                        {archivingDocumentId === item.id && (
                          <form
                            className="patient-document-archive-form"
                            onSubmit={(event) =>
                              void handleArchiveChange(event, item)
                            }
                          >
                            <p className="patient-document-content-boundary">
                              {archiveState.data.currentArchived
                                ? 'Restore returns this record to the active register. Its filing, content, review, and prior archive history remain unchanged.'
                                : 'Archive removes this record from the default active register without deleting its filing, protected bytes, review evidence, or prior versions.'}
                            </p>
                            <div className="field">
                              <label
                                className="label"
                                htmlFor={`document-archive-reason-${item.id}`}
                              >
                                {archiveState.data.currentArchived
                                  ? 'Restore reason *'
                                  : 'Archive reason *'}
                              </label>
                              <textarea
                                id={`document-archive-reason-${item.id}`}
                                className="textarea"
                                value={archiveReason}
                                onChange={(event) => {
                                  setArchiveReason(event.target.value)
                                  if (archiveMutationStatus === 'error') {
                                    setArchiveMutationStatus('idle')
                                    setArchiveMutationError('')
                                  }
                                }}
                                maxLength={250}
                                required
                              />
                            </div>
                            <p className="patient-document-history-boundary">
                              This action uses the loaded{' '}
                              {archiveState.data.currentArchived
                                ? 'archived'
                                : 'active'}{' '}
                              state. A concurrent change is rejected so it can
                              be reviewed before retrying.
                            </p>
                            {archiveMutationStatus === 'error' && (
                              <div className="cl-inline-error" role="alert">
                                {archiveMutationError}
                              </div>
                            )}
                            <div className="patient-document-intake-actions">
                              <button
                                className="cl-btn-primary"
                                type="submit"
                                disabled={
                                  archiveMutationStatus === 'saving' ||
                                  !archiveReason.trim()
                                }
                              >
                                {archiveState.data.currentArchived ? (
                                  <RotateCcw size={15} />
                                ) : (
                                  <Archive size={15} />
                                )}
                                {archiveMutationStatus === 'saving'
                                  ? archiveState.data.currentArchived
                                    ? 'Restoringâ€¦'
                                    : 'Archivingâ€¦'
                                  : archiveState.data.currentArchived
                                    ? 'Restore to active register'
                                    : 'Archive document'}
                              </button>
                              <button
                                className="cl-btn-secondary"
                                type="button"
                                disabled={archiveMutationStatus === 'saving'}
                                onClick={closeArchiveWorkflow}
                              >
                                Cancel
                              </button>
                            </div>
                          </form>
                        )}
                        <div className="patient-document-history-summary">
                          <span>
                            {archiveState.data.eventCount}{' '}
                            {archiveState.data.eventCount === 1
                              ? 'transition'
                              : 'transitions'}
                          </span>
                          <span>
                            Dataset {archiveState.data.datasetId} / version{' '}
                            {archiveState.data.datasetVersion}
                          </span>
                        </div>
                        {archiveState.data.events.length === 0 ? (
                          <p className="patient-document-panel-empty">
                            No archive or restore transitions have been
                            retained.
                          </p>
                        ) : (
                          <ol className="patient-document-archive-history">
                            {archiveState.data.events.map((archiveEvent) => (
                              <li key={archiveEvent.eventId}>
                                <div className="patient-document-archive-event-heading">
                                  <strong>{archiveEvent.action}</strong>
                                  <span>
                                    {archiveEvent.fromArchived
                                      ? 'Archived'
                                      : 'Active'}{' '}
                                    â†’{' '}
                                    {archiveEvent.toArchived
                                      ? 'Archived'
                                      : 'Active'}
                                  </span>
                                </div>
                                <p>{archiveEvent.reason}</p>
                                <dl>
                                  <div>
                                    <dt>Actor and time</dt>
                                    <dd>
                                      {archiveEvent.actor} /{' '}
                                      <time dateTime={archiveEvent.occurredAt}>
                                        {formatVersionTime(
                                          archiveEvent.occurredAt,
                                        )}
                                      </time>
                                    </dd>
                                  </div>
                                  <div>
                                    <dt>Retained chart state</dt>
                                    <dd>
                                      Version {archiveEvent.documentVersion} /{' '}
                                      {archiveEvent.reviewStatus} /{' '}
                                      <span
                                        title={
                                          archiveEvent.contentHash || undefined
                                        }
                                      >
                                        {shortHash(archiveEvent.contentHash)}
                                      </span>
                                    </dd>
                                  </div>
                                </dl>
                              </li>
                            ))}
                          </ol>
                        )}
                        {archiveState.data.eventCount >
                          archiveState.data.returnedCount && (
                          <p className="patient-document-history-boundary">
                            Showing the newest{' '}
                            {archiveState.data.returnedCount} of{' '}
                            {archiveState.data.eventCount} archive events.
                          </p>
                        )}
                      </>
                    )}
                  </section>
                )}
                {previewState?.documentId === item.id && (
                  <section
                    className="patient-document-inline-preview"
                    id={`document-inline-preview-${item.id}`}
                    aria-label={`Inline preview for ${item.name}`}
                  >
                    <div className="patient-document-preview-heading">
                      <div>
                        <span className="document-workspace-eyebrow">
                          Authenticated on demand
                        </span>
                        <h4>Previewing {item.name}</h4>
                      </div>
                      <button
                        className="cl-btn-secondary"
                        type="button"
                        onClick={closeDocumentPreview}
                      >
                        <X size={14} />
                        Close preview
                      </button>
                    </div>
                    {previewState.status === 'loading' && (
                      <div
                        className="patient-document-panel-loading"
                        role="status"
                      >
                        <span className="spinner" aria-hidden="true" />
                        Loading protected document bytes…
                      </div>
                    )}
                    {previewState.status === 'error' && (
                      <div className="patient-document-preview-error">
                        <div className="cl-inline-error" role="alert">
                          {previewState.message}
                        </div>
                        <div className="patient-document-preview-error-actions">
                          <button
                            className="cl-btn-secondary"
                            type="button"
                            onClick={() => void openDocumentPreview(item, true)}
                          >
                            <RefreshCw size={14} />
                            Retry preview
                          </button>
                          <button
                            className="cl-btn-secondary"
                            type="button"
                            disabled={downloadingId === item.id}
                            onClick={() => void downloadDocument(item)}
                          >
                            <Download size={14} />
                            Protected download
                          </button>
                        </div>
                      </div>
                    )}
                    {previewState.status === 'ready' && (
                      <>
                        <dl className="patient-document-preview-facts">
                          <div>
                            <dt>File</dt>
                            <dd>{previewState.fileName}</dd>
                          </div>
                          <div>
                            <dt>Type</dt>
                            <dd>{previewState.contentType}</dd>
                          </div>
                          <div>
                            <dt>Size</dt>
                            <dd>{formatBytes(previewState.sizeBytes)}</dd>
                          </div>
                          <div>
                            <dt>Version</dt>
                            <dd>{item.versionLabel}</dd>
                          </div>
                        </dl>
                        {previewState.kind === 'text' && (
                          <pre
                            className="patient-document-text-preview"
                            tabIndex={0}
                            aria-label={`Text content of ${item.name}`}
                          >
                            {previewState.text}
                          </pre>
                        )}
                        {previewState.kind === 'image' &&
                          previewState.objectUrl && (
                            <div className="patient-document-image-preview">
                              <img
                                src={previewState.objectUrl}
                                alt={`Preview of ${item.name}`}
                              />
                            </div>
                          )}
                        {previewState.kind === 'pdf' &&
                          previewState.objectUrl && (
                            <iframe
                              className="patient-document-pdf-preview"
                              src={previewState.objectUrl}
                              title={`${item.name} PDF preview`}
                            />
                          )}
                        {previewState.isTruncated && (
                          <p className="patient-document-preview-notice">
                            Showing the first{' '}
                            {formatBytes(TEXT_PREVIEW_LIMIT_BYTES)}. Download
                            the file to read the complete document.
                          </p>
                        )}
                        <p className="patient-document-content-boundary">
                          Preview bytes are fetched only after this panel opens
                          through the same protected transport as download. The
                          temporary browser URL is revoked when the panel
                          closes.
                        </p>
                      </>
                    )}
                  </section>
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
