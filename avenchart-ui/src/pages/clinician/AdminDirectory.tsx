import { useEffect, useState, type FormEvent } from 'react'
import { useOutletContext } from 'react-router-dom'
import { Pencil, Plus, Trash2, X } from 'lucide-react'
import {
  createAdministrationFacility,
  deleteAdministrationFacility,
  getAdministrationDirectory,
  updateAdministrationFacility,
  type AdministrationDirectoryResponse,
  type AdministrationFacilityItem,
  type AdministrationFacilityMutationInput,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

type AsyncState<T> =
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; message: string }

type FacilityForm = {
  code: string
  name: string
  phone: string
  street: string
  city: string
  state: string
  postalCode: string
  color: string
  active: boolean
}

function emptyFacilityForm(): FacilityForm {
  return { code: '', name: '', phone: '', street: '', city: '', state: '', postalCode: '', color: '#0f6e56', active: true }
}

function facilityToForm(facility: AdministrationFacilityItem): FacilityForm {
  return {
    code: facility.code, name: facility.name, phone: facility.phone ?? '', street: facility.street ?? '', city: facility.city ?? '',
    state: facility.state ?? '', postalCode: facility.postalCode ?? '', color: facility.color ?? '#0f6e56', active: facility.active,
  }
}

function normalizeFacilityForm(form: FacilityForm): AdministrationFacilityMutationInput {
  const optional = (value: string) => value.trim() || null
  return {
    code: form.code.trim(), name: form.name.trim(), phone: optional(form.phone), street: optional(form.street), city: optional(form.city),
    state: optional(form.state), postalCode: optional(form.postalCode), color: optional(form.color), active: form.active,
  }
}

export default function AdminDirectory() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const [state, setState] = useState<AsyncState<AdministrationDirectoryResponse>>({ status: 'loading' })
  const [tab, setTab] = useState<'users' | 'facilities' | 'access'>('users')
  const [facilityForm, setFacilityForm] = useState<FacilityForm>(() => emptyFacilityForm())
  const [editingFacilityId, setEditingFacilityId] = useState<number | 'new' | null>(null)
  const [savingFacility, setSavingFacility] = useState(false)
  const [deletingFacilityId, setDeletingFacilityId] = useState<number | null>(null)

  useEffect(() => {
    getAdministrationDirectory(session.sessionId)
      .then((data) => setState({ status: 'ready', data }))
      .catch((err) => setState({ status: 'error', message: err instanceof Error ? err.message : 'Failed.' }))
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  function beginFacilityCreate() {
    setFacilityForm(emptyFacilityForm())
    setEditingFacilityId('new')
  }

  function beginFacilityEdit(facility: AdministrationFacilityItem) {
    setFacilityForm(facilityToForm(facility))
    setEditingFacilityId(facility.id)
  }

  function cancelFacilityEdit() {
    setEditingFacilityId(null)
    setFacilityForm(emptyFacilityForm())
  }

  async function saveFacility(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (editingFacilityId === null || savingFacility) return
    setSavingFacility(true)
    try {
      const body = normalizeFacilityForm(facilityForm)
      const mutation = editingFacilityId === 'new'
        ? await createAdministrationFacility(session.sessionId, body)
        : await updateAdministrationFacility(session.sessionId, editingFacilityId, body)
      setState({ status: 'ready', data: mutation.detail })
      showToast(editingFacilityId === 'new' ? 'Facility created.' : 'Facility updated.', 'success')
      cancelFacilityEdit()
    } catch {
      showToast('Could not save this facility. Check the required fields and try again.', 'error')
    } finally {
      setSavingFacility(false)
    }
  }

  async function removeFacility(facility: AdministrationFacilityItem) {
    if (deletingFacilityId !== null || !window.confirm(`Delete ${facility.name}? This cannot be undone.`)) return
    setDeletingFacilityId(facility.id)
    try {
      await deleteAdministrationFacility(session.sessionId, facility.id)
      const refreshed = await getAdministrationDirectory(session.sessionId)
      setState({ status: 'ready', data: refreshed })
      if (editingFacilityId === facility.id) cancelFacilityEdit()
      showToast('Facility deleted.', 'success')
    } catch {
      showToast('Could not delete this facility. It may still be in use.', 'error')
    } finally {
      setDeletingFacilityId(null)
    }
  }

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <h1 className="clinician-page-title">Administration</h1>
        {state.status === 'ready' && (
          <p className="clinician-page-subtitle">
            {state.data.counts.users} users · {state.data.counts.facilities} facilities · {state.data.counts.accessGroups} access groups
          </p>
        )}
      </div>

      {state.status === 'loading' && (
        <div className="cl-card">
          <div className="skeleton-list">{[0,1,2,3].map((i)=><div key={i} className="skeleton-row" style={{height:56}} />)}</div>
        </div>
      )}
      {state.status === 'error' && <div className="error-banner">{state.message}</div>}
      {state.status === 'ready' && (() => {
        const { data } = state
        return (
          <>
            <div className="cl-tab-bar">
              {([
                { id: 'users', label: `Users (${data.counts.users})` },
                { id: 'facilities', label: `Facilities (${data.counts.facilities})` },
                { id: 'access', label: `Access control (${data.counts.accessGroups})` },
              ] as const).map((t) => (
                <button
                  key={t.id}
                  className={`cl-tab-btn${tab === t.id ? ' cl-tab-btn-active' : ''}`}
                  type="button"
                  onClick={() => setTab(t.id)}
                >
                  {t.label}
                </button>
              ))}
            </div>

            {tab === 'users' && (
              <section className="cl-card" style={{ padding: 0, overflow: 'hidden' }}>
                <table className="cl-table">
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Username</th>
                      <th>Role</th>
                      <th>Facility</th>
                      <th>NPI</th>
                      <th>Active</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.users.map((u) => (
                      <tr key={u.id}>
                        <td>
                          {u.displayName}
                          {u.email && <p className="cl-table-sub">{u.email}</p>}
                        </td>
                        <td className="cl-td-muted">{u.username}</td>
                        <td>{u.role}</td>
                        <td className="cl-td-muted">{u.facilityName ?? '—'}</td>
                        <td className="cl-td-muted">{u.npi ?? '—'}</td>
                        <td>
                          <span className={`cl-badge ${u.active ? 'cl-badge-green' : 'cl-badge-muted'}`}>
                            {u.active ? 'Active' : 'Inactive'}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </section>
            )}

            {tab === 'facilities' && (
              <section className="cl-card" style={{ padding: 0, overflow: 'hidden' }}>
                <div className="cl-admin-facility-header">
                  <div>
                    <h2 className="cl-card-title">Facility directory</h2>
                    <p className="clinician-page-subtitle">Locations used for scheduling, care teams, and staff assignments.</p>
                  </div>
                  {editingFacilityId === null && (
                    <button className="cl-btn-primary" type="button" onClick={beginFacilityCreate}>
                      <Plus size={15} /> Add facility
                    </button>
                  )}
                </div>

                {editingFacilityId !== null && (
                  <form className="cl-admin-facility-form" onSubmit={saveFacility}>
                    <div className="cl-admin-form-heading">
                      <div>
                        <p className="cl-form-section-label">{editingFacilityId === 'new' ? 'New facility' : 'Edit facility'}</p>
                        <p className="cl-admin-form-copy">Required fields are marked. Changes apply to the shared facility directory.</p>
                      </div>
                      <button className="cl-icon-button" type="button" onClick={cancelFacilityEdit} aria-label="Cancel facility edit" title="Cancel">
                        <X size={16} />
                      </button>
                    </div>
                    <div className="cl-admin-form-grid">
                      <label className="cl-admin-field"><span>Facility name <em>*</em></span><input className="ne-input" value={facilityForm.name} onChange={(event) => setFacilityForm((form) => ({ ...form, name: event.target.value }))} required /></label>
                      <label className="cl-admin-field"><span>Code <em>*</em></span><input className="ne-input" value={facilityForm.code} onChange={(event) => setFacilityForm((form) => ({ ...form, code: event.target.value }))} required /></label>
                      <label className="cl-admin-field"><span>Phone</span><input className="ne-input" type="tel" value={facilityForm.phone} onChange={(event) => setFacilityForm((form) => ({ ...form, phone: event.target.value }))} /></label>
                      <label className="cl-admin-field"><span>Street</span><input className="ne-input" value={facilityForm.street} onChange={(event) => setFacilityForm((form) => ({ ...form, street: event.target.value }))} /></label>
                      <label className="cl-admin-field"><span>City</span><input className="ne-input" value={facilityForm.city} onChange={(event) => setFacilityForm((form) => ({ ...form, city: event.target.value }))} /></label>
                      <label className="cl-admin-field cl-admin-field-state"><span>State</span><input className="ne-input" value={facilityForm.state} onChange={(event) => setFacilityForm((form) => ({ ...form, state: event.target.value }))} /></label>
                      <label className="cl-admin-field cl-admin-field-postal"><span>Postal code</span><input className="ne-input" value={facilityForm.postalCode} onChange={(event) => setFacilityForm((form) => ({ ...form, postalCode: event.target.value }))} /></label>
                      <label className="cl-admin-field cl-admin-field-color"><span>Directory color</span><input className="cl-color-input" type="color" value={facilityForm.color} onChange={(event) => setFacilityForm((form) => ({ ...form, color: event.target.value }))} /></label>
                      <label className="cl-admin-active-toggle"><input type="checkbox" checked={facilityForm.active} onChange={(event) => setFacilityForm((form) => ({ ...form, active: event.target.checked }))} /><span>Facility is active</span></label>
                    </div>
                    <div className="cl-inline-form-actions">
                      <button className="cl-btn-primary" type="submit" disabled={savingFacility}>{savingFacility ? 'Saving...' : editingFacilityId === 'new' ? 'Create facility' : 'Save changes'}</button>
                      <button className="cl-btn-secondary" type="button" onClick={cancelFacilityEdit} disabled={savingFacility}>Cancel</button>
                    </div>
                  </form>
                )}
                <table className="cl-table">
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Code</th>
                      <th>Address</th>
                      <th>Phone</th>
                      <th>Active</th>
                      <th><span className="sr-only">Actions</span></th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.facilities.map((f) => (
                      <tr key={f.id}>
                        <td>
                          <span className="cl-facility-color" style={{ background: f.color ?? '#ccc' }} />
                          {f.name}
                        </td>
                        <td className="cl-td-muted">{f.code}</td>
                        <td className="cl-td-muted">
                          {[f.street, f.city, f.state, f.postalCode].filter(Boolean).join(', ') || '—'}
                        </td>
                        <td className="cl-td-muted">{f.phone ?? '—'}</td>
                        <td>
                          <span className={`cl-badge ${f.active ? 'cl-badge-green' : 'cl-badge-muted'}`}>
                            {f.active ? 'Active' : 'Inactive'}
                          </span>
                        </td>
                        <td className="cl-admin-row-actions">
                          <button className="cl-icon-button" type="button" onClick={() => beginFacilityEdit(f)} aria-label={`Edit ${f.name}`} title="Edit facility"><Pencil size={15} /></button>
                          <button className="cl-icon-button cl-icon-button-danger" type="button" onClick={() => removeFacility(f)} disabled={deletingFacilityId === f.id} aria-label={`Delete ${f.name}`} title="Delete facility"><Trash2 size={15} /></button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </section>
            )}

            {tab === 'access' && (
              <section className="cl-card">
                <div className="cl-card-header">
                  <h2 className="cl-card-title">Access groups</h2>
                  <p className="clinician-page-subtitle">
                    {data.counts.accessGroupPermissions} permissions · {data.counts.accessUserMemberships} user memberships
                  </p>
                </div>
                {data.accessControl.groups.length === 0 ? (
                  <p className="cl-empty-text">No access groups configured.</p>
                ) : (
                  <ul className="cl-clinical-list">
                    {data.accessControl.groups.map((g) => (
                      <li key={g.id} className="cl-clinical-row">
                        <div>
                          <p className="cl-clinical-title">{g.name}</p>
                          <p className="cl-clinical-meta">{g.value} · {g.permissionCount} permissions</p>
                        </div>
                      </li>
                    ))}
                  </ul>
                )}
              </section>
            )}
          </>
        )
      })()}
    </div>
  )
}
