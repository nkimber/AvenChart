import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useOutletContext } from 'react-router-dom'
import { ChevronLeft, ChevronRight, Clock, X } from 'lucide-react'
import { searchAppointments, type AppointmentListItem } from '../../api.ts'
import type { ClinicianOutletContext } from './ClinicianShell.tsx'

// Distinct provider colors — stable palette, assigned alphabetically per month
const PALETTE = [
  { bg: '#e0f2fe', fg: '#0369a1', dot: '#0284c7', border: '#7dd3fc' },
  { bg: '#fce7f3', fg: '#9d174d', dot: '#db2777', border: '#f9a8d4' },
  { bg: '#d1fae5', fg: '#065f46', dot: '#059669', border: '#6ee7b7' },
  { bg: '#fef3c7', fg: '#92400e', dot: '#d97706', border: '#fcd34d' },
  { bg: '#ede9fe', fg: '#4c1d95', dot: '#7c3aed', border: '#c4b5fd' },
  { bg: '#ffedd5', fg: '#9a3412', dot: '#ea580c', border: '#fdba74' },
  { bg: '#cffafe', fg: '#164e63', dot: '#0891b2', border: '#67e8f9' },
  { bg: '#dcfce7', fg: '#14532d', dot: '#16a34a', border: '#86efac' },
]

type ProviderEntry = { name: string; color: (typeof PALETTE)[0]; count: number }

function isoDate(d: Date) {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

function buildCalGrid(month: Date): Date[] {
  const y = month.getFullYear(), m = month.getMonth()
  const first = new Date(y, m, 1)
  const startPad = first.getDay()
  const daysInMonth = new Date(y, m + 1, 0).getDate()
  const days: Date[] = []
  for (let i = startPad; i > 0; i--) days.push(new Date(y, m, 1 - i))
  for (let i = 1; i <= daysInMonth; i++) days.push(new Date(y, m, i))
  let n = 1
  while (days.length < 42) days.push(new Date(y, m + 1, n++))
  return days
}

function fmt12(t?: string | null) {
  if (!t) return ''
  const [h, min] = t.split(':').map(Number)
  return `${h % 12 || 12}:${String(min).padStart(2, '0')} ${h >= 12 ? 'pm' : 'am'}`
}

function heatLevel(n: number) {
  if (n <= 0) return 0
  if (n <= 2) return 1
  if (n <= 5) return 2
  if (n <= 8) return 3
  return 4
}

const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']

export default function ClinicianCalendar() {
  const { session } = useOutletContext<ClinicianOutletContext>()
  const navigate = useNavigate()
  const todayStr = isoDate(new Date())

  const [month, setMonth] = useState(() => {
    const d = new Date()
    return new Date(d.getFullYear(), d.getMonth(), 1)
  })
  const [appointments, setAppointments] = useState<AppointmentListItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [selectedDate, setSelectedDate] = useState<string | null>(null)
  const [activeProviders, setActiveProviders] = useState<Set<string>>(new Set())

  useEffect(() => {
    setLoading(true)
    setError(null)
    const y = month.getFullYear(), m = month.getMonth()
    const from = isoDate(new Date(y, m, 1))
    const to = isoDate(new Date(y, m + 1, 0))
    searchAppointments(session.sessionId, { fromDate: from, toDate: to, limit: 500 })
      .then((data) => {
        setAppointments(data.appointments)
        const names = new Set(data.appointments.map((a) => a.providerName ?? 'Unassigned'))
        setActiveProviders(names)
        setLoading(false)
      })
      .catch((err) => {
        setError(err instanceof Error ? err.message : 'Failed to load appointments.')
        setLoading(false)
      })
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [month])

  // Stable alphabetical color assignment
  const providers = useMemo<ProviderEntry[]>(() => {
    const map = new Map<string, number>()
    appointments.forEach((a) => {
      const name = a.providerName ?? 'Unassigned'
      map.set(name, (map.get(name) ?? 0) + 1)
    })
    return [...map.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([name, count], i) => ({ name, count, color: PALETTE[i % PALETTE.length] }))
  }, [appointments])

  const providerColorMap = useMemo(() => {
    const m = new Map<string, (typeof PALETTE)[0]>()
    providers.forEach((p) => m.set(p.name, p.color))
    return m
  }, [providers])

  // Filter and group by date
  const byDate = useMemo(() => {
    const map = new Map<string, AppointmentListItem[]>()
    appointments
      .filter((a) => activeProviders.has(a.providerName ?? 'Unassigned'))
      .forEach((a) => {
        const list = map.get(a.date) ?? []
        list.push(a)
        map.set(a.date, list)
      })
    map.forEach((list) => list.sort((a, b) => (a.startTime ?? '').localeCompare(b.startTime ?? '')))
    return map
  }, [appointments, activeProviders])

  const grid = useMemo(() => buildCalGrid(month), [month])

  // Summary stats
  const monthTotal = useMemo(() => [...byDate.values()].reduce((s, l) => s + l.length, 0), [byDate])
  const todayCount = byDate.get(todayStr)?.length ?? 0

  let busiestDay = '', busiestCount = 0
  byDate.forEach((list, date) => {
    if (list.length > busiestCount) { busiestCount = list.length; busiestDay = date }
  })
  const busiestLabel = busiestDay
    ? new Date(busiestDay + 'T12:00:00').toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
    : ''

  function prevMonth() { setMonth((m) => new Date(m.getFullYear(), m.getMonth() - 1, 1)); setSelectedDate(null) }
  function nextMonth() { setMonth((m) => new Date(m.getFullYear(), m.getMonth() + 1, 1)); setSelectedDate(null) }
  function goToday() {
    const d = new Date()
    setMonth(new Date(d.getFullYear(), d.getMonth(), 1))
    setSelectedDate(todayStr)
  }

  function toggleProvider(name: string) {
    setActiveProviders((prev) => {
      const next = new Set(prev)
      if (next.has(name)) {
        if (next.size === 1) return prev
        next.delete(name)
      } else {
        next.add(name)
      }
      return next
    })
  }

  function selectAll() {
    setActiveProviders(new Set(providers.map((p) => p.name)))
  }

  const monthLabel = month.toLocaleDateString('en-US', { month: 'long', year: 'numeric' })
  const selectedAppts = selectedDate ? (byDate.get(selectedDate) ?? []) : []
  const selectedLabel = selectedDate
    ? new Date(selectedDate + 'T12:00:00').toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric' })
    : ''
  const panelOpen = selectedDate !== null

  return (
    <div className="cal-page">
      {/* ── Header ── */}
      <div className="cal-header">
        <div className="cal-month-nav">
          <button className="cal-nav-btn" type="button" onClick={prevMonth} aria-label="Previous month">
            <ChevronLeft size={16} />
          </button>
          <h1 className="cal-month-title">{monthLabel}</h1>
          <button className="cal-nav-btn" type="button" onClick={nextMonth} aria-label="Next month">
            <ChevronRight size={16} />
          </button>
          <button className="cal-today-btn" type="button" onClick={goToday}>Today</button>
        </div>

        {!loading && !error && (
          <div className="cal-stats-bar">
            <div className="cal-stat">
              <span className="cal-stat-v">{monthTotal}</span>
              <span className="cal-stat-l">this month</span>
            </div>
            <div className="cal-stat-sep" />
            <div className="cal-stat">
              <span className="cal-stat-v">{todayCount}</span>
              <span className="cal-stat-l">today</span>
            </div>
            <div className="cal-stat-sep" />
            <div className="cal-stat">
              <span className="cal-stat-v">{providers.length}</span>
              <span className="cal-stat-l">provider{providers.length !== 1 ? 's' : ''}</span>
            </div>
            {busiestDay && (
              <>
                <div className="cal-stat-sep" />
                <div className="cal-stat">
                  <span className="cal-stat-v">{busiestCount}</span>
                  <span className="cal-stat-l">busiest ({busiestLabel})</span>
                </div>
              </>
            )}
          </div>
        )}
      </div>

      {/* ── Provider filter ── */}
      {providers.length > 0 && (
        <div className="cal-provider-row">
          <span className="cal-provider-label">Providers</span>
          <div className="cal-provider-chips">
            {activeProviders.size < providers.length && (
              <button className="cal-all-btn" type="button" onClick={selectAll}>Show all</button>
            )}
            {providers.map((p) => {
              const active = activeProviders.has(p.name)
              return (
                <button
                  key={p.name}
                  className={`cal-pchip${active ? ' cal-pchip-on' : ' cal-pchip-off'}`}
                  type="button"
                  onClick={() => toggleProvider(p.name)}
                  style={active ? {
                    background: p.color.bg,
                    borderColor: p.color.border,
                    color: p.color.fg,
                  } : undefined}
                  title={active ? `Hide ${p.name}` : `Show ${p.name}`}
                >
                  <span className="cal-pdot" style={{ background: active ? p.color.dot : undefined }} />
                  {p.name}
                  <span className="cal-pcount">{p.count}</span>
                </button>
              )
            })}
          </div>
        </div>
      )}

      {/* ── Main: grid + panel ── */}
      <div className={`cal-layout${panelOpen ? ' cal-layout-open' : ''}`}>
        {/* Grid */}
        <div className="cal-grid-wrap">
          <div className="cal-wdrow">
            {WEEKDAYS.map((d) => <div key={d} className="cal-wdlabel">{d}</div>)}
          </div>

          {loading && (
            <div className="cal-skeleton-grid">
              {Array.from({ length: 35 }).map((_, i) => <div key={i} className="cal-skel-cell" />)}
            </div>
          )}

          {error && <div className="error-banner" style={{ margin: '12px 0' }}>{error}</div>}

          {!loading && !error && (
            <div className="cal-grid">
              {grid.map((day) => {
                const ds = isoDate(day)
                const inMonth = day.getMonth() === month.getMonth()
                const isToday = ds === todayStr
                const isSel = ds === selectedDate
                const isWeekend = day.getDay() === 0 || day.getDay() === 6
                const dayAppts = byDate.get(ds) ?? []
                const heat = heatLevel(dayAppts.length)
                const show = dayAppts.slice(0, 3)
                const overflow = dayAppts.length - show.length

                const cellClass = [
                  'cal-day',
                  !inMonth ? 'cal-day-out' : '',
                  isToday ? 'cal-day-today' : '',
                  isSel ? 'cal-day-sel' : '',
                  isWeekend && inMonth ? 'cal-day-wknd' : '',
                  heat > 0 && inMonth ? `cal-heat-${heat}` : '',
                ].filter(Boolean).join(' ')

                return (
                  <button
                    key={ds}
                    className={cellClass}
                    type="button"
                    onClick={() => setSelectedDate(isSel ? null : ds)}
                    aria-label={`${ds}${dayAppts.length > 0 ? `, ${dayAppts.length} appointments` : ''}`}
                    aria-pressed={isSel}
                  >
                    <div className="cal-day-top">
                      <span className={isToday ? 'cal-daynum-today' : 'cal-daynum'}>
                        {day.getDate()}
                      </span>
                      {dayAppts.length > 0 && inMonth && (
                        <span className="cal-daycnt">{dayAppts.length}</span>
                      )}
                    </div>

                    {inMonth && (
                      <div className="cal-chips">
                        {show.map((appt) => {
                          const prov = appt.providerName ?? 'Unassigned'
                          const c = providerColorMap.get(prov) ?? PALETTE[0]
                          return (
                            <div key={appt.id} className="cal-chip" style={{ background: c.bg, color: c.fg }}>
                              <span className="cal-chip-t">{fmt12(appt.startTime)}</span>
                              <span className="cal-chip-p">{appt.patientDisplayName}</span>
                            </div>
                          )
                        })}
                        {overflow > 0 && (
                          <span className="cal-chip-more">+{overflow} more</span>
                        )}
                      </div>
                    )}
                  </button>
                )
              })}
            </div>
          )}
        </div>

        {/* Day detail panel */}
        {panelOpen && (
          <aside className="cal-panel" aria-label={`Appointments for ${selectedLabel}`}>
            <div className="cal-panel-head">
              <div>
                <p className="cal-panel-date">{selectedLabel}</p>
                <p className="cal-panel-cnt">
                  {selectedAppts.length === 0
                    ? 'No appointments'
                    : `${selectedAppts.length} appointment${selectedAppts.length !== 1 ? 's' : ''}`}
                </p>
              </div>
              <button className="cal-panel-x" type="button" onClick={() => setSelectedDate(null)} aria-label="Close">
                <X size={15} />
              </button>
            </div>

            <div className="cal-panel-body">
              {selectedAppts.length === 0 ? (
                <div className="cal-panel-empty">
                  <Clock size={36} aria-hidden="true" />
                  <p>No appointments scheduled.</p>
                </div>
              ) : (
                <ul className="cal-panel-list">
                  {selectedAppts.map((appt) => {
                    const prov = appt.providerName ?? 'Unassigned'
                    const c = providerColorMap.get(prov) ?? PALETTE[0]
                    const cancelled = appt.status?.toLowerCase().includes('cancel')
                    const completed = appt.status?.toLowerCase().includes('complet') || appt.status?.toLowerCase().includes('check')
                    return (
                      <li key={appt.id} className="cal-pappt" style={{ borderLeftColor: c.dot }}>
                        <p className="cal-pappt-time">{fmt12(appt.startTime)}</p>
                        <button
                          className="cal-pappt-name"
                          type="button"
                          onClick={() => navigate(`/clinician/patients/${appt.patientId}/summary`)}
                        >
                          {appt.patientDisplayName}
                        </button>
                        <p className="cal-pappt-type">{appt.title}</p>
                        <div className="cal-pappt-row">
                          <span className="cal-pbadge" style={{ background: c.bg, color: c.fg }}>
                            <span className="cal-pdot" style={{ background: c.dot }} />
                            {prov}
                          </span>
                          {appt.status && (
                            <span className={`cl-badge ${cancelled ? 'cl-badge-coral' : completed ? 'cl-badge-green' : 'cl-badge-muted'}`}>
                              {appt.status}
                            </span>
                          )}
                        </div>
                        {appt.room && <p className="cal-pappt-room">Room {appt.room}</p>}
                      </li>
                    )
                  })}
                </ul>
              )}
            </div>

            {selectedAppts.length > 0 && (
              <div className="cal-panel-foot">
                <button
                  className="cl-btn-secondary"
                  type="button"
                  onClick={() => {
                    if (selectedDate) navigate(`/clinician/schedule?date=${selectedDate}`)
                  }}
                  style={{ width: '100%', justifyContent: 'center' }}
                >
                  Open in schedule view
                </button>
              </div>
            )}
          </aside>
        )}
      </div>
    </div>
  )
}
