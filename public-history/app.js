// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

const historyData = window.AVENCHART_HISTORY
const number = new Intl.NumberFormat('en-US')
const shortDate = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', year: 'numeric', timeZone: 'UTC' })
const pageSize = 50
let visibleCount = pageSize
let filteredCommits = historyData.commits

const byId = (id) => document.getElementById(id)
const formatDate = (value) => shortDate.format(new Date(`${value}T00:00:00Z`))

function populateSummary() {
  const { summary } = historyData
  byId('hero-commit-count').textContent = number.format(summary.commits)
  byId('hero-date-range').textContent = `${formatDate(summary.firstDate)} — ${formatDate(summary.lastDate)}`
  byId('active-days').textContent = number.format(summary.activeDays)
  byId('total-additions').textContent = number.format(summary.additions)
  byId('total-deletions').textContent = number.format(summary.deletions)
  byId('author-count').textContent = number.format(summary.authors.length)
}

function renderPulse() {
  const svg = byId('pulse-chart')
  const tooltip = byId('pulse-tooltip')
  const chronological = historyData.commits.toReversed()
  const width = 1120
  const height = 260
  const insetX = 8
  const insetY = 22
  const values = chronological.map((commit) => commit.cumulativeNet)
  const minimum = Math.min(...values)
  const maximum = Math.max(...values)
  const range = Math.max(1, maximum - minimum)
  const xAt = (index) => insetX + (index / Math.max(1, chronological.length - 1)) * (width - insetX * 2)
  const yAt = (value) => height - insetY - ((value - minimum) / range) * (height - insetY * 2)
  const points = chronological.map((commit, index) => `${xAt(index).toFixed(2)},${yAt(commit.cumulativeNet).toFixed(2)}`).join(' ')

  svg.setAttribute('viewBox', `0 0 ${width} ${height}`)
  svg.innerHTML = `
    <title id="pulse-title">AvenChart retained source activity</title>
    <desc id="pulse-description">${number.format(chronological.length)} check-ins from ${formatDate(historyData.summary.firstDate)} to ${formatDate(historyData.summary.lastDate)}. The line shows cumulative net source change.</desc>
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
  chronological.forEach((commit, index) => {
    const line = document.createElementNS('http://www.w3.org/2000/svg', 'line')
    const x = xAt(index)
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

  byId('month-axis').innerHTML = historyData.monthly
    .map(({ month }) => `<span>${new Date(`${month}-01T00:00:00Z`).toLocaleDateString('en-US', { month: 'short', year: 'numeric', timeZone: 'UTC' })}</span>`)
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
