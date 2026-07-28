import {
  useEffect,
  useEffectEvent,
  useState,
  type FormEvent,
} from 'react'
import {
  getAuthorizationPolicyCatalog,
  type AuthorizationPolicyCatalogResponse,
  type AuthorizationPolicyGap,
  type AuthorizationPolicyRule,
} from '../../api.ts'

const PAGE_SIZE = 8

type AsyncState =
  | { status: 'loading' }
  | { status: 'ready'; data: AuthorizationPolicyCatalogResponse }
  | { status: 'error'; message: string }

const gapOptions: Array<{ value: AuthorizationPolicyGap; label: string }> = [
  { value: 'all', label: 'All registered rules' },
  { value: 'production-approval', label: 'Production approval missing' },
  { value: 'facility-scope', label: 'Facility scope missing' },
  { value: 'patient-scope', label: 'Patient scope missing' },
  { value: 'purpose', label: 'Purpose condition missing' },
  { value: 'exceptional-access', label: 'Exceptional-access decision missing' },
]

function policyError(error: unknown) {
  return error instanceof Error
    ? error.message
    : 'The authorization policy registry could not be loaded.'
}

function formatGap(value: string) {
  return value.replaceAll('-', ' ')
}

export default function AuthorizationPolicyRegistry({
  sessionId,
}: {
  sessionId: string
}) {
  const [state, setState] = useState<AsyncState>({ status: 'loading' })
  const [queryDraft, setQueryDraft] = useState('')
  const [query, setQuery] = useState('')
  const [gap, setGap] = useState<AuthorizationPolicyGap>('all')
  const [offset, setOffset] = useState(0)
  const [reload, setReload] = useState(0)
  const [selected, setSelected] = useState<AuthorizationPolicyRule | null>(
    null,
  )

  const load = useEffectEvent(async (signal?: AbortSignal) => {
    setState({ status: 'loading' })
    try {
      const data = await getAuthorizationPolicyCatalog(
        sessionId,
        { query, gap, offset, limit: PAGE_SIZE },
        signal,
      )
      setState({ status: 'ready', data })
      setSelected((current) => {
        if (current) {
          return (
            data.rules.find((rule) => rule.policyId === current.policyId) ?? null
          )
        }
        return null
      })
    } catch (error) {
      if (signal?.aborted) return
      setState({ status: 'error', message: policyError(error) })
    }
  })

  useEffect(() => {
    const controller = new AbortController()
    void load(controller.signal)
    return () => controller.abort()
  }, [sessionId, query, gap, offset, reload])

  function applyFilters(event: FormEvent) {
    event.preventDefault()
    setOffset(0)
    setQuery(queryDraft.trim())
  }

  const data = state.status === 'ready' ? state.data : null
  const page = data ? Math.floor(data.offset / data.limit) + 1 : 1
  const pages = data ? Math.max(1, Math.ceil(data.total / data.limit)) : 1

  return (
    <section
      className="authorization-policy-registry"
      aria-labelledby="authorization-policy-heading"
    >
      <div className="authorization-policy-heading">
        <div>
          <p className="practice-governance-kicker">SEC-01 local foundation</p>
          <h3 id="authorization-policy-heading">
            Authorization policy coverage
          </h3>
          <p>
            This versioned registry is the source for every current server ACL
            decision rule. It documents what is enforced locally and what still
            requires accountable policy.
          </p>
        </div>
        {data && (
          <span className="authorization-policy-revision">
            {data.revision}
          </span>
        )}
      </div>

      <aside className="authorization-policy-boundary" role="note">
        <strong>Policy boundary:</strong> these rules reproduce the current
        local ACL matrix. None is production-approved. Facility and
        patient/team scope, purpose of use, effective intervals, and emergency
        access are not selected or enforced.
      </aside>

      {data && (
        <div
          className="authorization-policy-counts"
          aria-label="Authorization policy counts"
        >
          <article>
            <strong>{data.counts.total}</strong>
            <span>Registered ACL rules</span>
          </article>
          <article>
            <strong>{data.counts.locallyEnforced}</strong>
            <span>Locally enforced</span>
          </article>
          <article>
            <strong>{data.counts.productionApproved}</strong>
            <span>Production approved</span>
          </article>
          <article>
            <strong>{data.counts.facilityScoped}</strong>
            <span>Facility scoped</span>
          </article>
          <article>
            <strong>{data.counts.patientScoped}</strong>
            <span>Patient scoped</span>
          </article>
          <article>
            <strong>{data.counts.purposeConditioned}</strong>
            <span>Purpose conditioned</span>
          </article>
          <article>
            <strong>{data.counts.exceptionalAccessDecided}</strong>
            <span>Exceptional access decided</span>
          </article>
        </div>
      )}

      <form className="authorization-policy-filters" onSubmit={applyFilters}>
        <label className="cl-admin-field">
          <span>Search policy rules</span>
          <input
            className="ne-input"
            type="search"
            value={queryDraft}
            maxLength={100}
            placeholder="Capability, permission, owner, or ACL key"
            onChange={(event) => setQueryDraft(event.target.value)}
          />
        </label>
        <label className="cl-admin-field">
          <span>Policy gap</span>
          <select
            className="ne-input"
            value={gap}
            onChange={(event) => {
              setGap(event.target.value as AuthorizationPolicyGap)
              setOffset(0)
            }}
          >
            {gapOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </label>
        <button className="cl-btn-secondary" type="submit">
          Apply
        </button>
        <button
          className="cl-btn-ghost"
          type="button"
          onClick={() => {
            setQueryDraft('')
            setQuery('')
            setGap('all')
            setOffset(0)
          }}
        >
          Clear
        </button>
      </form>

      {state.status === 'loading' && (
        <div className="authorization-policy-message" role="status">
          Loading authorization policy coverage…
        </div>
      )}
      {state.status === 'error' && (
        <div className="authorization-policy-message" role="alert">
          <p>{state.message}</p>
          <button
            className="cl-btn-secondary"
            type="button"
            onClick={() => setReload((value) => value + 1)}
          >
            Retry
          </button>
        </div>
      )}
      {data && data.rules.length === 0 && (
        <div className="authorization-policy-message">
          <p>
            {query || gap !== 'all'
              ? 'No policy rules match these filters.'
              : 'No authorization policy rules are registered.'}
          </p>
          {(query || gap !== 'all') && (
            <button
              className="cl-btn-secondary"
              type="button"
              onClick={() => {
                setQueryDraft('')
                setQuery('')
                setGap('all')
                setOffset(0)
              }}
            >
              Clear filters
            </button>
          )}
        </div>
      )}

      {data && data.rules.length > 0 && (
        <>
          <div className="authorization-policy-table-wrap">
            <table className="cl-table authorization-policy-table">
              <thead>
                <tr>
                  <th>Capability and rule</th>
                  <th>Server ACL</th>
                  <th>Owner</th>
                  <th>State</th>
                  <th>Detail</th>
                </tr>
              </thead>
              <tbody>
                {data.rules.map((rule) => (
                  <tr key={rule.policyId}>
                    <td>
                      <strong>{rule.permissionName}</strong>
                      <small>
                        {rule.capability} · {rule.policyId}
                      </small>
                    </td>
                    <td>
                      <code>
                        {rule.section}:{rule.permission}:{rule.minimumLevel}
                      </code>
                    </td>
                    <td>{rule.owner}</td>
                    <td>
                      <span className="cl-badge cl-badge-amber">
                        Owner gated
                      </span>
                    </td>
                    <td>
                      <button
                        className="cl-btn-ghost"
                        type="button"
                        aria-expanded={selected?.policyId === rule.policyId}
                        onClick={() =>
                          setSelected((current) =>
                            current?.policyId === rule.policyId ? null : rule,
                          )
                        }
                      >
                        {selected?.policyId === rule.policyId ? 'Close' : 'Open'}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div
            className="authorization-policy-pagination"
            aria-label="Authorization policy pages"
          >
            <span>
              Page {page} of {pages} · showing {data.returned} of {data.total}
            </span>
            <div>
              <button
                className="cl-btn-secondary"
                type="button"
                disabled={data.offset === 0}
                onClick={() =>
                  setOffset(Math.max(0, data.offset - data.limit))
                }
              >
                Previous
              </button>
              <button
                className="cl-btn-secondary"
                type="button"
                disabled={data.offset + data.returned >= data.total}
                onClick={() => setOffset(data.offset + data.limit)}
              >
                Next
              </button>
            </div>
          </div>
        </>
      )}

      {selected && (
        <section
          className="authorization-policy-detail"
          aria-label="Authorization policy detail"
        >
          <div>
            <p className="practice-governance-kicker">Selected rule</p>
            <h4>{selected.permissionName}</h4>
            <p>
              <code>{selected.policyId}</code>
            </p>
          </div>
          <dl>
            <div>
              <dt>Local state</dt>
              <dd>{formatGap(selected.policyState)}</dd>
            </div>
            <div>
              <dt>Approval</dt>
              <dd>{formatGap(selected.approvalState)}</dd>
            </div>
            <div>
              <dt>Enforcement</dt>
              <dd>{formatGap(selected.enforcement)}</dd>
            </div>
            <div>
              <dt>Verification</dt>
              <dd>{formatGap(selected.verificationState)}</dd>
            </div>
            <div>
              <dt>Subject</dt>
              <dd>{formatGap(selected.subjectType)}</dd>
            </div>
            <div>
              <dt>Organization</dt>
              <dd>{formatGap(selected.organizationScope)}</dd>
            </div>
            <div>
              <dt>Facility scope</dt>
              <dd>{formatGap(selected.facilityScope)}</dd>
            </div>
            <div>
              <dt>Patient/team scope</dt>
              <dd>{formatGap(selected.patientScope)}</dd>
            </div>
            <div>
              <dt>Purpose</dt>
              <dd>{formatGap(selected.purposeRequirement)}</dd>
            </div>
            <div>
              <dt>Exceptional access</dt>
              <dd>{formatGap(selected.exceptionalAccess)}</dd>
            </div>
          </dl>
          <div>
            <strong>Open governance gaps</strong>
            <ul>
              {selected.openGaps.map((item) => (
                <li key={item}>{formatGap(item)}</li>
              ))}
            </ul>
          </div>
        </section>
      )}

      {data && (
        <details className="authorization-policy-gaps">
          <summary>Registry-wide governance gaps</summary>
          <ul>
            {data.registryGaps.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </details>
      )}
    </section>
  )
}
