import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useNavigate, useOutletContext } from 'react-router-dom'
import { Search, UserCircle } from 'lucide-react'
import { searchPatients, type PatientListItem } from '../../api.ts'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

type AsyncState<T> =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; data: T; total: number }
  | { status: 'error'; message: string }

export default function PatientSearch() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const navigate = useNavigate()
  const [query, setQuery] = useState('')
  const [state, setState] = useState<AsyncState<PatientListItem[]>>({ status: 'idle' })
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => { inputRef.current?.focus() }, [])

  function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!query.trim()) return
    setState({ status: 'loading' })
    searchPatients(session.sessionId, { search: query.trim(), limit: 25 })
      .then((data) => setState({ status: 'ready', data: data.patients, total: data.totalMatches }))
      .catch((err) => setState({ status: 'error', message: err instanceof Error ? err.message : 'Search failed.' }))
  }

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <h1 className="clinician-page-title">Patient search</h1>
      </div>

      <section className="cl-card">
        <form className="cl-search-form" onSubmit={handleSubmit}>
          <div className="cl-search-input-wrap">
            <Search size={16} className="cl-search-icon" aria-hidden="true" />
            <input
              ref={inputRef}
              className="cl-search-input"
              type="search"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Search by name, DOB (YYYY-MM-DD), chart #, or email…"
              aria-label="Search patients"
            />
          </div>
          <button className="cl-btn-primary" type="submit" disabled={state.status === 'loading'}>
            {state.status === 'loading' ? 'Searching…' : 'Search'}
          </button>
        </form>
      </section>

      {state.status === 'loading' && (
        <section className="cl-card">
          <div className="skeleton-list">
            {[0, 1, 2, 3].map((i) => <div key={i} className="skeleton-row" style={{ height: 60 }} />)}
          </div>
        </section>
      )}

      {state.status === 'error' && (
        <div className="error-banner">{state.message}</div>
      )}

      {state.status === 'ready' && state.data.length === 0 && (
        <section className="cl-card">
          <p className="cl-empty-text">No patients found for "{query}".</p>
        </section>
      )}

      {state.status === 'ready' && state.data.length > 0 && (
        <section className="cl-card" style={{ padding: 0, overflow: 'hidden' }}>
          <div className="cl-result-meta">
            {state.total} patient{state.total === 1 ? '' : 's'} found
            {state.total > state.data.length ? `, showing first ${state.data.length}` : ''}
          </div>
          <ul className="cl-patient-list" role="list">
            {state.data.map((p) => (
              <li key={p.canonicalId}>
                <button
                  className="cl-patient-row"
                  type="button"
                  onClick={() => navigate(`/clinician/patients/${p.canonicalId}`)}
                >
                  <div className="cl-patient-avatar" aria-hidden="true">
                    {p.firstName[0]?.toUpperCase()}{p.lastName[0]?.toUpperCase()}
                  </div>
                  <div className="cl-patient-info">
                    <p className="cl-patient-name">{p.displayName}</p>
                    <p className="cl-patient-meta">
                      {p.dateOfBirth} · {p.age}y
                      {p.sex ? ` · ${p.sex}` : ''}
                      {p.pubpid ? ` · #${p.pubpid}` : ''}
                    </p>
                  </div>
                  <div className="cl-patient-right">
                    {p.primaryProviderName && (
                      <p className="cl-patient-provider">{p.primaryProviderName}</p>
                    )}
                    <div className="cl-patient-counts">
                      <span title="Appointments">{p.counts.appointments} appts</span>
                      <span title="Encounters">{p.counts.encounters} enc</span>
                    </div>
                  </div>
                </button>
              </li>
            ))}
          </ul>
        </section>
      )}

      {state.status === 'idle' && (
        <div className="cl-search-empty-state">
          <UserCircle size={48} aria-hidden="true" />
          <p>Enter a name, date of birth, chart number, or email to find a patient.</p>
        </div>
      )}
    </div>
  )
}
