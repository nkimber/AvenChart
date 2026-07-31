// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useState } from 'react'
import {
  createClinicalAlertRuleChangeRequest,
  getClinicalAlertRuleChangeRequest,
  getClinicalAlertRuleChangeRequests,
  getClinicalAlertRules,
  transitionClinicalAlertRuleChangeRequest,
  type ClinicalAlertRuleChangeRequest,
  type ClinicalAlertRuleChangeRequestDetail,
  type ClinicalAlertRuleItem,
  type FormChangeRequestStatus,
  type GovernanceStatus,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'

type Draft = ClinicalAlertRuleItem & { reason: string }
const actions: Record<GovernanceStatus, Array<'submit' | 'approve' | 'reject' | 'activate' | 'cancel'>> = {
  draft: ['submit', 'cancel'], submitted: ['approve', 'reject', 'cancel'], approved: ['activate', 'cancel'], rejected: [], activated: [], cancelled: [],
}
const statuses: FormChangeRequestStatus[] = ['open', 'all', 'draft', 'submitted', 'approved', 'rejected', 'activated', 'cancelled']
const emptyDraft = (): Draft => ({ key: '', title: '', triggerType: 'patient', targetType: 'banner', severity: 'info', message: '', sequence: 10, active: true, reason: '' })

export default function AlertRuleGovernance({ sessionId }: { sessionId: string }) {
  const [rules, setRules] = useState<ClinicalAlertRuleItem[]>([])
  const [requests, setRequests] = useState<ClinicalAlertRuleChangeRequest[]>([])
  const [draft, setDraft] = useState<Draft>(emptyDraft)
  const [requestStatus, setRequestStatus] = useState<FormChangeRequestStatus>('open')
  const [requestOffset, setRequestOffset] = useState(0)
  const [requestTotal, setRequestTotal] = useState(0)
  const [selectedRequest, setSelectedRequest] = useState<ClinicalAlertRuleChangeRequestDetail | null>(null)
  const [busy, setBusy] = useState(false)
  const refresh = async (offset = requestOffset, status = requestStatus) => {
    const [catalog, queue] = await Promise.all([
      getClinicalAlertRules(sessionId),
      getClinicalAlertRuleChangeRequests(sessionId, { status, offset, limit: 12 }),
    ])
    setRules(catalog.rules); setRequests(queue.requests); setRequestOffset(offset); setRequestTotal(queue.total)
  }
  // The session ID is the authoritative initial-load boundary.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { void refresh().catch(() => showToast('Could not load alert rules.', 'error')) }, [sessionId])
  const selectBaseline = (key: string) => { const rule = rules.find((item) => item.key === key); setDraft(rule ? { ...rule, reason: '' } : emptyDraft()) }
  const propose = async () => {
    if (!draft.key.trim() || !draft.reason.trim()) return
    setBusy(true)
    try { await createClinicalAlertRuleChangeRequest(sessionId, draft); setDraft(emptyDraft()); await refresh(); showToast('Alert-rule proposal created.', 'success') }
    catch { showToast('Could not create the alert-rule proposal. Check required fields and rule sequence.', 'error') }
    finally { setBusy(false) }
  }
  const transition = async (request: ClinicalAlertRuleChangeRequest, action: 'submit' | 'approve' | 'reject' | 'activate' | 'cancel') => {
    setBusy(true)
    try { await transitionClinicalAlertRuleChangeRequest(sessionId, request.requestId, action, { expectedVersion: request.version, note: action === 'reject' || action === 'cancel' ? 'Governance decision' : null }); await refresh(); showToast(`Alert-rule proposal ${action}.`, 'success') }
    catch { showToast('The proposal changed. Refresh and try again.', 'error') }
    finally { setBusy(false) }
  }
  const openRequest = async (requestId: string) => {
    setBusy(true)
    try { setSelectedRequest(await getClinicalAlertRuleChangeRequest(sessionId, requestId)) }
    catch { showToast('Could not load immutable request evidence.', 'error') }
    finally { setBusy(false) }
  }

  return <section className="cl-card">
    <h2 className="cl-card-title">Rules and alerts governance</h2>
    <p className="clinician-page-subtitle">Active clinical alerts remain read-only. Prepare a complete rule definition, record why it changes, then advance its proposal.</p>
    <div className="cl-access-grid"><section className="cl-access-panel"><h3 className="cl-access-title">Proposed rule</h3>
      <label className="cl-admin-field"><span>Copy active rule</span><select className="ne-input" value={rules.some((rule) => rule.key === draft.key) ? draft.key : ''} onChange={(event) => selectBaseline(event.target.value)}><option value="">New alert rule</option>{rules.map((rule) => <option key={rule.key} value={rule.key}>{rule.title} ({rule.key})</option>)}</select></label>
      <label className="cl-admin-field"><span>Rule key</span><input className="ne-input" value={draft.key} onChange={(event) => setDraft({ ...draft, key: event.target.value.toUpperCase() })} required /></label>
      <label className="cl-admin-field"><span>Title</span><input className="ne-input" value={draft.title} onChange={(event) => setDraft({ ...draft, title: event.target.value })} required /></label>
      <label className="cl-admin-field"><span>Trigger</span><select className="ne-input" value={draft.triggerType} onChange={(event) => setDraft({ ...draft, triggerType: event.target.value as ClinicalAlertRuleItem['triggerType'] })}><option value="patient">Patient</option><option value="encounter">Encounter</option><option value="appointment">Appointment</option></select></label>
      <label className="cl-admin-field"><span>Target</span><select className="ne-input" value={draft.targetType} onChange={(event) => setDraft({ ...draft, targetType: event.target.value as ClinicalAlertRuleItem['targetType'] })}><option value="banner">Banner</option><option value="reminder">Reminder</option></select></label>
      <label className="cl-admin-field"><span>Severity</span><select className="ne-input" value={draft.severity} onChange={(event) => setDraft({ ...draft, severity: event.target.value as ClinicalAlertRuleItem['severity'] })}><option value="info">Info</option><option value="warning">Warning</option><option value="critical">Critical</option></select></label>
      <label className="cl-admin-field"><span>Alert message</span><textarea className="ne-input" value={draft.message} onChange={(event) => setDraft({ ...draft, message: event.target.value })} required /></label>
      <label className="cl-admin-field"><span>Sequence</span><input className="ne-input" type="number" min="0" value={draft.sequence} onChange={(event) => setDraft({ ...draft, sequence: Number(event.target.value) })} required /></label>
      <label className="cl-admin-active-toggle"><input type="checkbox" checked={draft.active} onChange={(event) => setDraft({ ...draft, active: event.target.checked })} /><span>Rule is active</span></label>
      <label className="cl-admin-field"><span>Reason</span><textarea className="ne-input" value={draft.reason} onChange={(event) => setDraft({ ...draft, reason: event.target.value })} required /></label>
      <button className="cl-btn-primary" type="button" disabled={busy || !draft.key.trim() || !draft.reason.trim()} onClick={() => void propose()}>Create proposal</button>
    </section></div>
    <section className="cl-access-panel" style={{ marginTop: 12 }}><h3 className="cl-access-title">Request history</h3>
      <div className="practice-setting-actions"><label className="cl-admin-field"><span>Status</span><select className="ne-input" value={requestStatus} onChange={(event) => setRequestStatus(event.target.value as FormChangeRequestStatus)}>{statuses.map((status) => <option key={status} value={status}>{status}</option>)}</select></label><button className="cl-btn-secondary" type="button" disabled={busy} onClick={() => void refresh(0)}>Apply filter</button></div>
      <p className="cl-empty-text">{requestTotal} alert-rule request(s) match this filter.</p>
      {requests.map((request) => <div className="cl-access-row" key={request.requestId}><div><p><strong>{request.proposedDefinition.title}</strong> · {request.ruleKey}</p><span>{request.status} · v{request.version} · {request.reason}</span></div><div className="cl-access-actions"><button className="cl-btn-secondary" type="button" disabled={busy} onClick={() => void openRequest(request.requestId)}>Evidence</button>{actions[request.status].map((action) => <button key={action} className="cl-btn-secondary" type="button" disabled={busy} onClick={() => void transition(request, action)}>{action}</button>)}</div></div>)}
      {requests.length === 0 && <p className="cl-empty-text">No alert-rule requests match this filter.</p>}
      <div className="practice-setting-actions"><button className="cl-btn-secondary" type="button" disabled={busy || requestOffset === 0} onClick={() => void refresh(Math.max(0, requestOffset - 12))}>Previous</button><button className="cl-btn-secondary" type="button" disabled={busy || requests.length < 12} onClick={() => void refresh(requestOffset + 12)}>Next</button></div>
    </section>
    {selectedRequest && <section className="cl-access-panel" style={{ marginTop: 12 }}><h3 className="cl-access-title">Immutable request evidence</h3><p><strong>{selectedRequest.request.ruleKey}</strong> · {selectedRequest.request.status} · version {selectedRequest.request.version}</p><p className="cl-empty-text">Created by {selectedRequest.request.createdBy ?? 'unknown'}; active baseline: {selectedRequest.activeRule ? 'available' : 'none'}.</p><ul className="report-event-list">{selectedRequest.events.map((event) => <li key={event.eventId}><div><strong>{event.action}</strong><span>{event.username} · {new Date(event.occurredAt).toLocaleString()}</span></div>{event.note && <p>{event.note}</p>}</li>)}</ul></section>}
  </section>
}
