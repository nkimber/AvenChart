import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { Download, FolderOpen } from 'lucide-react'
import { getPatientDocuments, type PatientDocumentItem } from '../../api.ts'
import type { PatientOutletContext } from './PatientShell.tsx'

type AsyncState<T> =
  | { status: 'loading' }
  | { status: 'ready'; data: T }
  | { status: 'error'; message: string }

function formatBytes(n?: number | null) {
  if (!n) return ''
  if (n < 1024) return `${n} B`
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KB`
  return `${(n / (1024 * 1024)).toFixed(1)} MB`
}

export default function PatientDocuments() {
  const { session, patientId } = useOutletContext<PatientOutletContext>()
  const [state, setState] = useState<AsyncState<PatientDocumentItem[]>>({ status: 'loading' })

  useEffect(() => {
    getPatientDocuments(session.sessionId, patientId)
      .then((data) => setState({ status: 'ready', data: data.documents }))
      .catch((err) => setState({ status: 'error', message: err instanceof Error ? err.message : 'Failed to load.' }))
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [patientId])

  return (
    <div className="clinician-page">
      {state.status === 'loading' && (
        <div className="cl-card">
          <div className="skeleton-list">
            {[0, 1, 2].map((i) => <div key={i} className="skeleton-row" style={{ height: 56 }} />)}
          </div>
        </div>
      )}
      {state.status === 'error' && <div className="error-banner">{state.message}</div>}
      {state.status === 'ready' && state.data.length === 0 && (
        <div className="cl-card">
          <div className="cl-search-empty-state">
            <FolderOpen size={40} />
            <p>No documents on file.</p>
          </div>
        </div>
      )}
      {state.status === 'ready' && state.data.length > 0 && (
        <section className="cl-card" style={{ padding: 0, overflow: 'hidden' }}>
          <table className="cl-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Category</th>
                <th>Date</th>
                <th>Size</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {state.data.map((doc) => (
                <tr key={doc.id}>
                  <td>{doc.name}</td>
                  <td className="cl-td-muted">{doc.categoryName}</td>
                  <td className="cl-td-muted">{doc.docDate}</td>
                  <td className="cl-td-muted">{formatBytes(doc.sizeBytes)}</td>
                  <td>
                    {doc.canDownload && (
                      <a
                        className="cl-link"
                        href={`/api/documents/${patientId}/${doc.id}/download`}
                        download={doc.name}
                        aria-label={`Download ${doc.name}`}
                      >
                        <Download size={14} />
                      </a>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      )}
    </div>
  )
}
