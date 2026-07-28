import { useEffect, useEffectEvent, useState } from "react";
import { Search, Send, WalletCards } from "lucide-react";
import {
  createBillingCollectionsFollowUp,
  dispatchBillingStatementBatch,
  getBillingCollectionsWorkQueue,
  getBillingStatementBatch,
  getPatientBilling,
  type CollectionsWorkQueueResponse,
  type PatientBillingResponse,
  type StatementBatchResponse,
} from "../../api.ts";
import { showToast } from "../../components/Toast.tsx";
import type { ClinicianOutletContext } from "./ClinicianShell.tsx";
import { useOutletContext, useSearchParams } from "react-router-dom";

function money(value: number) {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
  }).format(value);
}

export default function BillingWorkspace() {
  const { session } = useOutletContext<ClinicianOutletContext>();
  const [searchParams, setSearchParams] = useSearchParams();
  const requestedPatientId = searchParams.get("patientId")?.trim() ?? "";
  const [patientIdInput, setPatientIdInput] = useState(requestedPatientId);
  const [patientAccount, setPatientAccount] =
    useState<PatientBillingResponse | null>(null);
  const [patientAccountLoading, setPatientAccountLoading] = useState(false);
  const [patientAccountError, setPatientAccountError] = useState<string | null>(
    null,
  );
  const [batch, setBatch] = useState<StatementBatchResponse | null>(null);
  const [collections, setCollections] =
    useState<CollectionsWorkQueueResponse | null>(null);
  const [batchError, setBatchError] = useState<string | null>(null);
  const [dispatching, setDispatching] = useState(false);
  const [followUpPatientId, setFollowUpPatientId] = useState<string | null>(
    null,
  );
  const [followUpNote, setFollowUpNote] = useState("");
  const [savingFollowUp, setSavingFollowUp] = useState(false);

  function load() {
    setBatchError(null);
    getBillingStatementBatch(session.sessionId, 10)
      .then(setBatch)
      .catch(() => setBatchError("Could not load statement candidates."));
    getBillingCollectionsWorkQueue(session.sessionId, 10)
      .then(setCollections)
      .catch(() => {});
  }

  useEffect(() => {
    load(); // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function loadPatientAccount(patientId: string) {
    const normalizedPatientId = patientId.trim();
    if (!normalizedPatientId) {
      setPatientAccount(null);
      setPatientAccountError(null);
      return;
    }
    setPatientAccountLoading(true);
    setPatientAccountError(null);
    try {
      setPatientAccount(
        await getPatientBilling(session.sessionId, normalizedPatientId),
      );
    } catch {
      setPatientAccount(null);
      setPatientAccountError(
        "Could not load this patient account. Check the patient ID and try again.",
      );
    } finally {
      setPatientAccountLoading(false);
    }
  }

  const loadRequestedPatientAccount = useEffectEvent(loadPatientAccount);
  useEffect(() => {
    setPatientIdInput(requestedPatientId);
    void loadRequestedPatientAccount(requestedPatientId);
  }, [requestedPatientId]);

  function selectPatientAccount(patientId: string) {
    setPatientIdInput(patientId);
    setSearchParams(patientId ? { patientId } : {}, { replace: true });
    void loadPatientAccount(patientId);
  }

  async function dispatch() {
    if (
      !batch ||
      dispatching ||
      !window.confirm(`Dispatch ${batch.candidateCount} statement candidates?`)
    )
      return;
    setDispatching(true);
    try {
      const result = await dispatchBillingStatementBatch(session.sessionId, 10);
      showToast(
        `${result.dispatchedStatementCount} statements dispatched (${result.emailQueueCount} email, ${result.printQueueCount} print).`,
        "success",
      );
      load();
    } catch {
      showToast("Could not dispatch the statement batch.", "error");
    } finally {
      setDispatching(false);
    }
  }

  async function createFollowUp() {
    if (
      !followUpPatientId ||
      savingFollowUp ||
      !followUpNote.trim() ||
      !window.confirm("Create this local collections follow-up?")
    )
      return;
    setSavingFollowUp(true);
    try {
      await createBillingCollectionsFollowUp(session.sessionId, {
        patientId: followUpPatientId,
        assignedTo: session.username,
        action: "follow-up",
        note: followUpNote.trim(),
      });
      showToast("Collections follow-up created.", "success");
      setFollowUpPatientId(null);
      setFollowUpNote("");
      load();
    } catch {
      showToast("Could not create the collections follow-up.", "error");
    } finally {
      setSavingFollowUp(false);
    }
  }

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <div>
          <h1 className="clinician-page-title">Billing</h1>
          <p className="clinician-page-subtitle">
            Statement candidates and local dispatch readiness.
          </p>
        </div>
        <button
          className="cl-btn-primary"
          type="button"
          disabled={!batch || dispatching}
          onClick={dispatch}
        >
          <Send size={15} />{" "}
          {dispatching ? "Dispatching..." : "Dispatch statements"}
        </button>
      </div>
      <section className="cl-card billing-account-search">
        <div>
          <h2 className="cl-card-title">Patient account</h2>
          <p className="cl-empty-text">
            Open balances, aging, statement readiness, and immutable ledger
            provenance by canonical or public patient ID.
          </p>
        </div>
        <form
          className="billing-account-search-form"
          onSubmit={(event) => {
            event.preventDefault();
            selectPatientAccount(patientIdInput);
          }}
        >
          <div className="field">
            <label className="label" htmlFor="billing-patient-id">
              Patient ID
            </label>
            <input
              id="billing-patient-id"
              className="input"
              value={patientIdInput}
              onChange={(event) => setPatientIdInput(event.target.value)}
              placeholder="MOD-PAT-0004"
            />
          </div>
          <button
            className="cl-btn-primary"
            type="submit"
            disabled={patientAccountLoading || !patientIdInput.trim()}
          >
            <Search size={15} />
            {patientAccountLoading ? "Loading…" : "Open account"}
          </button>
        </form>
      </section>
      {patientAccountError && (
        <div className="error-banner" role="alert">
          {patientAccountError}
          <button
            className="cl-link"
            type="button"
            onClick={() => void loadPatientAccount(patientIdInput)}
          >
            Retry
          </button>
        </div>
      )}
      {patientAccountLoading && !patientAccount && (
        <section className="cl-card" aria-label="Loading patient account">
          <div className="skeleton-list">
            {[0, 1, 2].map((item) => (
              <div className="skeleton-row" key={item} style={{ height: 48 }} />
            ))}
          </div>
        </section>
      )}
      {patientAccount && (
        <section
          className="billing-account-detail"
          id="patient-account"
          aria-label="Patient account summary"
        >
          <section className="cl-card">
            <div className="cl-card-header">
              <div>
                <h2 className="cl-card-title">
                  {patientAccount.patientDisplayName}
                </h2>
                <p className="cl-empty-text">
                  {patientAccount.pubpid} · As of{" "}
                  {patientAccount.agingSummary.asOfDate}
                </p>
              </div>
              <span className="cl-badge cl-badge-muted">
                {patientAccount.statementSummary.statementStatus}
              </span>
            </div>
            <div className="cl-stats-grid">
              <div className="cl-stat-tile">
                <span className="cl-stat-tile-value">
                  {money(patientAccount.accountSummary.chargeAmount)}
                </span>
                <span className="cl-stat-tile-label">Charges</span>
              </div>
              <div className="cl-stat-tile">
                <span className="cl-stat-tile-value">
                  {money(patientAccount.accountSummary.paymentAmount)}
                </span>
                <span className="cl-stat-tile-label">Payments</span>
              </div>
              <div className="cl-stat-tile">
                <span className="cl-stat-tile-value">
                  {money(patientAccount.accountSummary.adjustmentAmount)}
                </span>
                <span className="cl-stat-tile-label">Adjustments</span>
              </div>
              <div className="cl-stat-tile">
                <span className="cl-stat-tile-value">
                  {money(patientAccount.accountSummary.balanceAmount)}
                </span>
                <span className="cl-stat-tile-label">Account balance</span>
              </div>
            </div>
          </section>
          <div className="billing-account-columns">
            <section className="cl-card">
              <div className="cl-card-header">
                <h3 className="cl-card-title">Aging</h3>
                <span className="cl-badge cl-badge-muted">
                  {money(patientAccount.agingSummary.totalBalanceAmount)}
                </span>
              </div>
              <dl className="billing-account-facts">
                <div>
                  <dt>Current</dt>
                  <dd>{money(patientAccount.agingSummary.currentAmount)}</dd>
                </div>
                <div>
                  <dt>31–60 days</dt>
                  <dd>{money(patientAccount.agingSummary.days31To60Amount)}</dd>
                </div>
                <div>
                  <dt>61–90 days</dt>
                  <dd>{money(patientAccount.agingSummary.days61To90Amount)}</dd>
                </div>
                <div>
                  <dt>Over 90 days</dt>
                  <dd>{money(patientAccount.agingSummary.over90Amount)}</dd>
                </div>
              </dl>
            </section>
            <section className="cl-card">
              <div className="cl-card-header">
                <h3 className="cl-card-title">Statement readiness</h3>
                <span className="cl-badge cl-badge-muted">
                  {patientAccount.statementSummary.statementStatus}
                </span>
              </div>
              <dl className="billing-account-facts">
                <div>
                  <dt>Statement date</dt>
                  <dd>{patientAccount.statementSummary.statementDate}</dd>
                </div>
                <div>
                  <dt>Due date</dt>
                  <dd>{patientAccount.statementSummary.dueDate}</dd>
                </div>
                <div>
                  <dt>Past due</dt>
                  <dd>
                    {money(patientAccount.statementSummary.pastDueAmount)}
                  </dd>
                </div>
                <div>
                  <dt>Balance due</dt>
                  <dd>
                    {money(patientAccount.statementSummary.balanceDueAmount)}
                  </dd>
                </div>
                <div>
                  <dt>Oldest open</dt>
                  <dd>
                    {patientAccount.statementSummary.oldestOpenDate} (
                    {patientAccount.statementSummary.oldestOpenAgeDays} days)
                  </dd>
                </div>
                <div>
                  <dt>Open encounters</dt>
                  <dd>{patientAccount.statementSummary.openEncounterCount}</dd>
                </div>
              </dl>
            </section>
          </div>
          <section className="cl-card">
            <div className="cl-card-header">
              <div>
                <h3 className="cl-card-title">Account ledger</h3>
                <p className="cl-empty-text">
                  {patientAccount.ledgerSummary.entryCount} entries ·{" "}
                  {patientAccount.ledgerSummary.firstEntryDate ??
                    "No first date"}{" "}
                  through{" "}
                  {patientAccount.ledgerSummary.lastEntryDate ?? "No last date"}
                </p>
              </div>
              <span className="cl-badge cl-badge-muted">
                Ending {money(patientAccount.ledgerSummary.endingBalanceAmount)}
              </span>
            </div>
            <div
              className="cl-table-scroll"
              role="region"
              aria-label="Patient billing ledger"
              tabIndex={0}
            >
              <table className="cl-table">
                <thead>
                  <tr>
                    <th>Date</th>
                    <th>Type</th>
                    <th>Description</th>
                    <th>Encounter / reference</th>
                    <th>Amount</th>
                    <th>Running balance</th>
                  </tr>
                </thead>
                <tbody>
                  {patientAccount.ledgerEntries.map((entry) => (
                    <tr key={entry.entryId}>
                      <td>{entry.entryDate}</td>
                      <td>{entry.entryType}</td>
                      <td>
                        {entry.description}
                        {entry.code && (
                          <p className="cl-table-sub">{entry.code}</p>
                        )}
                      </td>
                      <td>
                        #{entry.encounter}
                        {entry.reference && (
                          <p className="cl-table-sub">{entry.reference}</p>
                        )}
                      </td>
                      <td>{money(entry.amount)}</td>
                      <td>{money(entry.runningBalanceAmount)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {patientAccount.ledgerEntries.length === 0 && (
              <p className="cl-empty-text">
                No ledger entries are recorded for this patient.
              </p>
            )}
          </section>
        </section>
      )}
      {batchError && <div className="error-banner">{batchError}</div>}
      {!batch && !batchError && (
        <div className="cl-card">
          <div className="skeleton-list">
            {[0, 1, 2].map((i) => (
              <div key={i} className="skeleton-row" style={{ height: 62 }} />
            ))}
          </div>
        </div>
      )}
      {batch && (
        <>
          <section className="cl-card">
            <div className="cl-card-header">
              <h2 className="cl-card-title">
                <WalletCards size={16} /> Statement batch
              </h2>
              <span className="cl-badge cl-badge-muted">
                As of {batch.asOfDate}
              </span>
            </div>
            <div className="cl-stats-grid">
              <div className="cl-stat-tile">
                <span className="cl-stat-tile-value">
                  {batch.candidateCount}
                </span>
                <span className="cl-stat-tile-label">Candidates</span>
              </div>
              <div className="cl-stat-tile">
                <span className="cl-stat-tile-value">
                  {money(batch.totalBalanceAmount)}
                </span>
                <span className="cl-stat-tile-label">Balance due</span>
              </div>
              <div className="cl-stat-tile">
                <span className="cl-stat-tile-value">
                  {money(batch.totalPastDueAmount)}
                </span>
                <span className="cl-stat-tile-label">Past due</span>
              </div>
            </div>
          </section>
          <section
            className="cl-card"
            style={{ padding: 0, overflow: "hidden" }}
          >
            <table className="cl-table">
              <thead>
                <tr>
                  <th>Patient</th>
                  <th>Statement</th>
                  <th>Due</th>
                  <th>Balance</th>
                  <th>Delivery</th>
                </tr>
              </thead>
              <tbody>
                {batch.candidates.map((candidate) => (
                  <tr key={candidate.patientId}>
                    <td>
                      <button
                        className="cl-link"
                        type="button"
                        onClick={() =>
                          selectPatientAccount(candidate.patientId)
                        }
                      >
                        {candidate.patientDisplayName}
                      </button>
                      <p className="cl-table-sub">{candidate.pubpid}</p>
                    </td>
                    <td>
                      {candidate.statementNumber}
                      <p className="cl-table-sub">
                        {candidate.statementStatus}
                      </p>
                    </td>
                    <td>
                      {candidate.dueDate}
                      <p className="cl-table-sub">
                        {candidate.oldestOpenAgeDays} days open
                      </p>
                    </td>
                    <td>{money(candidate.balanceDueAmount)}</td>
                    <td className="cl-td-muted">{candidate.deliveryMethod}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            {batch.candidates.length === 0 && (
              <p className="cl-empty-text">
                No statement candidates are ready.
              </p>
            )}
          </section>
          {collections && (
            <section
              className="cl-card"
              style={{ padding: 0, overflow: "hidden" }}
            >
              <div
                className="cl-card-header"
                style={{ padding: "16px 20px 12px" }}
              >
                <h2 className="cl-card-title">Collections queue</h2>
                <span className="cl-badge cl-badge-muted">
                  {collections.highPriorityCount} high priority
                </span>
              </div>
              <table className="cl-table">
                <thead>
                  <tr>
                    <th>Patient</th>
                    <th>Tier</th>
                    <th>Recommended action</th>
                    <th>Past due</th>
                    <th>Over 90</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {collections.items.map((item) => (
                    <tr key={item.patientId}>
                      <td>
                        <button
                          className="cl-link"
                          type="button"
                          onClick={() => selectPatientAccount(item.patientId)}
                        >
                          {item.patientDisplayName}
                        </button>
                        <p className="cl-table-sub">{item.pubpid}</p>
                      </td>
                      <td>{item.collectionTier}</td>
                      <td className="cl-td-muted">{item.recommendedAction}</td>
                      <td>{money(item.pastDueAmount)}</td>
                      <td>{money(item.over90Amount)}</td>
                      <td>
                        <button
                          className="cl-btn-secondary"
                          type="button"
                          onClick={() => {
                            setFollowUpPatientId(item.patientId);
                            setFollowUpNote(item.recommendedAction);
                          }}
                        >
                          Follow up
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {followUpPatientId && (
                <div className="cl-inline-form" style={{ margin: 16 }}>
                  <label className="cl-admin-field">
                    <span>Follow-up note</span>
                    <textarea
                      className="ne-input"
                      value={followUpNote}
                      onChange={(event) => setFollowUpNote(event.target.value)}
                      required
                    />
                  </label>
                  <div className="cl-inline-form-actions">
                    <button
                      className="cl-btn-primary"
                      type="button"
                      disabled={savingFollowUp || !followUpNote.trim()}
                      onClick={createFollowUp}
                    >
                      {savingFollowUp ? "Saving..." : "Create follow-up"}
                    </button>
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      onClick={() => setFollowUpPatientId(null)}
                    >
                      Cancel
                    </button>
                  </div>
                </div>
              )}
              {collections.items.length === 0 && (
                <p className="cl-empty-text">
                  No accounts need collections follow-up.
                </p>
              )}
            </section>
          )}
        </>
      )}
    </div>
  );
}
