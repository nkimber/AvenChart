import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useOutletContext } from 'react-router-dom'
import { ArchiveRestore, ChevronRight, FileText, Pencil, Plus, TrendingUp } from 'lucide-react'
import {
  archiveEncounter,
  archiveEncounterDocument,
  createEncounterDocument,
  createEncounterSoapNote,
  createEncounterVitals,
  getEncounterSoapNoteTemplates,
  getEncounterAuditHistory,
  getEncounterDetail,
  getEncounterLayoutForm,
  getEncounterLayoutForms,
  moveEncounterDocument,
  replaceEncounterDocumentContent,
  restoreEncounter,
  restoreEncounterDocument,
  searchEncounters,
  signEncounter,
  saveEncounterLayoutForm,
  signEncounterDocument,
  updateEncounterDocumentMetadata,
  updateEncounter,
  type EncounterDocumentAttachment,
  type EncounterDetail,
  type EncounterListItem,
  type EncounterSoapNoteTemplate,
  type EncounterVitals,
  type EncounterAuditHistory,
  type EncounterLayoutForm,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { PatientOutletContext } from './PatientShell.tsx'

// Simple SVG sparkline for a series of numeric values
function Sparkline({ values, color = '#0f6e56' }: { values: number[]; color?: string }) {
  if (values.length < 2) return null
  const w = 80, h = 28
  const min = Math.min(...values), max = Math.max(...values)
  const range = max - min || 1
  const pts = values.map((v, i) => {
    const x = (i / (values.length - 1)) * (w - 4) + 2
    const y = h - 2 - ((v - min) / range) * (h - 4)
    return `${x},${y}`
  }).join(' ')
  return (
    <svg width={w} height={h} viewBox={`0 0 ${w} ${h}`} aria-hidden="true" className="vital-sparkline">
      <polyline points={pts} fill="none" stroke={color} strokeWidth="1.5" strokeLinejoin="round" strokeLinecap="round" />
      {values.map((v, i) => {
        const x = (i / (values.length - 1)) * (w - 4) + 2
        const y = h - 2 - ((v - min) / range) * (h - 4)
        return <circle key={i} cx={x} cy={y} r={i === values.length - 1 ? 2.5 : 1.5} fill={color} />
      })}
    </svg>
  )
}

type ListState =
  | { status: 'loading' }
  | { status: 'ready'; data: EncounterListItem[] }
  | { status: 'error'; message: string }

type DetailState =
  | { status: 'idle' }
  | { status: 'loading'; id: number }
  | { status: 'ready'; data: EncounterDetail }
  | { status: 'error'; message: string }

function vitalRow(label: string, value?: string | number | null, unit?: string) {
  if (value === null || value === undefined) return null
  return (
    <div className="cl-vital-item">
      <span className="cl-vital-value">{value}{unit ? ` ${unit}` : ''}</span>
      <span className="cl-vital-label">{label}</span>
    </div>
  )
}

function extractVitalSeries(encounters: EncounterListItem[], details: Map<number, EncounterDetail>) {
  const series: { date: string; vitals: EncounterVitals }[] = []
  for (const enc of [...encounters].reverse()) {
    const d = details.get(enc.id)
    if (d?.vitals) series.push({ date: enc.date, vitals: d.vitals })
  }
  return series
}

const BLANK_VITALS = {
  systolic: '', diastolic: '', pulse: '', temperature: '', respiration: '', oxygenSaturation: '', weight: '', height: '',
}
const BLANK_SOAP = { subjective: '', objective: '', assessment: '', plan: '' }
const today = () => new Date().toISOString().slice(0, 10)

type DocumentForm = { categoryId: string; name: string; docDate: string; notes: string; content: string }

function EncounterDocuments({ sessionId, detail, targetEncounters, onDetailChange }: { sessionId: string; detail: EncounterDetail; targetEncounters: EncounterListItem[]; onDetailChange: (detail: EncounterDetail) => void }) {
  const [addOpen, setAddOpen] = useState(false)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [reviewingId, setReviewingId] = useState<number | null>(null)
  const [replacingId, setReplacingId] = useState<number | null>(null)
  const [movingId, setMovingId] = useState<number | null>(null)
  const [replacementContent, setReplacementContent] = useState('')
  const [targetEncounter, setTargetEncounter] = useState('')
  const [saving, setSaving] = useState(false)
  const [form, setForm] = useState<DocumentForm>({ categoryId: String(detail.documents.find((document) => document.deleted === 0)?.categoryId ?? 1), name: '', docDate: today(), notes: '', content: '' })
  const [reviewForm, setReviewForm] = useState({ reviewStatus: 'Reviewed', reviewedBy: '' })

  function openAdd() {
    setEditingId(null); setReviewingId(null)
    setForm({ categoryId: String(detail.documents.find((document) => document.deleted === 0)?.categoryId ?? 1), name: '', docDate: today(), notes: '', content: '' })
    setAddOpen(true)
  }

  function openEdit(document: EncounterDocumentAttachment) {
    setAddOpen(false); setReviewingId(null); setEditingId(document.id)
    setForm({ categoryId: String(document.categoryId), name: document.name, docDate: document.docDate, notes: document.notes ?? '', content: '' })
  }

  async function saveDocument(event: React.FormEvent) {
    event.preventDefault()
    const categoryId = Number(form.categoryId)
    if (!Number.isInteger(categoryId) || categoryId <= 0) { showToast('Enter a valid numeric category ID.', 'error'); return }
    setSaving(true)
    try {
      const result = editingId == null
        ? await createEncounterDocument(sessionId, detail.encounter, { categoryId, name: form.name, docDate: form.docDate, content: form.content, notes: form.notes || null })
        : await updateEncounterDocumentMetadata(sessionId, detail.encounter, editingId, { categoryId, name: form.name, docDate: form.docDate, notes: form.notes || null })
      onDetailChange(result.detail); setAddOpen(false); setEditingId(null)
      showToast(editingId == null ? 'Text attachment added.' : 'Document filing updated.', 'success')
    } catch { showToast(editingId == null ? 'Could not add attachment.' : 'Could not update document filing.', 'error') } finally { setSaving(false) }
  }

  async function changeArchive(document: EncounterDocumentAttachment, restore = false) {
    if (!restore && !window.confirm(`Archive “${document.name}”? It can be restored later.`)) return
    setSaving(true)
    try {
      const result = restore
        ? await restoreEncounterDocument(sessionId, detail.encounter, document.id)
        : await archiveEncounterDocument(sessionId, detail.encounter, document.id)
      onDetailChange(result.detail); showToast(restore ? 'Document restored.' : 'Document archived.', 'success')
    } catch { showToast(restore ? 'Could not restore document.' : 'Could not archive document.', 'error') } finally { setSaving(false) }
  }

  async function saveReview(event: React.FormEvent, document: EncounterDocumentAttachment) {
    event.preventDefault()
    setSaving(true)
    try {
      const result = await signEncounterDocument(sessionId, detail.encounter, document.id, reviewForm)
      onDetailChange(result.detail); setReviewingId(null); showToast(`Document marked ${reviewForm.reviewStatus.toLowerCase()}.`, 'success')
    } catch { showToast('Could not record document review.', 'error') } finally { setSaving(false) }
  }

  async function replaceContent(event: React.FormEvent, document: EncounterDocumentAttachment) {
    event.preventDefault()
    if (!window.confirm(`Replace the current content of “${document.name}”? A new version will be retained by the protected document lifecycle.`)) return
    setSaving(true)
    try {
      const result = await replaceEncounterDocumentContent(sessionId, detail.encounter, document.id, { fileName: document.name, content: replacementContent })
      onDetailChange(result.detail); setReplacingId(null); setReplacementContent(''); showToast('Document content replaced as a new version.', 'success')
    } catch { showToast('Could not replace document content.', 'error') } finally { setSaving(false) }
  }

  async function moveDocument(event: React.FormEvent, document: EncounterDocumentAttachment) {
    event.preventDefault()
    const target = Number(targetEncounter)
    if (!Number.isInteger(target) || target === detail.encounter) { showToast('Choose another encounter for this patient.', 'error'); return }
    if (!window.confirm(`Move “${document.name}” to encounter #${target}?`)) return
    setSaving(true)
    try {
      const result = await moveEncounterDocument(sessionId, detail.encounter, document.id, target)
      onDetailChange(result.sourceDetail); setMovingId(null); setTargetEncounter(''); showToast('Document moved to the selected encounter.', 'success')
    } catch { showToast('Could not move document.', 'error') } finally { setSaving(false) }
  }

  return (
    <div className="cl-card">
      <div className="cl-card-header">
        <h2 className="cl-card-title">Attachments</h2>
        <button className="cl-btn-icon" type="button" aria-label="Add text attachment" onClick={openAdd}><Plus size={14} /></button>
      </div>
      {(addOpen || editingId != null) && (
        <form onSubmit={saveDocument}>
          <div className="form-row">
            <div className="field"><label className="label" htmlFor="attachment-name">Name</label><input id="attachment-name" className="input" required value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} /></div>
            <div className="field"><label className="label" htmlFor="attachment-date">Document date</label><input id="attachment-date" className="input" type="date" required value={form.docDate} onChange={(event) => setForm((current) => ({ ...current, docDate: event.target.value }))} /></div>
            <div className="field"><label className="label" htmlFor="attachment-category">Category ID</label><input id="attachment-category" className="input" type="number" min="1" required value={form.categoryId} onChange={(event) => setForm((current) => ({ ...current, categoryId: event.target.value }))} /></div>
          </div>
          {editingId == null && <div className="field" style={{ marginBottom: 10 }}><label className="label" htmlFor="attachment-content">Attachment text</label><textarea id="attachment-content" className="textarea" rows={4} required value={form.content} onChange={(event) => setForm((current) => ({ ...current, content: event.target.value }))} /></div>}
          <div className="field" style={{ marginBottom: 10 }}><label className="label" htmlFor="attachment-notes">Filing note</label><input id="attachment-notes" className="input" value={form.notes} onChange={(event) => setForm((current) => ({ ...current, notes: event.target.value }))} /></div>
          <div className="cl-inline-form-actions"><button className="cl-btn-primary" type="submit" disabled={saving}>{saving ? 'Saving…' : editingId == null ? 'Add attachment' : 'Save filing'}</button><button className="cl-btn-secondary" type="button" disabled={saving} onClick={() => { setAddOpen(false); setEditingId(null) }}>Cancel</button></div>
        </form>
      )}
      {detail.documents.length === 0 && !addOpen && <p className="cl-empty-text">No encounter attachments. Add a text attachment to begin this local workflow.</p>}
      {detail.documents.map((document) => (
        <div key={document.id} className="cl-soap-section" style={{ opacity: document.deleted ? 0.65 : 1 }}>
          <div className="cl-card-header"><p className="cl-soap-label">{document.name}</p><span className="cl-badge cl-badge-muted">{document.deleted ? 'Archived' : document.reviewStatus}</span></div>
          <p className="cl-empty-text">{document.categoryName} · {document.docDate} · {document.versionLabel}{document.reviewedBy ? ` · ${document.reviewedBy}` : ''}</p>
          {document.notes && <p className="cl-soap-text">{document.notes}</p>}
          {!document.deleted && <div className="cl-inline-form-actions"><button className="cl-btn-secondary" type="button" onClick={() => openEdit(document)} disabled={saving}>Edit filing</button><button className="cl-btn-secondary" type="button" onClick={() => { setReplacingId(document.id); setMovingId(null); setReviewingId(null); setReplacementContent('') }} disabled={saving}>Replace text</button><button className="cl-btn-secondary" type="button" onClick={() => { setMovingId(document.id); setReplacingId(null); setReviewingId(null); setTargetEncounter('') }} disabled={saving}>Move</button><button className="cl-btn-secondary" type="button" onClick={() => { setReviewingId(document.id); setReplacingId(null); setMovingId(null); setReviewForm({ reviewStatus: 'Reviewed', reviewedBy: '' }) }} disabled={saving}>Review</button><button className="cl-btn-secondary" type="button" onClick={() => changeArchive(document)} disabled={saving}><ArchiveRestore size={14} /> Archive</button></div>}
          {document.deleted && <div className="cl-inline-form-actions"><button className="cl-btn-secondary" type="button" onClick={() => changeArchive(document, true)} disabled={saving}>Restore</button></div>}
          {replacingId === document.id && <form onSubmit={(event) => replaceContent(event, document)} style={{ marginTop: 10 }}><div className="field"><label className="label" htmlFor={`replacement-${document.id}`}>Replacement text</label><textarea id={`replacement-${document.id}`} className="textarea" rows={4} required value={replacementContent} onChange={(event) => setReplacementContent(event.target.value)} /></div><div className="cl-inline-form-actions"><button className="cl-btn-primary" type="submit" disabled={saving}>Replace content</button><button className="cl-btn-secondary" type="button" disabled={saving} onClick={() => setReplacingId(null)}>Cancel</button></div></form>}
          {movingId === document.id && <form onSubmit={(event) => moveDocument(event, document)} style={{ marginTop: 10 }}><div className="field"><label className="label" htmlFor={`move-${document.id}`}>Target encounter</label><select id={`move-${document.id}`} className="input" required value={targetEncounter} onChange={(event) => setTargetEncounter(event.target.value)}><option value="">Choose encounter</option>{targetEncounters.filter((encounter) => encounter.encounter !== detail.encounter).map((encounter) => <option key={encounter.encounter} value={encounter.encounter}>#{encounter.encounter} · {encounter.date} · {encounter.reason ?? 'Visit'}</option>)}</select></div><div className="cl-inline-form-actions"><button className="cl-btn-primary" type="submit" disabled={saving}>Move attachment</button><button className="cl-btn-secondary" type="button" disabled={saving} onClick={() => setMovingId(null)}>Cancel</button></div></form>}
          {reviewingId === document.id && <form onSubmit={(event) => saveReview(event, document)} style={{ marginTop: 10 }}><div className="form-row"><div className="field"><label className="label" htmlFor={`review-status-${document.id}`}>Review decision</label><select id={`review-status-${document.id}`} className="input" value={reviewForm.reviewStatus} onChange={(event) => setReviewForm((current) => ({ ...current, reviewStatus: event.target.value }))}><option>Reviewed</option><option>Signed</option><option>Denied</option></select></div><div className="field"><label className="label" htmlFor={`reviewer-${document.id}`}>Reviewed by</label><input id={`reviewer-${document.id}`} className="input" required value={reviewForm.reviewedBy} onChange={(event) => setReviewForm((current) => ({ ...current, reviewedBy: event.target.value }))} /></div></div><div className="cl-inline-form-actions"><button className="cl-btn-primary" type="submit" disabled={saving}>Save review</button><button className="cl-btn-secondary" type="button" onClick={() => setReviewingId(null)} disabled={saving}>Cancel</button></div></form>}
        </div>
      ))}
    </div>
  )
}

function EncounterSignatures({ sessionId, username, detail, onDetailChange }: { sessionId: string; username: string; detail: EncounterDetail; onDetailChange: (detail: EncounterDetail) => void }) {
  const [open, setOpen] = useState(false)
  const [saving, setSaving] = useState(false)
  const [form, setForm] = useState({ signerUsername: username, isLock: false, amendment: '' })

  function openForm() {
    setForm({ signerUsername: username, isLock: false, amendment: '' })
    setOpen(true)
  }

  async function saveSignature(event: React.FormEvent) {
    event.preventDefault()
    if (!window.confirm(form.isLock ? 'Record and lock this encounter signature?' : 'Record this encounter signature?')) return
    setSaving(true)
    try {
      const result = await signEncounter(sessionId, detail.encounter, { signerUsername: form.signerUsername, signedAt: new Date().toISOString(), isLock: form.isLock, amendment: form.amendment || null })
      onDetailChange(result.detail); setOpen(false); showToast(form.amendment ? 'Signature and amendment recorded.' : 'Encounter signature recorded.', 'success')
    } catch { showToast('Could not record encounter signature.', 'error') } finally { setSaving(false) }
  }

  return (
    <div className="cl-card">
      <div className="cl-card-header"><h2 className="cl-card-title">Signatures and amendments</h2><button className="cl-btn-secondary" type="button" onClick={openForm} disabled={saving}>Record signature</button></div>
      {open && <form onSubmit={saveSignature}><div className="form-row"><div className="field"><label className="label" htmlFor="encounter-signer">Signer</label><input id="encounter-signer" className="input" readOnly value={form.signerUsername} /></div><div className="field"><label className="label" htmlFor="encounter-lock">Lock encounter</label><select id="encounter-lock" className="input" value={form.isLock ? 'yes' : 'no'} onChange={(event) => setForm((current) => ({ ...current, isLock: event.target.value === 'yes' }))}><option value="no">No — signature only</option><option value="yes">Yes — lock this signature</option></select></div></div><div className="field" style={{ marginBottom: 10 }}><label className="label" htmlFor="encounter-amendment">Amendment note (optional)</label><textarea id="encounter-amendment" className="textarea" rows={3} value={form.amendment} onChange={(event) => setForm((current) => ({ ...current, amendment: event.target.value }))} /></div><div className="cl-inline-form-actions"><button className="cl-btn-primary" type="submit" disabled={saving}>{saving ? 'Saving…' : 'Record signature'}</button><button className="cl-btn-secondary" type="button" disabled={saving} onClick={() => setOpen(false)}>Cancel</button></div></form>}
      {detail.signatures.length === 0 && !open && <p className="cl-empty-text">No encounter signatures recorded.</p>}
      {detail.signatures.map((signature) => <div key={signature.id} className="cl-soap-section"><div className="cl-card-header"><p className="cl-soap-label">{signature.signerUsername}</p><span className="cl-badge cl-badge-muted">{signature.isLock ? 'Locked' : 'Signed'}</span></div><p className="cl-empty-text">{signature.signedAt}</p>{signature.amendment && <p className="cl-soap-text">Amendment: {signature.amendment}</p>}</div>)}
      {detail.amendmentHistory.length > 0 && <div style={{ marginTop: 12 }}><p className="cl-soap-label">Amendment history</p>{detail.amendmentHistory.map((amendment) => <p key={amendment.signatureId} className="cl-empty-text">{amendment.signedAt} · {amendment.signerUsername}: {amendment.amendment}</p>)}</div>}
    </div>
  )
}

function EncounterAudit({ sessionId, detail }: { sessionId: string; detail: EncounterDetail }) {
  const [history, setHistory] = useState<EncounterAuditHistory | null>(null)
  const [expanded, setExpanded] = useState(false)
  const [failed, setFailed] = useState(false)

  async function load() {
    try { setHistory(await getEncounterAuditHistory(sessionId, detail.encounter)); setFailed(false) }
    catch { setFailed(true) }
  }

  return <div className="cl-card">
    <div className="cl-card-header"><h2 className="cl-card-title">Encounter audit</h2><button className="cl-btn-secondary" type="button" onClick={() => { setExpanded((current) => !current); if (!history) void load() }}>{expanded ? 'Hide history' : 'View history'}</button></div>
    {!expanded && <p className="cl-empty-text">Local summary changes retain actor, time, action, and changed-field evidence without duplicating clinical values.</p>}
    {expanded && !history && !failed && <p className="cl-empty-text">Loading audit historyâ€¦</p>}
    {expanded && failed && <p className="cl-empty-text">Audit history could not be loaded.</p>}
    {expanded && history?.events.length === 0 && <p className="cl-empty-text">No audited summary changes for this encounter.</p>}
    {expanded && history?.events.map((event) => <div key={event.eventId} className="cl-soap-section"><div className="cl-card-header"><p className="cl-soap-label">{event.action}</p><span className="cl-badge cl-badge-muted">{event.username}</span></div><p className="cl-empty-text">{new Date(event.occurredAt).toLocaleString()} Â· {event.changedFields.join(', ')}</p></div>)}
  </div>
}

function EncounterLayoutFormPanel({ sessionId, encounter }: { sessionId: string; encounter: number }) {
  const [forms, setForms] = useState<{ key: string; title: string }[]>([])
  const [selectedKey, setSelectedKey] = useState('')
  const [form, setForm] = useState<EncounterLayoutForm | null>(null)
  const [values, setValues] = useState<Record<string, string>>({})
  const [open, setOpen] = useState(false)
  const [loading, setLoading] = useState(false)
  const [savingForm, setSavingForm] = useState(false)

  function initialValues(detail: EncounterLayoutForm) {
    const saved = detail.latestRecord?.values ?? {}
    return Object.fromEntries(detail.groups.flatMap((group) => group.fields.map((field) => [field.key, saved[field.key] ?? field.defaultValue ?? field.options.find((option) => option.isDefault)?.key ?? ''])))
  }

  useEffect(() => {
    let cancelled = false
    getEncounterLayoutForms(sessionId, encounter).then((catalog) => {
      if (cancelled) return
      setForms(catalog.forms)
      setSelectedKey(catalog.forms[0]?.key ?? '')
    }).catch(() => { if (!cancelled) setForms([]) })
    return () => { cancelled = true }
  }, [encounter, sessionId])

  async function load() {
    if (!selectedKey) return
    setLoading(true)
    try { const detail = await getEncounterLayoutForm(sessionId, encounter, selectedKey); setForm(detail); setValues(initialValues(detail)); setOpen(true) }
    catch { showToast('Could not load the configured form.', 'error') }
    finally { setLoading(false) }
  }

  async function save(event: React.FormEvent) {
    event.preventDefault()
    if (!form) return
    setSavingForm(true)
    try { const saved = await saveEncounterLayoutForm(sessionId, encounter, form.layoutKey, values); setForm(saved); setValues(initialValues(saved)); showToast(`${saved.title} saved as revision ${saved.latestRecord?.revision}.`, 'success') }
    catch { showToast('Could not save the configured form. Complete required fields and use valid list values.', 'error') }
    finally { setSavingForm(false) }
  }

  if (forms.length === 0) return null
  return <section className="cl-card">
    <div className="cl-card-header"><div><h2 className="cl-card-title">Configured encounter form</h2><p className="cl-empty-text">Layout-backed values are saved as immutable revisions and do not modify core demographics.</p></div><div className="cl-inline-form-actions"><select className="input" value={selectedKey} onChange={(event) => { setSelectedKey(event.target.value); setOpen(false); setForm(null) }}>{forms.map((item) => <option key={item.key} value={item.key}>{item.title}</option>)}</select><button className="cl-btn-secondary" type="button" onClick={() => void load()} disabled={loading}>{loading ? 'Loading…' : open ? 'Reload' : 'Open form'}</button></div></div>
    {open && form && <form onSubmit={save}>{form.groups.map((group) => <fieldset key={group.key} className="cl-soap-section"><legend className="cl-soap-label">{group.title}</legend>{group.fields.map((field) => <div className="field" key={field.key} style={{ marginBottom: 10 }}><label className="label" htmlFor={`layout-${form.layoutKey}-${field.key}`}>{field.label}{field.required ? ' *' : ''}</label>{field.fieldType === 'textarea' ? <textarea id={`layout-${form.layoutKey}-${field.key}`} className="textarea" rows={3} maxLength={field.maxLength || undefined} value={values[field.key] ?? ''} onChange={(event) => setValues((current) => ({ ...current, [field.key]: event.target.value }))} required={field.required} /> : field.fieldType === 'select' ? <select id={`layout-${form.layoutKey}-${field.key}`} className="input" value={values[field.key] ?? ''} onChange={(event) => setValues((current) => ({ ...current, [field.key]: event.target.value }))} required={field.required}><option value="">Select…</option>{field.options.map((option) => <option key={option.key} value={option.key}>{option.title}</option>)}</select> : field.fieldType === 'checkbox' ? <label><input id={`layout-${form.layoutKey}-${field.key}`} type="checkbox" checked={values[field.key] === 'true'} onChange={(event) => setValues((current) => ({ ...current, [field.key]: event.target.checked ? 'true' : 'false' }))} /> Yes</label> : <input id={`layout-${form.layoutKey}-${field.key}`} className="input" type={field.fieldType === 'number' ? 'number' : field.fieldType === 'date' ? 'date' : 'text'} maxLength={field.maxLength || undefined} value={values[field.key] ?? ''} onChange={(event) => setValues((current) => ({ ...current, [field.key]: event.target.value }))} required={field.required} />}</div>)}</fieldset>)}<div className="cl-inline-form-actions"><button className="cl-btn-primary" type="submit" disabled={savingForm}>{savingForm ? 'Saving…' : form.latestRecord ? 'Save new revision' : 'Save form'}</button><button className="cl-btn-secondary" type="button" onClick={() => setOpen(false)} disabled={savingForm}>Close</button></div>{form.latestRecord && <p className="cl-empty-text">Latest revision {form.latestRecord.revision} saved by {form.latestRecord.savedBy} at {new Date(form.latestRecord.savedAt).toLocaleString()}.</p>}</form>}
  </section>
}

export default function PatientEncounters() {
  const { session, patientId } = useOutletContext<PatientOutletContext>()
  const navigate = useNavigate()
  const [listState, setListState] = useState<ListState>({ status: 'loading' })
  const [detailState, setDetailState] = useState<DetailState>({ status: 'idle' })
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [detailCache, setDetailCache] = useState<Map<number, EncounterDetail>>(new Map())
  const [showTrends, setShowTrends] = useState(false)
  const [addVitalsOpen, setAddVitalsOpen] = useState(false)
  const [addSoapOpen, setAddSoapOpen] = useState(false)
  const [vitalsForm, setVitalsForm] = useState(BLANK_VITALS)
  const [soapForm, setSoapForm] = useState(BLANK_SOAP)
  const [soapTemplates, setSoapTemplates] = useState<EncounterSoapNoteTemplate[]>([])
  const [soapTemplateError, setSoapTemplateError] = useState<string | null>(null)
  const [selectedSoapTemplateId, setSelectedSoapTemplateId] = useState('')
  const [saving, setSaving] = useState(false)
  const [showArchived, setShowArchived] = useState(false)
  const [archiving, setArchiving] = useState(false)
  const [editSummaryOpen, setEditSummaryOpen] = useState(false)
  const [summaryForm, setSummaryForm] = useState({ reason: '', sensitivity: '', referralSource: '', externalId: '', posCode: '', billingNote: '' })

  useEffect(() => {
    setDetailCache(new Map())
    searchEncounters(session.sessionId, { patientId, limit: 50, archived: showArchived })
      .then((data) => setListState({ status: 'ready', data: data.encounters }))
      .catch((err) => setListState({ status: 'error', message: err instanceof Error ? err.message : 'Failed to load.' }))
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [patientId, showArchived])

  useEffect(() => {
    let cancelled = false
    getEncounterSoapNoteTemplates(session.sessionId)
      .then((catalog) => {
        if (cancelled) return
        setSoapTemplates(catalog.templates)
        setSelectedSoapTemplateId((current) => current || catalog.templates.find((template) => template.isDefault)?.templateId || '')
      })
      .catch(() => {
        if (!cancelled) setSoapTemplateError('SOAP templates are unavailable. You can still write a note manually.')
      })
    return () => { cancelled = true }
  }, [session.sessionId])

  async function changeArchiveState(encounter: number, restore = false) {
    if (!restore && !window.confirm('Archive this encounter? Its notes, vitals, signatures, and documents remain intact and can be restored.')) return
    setArchiving(true)
    try {
      if (restore) await restoreEncounter(session.sessionId, encounter)
      else await archiveEncounter(session.sessionId, encounter)
      showToast(restore ? 'Encounter restored.' : 'Encounter archived.', 'success')
      setSelectedId(null)
      setDetailState({ status: 'idle' })
      const response = await searchEncounters(session.sessionId, { patientId, limit: 50, archived: showArchived })
      setListState({ status: 'ready', data: response.encounters })
    } catch { showToast(restore ? 'Could not restore encounter.' : 'Could not archive encounter.', 'error') }
    finally { setArchiving(false) }
  }

  function openSummaryEditor(enc: EncounterDetail) {
    setSummaryForm({ reason: enc.reason ?? '', sensitivity: enc.sensitivity ?? '', referralSource: enc.referralSource ?? '', externalId: enc.externalId ?? '', posCode: enc.posCode?.toString() ?? '', billingNote: enc.billingNote ?? '' })
    setEditSummaryOpen(true)
  }

  async function saveSummary(event: React.FormEvent, encounter: number) {
    event.preventDefault(); setSaving(true)
    try {
      const updated = await updateEncounter(session.sessionId, encounter, { reason: summaryForm.reason, sensitivity: summaryForm.sensitivity || null, referralSource: summaryForm.referralSource || null, externalId: summaryForm.externalId || null, posCode: summaryForm.posCode ? Number(summaryForm.posCode) : null, billingNote: summaryForm.billingNote || null })
      setDetailState({ status: 'ready', data: updated }); setDetailCache((current) => new Map(current).set(updated.id, updated)); setEditSummaryOpen(false); showToast('Encounter summary updated.', 'success')
    } catch { showToast('Could not update encounter summary.', 'error') } finally { setSaving(false) }
  }

  const vitalSeries = useMemo(() => {
    if (listState.status !== 'ready') return []
    return extractVitalSeries(listState.data, detailCache)
  }, [listState, detailCache])
  const selectedSoapTemplate = soapTemplates.find((template) => template.templateId === selectedSoapTemplateId)

  function openEncounter(id: number) {
    setSelectedId(id)
    setAddVitalsOpen(false)
    setAddSoapOpen(false)
    setEditSummaryOpen(false)
    setVitalsForm(BLANK_VITALS)
    setSoapForm(BLANK_SOAP)
    setDetailState({ status: 'loading', id })
    getEncounterDetail(session.sessionId, id, undefined, true)
      .then((data) => {
        setDetailState({ status: 'ready', data })
        setDetailCache((prev) => new Map(prev).set(id, data))
      })
      .catch((err) => setDetailState({ status: 'error', message: err instanceof Error ? err.message : 'Failed to load.' }))
  }

  async function handleAddVitals(e: React.FormEvent) {
    e.preventDefault()
    if (selectedId == null) return
    setSaving(true)
    try {
      await createEncounterVitals(session.sessionId, selectedId, {
        dateTime: new Date().toISOString().replace('T', ' ').slice(0, 19),
        systolic: vitalsForm.systolic ? Number(vitalsForm.systolic) : undefined,
        diastolic: vitalsForm.diastolic ? Number(vitalsForm.diastolic) : undefined,
        pulse: vitalsForm.pulse ? Number(vitalsForm.pulse) : undefined,
        temperature: vitalsForm.temperature ? Number(vitalsForm.temperature) : undefined,
        respiration: vitalsForm.respiration ? Number(vitalsForm.respiration) : undefined,
        oxygenSaturation: vitalsForm.oxygenSaturation ? Number(vitalsForm.oxygenSaturation) : undefined,
        weight: vitalsForm.weight ? Number(vitalsForm.weight) : undefined,
        height: vitalsForm.height ? Number(vitalsForm.height) : undefined,
      })
      showToast('Vitals recorded.', 'success')
      setAddVitalsOpen(false)
      setVitalsForm(BLANK_VITALS)
      openEncounter(selectedId)
    } catch { showToast('Could not record vitals.', 'error') }
    finally { setSaving(false) }
  }

  async function handleAddSoap(e: React.FormEvent) {
    e.preventDefault()
    if (selectedId == null) return
    setSaving(true)
    try {
      await createEncounterSoapNote(session.sessionId, selectedId, {
        dateTime: new Date().toISOString().replace('T', ' ').slice(0, 19),
        ...soapForm,
      })
      showToast('SOAP note saved.', 'success')
      setAddSoapOpen(false)
      setSoapForm(BLANK_SOAP)
      openEncounter(selectedId)
    } catch { showToast('Could not save SOAP note.', 'error') }
    finally { setSaving(false) }
  }

  function applySoapTemplate() {
    const template = soapTemplates.find((item) => item.templateId === selectedSoapTemplateId)
    if (!template) return
    const hasDraft = Object.values(soapForm).some(Boolean)
    if (hasDraft && !window.confirm('Apply this template and replace the current SOAP draft?')) return
    setSoapForm({
      subjective: template.subjective,
      objective: template.objective,
      assessment: template.assessment,
      plan: template.plan,
    })
  }

  return (
    <div className="clinician-page">
      {/* Vitals trend panel */}
      {vitalSeries.length >= 2 && (
        <section className="cl-card" style={{ marginBottom: 16 }}>
          <div className="cl-card-header">
            <h2 className="cl-card-title"><TrendingUp size={15} /> Vital trends ({vitalSeries.length} visits)</h2>
            <button className="cl-link" type="button" onClick={() => setShowTrends((s) => !s)}>
              {showTrends ? 'Hide' : 'Show'}
            </button>
          </div>
          {showTrends && (
            <div className="vital-trends-grid">
              {[
                { label: 'Systolic BP', key: 'systolic' as const, color: '#993c1d' },
                { label: 'Diastolic BP', key: 'diastolic' as const, color: '#d97706' },
                { label: 'Pulse', key: 'pulse' as const, color: '#0f6e56' },
                { label: 'Weight (lbs)', key: 'weight' as const, color: '#7c3aed' },
                { label: 'O₂ Sat (%)', key: 'oxygenSaturation' as const, color: '#0891b2' },
                { label: 'Temp (°F)', key: 'temperature' as const, color: '#db2777' },
              ].map(({ label, key, color }) => {
                const vals = vitalSeries
                  .map((s) => s.vitals[key])
                  .filter((v): v is number => v != null)
                if (vals.length < 2) return null
                const latest = vals[vals.length - 1]
                return (
                  <div key={key} className="vital-trend-item">
                    <div className="vital-trend-top">
                      <span className="vital-trend-label">{label}</span>
                      <span className="vital-trend-value">{latest}</span>
                    </div>
                    <Sparkline values={vals} color={color} />
                  </div>
                )
              })}
            </div>
          )}
        </section>
      )}

      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 12 }}>
        <button className="cl-btn-secondary" type="button" onClick={() => { setSelectedId(null); setDetailState({ status: 'idle' }); setShowArchived((value) => !value) }} style={{ marginRight: 8 }}>
          {showArchived ? 'Show active' : 'Show archived'}
        </button>
        <button
          className="cl-btn-primary"
          type="button"
          onClick={() => navigate(`/clinician/patients/${patientId}/encounters/new`)}
        >
          <Plus size={14} /> New encounter
        </button>
      </div>

      <div className="cl-encounter-layout">
        {/* Encounter list */}
        <aside className="cl-encounter-list">
          {listState.status === 'loading' && (
            <div className="skeleton-list">
              {[0, 1, 2, 3].map((i) => <div key={i} className="skeleton-row" style={{ height: 64 }} />)}
            </div>
          )}
          {listState.status === 'error' && <p className="cl-empty-text">{listState.message}</p>}
          {listState.status === 'ready' && listState.data.length === 0 && (
            <p className="cl-empty-text">No encounters on file.</p>
          )}
          {listState.status === 'ready' && listState.data.map((enc) => (
            <button
              key={enc.id}
              className={`cl-encounter-item${selectedId === enc.id ? ' cl-encounter-item-active' : ''}`}
              type="button"
              onClick={() => openEncounter(enc.id)}
            >
              <div className="cl-encounter-item-inner">
                <div>
                  <p className="cl-encounter-date">{enc.date}</p>
                  <p className="cl-encounter-reason">{enc.reason ?? 'Visit'}</p>
                  {enc.diagnosisText && <p className="cl-encounter-dx">{enc.diagnosisText}</p>}
                </div>
                <ChevronRight size={14} />
              </div>
              <div className="cl-encounter-badges">
                {enc.hasSoapNote && <span className="cl-badge cl-badge-teal">SOAP</span>}
                {enc.hasVitals && <span className="cl-badge cl-badge-blue">Vitals</span>}
                {enc.billingLineCount > 0 && <span className="cl-badge cl-badge-muted">{enc.billingLineCount} billing</span>}
              </div>
            </button>
          ))}
        </aside>

        {/* Encounter detail */}
        <section className="cl-encounter-detail">
          {detailState.status === 'idle' && (
            <div className="cl-encounter-empty">
              <FileText size={40} />
              <p>Select an encounter to view details.</p>
            </div>
          )}
          {detailState.status === 'loading' && (
            <div className="skeleton-list">
              {[0, 1, 2].map((i) => <div key={i} className="skeleton-row" style={{ height: 80 }} />)}
            </div>
          )}
          {detailState.status === 'error' && <p className="cl-empty-text">{detailState.message}</p>}
          {detailState.status === 'ready' && (() => {
            const { data: enc } = detailState
            return (
              <>
                <div className="cl-card">
                  <div className="cl-card-header">
                    <h2 className="cl-card-title">
                      {enc.date} — {enc.reason ?? 'Visit'}
                    </h2>
                    <span className="cl-badge cl-badge-muted">Enc #{enc.encounter}</span>
                  </div>
                  <div className="cl-inline-form-actions" style={{ marginTop: 10 }}>
                    <button className="cl-btn-secondary" type="button" onClick={() => openSummaryEditor(enc)} disabled={saving}>
                      <Pencil size={14} /> Edit summary
                    </button>
                  </div>
                  {editSummaryOpen && (
                    <form onSubmit={(event) => saveSummary(event, enc.encounter)} style={{ marginTop: 14 }}>
                      <div className="form-row">
                        <div className="field">
                          <label className="label" htmlFor="encounter-reason">Reason</label>
                          <input id="encounter-reason" className="input" required value={summaryForm.reason} onChange={(event) => setSummaryForm((form) => ({ ...form, reason: event.target.value }))} />
                        </div>
                        <div className="field">
                          <label className="label" htmlFor="encounter-pos">Place of service</label>
                          <input id="encounter-pos" className="input" type="number" min="0" value={summaryForm.posCode} onChange={(event) => setSummaryForm((form) => ({ ...form, posCode: event.target.value }))} />
                        </div>
                      </div>
                      <div className="form-row">
                        <div className="field">
                          <label className="label" htmlFor="encounter-sensitivity">Sensitivity</label>
                          <input id="encounter-sensitivity" className="input" value={summaryForm.sensitivity} onChange={(event) => setSummaryForm((form) => ({ ...form, sensitivity: event.target.value }))} />
                        </div>
                        <div className="field">
                          <label className="label" htmlFor="encounter-referral">Referral source</label>
                          <input id="encounter-referral" className="input" value={summaryForm.referralSource} onChange={(event) => setSummaryForm((form) => ({ ...form, referralSource: event.target.value }))} />
                        </div>
                      </div>
                      <div className="form-row">
                        <div className="field">
                          <label className="label" htmlFor="encounter-external-id">External reference</label>
                          <input id="encounter-external-id" className="input" value={summaryForm.externalId} onChange={(event) => setSummaryForm((form) => ({ ...form, externalId: event.target.value }))} />
                        </div>
                        <div className="field">
                          <label className="label" htmlFor="encounter-billing-note">Billing note</label>
                          <input id="encounter-billing-note" className="input" value={summaryForm.billingNote} onChange={(event) => setSummaryForm((form) => ({ ...form, billingNote: event.target.value }))} />
                        </div>
                      </div>
                      <div className="cl-inline-form-actions">
                        <button className="cl-btn-primary" type="submit" disabled={saving}>{saving ? 'Saving…' : 'Save summary'}</button>
                        <button className="cl-btn-secondary" type="button" onClick={() => setEditSummaryOpen(false)} disabled={saving}>Cancel</button>
                      </div>
                    </form>
                  )}
                  <div className="cl-inline-form-actions" style={{ marginTop: 10 }}>
                    <button className="cl-btn-secondary" type="button" disabled={archiving} onClick={() => changeArchiveState(enc.encounter, showArchived)}>{archiving ? 'Saving…' : showArchived ? 'Restore encounter' : 'Archive encounter'}</button>
                  </div>
                  <ul className="fact-list">
                    {enc.providerName && <li className="fact-row"><span>Provider</span><span>{enc.providerName}</span></li>}
                    {enc.facilityName && <li className="fact-row"><span>Facility</span><span>{enc.facilityName}</span></li>}
                    {enc.diagnosisText && <li className="fact-row"><span>Diagnosis</span><span>{enc.diagnosisCode} — {enc.diagnosisText}</span></li>}
                  </ul>
                </div>

                <div className="cl-card">
                  <div className="cl-card-header">
                    <h2 className="cl-card-title">Vitals</h2>
                    <button className="cl-btn-icon" type="button" aria-label="Record vitals" onClick={() => { setAddVitalsOpen((o) => !o); setAddSoapOpen(false) }}>
                      <Plus size={14} />
                    </button>
                  </div>
                  {addVitalsOpen && (
                    <form className="cl-vitals-form" onSubmit={handleAddVitals}>
                      <div className="cl-vitals-input-grid">
                        {[
                          { id: 'v-sys', label: 'Systolic', key: 'systolic' as const, placeholder: '120' },
                          { id: 'v-dia', label: 'Diastolic', key: 'diastolic' as const, placeholder: '80' },
                          { id: 'v-pulse', label: 'Pulse (bpm)', key: 'pulse' as const, placeholder: '72' },
                          { id: 'v-temp', label: 'Temp (°F)', key: 'temperature' as const, placeholder: '98.6' },
                          { id: 'v-resp', label: 'Resp (/min)', key: 'respiration' as const, placeholder: '16' },
                          { id: 'v-o2', label: 'O₂ Sat (%)', key: 'oxygenSaturation' as const, placeholder: '99' },
                          { id: 'v-wt', label: 'Weight (lbs)', key: 'weight' as const, placeholder: '150' },
                          { id: 'v-ht', label: 'Height (in)', key: 'height' as const, placeholder: '68' },
                        ].map(({ id, label, key, placeholder }) => (
                          <div key={key} className="field">
                            <label className="label" htmlFor={id}>{label}</label>
                            <input id={id} type="number" step="0.1" className="input" placeholder={placeholder}
                              value={vitalsForm[key]} onChange={(e) => setVitalsForm((f) => ({ ...f, [key]: e.target.value }))} />
                          </div>
                        ))}
                      </div>
                      <div className="cl-inline-form-actions">
                        <button className="cl-btn-primary" type="submit" disabled={saving}>{saving ? 'Saving…' : 'Record vitals'}</button>
                        <button className="cl-btn-secondary" type="button" onClick={() => setAddVitalsOpen(false)}>Cancel</button>
                      </div>
                    </form>
                  )}
                  {enc.vitals ? (
                    <div className="cl-vitals-grid">
                      {vitalRow('BP', enc.vitals.bloodPressure ?? (enc.vitals.systolic ? `${enc.vitals.systolic}/${enc.vitals.diastolic}` : null))}
                      {vitalRow('Pulse', enc.vitals.pulse, 'bpm')}
                      {vitalRow('Temp', enc.vitals.temperature, '°F')}
                      {vitalRow('Resp', enc.vitals.respiration, '/min')}
                      {vitalRow('O₂ Sat', enc.vitals.oxygenSaturation, '%')}
                      {vitalRow('Weight', enc.vitals.weight, 'lbs')}
                      {vitalRow('Height', enc.vitals.height, 'in')}
                      {vitalRow('BMI', enc.vitals.bmi)}
                    </div>
                  ) : !addVitalsOpen && (
                    <p className="cl-empty-text">No vitals recorded. <button className="cl-link" type="button" onClick={() => setAddVitalsOpen(true)}>Add vitals</button></p>
                  )}
                </div>

                <div className="cl-card">
                  <div className="cl-card-header">
                    <h2 className="cl-card-title">SOAP note</h2>
                    <button className="cl-btn-icon" type="button" aria-label="Add SOAP note" onClick={() => {
                      setAddSoapOpen((o) => !o); setAddVitalsOpen(false)
                      if (enc.soapNote) setSoapForm({
                        subjective: enc.soapNote.subjective ?? '',
                        objective: enc.soapNote.objective ?? '',
                        assessment: enc.soapNote.assessment ?? '',
                        plan: enc.soapNote.plan ?? '',
                      })
                    }}>
                      <Plus size={14} />
                    </button>
                  </div>
                  {addSoapOpen && (
                    <form onSubmit={handleAddSoap}>
                      <div className="form-row" style={{ alignItems: 'end', marginBottom: 12 }}>
                        <div className="field">
                          <label className="label" htmlFor="soap-template">SOAP template</label>
                          <select id="soap-template" className="input" value={selectedSoapTemplateId} onChange={(event) => setSelectedSoapTemplateId(event.target.value)}>
                            <option value="">Manual SOAP note</option>
                            {soapTemplates.map((template) => <option key={template.templateId} value={template.templateId}>{template.category}: {template.name}</option>)}
                          </select>
                        </div>
                        <div className="field" style={{ flex: '0 0 auto' }}>
                          <button className="cl-btn-secondary" type="button" onClick={applySoapTemplate} disabled={!selectedSoapTemplate}>Apply template</button>
                        </div>
                      </div>
                      {selectedSoapTemplate && <p className="cl-empty-text" style={{ marginTop: -4, marginBottom: 12 }}>{selectedSoapTemplate.description}</p>}
                      {soapTemplateError && <p className="cl-empty-text" style={{ marginTop: -4, marginBottom: 12 }}>{soapTemplateError}</p>}
                      {(['subjective', 'objective', 'assessment', 'plan'] as const).map((field) => (
                        <div key={field} className="field" style={{ marginBottom: 10 }}>
                          <label className="label" htmlFor={`soap-${field}`} style={{ textTransform: 'capitalize' }}>{field}</label>
                          <textarea id={`soap-${field}`} className="textarea" rows={3}
                            value={soapForm[field]} onChange={(e) => setSoapForm((f) => ({ ...f, [field]: e.target.value }))} />
                        </div>
                      ))}
                      <div className="cl-inline-form-actions">
                        <button className="cl-btn-primary" type="submit" disabled={saving}>{saving ? 'Saving…' : 'Save SOAP note'}</button>
                        <button className="cl-btn-secondary" type="button" onClick={() => setAddSoapOpen(false)}>Cancel</button>
                      </div>
                    </form>
                  )}
                  {enc.soapNote && (enc.soapNote.subjective ?? enc.soapNote.objective ?? enc.soapNote.assessment ?? enc.soapNote.plan) ? (
                    [
                      { label: 'Subjective', text: enc.soapNote.subjective },
                      { label: 'Objective', text: enc.soapNote.objective },
                      { label: 'Assessment', text: enc.soapNote.assessment },
                      { label: 'Plan', text: enc.soapNote.plan },
                    ].filter((s) => s.text).map((s) => (
                      <div key={s.label} className="cl-soap-section">
                        <p className="cl-soap-label">{s.label}</p>
                        <p className="cl-soap-text">{s.text}</p>
                      </div>
                    ))
                  ) : !addSoapOpen && (
                    <p className="cl-empty-text">No SOAP note. <button className="cl-link" type="button" onClick={() => setAddSoapOpen(true)}>Add note</button></p>
                  )}
                </div>

                <EncounterLayoutFormPanel sessionId={session.sessionId} encounter={enc.encounter} />

                <EncounterSignatures
                  sessionId={session.sessionId}
                  username={session.username}
                  detail={enc}
                  onDetailChange={(updated) => {
                    setDetailState({ status: 'ready', data: updated })
                    setDetailCache((current) => new Map(current).set(updated.id, updated))
                  }}
                />

                <EncounterAudit sessionId={session.sessionId} detail={enc} />

                <EncounterDocuments
                  sessionId={session.sessionId}
                  detail={enc}
                  targetEncounters={listState.status === 'ready' ? listState.data : []}
                  onDetailChange={(updated) => {
                    setDetailState({ status: 'ready', data: updated })
                    setDetailCache((current) => new Map(current).set(updated.id, updated))
                  }}
                />

                {enc.diagnosisCodes.length > 0 && (
                  <div className="cl-card">
                    <div className="cl-card-header">
                      <h2 className="cl-card-title">Diagnosis codes</h2>
                    </div>
                    <ul className="cl-clinical-list">
                      {enc.diagnosisCodes.map((dx) => (
                        <li key={dx.code} className="cl-clinical-row">
                          <div>
                            <span className="cl-dx-code">{dx.code}</span>
                            <span>{dx.description ?? ''}</span>
                            <p className="cl-empty-text" style={{ margin: '4px 0 0' }}>
                              {dx.sources.join(' · ') || 'Encounter diagnosis'}
                              {dx.billingLineCount > 0 && ` · ${dx.billingLineCount} billing link${dx.billingLineCount === 1 ? '' : 's'}`}
                              {dx.procedureOrderCount > 0 && ` · ${dx.procedureOrderCount} procedure link${dx.procedureOrderCount === 1 ? '' : 's'}`}
                            </p>
                            {dx.supportingBillingCodes.length > 0 && <p className="cl-empty-text" style={{ margin: '2px 0 0' }}>Billing support: {dx.supportingBillingCodes.join(', ')}</p>}
                          </div>
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
              </>
            )
          })()}
        </section>
      </div>
    </div>
  )
}
