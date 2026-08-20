// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

const historyData = window.AVENCHART_HISTORY
const number = new Intl.NumberFormat('en-US')
const shortDate = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', year: 'numeric', timeZone: 'UTC' })
const axisDate = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', timeZone: 'UTC' })
const pageSize = 50
const defaultView = 'introduction'
let visibleCount = pageSize
let filteredCommits = historyData.commits

const byId = (id) => document.getElementById(id)
const formatDate = (value) => shortDate.format(new Date(`${value}T00:00:00Z`))

function populateSummary() {
  const { phase, summary } = historyData
  byId('hero-commit-count').textContent = number.format(summary.commits)
  byId('hero-date-range').textContent = `${formatDate(summary.firstDate)} — ${formatDate(summary.lastDate)}`
  byId('active-days').textContent = number.format(summary.activeDays)
  byId('total-additions').textContent = number.format(summary.additions)
  byId('total-deletions').textContent = number.format(summary.deletions)
  byId('net-source-growth').textContent = number.format(summary.net)

  if (phase) {
    byId('phase-one-coverage').textContent = number.format(phase.functionalCoverageEstimate)
    byId('phase-one-closure-date').textContent = formatDate(phase.closedOn)
    byId('phase-one-closure-date').dateTime = phase.closedOn
    byId('phase-one-snapshot-ref').textContent = phase.snapshotRef
    byId('phase-one-revision').textContent = historyData.sourceRevision.slice(0, 12)
    byId('phase-one-revision-link').href = `${historyData.repositoryUrl}/commit/${historyData.sourceRevision}`
  }
}

function activateWorkbenchView(requestedView, { focus = false } = {}) {
  const panel = document.querySelector(`[data-workbench-panel="${CSS.escape(requestedView)}"]`)
  const view = panel ? requestedView : defaultView

  document.querySelectorAll('[data-workbench-panel]').forEach((candidate) => {
    candidate.hidden = candidate.dataset.workbenchPanel !== view
  })

  document.querySelectorAll('[data-workbench-view]').forEach((link) => {
    const isCurrent = link.dataset.workbenchView === view
    link.classList.toggle('is-current', isCurrent)
    if (isCurrent) link.setAttribute('aria-current', 'page')
    else link.removeAttribute('aria-current')
  })

  const activePanel = document.querySelector(`[data-workbench-panel="${CSS.escape(view)}"]`)
  if (focus) activePanel?.focus({ preventScroll: true })
  document.title = view === defaultView
    ? 'AvenChart · Program workbench'
    : `${activePanel?.querySelector('h1')?.textContent ?? 'Phase 1'} · AvenChart workbench`
}

function initializeWorkbenchNavigation() {
  const viewFromHash = window.location.hash.slice(1)
  activateWorkbenchView(viewFromHash || defaultView)

  document.querySelectorAll('[data-workbench-view]').forEach((link) => {
    link.addEventListener('click', (event) => {
      event.preventDefault()
      const view = link.dataset.workbenchView
      if (!view) return
      if (window.location.hash !== `#${view}`) window.history.pushState(null, '', `#${view}`)
      activateWorkbenchView(view, { focus: true })
      window.scrollTo({ top: byId('workbench-navigation').offsetTop - 16, behavior: 'smooth' })
    })
  })

  window.addEventListener('popstate', () => {
    activateWorkbenchView(window.location.hash.slice(1) || defaultView)
  })
}

function renderPulse() {
  const svg = byId('pulse-chart')
  const tooltip = byId('pulse-tooltip')
  const chronological = historyData.commits.toReversed()
  const width = 1120
  const height = 260
  const insetX = 8
  const insetY = 22
  const plotWidth = width - insetX * 2
  const millisecondsPerDay = 24 * 60 * 60 * 1000
  const firstDay = new Date(`${historyData.summary.firstDate}T00:00:00Z`).getTime()
  const lastDay = new Date(`${historyData.summary.lastDate}T00:00:00Z`).getTime()
  const calendarDays = []

  for (let timestamp = firstDay; timestamp <= lastDay; timestamp += millisecondsPerDay) {
    calendarDays.push(new Date(timestamp).toISOString().slice(0, 10))
  }

  const commitsByDay = new Map(calendarDays.map((date) => [date, []]))
  chronological.forEach((commit, index) => commitsByDay.get(commit.date)?.push({ commit, index }))

  let cumulativeNet = 0
  const dailySeries = calendarDays.map((date) => {
    const commits = commitsByDay.get(date)
    if (commits.length) cumulativeNet = commits.at(-1).commit.cumulativeNet
    return { date, commits, cumulativeNet }
  })

  const values = [0, ...dailySeries.map((day) => day.cumulativeNet)]
  const minimum = Math.min(...values)
  const maximum = Math.max(...values)
  const range = Math.max(1, maximum - minimum)
  const dayWidth = plotWidth / Math.max(1, dailySeries.length)
  const xAtDayStart = (index) => insetX + index * dayWidth
  const xAtDayEnd = (index) => insetX + (index + 1) * dayWidth
  const yAt = (value) => height - insetY - ((value - minimum) / range) * (height - insetY * 2)
  const points = [
    `${xAtDayStart(0).toFixed(2)},${yAt(0).toFixed(2)}`,
    ...dailySeries.map((day, index) => `${xAtDayEnd(index).toFixed(2)},${yAt(day.cumulativeNet).toFixed(2)}`),
  ].join(' ')

  const tickCount = Math.min(5, dailySeries.length)
  const tickIndexes = Array.from({ length: tickCount }, (_, index) => (
    tickCount === 1 ? 0 : Math.round((index / (tickCount - 1)) * (dailySeries.length - 1))
  ))

  svg.setAttribute('viewBox', `0 0 ${width} ${height}`)
  svg.innerHTML = `
    <title id="pulse-title">Source growth of the autonomous AvenChart rewrite</title>
    <desc id="pulse-description">${number.format(chronological.length)} retained source check-ins across ${number.format(dailySeries.length)} consecutive calendar days, from ${formatDate(historyData.summary.firstDate)} to ${formatDate(historyData.summary.lastDate)}. Each vertical mark is a check-in made while the autonomous engineering agent builds or refines a functional slice. Periods without check-ins remain visible as flat, unmarked spans. The line shows cumulative lines added minus lines removed, not feature-completion progress.</desc>
    <defs>
      <linearGradient id="pulse-fill" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stop-color="#29a6a0" stop-opacity="0.35" />
        <stop offset="1" stop-color="#29a6a0" stop-opacity="0" />
      </linearGradient>
    </defs>
    <polygon points="${insetX},${height - insetY} ${points} ${width - insetX},${height - insetY}" fill="url(#pulse-fill)" />
    <polyline points="${points}" fill="none" stroke="#51c7bf" stroke-width="2.2" stroke-linejoin="round" />
    <g id="pulse-marks"></g>
  `

  const marks = svg.querySelector('#pulse-marks')
  dailySeries.forEach((day, dayIndex) => {
    day.commits.forEach(({ commit, index }, commitIndex) => {
      const line = document.createElementNS('http://www.w3.org/2000/svg', 'line')
      const x = xAtDayStart(dayIndex) + dayWidth * ((commitIndex + 1) / (day.commits.length + 1))
      const intensity = Math.min(18, 3 + Math.log2(commit.additions + commit.deletions + 1) * 1.8)
      line.setAttribute('x1', x)
      line.setAttribute('x2', x)
      line.setAttribute('y1', height - insetY)
      line.setAttribute('y2', height - insetY - intensity)
      line.setAttribute('stroke', commit.deletions > commit.additions ? '#e05f4f' : '#d7e5e1')
      line.setAttribute('stroke-opacity', '0.48')
      line.setAttribute('stroke-width', '1.15')
      line.dataset.index = index
      marks.append(line)
    })
  })

  const showTooltip = (event) => {
    const index = Number(event.target.dataset.index)
    if (!Number.isInteger(index)) return
    const commit = chronological[index]
    tooltip.innerHTML = `<strong>${commit.shortHash}</strong><br>${escapeHtml(commit.subject)}<br><span>${formatDate(commit.date)} · +${number.format(commit.additions)} / −${number.format(commit.deletions)}</span>`
    tooltip.hidden = false
    const bounds = svg.getBoundingClientRect()
    tooltip.style.left = `${Math.min(bounds.width - 280, Math.max(0, event.clientX - bounds.left + 12))}px`
    tooltip.style.top = `${Math.max(0, event.clientY - bounds.top - 82)}px`
  }

  marks.addEventListener('pointermove', showTooltip)
  marks.addEventListener('pointerleave', () => { tooltip.hidden = true })

  byId('date-axis').innerHTML = tickIndexes
    .map((index) => `<span>${axisDate.format(new Date(`${dailySeries[index].date}T00:00:00Z`))}</span>`)
    .join('')
}

function renderActivity() {
  const peak = Math.max(...historyData.monthly.map((month) => month.commits), 1)
  byId('monthly-bars').innerHTML = historyData.monthly
    .map(({ month, commits }) => `
      <div class="month-bar" title="${commits} retained check-ins in ${month}">
        <span class="month-bar-fill" style="height:${Math.max(2, (commits / peak) * 100)}%"></span>
        <strong>${number.format(commits)}</strong>
        <span>${month}</span>
      </div>
    `)
    .join('')

  byId('area-ledger').innerHTML = historyData.areaTotals
    .map(({ area, count }) => `<div class="area-row"><span>${area}</span><span>${number.format(count)}</span></div>`)
    .join('')

  const areaFilter = byId('area-filter')
  historyData.areaTotals
    .map(({ area }) => area)
    .sort()
    .forEach((area) => areaFilter.add(new Option(area, area)))
}

function renderCommits() {
  const visible = filteredCommits.slice(0, visibleCount)
  byId('commit-list').innerHTML = visible
    .map((commit) => `
      <li class="commit-row">
        <a class="commit-hash" href="${historyData.repositoryUrl}/commit/${commit.hash}" aria-label="Open commit ${commit.shortHash} on GitHub">${commit.shortHash}</a>
        <div>
          <p class="commit-subject">${escapeHtml(commit.subject)}</p>
          <div class="commit-areas">${commit.areas.map((area) => `<span class="area-pill">${escapeHtml(area)}</span>`).join('')}</div>
        </div>
        <time class="commit-date" datetime="${commit.date}">${formatDate(commit.date)}</time>
        <span class="commit-delta"><span class="plus">+${number.format(commit.additions)}</span> / <span class="minus">−${number.format(commit.deletions)}</span></span>
      </li>
    `)
    .join('')

  byId('history-result-count').textContent = `${number.format(filteredCommits.length)} matching check-ins`
  byId('load-more').hidden = visibleCount >= filteredCommits.length
}

function escapeHtml(value) {
  const span = document.createElement('span')
  span.textContent = value
  return span.innerHTML
}

function applyFilters() {
  const query = byId('history-search').value.trim().toLocaleLowerCase()
  const area = byId('area-filter').value
  filteredCommits = historyData.commits.filter((commit) => {
    const matchesQuery = !query || commit.subject.toLocaleLowerCase().includes(query) || commit.shortHash.includes(query)
    const matchesArea = !area || commit.areas.includes(area)
    return matchesQuery && matchesArea
  })
  visibleCount = pageSize
  renderCommits()
}

function initialize() {
  initializeWorkbenchNavigation()
  populateSummary()
  renderPulse()
  renderActivity()
  renderCommits()
  byId('history-search').addEventListener('input', applyFilters)
  byId('area-filter').addEventListener('change', applyFilters)
  byId('clear-filters').addEventListener('click', () => {
    byId('history-search').value = ''
    byId('area-filter').value = ''
    applyFilters()
    byId('history-search').focus()
  })
  byId('load-more').addEventListener('click', () => {
    visibleCount += pageSize
    renderCommits()
  })
}

initialize()
