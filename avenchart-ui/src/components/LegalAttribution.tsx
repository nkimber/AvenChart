// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

const projectLicenseUrl = '/LICENSE.txt'
const projectSourceUrl = 'https://github.com/nkimber/Legacy EHR-Legacy'
const openEmrHomepageUrl = 'https://www.open-emr.org/'
const openEmrSourceUrl = 'https://github.com/legacy-ehr/legacy-ehr'

export default function LegalAttribution() {
  return (
    <aside className="legal-attribution" aria-label="Open source license and original project attribution">
      <p className="legal-attribution-label">Open source &amp; original project</p>
      <p>
        This independent modernization experiment is licensed under the GNU GPL v3 or later and was developed with
        reference to the original Legacy EHR project. It is not affiliated with or endorsed by the Legacy EHR Foundation.
      </p>
      <nav className="legal-attribution-links" aria-label="License and original Legacy EHR links">
        <a href={projectLicenseUrl} target="_blank" rel="noreferrer">
          Software license
        </a>
        <a href={projectSourceUrl} target="_blank" rel="noreferrer">
          Modernized source
        </a>
        <a href={openEmrHomepageUrl} target="_blank" rel="noreferrer">
          Original Legacy EHR project
        </a>
        <a href={openEmrSourceUrl} target="_blank" rel="noreferrer">
          Original source code
        </a>
      </nav>
    </aside>
  )
}
