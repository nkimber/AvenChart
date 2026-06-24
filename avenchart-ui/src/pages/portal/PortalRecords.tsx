import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import {
  ChevronDown,
  ChevronRight,
  Download,
  File,
  FileImage,
  FileText,
  FlaskConical,
  Heart,
} from 'lucide-react'
import {
  downloadPatientPortalDocuments,
  downloadPatientPortalGeneratedMedicalReportPdf,
  getPatientPortalClinicalSummary,
  getPatientPortalDocuments,
  getPatientPortalLabResults,
  type PatientPortalClinicalSummaryResponse,
  type PatientPortalDocumentItem,
  type PatientPortalDocumentsResponse,
  type PatientPortalLabOrderItem,
  type PatientPortalLabResultsResponse,
} from '../../api.ts'
import type { PortalOutletContext } from './PortalShell.tsx'

type AsyncState<T> =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; message: string }

type RecordsTab = 'documents' | 'lab' | 'health' | 'report'

const TABS: { key: RecordsTab; label: string; icon: typeof FileText }[] = [
  { key: 'documents', label: 'Documents', icon: FileText },
  { key: 'lab', label: 'Lab results', icon: FlaskConical },
  { key: 'health', label: 'Health summary', icon: Heart },
  { key: 'report', label: 'Medical report', icon: Download },
]

function formatBytes(value?: number | null) {
  if (!value || value <= 0) return ''
  if (value < 1024) return `${value} B`
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`
  return `${(value / (1024 * 1024)).toFixed(1)} MB`
}

function triggerBlobDownload(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  URL.revokeObjectURL(url)
}

function abnormalClass(flag?: string | null) {
  if (!flag) return ''
  const f = flag.toUpperCase()
  if (f === 'H' || f === 'HH') return 'lab-result-high'
  if (f === 'L' || f === 'LL') return 'lab-result-low'
  if (f === 'A' || f === 'AA') return 'lab-result-abnormal'
  return ''
}

function docIcon(name: string) {
  const ext = name.split('.').pop()?.toLowerCase() ?? ''
  if (ext === 'pdf') return <FileText size={16} />
  if (['jpg', 'jpeg', 'png', 'gif', 'webp', 'tiff', 'bmp'].includes(ext)) return <FileImage size={16} />
  return <File size={16} />
}

function docIconClass(name: string) {
  const ext = name.split('.').pop()?.toLowerCase() ?? ''
  if (ext === 'pdf') return 'doc-icon-wrap doc-icon-pdf'
  if (['jpg', 'jpeg', 'png', 'gif', 'webp'].includes(ext)) return 'doc-icon-wrap doc-icon-image'
  return 'doc-icon-wrap'
}

function LabOrder({ order }: { order: PatientPortalLabOrderItem }) {
  const [expanded, setExpanded] = useState(false)
  return (
    <li className="lab-order">
      <button className="lab-order-header" type="button" onClick={() => setExpanded((e) => !e)}>
        <div className="lab-order-info">
          <p className="lab-order-name">{order.procedureName}</p>
          <p className="lab-order-meta">
            Ordered {order.orderDate}
            {order.orderStatus ? ` · ${order.orderStatus}` : ''}
            {` · ${order.resultCount} result${order.resultCount === 1 ? '' : 's'}`}
          </p>
        </div>
        {expanded ? (
          <ChevronDown size={16} className="lab-chevron" />
        ) : (
          <ChevronRight size={16} className="lab-chevron" />
        )}
      </button>

      {expanded && (
        <div className="lab-order-body">
          {order.reports.length === 0 ? (
            <p className="muted" style={{ padding: '10px 16px', fontSize: 13 }}>
              No reports filed for this order.
            </p>
          ) : (
            order.reports.map((report) => (
              <div key={report.id} className="lab-report">
                <div className="lab-report-header">
                  <span className="lab-report-label">
                    {report.dateCollected ? `Collected ${report.dateCollected}` : 'Report'}
                  </span>
                  {report.reportStatus && <span className="badge-new">{report.reportStatus}</span>}
                </div>
                {report.results.length > 0 && (
                  <table className="lab-result-table">
                    <thead>
                      <tr>
                        <th>Test</th>
                        <th>Value</th>
                        <th>Range</th>
                        <th>Flag</th>
                      </tr>
                    </thead>
                    <tbody>
                      {report.results.map((result) => (
                        <tr
                          key={result.id}
                          className={abnormalClass(result.abnormal) ? 'lab-result-row-flagged' : ''}
                        >
                          <td className="lab-result-name">{result.resultName}</td>
                          <td className="lab-result-value">
                            {result.value ?? '—'}
                            {result.units ? (
                              <span className="lab-result-units"> {result.units}</span>
                            ) : null}
                          </td>
                          <td className="lab-result-range">{result.range ?? '—'}</td>
                          <td>
                            {result.abnormal ? (
                              <span className={`lab-result-flag ${abnormalClass(result.abnormal)}`}>
                                {result.abnormal}
                              </span>
                            ) : null}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            ))
          )}
        </div>
      )}
    </li>
  )
}

export default function PortalRecords() {
  const { session } = useOutletContext<PortalOutletContext>()
  const [activeTab, setActiveTab] = useState<RecordsTab>('documents')

  const [docsState, setDocsState] = useState<AsyncState<PatientPortalDocumentsResponse>>({
    status: 'idle',
  })
  const [downloadingId, setDownloadingId] = useState<number | null>(null)
  const [downloadError, setDownloadError] = useState<string | null>(null)

  const [labState, setLabState] = useState<AsyncState<PatientPortalLabResultsResponse>>({
    status: 'idle',
  })
  const [healthState, setHealthState] = useState<AsyncState<PatientPortalClinicalSummaryResponse>>(
    { status: 'idle' },
  )

  const [reportDownloading, setReportDownloading] = useState(false)
  const [reportError, setReportError] = useState<string | null>(null)

  // Prefetch all three data tabs in parallel on mount (#10)
  useEffect(() => {
    loadDocs()
    loadLab()
    loadHealth()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  function loadDocs() {
    setDocsState({ status: 'loading' })
    getPatientPortalDocuments(session.sessionId)
      .then((data) => setDocsState({ status: 'ready', data }))
      .catch((err) =>
        setDocsState({
          status: 'error',
          message: err instanceof Error ? err.message : 'Could not load documents.',
        }),
      )
  }

  function loadLab() {
    setLabState({ status: 'loading' })
    getPatientPortalLabResults(session.sessionId)
      .then((data) => setLabState({ status: 'ready', data }))
      .catch((err) =>
        setLabState({
          status: 'error',
          message: err instanceof Error ? err.message : 'Could not load lab results.',
        }),
      )
  }

  function loadHealth() {
    setHealthState({ status: 'loading' })
    getPatientPortalClinicalSummary(session.sessionId)
      .then((data) => setHealthState({ status: 'ready', data }))
      .catch((err) =>
        setHealthState({
          status: 'error',
          message: err instanceof Error ? err.message : 'Could not load health summary.',
        }),
      )
  }

  function handleDownloadDoc(doc: PatientPortalDocumentItem) {
    setDownloadError(null)
    setDownloadingId(doc.id)
    downloadPatientPortalDocuments(session.sessionId, { documentIds: [doc.id] })
      .then((blob) => triggerBlobDownload(blob, doc.name))
      .catch((err) =>
        setDownloadError(
          err instanceof Error ? err.message : 'Could not download that document.',
        ),
      )
      .finally(() => setDownloadingId(null))
  }

  function handleDownloadReport() {
    setReportDownloading(true)
    setReportError(null)
    downloadPatientPortalGeneratedMedicalReportPdf(session.sessionId)
      .then((blob) => triggerBlobDownload(blob, `medical-report-${session.portalUsername}.pdf`))
      .catch((err) =>
        setReportError(err instanceof Error ? err.message : 'Could not generate the report.'),
      )
      .finally(() => setReportDownloading(false))
  }

  return (
    <div className="portal-page">
      <nav className="records-tab-nav">
        {TABS.map((tab) => {
          const Icon = tab.icon
          return (
            <button
              key={tab.key}
              className={`records-tab${activeTab === tab.key ? ' records-tab-active' : ''}`}
              type="button"
              onClick={() => setActiveTab(tab.key)}
            >
              <Icon size={15} />
              {tab.label}
            </button>
          )
        })}
      </nav>

      {/* Documents */}
      {activeTab === 'documents' && (
        <section className="portal-section">
          <h2 className="portal-section-title" style={{ marginBottom: 16 }}>Documents</h2>
          {downloadError && <div className="error-banner">{downloadError}</div>}
          {docsState.status === 'loading' && (
            <div className="skeleton-list">
              {[0, 1, 2].map((i) => <div key={i} className="skeleton-row" />)}
            </div>
          )}
          {docsState.status === 'error' && (
            <div className="error-banner">{docsState.message}</div>
          )}
          {docsState.status === 'ready' &&
            (docsState.data.documents.length === 0 ? (
              <div className="empty-state">
                <div className="empty-state-icon-wrap">
                  <FileText size={28} />
                </div>
                <p className="empty-state-text">No documents on file.</p>
              </div>
            ) : (
              <ul className="panel-list">
                {docsState.data.documents.map((doc) => (
                  <li className="panel-row" key={doc.id}>
                    <div className={docIconClass(doc.name)}>{docIcon(doc.name)}</div>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <p className="panel-row-title">{doc.name}</p>
                      <p className="panel-row-meta">
                        {doc.categoryName} · {doc.docDate}
                        {formatBytes(doc.sizeBytes) ? ` · ${formatBytes(doc.sizeBytes)}` : ''}
                      </p>
                    </div>
                    {doc.canDownload ? (
                      <button
                        className="link-button"
                        type="button"
                        onClick={() => handleDownloadDoc(doc)}
                        disabled={downloadingId === doc.id}
                      >
                        {downloadingId === doc.id ? 'Downloading…' : 'Download'}
                      </button>
                    ) : (
                      <span className="muted">Unavailable</span>
                    )}
                  </li>
                ))}
              </ul>
            ))}
        </section>
      )}

      {/* Lab results */}
      {activeTab === 'lab' && (
        <section className="portal-section">
          <h2 className="portal-section-title" style={{ marginBottom: 16 }}>Lab results</h2>
          {labState.status === 'loading' && (
            <div className="skeleton-list">
              {[0, 1, 2].map((i) => (
                <div key={i} className="skeleton-row" style={{ height: 64 }} />
              ))}
            </div>
          )}
          {labState.status === 'error' && (
            <div className="error-banner">{labState.message}</div>
          )}
          {labState.status === 'ready' &&
            (labState.data.orders.length === 0 ? (
              <div className="empty-state">
                <div className="empty-state-icon-wrap">
                  <FlaskConical size={28} />
                </div>
                <p className="empty-state-text">No lab orders on file.</p>
              </div>
            ) : (
              <ul className="lab-order-list">
                {labState.data.orders.map((order) => (
                  <LabOrder key={order.id} order={order} />
                ))}
              </ul>
            ))}
        </section>
      )}

      {/* Health summary */}
      {activeTab === 'health' && (
        <section className="portal-section">
          {healthState.status === 'loading' && (
            <div className="skeleton-list">
              {[0, 1, 2, 3].map((i) => <div key={i} className="skeleton-row" />)}
            </div>
          )}
          {healthState.status === 'error' && (
            <div className="error-banner">{healthState.message}</div>
          )}
          {healthState.status === 'ready' &&
            (() => {
              const s = healthState.data
              const categories = [
                {
                  label: `Problems (${s.problemCount})`,
                  items: s.problems,
                  render: (item: typeof s.problems[0]) => (
                    <li key={item.id} className="panel-row">
                      <div>
                        <p className="panel-row-title">{item.title}</p>
                        <p className="panel-row-meta">
                          {item.startDate ? `Since ${item.startDate}` : ''}
                          {item.reportedDate ? ` · Reported ${item.reportedDate}` : ''}
                          {item.endDate ? ` · Resolved ${item.endDate}` : ''}
                        </p>
                      </div>
                    </li>
                  ),
                  empty: 'No active problems on file.',
                },
                {
                  label: `Allergies (${s.allergyCount})`,
                  items: s.allergies,
                  render: (item: typeof s.allergies[0]) => (
                    <li key={item.id} className="panel-row">
                      <div>
                        <p className="panel-row-title">{item.title}</p>
                        <p className="panel-row-meta">
                          {item.reaction ?? 'Reaction not noted'}
                          {item.severity ? ` · ${item.severity}` : ''}
                        </p>
                      </div>
                    </li>
                  ),
                  empty: 'No known allergies on file.',
                },
                {
                  label: `Medications (${s.medicationCount})`,
                  items: s.medications,
                  render: (item: typeof s.medications[0]) => (
                    <li key={item.id} className="panel-row">
                      <div>
                        <p className="panel-row-title">{item.title}</p>
                        <p className="panel-row-meta">
                          {item.startDate ? `Started ${item.startDate}` : ''}
                          {item.endDate ? ` · Ended ${item.endDate}` : ''}
                        </p>
                      </div>
                    </li>
                  ),
                  empty: 'No active medications on file.',
                },
                {
                  label: `Prescriptions (${s.prescriptionCount})`,
                  items: s.prescriptions,
                  render: (item: typeof s.prescriptions[0]) => (
                    <li key={item.id} className="panel-row">
                      <div>
                        <p className="panel-row-title">{item.drug}</p>
                        <p className="panel-row-meta">
                          {item.dosage ?? ''}
                          {item.quantity ? ` · Qty ${item.quantity}` : ''}
                          {item.route ? ` · ${item.route}` : ''}
                        </p>
                      </div>
                    </li>
                  ),
                  empty: 'No active prescriptions on file.',
                },
              ] as const

              return (
                <>
                  <div className="portal-section-header" style={{ marginBottom: 16 }}>
                    <h2 className="portal-section-title">Health summary</h2>
                    {/* "as of" date from the API response (#4) */}
                    {s.asOfDate && (
                      <span className="health-as-of">As of {s.asOfDate}</span>
                    )}
                  </div>
                  <div className="health-grid">
                    {categories.map((cat) => (
                      <div key={cat.label} className="health-category">
                        <h3 className="health-category-title">{cat.label}</h3>
                        {cat.items.length === 0 ? (
                          <p className="muted empty-row">{cat.empty}</p>
                        ) : (
                          <ul className="panel-list">
                            {/* @ts-expect-error - heterogeneous union renders fine */}
                            {cat.items.map(cat.render)}
                          </ul>
                        )}
                      </div>
                    ))}
                  </div>
                </>
              )
            })()}
        </section>
      )}

      {/* Medical report */}
      {activeTab === 'report' && (
        <section className="portal-section">
          <h2 className="portal-section-title" style={{ marginBottom: 8 }}>Medical report</h2>
          <p className="muted" style={{ marginBottom: 24 }}>
            Generate a comprehensive PDF summary of your medical record including problems,
            medications, lab results, and more.
          </p>
          {reportError && <div className="error-banner">{reportError}</div>}
          <button
            className="button-primary"
            type="button"
            style={{ maxWidth: 280 }}
            onClick={handleDownloadReport}
            disabled={reportDownloading}
          >
            <Download size={15} style={{ marginRight: 8, verticalAlign: 'middle' }} />
            {reportDownloading ? 'Preparing your report…' : 'Download medical report (PDF)'}
          </button>
          <p className="muted" style={{ marginTop: 14, fontSize: 12 }}>
            The report is generated fresh each time and may take a few seconds to prepare.
          </p>
        </section>
      )}
    </div>
  )
}
