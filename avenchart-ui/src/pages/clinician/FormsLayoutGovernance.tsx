import { useEffect, useState } from 'react'
import {
  createFormLayoutChangeRequest,
  createFormOptionListChangeRequest,
  getFormLayout,
  getFormLayoutChangeRequest,
  getFormLayoutChangeRequests,
  getFormLayouts,
  getFormOptionList,
  getFormOptionListChangeRequest,
  getFormOptionListChangeRequests,
  getFormOptionLists,
  transitionFormLayoutChangeRequest,
  transitionFormOptionListChangeRequest,
  type FormChangeRequestStatus,
  type FormLayoutChangeRequest,
  type FormLayoutChangeRequestDetail,
  type FormLayoutDefinition,
  type FormOptionListChangeRequest,
  type FormOptionListChangeRequestDetail,
  type FormOptionListDefinition,
  type GovernanceStatus,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'

type Props = { sessionId: string }
type LayoutDraft = { key: string; title: string; mapping: string; sequence: number; active: boolean; groups: string; fields: string; reason: string }
type ListDraft = { key: string; title: string; active: boolean; options: string; reason: string }
type FormLayoutDefinitionGroup = FormLayoutDefinition['groups'][number]
type FormLayoutDefinitionField = FormLayoutDefinition['fields'][number]
type FormOptionListDefinitionOption = FormOptionListDefinition['options'][number]
type RequestDetail =
  | { kind: 'layout'; detail: FormLayoutChangeRequestDetail }
  | { kind: 'option-list'; detail: FormOptionListChangeRequestDetail }

const actions: Record<GovernanceStatus, Array<'submit' | 'approve' | 'reject' | 'activate' | 'cancel'>> = {
  draft: ['submit', 'cancel'], submitted: ['approve', 'reject', 'cancel'], approved: ['activate', 'cancel'], rejected: [], activated: [], cancelled: [],
}
const statuses: FormChangeRequestStatus[] = ['open', 'all', 'draft', 'submitted', 'approved', 'rejected', 'activated', 'cancelled']
const emptyLayout = (): LayoutDraft => ({ key: '', title: '', mapping: '', sequence: 10, active: true, groups: '[]', fields: '[]', reason: '' })
const emptyList = (): ListDraft => ({ key: '', title: '', active: true, options: '[]', reason: '' })

export default function FormsLayoutGovernance({ sessionId }: Props) {
  const [layouts, setLayouts] = useState<Array<{ key: string; title: string }>>([])
  const [lists, setLists] = useState<Array<{ key: string; title: string }>>([])
  const [layoutDraft, setLayoutDraft] = useState<LayoutDraft>(emptyLayout)
  const [listDraft, setListDraft] = useState<ListDraft>(emptyList)
  const [layoutRequests, setLayoutRequests] = useState<FormLayoutChangeRequest[]>([])
  const [listRequests, setListRequests] = useState<FormOptionListChangeRequest[]>([])
  const [requestStatus, setRequestStatus] = useState<FormChangeRequestStatus>('open')
  const [requestOffset, setRequestOffset] = useState(0)
  const [requestTotals, setRequestTotals] = useState({ layouts: 0, lists: 0 })
  const [selectedRequest, setSelectedRequest] = useState<RequestDetail | null>(null)
  const [busy, setBusy] = useState(false)

  const refresh = async (offset = requestOffset, status = requestStatus) => {
    const [layoutCatalog, listCatalog, layoutQueue, listQueue] = await Promise.all([
      getFormLayouts(sessionId), getFormOptionLists(sessionId),
      getFormLayoutChangeRequests(sessionId, { status, offset, limit: 12 }),
      getFormOptionListChangeRequests(sessionId, { status, offset, limit: 12 }),
    ])
    setLayouts(layoutCatalog.layouts)
    setLists(listCatalog.lists)
    setLayoutRequests(layoutQueue.requests)
    setListRequests(listQueue.requests)
    setRequestTotals({ layouts: layoutQueue.total, lists: listQueue.total })
    setRequestOffset(offset)
  }

  // The session ID is the authoritative initial-load boundary.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { void refresh().catch(() => showToast('Could not load forms governance.', 'error')) }, [sessionId])

  const selectLayoutBaseline = async (key: string) => {
    if (!key) { setLayoutDraft(emptyLayout()); return }
    try {
      const detail = await getFormLayout(sessionId, key)
      setLayoutDraft({ key: detail.layout.key, title: detail.layout.title, mapping: detail.layout.mapping, sequence: detail.layout.sequence, active: detail.layout.active, groups: JSON.stringify(detail.groups, null, 2), fields: JSON.stringify(detail.fields, null, 2), reason: '' })
    } catch { showToast('Could not load the form layout baseline.', 'error') }
  }
  const selectListBaseline = async (key: string) => {
    if (!key) { setListDraft(emptyList()); return }
    try {
      const detail = await getFormOptionList(sessionId, key)
      setListDraft({ key: detail.list.key, title: detail.list.title, active: detail.list.active, options: JSON.stringify(detail.options, null, 2), reason: '' })
    } catch { showToast('Could not load the option-list baseline.', 'error') }
  }
  const proposeLayout = async () => {
    if (!layoutDraft.reason.trim() || !layoutDraft.key.trim()) return
    setBusy(true)
    try {
      const groups = JSON.parse(layoutDraft.groups) as FormLayoutDefinitionGroup[]
      const fields = JSON.parse(layoutDraft.fields) as FormLayoutDefinitionField[]
      await createFormLayoutChangeRequest(sessionId, { key: layoutDraft.key, title: layoutDraft.title, mapping: layoutDraft.mapping, sequence: Number(layoutDraft.sequence), active: layoutDraft.active, groups, fields, reason: layoutDraft.reason.trim() })
      setLayoutDraft(emptyLayout()); await refresh(); showToast('Form layout proposal created.', 'success')
    } catch { showToast('The layout definition is invalid. Check required fields and structured group/field JSON.', 'error') }
    finally { setBusy(false) }
  }
  const proposeList = async () => {
    if (!listDraft.reason.trim() || !listDraft.key.trim()) return
    setBusy(true)
    try {
      const options = JSON.parse(listDraft.options) as FormOptionListDefinitionOption[]
      await createFormOptionListChangeRequest(sessionId, { key: listDraft.key, title: listDraft.title, active: listDraft.active, options, reason: listDraft.reason.trim() })
      setListDraft(emptyList()); await refresh(); showToast('Option-list proposal created.', 'success')
    } catch { showToast('The option-list definition is invalid. Check required fields and structured option JSON.', 'error') }
    finally { setBusy(false) }
  }
  const moveLayout = async (request: FormLayoutChangeRequest, action: 'submit' | 'approve' | 'reject' | 'activate' | 'cancel') => {
    setBusy(true)
    try { await transitionFormLayoutChangeRequest(sessionId, request.requestId, action, { note: action === 'reject' || action === 'cancel' ? 'Governance decision' : null, expectedVersion: request.version }); await refresh() }
    catch { showToast('The layout proposal changed. Refresh and try again.', 'error') }
    finally { setBusy(false) }
  }
  const moveList = async (request: FormOptionListChangeRequest, action: 'submit' | 'approve' | 'reject' | 'activate' | 'cancel') => {
    setBusy(true)
    try { await transitionFormOptionListChangeRequest(sessionId, request.requestId, action, { note: action === 'reject' || action === 'cancel' ? 'Governance decision' : null, expectedVersion: request.version }); await refresh() }
    catch { showToast('The option-list proposal changed. Refresh and try again.', 'error') }
    finally { setBusy(false) }
  }
  const openRequest = async (kind: RequestDetail['kind'], requestId: string) => {
    setBusy(true)
    try {
      if (kind === 'layout') {
        setSelectedRequest({ kind, detail: await getFormLayoutChangeRequest(sessionId, requestId) })
      } else {
        setSelectedRequest({ kind, detail: await getFormOptionListChangeRequest(sessionId, requestId) })
      }
    } catch { showToast('Could not load immutable request evidence.', 'error') }
    finally { setBusy(false) }
  }
  const requests = [
    ...layoutRequests.map((request) => ({ kind: 'layout' as const, label: 'Layout', request })),
    ...listRequests.map((request) => ({ kind: 'option-list' as const, label: 'Option list', request })),
  ]
  const selected = selectedRequest?.detail
  const selectedKey = selectedRequest?.kind === 'layout'
    ? selectedRequest.detail.request.layoutKey
    : selectedRequest?.kind === 'option-list'
      ? selectedRequest.detail.request.listKey
      : null
  const selectedHasActiveBaseline = selectedRequest?.kind === 'layout'
    ? Boolean(selectedRequest.detail.activeLayout)
    : selectedRequest?.kind === 'option-list'
      ? Boolean(selectedRequest.detail.activeList)
      : false
  const hasNextPage = layoutRequests.length === 12 || listRequests.length === 12

  return <section className="cl-card">
    <h2 className="cl-card-title">Forms and layouts governance</h2>
    <p className="clinician-page-subtitle">Active definitions remain read-only. Copy a baseline or start new, edit the complete definition, record why it changes, then advance the proposal through review.</p>
    <div className="cl-access-grid">
      <section className="cl-access-panel"><h3 className="cl-access-title">Form layout proposal</h3>
        <label className="cl-admin-field"><span>Copy active layout</span><select className="ne-input" value={layouts.some((item) => item.key === layoutDraft.key) ? layoutDraft.key : ''} onChange={(event) => void selectLayoutBaseline(event.target.value)}><option value="">New form layout</option>{layouts.map((item) => <option key={item.key} value={item.key}>{item.title} ({item.key})</option>)}</select></label>
        <label className="cl-admin-field"><span>Layout key</span><input className="ne-input" value={layoutDraft.key} onChange={(event) => setLayoutDraft({ ...layoutDraft, key: event.target.value })} required /></label>
        <label className="cl-admin-field"><span>Title</span><input className="ne-input" value={layoutDraft.title} onChange={(event) => setLayoutDraft({ ...layoutDraft, title: event.target.value })} required /></label>
        <label className="cl-admin-field"><span>Mapping</span><input className="ne-input" value={layoutDraft.mapping} onChange={(event) => setLayoutDraft({ ...layoutDraft, mapping: event.target.value })} required /></label>
        <label className="cl-admin-field"><span>Sequence</span><input className="ne-input" type="number" min="0" value={layoutDraft.sequence} onChange={(event) => setLayoutDraft({ ...layoutDraft, sequence: Number(event.target.value) })} required /></label>
        <label className="cl-admin-active-toggle"><input type="checkbox" checked={layoutDraft.active} onChange={(event) => setLayoutDraft({ ...layoutDraft, active: event.target.checked })} /><span>Layout is active</span></label>
        <label className="cl-admin-field"><span>Groups JSON</span><textarea className="ne-input" rows={8} value={layoutDraft.groups} onChange={(event) => setLayoutDraft({ ...layoutDraft, groups: event.target.value })} /></label>
        <label className="cl-admin-field"><span>Fields JSON</span><textarea className="ne-input" rows={12} value={layoutDraft.fields} onChange={(event) => setLayoutDraft({ ...layoutDraft, fields: event.target.value })} /></label>
        <label className="cl-admin-field"><span>Why this definition changes</span><textarea className="ne-input" value={layoutDraft.reason} onChange={(event) => setLayoutDraft({ ...layoutDraft, reason: event.target.value })} required /></label>
        <button className="cl-btn-primary" type="button" disabled={busy || !layoutDraft.reason.trim() || !layoutDraft.key.trim()} onClick={() => void proposeLayout()}>Propose layout</button>
      </section>
      <section className="cl-access-panel"><h3 className="cl-access-title">Option-list proposal</h3>
        <label className="cl-admin-field"><span>Copy active option list</span><select className="ne-input" value={lists.some((item) => item.key === listDraft.key) ? listDraft.key : ''} onChange={(event) => void selectListBaseline(event.target.value)}><option value="">New option list</option>{lists.map((item) => <option key={item.key} value={item.key}>{item.title} ({item.key})</option>)}</select></label>
        <label className="cl-admin-field"><span>List key</span><input className="ne-input" value={listDraft.key} onChange={(event) => setListDraft({ ...listDraft, key: event.target.value })} required /></label>
        <label className="cl-admin-field"><span>Title</span><input className="ne-input" value={listDraft.title} onChange={(event) => setListDraft({ ...listDraft, title: event.target.value })} required /></label>
        <label className="cl-admin-active-toggle"><input type="checkbox" checked={listDraft.active} onChange={(event) => setListDraft({ ...listDraft, active: event.target.checked })} /><span>List is active</span></label>
        <label className="cl-admin-field"><span>Options JSON</span><textarea className="ne-input" rows={12} value={listDraft.options} onChange={(event) => setListDraft({ ...listDraft, options: event.target.value })} /></label>
        <label className="cl-admin-field"><span>Why this definition changes</span><textarea className="ne-input" value={listDraft.reason} onChange={(event) => setListDraft({ ...listDraft, reason: event.target.value })} required /></label>
        <button className="cl-btn-primary" type="button" disabled={busy || !listDraft.reason.trim() || !listDraft.key.trim()} onClick={() => void proposeList()}>Propose option list</button>
      </section>
    </div>
    <section className="cl-access-panel" style={{ marginTop: 12 }}><h3 className="cl-access-title">Request history</h3>
      <div className="practice-setting-actions"><label className="cl-admin-field"><span>Status</span><select className="ne-input" value={requestStatus} onChange={(event) => setRequestStatus(event.target.value as FormChangeRequestStatus)}>{statuses.map((status) => <option key={status} value={status}>{status}</option>)}</select></label><button className="cl-btn-secondary" type="button" disabled={busy} onClick={() => void refresh(0)}>Apply filter</button></div>
      <p className="cl-empty-text">{requestTotals.layouts} layout and {requestTotals.lists} option-list request(s) match this filter.</p>
      {requests.map(({ kind, label, request }) => <div className="cl-access-row" key={request.requestId}><div><p>{label}: {request.proposedDefinition.title}</p><span>{request.status} · v{request.version} · {request.reason}</span></div><div className="cl-access-actions"><button className="cl-btn-secondary" type="button" disabled={busy} onClick={() => void openRequest(kind, request.requestId)}>Evidence</button>{actions[request.status].map((action) => <button key={action} className="cl-btn-secondary" type="button" disabled={busy} onClick={() => void (kind === 'layout' ? moveLayout(request as FormLayoutChangeRequest, action) : moveList(request as FormOptionListChangeRequest, action))}>{action}</button>)}</div></div>)}
      {requests.length === 0 && <p className="cl-empty-text">No form or option-list requests match this filter.</p>}
      <div className="practice-setting-actions"><button className="cl-btn-secondary" type="button" disabled={busy || requestOffset === 0} onClick={() => void refresh(Math.max(0, requestOffset - 12))}>Previous</button><button className="cl-btn-secondary" type="button" disabled={busy || !hasNextPage} onClick={() => void refresh(requestOffset + 12)}>Next</button></div>
    </section>
    {selected && <section className="cl-access-panel" style={{ marginTop: 12 }}><h3 className="cl-access-title">Immutable request evidence</h3><p><strong>{selectedKey}</strong> · {selected.request.status} · version {selected.request.version}</p><p className="cl-empty-text">Created by {selected.request.createdBy} on {new Date(selected.request.createdAt).toLocaleString()}. Active baseline: {selectedHasActiveBaseline ? 'available' : 'none'}.</p><ul className="report-event-list">{selected.events.map((event) => <li key={event.eventId}><div><strong>{event.action}</strong><span>{event.username} · {new Date(event.occurredAt).toLocaleString()}</span></div>{event.note && <p>{event.note}</p>}</li>)}</ul></section>}
  </section>
}
