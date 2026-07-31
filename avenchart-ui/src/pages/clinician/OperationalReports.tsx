// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useState } from "react";
import { useOutletContext } from "react-router-dom";
import {
  downloadReportFamilyCsv,
  getOperationalReports,
  getReportFamilies,
  type OperationalReportsResponse,
  type ReportFamily,
} from "../../api.ts";
import { showToast } from "../../components/Toast.tsx";
import type { ClinicianOutletContext } from "./ClinicianShell.tsx";
import GovernedReportDefinitions from "./GovernedReportDefinitions.tsx";
import GovernedReportExecution from "./GovernedReportExecution.tsx";

type AsyncState<T> =
  | { status: "loading" }
  | { status: "ready"; data: T }
  | { status: "error"; message: string };

function StatTile({
  label,
  value,
  note,
}: {
  label: string;
  value: number | string;
  note?: string;
}) {
  return (
    <div className="cl-stat-tile">
      <span className="cl-stat-tile-value">
        {typeof value === "number" ? value.toLocaleString() : value}
      </span>
      <span className="cl-stat-tile-label">{label}</span>
      {note && <span className="cl-stat-tile-note">{note}</span>}
    </div>
  );
}

function formatCurrency(n: number) {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
    maximumFractionDigits: 0,
  }).format(n);
}

export default function OperationalReports() {
  const { session } = useOutletContext<ClinicianOutletContext>();
  const [state, setState] = useState<AsyncState<OperationalReportsResponse>>({
    status: "loading",
  });
  const [families, setFamilies] = useState<ReportFamily[]>([]);
  const [reportType, setReportType] = useState("operational");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");

  useEffect(() => {
    getOperationalReports(session.sessionId)
      .then((data) => setState({ status: "ready", data }))
      .catch((err) =>
        setState({
          status: "error",
          message: err instanceof Error ? err.message : "Failed to load.",
        }),
      );
  }, [session.sessionId]);
  useEffect(() => {
    getReportFamilies(session.sessionId)
      .then(setFamilies)
      .catch(() => showToast("Could not load report families.", "error"));
  }, [session.sessionId]);
  async function exportFamily() {
    try {
      const blob = await downloadReportFamilyCsv(
        session.sessionId,
        reportType,
        fromDate || undefined,
        toDate || undefined,
      );
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `legacy-ehr-${reportType}-report.csv`;
      link.click();
      window.setTimeout(() => URL.revokeObjectURL(url), 0);
      showToast("Report CSV downloaded.", "success");
    } catch {
      showToast("Could not export the report CSV.", "error");
    }
  }

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <h1 className="clinician-page-title">Operational reports</h1>
        {state.status === "ready" && (
          <p className="clinician-page-subtitle">
            As of {state.data.asOfDate} · {state.data.currentYear}
          </p>
        )}
      </div>

      <GovernedReportDefinitions
        sessionId={session.sessionId}
        username={session.username}
      />
      <GovernedReportExecution
        sessionId={session.sessionId}
        username={session.username}
      />

      {state.status === "loading" && (
        <div className="cl-card">
          <div className="skeleton-list">
            {[0, 1, 2, 3].map((i) => (
              <div key={i} className="skeleton-row" style={{ height: 64 }} />
            ))}
          </div>
        </div>
      )}
      {state.status === "error" && (
        <div className="error-banner">{state.message}</div>
      )}
      {state.status === "ready" &&
        (() => {
          const { data } = state;
          const c = data.counts;
          return (
            <>
              <section className="cl-card">
                <div className="cl-card-header">
                  <div>
                    <h2 className="cl-card-title">
                      Local family exports
                    </h2>
                    <p className="cl-empty-text">
                      This compatibility path predates governed run evidence.
                      Use Governed report execution for revision-pinned,
                      policy-checked preview, history, checksum, and protected
                      download. This direct export remains local-only legacy
                      behavior.
                    </p>
                  </div>
                </div>
                <div className="cl-inline-form">
                  <label className="cl-admin-field">
                    <span>Family</span>
                    <select
                      className="ne-input"
                      value={reportType}
                      onChange={(event) => setReportType(event.target.value)}
                    >
                      {families.map((family) => (
                        <option key={family.key} value={family.key}>
                          {family.name}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label className="cl-admin-field">
                    <span>From</span>
                    <input
                      className="ne-input"
                      type="date"
                      value={fromDate}
                      onChange={(event) => setFromDate(event.target.value)}
                    />
                  </label>
                  <label className="cl-admin-field">
                    <span>To</span>
                    <input
                      className="ne-input"
                      type="date"
                      value={toDate}
                      onChange={(event) => setToDate(event.target.value)}
                    />
                  </label>
                  <div className="cl-inline-form-actions">
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      onClick={exportFamily}
                    >
                      Export CSV
                    </button>
                  </div>
                </div>
              </section>
              {/* Patients & portal */}
              <section className="cl-card">
                <div className="cl-card-header">
                  <h2 className="cl-card-title">Patients</h2>
                </div>
                <div className="cl-stats-grid">
                  <StatTile label="Total patients" value={c.patients} />
                  <StatTile label="Portal enrolled" value={c.portalPatients} />
                  <StatTile label="Providers" value={c.providers} />
                  <StatTile label="Facilities" value={c.facilities} />
                </div>
              </section>

              {/* Appointments */}
              <section className="cl-card">
                <div className="cl-card-header">
                  <h2 className="cl-card-title">Appointments & encounters</h2>
                </div>
                <div className="cl-stats-grid">
                  <StatTile label="All appointments" value={c.appointments} />
                  <StatTile label="Future" value={c.futureAppointments} />
                  <StatTile
                    label={`${data.currentYear} appointments`}
                    value={c.currentYearAppointments}
                  />
                  <StatTile label="All encounters" value={c.encounters} />
                  <StatTile
                    label={`${data.currentYear} encounters`}
                    value={c.currentYearEncounters}
                  />
                </div>
              </section>

              {/* Billing */}
              <section className="cl-card">
                <div className="cl-card-header">
                  <h2 className="cl-card-title">Billing</h2>
                </div>
                <div className="cl-stats-grid">
                  <StatTile label="Billing lines" value={c.billingLines} />
                  <StatTile
                    label="Total billed"
                    value={formatCurrency(c.billingTotal)}
                  />
                  <StatTile label="Lab reports" value={c.labReports} />
                  <StatTile label="Documents" value={c.patientDocuments} />
                  <StatTile label="Total messages" value={c.messages} />
                  <StatTile label="New messages" value={c.newMessages} />
                </div>
              </section>

              {/* Provider activity */}
              {data.providerActivity.length > 0 && (
                <section
                  className="cl-card"
                  style={{ padding: 0, overflow: "hidden" }}
                >
                  <div
                    className="cl-card-header"
                    style={{ padding: "16px 20px 12px" }}
                  >
                    <h2 className="cl-card-title">Provider activity</h2>
                  </div>
                  <table className="cl-table">
                    <thead>
                      <tr>
                        <th>Provider</th>
                        <th>Encounters</th>
                        <th>Billed</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.providerActivity.map((p) => (
                        <tr key={p.username}>
                          <td>{p.displayName}</td>
                          <td>{p.encounters.toLocaleString()}</td>
                          <td>{formatCurrency(p.billingTotal)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </section>
              )}

              {/* Facility activity */}
              {data.facilityActivity.length > 0 && (
                <section
                  className="cl-card"
                  style={{ padding: 0, overflow: "hidden" }}
                >
                  <div
                    className="cl-card-header"
                    style={{ padding: "16px 20px 12px" }}
                  >
                    <h2 className="cl-card-title">Facility activity</h2>
                  </div>
                  <table className="cl-table">
                    <thead>
                      <tr>
                        <th>Facility</th>
                        <th>Appointments</th>
                        <th>Encounters</th>
                        <th>Billed</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.facilityActivity.map((f) => (
                        <tr key={f.code}>
                          <td>{f.name}</td>
                          <td>{f.appointments.toLocaleString()}</td>
                          <td>{f.encounters.toLocaleString()}</td>
                          <td>{formatCurrency(f.billingTotal)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </section>
              )}

              {/* Clinical conditions */}
              {data.clinicalConditions.length > 0 && (
                <section
                  className="cl-card"
                  style={{ padding: 0, overflow: "hidden" }}
                >
                  <div
                    className="cl-card-header"
                    style={{ padding: "16px 20px 12px" }}
                  >
                    <h2 className="cl-card-title">Clinical conditions</h2>
                  </div>
                  <table className="cl-table">
                    <thead>
                      <tr>
                        <th>Condition</th>
                        <th>Code</th>
                        <th>Patients</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.clinicalConditions.map((cond) => (
                        <tr key={`${cond.title}-${cond.diagnosis}`}>
                          <td>{cond.title}</td>
                          <td className="cl-td-muted">{cond.diagnosis}</td>
                          <td>{cond.patients.toLocaleString()}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </section>
              )}
            </>
          );
        })()}
    </div>
  );
}
