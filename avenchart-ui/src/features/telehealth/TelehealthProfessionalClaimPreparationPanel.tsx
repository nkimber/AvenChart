// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useEffect, useId, useRef, useState } from 'react'
import { isRequestCancellation } from '../../api/transport.ts'
import { getTelehealthProfessionalClaimPreparation, type TelehealthProfessionalClaimPreparationWorkspace } from './api.ts'

type Props = { consultationId: string }

export default function TelehealthProfessionalClaimPreparationPanel({ consultationId }: Props) {
  const headingId = useId()
  const errorRef = useRef<HTMLParagraphElement>(null)
  const [workspace, setWorkspace] = useState<TelehealthProfessionalClaimPreparationWorkspace | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => { if (error) errorRef.current?.focus() }, [error])
  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true); setError(null)
    try { setWorkspace(await getTelehealthProfessionalClaimPreparation(consultationId, signal)) }
    catch (caught) { if (!isRequestCancellation(caught)) setError(caught instanceof Error ? caught.message : 'Claim-preparation blockers could not be loaded.') }
    finally { setLoading(false) }
  }, [consultationId])
  useEffect(() => { const controller = new AbortController(); void load(controller.signal); return () => controller.abort() }, [load])
  return <section className="telehealth-completion-review" aria-labelledby={headingId} aria-busy={loading}>
    <div className="telehealth-heading"><div><p className="telehealth-kicker">Professional claim preparation</p><h4 id={headingId}>Claim-preparation blockers</h4></div><button className="telehealth-button telehealth-button-secondary" type="button" disabled={loading} onClick={() => void load()}>{loading ? 'Refreshing…' : 'Reload blockers'}</button></div>
    <p role="note">This is a structural, non-production assessment. It cannot prepare or submit a claim, and it does not promise payer coverage or payment.</p>
    {error ? <p ref={errorRef} tabIndex={-1} className="telehealth-error" role="alert">{error}</p> : null}
    {workspace ? <><p role="status">Target: {workspace.targetStandard}. Preparation and submission remain unavailable.</p><ul>{workspace.blockers.map((blocker) => <li key={blocker}>{blocker}</li>)}</ul><ul>{workspace.limitations.map((limitation) => <li key={limitation}>{limitation}</li>)}</ul></> : null}
  </section>
}
