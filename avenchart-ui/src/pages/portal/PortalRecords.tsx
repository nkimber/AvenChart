// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useState } from "react";
import { useLocation, useOutletContext } from "react-router-dom";
import {
  ChevronDown,
  ChevronRight,
  Download,
  File,
  FileImage,
  FileText,
  FlaskConical,
  Heart,
  RefreshCw,
} from "lucide-react";
import {
  downloadPatientPortalDocuments,
  downloadPatientPortalGeneratedMedicalReportPdf,
  downloadPatientPortalGeneratedMedicalReportPackage,
  getPatientPortalClinicalSummary,
  getPatientPortalDocuments,
  getPatientPortalGeneratedMedicalReportAudit,
  getPatientPortalLabResults,
  getPatientPortalMedicalReport,
  getPatientPortalPrescriptionRefillHistory,
  generatePatientPortalMedicalReport,
  requestPatientPortalPrescriptionRefill,
  type PatientPortalClinicalSummaryResponse,
  type PatientPortalDocumentItem,
  type PatientPortalDocumentsResponse,
  type PatientPortalGeneratedMedicalReportAuditResponse,
  type PatientPortalLabOrderItem,
  type PatientPortalLabResultsResponse,
  type PatientPortalGeneratedMedicalReportResponse,
  type PatientPortalMedicalReportGenerationInput,
  type PatientPortalMedicalReportResponse,
  type PatientPortalPrescriptionRefillHistoryResponse,
} from "../../api.ts";
import type { PortalOutletContext } from "./PortalShell.tsx";
import { showToast } from "../../components/Toast.tsx";
import {
  LabResultFlag,
  labResultFlagClass,
} from "../../components/LabResultFlag.tsx";

type AsyncState<T> =
  | { status: "idle" }
  | { status: "loading" }
  | { status: "ready"; data: T }
  | { status: "error"; message: string };

type RecordsTab = "documents" | "lab" | "health" | "report";

const TABS: { key: RecordsTab; label: string; icon: typeof FileText }[] = [
  { key: "documents", label: "Documents", icon: FileText },
  { key: "lab", label: "Lab results", icon: FlaskConical },
  { key: "health", label: "Health summary", icon: Heart },
  { key: "report", label: "Medical report", icon: Download },
];

function formatBytes(value?: number | null) {
  if (!value || value <= 0) return "";
  if (value < 1024) return `${value} B`;
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`;
  return `${(value / (1024 * 1024)).toFixed(1)} MB`;
}

function triggerBlobDownload(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

function toggleSelection(ids: Set<string>, id: string) {
  const next = new Set(ids);
  if (next.has(id)) next.delete(id);
  else next.add(id);
  return next;
}

function refillRequestStatus(status: string) {
  if (status.toLowerCase() === "approved")
    return { label: "Approved", className: "refill-status-approved" };
  if (status.toLowerCase() === "pending")
    return { label: "Pending review", className: "refill-status-pending" };
  if (status.toLowerCase() === "clarification-requested")
    return {
      label: "Clarification requested",
      className: "refill-status-clarification",
    };
  if (status.toLowerCase() === "denied")
    return { label: "Denied", className: "refill-status-denied" };
  if (status.toLowerCase() === "completed")
    return { label: "Completed", className: "refill-status-completed" };
  return {
    label: `Submitted · ${status || "status unavailable"}`,
    className: "refill-status-submitted",
  };
}

function docIcon(name: string) {
  const ext = name.split(".").pop()?.toLowerCase() ?? "";
  if (ext === "pdf") return <FileText size={16} />;
  if (["jpg", "jpeg", "png", "gif", "webp", "tiff", "bmp"].includes(ext))
    return <FileImage size={16} />;
  return <File size={16} />;
}

function docIconClass(name: string) {
  const ext = name.split(".").pop()?.toLowerCase() ?? "";
  if (ext === "pdf") return "doc-icon-wrap doc-icon-pdf";
  if (["jpg", "jpeg", "png", "gif", "webp"].includes(ext))
    return "doc-icon-wrap doc-icon-image";
  return "doc-icon-wrap";
}

function LabOrder({ order }: { order: PatientPortalLabOrderItem }) {
  const [expanded, setExpanded] = useState(false);
  return (
    <li className="lab-order">
      <button
        className="lab-order-header"
        type="button"
        onClick={() => setExpanded((e) => !e)}
      >
        <div className="lab-order-info">
          <p className="lab-order-name">{order.procedureName}</p>
          <p className="lab-order-meta">
            Ordered {order.orderDate}
            {order.orderStatus ? ` · ${order.orderStatus}` : ""}
            {` · ${order.resultCount} result${order.resultCount === 1 ? "" : "s"}`}
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
            <p className="muted" style={{ padding: "10px 16px", fontSize: 13 }}>
              No reports filed for this order.
            </p>
          ) : (
            order.reports.map((report) => (
              <div key={report.id} className="lab-report">
                <div className="lab-report-header">
                  <span className="lab-report-label">
                    {report.dateCollected
                      ? `Collected ${report.dateCollected}`
                      : "Report"}
                  </span>
                  {report.reportStatus && (
                    <span className="badge-new">{report.reportStatus}</span>
                  )}
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
                          className={
                            labResultFlagClass(result.abnormal)
                              ? "lab-result-row-flagged"
                              : ""
                          }
                        >
                          <td className="lab-result-name">
                            {result.resultName}
                          </td>
                          <td className="lab-result-value">
                            {result.value ?? "—"}
                            {result.units ? (
                              <span className="lab-result-units">
                                {" "}
                                {result.units}
                              </span>
                            ) : null}
                          </td>
                          <td className="lab-result-range">
                            {result.range ?? "—"}
                          </td>
                          <td>
                            <LabResultFlag value={result.abnormal} />
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
  );
}

const SESSION_TAB_KEY = "portal-records-tab";

export default function PortalRecords() {
  const { session, refreshHome } = useOutletContext<PortalOutletContext>();
  const location = useLocation();

  // Persist active tab across navigations within the session (#3)
  const [activeTab, setActiveTab] = useState<RecordsTab>(() => {
    // Allow link state to override (e.g., Account "medical report" link)
    if (
      location.state?.tab &&
      ["documents", "lab", "health", "report"].includes(location.state.tab)
    ) {
      return location.state.tab as RecordsTab;
    }
    const saved = sessionStorage.getItem(SESSION_TAB_KEY);
    return (saved as RecordsTab | null) ?? "documents";
  });

  function switchTab(tab: RecordsTab) {
    setActiveTab(tab);
    sessionStorage.setItem(SESSION_TAB_KEY, tab);
  }

  const [docsState, setDocsState] = useState<
    AsyncState<PatientPortalDocumentsResponse>
  >({
    status: "idle",
  });
  const [downloadingId, setDownloadingId] = useState<number | null>(null);
  const [downloadError, setDownloadError] = useState<string | null>(null);

  const [labState, setLabState] = useState<
    AsyncState<PatientPortalLabResultsResponse>
  >({
    status: "idle",
  });
  const [healthState, setHealthState] = useState<
    AsyncState<PatientPortalClinicalSummaryResponse>
  >({ status: "idle" });
  const [refillHistoryState, setRefillHistoryState] = useState<
    AsyncState<PatientPortalPrescriptionRefillHistoryResponse>
  >({ status: "idle" });

  const [reportDownloading, setReportDownloading] = useState(false);
  const [reportOptions, setReportOptions] = useState<
    AsyncState<PatientPortalMedicalReportResponse>
  >({ status: "idle" });
  const [selectedSections, setSelectedSections] = useState<Set<string>>(
    () => new Set(),
  );
  const [selectedIssues, setSelectedIssues] = useState<Set<string>>(
    () => new Set(),
  );
  const [selectedForms, setSelectedForms] = useState<Set<string>>(
    () => new Set(),
  );
  const [selectedOrders, setSelectedOrders] = useState<Set<string>>(
    () => new Set(),
  );
  const [generatedReport, setGeneratedReport] =
    useState<PatientPortalGeneratedMedicalReportResponse | null>(null);
  const [reportAudit, setReportAudit] = useState<
    AsyncState<PatientPortalGeneratedMedicalReportAuditResponse>
  >({ status: "idle" });
  const [generatingReport, setGeneratingReport] = useState(false);
  const [reportSelectionError, setReportSelectionError] = useState<
    string | null
  >(null);
  const [refillOpenId, setRefillOpenId] = useState<string | null>(null);
  const [refillNote, setRefillNote] = useState("");
  const [refillingId, setRefillingId] = useState<string | null>(null);

  // Prefetch all three data tabs in parallel on mount (#10)
  useEffect(() => {
    loadDocs();
    loadLab();
    loadHealth();
    loadRefillHistory();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (activeTab !== "report" || reportAudit.status !== "idle") return;
    void loadReportAudit();
    // The audit is loaded once on first entry and explicitly refreshed after
    // report generation or download.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeTab, reportAudit.status]);

  function loadDocs() {
    setDocsState({ status: "loading" });
    getPatientPortalDocuments(session.sessionId)
      .then((data) => setDocsState({ status: "ready", data }))
      .catch((err) =>
        setDocsState({
          status: "error",
          message:
            err instanceof Error ? err.message : "Could not load documents.",
        }),
      );
  }

  function loadLab() {
    setLabState({ status: "loading" });
    getPatientPortalLabResults(session.sessionId)
      .then((data) => setLabState({ status: "ready", data }))
      .catch((err) =>
        setLabState({
          status: "error",
          message:
            err instanceof Error ? err.message : "Could not load lab results.",
        }),
      );
  }

  function loadHealth() {
    setHealthState({ status: "loading" });
    getPatientPortalClinicalSummary(session.sessionId)
      .then((data) => setHealthState({ status: "ready", data }))
      .catch((err) =>
        setHealthState({
          status: "error",
          message:
            err instanceof Error
              ? err.message
              : "Could not load health summary.",
        }),
      );
  }

  function loadRefillHistory() {
    setRefillHistoryState({ status: "loading" });
    getPatientPortalPrescriptionRefillHistory(session.sessionId)
      .then((data) => setRefillHistoryState({ status: "ready", data }))
      .catch((error) =>
        setRefillHistoryState({
          status: "error",
          message:
            error instanceof Error
              ? error.message
              : "Could not load refill request history.",
        }),
      );
  }

  function handleDownloadDoc(doc: PatientPortalDocumentItem) {
    setDownloadError(null);
    setDownloadingId(doc.id);
    downloadPatientPortalDocuments(session.sessionId, { documentIds: [doc.id] })
      .then((blob) => {
        triggerBlobDownload(blob, doc.name);
        showToast(`Downloaded: ${doc.name}`);
      })
      .catch((err) => {
        const msg =
          err instanceof Error
            ? err.message
            : "Could not download that document.";
        setDownloadError(msg);
        showToast(msg, "error");
      })
      .finally(() => setDownloadingId(null));
  }

  function handleDownloadReport() {
    setReportDownloading(true);
    downloadPatientPortalGeneratedMedicalReportPdf(session.sessionId)
      .then((blob) => {
        triggerBlobDownload(
          blob,
          `medical-report-${session.portalUsername}.pdf`,
        );
        void loadReportAudit();
        showToast("Medical report downloaded.");
      })
      .catch((err) =>
        showToast(
          err instanceof Error ? err.message : "Could not generate the report.",
          "error",
        ),
      )
      .finally(() => setReportDownloading(false));
  }

  function reportInput(): PatientPortalMedicalReportGenerationInput {
    return {
      sectionIds: [...selectedSections],
      issueIds: [...selectedIssues],
      encounterFormIds: [...selectedForms],
      procedureOrderIds: [...selectedOrders],
    };
  }

  function loadReportOptions() {
    setReportOptions({ status: "loading" });
    getPatientPortalMedicalReport(session.sessionId)
      .then((data) => {
        if (!data.authenticated)
          throw new Error(
            data.failureReason ?? "Medical report options are unavailable.",
          );
        setReportOptions({ status: "ready", data });
        setSelectedSections(
          new Set(
            data.sections
              .filter((section) => section.selected)
              .map((section) => section.id),
          ),
        );
        setSelectedIssues(new Set(data.issues.map((issue) => issue.id)));
        setSelectedForms(
          new Set(
            data.encounters.flatMap((encounter) =>
              encounter.forms.map((form) => form.id),
            ),
          ),
        );
        setSelectedOrders(
          new Set(data.procedureOrders.map((order) => order.id)),
        );
      })
      .catch((error) =>
        setReportOptions({
          status: "error",
          message:
            error instanceof Error
              ? error.message
              : "Could not load report options.",
        }),
      );
  }

  async function loadReportAudit() {
    setReportAudit({ status: "loading" });
    try {
      const data = await getPatientPortalGeneratedMedicalReportAudit(
        session.sessionId,
      );
      if (!data.authenticated) {
        throw new Error(
          data.failureReason ?? "Report activity is unavailable.",
        );
      }
      setReportAudit({ status: "ready", data });
    } catch (error) {
      setReportAudit({
        status: "error",
        message:
          error instanceof Error
            ? error.message
            : "Could not load report activity.",
      });
    }
  }

  async function generateReport() {
    if (
      selectedSections.size +
        selectedIssues.size +
        selectedForms.size +
        selectedOrders.size ===
      0
    ) {
      setReportSelectionError("Select at least one report item.");
      return;
    }
    setReportSelectionError(null);
    setGeneratingReport(true);
    try {
      const result = await generatePatientPortalMedicalReport(
        session.sessionId,
        reportInput(),
      );
      if (!result.authenticated)
        throw new Error(
          result.failureReason ?? "Could not generate the report.",
        );
      setGeneratedReport(result);
      void loadReportAudit();
      showToast("Medical report generated.", "success");
    } catch (error) {
      showToast(
        error instanceof Error
          ? error.message
          : "Could not generate the report.",
        "error",
      );
    } finally {
      setGeneratingReport(false);
    }
  }

  function downloadSelectedReport(kind: "pdf" | "package") {
    if (
      selectedSections.size +
        selectedIssues.size +
        selectedForms.size +
        selectedOrders.size ===
      0
    ) {
      setReportSelectionError("Select at least one report item.");
      return;
    }
    setReportSelectionError(null);
    const action =
      kind === "pdf"
        ? downloadPatientPortalGeneratedMedicalReportPdf(
            session.sessionId,
            reportInput(),
          )
        : downloadPatientPortalGeneratedMedicalReportPackage(
            session.sessionId,
            reportInput(),
          );
    setReportDownloading(true);
    action
      .then((blob) => {
        triggerBlobDownload(
          blob,
          kind === "pdf"
            ? `medical-report-${session.portalUsername}.pdf`
            : `medical-report-${session.portalUsername}.zip`,
        );
        void loadReportAudit();
      })
      .catch((error) =>
        showToast(
          error instanceof Error
            ? error.message
            : "Could not download the report.",
          "error",
        ),
      )
      .finally(() => setReportDownloading(false));
  }

  async function requestRefill(prescriptionId: string, drug: string) {
    setRefillingId(prescriptionId);
    try {
      const result = await requestPatientPortalPrescriptionRefill(
        session.sessionId,
        prescriptionId,
        {
          requestDate: new Date().toISOString().slice(0, 10),
          note: refillNote.trim() || null,
        },
      );
      if (!result.created)
        throw new Error(
          result.failureReason ?? "Could not submit the refill request.",
        );
      setRefillOpenId(null);
      setRefillNote("");
      loadRefillHistory();
      refreshHome();
      showToast(`Refill request sent for ${drug}.`, "success");
    } catch (error) {
      showToast(
        error instanceof Error
          ? error.message
          : "Could not submit the refill request.",
        "error",
      );
    } finally {
      setRefillingId(null);
    }
  }

  return (
    <div className="portal-page">
      <nav className="records-tab-nav">
        {TABS.map((tab) => {
          const Icon = tab.icon;
          return (
            <button
              key={tab.key}
              className={`records-tab${activeTab === tab.key ? " records-tab-active" : ""}`}
              type="button"
              onClick={() => switchTab(tab.key)}
            >
              <Icon size={15} />
              {tab.label}
            </button>
          );
        })}
      </nav>

      {/* Documents */}
      {activeTab === "documents" && (
        <section className="portal-section">
          <h2 className="portal-section-title" style={{ marginBottom: 16 }}>
            Documents
          </h2>
          {downloadError && <div className="error-banner">{downloadError}</div>}
          {docsState.status === "loading" && (
            <div className="skeleton-list">
              {[0, 1, 2].map((i) => (
                <div key={i} className="skeleton-row" />
              ))}
            </div>
          )}
          {docsState.status === "error" && (
            <div className="error-banner">{docsState.message}</div>
          )}
          {docsState.status === "ready" &&
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
                    <div className={docIconClass(doc.name)}>
                      {docIcon(doc.name)}
                    </div>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <p className="panel-row-title">{doc.name}</p>
                      <p className="panel-row-meta">
                        {doc.categoryName} · {doc.docDate}
                        {formatBytes(doc.sizeBytes)
                          ? ` · ${formatBytes(doc.sizeBytes)}`
                          : ""}
                      </p>
                    </div>
                    {doc.canDownload ? (
                      <button
                        className="link-button"
                        type="button"
                        onClick={() => handleDownloadDoc(doc)}
                        disabled={downloadingId === doc.id}
                      >
                        {downloadingId === doc.id ? "Downloading…" : "Download"}
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
      {activeTab === "lab" && (
        <section className="portal-section">
          <h2 className="portal-section-title" style={{ marginBottom: 16 }}>
            Lab results
          </h2>
          {labState.status === "loading" && (
            <div className="skeleton-list">
              {[0, 1, 2].map((i) => (
                <div key={i} className="skeleton-row" style={{ height: 64 }} />
              ))}
            </div>
          )}
          {labState.status === "error" && (
            <div className="error-banner">{labState.message}</div>
          )}
          {labState.status === "ready" &&
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
      {activeTab === "health" && (
        <section className="portal-section">
          {healthState.status === "loading" && (
            <div className="skeleton-list">
              {[0, 1, 2, 3].map((i) => (
                <div key={i} className="skeleton-row" />
              ))}
            </div>
          )}
          {healthState.status === "error" && (
            <div className="error-banner">{healthState.message}</div>
          )}
          {healthState.status === "ready" &&
            (() => {
              const s = healthState.data;
              const categories = [
                {
                  label: `Problems (${s.problemCount})`,
                  items: s.problems,
                  render: (item: (typeof s.problems)[0]) => (
                    <li key={item.id} className="panel-row">
                      <div>
                        <p className="panel-row-title">{item.title}</p>
                        <p className="panel-row-meta">
                          {item.startDate ? `Since ${item.startDate}` : ""}
                          {item.reportedDate
                            ? ` · Reported ${item.reportedDate}`
                            : ""}
                          {item.endDate ? ` · Resolved ${item.endDate}` : ""}
                        </p>
                      </div>
                    </li>
                  ),
                  empty: "No active problems on file.",
                },
                {
                  label: `Allergies (${s.allergyCount})`,
                  items: s.allergies,
                  render: (item: (typeof s.allergies)[0]) => (
                    <li key={item.id} className="panel-row">
                      <div>
                        <p className="panel-row-title">{item.title}</p>
                        <p className="panel-row-meta">
                          {item.reaction ?? "Reaction not noted"}
                          {item.severity ? ` · ${item.severity}` : ""}
                        </p>
                      </div>
                    </li>
                  ),
                  empty: "No known allergies on file.",
                },
                {
                  label: `Medications (${s.medicationCount})`,
                  items: s.medications,
                  render: (item: (typeof s.medications)[0]) => (
                    <li key={item.id} className="panel-row">
                      <div>
                        <p className="panel-row-title">{item.title}</p>
                        <p className="panel-row-meta">
                          {item.startDate ? `Started ${item.startDate}` : ""}
                          {item.endDate ? ` · Ended ${item.endDate}` : ""}
                        </p>
                      </div>
                    </li>
                  ),
                  empty: "No active medications on file.",
                },
                {
                  label: `Prescriptions (${s.prescriptionCount})`,
                  items: s.prescriptions,
                  render: (item: (typeof s.prescriptions)[0]) => (
                    <li key={item.id} className="panel-row">
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <p className="panel-row-title">{item.drug}</p>
                        <p className="panel-row-meta">
                          {item.dosage ?? ""}
                          {item.quantity ? ` · Qty ${item.quantity}` : ""}
                          {item.route ? ` · ${item.route}` : ""}
                        </p>
                        {refillOpenId === item.id && (
                          <div className="portal-refill-form">
                            <label
                              className="label"
                              htmlFor={`refill-note-${item.id}`}
                            >
                              Note for your care team (optional)
                            </label>
                            <textarea
                              id={`refill-note-${item.id}`}
                              className="input"
                              rows={2}
                              value={refillNote}
                              onChange={(event) =>
                                setRefillNote(event.target.value)
                              }
                            />
                            <div className="portal-refill-actions">
                              <button
                                className="button-primary"
                                type="button"
                                disabled={refillingId === item.id}
                                onClick={() =>
                                  requestRefill(item.id, item.drug)
                                }
                              >
                                {refillingId === item.id
                                  ? "Sending..."
                                  : "Send request"}
                              </button>
                              <button
                                className="button-secondary"
                                type="button"
                                disabled={refillingId === item.id}
                                onClick={() => {
                                  setRefillOpenId(null);
                                  setRefillNote("");
                                }}
                              >
                                Cancel
                              </button>
                            </div>
                          </div>
                        )}
                      </div>
                      {refillOpenId !== item.id && (
                        <button
                          className="link-button"
                          type="button"
                          onClick={() => {
                            setRefillOpenId(item.id);
                            setRefillNote("");
                          }}
                        >
                          Request refill
                        </button>
                      )}
                    </li>
                  ),
                  empty: "No active prescriptions on file.",
                },
              ] as const;
              const refillRequests =
                refillHistoryState.status === "ready"
                  ? refillHistoryState.data.requests
                  : [];

              return (
                <>
                  <div
                    className="portal-section-header"
                    style={{ marginBottom: 16 }}
                  >
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
                  <div className="refill-history">
                    <div className="refill-history-header">
                      <div>
                        <h3>Refill request history</h3>
                        <p>
                          Requests sent from this portal and their current
                          local review status.
                        </p>
                      </div>
                      <button
                        className="button-secondary"
                        type="button"
                        disabled={refillHistoryState.status === "loading"}
                        onClick={loadRefillHistory}
                      >
                        <RefreshCw size={14} /> Refresh
                      </button>
                    </div>
                    <div className="hint-banner">
                      These states describe your care team&apos;s local review.
                      “Completed” does not confirm that a pharmacy dispensed or
                      delivered medication.
                    </div>
                    {refillHistoryState.status === "loading" && (
                      <p className="muted" aria-live="polite">
                        Loading refill request history…
                      </p>
                    )}
                    {refillHistoryState.status === "error" && (
                      <div className="error-banner">
                        <span>{refillHistoryState.message}</span>
                        <button
                          className="button-secondary"
                          type="button"
                          onClick={loadRefillHistory}
                        >
                          Try again
                        </button>
                      </div>
                    )}
                    {refillHistoryState.status === "ready" &&
                      refillRequests.length === 0 && (
                        <p className="muted empty-row">
                          No refill requests have been submitted from this
                          portal.
                        </p>
                      )}
                    {refillHistoryState.status === "ready" &&
                      refillRequests.length > 0 && (
                        <ul className="refill-history-list">
                          {refillRequests.map((request) => {
                            const status = refillRequestStatus(request.status);
                            return (
                              <li key={request.messageId}>
                                <div className="refill-history-title">
                                  <strong>{request.drug}</strong>
                                  <span className={status.className}>
                                    {status.label}
                                  </span>
                                </div>
                                <p>
                                  Requested {request.requestDate} · Prescription{" "}
                                  {request.prescriptionId}
                                </p>
                                {request.patientNote && (
                                  <p>Patient note: {request.patientNote}</p>
                                )}
                                {request.staffResponse && (
                                  <p className="refill-history-response">
                                    Care team response: {request.staffResponse}
                                  </p>
                                )}
                                <p>
                                  Last updated{" "}
                                  {new Date(request.updatedAt).toLocaleString()}{" "}
                                  by {request.updatedBy}
                                </p>
                              </li>
                            );
                          })}
                        </ul>
                      )}
                    {refillHistoryState.status === "ready" && (
                      <p className="refill-history-source">
                        Source: refill request lifecycle · dataset{" "}
                        {refillHistoryState.data.datasetId} ·{" "}
                        {refillHistoryState.data.datasetVersion}
                      </p>
                    )}
                  </div>
                </>
              );
            })()}
        </section>
      )}

      {/* Medical report */}
      {activeTab === "report" && (
        <section className="portal-section">
          <h2 className="portal-section-title" style={{ marginBottom: 8 }}>
            Medical report
          </h2>
          <p className="muted" style={{ marginBottom: 20 }}>
            Download a comprehensive PDF summary of your medical record,
            generated fresh on demand.
          </p>

          {reportOptions.status === "idle" && (
            <button
              className="button-secondary"
              style={{ width: "auto", marginBottom: 18 }}
              type="button"
              onClick={loadReportOptions}
            >
              Choose report contents
            </button>
          )}
          {reportOptions.status === "loading" && (
            <div className="skeleton-list">
              <div className="skeleton-row" style={{ height: 130 }} />
            </div>
          )}
          {reportOptions.status === "error" && (
            <div className="error-banner">{reportOptions.message}</div>
          )}
          {reportOptions.status === "ready" && (
            <div className="report-builder">
              <div className="report-builder-heading">
                <p className="report-contents-label">Choose what to include</p>
                <div className="portal-refill-actions">
                  <button
                    className="button-secondary"
                    type="button"
                    onClick={() => {
                      setSelectedSections(
                        new Set(
                          reportOptions.data.sections.map((item) => item.id),
                        ),
                      );
                      setSelectedIssues(
                        new Set(
                          reportOptions.data.issues.map((item) => item.id),
                        ),
                      );
                      setSelectedForms(
                        new Set(
                          reportOptions.data.encounters.flatMap((encounter) =>
                            encounter.forms.map((item) => item.id),
                          ),
                        ),
                      );
                      setSelectedOrders(
                        new Set(
                          reportOptions.data.procedureOrders.map(
                            (item) => item.id,
                          ),
                        ),
                      );
                      setReportSelectionError(null);
                    }}
                  >
                    Select all
                  </button>
                  <button
                    className="button-secondary"
                    type="button"
                    onClick={() => {
                      setSelectedSections(new Set());
                      setSelectedIssues(new Set());
                      setSelectedForms(new Set());
                      setSelectedOrders(new Set());
                      setReportSelectionError(null);
                    }}
                  >
                    Select none
                  </button>
                </div>
              </div>
              <div className="report-builder-grid">
                <div>
                  <h3>Sections</h3>
                  {reportOptions.data.sections.map((section) => (
                    <label key={section.id}>
                      <input
                        type="checkbox"
                        checked={selectedSections.has(section.id)}
                        onChange={() =>
                          setSelectedSections((ids) =>
                            toggleSelection(ids, section.id),
                          )
                        }
                      />{" "}
                      {section.label}
                    </label>
                  ))}
                </div>
                <div>
                  <h3>Issues</h3>
                  {reportOptions.data.issues.map((issue) => (
                    <label key={issue.id}>
                      <input
                        type="checkbox"
                        checked={selectedIssues.has(issue.id)}
                        onChange={() =>
                          setSelectedIssues((ids) =>
                            toggleSelection(ids, issue.id),
                          )
                        }
                      />{" "}
                      {issue.typeLabel}: {issue.title}
                    </label>
                  ))}
                  {reportOptions.data.issues.length === 0 && (
                    <p className="muted">No issues available.</p>
                  )}
                </div>
                <div>
                  <h3>Encounter forms</h3>
                  {reportOptions.data.encounters.flatMap((encounter) =>
                    encounter.forms.map((form) => (
                      <label key={form.id}>
                        <input
                          type="checkbox"
                          checked={selectedForms.has(form.id)}
                          onChange={() =>
                            setSelectedForms((ids) =>
                              toggleSelection(ids, form.id),
                            )
                          }
                        />{" "}
                        {form.display} — {encounter.date} (encounter #
                        {encounter.encounter})
                      </label>
                    )),
                  )}
                  {reportOptions.data.encounters.every(
                    (encounter) => encounter.forms.length === 0,
                  ) && <p className="muted">No forms available.</p>}
                </div>
                <div>
                  <h3>Procedure orders</h3>
                  {reportOptions.data.procedureOrders.map((order) => (
                    <label key={order.id}>
                      <input
                        type="checkbox"
                        checked={selectedOrders.has(order.id)}
                        onChange={() =>
                          setSelectedOrders((ids) =>
                            toggleSelection(ids, order.id),
                          )
                        }
                      />{" "}
                      {order.procedureName} — ordered {order.orderDate}
                    </label>
                  ))}
                  {reportOptions.data.procedureOrders.length === 0 && (
                    <p className="muted">No orders available.</p>
                  )}
                </div>
              </div>
              {reportSelectionError && (
                <div className="error-banner" role="alert">
                  {reportSelectionError}
                </div>
              )}
              <button
                className="button-primary"
                style={{ width: "auto" }}
                type="button"
                onClick={generateReport}
                disabled={generatingReport}
              >
                {generatingReport
                  ? "Generating..."
                  : "Generate selected report"}
              </button>
              {generatedReport && (
                <div className="report-generated">
                  <strong>{generatedReport.title}</strong>
                  <span>
                    Generated {generatedReport.generatedOn} with{" "}
                    {generatedReport.summaryLines.length} summary lines.
                  </span>
                  <div className="portal-refill-actions">
                    {generatedReport.pdfDownloadAvailable && (
                      <button
                        className="button-secondary"
                        type="button"
                        disabled={reportDownloading}
                        onClick={() => downloadSelectedReport("pdf")}
                      >
                        Download PDF
                      </button>
                    )}
                    {generatedReport.packageDownloadAvailable && (
                      <button
                        className="button-secondary"
                        type="button"
                        disabled={reportDownloading}
                        onClick={() => downloadSelectedReport("package")}
                      >
                        Download package
                      </button>
                    )}
                  </div>
                </div>
              )}
            </div>
          )}

          <div
            className="report-history"
            aria-labelledby="report-history-title"
          >
            <div className="report-builder-heading">
              <h3 id="report-history-title">Report activity</h3>
              {reportAudit.status === "ready" && (
                <span className="muted">
                  {reportAudit.data.auditEventCount} recorded event
                  {reportAudit.data.auditEventCount === 1 ? "" : "s"}
                </span>
              )}
            </div>
            {reportAudit.status === "loading" && (
              <div className="skeleton-list" aria-live="polite">
                <span className="sr-only">Loading report activity</span>
                <div className="skeleton-row" style={{ height: 64 }} />
              </div>
            )}
            {reportAudit.status === "error" && (
              <>
                <div className="error-banner" role="alert">
                  {reportAudit.message}
                </div>
                <button
                  className="button-secondary"
                  type="button"
                  onClick={() => void loadReportAudit()}
                >
                  Retry report activity
                </button>
              </>
            )}
            {reportAudit.status === "ready" &&
              reportAudit.data.auditEvents.length === 0 && (
                <p className="muted">No report activity has been recorded.</p>
              )}
            {reportAudit.status === "ready" &&
              reportAudit.data.auditEvents.length > 0 && (
                <ol className="report-history-list">
                  {[...reportAudit.data.auditEvents]
                    .reverse()
                    .slice(0, 10)
                    .map((event) => (
                      <li key={event.id}>
                        <div>
                          <strong>{event.eventLabel}</strong>
                          <span>{event.eventAt}</span>
                        </div>
                        <p>{event.summary}</p>
                        {event.artifactName && (
                          <span className="muted">
                            {event.artifactName}
                            {event.artifactContentType
                              ? ` · ${event.artifactContentType}`
                              : ""}
                          </span>
                        )}
                      </li>
                    ))}
                </ol>
              )}
          </div>

          {/* Report content summary from already-loaded health state (#9) */}
          {healthState.status === "ready" &&
            (() => {
              const s = healthState.data;
              const bullets = [
                s.problemCount > 0 &&
                  `${s.problemCount} problem${s.problemCount === 1 ? "" : "s"}`,
                s.allergyCount > 0 &&
                  `${s.allergyCount} ${s.allergyCount === 1 ? "allergy" : "allergies"}`,
                s.medicationCount > 0 &&
                  `${s.medicationCount} medication${s.medicationCount === 1 ? "" : "s"}`,
                s.prescriptionCount > 0 &&
                  `${s.prescriptionCount} prescription${s.prescriptionCount === 1 ? "" : "s"}`,
                labState.status === "ready" &&
                  labState.data.orders.length > 0 &&
                  `${labState.data.orders.length} lab order${labState.data.orders.length === 1 ? "" : "s"}`,
                docsState.status === "ready" &&
                  docsState.data.documents.length > 0 &&
                  `${docsState.data.documents.length} document${docsState.data.documents.length === 1 ? "" : "s"}`,
              ].filter(Boolean) as string[];
              if (bullets.length === 0) return null;
              return (
                <div className="report-contents-box">
                  <p className="report-contents-label">
                    This report will include:
                  </p>
                  <ul className="report-contents-list">
                    {bullets.map((b) => (
                      <li key={b}>{b}</li>
                    ))}
                  </ul>
                </div>
              );
            })()}

          <button
            className="button-primary"
            type="button"
            style={{ maxWidth: 300 }}
            onClick={handleDownloadReport}
            disabled={reportDownloading}
          >
            <Download
              size={15}
              style={{ marginRight: 8, verticalAlign: "middle" }}
            />
            {reportDownloading
              ? "Preparing your report…"
              : "Download medical report (PDF)"}
          </button>
          <p className="muted" style={{ marginTop: 14, fontSize: 12 }}>
            Generation may take a few seconds.
          </p>
        </section>
      )}
    </div>
  );
}
