import { useEffect, useState } from "react";
import { useOutletContext } from "react-router-dom";
import { AlertTriangle, Plus, Search, X } from "lucide-react";
import {
  getClinicalLists,
  createProblem,
  deactivateProblem,
  deleteProblem,
  createAllergy,
  deactivateAllergy,
  deleteAllergy,
  createMedication,
  deactivateMedication,
  restoreMedication,
  createImmunization,
  createPrescription,
  deleteImmunization,
  markImmunizationEnteredInError,
  searchClinicalMedicationVocabulary,
  type ClinicalListsResponse,
  type MedicationVocabularyItem,
} from "../../api.ts";
import { showToast } from "../../components/Toast.tsx";
import type { PatientOutletContext } from "./PatientShell.tsx";

type AsyncState<T> =
  | { status: "loading" }
  | { status: "ready"; data: T }
  | { status: "error"; message: string };

function statusDot(activity: number) {
  return (
    <span
      role="img"
      className={`cl-activity-dot ${activity === 1 ? "cl-activity-active" : "cl-activity-inactive"}`}
      aria-label={activity === 1 ? "Active" : "Inactive"}
    />
  );
}

function today() {
  return new Date().toISOString().slice(0, 10);
}

type AddMode =
  | "problem"
  | "allergy"
  | "medication"
  | "prescription"
  | "immunization"
  | null;

type VocabularyState =
  | { status: "idle" }
  | { status: "loading" }
  | { status: "ready"; items: MedicationVocabularyItem[] }
  | { status: "error"; message: string };

type LifecycleTarget = {
  type: "problem" | "allergy" | "medication" | "immunization";
  action: "deactivate" | "delete" | "entered-in-error" | "restore";
  id: string;
  title: string;
  expectedVersion?: number;
};

export default function PatientChart() {
  const { session, patientId } = useOutletContext<PatientOutletContext>();
  const [state, setState] = useState<AsyncState<ClinicalListsResponse>>({
    status: "loading",
  });
  const [addMode, setAddMode] = useState<AddMode>(null);
  const [working, setWorking] = useState(false);
  const [lifecycleTarget, setLifecycleTarget] =
    useState<LifecycleTarget | null>(null);
  const [lifecycleReason, setLifecycleReason] = useState("");

  // Add-problem form state
  const [newProbTitle, setNewProbTitle] = useState("");
  const [newProbDx, setNewProbDx] = useState("");
  const [newProbDate, setNewProbDate] = useState(today());
  const [newProbComments, setNewProbComments] = useState("");

  // Add-allergy form state
  const [newAllergyTitle, setNewAllergyTitle] = useState("");
  const [newAllergyReaction, setNewAllergyReaction] = useState("");
  const [newAllergySeverity, setNewAllergySeverity] = useState("mild");
  const [newAllergyDate, setNewAllergyDate] = useState(today());
  const [newAllergyComments, setNewAllergyComments] = useState("");

  // Add-medication form state
  const [newMedTitle, setNewMedTitle] = useState("");
  const [newMedDx, setNewMedDx] = useState("");
  const [newMedDate, setNewMedDate] = useState(today());
  const [newMedComments, setNewMedComments] = useState("");

  // Add-prescription form state
  const [rxQuery, setRxQuery] = useState("");
  const [rxVocabulary, setRxVocabulary] = useState<VocabularyState>({
    status: "idle",
  });
  const [selectedRx, setSelectedRx] =
    useState<MedicationVocabularyItem | null>(null);
  const [newRxStartDate, setNewRxStartDate] = useState(today());
  const [newRxDosage, setNewRxDosage] = useState("");
  const [newRxQuantity, setNewRxQuantity] = useState("");
  const [newRxFrequency, setNewRxFrequency] = useState("");
  const [newRxDuration, setNewRxDuration] = useState("");
  const [newRxRoute, setNewRxRoute] = useState("");
  const [newRxRefills, setNewRxRefills] = useState("0");
  const [newRxDiagnosis, setNewRxDiagnosis] = useState("");
  const [newRxNote, setNewRxNote] = useState("");

  // Add-immunization form state
  const [newImmVaccine, setNewImmVaccine] = useState("");
  const [newImmDate, setNewImmDate] = useState("");
  const [newImmManufacturer, setNewImmManufacturer] = useState("");
  const [newImmLot, setNewImmLot] = useState("");

  function load() {
    setState({ status: "loading" });
    getClinicalLists(session.sessionId, patientId)
      .then((data) => setState({ status: "ready", data }))
      .catch((err) =>
        setState({
          status: "error",
          message: err instanceof Error ? err.message : "Could not load chart.",
        }),
      );
  }

  useEffect(() => {
    load();
  }, [patientId]); // eslint-disable-line react-hooks/exhaustive-deps

  async function handleAddProblem(e: React.FormEvent) {
    e.preventDefault();
    if (!newProbTitle.trim() || !newProbDate) return;
    setWorking(true);
    try {
      const result = await createProblem(session.sessionId, {
        patientId,
        title: newProbTitle.trim(),
        dateTime: newProbDate,
        diagnosis: newProbDx.trim() || null,
        comments: newProbComments.trim(),
      });
      setState({ status: "ready", data: result.detail });
      setAddMode(null);
      setNewProbTitle("");
      setNewProbDx("");
      setNewProbDate(today());
      setNewProbComments("");
      showToast("Problem added.", "success");
    } catch {
      showToast("Could not add problem.", "error");
    } finally {
      setWorking(false);
    }
  }

  async function handleAddAllergy(e: React.FormEvent) {
    e.preventDefault();
    if (!newAllergyTitle.trim() || !newAllergyDate) return;
    setWorking(true);
    try {
      const result = await createAllergy(session.sessionId, {
        patientId,
        title: newAllergyTitle.trim(),
        dateTime: newAllergyDate,
        reaction: newAllergyReaction.trim(),
        severity: newAllergySeverity,
        comments: newAllergyComments.trim(),
      });
      setState({ status: "ready", data: result.detail });
      setAddMode(null);
      setNewAllergyTitle("");
      setNewAllergyReaction("");
      setNewAllergySeverity("mild");
      setNewAllergyDate(today());
      setNewAllergyComments("");
      showToast("Allergy added.", "success");
    } catch {
      showToast("Could not add allergy.", "error");
    } finally {
      setWorking(false);
    }
  }

  async function handleAddMedication(e: React.FormEvent) {
    e.preventDefault();
    if (!newMedTitle.trim() || !newMedDate) return;
    setWorking(true);
    try {
      const result = await createMedication(session.sessionId, {
        patientId,
        title: newMedTitle.trim(),
        dateTime: newMedDate,
        diagnosis: newMedDx.trim() || null,
        comments: newMedComments.trim(),
      });
      setState({ status: "ready", data: result.detail });
      setAddMode(null);
      setNewMedTitle("");
      setNewMedDx("");
      setNewMedDate(today());
      setNewMedComments("");
      showToast("Medication added.", "success");
    } catch {
      showToast("Could not add medication.", "error");
    } finally {
      setWorking(false);
    }
  }

  async function handleVocabularySearch() {
    setSelectedRx(null);
    setRxVocabulary({ status: "loading" });
    try {
      const items = await searchClinicalMedicationVocabulary(
        session.sessionId,
        rxQuery,
      );
      setRxVocabulary({ status: "ready", items });
    } catch (error) {
      setRxVocabulary({
        status: "error",
        message:
          error instanceof Error
            ? error.message
            : "The local medication catalog could not be searched.",
      });
    }
  }

  function selectVocabularyItem(item: MedicationVocabularyItem) {
    setSelectedRx(item);
    setNewRxRoute(item.route);
    setNewRxFrequency(item.frequency ?? "");
    setNewRxDuration(
      item.durationDays === null || item.durationDays === undefined
        ? ""
        : String(item.durationDays),
    );
    const suggestedDose = [
      item.doseAmount,
      item.doseUnit,
      item.frequency,
    ]
      .filter((value) => value !== null && value !== undefined && value !== "")
      .join(" ");
    setNewRxDosage(suggestedDose);
  }

  function resetPrescriptionForm() {
    setRxQuery("");
    setRxVocabulary({ status: "idle" });
    setSelectedRx(null);
    setNewRxStartDate(today());
    setNewRxDosage("");
    setNewRxQuantity("");
    setNewRxFrequency("");
    setNewRxDuration("");
    setNewRxRoute("");
    setNewRxRefills("0");
    setNewRxDiagnosis("");
    setNewRxNote("");
  }

  async function handleAddPrescription(e: React.FormEvent) {
    e.preventDefault();
    const refills = Number(newRxRefills);
    const durationDays = newRxDuration ? Number(newRxDuration) : null;
    if (
      !selectedRx ||
      Boolean(selectedRx.controlledSubstanceSchedule) ||
      !newRxStartDate ||
      !newRxDosage.trim() ||
      !newRxQuantity.trim() ||
      !Number.isInteger(refills) ||
      refills < 0 ||
      refills > 12 ||
      (durationDays !== null &&
        (!Number.isInteger(durationDays) || durationDays <= 0))
    )
      return;

    setWorking(true);
    try {
      const result = await createPrescription(session.sessionId, {
        patientId,
        startDate: newRxStartDate,
        drug: selectedRx.displayName,
        rxNormCode: selectedRx.rxNormCode,
        dosage: newRxDosage.trim(),
        quantity: newRxQuantity.trim(),
        doseAmount: selectedRx.doseAmount ?? null,
        doseUnit: selectedRx.doseUnit ?? null,
        frequency: newRxFrequency.trim() || null,
        durationDays,
        route: newRxRoute.trim() || null,
        refills,
        diagnosis: newRxDiagnosis.trim(),
        note: newRxNote.trim(),
      });
      setState({ status: "ready", data: result.detail });
      setAddMode(null);
      resetPrescriptionForm();
      showToast("Prescription created in the local target.", "success");
    } catch {
      showToast("Could not create the prescription.", "error");
    } finally {
      setWorking(false);
    }
  }

  async function handleAddImmunization(e: React.FormEvent) {
    e.preventDefault();
    if (!newImmVaccine || !newImmDate) return;
    setWorking(true);
    try {
      const result = await createImmunization(session.sessionId, {
        patientId,
        vaccine: newImmVaccine,
        administeredAt: newImmDate,
        manufacturer: newImmManufacturer || null,
        lotNumber: newImmLot || null,
      });
      setState({ status: "ready", data: result.detail });
      setAddMode(null);
      setNewImmVaccine("");
      setNewImmDate("");
      setNewImmManufacturer("");
      setNewImmLot("");
      showToast("Immunization recorded.", "success");
    } catch {
      showToast("Could not add immunization.", "error");
    } finally {
      setWorking(false);
    }
  }

  function beginLifecycleAction(target: LifecycleTarget) {
    setLifecycleTarget(target);
    setLifecycleReason("");
  }

  function cancelLifecycleAction() {
    setLifecycleTarget(null);
    setLifecycleReason("");
  }

  async function confirmLifecycleAction() {
    if (!lifecycleTarget || !lifecycleReason.trim()) return;
    setWorking(true);
    try {
      const reason = lifecycleReason.trim();
      let detail: ClinicalListsResponse;
      if (
        lifecycleTarget.type === "problem" &&
        lifecycleTarget.action === "deactivate"
      ) {
        detail = (
          await deactivateProblem(
            session.sessionId,
            lifecycleTarget.id,
            reason,
          )
        ).detail;
      } else if (
        lifecycleTarget.type === "allergy" &&
        lifecycleTarget.action === "deactivate"
      ) {
        detail = (
          await deactivateAllergy(
            session.sessionId,
            lifecycleTarget.id,
            reason,
          )
        ).detail;
      } else if (
        lifecycleTarget.type === "medication" &&
        lifecycleTarget.action === "deactivate"
      ) {
        detail = (
          await deactivateMedication(
            session.sessionId,
            lifecycleTarget.id,
            reason,
            lifecycleTarget.expectedVersion ?? 0,
          )
        ).detail;
      } else if (
        lifecycleTarget.type === "medication" &&
        lifecycleTarget.action === "restore"
      ) {
        detail = (
          await restoreMedication(
            session.sessionId,
            lifecycleTarget.id,
            reason,
            lifecycleTarget.expectedVersion ?? 0,
          )
        ).detail;
      } else if (
        lifecycleTarget.type === "immunization" &&
        lifecycleTarget.action === "entered-in-error"
      ) {
        detail = (
          await markImmunizationEnteredInError(
            session.sessionId,
            Number(lifecycleTarget.id),
            reason,
          )
        ).detail;
      } else {
        if (lifecycleTarget.type === "problem") {
          await deleteProblem(
            session.sessionId,
            lifecycleTarget.id,
          );
        } else if (lifecycleTarget.type === "allergy") {
          await deleteAllergy(
            session.sessionId,
            lifecycleTarget.id,
          );
        } else {
          await deleteImmunization(
            session.sessionId,
            Number(lifecycleTarget.id),
          );
        }
        detail = await getClinicalLists(session.sessionId, patientId);
      }
      setState({ status: "ready", data: detail });
      showToast(
        lifecycleTarget.action === "delete"
          ? `${lifecycleTarget.title} permanently deleted.`
          : lifecycleTarget.action === "restore"
            ? `${lifecycleTarget.title} restored to the active medication list.`
            : `${lifecycleTarget.title} moved to history.`,
        "success",
      );
      cancelLifecycleAction();
    } catch {
      showToast("Could not complete the clinical-list action.", "error");
    } finally {
      setWorking(false);
    }
  }

  function renderLifecycleConfirmation(type: LifecycleTarget["type"], id: string) {
    if (
      !lifecycleTarget ||
      lifecycleTarget.type !== type ||
      lifecycleTarget.id !== id
    )
      return null;

    const destructive = lifecycleTarget.action === "delete";
    return (
      <div className="cl-lifecycle-confirmation">
        <p>
          {destructive
            ? `Permanently delete ${lifecycleTarget.title}? This removes the local record and its visible history.`
            : lifecycleTarget.action === "entered-in-error"
              ? `Why was ${lifecycleTarget.title} entered in error?`
              : `Why is ${lifecycleTarget.title} being deactivated?`}
        </p>
        <label>
          {destructive ? "Type DELETE to confirm" : "Clinical reason"}
          <input
            className="ne-input"
            value={lifecycleReason}
            onChange={(event) => setLifecycleReason(event.target.value)}
            maxLength={500}
            required
            autoFocus
          />
        </label>
        <div className="cl-inline-form-actions">
          <button
            className={destructive ? "cl-btn-danger" : "cl-btn-primary"}
            type="button"
            disabled={
              working ||
              (destructive
                ? lifecycleReason.trim() !== "DELETE"
                : !lifecycleReason.trim())
            }
            onClick={confirmLifecycleAction}
          >
            {destructive ? "Delete permanently" : "Confirm"}
          </button>
          <button
            className="cl-btn-secondary"
            type="button"
            disabled={working}
            onClick={cancelLifecycleAction}
          >
            Cancel
          </button>
        </div>
      </div>
    );
  }

  if (state.status === "loading")
    return (
      <div className="clinician-page">
        <div className="cl-grid-two">
          {[0, 1, 2, 3].map((i) => (
            <section key={i} className="cl-card">
              <div className="skeleton-list">
                {[0, 1, 2].map((j) => (
                  <div key={j} className="skeleton-row" />
                ))}
              </div>
            </section>
          ))}
        </div>
      </div>
    );

  if (state.status === "error")
    return (
      <div className="clinician-page">
        <div className="error-banner">{state.message}</div>
      </div>
    );

  const { data } = state;
  const activeProblemCount = data.problems.filter(
    (item) => item.activity === 1,
  ).length;
  const activeAllergyCount = data.allergies.filter(
    (item) => item.activity === 1,
  ).length;
  const activeMedicationCount = data.medications.filter(
    (item) => item.activity === 1,
  ).length;
  const activeImmunizationCount = data.immunizations.filter(
    (item) => !item.enteredInError,
  ).length;

  return (
    <div className="clinician-page">
      <div className="cl-grid-two">
        {/* Problems */}
        <section className="cl-card">
          <div className="cl-card-header">
            <div>
              <h2 className="cl-card-title">Problems</h2>
              <p className="clinician-page-subtitle">
                {activeProblemCount} active ·{" "}
                {data.problems.length - activeProblemCount} historical
              </p>
            </div>
            <button
              className="cl-btn-icon"
              type="button"
              onClick={() =>
                setAddMode(addMode === "problem" ? null : "problem")
              }
              aria-label="Add problem"
            >
              <Plus size={15} />
            </button>
          </div>
          {addMode === "problem" && (
            <form className="cl-inline-form" onSubmit={handleAddProblem}>
              <label>
                Problem title
                <input
                  className="ne-input"
                  value={newProbTitle}
                  onChange={(e) => setNewProbTitle(e.target.value)}
                  maxLength={255}
                  required
                />
              </label>
              <div className="form-row">
                <label>
                  Onset or recorded date
                  <input
                    className="ne-input"
                    type="date"
                    value={newProbDate}
                    onChange={(e) => setNewProbDate(e.target.value)}
                    required
                  />
                </label>
                <label>
                  Diagnosis code (optional)
                  <input
                    className="ne-input"
                    value={newProbDx}
                    onChange={(e) => setNewProbDx(e.target.value)}
                    maxLength={64}
                  />
                </label>
              </div>
              <label>
                Clinical note (optional)
                <textarea
                  className="ne-input"
                  value={newProbComments}
                  onChange={(e) => setNewProbComments(e.target.value)}
                  maxLength={500}
                  rows={2}
                />
              </label>
              <div className="cl-inline-form-actions">
                <button
                  className="cl-btn-primary"
                  type="submit"
                  disabled={
                    working || !newProbTitle.trim() || !newProbDate
                  }
                >
                  Add
                </button>
                <button
                  className="cl-btn-secondary"
                  type="button"
                  onClick={() => setAddMode(null)}
                >
                  Cancel
                </button>
              </div>
            </form>
          )}
          {data.problems.length === 0 ? (
            <p className="cl-empty-text">No problems on file.</p>
          ) : (
            <ul className="cl-clinical-list">
              {data.problems.map((p) => (
                <li
                  key={p.id}
                  className="cl-clinical-row cl-clinical-row-interactive"
                >
                  {statusDot(p.activity)}
                  <div className="cl-clinical-body">
                    <p className="cl-clinical-title">{p.title}</p>
                    {(p.diagnosis ?? p.date) && (
                      <p className="cl-clinical-meta">
                        {p.diagnosis ?? ""}
                        {p.date ? ` · ${p.date}` : ""}
                      </p>
                    )}
                    {p.activity === 0 && (
                      <p className="cl-clinical-meta">
                        Inactive{p.endDate ? ` since ${p.endDate}` : ""}
                      </p>
                    )}
                    {p.comments && (
                      <p className="cl-clinical-meta">{p.comments}</p>
                    )}
                  </div>
                  <div className="cl-lifecycle-actions">
                    <button
                      className="cl-clinical-action"
                      type="button"
                      aria-label={
                        p.activity === 1
                          ? `Deactivate ${p.title}`
                          : `Delete ${p.title}`
                      }
                      disabled={working}
                      onClick={() =>
                        beginLifecycleAction({
                          type: "problem",
                          action: p.activity === 1 ? "deactivate" : "delete",
                          id: p.id,
                          title: p.title,
                        })
                      }
                    >
                      <X size={12} />
                      {p.activity === 1 ? "Deactivate" : "Delete record"}
                    </button>
                  </div>
                  {renderLifecycleConfirmation("problem", p.id)}
                </li>
              ))}
            </ul>
          )}
        </section>

        {/* Allergies */}
        <section className="cl-card">
          <div className="cl-card-header">
            <div>
              <h2 className="cl-card-title">Allergies</h2>
              <p className="clinician-page-subtitle">
                {activeAllergyCount} active ·{" "}
                {data.allergies.length - activeAllergyCount} historical
              </p>
            </div>
            <button
              className="cl-btn-icon"
              type="button"
              onClick={() =>
                setAddMode(addMode === "allergy" ? null : "allergy")
              }
              aria-label="Add allergy"
            >
              <Plus size={15} />
            </button>
          </div>
          {addMode === "allergy" && (
            <form className="cl-inline-form" onSubmit={handleAddAllergy}>
              <label>
                Allergen name
                <input
                  className="ne-input"
                  value={newAllergyTitle}
                  onChange={(e) => setNewAllergyTitle(e.target.value)}
                  maxLength={255}
                  required
                />
              </label>
              <div className="form-row">
                <label>
                  Recorded date
                  <input
                    className="ne-input"
                    type="date"
                    value={newAllergyDate}
                    onChange={(e) => setNewAllergyDate(e.target.value)}
                    required
                  />
                </label>
                <label>
                  Severity
                  <select
                    className="ne-input"
                    value={newAllergySeverity}
                    onChange={(e) => setNewAllergySeverity(e.target.value)}
                  >
                    <option value="mild">Mild</option>
                    <option value="moderate">Moderate</option>
                    <option value="severe">Severe</option>
                    <option value="life-threatening">
                      Life-threatening
                    </option>
                  </select>
                </label>
              </div>
              <label>
                Reaction (optional)
                <input
                  className="ne-input"
                  value={newAllergyReaction}
                  onChange={(e) => setNewAllergyReaction(e.target.value)}
                  maxLength={255}
                />
              </label>
              <label>
                Clinical note (optional)
                <textarea
                  className="ne-input"
                  value={newAllergyComments}
                  onChange={(e) => setNewAllergyComments(e.target.value)}
                  maxLength={500}
                  rows={2}
                />
              </label>
              <div className="cl-inline-form-actions">
                <button
                  className="cl-btn-primary"
                  type="submit"
                  disabled={
                    working ||
                    !newAllergyTitle.trim() ||
                    !newAllergyDate
                  }
                >
                  Add
                </button>
                <button
                  className="cl-btn-secondary"
                  type="button"
                  onClick={() => setAddMode(null)}
                >
                  Cancel
                </button>
              </div>
            </form>
          )}
          {data.allergies.length === 0 ? (
            <p className="cl-empty-text">No known allergies on file.</p>
          ) : (
            <ul className="cl-clinical-list">
              {data.allergies.map((a) => (
                <li
                  key={a.id}
                  className="cl-clinical-row cl-clinical-row-interactive"
                >
                  {statusDot(a.activity)}
                  <div className="cl-clinical-body">
                    <p className="cl-clinical-title">{a.title}</p>
                    {(a.reaction ?? a.severity) && (
                      <p className="cl-clinical-meta">
                        {a.reaction ?? ""}
                        {a.severity ? ` · ${a.severity}` : ""}
                      </p>
                    )}
                    {a.activity === 0 && (
                      <p className="cl-clinical-meta">
                        Inactive{a.endDate ? ` since ${a.endDate}` : ""}
                      </p>
                    )}
                    {a.comments && (
                      <p className="cl-clinical-meta">{a.comments}</p>
                    )}
                  </div>
                  <div className="cl-lifecycle-actions">
                    <button
                      className="cl-clinical-action"
                      type="button"
                      aria-label={
                        a.activity === 1
                          ? `Deactivate ${a.title}`
                          : `Delete ${a.title}`
                      }
                      disabled={working}
                      onClick={() =>
                        beginLifecycleAction({
                          type: "allergy",
                          action: a.activity === 1 ? "deactivate" : "delete",
                          id: a.id,
                          title: a.title,
                        })
                      }
                    >
                      <X size={12} />
                      {a.activity === 1 ? "Deactivate" : "Delete record"}
                    </button>
                  </div>
                  {renderLifecycleConfirmation("allergy", a.id)}
                </li>
              ))}
            </ul>
          )}
        </section>

        {/* Medications */}
        <section className="cl-card">
          <div className="cl-card-header">
            <div>
              <h2 className="cl-card-title">Medications</h2>
              <p className="clinician-page-subtitle">
                {activeMedicationCount} active ·{" "}
                {data.medications.length - activeMedicationCount} historical
              </p>
            </div>
            <button
              className="cl-btn-icon"
              type="button"
              onClick={() =>
                setAddMode(addMode === "medication" ? null : "medication")
              }
              aria-label="Add medication"
            >
              <Plus size={15} />
            </button>
          </div>
          {addMode === "medication" && (
            <form className="cl-inline-form" onSubmit={handleAddMedication}>
              <label>
                Medication name
                <input
                  className="ne-input"
                  value={newMedTitle}
                  onChange={(e) => setNewMedTitle(e.target.value)}
                  maxLength={255}
                  required
                />
              </label>
              <div className="form-row">
                <label>
                  Started or recorded date
                  <input
                    className="ne-input"
                    type="date"
                    value={newMedDate}
                    onChange={(e) => setNewMedDate(e.target.value)}
                    required
                  />
                </label>
                <label>
                  Diagnosis code (optional)
                  <input
                    className="ne-input"
                    value={newMedDx}
                    onChange={(e) => setNewMedDx(e.target.value)}
                    maxLength={64}
                  />
                </label>
              </div>
              <label>
                Clinical note (optional)
                <textarea
                  className="ne-input"
                  value={newMedComments}
                  onChange={(e) => setNewMedComments(e.target.value)}
                  maxLength={500}
                  rows={2}
                />
              </label>
              <div className="cl-inline-form-actions">
                <button
                  className="cl-btn-primary"
                  type="submit"
                  disabled={
                    working || !newMedTitle.trim() || !newMedDate
                  }
                >
                  Add
                </button>
                <button
                  className="cl-btn-secondary"
                  type="button"
                  onClick={() => setAddMode(null)}
                >
                  Cancel
                </button>
              </div>
            </form>
          )}
          {data.medications.length === 0 ? (
            <p className="cl-empty-text">No medications on file.</p>
          ) : (
            <ul className="cl-clinical-list">
              {data.medications.map((m) => (
                <li
                  key={m.id}
                  className="cl-clinical-row cl-clinical-row-interactive"
                >
                  {statusDot(m.activity)}
                  <div className="cl-clinical-body">
                    <p className="cl-clinical-title">{m.title}</p>
                    {(m.diagnosis ?? m.date) && (
                      <p className="cl-clinical-meta">
                        {m.diagnosis ?? ""}
                        {m.date ? ` · ${m.date}` : ""}
                      </p>
                    )}
                    {m.activity === 0 && (
                      <p className="cl-clinical-meta">
                        Inactive{m.endDate ? ` since ${m.endDate}` : ""}
                      </p>
                    )}
                    {m.comments && (
                      <p className="cl-clinical-meta">{m.comments}</p>
                    )}
                    <p className="cl-clinical-meta">
                      Local lifecycle version {m.lifecycleVersion} · {m.lifecycleEventCount} event{m.lifecycleEventCount === 1 ? "" : "s"}
                    </p>
                  </div>
                  <div className="cl-lifecycle-actions">
                    <button
                      className="cl-clinical-action"
                      type="button"
                      aria-label={
                        m.activity === 1
                          ? `Deactivate ${m.title}`
                          : `Restore ${m.title}`
                      }
                      disabled={working}
                      onClick={() =>
                        beginLifecycleAction({
                          type: "medication",
                          action: m.activity === 1 ? "deactivate" : "restore",
                          id: m.id,
                          title: m.title,
                          expectedVersion: m.lifecycleVersion,
                        })
                      }
                    >
                      <X size={12} />
                      {m.activity === 1 ? "Deactivate" : "Restore"}
                    </button>
                  </div>
                  {renderLifecycleConfirmation("medication", m.id)}
                </li>
              ))}
            </ul>
          )}
        </section>

        {/* Prescriptions */}
        <section className="cl-card cl-card-wide">
          <div className="cl-card-header">
            <div>
              <h2 className="cl-card-title">
                Prescriptions ({data.prescriptions.length})
              </h2>
              <p className="clinician-page-subtitle">
                Local target catalog · dataset {data.datasetId} ·{" "}
                {data.datasetVersion}
              </p>
            </div>
            <button
              className="cl-btn-icon"
              type="button"
              onClick={() => {
                const opening = addMode !== "prescription";
                setAddMode(opening ? "prescription" : null);
                if (!opening) resetPrescriptionForm();
              }}
              aria-label="Add prescription"
            >
              <Plus size={15} />
            </button>
          </div>
          <div className="hint-banner">
            This searchable RXCUI list is a bounded local catalog for synthetic
            workflow validation. It is not an authoritative drug knowledge
            base and does not perform formulary, interaction, pharmacy,
            eRx/EPCS, or controlled-substance authorization.
          </div>
          {addMode === "prescription" && (
            <form
              className="cl-inline-form rx-create-form"
              onSubmit={handleAddPrescription}
            >
              <fieldset>
                <legend>1. Select a local medication</legend>
                <div className="rx-catalog-search">
                  <label htmlFor="rx-catalog-query">Drug name or RXCUI</label>
                  <div>
                    <input
                      id="rx-catalog-query"
                      className="ne-input"
                      value={rxQuery}
                      onChange={(e) => setRxQuery(e.target.value)}
                      placeholder="Search the local medication catalog"
                    />
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      disabled={rxVocabulary.status === "loading"}
                      onClick={handleVocabularySearch}
                    >
                      <Search size={14} /> Search catalog
                    </button>
                  </div>
                </div>
                {rxVocabulary.status === "loading" && (
                  <p className="cl-empty-text" aria-live="polite">
                    Searching the local medication catalog…
                  </p>
                )}
                {rxVocabulary.status === "error" && (
                  <div className="error-banner">
                    {rxVocabulary.message}
                  </div>
                )}
                {rxVocabulary.status === "ready" &&
                  rxVocabulary.items.length === 0 && (
                    <p className="cl-empty-text">
                      No local medications match this search.
                    </p>
                  )}
                {rxVocabulary.status === "ready" &&
                  rxVocabulary.items.length > 0 && (
                    <div
                      className="rx-catalog-results"
                      role="listbox"
                      aria-label="Local medication matches"
                    >
                      {rxVocabulary.items.map((item) => (
                        <button
                          key={item.rxNormCode}
                          className={
                            selectedRx?.rxNormCode === item.rxNormCode
                              ? "rx-catalog-option rx-catalog-option-selected"
                              : "rx-catalog-option"
                          }
                          type="button"
                          role="option"
                          aria-selected={
                            selectedRx?.rxNormCode === item.rxNormCode
                          }
                          onClick={() => selectVocabularyItem(item)}
                        >
                          <strong>{item.displayName}</strong>
                          <span>
                            RXCUI {item.rxNormCode} · {item.form} ·{" "}
                            {item.route}
                            {item.controlledSubstanceSchedule
                              ? ` · Schedule ${item.controlledSubstanceSchedule}`
                              : ""}
                          </span>
                        </button>
                      ))}
                    </div>
                  )}
              </fieldset>

              {selectedRx && (
                <fieldset>
                  <legend>2. Complete prescription details</legend>
                  <div className="rx-selected-medication" role="status">
                    <div>
                      <strong>{selectedRx.displayName}</strong>
                      <span>
                        RXCUI {selectedRx.rxNormCode} ·{" "}
                        {selectedRx.strength} · {selectedRx.form}
                      </span>
                    </div>
                    {selectedRx.controlledSubstanceSchedule && (
                      <span className="rx-warning">
                        <AlertTriangle size={14} /> Schedule{" "}
                        {selectedRx.controlledSubstanceSchedule}: governed
                        authorization is not implemented
                      </span>
                    )}
                  </div>
                  <div className="rx-create-grid">
                    <label>
                      Start date
                      <input
                        className="ne-input"
                        type="date"
                        value={newRxStartDate}
                        onChange={(e) => setNewRxStartDate(e.target.value)}
                        required
                      />
                    </label>
                    <label>
                      Directions
                      <input
                        className="ne-input"
                        value={newRxDosage}
                        onChange={(e) => setNewRxDosage(e.target.value)}
                        placeholder="For example, 1 tablet twice daily"
                        required
                      />
                    </label>
                    <label>
                      Quantity
                      <input
                        className="ne-input"
                        value={newRxQuantity}
                        onChange={(e) => setNewRxQuantity(e.target.value)}
                        placeholder="For example, 30"
                        required
                      />
                    </label>
                    <label>
                      Route
                      <input
                        className="ne-input"
                        value={newRxRoute}
                        onChange={(e) => setNewRxRoute(e.target.value)}
                      />
                    </label>
                    <label>
                      Frequency
                      <input
                        className="ne-input"
                        value={newRxFrequency}
                        onChange={(e) => setNewRxFrequency(e.target.value)}
                      />
                    </label>
                    <label>
                      Duration (days)
                      <input
                        className="ne-input"
                        type="number"
                        min={1}
                        max={365}
                        value={newRxDuration}
                        onChange={(e) => setNewRxDuration(e.target.value)}
                      />
                    </label>
                    <label>
                      Authorized refills
                      <input
                        className="ne-input"
                        type="number"
                        min={0}
                        max={12}
                        value={newRxRefills}
                        onChange={(e) => setNewRxRefills(e.target.value)}
                        required
                      />
                    </label>
                    <label>
                      Diagnosis
                      <input
                        className="ne-input"
                        value={newRxDiagnosis}
                        onChange={(e) => setNewRxDiagnosis(e.target.value)}
                        placeholder="Optional diagnosis code"
                      />
                    </label>
                    <label className="rx-create-note">
                      Prescription note
                      <input
                        className="ne-input"
                        value={newRxNote}
                        onChange={(e) => setNewRxNote(e.target.value)}
                        maxLength={250}
                      />
                    </label>
                  </div>
                </fieldset>
              )}
              <div className="cl-inline-form-actions">
                <button
                  className="cl-btn-primary"
                  type="submit"
                  disabled={
                    working ||
                    !selectedRx ||
                    Boolean(selectedRx.controlledSubstanceSchedule) ||
                    !newRxStartDate ||
                    !newRxDosage.trim() ||
                    !newRxQuantity.trim()
                  }
                >
                  Create local prescription
                </button>
                <button
                  className="cl-btn-secondary"
                  type="button"
                  disabled={working}
                  onClick={() => {
                    setAddMode(null);
                    resetPrescriptionForm();
                  }}
                >
                  Cancel
                </button>
              </div>
            </form>
          )}
          {data.prescriptions.length === 0 ? (
            <p className="cl-empty-text">No prescriptions on file.</p>
          ) : (
            <ul className="cl-clinical-list">
              {data.prescriptions.map((rx) => (
                <li key={rx.id} className="cl-clinical-row">
                  {statusDot(rx.active)}
                  <div className="cl-clinical-body">
                    <p className="cl-clinical-title">{rx.drug}</p>
                    <p className="cl-clinical-meta">
                      {[
                        rx.dosage,
                        rx.quantity ? `Qty ${rx.quantity}` : null,
                        rx.route,
                        rx.rxNormCode ? `RXCUI ${rx.rxNormCode}` : null,
                        `${rx.refills} refill${rx.refills === 1 ? "" : "s"}`,
                      ]
                        .filter(Boolean)
                        .join(" · ")}
                      {rx.providerName ? ` · ${rx.providerName}` : ""}
                    </p>
                    <p className="cl-clinical-meta">
                      RX ID {rx.id}
                      {rx.startDate ? ` · Started ${rx.startDate}` : ""}
                      {rx.note ? ` · ${rx.note}` : ""}
                    </p>
                    {rx.controlledSubstanceReviewRequired && (
                      <p className="rx-warning">
                        <AlertTriangle size={13} />{" "}
                        {rx.controlledSubstanceReason ??
                          "Controlled-substance review required."}
                      </p>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="cl-card cl-card-wide">
          <div className="cl-card-header">
            <div>
              <h2 className="cl-card-title">Medication reconciliation</h2>
              <p className="clinician-page-subtitle">
                Read-only comparison of the local medication and prescription
                lists. Review clinical context before changing either record.
              </p>
            </div>
            <span className="cl-badge cl-badge-muted">
              {data.medicationReconciliations.length} comparison
              {data.medicationReconciliations.length === 1 ? "" : "s"}
            </span>
          </div>
          {data.medicationDuplicates.length > 0 && (
            <div className="error-banner" style={{ marginBottom: 12 }}>
              <strong>Possible duplicate active medications:</strong>{" "}
              {data.medicationDuplicates
                .map(
                  (duplicate) =>
                    `${duplicate.displayTitle} (${duplicate.activeCount})`,
                )
                .join(", ")}
            </div>
          )}
          {data.medicationReconciliations.length === 0 ? (
            <p className="cl-empty-text">
              No medication or prescription records are available to compare.
            </p>
          ) : (
            <ul className="cl-clinical-list">
              {data.medicationReconciliations.map((item) => (
                <li key={item.normalizedTitle} className="cl-clinical-row">
                  <div>
                    <p className="cl-clinical-title">{item.displayTitle}</p>
                    <p className="cl-clinical-meta">
                      {item.medicationCount} medication record
                      {item.medicationCount === 1 ? "" : "s"} ·{" "}
                      {item.prescriptionCount} prescription record
                      {item.prescriptionCount === 1 ? "" : "s"}
                      {item.diagnoses.length > 0
                        ? ` · ${item.diagnoses.join(", ")}`
                        : ""}
                    </p>
                  </div>
                  <span
                    className={`cl-badge ${item.status === "matched" ? "cl-badge-green" : "cl-badge-muted"}`}
                  >
                    {item.status}
                  </span>
                </li>
              ))}
            </ul>
          )}
          {data.prescriptionDiagnosisInteractions.length > 0 && (
            <div style={{ marginTop: 14 }}>
              <p className="cl-soap-label">Prescription diagnosis links</p>
              <ul className="cl-clinical-list">
                {data.prescriptionDiagnosisInteractions.map((item) => (
                  <li key={item.diagnosis} className="cl-clinical-row">
                    <div>
                      <p className="cl-clinical-title">{item.diagnosis}</p>
                      <p className="cl-clinical-meta">
                        {item.drugs.join(", ")}
                      </p>
                    </div>
                    <span className="cl-badge cl-badge-muted">
                      {item.status}
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </section>

        {/* Immunizations */}
        <section className="cl-card cl-card-wide">
          <div className="cl-card-header">
            <div>
              <h2 className="cl-card-title">Immunizations</h2>
              <p className="clinician-page-subtitle">
                {activeImmunizationCount} recorded ·{" "}
                {data.immunizations.length - activeImmunizationCount} entered
                in error
              </p>
            </div>
            <button
              className="cl-btn-icon"
              type="button"
              onClick={() =>
                setAddMode(addMode === "immunization" ? null : "immunization")
              }
              aria-label="Add immunization"
            >
              <Plus size={15} />
            </button>
          </div>
          {addMode === "immunization" && (
            <form className="cl-inline-form" onSubmit={handleAddImmunization}>
              <div className="form-row">
                <input
                  className="ne-input"
                  placeholder="Vaccine name…"
                  value={newImmVaccine}
                  onChange={(e) => setNewImmVaccine(e.target.value)}
                  required
                  style={{ flex: 2 }}
                />
                <input
                  className="ne-input"
                  type="date"
                  placeholder="Date administered"
                  value={newImmDate}
                  onChange={(e) => setNewImmDate(e.target.value)}
                  required
                />
              </div>
              <div className="form-row">
                <input
                  className="ne-input"
                  placeholder="Manufacturer (optional)"
                  value={newImmManufacturer}
                  onChange={(e) => setNewImmManufacturer(e.target.value)}
                />
                <input
                  className="ne-input"
                  placeholder="Lot number (optional)"
                  value={newImmLot}
                  onChange={(e) => setNewImmLot(e.target.value)}
                />
              </div>
              <div className="cl-inline-form-actions">
                <button
                  className="cl-btn-primary"
                  type="submit"
                  disabled={working || !newImmVaccine || !newImmDate}
                >
                  Record
                </button>
                <button
                  className="cl-btn-secondary"
                  type="button"
                  onClick={() => setAddMode(null)}
                >
                  Cancel
                </button>
              </div>
            </form>
          )}
          {data.immunizations.length === 0 ? (
            <p className="cl-empty-text">No immunizations on file.</p>
          ) : (
            <ul className="cl-clinical-list">
              {data.immunizations.map((imm) => (
                <li
                  key={imm.id}
                  className="cl-clinical-row cl-clinical-row-interactive"
                >
                  <span
                    role="img"
                    className={`cl-activity-dot ${
                      imm.enteredInError
                        ? "cl-activity-inactive"
                        : "cl-activity-active"
                    }`}
                    aria-label={
                      imm.enteredInError ? "Entered in error" : "Recorded"
                    }
                  />
                  <div className="cl-clinical-body">
                    <p className="cl-clinical-title">{imm.vaccine}</p>
                    <p className="cl-clinical-meta">
                      {imm.administeredAt ?? ""}
                      {imm.manufacturer ? ` · ${imm.manufacturer}` : ""}
                      {imm.lotNumber ? ` · Lot: ${imm.lotNumber}` : ""}
                    </p>
                    {imm.note && (
                      <p className="cl-clinical-meta">{imm.note}</p>
                    )}
                  </div>
                  <div className="cl-lifecycle-actions">
                    <button
                      className="cl-clinical-action"
                      type="button"
                      aria-label={
                        imm.enteredInError
                          ? `Delete ${imm.vaccine}`
                          : `Mark ${imm.vaccine} entered in error`
                      }
                      disabled={working}
                      onClick={() =>
                        beginLifecycleAction({
                          type: "immunization",
                          action: imm.enteredInError
                            ? "delete"
                            : "entered-in-error",
                          id: String(imm.id),
                          title: imm.vaccine,
                        })
                      }
                    >
                      <X size={12} />
                      {imm.enteredInError
                        ? "Delete record"
                        : "Entered in error"}
                    </button>
                  </div>
                  {renderLifecycleConfirmation(
                    "immunization",
                    String(imm.id),
                  )}
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  );
}
