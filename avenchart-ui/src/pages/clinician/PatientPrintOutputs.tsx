import { useEffect, useState } from 'react'
import { Printer } from 'lucide-react'
import { useOutletContext } from 'react-router-dom'
import {
  getPatientPrintableOutput,
  getPatientReferrals,
  searchEncounters,
  type EncounterListItem,
  type PatientReferral,
} from '../../api.ts'
import { showToast } from '../../components/Toast.tsx'
import type { PatientOutletContext } from './PatientShell.tsx'

type PrintableOutput =
  | 'demographics'
  | 'chart-labels'
  | 'address-label'
  | 'referral'
  | 'fee-sheet'

export default function PatientPrintOutputs() {
  const { session, patientId } = useOutletContext<PatientOutletContext>()
  const [referrals, setReferrals] = useState<PatientReferral[]>([])
  const [encounters, setEncounters] = useState<EncounterListItem[]>([])
  const [referralId, setReferralId] = useState('')
  const [encounterId, setEncounterId] = useState('')
  const [labelCount, setLabelCount] = useState(30)

  useEffect(() => {
    Promise.all([
      getPatientReferrals(session.sessionId, patientId),
      searchEncounters(session.sessionId, { patientId, limit: 100 }),
    ])
      .then(([referralResult, encounterResult]) => {
        setReferrals(referralResult)
        setEncounters(encounterResult.encounters)
        setReferralId(referralResult[0]?.id ?? '')
        setEncounterId(
          encounterResult.encounters[0]?.encounter
            ? String(encounterResult.encounters[0].encounter)
            : '',
        )
      })
      .catch(() =>
        showToast('Could not load printable-output choices.', 'error'),
      )
  }, [session.sessionId, patientId])

  async function print(output: PrintableOutput) {
    if (output === 'referral' && !referralId) {
      showToast('Select a referral first.', 'error')
      return
    }
    if (output === 'fee-sheet' && !encounterId) {
      showToast('Select an encounter first.', 'error')
      return
    }

    const popup = window.open('', '_blank', 'noopener,noreferrer')
    try {
      const html = await getPatientPrintableOutput(
        session.sessionId,
        patientId,
        output,
        {
          referralId: output === 'referral' ? referralId : undefined,
          encounterId:
            output === 'fee-sheet' ? Number(encounterId) : undefined,
          labelCount: output === 'chart-labels' ? labelCount : undefined,
        },
      )
      if (!popup) {
        showToast(
          'Popup was blocked. Allow popups to print this output.',
          'error',
        )
        return
      }
      popup.document.open()
      popup.document.write(html)
      popup.document.close()
      popup.focus()
      setTimeout(() => popup.print(), 150)
    } catch {
      popup?.close()
      showToast('Could not generate the printable output.', 'error')
    }
  }

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <h1 className="clinician-page-title">Printable Outputs</h1>
        <p className="clinician-page-subtitle">
          Prepare legacy-aligned patient outputs locally. This opens the
          browser print dialog; it does not send data to a physical printer or
          external destination.
        </p>
      </div>

      <section className="cl-card">
        <h2 className="cl-card-title">Patient demographics</h2>
        <p className="cl-table-sub">Full demographic/contact summary.</p>
        <button
          className="cl-btn-primary"
          onClick={() => void print('demographics')}
        >
          <Printer size={15} /> Print demographics
        </button>
      </section>

      <section className="cl-card">
        <h2 className="cl-card-title">Chart and address labels</h2>
        <div className="cl-inline-form">
          <label className="cl-admin-field">
            <span>Chart-label count</span>
            <input
              className="ne-input"
              type="number"
              min="1"
              max="60"
              value={labelCount}
              onChange={(event) =>
                setLabelCount(
                  Math.max(1, Math.min(60, Number(event.target.value) || 1)),
                )
              }
            />
          </label>
        </div>
        <div className="cl-actions">
          <button
            className="cl-btn-secondary"
            onClick={() => void print('chart-labels')}
          >
            <Printer size={15} /> Print chart labels
          </button>
          <button
            className="cl-btn-secondary"
            onClick={() => void print('address-label')}
          >
            <Printer size={15} /> Print address label
          </button>
        </div>
      </section>

      <section className="cl-card">
        <h2 className="cl-card-title">Referral form</h2>
        <select
          className="ne-input"
          aria-label="Referral to print"
          value={referralId}
          onChange={(event) => setReferralId(event.target.value)}
        >
          <option value="">Select a referral</option>
          {referrals.map((referral) => (
            <option key={referral.id} value={referral.id}>
              {new Date(referral.requestedAt).toLocaleDateString()} ·{' '}
              {referral.destination} · {referral.status}
            </option>
          ))}
        </select>
        <button
          className="cl-btn-secondary"
          onClick={() => void print('referral')}
        >
          <Printer size={15} /> Print referral
        </button>
      </section>

      <section className="cl-card">
        <h2 className="cl-card-title">Superbill / fee sheet</h2>
        <select
          className="ne-input"
          aria-label="Encounter to print"
          value={encounterId}
          onChange={(event) => setEncounterId(event.target.value)}
        >
          <option value="">Select an encounter</option>
          {encounters.map((encounter) => (
            <option key={encounter.encounter} value={encounter.encounter}>
              {encounter.date} · #{encounter.encounter} ·{' '}
              {encounter.reason || 'No reason recorded'}
            </option>
          ))}
        </select>
        <button
          className="cl-btn-secondary"
          onClick={() => void print('fee-sheet')}
        >
          <Printer size={15} /> Print fee sheet
        </button>
      </section>
    </div>
  )
}
