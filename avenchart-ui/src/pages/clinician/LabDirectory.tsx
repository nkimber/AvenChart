// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useEffectEvent, useState } from 'react'
import { Plus, RefreshCw, Trash2 } from 'lucide-react'
import {
  createProcedureLabProviderOrganization,
  deleteProcedureLabProviderOrganization,
  getProcedureLabProviderAddressBook,
  getProcedureLabProviders,
  isRequestCancellation,
  type ProcedureLabProviderAddressBookResponse,
  type ProcedureLabProviderDirectoryResponse,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import { useOutletContext } from 'react-router-dom'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

type DirectoryState =
  | { status: 'loading' }
  | { status: 'ready'; providers: ProcedureLabProviderDirectoryResponse; addressBook: ProcedureLabProviderAddressBookResponse }
  | { status: 'error'; message: string }

export default function LabDirectory() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const [state, setState] = useState<DirectoryState>({ status: 'loading' })
  const [organization, setOrganization] = useState('')
  const [type, setType] = useState('ord_lab')
  const [saving, setSaving] = useState(false)
  const [deletingId, setDeletingId] = useState<number | null>(null)

  function load(signal?: AbortSignal) {
    setState({ status: 'loading' })
    Promise.all([
      getProcedureLabProviders(session.sessionId, true, signal),
      getProcedureLabProviderAddressBook(session.sessionId, signal),
    ])
      .then(([providers, addressBook]) => setState({ status: 'ready', providers, addressBook }))
      .catch((error: unknown) => {
        if (isRequestCancellation(error)) return
        setState({ status: 'error', message: error instanceof Error ? error.message : 'Could not load the local lab directory.' })
      })
  }

  const loadForSession = useEffectEvent(load)
  useEffect(() => {
    const controller = new AbortController()
    loadForSession(controller.signal)
    return () => controller.abort()
  }, [session.sessionId])

  async function createOrganization() {
    if (!organization.trim()) {
      showToast('An ordering organization is required.', 'error')
      return
    }
    setSaving(true)
    try {
      await createProcedureLabProviderOrganization(session.sessionId, { organization: organization.trim(), type, active: true })
      setOrganization('')
      showToast('Local lab ordering organization saved.', 'success')
      load()
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Could not save the local ordering organization.', 'error')
    } finally {
      setSaving(false)
    }
  }

  async function deleteOrganization(id: number, name: string) {
    if (!window.confirm(`Remove ${name} from the local lab ordering address book?`)) return
    setDeletingId(id)
    try {
      await deleteProcedureLabProviderOrganization(session.sessionId, id)
      showToast('Local lab ordering organization removed.', 'success')
      load()
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Could not remove the local ordering organization.', 'error')
    } finally {
      setDeletingId(null)
    }
  }

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <div>
          <h1 className="clinician-page-title">Lab directory</h1>
          <p className="clinician-page-subtitle">Local lab providers and ordering organizations. Connection credentials and partner transmission are deliberately outside this workspace.</p>
        </div>
        <button className="cl-btn-secondary" type="button" onClick={() => load()} disabled={state.status === 'loading'}>
          <RefreshCw size={15} aria-hidden="true" /> Refresh
        </button>
      </div>

      {state.status === 'loading' && <section className="cl-card" aria-live="polite">Loading local lab directory…</section>}
      {state.status === 'error' && <section className="cl-card"><div className="error-banner" role="alert">{state.message}</div><button className="cl-btn-secondary" type="button" onClick={() => load()}>Retry</button></section>}
      {state.status === 'ready' && <>
        <section className="cl-card" aria-label="Local lab directory totals">
          <div className="lab-result-summary">
            <span>{state.providers.totalProviders} providers</span>
            <span>{state.providers.activeProviders} active</span>
            <span>{state.providers.inactiveProviders} inactive</span>
            <span>{state.addressBook.organizations.length} ordering organizations</span>
          </div>
          <p className="cl-table-sub">Dataset {state.providers.datasetId} · {state.providers.datasetVersion}. This is a local directory, not a connected lab network.</p>
        </section>

        <section className="cl-card">
          <h2 className="cl-card-title">Add ordering organization</h2>
          <div className="cl-inline-form">
            <label className="cl-admin-field"><span>Organization</span><input className="ne-input" value={organization} onChange={(event) => setOrganization(event.target.value)} maxLength={255} /></label>
            <label className="cl-admin-field"><span>Type</span><select className="ne-input" value={type} onChange={(event) => setType(event.target.value)}><option value="ord_lab">Ordering lab</option><option value="ord_provider">Ordering provider</option><option value="ord_facility">Ordering facility</option></select></label>
            <button className="cl-btn-primary" type="button" disabled={saving || !organization.trim()} onClick={() => void createOrganization()}><Plus size={15} aria-hidden="true" />{saving ? 'Saving…' : 'Save organization'}</button>
          </div>
        </section>

        <section className="cl-card" style={{ padding: 0 }}>
          <div className="cl-table-wrap" role="region" aria-label="Local lab providers" tabIndex={0}>
            <table className="cl-table"><thead><tr><th scope="col">Provider</th><th scope="col">Ordering organization</th><th scope="col">Protocol</th><th scope="col">Activity</th><th scope="col">Local workload</th></tr></thead><tbody>
              {state.providers.providers.map((provider) => <tr key={provider.id}><td><strong>{provider.name}</strong><p className="cl-table-sub">{provider.npi ? `NPI ${provider.npi}` : 'No NPI recorded'}</p></td><td>{provider.labDirectorName ?? 'Not assigned'}<p className="cl-table-sub">{provider.labDirectorType ?? '—'}</p></td><td>{provider.protocol ?? 'Local default'}<p className="cl-table-sub">{provider.usage ?? '—'} · {provider.direction ?? '—'}</p></td><td><span className={`cl-badge ${provider.active ? 'cl-badge-green' : 'cl-badge-amber'}`}>{provider.active ? 'Active' : 'Inactive'}</span></td><td>{provider.orderCount} orders · {provider.reportCount} reports<p className="cl-table-sub">{provider.futureOrderCount} future orders</p></td></tr>)}
              {state.providers.providers.length === 0 && <tr><td colSpan={5} className="cl-empty-text">No local lab providers are configured.</td></tr>}
            </tbody></table>
          </div>
        </section>

        <section className="cl-card" style={{ padding: 0 }}>
          <div className="cl-table-wrap" role="region" aria-label="Lab ordering address book" tabIndex={0}>
            <table className="cl-table"><thead><tr><th scope="col">Organization</th><th scope="col">Type</th><th scope="col">Activity</th><th scope="col"><span className="sr-only">Actions</span></th></tr></thead><tbody>
              {state.addressBook.organizations.map((item) => <tr key={item.id}><td>{item.organization}</td><td>{item.type}</td><td><span className={`cl-badge ${item.active ? 'cl-badge-green' : 'cl-badge-amber'}`}>{item.active ? 'Active' : 'Inactive'}</span></td><td><button className="cl-icon-button cl-icon-button-danger" type="button" disabled={deletingId !== null} onClick={() => void deleteOrganization(item.id, item.organization)} aria-label={`Remove ${item.organization}`}><Trash2 size={15} aria-hidden="true" /></button></td></tr>)}
              {state.addressBook.organizations.length === 0 && <tr><td colSpan={4} className="cl-empty-text">No ordering organizations are configured.</td></tr>}
            </tbody></table>
          </div>
        </section>
      </>}
    </div>
  )
}
