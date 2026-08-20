// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { execFileSync } from 'node:child_process'
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const repositoryRoot = resolve(here, '..')
const outputPath = resolve(repositoryRoot, 'public-history', 'history-data.js')
const historyBasePath = resolve(repositoryRoot, '.public-history-base')
if (!existsSync(historyBasePath)) {
  throw new Error('The fixed Phase 1 history boundary is missing: .public-history-base')
}

const historyRef = readFileSync(historyBasePath, 'utf8').trim()
if (!/^[0-9a-f]{40}$/.test(historyRef)) {
  throw new Error('.public-history-base must contain one exact 40-character Git revision')
}

const sourceRevision = execFileSync('git', ['rev-parse', `${historyRef}^{commit}`], {
  cwd: repositoryRoot,
  encoding: 'utf8',
}).trim()

if (sourceRevision !== historyRef) {
  throw new Error(`Phase 1 history boundary did not resolve exactly: expected ${historyRef}, received ${sourceRevision}`)
}

const recordSeparator = '\x1e'
const fieldSeparator = '\x1f'

const log = execFileSync(
  'git',
  [
    'log',
    '--reverse',
    '--date=short',
    `--format=${recordSeparator}%H${fieldSeparator}%ad${fieldSeparator}%an${fieldSeparator}%s`,
    '--numstat',
    '--no-renames',
    historyRef,
  ],
  { cwd: repositoryRoot, encoding: 'utf8', maxBuffer: 128 * 1024 * 1024 },
)

function classify(paths) {
  const areas = new Set()

  for (const path of paths) {
    if (path.startsWith('avenchart-ui/')) areas.add('AvenChart UI')
    if (path.startsWith('avenchart/frontend/')) areas.add('Reference UI')
    if (path.includes('/backend/')) areas.add('Backend API')
    if (path.includes('/database/')) areas.add('Database')
    if (/(^|\/)(e2e|scripts|tests?)(\/|$)|\.test\.|\.spec\./i.test(path)) areas.add('Verification')
    if (!path.includes('/backend/') && !path.includes('/frontend/') && !path.includes('/database/')) {
      if (path.startsWith('avenchart/')) areas.add('Runtime')
    }
  }

  return [...areas].sort()
}

const commits = log
  .split(recordSeparator)
  .map((record) => record.trim())
  .filter(Boolean)
  .map((record) => {
    const [header, ...lines] = record.split(/\r?\n/)
    const [hash, date, author, ...subjectParts] = header.split(fieldSeparator)
    const paths = []
    let additions = 0
    let deletions = 0

    for (const line of lines) {
      const match = /^(\d+|-)\t(\d+|-)\t(.+)$/.exec(line)
      if (!match) continue
      additions += match[1] === '-' ? 0 : Number(match[1])
      deletions += match[2] === '-' ? 0 : Number(match[2])
      paths.push(match[3])
    }

    return {
      hash,
      shortHash: hash.slice(0, 8),
      date,
      author,
      subject: subjectParts.join(fieldSeparator),
      additions,
      deletions,
      files: paths.length,
      areas: classify(paths),
    }
  })

const monthly = new Map()
const areaTotals = new Map()
let totalAdditions = 0
let totalDeletions = 0
let cumulativeAdditions = 0
let cumulativeDeletions = 0

for (const commit of commits) {
  totalAdditions += commit.additions
  totalDeletions += commit.deletions
  cumulativeAdditions += commit.additions
  cumulativeDeletions += commit.deletions
  commit.cumulativeNet = cumulativeAdditions - cumulativeDeletions

  const month = commit.date.slice(0, 7)
  const monthlyEntry = monthly.get(month) ?? { month, commits: 0, additions: 0, deletions: 0 }
  monthlyEntry.commits += 1
  monthlyEntry.additions += commit.additions
  monthlyEntry.deletions += commit.deletions
  monthly.set(month, monthlyEntry)

  for (const area of commit.areas) {
    areaTotals.set(area, (areaTotals.get(area) ?? 0) + 1)
  }
}

const firstDate = commits[0]?.date ?? null
const lastDate = commits.at(-1)?.date ?? null
const activeDays = firstDate && lastDate
  ? Math.max(1, Math.round((Date.parse(`${lastDate}T00:00:00Z`) - Date.parse(`${firstDate}T00:00:00Z`)) / 86_400_000) + 1)
  : 0

const payload = {
  generatedAt: execFileSync('git', ['show', '-s', '--format=%cI', historyRef], { cwd: repositoryRoot, encoding: 'utf8' }).trim(),
  sourceRevision,
  repositoryUrl: 'https://github.com/nkimber/AvenChart',
  phase: {
    id: 'phase-1',
    number: 1,
    name: 'Experimental autonomous build',
    status: 'closed',
    closedOn: '2026-08-20',
    snapshotRef: 'phase-1-experimental',
    functionalCoverageEstimate: 86,
    functionalCoverageQualifier: 'approximately',
    immutable: true,
  },
  summary: {
    commits: commits.length,
    firstDate,
    lastDate,
    activeDays,
    additions: totalAdditions,
    deletions: totalDeletions,
    net: totalAdditions - totalDeletions,
    authors: [...new Set(commits.map((commit) => commit.author))],
  },
  areaTotals: [...areaTotals.entries()]
    .map(([area, count]) => ({ area, count }))
    .sort((left, right) => right.count - left.count),
  monthly: [...monthly.values()],
  commits: commits.toReversed(),
}

const serialized = JSON.stringify(payload).replaceAll('<', '\\u003c')
mkdirSync(dirname(outputPath), { recursive: true })
writeFileSync(outputPath, `window.AVENCHART_HISTORY = ${serialized};\n`, 'utf8')
console.log(`Wrote ${commits.length} retained commits to ${outputPath}`)
