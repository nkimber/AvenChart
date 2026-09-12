// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

const steps = ['Visit reason', 'Safety', 'Your details', 'Waiting', 'Video visit', 'Wrap-up', 'Done']
const stage: Record<string, number> = { Draft: 0, LocationConfirmed: 1, Intake: 2, Verification: 2, OperationalReview: 2, Queued: 3, Reserved: 3, Connecting: 4, InConsultation: 4, WrapUp: 5, Closed: 6 }

export default function TelehealthVisitProgress({ status }: { status?: string }) {
  if (status === 'Cancelled' || status === 'Redirected') return null
  const current = stage[status ?? 'Draft'] ?? 0
  return <nav aria-label="Visit progress"><ol className="telehealth-progress">
    {steps.map((label, index) => <li key={label} aria-current={index === current ? 'step' : undefined} className={index < current ? 'is-complete' : ''}>
      <span aria-hidden="true">{index < current ? '✓' : index + 1}</span>{label}{index < current ? <span className="visually-hidden"> completed</span> : null}
    </li>)}
  </ol></nav>
}
