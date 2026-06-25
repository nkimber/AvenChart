import { useEffect, useMemo, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { CalendarClock, FileText, FlaskConical, Pill } from 'lucide-react'
import {
  searchEncounters,
  searchAppointments,
  getProcedureReportQueue,
  getClinicalLists,
  type EncounterListItem,
  type AppointmentListItem,
  type ProcedureReportQueueItem,
  type PrescriptionListItem,
} from '../../api.ts'
import type { PatientOutletContext } from './PatientShell.tsx'

type EventKind = 'encounter' | 'appointment' | 'lab' | 'prescription'

type TimelineEvent = {
  id: string
  kind: EventKind
  date: string
  title: string
  subtitle?: string | null
  badge?: string | null
  badgeClass?: string
}

const KIND_META: Record<EventKind, { label: string; icon: React.FC<{ size: number }>; cls: string }> = {
  encounter: { label: 'Encounter', icon: FileText, cls: 'tl-kind-encounter' },
  appointment: { label: 'Appointment', icon: CalendarClock, cls: 'tl-kind-appt' },
  lab: { label: 'Lab result', icon: FlaskConical, cls: 'tl-kind-lab' },
  prescription: { label: 'Prescription', icon: Pill, cls: 'tl-kind-rx' },
}

const ALL_KINDS: EventKind[] = ['encounter', 'appointment', 'lab', 'prescription']

function toEvents(
  encounters: EncounterListItem[],
  appointments: AppointmentListItem[],
  labs: ProcedureReportQueueItem[],
  prescriptions: PrescriptionListItem[],
): TimelineEvent[] {
  const evts: TimelineEvent[] = [
    ...encounters.map((e): TimelineEvent => ({
      id: `enc-${e.id}`,
      kind: 'encounter',
      date: e.date,
      title: e.reason ?? 'Visit',
      subtitle: [e.providerName, e.facilityName].filter(Boolean).join(' · '),
      badge: e.diagnosisCode ?? undefined,
      badgeClass: 'cl-badge-teal',
    })),
    ...appointments.map((a): TimelineEvent => ({
      id: `appt-${a.id}`,
      kind: 'appointment',
      date: a.date,
      title: a.title,
      subtitle: [a.providerName, a.startTime?.slice(0, 5)].filter(Boolean).join(' · '),
      badge: a.status ?? undefined,
      badgeClass: a.status?.toLowerCase().includes('cancel') ? 'cl-badge-red' : 'cl-badge-muted',
    })),
    ...labs.map((l): TimelineEvent => ({
      id: `lab-${l.reportId}`,
      kind: 'lab',
      date: l.reportDate,
      title: l.procedureName ?? l.procedureCode ?? 'Lab result',
      subtitle: l.labName ?? undefined,
      badge: l.reviewedBy ? 'reviewed' : 'pending',
      badgeClass: l.reviewedBy ? 'cl-badge-green' : 'cl-badge-amber',
    })),
    ...prescriptions
      .filter((rx) => rx.startDate)
      .map((rx): TimelineEvent => ({
        id: `rx-${rx.id}`,
        kind: 'prescription',
        date: rx.startDate!,
        title: rx.drug,
        subtitle: [rx.dosage, rx.route].filter(Boolean).join(' · '),
        badge: rx.active ? 'active' : 'inactive',
        badgeClass: rx.active ? 'cl-badge-green' : 'cl-badge-muted',
      })),
  ]
  evts.sort((a, b) => b.date.localeCompare(a.date))
  return evts
}

export default function PatientTimeline() {
  const { session, patientId } = useOutletContext<PatientOutletContext>()
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [encounters, setEncounters] = useState<EncounterListItem[]>([])
  const [appointments, setAppointments] = useState<AppointmentListItem[]>([])
  const [labs, setLabs] = useState<ProcedureReportQueueItem[]>([])
  const [prescriptions, setPrescriptions] = useState<PrescriptionListItem[]>([])
  const [activeKinds, setActiveKinds] = useState<Set<EventKind>>(new Set(ALL_KINDS))

  useEffect(() => {
    setLoading(true)
    setError(null)
    Promise.all([
      searchEncounters(session.sessionId, { patientId, limit: 100 }),
      searchAppointments(session.sessionId, { patientId, limit: 100 }),
      getProcedureReportQueue(session.sessionId, { limit: 100 }),
      getClinicalLists(session.sessionId, patientId),
    ])
      .then(([enc, appt, labData, lists]) => {
        setEncounters(enc.encounters)
        setAppointments(appt.appointments)
        setLabs(labData.reports.filter((r) => r.patientId === patientId))
        setPrescriptions(lists.prescriptions)
        setLoading(false)
      })
      .catch((err) => {
        setError(err instanceof Error ? err.message : 'Could not load timeline.')
        setLoading(false)
      })
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [patientId])

  const events = useMemo(
    () => toEvents(encounters, appointments, labs, prescriptions).filter((e) => activeKinds.has(e.kind)),
    [encounters, appointments, labs, prescriptions, activeKinds],
  )

  function toggleKind(k: EventKind) {
    setActiveKinds((prev) => {
      const next = new Set(prev)
      if (next.has(k) && next.size === 1) return prev
      if (next.has(k)) next.delete(k)
      else next.add(k)
      return next
    })
  }

  // Group events by year-month
  const grouped = useMemo(() => {
    const map = new Map<string, TimelineEvent[]>()
    for (const evt of events) {
      const key = evt.date.slice(0, 7)
      if (!map.has(key)) map.set(key, [])
      map.get(key)!.push(evt)
    }
    return Array.from(map.entries())
  }, [events])

  function monthLabel(ym: string) {
    const [y, m] = ym.split('-')
    return new Date(Number(y), Number(m) - 1, 1).toLocaleDateString('en-US', { month: 'long', year: 'numeric' })
  }

  return (
    <div className="clinician-page">
      {/* Kind filters */}
      <div className="tl-filters">
        {ALL_KINDS.map((k) => {
          const meta = KIND_META[k]
          const Icon = meta.icon
          return (
            <button
              key={k}
              type="button"
              className={`tl-filter-btn${activeKinds.has(k) ? ' tl-filter-btn-active' : ''} tl-filter-${k}`}
              onClick={() => toggleKind(k)}
            >
              <Icon size={13} />
              {meta.label}
            </button>
          )
        })}
      </div>

      {loading && (
        <div className="cl-card">
          <div className="skeleton-list">
            {[0, 1, 2, 3, 4, 5].map((i) => <div key={i} className="skeleton-row" style={{ height: 52 }} />)}
          </div>
        </div>
      )}
      {error && <div className="error-banner">{error}</div>}
      {!loading && !error && events.length === 0 && (
        <div className="cl-card"><p className="cl-empty-text">No events to display.</p></div>
      )}

      {!loading && !error && grouped.map(([ym, evts]) => (
        <div key={ym} className="tl-month-group">
          <p className="tl-month-label">{monthLabel(ym)}</p>
          <div className="tl-event-list">
            {evts.map((evt) => {
              const meta = KIND_META[evt.kind]
              const Icon = meta.icon
              return (
                <div key={evt.id} className={`tl-event ${meta.cls}`}>
                  <div className="tl-event-icon">
                    <Icon size={14} />
                  </div>
                  <div className="tl-event-body">
                    <div className="tl-event-top">
                      <p className="tl-event-title">{evt.title}</p>
                      {evt.badge && (
                        <span className={`cl-badge ${evt.badgeClass ?? 'cl-badge-muted'}`}>{evt.badge}</span>
                      )}
                    </div>
                    <p className="tl-event-meta">
                      {evt.date}
                      {evt.subtitle ? ` · ${evt.subtitle}` : ''}
                    </p>
                  </div>
                </div>
              )
            })}
          </div>
        </div>
      ))}
    </div>
  )
}
