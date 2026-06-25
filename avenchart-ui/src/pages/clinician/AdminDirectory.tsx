import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { getAdministrationDirectory, type AdministrationDirectoryResponse } from '../../api.ts'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

type AsyncState<T> =
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; message: string }

export default function AdminDirectory() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const [state, setState] = useState<AsyncState<AdministrationDirectoryResponse>>({ status: 'loading' })
  const [tab, setTab] = useState<'users' | 'facilities' | 'access'>('users')

  useEffect(() => {
    getAdministrationDirectory(session.sessionId)
      .then((data) => setState({ status: 'ready', data }))
      .catch((err) => setState({ status: 'error', message: err instanceof Error ? err.message : 'Failed.' }))
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

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
                <table className="cl-table">
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Code</th>
                      <th>Address</th>
                      <th>Phone</th>
                      <th>Active</th>
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
