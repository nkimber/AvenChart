import { useEffect, useState } from "react";
import { useOutletContext } from "react-router-dom";
import {
  getProcedureResults,
  isRequestCancellation,
  type ProcedureResultsResponse,
} from "../../api.ts";
import {
  LabResultFlag,
  labResultFlagClass,
} from "../../components/LabResultFlag.tsx";
import type { PatientOutletContext } from "./PatientShell.tsx";

type AsyncState<T> =
  | { status: "loading" }
  | { status: "ready"; data: T }
  | { status: "error"; message: string };

function formatDate(value?: string | null) {
  if (!value) return "—";
  const parsed = new Date(value);
  return Number.isNaN(parsed.valueOf()) ? value : parsed.toLocaleDateString();
}

export default function PatientLabs() {
  const { session, patientId } = useOutletContext<PatientOutletContext>();
  const [loadAttempt, setLoadAttempt] = useState(0);
  const [state, setState] = useState<
    AsyncState<ProcedureResultsResponse>
  >({ status: "loading" });

  useEffect(() => {
    const controller = new AbortController();
    setState({ status: "loading" });
    getProcedureResults(session.sessionId, patientId, controller.signal)
      .then((data) => setState({ status: "ready", data }))
      .catch((error: unknown) => {
        if (isRequestCancellation(error)) return;
        setState({
          status: "error",
          message:
            error instanceof Error
              ? error.message
              : "Could not load lab results.",
        });
      });
    return () => controller.abort();
  }, [loadAttempt, patientId, session.sessionId]);

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <div>
          <h1 className="clinician-page-title">Lab results</h1>
          <p className="clinician-page-subtitle">
            Orders, reports, and current result values for this patient.
          </p>
        </div>
      </div>

      {state.status === "loading" && (
        <div className="cl-card" aria-live="polite">
          <span className="sr-only">Loading lab results</span>
          <div className="skeleton-list">
            {[0, 1, 2].map((item) => (
              <div
                key={item}
                className="skeleton-row"
                style={{ height: 60 }}
              />
            ))}
          </div>
        </div>
      )}

      {state.status === "error" && (
        <div className="cl-card">
          <div className="error-banner" role="alert">
            {state.message}
          </div>
          <button
            className="cl-btn-secondary"
            type="button"
            onClick={() => setLoadAttempt((attempt) => attempt + 1)}
          >
            Retry
          </button>
        </div>
      )}

      {state.status === "ready" && state.data.orders.length === 0 && (
        <div className="cl-card">
          <p className="cl-empty-text">No lab orders or results are on file.</p>
        </div>
      )}

      {state.status === "ready" && state.data.orders.length > 0 && (
        <>
          <section className="cl-card" aria-label="Lab result totals">
            <div className="lab-result-summary">
              <span>{state.data.counts.orders} orders</span>
              <span>{state.data.counts.reports} reports</span>
              <span>{state.data.counts.results} results</span>
              <span>{state.data.counts.finalResults} final results</span>
            </div>
          </section>

          {state.data.orders.map((order) => (
            <section className="cl-card" key={order.id}>
              <div className="cl-card-header">
                <div>
                  <h2 className="cl-card-title">
                    {order.name ?? order.code ?? `Order ${order.id}`}
                  </h2>
                  <p className="cl-table-sub">
                    Ordered {formatDate(order.orderDate)}
                    {order.providerName ? ` · ${order.providerName}` : ""}
                  </p>
                </div>
                <span className="cl-badge">
                  {order.orderStatus ?? "Status unavailable"}
                </span>
              </div>

              {order.reports.length === 0 ? (
                <p className="cl-empty-text">
                  No report has been recorded for this order.
                </p>
              ) : (
                order.reports.map((report) => (
                  <div className="lab-report-detail" key={report.id}>
                    <div className="cl-card-header">
                      <div>
                        <h3>Report {formatDate(report.reportDate)}</h3>
                        <p className="cl-table-sub">
                          {report.specimenNumber
                            ? `Specimen ${report.specimenNumber}`
                            : "No specimen number"}
                          {report.reviewedBy
                            ? ` · Reviewed by ${report.reviewedBy}`
                            : " · Not reviewed"}
                        </p>
                      </div>
                      <span className="cl-badge">
                        {report.status ?? "Status unavailable"}
                      </span>
                    </div>

                    {report.results.length === 0 ? (
                      <p className="cl-empty-text">
                        No atomic results are attached to this report.
                      </p>
                    ) : (
                      <div
                        className="cl-table-wrap"
                        role="region"
                        aria-label={`${order.name ?? order.code ?? `Order ${order.id}`} report results`}
                        tabIndex={0}
                      >
                        <table className="cl-table">
                          <thead>
                            <tr>
                              <th>Result</th>
                              <th>Value</th>
                              <th>Reference range</th>
                              <th>Status</th>
                              <th>Reported</th>
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
                                <td>
                                  {result.text ??
                                    result.code ??
                                    `Result ${result.id}`}
                                  {result.hasPriorVersions && (
                                    <span className="cl-table-sub">
                                      {" "}
                                      · corrected ({result.versionLabel})
                                    </span>
                                  )}
                                </td>
                                <td>
                                  {result.result ?? "—"}{" "}
                                  {result.units ?? ""}
                                  <LabResultFlag value={result.abnormal} />
                                </td>
                                <td>{result.range ?? "—"}</td>
                                <td>{result.resultStatus ?? "—"}</td>
                                <td>{formatDate(result.resultDate)}</td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    )}
                  </div>
                ))
              )}
            </section>
          ))}
        </>
      )}
    </div>
  );
}
