// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useState, type FormEvent } from 'react'
import { Pencil, Plus, X } from 'lucide-react'
import {
  createProcedureReport,
  createProcedureResult,
  updateProcedureResult,
  type ProcedureResultItem,
  type ProcedureResultsResponse,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'

type Props = {
  sessionId: string
  orders: ProcedureResultsResponse['orders']
  onChange: (detail: ProcedureResultsResponse) => void
}

const today = () => new Date().toISOString().slice(0, 10)

export default function LabReportAndResultCapture({ sessionId, orders, onChange }: Props) {
  const [report, setReport] = useState({ orderId: '', specimenId: '', dateCollected: today(), dateReport: today(), notes: '' })
  const [result, setResult] = useState({ reportId: '', resultCode: '', resultText: '', dateTime: today(), units: '', value: '', range: '', abnormal: '', status: 'final' })
  const [editing, setEditing] = useState<{ id: number; reportId: number; form: typeof result } | null>(null)
  const [busy, setBusy] = useState(false)

  async function submitReport(event: FormEvent) {
    event.preventDefault()
    const orderId = Number(report.orderId)
    const specimenId = Number(report.specimenId)
    if (!Number.isInteger(orderId) || !Number.isInteger(specimenId)) return
    setBusy(true)
    try {
      onChange(await createProcedureReport(sessionId, { orderId, specimenId, dateCollected: `${report.dateCollected}T12:00:00`, dateReport: `${report.dateReport}T12:00:00`, reportStatus: 'received', reviewStatus: 'received', notes: report.notes.trim() }))
      setReport({ orderId: '', specimenId: '', dateCollected: today(), dateReport: today(), notes: '' })
      showToast('Local lab report recorded.', 'success')
    } catch (error) { showToast(error instanceof Error ? error.message : 'Could not record the local lab report.', 'error') } finally { setBusy(false) }
  }

  async function submitResult(event: FormEvent, editingId?: number) {
    event.preventDefault()
    const form = editing?.form ?? result
    const reportId = editing?.reportId ?? Number(form.reportId)
    if (!Number.isInteger(reportId) || !form.resultCode.trim() || !form.resultText.trim() || !form.value.trim()) return
    setBusy(true)
    const base = { resultCode: form.resultCode.trim(), resultText: form.resultText.trim(), dateTime: `${form.dateTime}T12:00:00`, units: form.units.trim(), result: form.value.trim(), range: form.range.trim(), abnormal: form.abnormal.trim(), status: form.status }
    try {
      const detail = editingId
        ? await updateProcedureResult(sessionId, editingId, base)
        : await createProcedureResult(sessionId, { reportId, ...base, facility: '', comments: '' })
      onChange(detail)
      setResult({ reportId: '', resultCode: '', resultText: '', dateTime: today(), units: '', value: '', range: '', abnormal: '', status: 'final' })
      setEditing(null)
      showToast(editingId ? 'Local result correction recorded.' : 'Local atomic result recorded.', 'success')
    } catch (error) { showToast(error instanceof Error ? error.message : 'Could not save the local result.', 'error') } finally { setBusy(false) }
  }

  function startEdit(resultItem: ProcedureResultItem, reportId: number) {
    setEditing({ id: resultItem.id, reportId, form: { reportId: reportId.toString(), resultCode: resultItem.code ?? '', resultText: resultItem.text ?? '', dateTime: resultItem.resultDate.slice(0, 10) || today(), units: resultItem.units ?? '', value: resultItem.result ?? '', range: resultItem.range ?? '', abnormal: resultItem.abnormal ?? '', status: resultItem.resultStatus ?? 'final' } })
  }

  const updateResultForm = (update: Partial<typeof result>) => {
    if (editing) setEditing({ ...editing, form: { ...editing.form, ...update } })
    else setResult((current) => ({ ...current, ...update }))
  }
  const form = editing?.form ?? result
  const selectedOrder = orders.find((order) => order.id === Number(report.orderId))
  const receivedSpecimens = selectedOrder?.specimens.filter((specimen) => specimen.lifecycleStatus === 'received') ?? []
  return <>
    <section className="cl-card"><h2 className="cl-card-title">Record local lab report</h2><p className="cl-table-sub">Select a received specimen from this order. This stores local evidence only; it does not prove partner receipt, provenance, or delivery.</p><form className="cl-admin-form-grid" onSubmit={(event) => void submitReport(event)}><label className="cl-admin-field"><span>Order</span><select className="ne-input" value={report.orderId} required onChange={(event) => setReport((current) => ({ ...current, orderId: event.target.value, specimenId: '' }))}><option value="">Select order</option>{orders.map((order) => <option key={order.id} value={order.id}>{order.code ?? 'No code'} · {order.name ?? `Order ${order.id}`}</option>)}</select></label><label className="cl-admin-field"><span>Received specimen / accession</span><select className="ne-input" value={report.specimenId} required disabled={!report.orderId || receivedSpecimens.length === 0} onChange={(event) => setReport((current) => ({ ...current, specimenId: event.target.value }))}><option value="">{!report.orderId ? 'Select an order first' : receivedSpecimens.length === 0 ? 'No received specimens for this order' : 'Select received specimen'}</option>{receivedSpecimens.map((specimen) => <option key={specimen.id} value={specimen.id}>{specimen.accessionIdentifier ?? specimen.specimenIdentifier ?? `Specimen ${specimen.id}`}</option>)}</select></label><label className="cl-admin-field"><span>Collected</span><input className="ne-input" type="date" value={report.dateCollected} onChange={(event) => setReport((current) => ({ ...current, dateCollected: event.target.value }))} /></label><label className="cl-admin-field"><span>Reported</span><input className="ne-input" type="date" value={report.dateReport} onChange={(event) => setReport((current) => ({ ...current, dateReport: event.target.value }))} /></label><label className="cl-admin-field"><span>Notes</span><input className="ne-input" value={report.notes} onChange={(event) => setReport((current) => ({ ...current, notes: event.target.value }))} /></label><div className="ne-actions"><button className="cl-btn-primary" type="submit" disabled={busy || !report.orderId || !report.specimenId}><Plus size={15} aria-hidden="true" />{busy ? 'Saving…' : 'Save report'}</button></div></form></section>
    <section className="cl-card"><h2 className="cl-card-title">{editing ? 'Correct local result' : 'Record local atomic result'}</h2><p className="cl-table-sub">A correction preserves the server’s existing result version snapshot. It is not a laboratory interface correction acknowledgement.</p><form className="cl-admin-form-grid" onSubmit={(event) => void submitResult(event, editing?.id)}>{!editing && <label className="cl-admin-field"><span>Report</span><select className="ne-input" value={form.reportId} required onChange={(event) => updateResultForm({ reportId: event.target.value })}><option value="">Select report</option>{orders.flatMap((order) => order.reports.map((item) => <option key={item.id} value={item.id}>{order.name ?? order.code ?? `Order ${order.id}`} · {item.specimenNumber ?? `Report ${item.id}`}</option>))}</select></label>}<label className="cl-admin-field"><span>Result code</span><input className="ne-input" value={form.resultCode} required onChange={(event) => updateResultForm({ resultCode: event.target.value })} /></label><label className="cl-admin-field"><span>Result name</span><input className="ne-input" value={form.resultText} required onChange={(event) => updateResultForm({ resultText: event.target.value })} /></label><label className="cl-admin-field"><span>Value</span><input className="ne-input" value={form.value} required onChange={(event) => updateResultForm({ value: event.target.value })} /></label><label className="cl-admin-field"><span>Units</span><input className="ne-input" value={form.units} onChange={(event) => updateResultForm({ units: event.target.value })} /></label><label className="cl-admin-field"><span>Reference range</span><input className="ne-input" value={form.range} onChange={(event) => updateResultForm({ range: event.target.value })} /></label><label className="cl-admin-field"><span>Flag</span><select className="ne-input" value={form.abnormal} onChange={(event) => updateResultForm({ abnormal: event.target.value })}><option value="">Normal / not supplied</option><option value="H">High</option><option value="L">Low</option><option value="A">Abnormal</option><option value="C">Critical</option></select></label><label className="cl-admin-field"><span>Result date</span><input className="ne-input" type="date" value={form.dateTime} onChange={(event) => updateResultForm({ dateTime: event.target.value })} /></label><label className="cl-admin-field"><span>Status</span><select className="ne-input" value={form.status} onChange={(event) => updateResultForm({ status: event.target.value })}><option value="final">Final</option><option value="preliminary">Preliminary</option><option value="corrected">Corrected</option></select></label><div className="ne-actions"><button className="cl-btn-primary" type="submit" disabled={busy || !form.resultCode.trim() || !form.resultText.trim() || !form.value.trim()}><Plus size={15} aria-hidden="true" />{busy ? 'Saving…' : editing ? 'Save correction' : 'Save result'}</button>{editing && <button className="cl-btn-secondary" type="button" onClick={() => setEditing(null)}><X size={15} aria-hidden="true" />Cancel</button>}</div></form></section>
    <section className="cl-card" aria-label="Correct local results"><h2 className="cl-card-title">Correct a recorded result</h2>{orders.flatMap((order) => order.reports.flatMap((report) => report.results.map((item) => <div className="ne-actions" key={item.id}><span>{item.text ?? item.code ?? `Result ${item.id}`} · {item.result ?? '—'} {item.units ?? ''}</span><button className="cl-btn-secondary" type="button" onClick={() => startEdit(item, report.id)}><Pencil size={14} aria-hidden="true" />Correct</button></div>))).length === 0 && <p className="cl-empty-text">No local atomic results are available to correct.</p>}</section>
  </>
}
