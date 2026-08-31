// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { readFile, readdir, stat, writeFile } from 'node:fs/promises'
import { basename } from 'node:path'

const distRoot = new URL('../dist/', import.meta.url)
const assetsRoot = new URL('./assets/', distRoot)
const initialBudgetBytes = 250 * 1024
const routeChunkBudgetBytes = 300 * 1024
// The ACS Calling SDK is deliberately lazy-loaded only after an authorized
// synthetic waiting-room grant exists. Its browser media engine is materially
// larger than ordinary route code, so constrain it separately rather than
// silently weakening the budget for every route.
const optionalCallingSdkBudgetBytes = 7 * 1024 * 1024
const optionalCallingSdkChunkPattern = /^TelehealthInternetCallingPocPanel-/

const indexHtml = await readFile(new URL('./index.html', distRoot), 'utf8')
const initialMatch = indexHtml.match(/<script[^>]+src="\/assets\/([^"]+\.js)"/)
if (!initialMatch) {
  throw new Error('Could not identify the initial JavaScript chunk in dist/index.html.')
}

const files = (await readdir(assetsRoot)).filter((file) => file.endsWith('.js'))
const chunks = await Promise.all(
  files.map(async (file) => ({
    file,
    bytes: (await stat(new URL(file, assetsRoot))).size,
  })),
)

const initial = chunks.find((chunk) => chunk.file === basename(initialMatch[1]))
if (!initial) {
  throw new Error(`Initial JavaScript chunk ${initialMatch[1]} was not emitted.`)
}

const violations = chunks.filter((chunk) =>
  chunk.file === initial.file
    ? chunk.bytes > initialBudgetBytes
    : chunk.bytes > (optionalCallingSdkChunkPattern.test(chunk.file) ? optionalCallingSdkBudgetBytes : routeChunkBudgetBytes),
)

const result = {
  generatedAt: new Date().toISOString(),
  budgets: {
    initialBytes: initialBudgetBytes,
    routeChunkBytes: routeChunkBudgetBytes,
    optionalCallingSdkBytes: optionalCallingSdkBudgetBytes,
  },
  initial,
  largestChunks: [...chunks].sort((a, b) => b.bytes - a.bytes).slice(0, 10),
  violations,
}

await writeFile(
  new URL('./bundle-budget.json', distRoot),
  `${JSON.stringify(result, null, 2)}\n`,
  'utf8',
)

if (violations.length) {
  throw new Error(
    `Bundle budget exceeded: ${violations.map((chunk) => `${chunk.file} (${chunk.bytes} bytes)`).join(', ')}`,
  )
}

console.log(
  `Bundle budget passed: initial ${initial.bytes}/${initialBudgetBytes} bytes; ${chunks.length} JavaScript chunks checked.`,
)
