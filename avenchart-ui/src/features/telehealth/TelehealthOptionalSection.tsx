// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useState, type ReactNode } from 'react'

/** Load optional work on demand, then retain any typed draft when collapsed. */
export default function TelehealthOptionalSection({ title, children }: { title: string; children: ReactNode }) {
  const [visited, setVisited] = useState(false)
  return <details className="telehealth-optional" onToggle={(event) => { if (event.currentTarget.open) setVisited(true) }}>
    <summary>{title}</summary>
    {visited ? children : null}
  </details>
}
