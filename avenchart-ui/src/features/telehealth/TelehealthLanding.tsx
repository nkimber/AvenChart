// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiRequestError, isRequestCancellation } from '../../api/transport.ts'
import { getPracticeContext, type TelehealthPracticeContext } from './api.ts'
import './telehealth.css'

export default function TelehealthLanding() {
  const [context, setContext] = useState<TelehealthPracticeContext | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    setError(null)
    getPracticeContext(controller.signal)
      .then((result) => setContext(result))
      .catch((caught: unknown) => {
        if (isRequestCancellation(caught)) return
        setContext(null)
        setError(
          caught instanceof ApiRequestError && caught.status === 404
            ? 'Immediate telehealth is not enabled for this practice.'
            : caught instanceof Error
              ? caught.message
              : 'Telehealth availability could not be checked.',
        )
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [attempt])

  return (
    <main className="telehealth-page telehealth-landing" id="main-content">
      <section className="telehealth-hero" aria-labelledby="telehealth-title">
        <p className="telehealth-kicker">Immediate care request</p>
        <h1 id="telehealth-title">Telehealth</h1>
        <p>Request a same-day review for a simple, low-acuity concern.</p>
        <div className="telehealth-synthetic" role="note">
          Synthetic demonstration only. This cannot be used for patient care.
        </div>
      </section>

      <section className="telehealth-emergency" aria-labelledby="telehealth-emergency-title">
        <h2 id="telehealth-emergency-title">If this may be an emergency</h2>
        <p>Call 911 now or go to the nearest emergency department. Do not wait for this service.</p>
        <a className="telehealth-button telehealth-button-danger" href="tel:911">Call 911</a>
      </section>

      <section className="telehealth-card" aria-live="polite" aria-busy={loading}>
        <h2>Practice availability</h2>
        {loading ? <p>Checking this practice…</p> : null}
        {error ? (
          <div>
            <p className="telehealth-error" role="alert">{error}</p>
            <button className="telehealth-button" type="button" onClick={() => setAttempt((value) => value + 1)}>Try again</button>
          </div>
        ) : null}
        {context ? (
          <div>
            <p><strong>{context.practiceDisplayName}</strong></p>
            <p>{context.entryMessage}</p>
            <p>Supported synthetic locations: {context.supportedStates.join(', ')}.</p>
            <div className="telehealth-actions">
              <Link className="telehealth-button" to="/portal/login">Sign in as an existing patient</Link>
              <Link className="telehealth-button telehealth-button-secondary" to="/telehealth/new">Start as a new patient</Link>
            </div>
          </div>
        ) : null}
      </section>
    </main>
  )
}
