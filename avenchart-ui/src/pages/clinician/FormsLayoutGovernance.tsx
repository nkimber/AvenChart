import { useEffect, useState } from 'react'
import {
  createFormLayoutChangeRequest,
  createFormOptionListChangeRequest,
  getFormLayout,
  getFormLayoutChangeRequests,
  getFormLayouts,
  getFormOptionList,
  getFormOptionListChangeRequests,
  getFormOptionLists,
  transitionFormLayoutChangeRequest,
  transitionFormOptionListChangeRequest,
  type FormLayoutChangeRequest,
  type FormOptionListChangeRequest,
  type GovernanceStatus,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'

type Props = { sessionId: string }
const actions: Record<GovernanceStatus, Array<'submit' | 'approve' | 'reject' | 'activate' | 'cancel'>> = {
  draft: ['submit', 'cancel'], submitted: ['approve', 'reject', 'cancel'], approved: ['activate', 'cancel'], rejected: [], activated: [], cancelled: [],
}

export default function FormsLayoutGovernance({ sessionId }: Props) {
  const [layouts, setLayouts] = useState<Array<{ key: string; title: string }>>([])
  const [lists, setLists] = useState<Array<{ key: string; title: string }>>([])
  const [layoutKey, setLayoutKey] = useState('')
  const [listKey, setListKey] = useState('')
  const [reason, setReason] = useState('')
  const [layoutRequests, setLayoutRequests] = useState<FormLayoutChangeRequest[]>([])
  const [listRequests, setListRequests] = useState<FormOptionListChangeRequest[]>([])
  const [busy, setBusy] = useState(false)

  const refresh = async () => {
    const [layoutCatalog, listCatalog, layoutQueue, listQueue] = await Promise.all([
      getFormLayouts(sessionId), getFormOptionLists(sessionId), getFormLayoutChangeRequests(sessionId, 'open'), getFormOptionListChangeRequests(sessionId, 'open'),
    ])
    setLayouts(layoutCatalog.layouts); setLists(listCatalog.lists); setLayoutRequests(layoutQueue.requests); setListRequests(listQueue.requests)
    setLayoutKey((value) => value || layoutCatalog.layouts[0]?.key || '')
    setListKey((value) => value || listCatalog.lists[0]?.key || '')
  }
  // The session ID is the only input to the initial authoritative refresh.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { void refresh().catch(() => showToast('Could not load forms governance.', 'error')) }, [sessionId])
  const proposeLayout = async () => {
    if (!layoutKey || !reason.trim()) return
    setBusy(true)
    try { const detail = await getFormLayout(sessionId, layoutKey); await createFormLayoutChangeRequest(sessionId, { key: detail.layout.key, title: detail.layout.title, mapping: detail.layout.mapping, sequence: detail.layout.sequence, active: detail.layout.active, groups: detail.groups, fields: detail.fields, reason: reason.trim() }); setReason(''); await refresh(); showToast('Form layout proposal created.', 'success') } catch { showToast('Could not create the form layout proposal.', 'error') } finally { setBusy(false) }
  }
  const proposeList = async () => {
    if (!listKey || !reason.trim()) return
    setBusy(true)
    try { const detail = await getFormOptionList(sessionId, listKey); await createFormOptionListChangeRequest(sessionId, { key: detail.list.key, title: detail.list.title, active: detail.list.active, options: detail.options, reason: reason.trim() }); setReason(''); await refresh(); showToast('Option-list proposal created.', 'success') } catch { showToast('Could not create the option-list proposal.', 'error') } finally { setBusy(false) }
  }
  const moveLayout = async (request: FormLayoutChangeRequest, action: 'submit' | 'approve' | 'reject' | 'activate' | 'cancel') => { setBusy(true); try { await transitionFormLayoutChangeRequest(sessionId, request.requestId, action, { note: action === 'reject' || action === 'cancel' ? 'Governance decision' : null, expectedVersion: request.version }); await refresh() } catch { showToast('The layout proposal changed. Refresh and try again.', 'error') } finally { setBusy(false) } }
  const moveList = async (request: FormOptionListChangeRequest, action: 'submit' | 'approve' | 'reject' | 'activate' | 'cancel') => { setBusy(true); try { await transitionFormOptionListChangeRequest(sessionId, request.requestId, action, { note: action === 'reject' || action === 'cancel' ? 'Governance decision' : null, expectedVersion: request.version }); await refresh() } catch { showToast('The option-list proposal changed. Refresh and try again.', 'error') } finally { setBusy(false) } }
  return <section className="cl-card">
    <h2 className="cl-card-title">Forms and layouts governance</h2>
    <p className="clinician-page-subtitle">Active definitions stay read-only here. Propose a complete definition, then advance it through the recorded review lifecycle.</p>
    <div className="cl-access-grid">
      <section className="cl-access-panel"><h3 className="cl-access-title">Create a proposal</h3>
        <label className="cl-admin-field"><span>Form layout</span><select className="ne-input" value={layoutKey} onChange={(event) => setLayoutKey(event.target.value)}>{layouts.map((item) => <option key={item.key} value={item.key}>{item.title} ({item.key})</option>)}</select></label>
        <label className="cl-admin-field"><span>Option list</span><select className="ne-input" value={listKey} onChange={(event) => setListKey(event.target.value)}>{lists.map((item) => <option key={item.key} value={item.key}>{item.title} ({item.key})</option>)}</select></label>
        <label className="cl-admin-field"><span>Why this definition changes</span><textarea className="ne-input" value={reason} onChange={(event) => setReason(event.target.value)} required /></label>
        <div className="cl-access-actions"><button className="cl-btn-primary" type="button" disabled={busy || !reason.trim()} onClick={() => void proposeLayout()}>Propose layout</button><button className="cl-btn-secondary" type="button" disabled={busy || !reason.trim()} onClick={() => void proposeList()}>Propose option list</button></div>
      </section>
      <section className="cl-access-panel"><h3 className="cl-access-title">Open proposals</h3>
        {[...layoutRequests.map((request) => ({ kind: 'Layout', request })), ...listRequests.map((request) => ({ kind: 'Option list', request }))].map(({ kind, request }) => <div className="cl-access-row" key={request.requestId}><div><p>{kind}: {kind === 'Layout' ? (request as FormLayoutChangeRequest).proposedDefinition.title : (request as FormOptionListChangeRequest).proposedDefinition.title}</p><span>{request.status} · v{request.version} · {request.reason}</span></div><div className="cl-access-actions">{actions[request.status].map((action) => <button key={action} className="cl-btn-secondary" type="button" disabled={busy} onClick={() => void (kind === 'Layout' ? moveLayout(request as FormLayoutChangeRequest, action) : moveList(request as FormOptionListChangeRequest, action))}>{action}</button>)}</div></div>)}
        {layoutRequests.length + listRequests.length === 0 && <p className="cl-empty-text">No open form or option-list proposals.</p>}
      </section>
    </div>
  </section>
}
