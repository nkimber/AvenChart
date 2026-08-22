// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'

let pendingSkipTargetObserver: MutationObserver | undefined

function focusMainContentAfterSkip() {
  const focusTarget = () => {
    const mainContent = document.getElementById('main-content')
    if (!mainContent) return false
    mainContent.focus()
    return true
  }

  if (focusTarget()) return

  pendingSkipTargetObserver?.disconnect()
  pendingSkipTargetObserver = new MutationObserver(() => {
    if (!focusTarget()) return
    pendingSkipTargetObserver?.disconnect()
    pendingSkipTargetObserver = undefined
  })
  pendingSkipTargetObserver.observe(document.body, { childList: true, subtree: true })
}

document.addEventListener('click', (event) => {
  if (!(event.target instanceof Element)) return
  if (!event.target.closest('a[href="#main-content"]')) return
  focusMainContentAfterSkip()
})

window.addEventListener('hashchange', () => {
  if (window.location.hash === '#main-content') focusMainContentAfterSkip()
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
