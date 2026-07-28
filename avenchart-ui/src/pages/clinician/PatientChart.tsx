import { useEffect, useState } from "react";
import { useOutletContext } from "react-router-dom";
import { AlertTriangle, Plus, Search, X } from "lucide-react";
import {
  getClinicalLists,
  createProblem,
  deactivateProblem,
  createAllergy,
  deactivateAllergy,
  createMedication,
  deactivateMedication,
  createImmunization,
  createPrescription,
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

function isoNow() {
  return new Date().toISOString().replace("T", " ").slice(0, 19);
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

export default function PatientChart() {
  const { session, patientId } = useOutletContext<PatientOutletContext>();
  const [state, setState] = useState<AsyncState<ClinicalListsResponse>>({
    status: "loading",
  });
  const [addMode, setAddMode] = useState<AddMode>(null);
  const [working, setWorking] = useState(false);

  // Add-problem form state
  const [newProbTitle, setNewProbTitle] = useState("");
  const [newProbDx, setNewProbDx] = useState("");

  // Add-allergy form state
  const [newAllergyTitle, setNewAllergyTitle] = useState("");
  const [newAllergyReaction, setNewAllergyReaction] = useState("");
  const [newAllergySeverity, setNewAllergySeverity] = useState("mild");

  // Add-medication form state
  const [newMedTitle, setNewMedTitle] = useState("");
  const [newMedDx, setNewMedDx] = useState("");

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
    if (!newProbTitle) return;
    setWorking(true);
    try {
      const result = await createProblem(session.sessionId, {
        patientId,
        title: newProbTitle,
        dateTime: isoNow(),
        diagnosis: newProbDx || null,
        comments: "",
      });
      setState({ status: "ready", data: result.detail });
      setAddMode(null);
      setNewProbTitle("");
      setNewProbDx("");
      showToast("Problem added.", "success");
    } catch {
      showToast("Could not add problem.", "error");
    } finally {
      setWorking(false);
    }
  }

  async function handleDeactivateProblem(id: string) {
    setWorking(true);
    try {
      const result = await deactivateProblem(
        session.sessionId,
        id,
        "Marked inactive by clinician",
      );
      setState({ status: "ready", data: result.detail });
      showToast("Problem marked inactive.", "success");
    } catch {
      showToast("Could not update problem.", "error");
    } finally {
      setWorking(false);
    }
  }

  async function handleAddAllergy(e: React.FormEvent) {
    e.preventDefault();
    if (!newAllergyTitle) return;
    setWorking(true);
    try {
      const result = await createAllergy(session.sessionId, {
        patientId,
        title: newAllergyTitle,
        dateTime: isoNow(),
        reaction: newAllergyReaction,
        severity: newAllergySeverity,
        comments: "",
      });
      setState({ status: "ready", data: result.detail });
      setAddMode(null);
      setNewAllergyTitle("");
      setNewAllergyReaction("");
      setNewAllergySeverity("mild");
      showToast("Allergy added.", "success");
    } catch {
      showToast("Could not add allergy.", "error");
    } finally {
      setWorking(false);
    }
  }

  async function handleDeactivateAllergy(id: string) {
    setWorking(true);
    try {
      const result = await deactivateAllergy(
        session.sessionId,
        id,
        "Marked inactive by clinician",
      );
      setState({ status: "ready", data: result.detail });
      showToast("Allergy marked inactive.", "success");
    } catch {
      showToast("Could not update allergy.", "error");
    } finally {
      setWorking(false);
    }
  }

  async function handleAddMedication(e: React.FormEvent) {
    e.preventDefault();
    if (!newMedTitle) return;
    setWorking(true);
    try {
      const result = await createMedication(session.sessionId, {
        patientId,
        title: newMedTitle,
        dateTime: isoNow(),
        diagnosis: newMedDx || null,
        comments: "",
      });
      setState({ status: "ready", data: result.detail });
      setAddMode(null);
      setNewMedTitle("");
      setNewMedDx("");
      showToast("Medication added.", "success");
    } catch {
      showToast("Could not add medication.", "error");
    } finally {
      setWorking(false);
    }
  }

  async function handleDeactivateMedication(id: string) {
    setWorking(true);
    try {
      const result = await deactivateMedication(
        session.sessionId,
        id,
        "Marked inactive by clinician",
      );
      setState({ status: "ready", data: result.detail });
      showToast("Medication marked inactive.", "success");
    } catch {
      showToast("Could not update medication.", "error");
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

  async function handleMarkImmunizationError(id: number) {
    setWorking(true);
    try {
      const result = await markImmunizationEnteredInError(
        session.sessionId,
        id,
      );
      setState({ status: "ready", data: result.detail });
      showToast("Immunization marked entered-in-error.", "success");
    } catch {
      showToast("Could not update immunization.", "error");
    } finally {
      setWorking(false);
    }
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

  return (
    <div className="clinician-page">
      <div className="cl-grid-two">
        {/* Problems */}
        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title">Problems ({data.problems.length})</h2>
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
              <input
                className="ne-input"
                placeholder="Problem title…"
                value={newProbTitle}
                onChange={(e) => setNewProbTitle(e.target.value)}
                required
              />
              <input
                className="ne-input"
                placeholder="Diagnosis code (optional)"
                value={newProbDx}
                onChange={(e) => setNewProbDx(e.target.value)}
              />
              <div className="cl-inline-form-actions">
                <button
                  className="cl-btn-primary"
                  type="submit"
                  disabled={working || !newProbTitle}
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
                  </div>
                  {p.activity === 1 && (
                    <button
                      className="cl-clinical-action"
                      type="button"
                      aria-label="Mark inactive"
                      disabled={working}
                      onClick={() => handleDeactivateProblem(p.id)}
                    >
                      <X size={12} />
                    </button>
                  )}
                </li>
              ))}
            </ul>
          )}
        </section>

        {/* Allergies */}
        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title">
              Allergies ({data.allergies.length})
            </h2>
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
              <input
                className="ne-input"
                placeholder="Allergen name…"
                value={newAllergyTitle}
                onChange={(e) => setNewAllergyTitle(e.target.value)}
                required
              />
              <input
                className="ne-input"
                placeholder="Reaction (optional)"
                value={newAllergyReaction}
                onChange={(e) => setNewAllergyReaction(e.target.value)}
              />
              <select
                className="ne-input"
                value={newAllergySeverity}
                onChange={(e) => setNewAllergySeverity(e.target.value)}
              >
                <option value="mild">Mild</option>
                <option value="moderate">Moderate</option>
                <option value="severe">Severe</option>
                <option value="life-threatening">Life-threatening</option>
              </select>
              <div className="cl-inline-form-actions">
                <button
                  className="cl-btn-primary"
                  type="submit"
                  disabled={working || !newAllergyTitle}
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
                  </div>
                  {a.activity === 1 && (
                    <button
                      className="cl-clinical-action"
                      type="button"
                      aria-label="Mark inactive"
                      disabled={working}
                      onClick={() => handleDeactivateAllergy(a.id)}
                    >
                      <X size={12} />
                    </button>
                  )}
                </li>
              ))}
            </ul>
          )}
        </section>

        {/* Medications */}
        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title">
              Medications ({data.medications.length})
            </h2>
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
              <input
                className="ne-input"
                placeholder="Medication name…"
                value={newMedTitle}
                onChange={(e) => setNewMedTitle(e.target.value)}
                required
              />
              <input
                className="ne-input"
                placeholder="Diagnosis code (optional)"
                value={newMedDx}
                onChange={(e) => setNewMedDx(e.target.value)}
              />
              <div className="cl-inline-form-actions">
                <button
                  className="cl-btn-primary"
                  type="submit"
                  disabled={working || !newMedTitle}
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
                    {m.date && <p className="cl-clinical-meta">{m.date}</p>}
                  </div>
                  {m.activity === 1 && (
                    <button
                      className="cl-clinical-action"
                      type="button"
                      aria-label="Mark inactive"
                      disabled={working}
                      onClick={() => handleDeactivateMedication(m.id)}
                    >
                      <X size={12} />
                    </button>
                  )}
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
            <h2 className="cl-card-title">
              Immunizations ({data.immunizations.length})
            </h2>
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
                  <div
                    className="cl-activity-dot cl-activity-active"
                    aria-hidden="true"
                  />
                  <div className="cl-clinical-body">
                    <p className="cl-clinical-title">{imm.vaccine}</p>
                    <p className="cl-clinical-meta">
                      {imm.administeredAt ?? ""}
                      {imm.manufacturer ? ` · ${imm.manufacturer}` : ""}
                      {imm.lotNumber ? ` · Lot: ${imm.lotNumber}` : ""}
                    </p>
                  </div>
                  <button
                    className="cl-clinical-action"
                    type="button"
                    aria-label="Mark entered in error"
                    disabled={working}
                    onClick={() => handleMarkImmunizationError(imm.id)}
                  >
                    <X size={12} />
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  );
}
