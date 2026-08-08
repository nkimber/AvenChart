// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

const projectLicenseUrl = '/LICENSE.txt'
const projectSourceUrl = 'https://github.com/nkimber/AvenChart'
const upstreamHomepageUrl = 'https://www.open-emr.org/'
const upstreamSourceUrl = 'https://github.com/legacy-ehr/legacy-ehr'
const upstreamCommunityUrl = 'https://community.open-emr.org/'

export default function LegalAttribution() {
  return (
    <aside className="legal-attribution" aria-label="Open source license and original project attribution">
      <p className="legal-attribution-label">Open source &amp; original project</p>
      <p>
        AvenChart is licensed under the GNU GPL v3 or later and was developed with reference to the original Legacy EHR
        project. We gratefully thank its maintainers, contributors, clinicians, implementers, and support community.
        The Legacy EHR name identifies that upstream source only; AvenChart is independent and is not affiliated with,
        certified by, or endorsed by the Legacy EHR Foundation or community.
      </p>
      <nav className="legal-attribution-links" aria-label="License and original Legacy EHR links">
        <a href={projectLicenseUrl} target="_blank" rel="noreferrer">
          Software license
        </a>
        <a href={projectSourceUrl} target="_blank" rel="noreferrer">
          AvenChart source
        </a>
        <a href={upstreamHomepageUrl} target="_blank" rel="noreferrer">
          Original Legacy EHR project
        </a>
        <a href={upstreamSourceUrl} target="_blank" rel="noreferrer">
          Original source code
        </a>
        <a href={upstreamCommunityUrl} target="_blank" rel="noreferrer">
          Legacy EHR community
        </a>
      </nav>
    </aside>
  )
}
