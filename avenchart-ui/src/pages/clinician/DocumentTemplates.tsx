// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import {
  useEffect,
  useEffectEvent,
  useMemo,
  useState,
  type FormEvent,
} from 'react'
import { Link, useOutletContext } from 'react-router-dom'
import {
  ChevronLeft,
  ChevronRight,
  Download,
  Eye,
  FileClock,
  FileText,
  Paperclip,
  Plus,
  RefreshCw,
  Save,
  Search,
  Upload,
  UserRound,
} from 'lucide-react'
import {
  createDocumentTemplate,
  downloadDocumentTemplateBinaryVersion,
  generateDocumentTemplateAttachment,
  getDocumentTemplateBinaryVersions,
  getDocumentTemplateHistory,
  getDocumentTemplates,
  getPatientDocumentCategoryOptions,
  renderDocumentTemplate,
  searchPatients,
  updateDocumentTemplate,
  uploadDocumentTemplateBinaryVersion,
  type DocumentTemplateBinaryVersion,
  type DocumentTemplateEvent,
  type DocumentTemplateItem,
  type DocumentTemplateListResponse,
  type PatientDocumentCategoryOption,
  type PatientListItem,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

const TEMPLATE_PAGE_SIZE = 8
const DETAIL_PAGE_SIZE = 5
const INLINE_TEXT_PREVIEW_LIMIT = 1024 * 1024

type TemplateDraft = {
  id?: string
  name: string
  content: string
  active: boolean
  createdAt?: string
  updatedAt?: string
}

type DetailState<T> =
  | { status: 'idle' | 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; message: string }

function newTemplateDraft(): TemplateDraft {
  return {
    name: '',
    content: 'Dear ***NAME***,\n\n',
    active: true,
  }
}

function caughtMessage(caught: unknown, fallback: string) {
  return caught instanceof Error ? caught.message : fallback
}

function formatDateTime(value?: string) {
  if (!value) return 'Not yet saved'
  const parsed = new Date(value)
  return Number.isNaN(parsed.valueOf())
    ? value
    : parsed.toLocaleString(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short',
      })
}

function actionLabel(action: DocumentTemplateEvent['action']) {
  return action.replaceAll('-', ' ')
}

function canPreviewInline(version: DocumentTemplateBinaryVersion) {
  return (
    version.mimetype === 'text/plain' &&
    version.sizeBytes <= INLINE_TEXT_PREVIEW_LIMIT
  )
}

function pageCount(total: number, pageSize: number) {
  return Math.max(1, Math.ceil(total / pageSize))
}

function selectedPage(offset: number, pageSize: number) {
  return Math.floor(offset / pageSize) + 1
}

export default function DocumentTemplates() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const [list, setList] = useState<DocumentTemplateListResponse | null>(null)
  const [listLoading, setListLoading] = useState(true)
  const [listError, setListError] = useState<string | null>(null)
  const [searchDraft, setSearchDraft] = useState('')
  const [search, setSearch] = useState('')
  const [includeInactive, setIncludeInactive] = useState(true)
  const [offset, setOffset] = useState(0)

  const [editing, setEditing] = useState<TemplateDraft>(newTemplateDraft)
  const [saveBusy, setSaveBusy] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [versions, setVersions] = useState<
    DetailState<DocumentTemplateBinaryVersion[]>
  >({ status: 'idle' })
  const [history, setHistory] = useState<DetailState<DocumentTemplateEvent[]>>({
    status: 'idle',
  })
  const [historyCount, setHistoryCount] = useState(0)
  const [versionsPage, setVersionsPage] = useState(1)
  const [historyPage, setHistoryPage] = useState(1)
  const [uploadBusy, setUploadBusy] = useState(false)
  const [downloadBusy, setDownloadBusy] = useState<string | null>(null)
  const [binaryPreview, setBinaryPreview] = useState<{
    fileName: string
    text: string
  } | null>(null)

  const [patientQuery, setPatientQuery] = useState('')
  const [patientSearch, setPatientSearch] = useState<
    DetailState<PatientListItem[]>
  >({ status: 'idle' })
  const [selectedPatient, setSelectedPatient] =
    useState<PatientListItem | null>(null)
  const [categories, setCategories] = useState<
    DetailState<PatientDocumentCategoryOption[]>
  >({ status: 'loading' })
  const [categoryId, setCategoryId] = useState('')
  const [documentDate, setDocumentDate] = useState(
    new Date().toISOString().slice(0, 10),
  )
  const [encounter, setEncounter] = useState('')
  const [rendered, setRendered] = useState('')
  const [previewBusy, setPreviewBusy] = useState(false)
  const [attachmentBusy, setAttachmentBusy] = useState<string | null>(null)
  const [attachmentError, setAttachmentError] = useState<string | null>(null)
  const [generatedDocumentId, setGeneratedDocumentId] = useState<number | null>(
    null,
  )

  async function fetchList(signal?: AbortSignal) {
    setListLoading(true)
    setListError(null)
    try {
      const response = await getDocumentTemplates(
        session.sessionId,
        {
          search: search || undefined,
          includeInactive,
          offset,
          limit: TEMPLATE_PAGE_SIZE,
        },
        signal,
      )
      if (response.total > 0 && response.items.length === 0 && offset > 0) {
        setOffset(
          Math.floor((response.total - 1) / TEMPLATE_PAGE_SIZE) *
            TEMPLATE_PAGE_SIZE,
        )
        return
      }
      setList(response)
    } catch (caught) {
      if (signal?.aborted) return
      setListError(caughtMessage(caught, 'Could not load document templates.'))
    } finally {
      if (!signal?.aborted) setListLoading(false)
    }
  }

  const loadListOnChange = useEffectEvent(fetchList)
  useEffect(() => {
    const controller = new AbortController()
    void loadListOnChange(controller.signal)
    return () => controller.abort()
  }, [session.sessionId, search, includeInactive, offset])

  async function loadCategories(signal?: AbortSignal) {
    setCategories({ status: 'loading' })
    try {
      const response = await getPatientDocumentCategoryOptions(
        session.sessionId,
        signal,
      )
      if (signal?.aborted) return
      setCategories({ status: 'ready', data: response.categories })
      setCategoryId((current) =>
        response.categories.some((item) => String(item.id) === current)
          ? current
          : String(response.categories[0]?.id ?? ''),
      )
    } catch (caught) {
      if (signal?.aborted) return
      setCategories({
        status: 'error',
        message: caughtMessage(
          caught,
          'Could not load protected document categories.',
        ),
      })
    }
  }

  const loadCategoriesOnMount = useEffectEvent(loadCategories)
  useEffect(() => {
    const controller = new AbortController()
    void loadCategoriesOnMount(controller.signal)
    return () => controller.abort()
  }, [session.sessionId])

  async function loadVersions(templateId: string) {
    setVersions({ status: 'loading' })
    try {
      const data = await getDocumentTemplateBinaryVersions(
        session.sessionId,
        templateId,
      )
      setVersions({ status: 'ready', data })
    } catch (caught) {
      setVersions({
        status: 'error',
        message: caughtMessage(
          caught,
          'Could not load binary template versions.',
        ),
      })
    }
  }

  async function loadHistory(templateId: string) {
    setHistory({ status: 'loading' })
    try {
      const response = await getDocumentTemplateHistory(
        session.sessionId,
        templateId,
      )
      setHistoryCount(response.eventCount)
      setHistory({ status: 'ready', data: response.events })
    } catch (caught) {
      setHistory({
        status: 'error',
        message: caughtMessage(caught, 'Could not load template history.'),
      })
    }
  }

  function resetOutput() {
    setRendered('')
    setBinaryPreview(null)
    setGeneratedDocumentId(null)
    setAttachmentError(null)
  }

  function chooseTemplate(template: DocumentTemplateItem) {
    setEditing({ ...template })
    setSaveError(null)
    setVersionsPage(1)
    setHistoryPage(1)
    resetOutput()
    void Promise.all([loadVersions(template.id), loadHistory(template.id)])
  }

  function chooseNewTemplate() {
    setEditing(newTemplateDraft())
    setSaveError(null)
    setVersions({ status: 'idle' })
    setHistory({ status: 'idle' })
    setHistoryCount(0)
    resetOutput()
  }

  async function saveTemplate() {
    const name = editing.name.trim()
    const content = editing.content.trim()
    if (!name || !content) {
      setSaveError('A template name and text content are required.')
      return
    }

    setSaveBusy(true)
    setSaveError(null)
    try {
      const saved = editing.id
        ? await updateDocumentTemplate(session.sessionId, editing.id, {
            name,
            content,
            active: editing.active,
          })
        : await createDocumentTemplate(session.sessionId, {
            name,
            content,
            active: editing.active,
          })
      setEditing({ ...saved })
      await Promise.all([
        fetchList(),
        loadVersions(saved.id),
        loadHistory(saved.id),
      ])
      showToast(
        editing.id ? 'Document template updated.' : 'Document template created.',
        'success',
      )
    } catch (caught) {
      setSaveError(caughtMessage(caught, 'Could not save document template.'))
    } finally {
      setSaveBusy(false)
    }
  }

  async function runPatientSearch(event?: FormEvent) {
    event?.preventDefault()
    const query = patientQuery.trim()
    if (!query) {
      setPatientSearch({
        status: 'error',
        message: 'Enter a patient name or identifier.',
      })
      return
    }

    setPatientSearch({ status: 'loading' })
    setSelectedPatient(null)
    resetOutput()
    try {
      const response = await searchPatients(session.sessionId, {
        search: query,
        limit: 8,
      })
      setPatientSearch({ status: 'ready', data: response.patients })
    } catch (caught) {
      setPatientSearch({
        status: 'error',
        message: caughtMessage(caught, 'Could not search patients.'),
      })
    }
  }

  function choosePatient(patient: PatientListItem) {
    setSelectedPatient(patient)
    setPatientQuery('')
    setPatientSearch({ status: 'idle' })
    resetOutput()
  }

  async function renderPreview() {
    if (!editing.id || !selectedPatient) {
      setAttachmentError('Select a saved template and a patient first.')
      return
    }

    setPreviewBusy(true)
    setAttachmentError(null)
    setGeneratedDocumentId(null)
    try {
      const response = await renderDocumentTemplate(
        session.sessionId,
        editing.id,
        selectedPatient.canonicalId,
      )
      setRendered(response.content)
      setBinaryPreview(null)
    } catch (caught) {
      setAttachmentError(
        caughtMessage(
          caught,
          'Only an active template can be rendered for an existing patient.',
        ),
      )
    } finally {
      setPreviewBusy(false)
    }
  }

  async function uploadBinary(file?: File) {
    if (!file || !editing.id) return
    setUploadBusy(true)
    setAttachmentError(null)
    try {
      await uploadDocumentTemplateBinaryVersion(
        session.sessionId,
        editing.id,
        {
          fileName: file.name,
          mimetype: file.type || 'application/octet-stream',
          contentBase64: await new Promise<string>((resolve, reject) => {
            const reader = new FileReader()
            reader.onload = () =>
              resolve(String(reader.result).split(',')[1] ?? '')
            reader.onerror = () =>
              reject(new Error('The selected template file could not be read.'))
            reader.readAsDataURL(file)
          }),
        },
      )
      setVersionsPage(1)
      await Promise.all([
        loadVersions(editing.id),
        loadHistory(editing.id),
      ])
      showToast('Binary template version uploaded.', 'success')
    } catch (caught) {
      setAttachmentError(
        caughtMessage(
          caught,
          'Upload failed. Use a TXT, ODT, DOCX, or non-nested ZIP file up to 25 MB.',
        ),
      )
    } finally {
      setUploadBusy(false)
    }
  }

  async function downloadBinary(version: DocumentTemplateBinaryVersion) {
    if (!editing.id) return
    setDownloadBusy(version.id)
    setAttachmentError(null)
    try {
      const blob = await downloadDocumentTemplateBinaryVersion(
        session.sessionId,
        editing.id,
        version.id,
      )
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = version.fileName
      document.body.append(anchor)
      anchor.click()
      anchor.remove()
      globalThis.setTimeout(() => URL.revokeObjectURL(url), 0)
    } catch (caught) {
      setAttachmentError(
        caughtMessage(caught, 'Could not download template version.'),
      )
    } finally {
      setDownloadBusy(null)
    }
  }

  async function previewBinary(version: DocumentTemplateBinaryVersion) {
    if (!editing.id || !canPreviewInline(version)) return
    setDownloadBusy(version.id)
    setAttachmentError(null)
    try {
      const blob = await downloadDocumentTemplateBinaryVersion(
        session.sessionId,
        editing.id,
        version.id,
      )
      setBinaryPreview({
        fileName: version.fileName,
        text: await blob.text(),
      })
      setRendered('')
    } catch (caught) {
      setAttachmentError(
        caughtMessage(caught, 'Could not preview template version.'),
      )
    } finally {
      setDownloadBusy(null)
    }
  }

  async function attachTemplate(versionId?: string) {
    if (!editing.id || !selectedPatient || !categoryId || !documentDate) {
      setAttachmentError(
        'Select a saved template, patient, filing category, and document date.',
      )
      return
    }

    const encounterNumber = encounter ? Number(encounter) : undefined
    if (
      encounterNumber !== undefined &&
      (!Number.isInteger(encounterNumber) || encounterNumber <= 0)
    ) {
      setAttachmentError('Encounter number must be a positive whole number.')
      return
    }

    setAttachmentBusy(versionId ?? 'text')
    setAttachmentError(null)
    setGeneratedDocumentId(null)
    try {
      const response = await generateDocumentTemplateAttachment(
        session.sessionId,
        editing.id,
        {
          patientId: selectedPatient.canonicalId,
          categoryId: Number(categoryId),
          encounter: encounterNumber,
          docDate: documentDate,
          binaryVersionId: versionId,
        },
      )
      setGeneratedDocumentId(response.id)
      await loadHistory(editing.id)
      showToast('Generated template attached to the patient chart.', 'success')
    } catch (caught) {
      setAttachmentError(
        caughtMessage(caught, 'Could not generate the patient attachment.'),
      )
    } finally {
      setAttachmentBusy(null)
    }
  }

  const visibleVersions = useMemo(() => {
    if (versions.status !== 'ready') return []
    const start = (versionsPage - 1) * DETAIL_PAGE_SIZE
    return versions.data.slice(start, start + DETAIL_PAGE_SIZE)
  }, [versions, versionsPage])

  const visibleHistory = useMemo(() => {
    if (history.status !== 'ready') return []
    const start = (historyPage - 1) * DETAIL_PAGE_SIZE
    return history.data.slice(start, start + DETAIL_PAGE_SIZE)
  }, [history, historyPage])

  const templatePage = selectedPage(offset, TEMPLATE_PAGE_SIZE)
  const templatePages = pageCount(list?.total ?? 0, TEMPLATE_PAGE_SIZE)
  const versionPages = pageCount(
    versions.status === 'ready' ? versions.data.length : 0,
    DETAIL_PAGE_SIZE,
  )
  const historyPages = pageCount(
    history.status === 'ready' ? history.data.length : 0,
    DETAIL_PAGE_SIZE,
  )

  return (
    <div className="clinician-page document-template-workspace">
      <div className="clinician-page-header">
        <div>
          <h1 className="clinician-page-title">Document Templates</h1>
          <p className="clinician-page-subtitle">
            Govern reusable text and versioned template files, preview
            patient-specific output, and file generated records into the
            protected document lifecycle.
          </p>
        </div>
        <button className="cl-btn-primary" type="button" onClick={chooseNewTemplate}>
          <Plus size={15} aria-hidden="true" /> New template
        </button>
      </div>

      <section className="cl-card" aria-labelledby="template-library-heading">
        <div className="cl-card-header">
          <div>
            <h2 className="cl-card-title" id="template-library-heading">
              Template library
            </h2>
            <p className="cl-card-subtitle">
              Search text and names. Retired templates remain discoverable but
              cannot generate output.
            </p>
          </div>
          {list && (
            <span className="document-template-counts">
              {list.activeCount} active / {list.retiredCount} retired
            </span>
          )}
        </div>

        <form
          className="document-template-filter"
          onSubmit={(event) => {
            event.preventDefault()
            setOffset(0)
            setSearch(searchDraft.trim())
          }}
        >
          <label className="cl-admin-field document-template-search">
            <span>Search templates</span>
            <span>
              <Search size={15} aria-hidden="true" />
              <input
                className="ne-input"
                value={searchDraft}
                maxLength={120}
                onChange={(event) => setSearchDraft(event.target.value)}
                placeholder="Name or template text"
              />
            </span>
          </label>
          <label className="document-template-check">
            <input
              type="checkbox"
              checked={includeInactive}
              onChange={(event) => {
                setIncludeInactive(event.target.checked)
                setOffset(0)
              }}
            />
            Show retired
          </label>
          <button className="cl-btn-secondary" type="submit">
            <Search size={15} aria-hidden="true" /> Apply
          </button>
        </form>

        {listError && (
          <div className="cl-inline-error" role="alert">
            <span>{listError}</span>
            <button
              className="link-button"
              type="button"
              onClick={() => void fetchList()}
            >
              Retry
            </button>
          </div>
        )}
        {listLoading && (
          <p className="cl-empty-text" role="status">
            Loading templates…
          </p>
        )}
        {!listLoading && !listError && list?.items.length === 0 && (
          <div className="cl-empty-state-sm">
            <FileText size={24} aria-hidden="true" />
            <p>No templates match these filters.</p>
            <button className="cl-btn-secondary" type="button" onClick={chooseNewTemplate}>
              Create a template
            </button>
          </div>
        )}
        {!listLoading && list && list.items.length > 0 && (
          <>
            <ul className="document-template-library-list" aria-label="Document templates">
              {list.items.map((template) => (
                <li key={template.id}>
                  <button
                    type="button"
                    aria-pressed={editing.id === template.id}
                    onClick={() => chooseTemplate(template)}
                  >
                    <span>
                      <strong>{template.name}</strong>
                      <small>Updated {formatDateTime(template.updatedAt)}</small>
                    </span>
                    <span
                      className={`cl-badge ${
                        template.active ? 'cl-badge-green' : 'cl-badge-muted'
                      }`}
                    >
                      {template.active ? 'Active' : 'Retired'}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
            <div className="document-template-pagination" aria-label="Template pages">
              <button
                className="cl-btn-secondary"
                type="button"
                disabled={offset === 0}
                onClick={() => setOffset(Math.max(0, offset - TEMPLATE_PAGE_SIZE))}
              >
                <ChevronLeft size={15} aria-hidden="true" /> Previous
              </button>
              <span>
                Page {templatePage} of {templatePages} · {list.total} result
                {list.total === 1 ? '' : 's'}
              </span>
              <button
                className="cl-btn-secondary"
                type="button"
                disabled={offset + TEMPLATE_PAGE_SIZE >= list.total}
                onClick={() => setOffset(offset + TEMPLATE_PAGE_SIZE)}
              >
                Next <ChevronRight size={15} aria-hidden="true" />
              </button>
            </div>
          </>
        )}
      </section>

      <section className="cl-card" aria-labelledby="template-editor-heading">
        <div className="cl-card-header">
          <div>
            <h2 className="cl-card-title" id="template-editor-heading">
              {editing.id ? `Edit ${editing.name || 'template'}` : 'New template'}
            </h2>
            {editing.id && (
              <p className="cl-card-subtitle">
                Created {formatDateTime(editing.createdAt)} · Last updated{' '}
                {formatDateTime(editing.updatedAt)}
              </p>
            )}
          </div>
          {editing.id && (
            <button
              className="cl-btn-secondary"
              type="button"
              onClick={() => {
                void loadVersions(editing.id!)
                void loadHistory(editing.id!)
              }}
            >
              <RefreshCw size={15} aria-hidden="true" /> Refresh details
            </button>
          )}
        </div>

        <div className="cl-admin-form-grid document-template-editor-grid">
          <label className="cl-admin-field">
            <span>Template name *</span>
            <input
              className="ne-input"
              value={editing.name}
              maxLength={120}
              onChange={(event) =>
                setEditing({ ...editing, name: event.target.value })
              }
            />
          </label>
          <label className="document-template-check document-template-active">
            <input
              type="checkbox"
              checked={editing.active}
              onChange={(event) =>
                setEditing({ ...editing, active: event.target.checked })
              }
            />
            Active for preview and patient attachment
          </label>
          <label className="cl-admin-field document-template-content">
            <span>Text content *</span>
            <textarea
              className="ne-input"
              rows={11}
              maxLength={250_000}
              value={editing.content}
              onChange={(event) =>
                setEditing({ ...editing, content: event.target.value })
              }
            />
            <small>
              Supported patient tokens: <code>***NAME***</code>,{' '}
              <code>***DOB***</code>, and <code>***PATIENT_ID***</code>.
            </small>
          </label>
        </div>
        {saveError && (
          <p className="cl-inline-error" role="alert">
            {saveError}
          </p>
        )}
        <div className="cl-actions">
          <button
            className="cl-btn-primary"
            type="button"
            disabled={saveBusy}
            onClick={() => void saveTemplate()}
          >
            <Save size={15} aria-hidden="true" />
            {saveBusy ? 'Saving…' : 'Save template'}
          </button>
        </div>
      </section>

      {editing.id && (
        <>
          <section className="cl-card" aria-labelledby="template-output-heading">
            <div className="cl-card-header">
              <div>
                <h2 className="cl-card-title" id="template-output-heading">
                  Preview and patient attachment
                </h2>
                <p className="cl-card-subtitle">
                  Choose the chart and filing context before rendering or
                  attaching text or a binary version.
                </p>
              </div>
            </div>

            <form className="document-template-patient-search" onSubmit={runPatientSearch}>
              <label className="cl-admin-field">
                <span>Find patient *</span>
                <input
                  className="ne-input"
                  value={patientQuery}
                  onChange={(event) => setPatientQuery(event.target.value)}
                  placeholder="Name, chart number, or patient ID"
                />
              </label>
              <button
                className="cl-btn-secondary"
                type="submit"
                disabled={patientSearch.status === 'loading'}
              >
                <Search size={15} aria-hidden="true" />
                {patientSearch.status === 'loading' ? 'Searching…' : 'Search'}
              </button>
            </form>

            {patientSearch.status === 'error' && (
              <p className="cl-inline-error" role="alert">
                {patientSearch.message}
              </p>
            )}
            {patientSearch.status === 'ready' && patientSearch.data.length === 0 && (
              <p className="cl-empty-text">No patients match that search.</p>
            )}
            {patientSearch.status === 'ready' && patientSearch.data.length > 0 && (
              <ul className="document-template-patient-results" aria-label="Patient search results">
                {patientSearch.data.map((patient) => (
                  <li key={patient.canonicalId}>
                    <button type="button" onClick={() => choosePatient(patient)}>
                      <UserRound size={16} aria-hidden="true" />
                      <span>
                        <strong>{patient.displayName}</strong>
                        <small>
                          {patient.canonicalId} · DOB {patient.dateOfBirth}
                        </small>
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            )}

            {selectedPatient && (
              <div className="document-template-selected-patient">
                <UserRound size={18} aria-hidden="true" />
                <span>
                  <strong>{selectedPatient.displayName}</strong>
                  <small>
                    {selectedPatient.canonicalId} · DOB {selectedPatient.dateOfBirth}
                  </small>
                </span>
                <button
                  className="link-button"
                  type="button"
                  onClick={() => {
                    setSelectedPatient(null)
                    resetOutput()
                  }}
                >
                  Change
                </button>
              </div>
            )}

            <div className="cl-admin-form-grid document-template-filing-grid">
              <label className="cl-admin-field">
                <span>Filing category *</span>
                <select
                  className="ne-input"
                  value={categoryId}
                  disabled={categories.status !== 'ready'}
                  onChange={(event) => setCategoryId(event.target.value)}
                >
                  <option value="">
                    {categories.status === 'loading'
                      ? 'Loading categories…'
                      : 'Select category'}
                  </option>
                  {categories.status === 'ready' &&
                    categories.data.map((category) => (
                      <option key={category.id} value={category.id}>
                        {category.name}
                      </option>
                    ))}
                </select>
              </label>
              <label className="cl-admin-field">
                <span>Document date *</span>
                <input
                  className="ne-input"
                  type="date"
                  value={documentDate}
                  onChange={(event) => setDocumentDate(event.target.value)}
                />
              </label>
              <label className="cl-admin-field">
                <span>Related encounter</span>
                <input
                  className="ne-input"
                  type="number"
                  min="1"
                  value={encounter}
                  onChange={(event) => setEncounter(event.target.value)}
                  placeholder="Optional encounter number"
                />
              </label>
            </div>
            {categories.status === 'error' && (
              <div className="cl-inline-error" role="alert">
                <span>{categories.message}</span>
                <button
                  className="link-button"
                  type="button"
                  onClick={() => void loadCategories()}
                >
                  Retry
                </button>
              </div>
            )}

            <div className="cl-actions">
              <button
                className="cl-btn-secondary"
                type="button"
                disabled={previewBusy || !editing.active}
                onClick={() => void renderPreview()}
              >
                <Eye size={15} aria-hidden="true" />
                {previewBusy ? 'Rendering…' : 'Render text preview'}
              </button>
              <button
                className="cl-btn-primary"
                type="button"
                disabled={attachmentBusy !== null || !editing.active}
                onClick={() => void attachTemplate()}
              >
                <Paperclip size={15} aria-hidden="true" />
                {attachmentBusy === 'text'
                  ? 'Attaching…'
                  : 'Attach rendered text'}
              </button>
            </div>

            {attachmentError && (
              <p className="cl-inline-error" role="alert">
                {attachmentError}
              </p>
            )}
            {generatedDocumentId && selectedPatient && (
              <p className="document-template-success" role="status">
                Patient document {generatedDocumentId} was created.{' '}
                <Link
                  to={`/clinician/patients/${encodeURIComponent(
                    selectedPatient.canonicalId,
                  )}/documents`}
                >
                  Open patient documents
                </Link>
              </p>
            )}
            {rendered && (
              <div className="document-template-preview" aria-live="polite">
                <h3>Rendered text preview</h3>
                <pre>{rendered}</pre>
              </div>
            )}
            {binaryPreview && (
              <div className="document-template-preview" aria-live="polite">
                <h3>Binary text preview · {binaryPreview.fileName}</h3>
                <pre>{binaryPreview.text}</pre>
              </div>
            )}
          </section>

          <div className="document-template-detail-grid">
            <section className="cl-card" aria-labelledby="binary-versions-heading">
              <div className="cl-card-header">
                <div>
                  <h2 className="cl-card-title" id="binary-versions-heading">
                    Binary versions
                  </h2>
                  <p className="cl-card-subtitle">
                    TXT, ODT, DOCX, or non-nested ZIP; 25 MB maximum. Only TXT
                    files up to 1 MB preview inline.
                  </p>
                </div>
                <label
                  className="cl-btn-secondary"
                  aria-disabled={uploadBusy}
                >
                  <Upload size={15} aria-hidden="true" />
                  {uploadBusy ? 'Uploading…' : 'Upload version'}
                  <input
                    className="sr-only"
                    type="file"
                    disabled={uploadBusy}
                    accept=".txt,.odt,.docx,.zip"
                    onChange={(event) => {
                      const file = event.currentTarget.files?.[0]
                      event.currentTarget.value = ''
                      void uploadBinary(file)
                    }}
                  />
                </label>
              </div>

              {versions.status === 'loading' && (
                <p className="cl-empty-text" role="status">
                  Loading binary versions…
                </p>
              )}
              {versions.status === 'error' && (
                <div className="cl-inline-error" role="alert">
                  <span>{versions.message}</span>
                  <button
                    className="link-button"
                    type="button"
                    onClick={() => void loadVersions(editing.id!)}
                  >
                    Retry
                  </button>
                </div>
              )}
              {versions.status === 'ready' && versions.data.length === 0 && (
                <p className="cl-empty-text">
                  No binary template versions have been uploaded.
                </p>
              )}
              {versions.status === 'ready' && versions.data.length > 0 && (
                <>
                  <div className="cl-table-wrap">
                    <table className="cl-table">
                      <caption className="sr-only">
                        Binary versions for {editing.name}
                      </caption>
                      <thead>
                        <tr>
                          <th>Version</th>
                          <th>File</th>
                          <th>Size</th>
                          <th>
                            <span className="sr-only">Actions</span>
                          </th>
                        </tr>
                      </thead>
                      <tbody>
                        {visibleVersions.map((version) => (
                          <tr key={version.id}>
                            <td>v{version.version}</td>
                            <td>
                              {version.fileName}
                              <p className="cl-table-sub">
                                {version.mimetype} ·{' '}
                                {formatDateTime(version.createdAt)}
                              </p>
                            </td>
                            <td>{version.sizeBytes.toLocaleString()} bytes</td>
                            <td>
                              <div className="document-template-row-actions">
                                {canPreviewInline(version) ? (
                                  <button
                                    className="cl-btn-secondary"
                                    type="button"
                                    disabled={downloadBusy === version.id}
                                    onClick={() => void previewBinary(version)}
                                  >
                                    <Eye size={14} aria-hidden="true" /> Preview
                                  </button>
                                ) : (
                                  <span className="document-template-download-only">
                                    Download only
                                  </span>
                                )}
                                <button
                                  className="cl-btn-secondary"
                                  type="button"
                                  disabled={downloadBusy === version.id}
                                  onClick={() => void downloadBinary(version)}
                                >
                                  <Download size={14} aria-hidden="true" />
                                  Download
                                </button>
                                <button
                                  className="cl-btn-secondary"
                                  type="button"
                                  disabled={
                                    attachmentBusy !== null || !editing.active
                                  }
                                  onClick={() => void attachTemplate(version.id)}
                                >
                                  <Paperclip size={14} aria-hidden="true" />
                                  {attachmentBusy === version.id
                                    ? 'Attaching…'
                                    : 'Attach'}
                                </button>
                              </div>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                  {versionPages > 1 && (
                    <div className="document-template-pagination" aria-label="Binary version pages">
                      <button
                        className="cl-btn-secondary"
                        type="button"
                        disabled={versionsPage === 1}
                        onClick={() => setVersionsPage(versionsPage - 1)}
                      >
                        <ChevronLeft size={15} aria-hidden="true" /> Previous
                      </button>
                      <span>
                        Page {versionsPage} of {versionPages}
                      </span>
                      <button
                        className="cl-btn-secondary"
                        type="button"
                        disabled={versionsPage === versionPages}
                        onClick={() => setVersionsPage(versionsPage + 1)}
                      >
                        Next <ChevronRight size={15} aria-hidden="true" />
                      </button>
                    </div>
                  )}
                </>
              )}
            </section>

            <section className="cl-card" aria-labelledby="template-history-heading">
              <div className="cl-card-header">
                <div>
                  <h2 className="cl-card-title" id="template-history-heading">
                    Audit history
                  </h2>
                  <p className="cl-card-subtitle">
                    Authenticated create, update, state, upload, and patient
                    attachment events.
                  </p>
                </div>
                {history.status === 'ready' && (
                  <span className="document-template-counts">
                    {history.data.length} of {historyCount} events
                  </span>
                )}
              </div>

              {history.status === 'loading' && (
                <p className="cl-empty-text" role="status">
                  Loading audit history…
                </p>
              )}
              {history.status === 'error' && (
                <div className="cl-inline-error" role="alert">
                  <span>{history.message}</span>
                  <button
                    className="link-button"
                    type="button"
                    onClick={() => void loadHistory(editing.id!)}
                  >
                    Retry
                  </button>
                </div>
              )}
              {history.status === 'ready' && history.data.length === 0 && (
                <p className="cl-empty-text">No audit events are available.</p>
              )}
              {history.status === 'ready' && history.data.length > 0 && (
                <>
                  <ol className="document-template-history-list">
                    {visibleHistory.map((event) => (
                      <li key={event.eventId}>
                        <FileClock size={16} aria-hidden="true" />
                        <div>
                          <div>
                            <strong>{actionLabel(event.action)}</strong>
                            <time dateTime={event.occurredAt}>
                              {formatDateTime(event.occurredAt)}
                            </time>
                          </div>
                          <p>{event.summary}</p>
                          <small>
                            Actor: {event.username}
                            {event.patientId
                              ? ` · Patient: ${event.patientId}`
                              : ''}
                            {event.patientDocumentId
                              ? ` · Document: ${event.patientDocumentId}`
                              : ''}
                          </small>
                        </div>
                      </li>
                    ))}
                  </ol>
                  {historyPages > 1 && (
                    <div className="document-template-pagination" aria-label="Audit history pages">
                      <button
                        className="cl-btn-secondary"
                        type="button"
                        disabled={historyPage === 1}
                        onClick={() => setHistoryPage(historyPage - 1)}
                      >
                        <ChevronLeft size={15} aria-hidden="true" /> Previous
                      </button>
                      <span>
                        Page {historyPage} of {historyPages}
                      </span>
                      <button
                        className="cl-btn-secondary"
                        type="button"
                        disabled={historyPage === historyPages}
                        onClick={() => setHistoryPage(historyPage + 1)}
                      >
                        Next <ChevronRight size={15} aria-hidden="true" />
                      </button>
                    </div>
                  )}
                </>
              )}
            </section>
          </div>
        </>
      )}
    </div>
  )
}
