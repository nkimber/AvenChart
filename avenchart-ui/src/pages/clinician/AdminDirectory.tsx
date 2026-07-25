import { useEffect, useState, type FormEvent } from 'react'
import { useOutletContext } from 'react-router-dom'
import { Check, Pencil, Plus, RotateCcw, Trash2, X } from 'lucide-react'
import {
  acceptAdministrationPortalProfileReview,
  createAdministrationFacility,
  createAdministrationUser,
  deleteAdministrationFacility,
  deleteAdministrationUser,
  getAdministrationDirectory,
  getPhiAccessAudit,
  grantAdministrationAccessMembership,
  grantAdministrationAccessPermission,
  revokeAdministrationAccessMembership,
  revokeAdministrationAccessPermission,
  revertAdministrationPortalProfileReview,
  updateAdministrationFacility,
  updateAdministrationUser,
  type AdministrationDirectoryResponse,
  type AdministrationFacilityItem,
  type AdministrationFacilityMutationInput,
  type AdministrationAccessGroupPermissionItem,
  type AdministrationAccessUserMembershipItem,
  type PhiAccessAuditResponse,
  type AdministrationPortalProfileReviewRequest,
  type AdministrationUserItem,
  type AdministrationUserMutationInput,
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

type UserForm = {
  username: string
  firstName: string
  lastName: string
  role: string
  calendar: boolean
  facilityId: string
  email: string
  npi: string
  active: boolean
}

function emptyUserForm(): UserForm {
  return { username: '', firstName: '', lastName: '', role: 'provider', calendar: true, facilityId: '', email: '', npi: '', active: true }
}

function userToForm(user: AdministrationUserItem): UserForm {
  return {
    username: user.username, firstName: user.firstName, lastName: user.lastName, role: user.role,
    calendar: user.calendar, facilityId: user.facilityId?.toString() ?? '', email: user.email ?? '', npi: user.npi ?? '', active: user.active,
  }
}

function normalizeUserForm(form: UserForm): AdministrationUserMutationInput {
  const optional = (value: string) => value.trim() || null
  const facilityId = Number(form.facilityId)
  return {
    username: form.username.trim(), firstName: form.firstName.trim(), lastName: form.lastName.trim(), role: form.role.trim(),
    calendar: form.calendar, facilityId: Number.isInteger(facilityId) && facilityId > 0 ? facilityId : null,
    email: optional(form.email), npi: optional(form.npi), active: form.active,
  }
}

type AccessMembershipForm = { userValue: string; groupValue: string }
type AccessPermissionForm = { groupValue: string; permissionKey: string; returnValue: 'addonly' | 'view' | 'write' | 'wsome' }

function emptyMembershipForm(): AccessMembershipForm { return { userValue: '', groupValue: '' } }
function emptyPermissionForm(): AccessPermissionForm { return { groupValue: '', permissionKey: '', returnValue: 'view' } }

export default function AdminDirectory() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const [state, setState] = useState<AsyncState<AdministrationDirectoryResponse>>({ status: 'loading' })
  const [tab, setTab] = useState<'users' | 'facilities' | 'access' | 'reviews' | 'audit'>('users')
  const [auditState, setAuditState] = useState<AsyncState<PhiAccessAuditResponse>>({ status: 'loading' })
  const [facilityForm, setFacilityForm] = useState<FacilityForm>(() => emptyFacilityForm())
  const [editingFacilityId, setEditingFacilityId] = useState<number | 'new' | null>(null)
  const [savingFacility, setSavingFacility] = useState(false)
  const [deletingFacilityId, setDeletingFacilityId] = useState<number | null>(null)
  const [userForm, setUserForm] = useState<UserForm>(() => emptyUserForm())
  const [editingUserId, setEditingUserId] = useState<number | 'new' | null>(null)
  const [savingUser, setSavingUser] = useState(false)
  const [deletingUserId, setDeletingUserId] = useState<number | null>(null)
  const [membershipForm, setMembershipForm] = useState<AccessMembershipForm>(() => emptyMembershipForm())
  const [permissionForm, setPermissionForm] = useState<AccessPermissionForm>(() => emptyPermissionForm())
  const [savingMembership, setSavingMembership] = useState(false)
  const [savingPermission, setSavingPermission] = useState(false)
  const [removingMembership, setRemovingMembership] = useState<string | null>(null)
  const [removingPermission, setRemovingPermission] = useState<string | null>(null)
  const [reviewActionId, setReviewActionId] = useState<string | null>(null)

  useEffect(() => {
    getAdministrationDirectory(session.sessionId)
      .then((data) => setState({ status: 'ready', data }))
      .catch((err) => setState({ status: 'error', message: err instanceof Error ? err.message : 'Failed.' }))
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    if (tab !== 'audit') return
    setAuditState({ status: 'loading' })
    getPhiAccessAudit(session.sessionId)
      .then((data) => setAuditState({ status: 'ready', data }))
      .catch((err) => setAuditState({ status: 'error', message: err instanceof Error ? err.message : 'Failed to load PHI access audit.' }))
  }, [session.sessionId, tab])

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

  function beginUserCreate() {
    setUserForm(emptyUserForm())
    setEditingUserId('new')
  }

  function beginUserEdit(user: AdministrationUserItem) {
    setUserForm(userToForm(user))
    setEditingUserId(user.id)
  }

  function cancelUserEdit() {
    setEditingUserId(null)
    setUserForm(emptyUserForm())
  }

  async function saveUser(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (editingUserId === null || savingUser) return
    setSavingUser(true)
    try {
      const body = normalizeUserForm(userForm)
      const mutation = editingUserId === 'new'
        ? await createAdministrationUser(session.sessionId, body)
        : await updateAdministrationUser(session.sessionId, editingUserId, body)
      setState({ status: 'ready', data: mutation.detail })
      showToast(editingUserId === 'new' ? 'Staff user created.' : 'Staff user updated.', 'success')
      cancelUserEdit()
    } catch {
      showToast('Could not save this staff user. Check the required fields and try again.', 'error')
    } finally {
      setSavingUser(false)
    }
  }

  async function removeUser(user: AdministrationUserItem) {
    if (user.id === session.staffId) {
      showToast('You cannot delete the signed-in staff user.', 'error')
      return
    }
    if (deletingUserId !== null || !window.confirm(`Delete ${user.displayName}? This also removes their access-group memberships.`)) return
    setDeletingUserId(user.id)
    try {
      await deleteAdministrationUser(session.sessionId, user.id)
      const refreshed = await getAdministrationDirectory(session.sessionId)
      setState({ status: 'ready', data: refreshed })
      if (editingUserId === user.id) cancelUserEdit()
      showToast('Staff user deleted.', 'success')
    } catch {
      showToast('Could not delete this staff user. It may still be in use.', 'error')
    } finally {
      setDeletingUserId(null)
    }
  }

  async function saveMembership(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (savingMembership || !membershipForm.userValue || !membershipForm.groupValue) return
    setSavingMembership(true)
    try {
      const mutation = await grantAdministrationAccessMembership(session.sessionId, membershipForm)
      setState({ status: 'ready', data: mutation.detail })
      setMembershipForm(emptyMembershipForm())
      showToast('Access-group membership saved.', 'success')
    } catch {
      showToast('Could not save this access-group membership.', 'error')
    } finally {
      setSavingMembership(false)
    }
  }

  async function removeMembership(membership: AdministrationAccessUserMembershipItem) {
    const key = `${membership.userValue}:${membership.groupValue}`
    if (membership.staffId === session.staffId) {
      showToast('You cannot remove the signed-in staff user from an access group.', 'error')
      return
    }
    if (removingMembership !== null || !window.confirm(`Remove ${membership.userName} from ${membership.groupName}?`)) return
    setRemovingMembership(key)
    try {
      const mutation = await revokeAdministrationAccessMembership(session.sessionId, membership.userValue, membership.groupValue)
      setState({ status: 'ready', data: mutation.detail })
      showToast('Access-group membership removed.', 'success')
    } catch {
      showToast('Could not remove this access-group membership.', 'error')
    } finally {
      setRemovingMembership(null)
    }
  }

  async function savePermission(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (savingPermission || !permissionForm.groupValue || !permissionForm.permissionKey) return
    const [sectionValue, permissionValue] = permissionForm.permissionKey.split(':', 2)
    if (!sectionValue || !permissionValue) return
    setSavingPermission(true)
    try {
      const mutation = await grantAdministrationAccessPermission(session.sessionId, {
        groupValue: permissionForm.groupValue,
        sectionValue,
        permissionValue,
        returnValue: permissionForm.returnValue,
      })
      setState({ status: 'ready', data: mutation.detail })
      setPermissionForm(emptyPermissionForm())
      showToast('Access permission saved.', 'success')
    } catch {
      showToast('Could not save this access permission.', 'error')
    } finally {
      setSavingPermission(false)
    }
  }

  async function removePermission(permission: AdministrationAccessGroupPermissionItem) {
    const key = `${permission.groupValue}:${permission.sectionValue}:${permission.permissionValue}`
    if (removingPermission !== null || !window.confirm(`Remove ${permission.permissionName} from ${permission.groupValue}?`)) return
    setRemovingPermission(key)
    try {
      const mutation = await revokeAdministrationAccessPermission(session.sessionId, permission.groupValue, permission.sectionValue, permission.permissionValue)
      setState({ status: 'ready', data: mutation.detail })
      showToast('Access permission removed.', 'success')
    } catch {
      showToast('Could not remove this access permission.', 'error')
    } finally {
      setRemovingPermission(null)
    }
  }

  async function resolveProfileReview(request: AdministrationPortalProfileReviewRequest, action: 'accept' | 'revert') {
    if (reviewActionId !== null) return
    const verb = action === 'accept' ? 'commit these changes to the chart' : 'revert these requested changes'
    if (!window.confirm(`Confirm: ${verb} for ${request.patientName}?`)) return
    setReviewActionId(request.id)
    try {
      const mutation = action === 'accept'
        ? await acceptAdministrationPortalProfileReview(session.sessionId, request.id)
        : await revertAdministrationPortalProfileReview(session.sessionId, request.id)
      setState({ status: 'ready', data: mutation.detail })
      showToast(action === 'accept' ? 'Portal profile changes committed.' : 'Portal profile changes reverted.', 'success')
    } catch {
      showToast('Could not resolve this profile review. It may already have changed.', 'error')
    } finally {
      setReviewActionId(null)
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
                { id: 'reviews', label: `Profile reviews (${data.counts.waitingProfileReviews})` },
                { id: 'audit', label: 'PHI access audit' },
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
                <div className="cl-admin-facility-header">
                  <div>
                    <h2 className="cl-card-title">Staff directory</h2>
                    <p className="clinician-page-subtitle">Local staff identity, facility assignment, calendar availability, and directory attributes.</p>
                  </div>
                  {editingUserId === null && (
                    <button className="cl-btn-primary" type="button" onClick={beginUserCreate}>
                      <Plus size={15} /> Add staff user
                    </button>
                  )}
                </div>

                {editingUserId !== null && (
                  <form className="cl-admin-facility-form" onSubmit={saveUser}>
                    <div className="cl-admin-form-heading">
                      <div>
                        <p className="cl-form-section-label">{editingUserId === 'new' ? 'New staff user' : 'Edit staff user'}</p>
                        <p className="cl-admin-form-copy">Password and production identity management remain outside this local directory workflow.</p>
                      </div>
                      <button className="cl-icon-button" type="button" onClick={cancelUserEdit} aria-label="Cancel staff user edit" title="Cancel"><X size={16} /></button>
                    </div>
                    <div className="cl-admin-form-grid">
                      <label className="cl-admin-field"><span>First name <em>*</em></span><input className="ne-input" value={userForm.firstName} onChange={(event) => setUserForm((form) => ({ ...form, firstName: event.target.value }))} required /></label>
                      <label className="cl-admin-field"><span>Last name <em>*</em></span><input className="ne-input" value={userForm.lastName} onChange={(event) => setUserForm((form) => ({ ...form, lastName: event.target.value }))} required /></label>
                      <label className="cl-admin-field"><span>Username <em>*</em></span><input className="ne-input" autoCapitalize="none" value={userForm.username} onChange={(event) => setUserForm((form) => ({ ...form, username: event.target.value }))} required /></label>
                      <label className="cl-admin-field"><span>Role <em>*</em></span><input className="ne-input" value={userForm.role} onChange={(event) => setUserForm((form) => ({ ...form, role: event.target.value }))} required /></label>
                      <label className="cl-admin-field"><span>Facility</span><select className="ne-input" value={userForm.facilityId} onChange={(event) => setUserForm((form) => ({ ...form, facilityId: event.target.value }))}><option value="">No facility assigned</option>{data.facilities.map((facility) => <option key={facility.id} value={facility.id}>{facility.name}{facility.active ? '' : ' (inactive)'}</option>)}</select></label>
                      <label className="cl-admin-field"><span>Email</span><input className="ne-input" type="email" value={userForm.email} onChange={(event) => setUserForm((form) => ({ ...form, email: event.target.value }))} /></label>
                      <label className="cl-admin-field"><span>NPI</span><input className="ne-input" value={userForm.npi} onChange={(event) => setUserForm((form) => ({ ...form, npi: event.target.value }))} /></label>
                      <label className="cl-admin-active-toggle"><input type="checkbox" checked={userForm.calendar} onChange={(event) => setUserForm((form) => ({ ...form, calendar: event.target.checked }))} /><span>Appears on calendar</span></label>
                      <label className="cl-admin-active-toggle"><input type="checkbox" checked={userForm.active} onChange={(event) => setUserForm((form) => ({ ...form, active: event.target.checked }))} /><span>Staff user is active</span></label>
                    </div>
                    <div className="cl-inline-form-actions">
                      <button className="cl-btn-primary" type="submit" disabled={savingUser}>{savingUser ? 'Saving...' : editingUserId === 'new' ? 'Create staff user' : 'Save changes'}</button>
                      <button className="cl-btn-secondary" type="button" onClick={cancelUserEdit} disabled={savingUser}>Cancel</button>
                    </div>
                  </form>
                )}
                <table className="cl-table">
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Username</th>
                      <th>Role</th>
                      <th>Facility</th>
                      <th>NPI</th>
                      <th>Active</th>
                      <th><span className="sr-only">Actions</span></th>
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
                        <td className="cl-admin-row-actions">
                          <button className="cl-icon-button" type="button" onClick={() => beginUserEdit(u)} aria-label={`Edit ${u.displayName}`} title="Edit staff user"><Pencil size={15} /></button>
                          <button className="cl-icon-button cl-icon-button-danger" type="button" onClick={() => removeUser(u)} disabled={deletingUserId === u.id || u.id === session.staffId} aria-label={`Delete ${u.displayName}`} title={u.id === session.staffId ? 'You cannot delete the signed-in staff user' : 'Delete staff user'}><Trash2 size={15} /></button>
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

            {tab === 'reviews' && (
              <section className="cl-card">
                <div className="cl-card-header">
                  <div>
                    <h2 className="cl-card-title">Portal profile reviews</h2>
                    <p className="clinician-page-subtitle">Review requested patient portal profile changes before they reach the chart.</p>
                  </div>
                  <span className="cl-badge cl-badge-muted">{data.portalActivity.waitingAuditCount} audits waiting</span>
                </div>
                {data.portalActivity.profileReviewRequests.length === 0 ? (
                  <p className="cl-empty-text">No portal profile changes are waiting for review.</p>
                ) : (
                  <div className="cl-review-list">
                    {data.portalActivity.profileReviewRequests.map((request) => {
                      const working = reviewActionId === request.id
                      const demographic = request.requestedDemographics
                      const contact = [demographic.email, demographic.phoneHome, demographic.phoneCell].filter(Boolean).join(' · ')
                      const address = [demographic.street, demographic.city, demographic.state, demographic.postalCode].filter(Boolean).join(', ')
                      return <article key={request.id} className="cl-review-card"><div className="cl-review-top"><div><p className="cl-form-section-label">{request.narrative}</p><h3>{request.patientName}</h3><p>{request.pubpid} · PID {request.legacyPid} · Requested {request.requestedAt}</p></div><span className="cl-badge cl-badge-muted">{request.status}</span></div><div className="cl-review-facts"><span><strong>Contact:</strong> {contact || 'Not supplied'}</span><span><strong>Address:</strong> {address || 'Not supplied'}</span><span><strong>Pending:</strong> {request.pendingAction}</span></div><div className="cl-inline-form-actions"><button className="cl-btn-primary" type="button" disabled={working} onClick={() => resolveProfileReview(request, 'accept')}><Check size={15} /> {working ? 'Working...' : 'Commit to chart'}</button><button className="cl-btn-secondary" type="button" disabled={working} onClick={() => resolveProfileReview(request, 'revert')}><RotateCcw size={15} /> Revert edits</button></div></article>
                    })}
                  </div>
                )}
              </section>
            )}

            {tab === 'audit' && (
              <section className="cl-card" style={{ padding: 0, overflow: 'hidden' }}>
                <div className="cl-admin-facility-header">
                  <div>
                    <h2 className="cl-card-title">Recent PHI access decisions</h2>
                    <p className="clinician-page-subtitle">Read-only access-control evidence from the protected audit endpoint. This view does not replace production retention or export policy.</p>
                  </div>
                  {auditState.status === 'ready' && <span className="cl-badge cl-badge-muted">{auditState.data.authorizedEvents} allowed · {auditState.data.deniedEvents} denied</span>}
                </div>
                {auditState.status === 'loading' && <div className="skeleton-list">{[0, 1, 2].map((item) => <div key={item} className="skeleton-row" style={{ height: 52 }} />)}</div>}
                {auditState.status === 'error' && <div className="error-banner">{auditState.message}</div>}
                {auditState.status === 'ready' && auditState.data.events.length === 0 && <p className="cl-empty-text" style={{ padding: '0 20px 20px' }}>No recent PHI access decisions are available.</p>}
                {auditState.status === 'ready' && auditState.data.events.length > 0 && <table className="cl-table"><thead><tr><th>When</th><th>User</th><th>Request</th><th>Permission</th><th>Decision</th></tr></thead><tbody>{auditState.data.events.map((entry) => <tr key={entry.auditId}><td className="cl-td-muted">{entry.occurredAt}</td><td>{entry.username}</td><td className="cl-td-muted">{entry.httpMethod} {entry.requestPath}</td><td className="cl-td-muted">{entry.requiredPermission}</td><td><span className={`cl-badge ${entry.authorized ? 'cl-badge-green' : 'cl-badge-red'}`}>{entry.authorized ? `Allowed (${entry.responseStatus})` : `Denied (${entry.responseStatus})`}</span></td></tr>)}</tbody></table>}
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
                <div className="cl-access-grid">
                  <section className="cl-access-panel">
                    <div>
                      <h3 className="cl-access-title">User memberships</h3>
                      <p className="cl-admin-form-copy">Assign a staff user to an existing access group.</p>
                    </div>
                    <form className="cl-access-form" onSubmit={saveMembership}>
                      <label className="cl-admin-field"><span>Staff user</span><select className="ne-input" value={membershipForm.userValue} onChange={(event) => setMembershipForm((form) => ({ ...form, userValue: event.target.value }))} required><option value="">Select staff user</option>{data.users.map((user) => <option key={user.id} value={user.username}>{user.displayName} ({user.username})</option>)}</select></label>
                      <label className="cl-admin-field"><span>Access group</span><select className="ne-input" value={membershipForm.groupValue} onChange={(event) => setMembershipForm((form) => ({ ...form, groupValue: event.target.value }))} required><option value="">Select group</option>{data.accessControl.groups.map((group) => <option key={group.id} value={group.value}>{group.name}</option>)}</select></label>
                      <button className="cl-btn-primary" type="submit" disabled={savingMembership || !membershipForm.userValue || !membershipForm.groupValue}>{savingMembership ? 'Saving...' : 'Add membership'}</button>
                    </form>
                    <ul className="cl-access-list">
                      {data.accessControl.userMemberships.map((membership) => {
                        const key = `${membership.userValue}:${membership.groupValue}`
                        const isCurrentUser = membership.staffId === session.staffId
                        return <li key={key} className="cl-access-row"><div><p>{membership.userName}</p><span>{membership.groupName}</span></div><button className="cl-icon-button cl-icon-button-danger" type="button" onClick={() => removeMembership(membership)} disabled={removingMembership === key || isCurrentUser} aria-label={`Remove ${membership.userName} from ${membership.groupName}`} title={isCurrentUser ? 'You cannot remove the signed-in user' : 'Remove membership'}><Trash2 size={15} /></button></li>
                      })}
                      {data.accessControl.userMemberships.length === 0 && <li className="cl-empty-text">No access-group memberships configured.</li>}
                    </ul>
                  </section>

                  <section className="cl-access-panel">
                    <div>
                      <h3 className="cl-access-title">Group permissions</h3>
                      <p className="cl-admin-form-copy">Grant or replace one existing permission on an access group.</p>
                    </div>
                    <form className="cl-access-form" onSubmit={savePermission}>
                      <label className="cl-admin-field"><span>Access group</span><select className="ne-input" value={permissionForm.groupValue} onChange={(event) => setPermissionForm((form) => ({ ...form, groupValue: event.target.value }))} required><option value="">Select group</option>{data.accessControl.groups.map((group) => <option key={group.id} value={group.value}>{group.name}</option>)}</select></label>
                      <label className="cl-admin-field"><span>Permission</span><select className="ne-input" value={permissionForm.permissionKey} onChange={(event) => setPermissionForm((form) => ({ ...form, permissionKey: event.target.value }))} required><option value="">Select permission</option>{data.accessControl.permissions.map((permission) => <option key={`${permission.sectionValue}:${permission.value}`} value={`${permission.sectionValue}:${permission.value}`}>{permission.name} ({permission.sectionValue})</option>)}</select></label>
                      <label className="cl-admin-field"><span>Access level</span><select className="ne-input" value={permissionForm.returnValue} onChange={(event) => setPermissionForm((form) => ({ ...form, returnValue: event.target.value as AccessPermissionForm['returnValue'] }))}><option value="view">View</option><option value="addonly">Add only</option><option value="write">Write</option><option value="wsome">Write some</option></select></label>
                      <button className="cl-btn-primary" type="submit" disabled={savingPermission || !permissionForm.groupValue || !permissionForm.permissionKey}>{savingPermission ? 'Saving...' : 'Save permission'}</button>
                    </form>
                    <ul className="cl-access-list">
                      {data.accessControl.groupPermissions.map((permission) => {
                        const key = `${permission.groupValue}:${permission.sectionValue}:${permission.permissionValue}`
                        return <li key={key} className="cl-access-row"><div><p>{permission.permissionName}</p><span>{permission.groupValue} · {permission.returnValue}</span></div><button className="cl-icon-button cl-icon-button-danger" type="button" onClick={() => removePermission(permission)} disabled={removingPermission === key} aria-label={`Remove ${permission.permissionName} from ${permission.groupValue}`} title="Remove permission"><Trash2 size={15} /></button></li>
                      })}
                      {data.accessControl.groupPermissions.length === 0 && <li className="cl-empty-text">No access permissions configured.</li>}
                    </ul>
                  </section>
                </div>
              </section>
            )}
          </>
        )
      })()}
    </div>
  )
}
