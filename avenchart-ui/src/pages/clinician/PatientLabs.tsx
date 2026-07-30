import { useEffect, useState, type FormEvent } from "react";
import { useOutletContext } from "react-router-dom";
import { FlaskConical, Plus } from "lucide-react";
import {
  createProcedureOrder,
  createProcedureSpecimen,
  getProcedureOrderCatalog,
  getProcedureResults,
  isRequestCancellation,
  searchEncounters,
  transitionProcedureSpecimen,
  type ProcedureOrderCatalogItem,
  type ProcedureResultsResponse,
  type ProcedureSpecimenItem,
  type ProcedureSpecimenTransitionInput,
} from "../../api.ts";
import {
  LabResultFlag,
  labResultFlagClass,
} from "../../components/LabResultFlag.tsx";
import type { PatientOutletContext } from "./PatientShell.tsx";
import LabReportAndResultCapture from "./LabReportAndResultCapture.tsx";

type AsyncState<T> =
  | { status: "loading" }
  | { status: "ready"; data: T }
  | { status: "error"; message: string };

function formatDate(value?: string | null) {
  if (!value) return "-";
  const parsed = new Date(value);
  return Number.isNaN(parsed.valueOf()) ? value : parsed.toLocaleDateString();
}

function today() {
  return new Date().toISOString().slice(0, 10);
}

type SpecimenAction = ProcedureSpecimenTransitionInput["action"];

function specimenActions(status: string): SpecimenAction[] {
  switch (status.toLowerCase()) {
    case "collected":
    case "recollected":
      return ["label", "receive", "reject"];
    case "labeled":
      return ["receive", "reject"];
    case "received":
      return ["reject"];
    case "rejected":
      return ["recollect"];
    default:
      return [];
  }
}

function specimenActionLabel(action: string) {
  return action === "recollect"
    ? "Recollect"
    : `${action.charAt(0).toUpperCase()}${action.slice(1)}`;
}

function SpecimenLifecycleCard({
  specimen,
  onTransition,
}: {
  specimen: ProcedureSpecimenItem;
  onTransition: (
    specimen: ProcedureSpecimenItem,
    input: ProcedureSpecimenTransitionInput,
  ) => Promise<void>;
}) {
  const [action, setAction] = useState<SpecimenAction | null>(null);
  const [reason, setReason] = useState("");
  const [specimenIdentifier, setSpecimenIdentifier] = useState("");
  const [accessionIdentifier, setAccessionIdentifier] = useState("");
  const [collectedDate, setCollectedDate] = useState(today());
  const [conditionCode, setConditionCode] = useState("");
  const [specimenCondition, setSpecimenCondition] = useState("");
  const [comments, setComments] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const actions = specimenActions(specimen.specimenStatus);
  const title =
    specimen.specimenIdentifier ??
    specimen.accessionIdentifier ??
    `Specimen ${specimen.id}`;

  async function submitTransition(event: FormEvent) {
    event.preventDefault();
    if (!action || !reason.trim()) return;
    if (
      action === "recollect" &&
      !specimenIdentifier.trim() &&
      !accessionIdentifier.trim()
    ) {
      setError("Recollection requires a new specimen or accession identifier.");
      return;
    }

    setSaving(true);
    setError(null);
    try {
      await onTransition(specimen, {
        action,
        expectedVersion: specimen.specimenVersion,
        reason: reason.trim(),
        specimenIdentifier:
          action === "recollect" ? specimenIdentifier.trim() : null,
        accessionIdentifier:
          action === "recollect" ? accessionIdentifier.trim() : null,
        collectedDate:
          action === "recollect" ? `${collectedDate}T12:00:00` : null,
        conditionCode: action === "recollect" ? conditionCode.trim() : null,
        specimenCondition:
          action === "recollect" ? specimenCondition.trim() : null,
        comments: action === "recollect" ? comments.trim() : null,
      });
      setAction(null);
      setReason("");
      setSpecimenIdentifier("");
      setAccessionIdentifier("");
      setCollectedDate(today());
      setConditionCode("");
      setSpecimenCondition("");
      setComments("");
    } catch (transitionError) {
      setError(
        transitionError instanceof Error
          ? transitionError.message
          : "Could not change specimen status.",
      );
    } finally {
      setSaving(false);
    }
  }

  return (
    <article className="cl-specimen-card">
      <div className="cl-card-header">
        <div>
          <h3>{title}</h3>
          <p className="cl-table-sub">
            Collected {formatDate(specimen.collectedDate)}
            {specimen.specimenType ? ` · ${specimen.specimenType}` : ""}
            {specimen.accessionIdentifier
              ? ` · accession ${specimen.accessionIdentifier}`
              : ""}
          </p>
        </div>
        <span className="cl-badge">
          {specimen.specimenStatus} · v{specimen.specimenVersion}
        </span>
      </div>

      {(specimen.specimenCondition || specimen.comments) && (
        <p className="cl-table-sub">
          {[specimen.specimenCondition, specimen.comments]
            .filter(Boolean)
            .join(" · ")}
        </p>
      )}

      <div className="ne-actions" aria-label={`Actions for ${title}`}>
        {actions.map((availableAction) => (
          <button
            className="cl-btn-secondary"
            key={availableAction}
            type="button"
            disabled={saving}
            onClick={() =>
              setAction((current) =>
                current === availableAction ? null : availableAction,
              )
            }
          >
            {specimenActionLabel(availableAction)}
          </button>
        ))}
      </div>

      {action && (
        <form
          className="cl-specimen-transition"
          onSubmit={(event) => void submitTransition(event)}
        >
          <p className="cl-table-sub">
            {specimenActionLabel(action)} from {specimen.specimenStatus}. The
            authenticated staff member and reason will be recorded.
          </p>
          {action === "recollect" && (
            <div className="cl-admin-form-grid">
              <label className="cl-admin-field">
                <span>New specimen identifier</span>
                <input
                  className="ne-input"
                  value={specimenIdentifier}
                  maxLength={255}
                  onChange={(event) =>
                    setSpecimenIdentifier(event.target.value)
                  }
                />
              </label>
              <label className="cl-admin-field">
                <span>New accession identifier</span>
                <input
                  className="ne-input"
                  value={accessionIdentifier}
                  maxLength={255}
                  onChange={(event) =>
                    setAccessionIdentifier(event.target.value)
                  }
                />
              </label>
              <label className="cl-admin-field">
                <span>Recollected date</span>
                <input
                  className="ne-input"
                  type="date"
                  value={collectedDate}
                  required
                  onChange={(event) => setCollectedDate(event.target.value)}
                />
              </label>
              <label className="cl-admin-field">
                <span>Condition code</span>
                <input
                  className="ne-input"
                  value={conditionCode}
                  maxLength={100}
                  onChange={(event) => setConditionCode(event.target.value)}
                />
              </label>
              <label className="cl-admin-field">
                <span>Specimen condition</span>
                <input
                  className="ne-input"
                  value={specimenCondition}
                  maxLength={255}
                  onChange={(event) =>
                    setSpecimenCondition(event.target.value)
                  }
                />
              </label>
              <label className="cl-admin-field">
                <span>Recollection comments</span>
                <input
                  className="ne-input"
                  value={comments}
                  maxLength={1000}
                  onChange={(event) => setComments(event.target.value)}
                />
              </label>
            </div>
          )}
          <label className="cl-admin-field">
            <span>Reason</span>
            <textarea
              className="ne-input"
              value={reason}
              required
              maxLength={500}
              rows={2}
              onChange={(event) => setReason(event.target.value)}
            />
          </label>
          {error && (
            <div className="error-banner" role="alert">
              {error}
            </div>
          )}
          <div className="ne-actions">
            <button
              className="cl-btn-primary"
              type="submit"
              disabled={
                saving ||
                !reason.trim() ||
                (action === "recollect" &&
                  !specimenIdentifier.trim() &&
                  !accessionIdentifier.trim())
              }
            >
              {saving
                ? "Saving…"
                : `Confirm ${specimenActionLabel(action)}`}
            </button>
            <button
              className="cl-btn-secondary"
              type="button"
              disabled={saving}
              onClick={() => setAction(null)}
            >
              Cancel
            </button>
          </div>
        </form>
      )}

      <details className="cl-result-history">
        <summary>
          {specimen.historyCount} lifecycle{" "}
          {specimen.historyCount === 1 ? "event" : "events"}
        </summary>
        {specimen.history.length === 0 ? (
          <p className="cl-table-sub">History is not loaded in this view.</p>
        ) : (
          <ol className="cl-specimen-history">
            {specimen.history.map((historyEvent) => (
              <li key={historyEvent.eventId}>
                <strong>{specimenActionLabel(historyEvent.action)}</strong>
                {" · "}
                {historyEvent.previousStatus
                  ? `${historyEvent.previousStatus} → ${historyEvent.currentStatus}`
                  : historyEvent.currentStatus}
                {" · "}
                {formatDate(historyEvent.occurredAt)}
                {" · "}
                {historyEvent.actor}
                {" · "}
                {historyEvent.reason}
                {" · "}v{historyEvent.resultingVersion}
                {(historyEvent.specimenIdentifier ||
                  historyEvent.accessionIdentifier) &&
                  ` · ${historyEvent.specimenIdentifier ?? historyEvent.accessionIdentifier}`}
              </li>
            ))}
          </ol>
        )}
      </details>
    </article>
  );
}

export default function PatientLabs() {
  const { session, patientId } = useOutletContext<PatientOutletContext>();
  const [loadAttempt, setLoadAttempt] = useState(0);
  const [state, setState] = useState<
    AsyncState<ProcedureResultsResponse>
  >({ status: "loading" });
  const [catalog, setCatalog] = useState<ProcedureOrderCatalogItem[]>([]);
  const [encounters, setEncounters] = useState<
    Array<{ encounter: number; date: string; reason?: string | null }>
  >([]);
  const [orderForm, setOrderForm] = useState({
    encounterId: "",
    catalogId: "",
    priority: "routine",
    diagnosis: "",
    instructions: "",
  });
  const [specimenForm, setSpecimenForm] = useState({
    orderId: "",
    specimenIdentifier: "",
    accessionIdentifier: "",
    specimenType: "",
    collectedDate: today(),
    comments: "",
  });
  const [savingOrder, setSavingOrder] = useState(false);
  const [savingSpecimen, setSavingSpecimen] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    setState({ status: "loading" });
    Promise.all([
      getProcedureResults(session.sessionId, patientId, controller.signal),
      getProcedureOrderCatalog(session.sessionId, controller.signal),
      searchEncounters(
        session.sessionId,
        { patientId, limit: 100 },
        controller.signal,
      ),
    ])
      .then(([data, orderCatalog, encounterResponse]) => {
        setState({ status: "ready", data });
        setCatalog(
          orderCatalog.items.filter(
            (item) => item.itemType === "ord" && item.active,
          ),
        );
        setEncounters(encounterResponse.encounters);
      })
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

  const selectedCatalogItem = catalog.find(
    (item) => item.id === Number(orderForm.catalogId),
  );

  async function submitOrder(event: FormEvent) {
    event.preventDefault();
    const encounterId = Number(orderForm.encounterId);
    if (!selectedCatalogItem || !Number.isInteger(encounterId)) {
      return;
    }
    setSavingOrder(true);
    try {
      const detail = await createProcedureOrder(session.sessionId, {
        patientId,
        encounterId,
        providerId: null,
        labId: selectedCatalogItem.labId ?? null,
        dateOrdered: today(),
        priority: orderForm.priority,
        status: "pending",
        procedureCode: selectedCatalogItem.code ?? "",
        procedureName: selectedCatalogItem.name,
        procedureType: selectedCatalogItem.procedureTypeName ?? "laboratory",
        diagnosis: orderForm.diagnosis.trim(),
        instructions: orderForm.instructions.trim(),
      });
      setState({ status: "ready", data: detail });
      setOrderForm((current) => ({ ...current, catalogId: "", diagnosis: "", instructions: "" }));
      setLoadAttempt((attempt) => attempt + 1);
    } catch (error) {
      setState({ status: "error", message: error instanceof Error ? error.message : "Could not save the local lab order." });
    } finally {
      setSavingOrder(false);
    }
  }

  async function submitSpecimen(event: FormEvent) {
    event.preventDefault();
    const orderId = Number(specimenForm.orderId);
    if (!Number.isInteger(orderId) || (!specimenForm.specimenIdentifier.trim() && !specimenForm.accessionIdentifier.trim())) return;
    setSavingSpecimen(true);
    try {
      const detail = await createProcedureSpecimen(session.sessionId, {
        orderId,
        specimenIdentifier: specimenForm.specimenIdentifier.trim(),
        accessionIdentifier: specimenForm.accessionIdentifier.trim(),
        specimenTypeCode: "",
        specimenType: specimenForm.specimenType.trim(),
        collectionMethodCode: "",
        collectionMethod: "",
        specimenLocationCode: "",
        specimenLocation: "",
        collectedDate: `${specimenForm.collectedDate}T12:00:00`,
        volumeValue: null,
        volumeUnit: "",
        conditionCode: "",
        specimenCondition: "",
        comments: specimenForm.comments.trim(),
      });
      setState({ status: "ready", data: detail });
      setSpecimenForm({ orderId: "", specimenIdentifier: "", accessionIdentifier: "", specimenType: "", collectedDate: today(), comments: "" });
      setLoadAttempt((attempt) => attempt + 1);
    } catch (error) {
      setState({ status: "error", message: error instanceof Error ? error.message : "Could not record the local specimen." });
    } finally {
      setSavingSpecimen(false);
    }
  }

  async function transitionSpecimen(
    specimen: ProcedureSpecimenItem,
    input: ProcedureSpecimenTransitionInput,
  ) {
    const detail = await transitionProcedureSpecimen(
      session.sessionId,
      specimen.id,
      input,
    );
    setState({ status: "ready", data: detail });
  }

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

      {state.status === "ready" && (
        <>
          <section className="cl-card">
            <div className="cl-card-header">
              <div>
                <h2 className="cl-card-title">Add local lab order</h2>
                <p className="cl-table-sub">Choose an active local catalog order and an existing encounter. This records a local order only; it does not transmit to a lab.</p>
              </div>
              <FlaskConical size={20} aria-hidden="true" />
            </div>
            <form className="cl-admin-form-grid" onSubmit={(event) => void submitOrder(event)}>
              <label className="cl-admin-field"><span>Encounter</span><select className="ne-input" value={orderForm.encounterId} required onChange={(event) => setOrderForm((current) => ({ ...current, encounterId: event.target.value }))}><option value="">Select encounter</option>{encounters.map((encounter) => <option key={encounter.encounter} value={encounter.encounter}>{encounter.date} · {encounter.reason ?? `Encounter ${encounter.encounter}`}</option>)}</select></label>
              <label className="cl-admin-field"><span>Catalog order</span><select className="ne-input" value={orderForm.catalogId} required onChange={(event) => setOrderForm((current) => ({ ...current, catalogId: event.target.value }))}><option value="">Select local order</option>{catalog.map((item) => <option key={item.id} value={item.id}>{item.code ?? "No code"} · {item.name}</option>)}</select></label>
              <label className="cl-admin-field"><span>Priority</span><select className="ne-input" value={orderForm.priority} onChange={(event) => setOrderForm((current) => ({ ...current, priority: event.target.value }))}><option value="routine">Routine</option><option value="urgent">Urgent</option><option value="stat">STAT</option></select></label>
              <label className="cl-admin-field"><span>Diagnosis / reason</span><input className="ne-input" value={orderForm.diagnosis} required maxLength={255} onChange={(event) => setOrderForm((current) => ({ ...current, diagnosis: event.target.value }))} /></label>
              <label className="cl-admin-field"><span>Instructions</span><input className="ne-input" value={orderForm.instructions} maxLength={1000} onChange={(event) => setOrderForm((current) => ({ ...current, instructions: event.target.value }))} /></label>
              <div className="ne-actions"><button className="cl-btn-primary" type="submit" disabled={savingOrder || !selectedCatalogItem || !orderForm.encounterId}><Plus size={15} aria-hidden="true" />{savingOrder ? "Saving." : "Save local order"}</button></div>
            </form>
            {catalog.length === 0 && <p className="cl-empty-text">No active local catalog orders are available. Add one in Lab Order Catalog before creating an order.</p>}
          </section>

          <LabReportAndResultCapture
            sessionId={session.sessionId}
            orders={state.data.orders}
            onChange={(data) => {
              setState({ status: "ready", data });
              setLoadAttempt((attempt) => attempt + 1);
            }}
          />

          <section className="cl-card">
            <h2 className="cl-card-title">Record local specimen</h2>
            <p className="cl-table-sub">Specimen capture is local evidence only. No barcode, label printer, courier, or laboratory accession integration is claimed.</p>
            <form className="cl-admin-form-grid" onSubmit={(event) => void submitSpecimen(event)}>
              <label className="cl-admin-field"><span>Order</span><select className="ne-input" value={specimenForm.orderId} required onChange={(event) => setSpecimenForm((current) => ({ ...current, orderId: event.target.value }))}><option value="">Select local order</option>{state.data.orders.map((order) => <option key={order.id} value={order.id}>{order.code ?? "No code"} · {order.name ?? `Order ${order.id}`}</option>)}</select></label>
              <label className="cl-admin-field"><span>Specimen identifier</span><input className="ne-input" value={specimenForm.specimenIdentifier} maxLength={255} onChange={(event) => setSpecimenForm((current) => ({ ...current, specimenIdentifier: event.target.value }))} /></label>
              <label className="cl-admin-field"><span>Accession identifier</span><input className="ne-input" value={specimenForm.accessionIdentifier} maxLength={255} onChange={(event) => setSpecimenForm((current) => ({ ...current, accessionIdentifier: event.target.value }))} /></label>
              <label className="cl-admin-field"><span>Specimen type</span><input className="ne-input" value={specimenForm.specimenType} maxLength={255} onChange={(event) => setSpecimenForm((current) => ({ ...current, specimenType: event.target.value }))} /></label>
              <label className="cl-admin-field"><span>Collected date</span><input className="ne-input" type="date" value={specimenForm.collectedDate} required onChange={(event) => setSpecimenForm((current) => ({ ...current, collectedDate: event.target.value }))} /></label>
              <label className="cl-admin-field"><span>Comments</span><input className="ne-input" value={specimenForm.comments} maxLength={1000} onChange={(event) => setSpecimenForm((current) => ({ ...current, comments: event.target.value }))} /></label>
              <div className="ne-actions"><button className="cl-btn-primary" type="submit" disabled={savingSpecimen || !specimenForm.orderId || (!specimenForm.specimenIdentifier.trim() && !specimenForm.accessionIdentifier.trim())}><Plus size={15} aria-hidden="true" />{savingSpecimen ? "Saving." : "Record specimen"}</button></div>
            </form>
          </section>
        </>
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

              {order.specimens.length > 0 && (
                <div className="cl-specimen-list">
                  {order.specimens.map((specimen) => (
                    <SpecimenLifecycleCard
                      key={specimen.id}
                      specimen={specimen}
                      onTransition={transitionSpecimen}
                    />
                  ))}
                </div>
              )}

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
                                  {result.result ?? "-"}{" "}
                                  {result.units ?? ""}
                                  <LabResultFlag value={result.abnormal} />
                                </td>
                                <td>{result.range ?? "-"}</td>
                                <td>{result.resultStatus ?? "-"}</td>
                                <td>
                                  {formatDate(result.resultDate)}
                                  {result.hasPriorVersions && (
                                    <details className="cl-result-history">
                                      <summary>
                                        {result.versionHistoryCount - 1} prior local {result.versionHistoryCount === 2 ? "version" : "versions"}
                                      </summary>
                                      <ul>
                                        {result.versionHistory.map((version) => (
                                          <li key={`${result.id}-${version.version}`}>
                                            <strong>{version.versionLabel}</strong>{" "}
                                            {version.result ?? "-"} {version.units ?? ""}
                                            {version.range ? ` · ${version.range}` : ""}
                                            {version.abnormal ? ` · ${version.abnormal}` : ""}
                                            {" · "}{formatDate(version.capturedAt)}
                                            {version.correctionActor ? ` · corrected by ${version.correctionActor}` : ""}
                                            {version.correctionReason ? ` · ${version.correctionReason}` : ""}
                                            {version.resultingVersion ? ` · became Version ${version.resultingVersion}` : ""}
                                          </li>
                                        ))}
                                      </ul>
                                    </details>
                                  )}
                                </td>
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
