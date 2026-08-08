// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useState } from 'react'
import { createModuleChangeRequest, getModuleCatalog, getModuleChangeRequest, getModuleChangeRequests, transitionModuleChangeRequest, type GovernanceStatus, type ModuleCatalogItem, type ModuleChangeRequest, type ModuleChangeRequestDetail } from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'

const actions: Record<GovernanceStatus, Array<'submit' | 'approve' | 'reject' | 'activate' | 'cancel'>> = { draft: ['submit', 'cancel'], submitted: ['approve', 'reject', 'cancel'], approved: ['activate', 'cancel'], rejected: [], activated: [], cancelled: [] }
const statuses: Array<GovernanceStatus | 'all' | 'open'> = ['open', 'all', 'draft', 'submitted', 'approved', 'rejected', 'activated', 'cancelled']

export default function ModuleGovernance({ sessionId }: { sessionId: string }) {
  const [modules, setModules] = useState<ModuleCatalogItem[]>([])
  const [requests, setRequests] = useState<ModuleChangeRequest[]>([])
  const [moduleKey, setModuleKey] = useState('')
  const [status, setStatus] = useState<'enabled' | 'disabled'>('disabled')
  const [reason, setReason] = useState('')
  const [requestStatus, setRequestStatus] = useState<GovernanceStatus | 'all' | 'open'>('open')
  const [selectedRequest, setSelectedRequest] = useState<ModuleChangeRequestDetail | null>(null)
  const [busy, setBusy] = useState(false)
  const refresh = async (filter = requestStatus) => {
    const [catalog, queue] = await Promise.all([getModuleCatalog(sessionId), getModuleChangeRequests(sessionId, filter)])
    setModules(catalog.modules); setRequests(queue.requests)
    const local = catalog.modules.find((item) => item.canChangeStatus)
    setModuleKey((current) => current || local?.key || '')
    setStatus((current) => current || (local?.status === 'enabled' ? 'disabled' : 'enabled'))
  }
  // The session ID is the authoritative initial-load boundary.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { void refresh().catch(() => showToast('Could not load module governance.', 'error')) }, [sessionId])
  const selected = modules.find((item) => item.key === moduleKey)
  const propose = async () => { if (!moduleKey || !reason.trim()) return; setBusy(true); try { await createModuleChangeRequest(sessionId, { moduleKey, status, reason: reason.trim() }); setReason(''); await refresh(); showToast('Module proposal created.', 'success') } catch { showToast('Could not create the module proposal. Only locally governed modules can change.', 'error') } finally { setBusy(false) } }
  const transition = async (request: ModuleChangeRequest, action: 'submit' | 'approve' | 'reject' | 'activate' | 'cancel') => { setBusy(true); try { await transitionModuleChangeRequest(sessionId, request.requestId, action, { expectedVersion: request.version, note: action === 'reject' || action === 'cancel' ? 'Governance decision' : null }); await refresh(); showToast(`Module proposal ${action}.`, 'success') } catch { showToast('The module proposal changed. Refresh and try again.', 'error') } finally { setBusy(false) } }
  const openRequest = async (requestId: string) => { setBusy(true); try { setSelectedRequest(await getModuleChangeRequest(sessionId, requestId)) } catch { showToast('Could not load immutable module-request evidence.', 'error') } finally { setBusy(false) } }
  return <section className="cl-card"><h2 className="cl-card-title">Module governance</h2><p className="clinician-page-subtitle">Only locally governed modules can change here. Partner-gated and owner-decision modules remain catalog facts until their accountable owner authorizes a separate change.</p><div className="cl-access-grid"><section className="cl-access-panel"><h3 className="cl-access-title">Propose module state</h3><label className="cl-admin-field"><span>Local module</span><select className="ne-input" value={moduleKey} onChange={(event) => setModuleKey(event.target.value)}>{modules.filter((item) => item.canChangeStatus).map((item) => <option key={item.key} value={item.key}>{item.displayName} ({item.status})</option>)}</select></label>{selected && <p className="cl-admin-form-copy">Current state: <strong>{selected.status}</strong>. {selected.description}</p>}<label className="cl-admin-field"><span>Proposed state</span><select className="ne-input" value={status} onChange={(event) => setStatus(event.target.value as 'enabled' | 'disabled')}><option value="enabled">Enabled</option><option value="disabled">Disabled</option></select></label><label className="cl-admin-field"><span>Why this module changes</span><textarea className="ne-input" value={reason} onChange={(event) => setReason(event.target.value)} required /></label><button className="cl-btn-primary" type="button" disabled={busy || !moduleKey || !reason.trim() || selected?.status === status} onClick={() => void propose()}>Create proposal</button></section><section className="cl-access-panel"><h3 className="cl-access-title">Request history</h3><label className="cl-admin-field"><span>Status</span><select className="ne-input" value={requestStatus} onChange={(event) => setRequestStatus(event.target.value as GovernanceStatus | 'all' | 'open')}>{statuses.map((item) => <option key={item} value={item}>{item}</option>)}</select></label><button className="cl-btn-secondary" type="button" disabled={busy} onClick={() => void refresh(requestStatus)}>Apply filter</button>{requests.map((request) => <div className="cl-access-row" key={request.requestId}><div><p><strong>{request.moduleKey}</strong>: {request.baselineStatus} → {request.proposedStatus}</p><span>{request.status} · v{request.version} · {request.reason}</span></div><div className="cl-access-actions"><button className="cl-btn-secondary" type="button" disabled={busy} onClick={() => void openRequest(request.requestId)}>Evidence</button>{actions[request.status].map((action) => <button key={action} className="cl-btn-secondary" type="button" disabled={busy} onClick={() => void transition(request, action)}>{action}</button>)}</div></div>)}{requests.length === 0 && <p className="cl-empty-text">No module proposals match this filter.</p>}</section></div>{selectedRequest && <section className="cl-access-panel" style={{ marginTop: 12 }}><h3 className="cl-access-title">Immutable request evidence</h3><p><strong>{selectedRequest.request.moduleKey}</strong> · {selectedRequest.request.status} · version {selectedRequest.request.version}</p><p className="cl-empty-text">Current module status: {selectedRequest.module.status}.</p><ul className="report-event-list">{selectedRequest.events.map((event) => <li key={event.eventId}><div><strong>{event.action}</strong><span>{event.username} · {new Date(event.occurredAt).toLocaleString()}</span></div>{event.note && <p>{event.note}</p>}</li>)}</ul></section>}</section>
}
