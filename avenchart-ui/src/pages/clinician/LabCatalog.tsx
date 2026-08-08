// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useEffectEvent, useMemo, useState } from 'react'
import { Pencil, Plus, RefreshCw, Trash2, X } from 'lucide-react'
import { useOutletContext } from 'react-router-dom'
import {
  createProcedureOrderCatalogItem,
  deleteProcedureOrderCatalogItem,
  getProcedureLabProviders,
  getProcedureOrderCatalog,
  isRequestCancellation,
  updateProcedureOrderCatalogItem,
  type ProcedureLabProviderDirectoryResponse,
  type ProcedureOrderCatalogInput,
  type ProcedureOrderCatalogItem,
  type ProcedureOrderCatalogResponse,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

type CatalogState =
  | { status: 'loading' }
  | { status: 'ready'; catalog: ProcedureOrderCatalogResponse; providers: ProcedureLabProviderDirectoryResponse }
  | { status: 'error'; message: string }

type CatalogForm = {
  itemType: 'grp' | 'ord'
  parentId: string
  labId: string
  name: string
  code: string
  procedureTypeName: string
  description: string
  specimen: string
  standardCode: string
  sequence: string
  active: boolean
}

function blankForm(): CatalogForm {
  return { itemType: 'grp', parentId: '', labId: '', name: '', code: '', procedureTypeName: '', description: '', specimen: '', standardCode: '', sequence: '0', active: true }
}

function formFromItem(item: ProcedureOrderCatalogItem): CatalogForm {
  return { itemType: item.itemType, parentId: item.parentId?.toString() ?? '', labId: item.labId?.toString() ?? '', name: item.name, code: item.code ?? '', procedureTypeName: item.procedureTypeName ?? '', description: item.description ?? '', specimen: item.specimen ?? '', standardCode: item.standardCode ?? '', sequence: item.sequence.toString(), active: item.active }
}

function toInput(form: CatalogForm): ProcedureOrderCatalogInput {
  const numberOrNull = (value: string) => value && Number.isInteger(Number(value)) ? Number(value) : null
  const textOrNull = (value: string) => value.trim() || null
  return { itemType: form.itemType, parentId: form.itemType === 'ord' ? numberOrNull(form.parentId) : null, labId: form.itemType === 'ord' ? numberOrNull(form.labId) : null, name: form.name.trim(), code: form.itemType === 'ord' ? textOrNull(form.code) : null, procedureTypeName: textOrNull(form.procedureTypeName), description: textOrNull(form.description), specimen: textOrNull(form.specimen), standardCode: textOrNull(form.standardCode), sequence: Number.isFinite(Number(form.sequence)) ? Number(form.sequence) : 0, active: form.active }
}

export default function LabCatalog() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const [state, setState] = useState<CatalogState>({ status: 'loading' })
  const [form, setForm] = useState<CatalogForm>(blankForm)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [saving, setSaving] = useState(false)
  const [deletingId, setDeletingId] = useState<number | null>(null)
  const [query, setQuery] = useState('')

  function load(signal?: AbortSignal) {
    setState({ status: 'loading' })
    Promise.all([getProcedureOrderCatalog(session.sessionId, signal), getProcedureLabProviders(session.sessionId, false, signal)])
      .then(([catalog, providers]) => setState({ status: 'ready', catalog, providers }))
      .catch((error: unknown) => {
        if (isRequestCancellation(error)) return
        setState({ status: 'error', message: error instanceof Error ? error.message : 'Could not load the local order catalog.' })
      })
  }

  const loadForSession = useEffectEvent(load)
  useEffect(() => {
    const controller = new AbortController()
    loadForSession(controller.signal)
    return () => controller.abort()
  }, [session.sessionId])

  const groups = useMemo(() => state.status === 'ready' ? state.catalog.items.filter((item) => item.itemType === 'grp') : [], [state])
  const visibleItems = useMemo(() => {
    if (state.status !== 'ready') return []
    const normalized = query.trim().toLowerCase()
    return normalized ? state.catalog.items.filter((item) => [item.name, item.code, item.labName, item.procedureTypeName, item.standardCode].some((value) => value?.toLowerCase().includes(normalized))) : state.catalog.items
  }, [query, state])

  function resetForm() {
    setForm(blankForm())
    setEditingId(null)
  }

  async function save() {
    const input = toInput(form)
    if (!input.name || (input.itemType === 'ord' && (!input.parentId || !input.labId || !input.code))) {
      showToast(input.itemType === 'ord' ? 'Orders require a group, local lab provider, name, and code.' : 'A group name is required.', 'error')
      return
    }
    setSaving(true)
    try {
      const catalog = editingId === null
        ? await createProcedureOrderCatalogItem(session.sessionId, input)
        : await updateProcedureOrderCatalogItem(session.sessionId, editingId, input)
      if (state.status === 'ready') setState({ ...state, catalog })
      resetForm()
      showToast(editingId === null ? 'Local order catalog item saved.' : 'Local order catalog item updated.', 'success')
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Could not save the local order catalog item.', 'error')
    } finally {
      setSaving(false)
    }
  }

  async function remove(item: ProcedureOrderCatalogItem) {
    if (!window.confirm(`Remove ${item.name} from the local order catalog? Groups with children cannot be removed.`)) return
    setDeletingId(item.id)
    try {
      await deleteProcedureOrderCatalogItem(session.sessionId, item.id)
      showToast('Local order catalog item removed.', 'success')
      load()
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Could not remove the local order catalog item.', 'error')
    } finally {
      setDeletingId(null)
    }
  }

  return <div className="clinician-page">
    <div className="clinician-page-header"><div><h1 className="clinician-page-title">Lab order catalog</h1><p className="clinician-page-subtitle">Manage the local procedure groups and order definitions used by the current lab workflow. Vendor compendium imports and partner publishing remain outside this workspace.</p></div><button className="cl-btn-secondary" type="button" disabled={state.status === 'loading'} onClick={() => load()}><RefreshCw size={15} aria-hidden="true" /> Refresh</button></div>
    {state.status === 'loading' && <section className="cl-card" aria-live="polite">Loading local order catalog…</section>}
    {state.status === 'error' && <section className="cl-card"><div className="error-banner" role="alert">{state.message}</div><button className="cl-btn-secondary" type="button" onClick={() => load()}>Retry</button></section>}
    {state.status === 'ready' && <>
      <section className="cl-card"><div className="lab-result-summary"><span>{state.catalog.totalItems} items</span><span>{state.catalog.groupCount} groups</span><span>{state.catalog.orderCount} orders</span><span>{state.catalog.labProviderCount} local labs</span></div><p className="cl-table-sub">Dataset {state.catalog.datasetId} · {state.catalog.datasetVersion}. A saved catalog is local evidence, not an external laboratory directory or transmission contract.</p></section>
      <section className="cl-card"><h2 className="cl-card-title">{editingId === null ? 'Add catalog item' : `Edit catalog item ${editingId}`}</h2><div className="cl-admin-form-grid">
        <label className="cl-admin-field"><span>Item type</span><select className="ne-input" value={form.itemType} onChange={(event) => setForm((current) => ({ ...current, itemType: event.target.value as CatalogForm['itemType'], parentId: '', labId: '', code: '' }))}><option value="grp">Group</option><option value="ord">Order</option></select></label>
        <label className="cl-admin-field"><span>Name</span><input className="ne-input" value={form.name} maxLength={255} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} /></label>
        {form.itemType === 'ord' && <><label className="cl-admin-field"><span>Parent group</span><select className="ne-input" value={form.parentId} onChange={(event) => setForm((current) => ({ ...current, parentId: event.target.value }))}><option value="">Select group</option>{groups.map((group) => <option key={group.id} value={group.id}>{group.name}</option>)}</select></label><label className="cl-admin-field"><span>Local lab provider</span><select className="ne-input" value={form.labId} onChange={(event) => setForm((current) => ({ ...current, labId: event.target.value }))}><option value="">Select provider</option>{state.providers.providers.map((provider) => <option key={provider.id} value={provider.id}>{provider.name}</option>)}</select></label><label className="cl-admin-field"><span>Order code</span><input className="ne-input" value={form.code} maxLength={255} onChange={(event) => setForm((current) => ({ ...current, code: event.target.value }))} /></label></>}
        <label className="cl-admin-field"><span>Procedure type</span><input className="ne-input" value={form.procedureTypeName} maxLength={255} placeholder={form.itemType === 'ord' ? 'laboratory' : 'Optional'} onChange={(event) => setForm((current) => ({ ...current, procedureTypeName: event.target.value }))} /></label>
        <label className="cl-admin-field"><span>Specimen</span><input className="ne-input" value={form.specimen} maxLength={255} onChange={(event) => setForm((current) => ({ ...current, specimen: event.target.value }))} /></label>
        <label className="cl-admin-field"><span>Standard code</span><input className="ne-input" value={form.standardCode} maxLength={255} onChange={(event) => setForm((current) => ({ ...current, standardCode: event.target.value }))} /></label>
        <label className="cl-admin-field"><span>Sequence</span><input className="ne-input" type="number" value={form.sequence} onChange={(event) => setForm((current) => ({ ...current, sequence: event.target.value }))} /></label>
        <label className="cl-admin-field"><span>Description</span><input className="ne-input" value={form.description} maxLength={1000} onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))} /></label>
        <label className="cl-checkbox-label"><input type="checkbox" checked={form.active} onChange={(event) => setForm((current) => ({ ...current, active: event.target.checked }))} /> Active</label>
      </div><div className="ne-actions"><button className="cl-btn-primary" type="button" disabled={saving} onClick={() => void save()}><Plus size={15} aria-hidden="true" />{saving ? 'Saving…' : editingId === null ? 'Save item' : 'Save changes'}</button>{editingId !== null && <button className="cl-btn-secondary" type="button" onClick={resetForm}><X size={15} aria-hidden="true" /> Cancel edit</button>}</div></section>
      <section className="cl-card"><label className="cl-admin-field"><span>Search local catalog</span><input className="ne-input" value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Name, code, lab, or standard code" /></label></section>
      <section className="cl-card" style={{ padding: 0 }}><div className="cl-table-wrap" role="region" aria-label="Local lab order catalog" tabIndex={0}><table className="cl-table"><thead><tr><th scope="col">Item</th><th scope="col">Group / lab</th><th scope="col">Clinical details</th><th scope="col">Activity</th><th scope="col"><span className="sr-only">Actions</span></th></tr></thead><tbody>{visibleItems.map((item) => <tr key={item.id}><td><strong>{item.name}</strong><p className="cl-table-sub">{item.itemType === 'grp' ? `Group · ${item.childCount} children` : item.code ?? 'Order code unavailable'}</p></td><td>{item.itemType === 'grp' ? 'Top-level group' : `${state.catalog.items.find((group) => group.id === item.parentId)?.name ?? 'Unknown group'} · ${item.labName ?? 'Local lab unavailable'}`}</td><td>{item.procedureTypeName ?? 'No type'}<p className="cl-table-sub">{item.specimen ?? 'No specimen'}{item.standardCode ? ` · ${item.standardCode}` : ''}</p></td><td><span className={`cl-badge ${item.active ? 'cl-badge-green' : 'cl-badge-amber'}`}>{item.active ? 'Active' : 'Inactive'}</span></td><td><div className="ne-actions"><button className="cl-icon-button" type="button" aria-label={`Edit ${item.name}`} onClick={() => { setEditingId(item.id); setForm(formFromItem(item)); window.scrollTo({ top: 0, behavior: 'smooth' }) }}><Pencil size={15} aria-hidden="true" /></button><button className="cl-icon-button cl-icon-button-danger" type="button" disabled={deletingId !== null || item.childCount > 0} aria-label={`Remove ${item.name}`} title={item.childCount > 0 ? 'Remove child items before removing this group.' : undefined} onClick={() => void remove(item)}><Trash2 size={15} aria-hidden="true" /></button></div></td></tr>)}{visibleItems.length === 0 && <tr><td colSpan={5} className="cl-empty-text">No local catalog items match the current search.</td></tr>}</tbody></table></div></section>
    </>}
  </div>
}
