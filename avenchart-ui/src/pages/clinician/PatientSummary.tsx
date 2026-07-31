// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useEffectEvent, useState } from "react";
import { Link, useNavigate, useOutletContext } from "react-router-dom";
import {
  Building2,
  ArchiveRestore,
  CalendarClock,
  FileText,
  Phone,
  Plus,
  Printer,
  Shield,
  Stethoscope,
  Trash2,
  UserPlus,
} from "lucide-react";
import {
  createPatientMergeAuditPlan,
  createPatientInsurance,
  createPatientRecordRequest,
  deletePatientInsurance,
  executePatientMerge,
  getPatientCareTeamOptions,
  getPatientAdministrationHistory,
  getPatientDeceasedStatusHistory,
  getPatientLifecycleHistory,
  getPatientMergePreview,
  getPatientProviderAssignmentHistory,
  getPatientProviderAssignmentOptions,
  getPatientRecordRequests,
  rollbackPatientMerge,
  transitionPatientLifecycle,
  updatePatientDeceasedStatus,
  updatePatientCareTeam,
  updatePatientContact,
  updatePatientDemographics,
  updatePatientEmployer,
  updatePatientGuardianContact,
  updatePatientInsurance,
  completePatientRecordRequest,
  updatePatientProviderAssignment,
  updatePatientPortalAccountAccess,
  updatePatientPortalAccountReset,
  type PatientCareTeamMember,
  type PatientCareTeamMemberUpdate,
  type PatientCareTeamOptionsResponse,
  type PatientCareTeamUpdate,
  type PatientChartSummary,
  type PatientEmployerUpdate,
  type PatientGuardianContactUpdate,
  type PatientInsuranceMutationInput,
  type PatientAdministrationHistoryItem,
  type PatientAdministrationHistoryResponse,
  type PatientLifecycleHistoryResponse,
  type PatientDeceasedStatusHistoryResponse,
  type PatientMergePreview,
  type PatientProviderAssignmentHistoryResponse,
  type PatientProviderAssignmentOptionsResponse,
  type PatientRecordRequest,
} from "../../api.ts";
import { showToast } from "../../components/Toast.tsx";
import PatientDisclosurePanel from "./PatientDisclosurePanel.tsx";
import type { PatientOutletContext } from "./PatientShell.tsx";

function fact(label: string, value?: string | null) {
  if (!value) return null;
  return (
    <li className="fact-row">
      <span>{label}</span>
      <span>{value}</span>
    </li>
  );
}

const BLANK_INS: PatientInsuranceMutationInput = {
  type: "primary",
  provider: "",
  planName: "",
  policyNumber: "",
  groupNumber: "",
  relationship: "self",
  subscriberFirstName: "",
  subscriberLastName: "",
  subscriberDateOfBirth: "",
  subscriberSex: "unknown",
};

type InsuranceMode =
  | { kind: "none" }
  | { kind: "add" }
  | { kind: "edit"; insuranceId: string };
type MergePreviewState =
  | { status: "idle" }
  | { status: "loading"; sourcePatientId: string }
  | { status: "ready"; data: PatientMergePreview }
  | { status: "error"; message: string };
type ProviderAssignmentHistoryState =
  | { status: "loading" }
  | { status: "ready"; data: PatientProviderAssignmentHistoryResponse }
  | { status: "error" };
type PatientAdministrationHistoryState =
  | { status: "loading" }
  | { status: "ready"; data: PatientAdministrationHistoryResponse }
  | { status: "error" };
type PatientLifecycleHistoryState =
  | { status: "loading" }
  | { status: "ready"; data: PatientLifecycleHistoryResponse }
  | { status: "error" };
type PatientDeceasedStatusHistoryState =
  | { status: "loading" }
  | { status: "ready"; data: PatientDeceasedStatusHistoryResponse }
  | { status: "error" };
type PatientAdministrationArea =
  | "all"
  | "demographics"
  | "contact"
  | "insurance";

const administrationAreaLabels: Record<
  Exclude<PatientAdministrationArea, "all">,
  string
> = {
  demographics: "Demographics",
  contact: "Contact",
  insurance: "Insurance",
};

const administrationFieldLabels: Record<string, string> = {
  firstName: "First name",
  lastName: "Last name",
  preferredName: "Preferred name",
  sex: "Sex",
  dateOfBirth: "Date of birth",
  street: "Street",
  city: "City",
  state: "State",
  postalCode: "Postal code",
  maritalStatus: "Marital status",
  occupation: "Occupation",
  race: "Race",
  ethnicity: "Ethnicity",
  interpreter: "Interpreter",
  familySize: "Family size",
  monthlyIncome: "Monthly income",
  homeless: "Homeless",
  financialReviewDate: "Financial review date",
  phoneHome: "Home phone",
  phoneCell: "Mobile phone",
  email: "Email",
  hipaaAllowSms: "SMS permission",
  hipaaAllowEmail: "Email permission",
  type: "Coverage type",
  provider: "Insurance company",
  planName: "Plan name",
  policyNumber: "Policy number",
  groupNumber: "Group number",
  relationship: "Subscriber relationship",
  subscriberFirstName: "Subscriber first name",
  subscriberMiddleName: "Subscriber middle name",
  subscriberLastName: "Subscriber last name",
  subscriberDateOfBirth: "Subscriber date of birth",
  subscriberSex: "Subscriber sex",
  subscriberStreet: "Subscriber street",
  subscriberStreetLine2: "Subscriber street line 2",
  subscriberCity: "Subscriber city",
  subscriberState: "Subscriber state",
  subscriberPostalCode: "Subscriber postal code",
  subscriberCountry: "Subscriber country",
  subscriberPhone: "Subscriber phone",
  subscriberEmployer: "Subscriber employer",
  subscriberEmployerStreet: "Employer street",
  subscriberEmployerStreetLine2: "Employer street line 2",
  subscriberEmployerCity: "Employer city",
  subscriberEmployerState: "Employer state",
  subscriberEmployerPostalCode: "Employer postal code",
  subscriberEmployerCountry: "Employer country",
};

function administrationEventTitle(event: PatientAdministrationHistoryItem) {
  const area =
    administrationAreaLabels[
      event.area as Exclude<PatientAdministrationArea, "all">
    ] ?? "Patient record";
  return `${area} ${event.action}`;
}

function administrationFieldLabel(field: string) {
  return administrationFieldLabels[field] ?? field;
}

function administrationValue(value: string | null | undefined) {
  return value?.trim() ? value : "Not recorded";
}

const mergeCountLabels: Array<{
  key: keyof PatientMergePreview["combinedCounts"];
  label: string;
}> = [
  { key: "appointments", label: "Appointments" },
  { key: "encounters", label: "Encounters" },
  { key: "prescriptions", label: "Prescriptions" },
  { key: "billingItems", label: "Billing items" },
  { key: "labOrders", label: "Lab orders" },
  { key: "messages", label: "Messages" },
  { key: "problems", label: "Problems" },
  { key: "allergies", label: "Allergies" },
  { key: "medications", label: "Medications" },
];

type RelationshipEditor =
  | "guardian"
  | "employer"
  | "provider"
  | "care-team"
  | null;

type CareTeamMemberDraft = PatientCareTeamMemberUpdate & {
  memberType: "provider" | "contact";
};

type CareTeamDraft = Omit<PatientCareTeamUpdate, "members"> & {
  members: CareTeamMemberDraft[];
};

const careTeamRoleOptions = [
  { value: "primary_care_provider", label: "Primary care provider" },
  { value: "physician", label: "Physician" },
  { value: "nurse", label: "Nurse" },
  { value: "case_manager", label: "Case manager" },
  { value: "social_worker", label: "Social worker" },
  { value: "pharmacist", label: "Pharmacist" },
  { value: "specialist", label: "Specialist" },
  { value: "caregiver", label: "Caregiver" },
  { value: "other", label: "Other" },
] as const;

const careTeamStatusOptions = [
  { value: "proposed", label: "Proposed" },
  { value: "active", label: "Active" },
  { value: "suspended", label: "Suspended" },
  { value: "inactive", label: "Inactive" },
  { value: "entered-in-error", label: "Entered in error" },
] as const;

function buildGuardianDraft(
  patient: PatientChartSummary,
): PatientGuardianContactUpdate {
  return {
    motherName: patient.motherName ?? "",
    guardianName: patient.guardianName ?? "",
    guardianRelationship: patient.guardianRelationship ?? "",
    guardianPhone: patient.guardianPhone ?? "",
    guardianEmail: patient.guardianEmail ?? "",
    guardianSex: patient.guardianSex ?? "",
    guardianAddress: patient.guardianAddress ?? "",
    guardianCity: patient.guardianCity ?? "",
    guardianState: patient.guardianState ?? "",
    guardianPostalCode: patient.guardianPostalCode ?? "",
    guardianCountry: patient.guardianCountry ?? "",
    guardianWorkPhone: patient.guardianWorkPhone ?? "",
  };
}

function buildEmployerDraft(
  patient: PatientChartSummary,
): PatientEmployerUpdate {
  return {
    employerName: patient.employerName ?? "",
    employerStreet: patient.employerStreet ?? "",
    employerCity: patient.employerCity ?? "",
    employerState: patient.employerState ?? "",
    employerPostalCode: patient.employerPostalCode ?? "",
    employerCountry: patient.employerCountry ?? "",
  };
}

function buildCareTeamMemberDraft(
  member?: PatientCareTeamMember,
): CareTeamMemberDraft {
  return {
    memberType: member?.contactId ? "contact" : "provider",
    userId: member?.userId ?? null,
    contactId: member?.contactId ?? null,
    role: member?.role ?? "primary_care_provider",
    facilityId: member?.facilityId ?? null,
    providerSince: member?.providerSince ?? "",
    status: member?.status ?? "active",
    note: member?.note ?? "",
  };
}

function buildCareTeamDraft(patient: PatientChartSummary): CareTeamDraft {
  return {
    teamName: patient.careTeam?.teamName ?? "Care Team",
    teamStatus: patient.careTeam?.teamStatus ?? "active",
    members: patient.careTeam?.members.map(buildCareTeamMemberDraft) ?? [],
  };
}

function formatAddress(
  street?: string | null,
  city?: string | null,
  state?: string | null,
  postalCode?: string | null,
  country?: string | null,
) {
  const locality = [city, state, postalCode].filter(Boolean).join(" ");
  return [street, locality, country].filter(Boolean).join(", ");
}

export default function PatientSummary() {
  const { session, patient, patientId, reload } =
    useOutletContext<PatientOutletContext>();
  const navigate = useNavigate();

  const [editDemoOpen, setEditDemoOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [relationshipEditor, setRelationshipEditor] =
    useState<RelationshipEditor>(null);
  const [relationshipSaving, setRelationshipSaving] = useState<Exclude<
    RelationshipEditor,
    null
  > | null>(null);
  const [relationshipError, setRelationshipError] = useState<string | null>(
    null,
  );
  const [guardianForm, setGuardianForm] = useState(() =>
    buildGuardianDraft(patient),
  );
  const [employerForm, setEmployerForm] = useState(() =>
    buildEmployerDraft(patient),
  );
  const [providerId, setProviderId] = useState<number | null>(
    patient.providerId ?? null,
  );
  const [providerReason, setProviderReason] = useState("");
  const [providerHistoryState, setProviderHistoryState] =
    useState<ProviderAssignmentHistoryState>({ status: "loading" });
  const [providerHistoryRetry, setProviderHistoryRetry] = useState(0);
  const [administrationHistoryState, setAdministrationHistoryState] =
    useState<PatientAdministrationHistoryState>({ status: "loading" });
  const [administrationHistoryRetry, setAdministrationHistoryRetry] =
    useState(0);
  const [administrationArea, setAdministrationArea] =
    useState<PatientAdministrationArea>("all");
  const [lifecycleHistoryState, setLifecycleHistoryState] =
    useState<PatientLifecycleHistoryState>({ status: "loading" });
  const [lifecycleHistoryRetry, setLifecycleHistoryRetry] = useState(0);
  const [lifecycleReason, setLifecycleReason] = useState("");
  const [lifecycleAction, setLifecycleAction] = useState<
    "retire" | "reactivate" | null
  >(null);
  const [deceasedHistoryState, setDeceasedHistoryState] =
    useState<PatientDeceasedStatusHistoryState>({ status: "loading" });
  const [deceasedHistoryRetry, setDeceasedHistoryRetry] = useState(0);
  const [deceasedSaving, setDeceasedSaving] = useState(false);
  const [deceasedForm, setDeceasedForm] = useState({
    deceasedDate: patient.deceasedDate ?? "",
    deceasedReason: patient.deceasedReason ?? "",
    correctionReason: "",
  });
  const [careTeamForm, setCareTeamForm] = useState<CareTeamDraft>(() =>
    buildCareTeamDraft(patient),
  );
  const [providerOptions, setProviderOptions] =
    useState<PatientProviderAssignmentOptionsResponse | null>(null);
  const [careTeamOptions, setCareTeamOptions] =
    useState<PatientCareTeamOptionsResponse | null>(null);
  const [relationshipOptionsState, setRelationshipOptionsState] = useState<
    "loading" | "ready" | "error"
  >("loading");
  const [relationshipOptionsRetry, setRelationshipOptionsRetry] = useState(0);
  const [contactForm, setContactForm] = useState({
    phoneHome: patient.phone ?? "",
    phoneCell: patient.phoneCell ?? "",
    email: patient.email ?? "",
    hipaaAllowSms: patient.hipaaAllowSms ?? "NO",
    hipaaAllowEmail: patient.hipaaAllowEmail ?? "NO",
  });
  const [demoForm, setDemoForm] = useState({
    firstName: patient.firstName ?? "",
    lastName: patient.lastName ?? "",
    preferredName: patient.preferredName ?? "",
    sex: patient.sex ?? "",
    dateOfBirth: patient.dateOfBirth ?? "",
    street: patient.street ?? "",
    city: patient.city ?? "",
    state: patient.state ?? "",
    postalCode: patient.postalCode ?? "",
    maritalStatus: patient.maritalStatus ?? "",
    occupation: patient.occupation ?? "",
    race: patient.race ?? "",
    ethnicity: patient.ethnicity ?? "",
    interpreter: patient.interpreter ?? "",
    familySize: patient.familySize ?? "",
    monthlyIncome: patient.monthlyIncome ?? "",
    homeless: patient.homeless ?? "NO",
    financialReviewDate: patient.financialReviewDate ?? "",
  });
  const [insMode, setInsMode] = useState<InsuranceMode>({ kind: "none" });
  const [insForm, setInsForm] =
    useState<PatientInsuranceMutationInput>(BLANK_INS);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [mergePreviewState, setMergePreviewState] = useState<MergePreviewState>(
    { status: "idle" },
  );
  const [mergeAuditRationale, setMergeAuditRationale] = useState("");
  const [mergeAuditState, setMergeAuditState] = useState<
    | { status: "idle" }
    | { status: "saving" }
    | { status: "ready"; auditId: string }
    | { status: "error"; message: string }
  >({ status: "idle" });
  const [mergeExecutionConfirmed, setMergeExecutionConfirmed] = useState(false);
  const [mergeExecutionState, setMergeExecutionState] = useState<
    | { status: "idle" }
    | { status: "saving" }
    | { status: "ready"; executionId: string; movedCount: number }
    | { status: "rolling-back"; executionId: string }
    | { status: "rolled-back"; executionId: string }
    | { status: "error"; message: string }
  >({ status: "idle" });
  const [portalAction, setPortalAction] = useState<"access" | "reset" | null>(
    null,
  );
  const [recordRequests, setRecordRequests] = useState<PatientRecordRequest[]>(
    [],
  );
  const [recordRequestLoading, setRecordRequestLoading] = useState(true);
  const [recordRequestAction, setRecordRequestAction] = useState<
    "create" | string | null
  >(null);

  useEffect(() => {
    const controller = new AbortController();
    setRelationshipOptionsState("loading");
    Promise.all([
      getPatientProviderAssignmentOptions(session.sessionId, controller.signal),
      getPatientCareTeamOptions(
        session.sessionId,
        patientId,
        controller.signal,
      ),
    ])
      .then(([providers, careTeam]) => {
        setProviderOptions(providers);
        setCareTeamOptions(careTeam);
        setRelationshipOptionsState("ready");
      })
      .catch((error: unknown) => {
        if (error instanceof DOMException && error.name === "AbortError")
          return;
        setRelationshipOptionsState("error");
      });
    return () => controller.abort();
  }, [patientId, relationshipOptionsRetry, session.sessionId]);

  useEffect(() => {
    const controller = new AbortController();
    setProviderHistoryState({ status: "loading" });
    getPatientProviderAssignmentHistory(
      session.sessionId,
      patientId,
      controller.signal,
    )
      .then((data) => setProviderHistoryState({ status: "ready", data }))
      .catch((error: unknown) => {
        if (error instanceof DOMException && error.name === "AbortError")
          return;
        setProviderHistoryState({ status: "error" });
      });
    return () => controller.abort();
  }, [patientId, providerHistoryRetry, session.sessionId]);

  useEffect(() => {
    const controller = new AbortController();
    setAdministrationHistoryState({ status: "loading" });
    getPatientAdministrationHistory(
      session.sessionId,
      patientId,
      controller.signal,
    )
      .then((data) =>
        setAdministrationHistoryState({ status: "ready", data }),
      )
      .catch((error: unknown) => {
        if (error instanceof DOMException && error.name === "AbortError")
          return;
        setAdministrationHistoryState({ status: "error" });
      });
    return () => controller.abort();
  }, [administrationHistoryRetry, patientId, session.sessionId]);

  useEffect(() => {
    const controller = new AbortController();
    setLifecycleHistoryState({ status: "loading" });
    getPatientLifecycleHistory(session.sessionId, patientId, controller.signal)
      .then((data) => setLifecycleHistoryState({ status: "ready", data }))
      .catch((error: unknown) => {
        if (error instanceof DOMException && error.name === "AbortError") return;
        setLifecycleHistoryState({ status: "error" });
      });
    return () => controller.abort();
  }, [lifecycleHistoryRetry, patientId, session.sessionId]);

  useEffect(() => {
    const controller = new AbortController();
    setDeceasedHistoryState({ status: "loading" });
    getPatientDeceasedStatusHistory(
      session.sessionId,
      patientId,
      controller.signal,
    )
      .then((data) => setDeceasedHistoryState({ status: "ready", data }))
      .catch((error: unknown) => {
        if (error instanceof DOMException && error.name === "AbortError") return;
        setDeceasedHistoryState({ status: "error" });
      });
    return () => controller.abort();
  }, [deceasedHistoryRetry, patientId, session.sessionId]);

  useEffect(() => {
    setDeceasedForm({
      deceasedDate: patient.deceasedDate ?? "",
      deceasedReason: patient.deceasedReason ?? "",
      correctionReason: "",
    });
  }, [patient.canonicalId, patient.deceasedDate, patient.deceasedReason]);

  async function loadRecordRequests() {
    setRecordRequestLoading(true);
    try {
      setRecordRequests(
        await getPatientRecordRequests(session.sessionId, patientId),
      );
    } catch {
      setRecordRequests([]);
    } finally {
      setRecordRequestLoading(false);
    }
  }

  const loadRecordRequestsOnPatientChange = useEffectEvent(loadRecordRequests);
  useEffect(() => {
    void loadRecordRequestsOnPatientChange();
  }, [session.sessionId, patientId]);

  function openAddInsurance() {
    setInsForm({ ...BLANK_INS });
    setInsMode({ kind: "add" });
  }

  function openEditInsurance(id: string) {
    const ins = patient.insurance.find((i) => i.id === id);
    if (!ins) return;
    setInsForm({
      type: ins.type ?? "primary",
      provider: ins.provider ?? "",
      planName: ins.planName ?? "",
      policyNumber: ins.policyNumber ?? "",
      groupNumber: ins.groupNumber ?? "",
      relationship: ins.relationship ?? "self",
      subscriberFirstName: "",
      subscriberLastName: "",
      subscriberDateOfBirth: "",
      subscriberSex: "unknown",
    });
    setInsMode({ kind: "edit", insuranceId: id });
  }

  async function handleSaveDemographics(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    try {
      await updatePatientContact(session.sessionId, patientId, contactForm);
      await updatePatientDemographics(session.sessionId, patientId, demoForm);
      showToast("Demographics saved.", "success");
      setEditDemoOpen(false);
      setAdministrationHistoryRetry((current) => current + 1);
      reload();
    } catch {
      showToast("Could not save demographics.", "error");
    } finally {
      setSaving(false);
    }
  }

  async function handleSaveInsurance(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    try {
      if (insMode.kind === "add") {
        await createPatientInsurance(session.sessionId, patientId, insForm);
        showToast("Insurance added.", "success");
      } else if (insMode.kind === "edit") {
        await updatePatientInsurance(
          session.sessionId,
          insMode.insuranceId,
          insForm,
        );
        showToast("Insurance updated.", "success");
      }
      setInsMode({ kind: "none" });
      setAdministrationHistoryRetry((current) => current + 1);
      reload();
    } catch {
      showToast("Could not save insurance.", "error");
    } finally {
      setSaving(false);
    }
  }

  async function handleDeleteInsurance(id: string) {
    if (!confirm("Remove this insurance record?")) return;
    setDeletingId(id);
    try {
      await deletePatientInsurance(session.sessionId, id);
      showToast("Insurance removed.", "success");
      setAdministrationHistoryRetry((current) => current + 1);
      reload();
    } catch {
      showToast("Could not remove insurance.", "error");
    } finally {
      setDeletingId(null);
    }
  }

  function openRelationshipEditor(editor: Exclude<RelationshipEditor, null>) {
    setRelationshipError(null);
    if (editor === "guardian") {
      setGuardianForm(buildGuardianDraft(patient));
    } else if (editor === "employer") {
      setEmployerForm(buildEmployerDraft(patient));
    } else if (editor === "provider") {
      setProviderId(patient.providerId ?? null);
      setProviderReason("");
    } else {
      setCareTeamForm(buildCareTeamDraft(patient));
    }
    setRelationshipEditor(editor);
  }

  function closeRelationshipEditor() {
    setRelationshipEditor(null);
    setRelationshipError(null);
  }

  function relationshipFailure(error: unknown, fallback: string) {
    const message = error instanceof Error ? error.message : fallback;
    setRelationshipError(message);
    showToast(fallback, "error");
  }

  async function saveGuardian(event: React.FormEvent) {
    event.preventDefault();
    setRelationshipSaving("guardian");
    setRelationshipError(null);
    try {
      await updatePatientGuardianContact(
        session.sessionId,
        patientId,
        guardianForm,
      );
      showToast("Guardian and representative details saved.", "success");
      setRelationshipEditor(null);
      reload();
    } catch (error) {
      relationshipFailure(
        error,
        "Could not save guardian and representative details.",
      );
    } finally {
      setRelationshipSaving(null);
    }
  }

  async function saveEmployer(event: React.FormEvent) {
    event.preventDefault();
    setRelationshipSaving("employer");
    setRelationshipError(null);
    try {
      await updatePatientEmployer(session.sessionId, patientId, employerForm);
      showToast("Employer details saved.", "success");
      setRelationshipEditor(null);
      reload();
    } catch (error) {
      relationshipFailure(error, "Could not save employer details.");
    } finally {
      setRelationshipSaving(null);
    }
  }

  async function saveProvider(event: React.FormEvent) {
    event.preventDefault();
    const assignmentChanged = providerId !== (patient.providerId ?? null);
    if (assignmentChanged && !providerReason.trim()) {
      setRelationshipError(
        "Enter a reason for changing the primary provider assignment.",
      );
      return;
    }
    setRelationshipSaving("provider");
    setRelationshipError(null);
    try {
      await updatePatientProviderAssignment(session.sessionId, patientId, {
        providerId,
        reason: assignmentChanged ? providerReason.trim() : null,
      });
      showToast("Primary provider assignment saved.", "success");
      setRelationshipEditor(null);
      setProviderReason("");
      setProviderHistoryRetry((current) => current + 1);
      reload();
    } catch (error) {
      relationshipFailure(
        error,
        "Could not save the primary provider assignment.",
      );
    } finally {
      setRelationshipSaving(null);
    }
  }

  function updateCareTeamMember(
    index: number,
    patch: Partial<CareTeamMemberDraft>,
  ) {
    setCareTeamForm((current) => ({
      ...current,
      members: current.members.map((member, memberIndex) =>
        memberIndex === index ? { ...member, ...patch } : member,
      ),
    }));
  }

  function selectCareTeamProvider(index: number, value: string) {
    const userId = value ? Number(value) : null;
    const provider = providerOptions?.providers.find(
      (option) => option.id === userId,
    );
    updateCareTeamMember(index, {
      memberType: "provider",
      userId,
      contactId: null,
      facilityId: provider?.facilityId ?? null,
    });
  }

  function selectCareTeamContact(index: number, value: string) {
    updateCareTeamMember(index, {
      memberType: "contact",
      userId: null,
      contactId: value ? Number(value) : null,
      facilityId: null,
    });
  }

  async function saveCareTeam(event: React.FormEvent) {
    event.preventDefault();
    const incompleteMember = careTeamForm.members.find(
      (member) =>
        (member.memberType === "provider" && !member.userId) ||
        (member.memberType === "contact" && !member.contactId),
    );
    if (incompleteMember) {
      setRelationshipError(
        "Choose a provider or patient contact for every care-team row, or remove the incomplete row.",
      );
      return;
    }

    setRelationshipSaving("care-team");
    setRelationshipError(null);
    try {
      await updatePatientCareTeam(session.sessionId, patientId, {
        teamName: careTeamForm.teamName,
        teamStatus: careTeamForm.teamStatus,
        members: careTeamForm.members.map(
          ({
            userId,
            contactId,
            role,
            facilityId,
            providerSince,
            status,
            note,
          }) => ({
            userId,
            contactId,
            role,
            facilityId,
            providerSince,
            status,
            note,
          }),
        ),
      });
      showToast("Care team saved.", "success");
      setRelationshipEditor(null);
      reload();
    } catch (error) {
      relationshipFailure(error, "Could not save the care team.");
    } finally {
      setRelationshipSaving(null);
    }
  }

  async function previewMerge(sourcePatientId: string) {
    setMergePreviewState({ status: "loading", sourcePatientId });
    setMergeAuditRationale("");
    setMergeAuditState({ status: "idle" });
    setMergeExecutionConfirmed(false);
    setMergeExecutionState({ status: "idle" });
    try {
      const preview = await getPatientMergePreview(
        session.sessionId,
        patientId,
        sourcePatientId,
      );
      setMergePreviewState({ status: "ready", data: preview });
    } catch {
      setMergePreviewState({
        status: "error",
        message:
          "Could not load this merge preview. The candidate may have changed.",
      });
    }
  }

  async function recordMergeReview() {
    if (mergePreviewState.status !== "ready") return;
    setMergeAuditState({ status: "saving" });
    try {
      const audit = await createPatientMergeAuditPlan(session.sessionId, {
        targetPatientId: mergePreviewState.data.targetPatient.canonicalId,
        sourcePatientId: mergePreviewState.data.sourcePatient.canonicalId,
        rationale: mergeAuditRationale || null,
      });
      setMergeAuditState({ status: "ready", auditId: audit.auditId });
      showToast(
        "Merge review evidence recorded. No records were merged.",
        "success",
      );
    } catch {
      setMergeAuditState({
        status: "error",
        message: "Could not record the merge review evidence.",
      });
    }
  }

  async function executeMerge() {
    if (mergeAuditState.status !== "ready" || !mergeExecutionConfirmed) return;
    setMergeExecutionState({ status: "saving" });
    try {
      const execution = await executePatientMerge(
        session.sessionId,
        mergeAuditState.auditId,
      );
      const movedCount = execution.movedRecords.reduce(
        (total, item) => total + item.recordCount,
        0,
      );
      setMergeExecutionState({
        status: "ready",
        executionId: execution.executionId,
        movedCount,
      });
      showToast(
        `Constrained merge completed with ${movedCount} manifest-recorded records.`,
        "success",
      );
      reload();
    } catch (error) {
      setMergeExecutionState({
        status: "error",
        message:
          error instanceof Error
            ? error.message
            : "The constrained merge was blocked.",
      });
    }
  }

  async function rollbackMerge(executionId: string) {
    if (
      !window.confirm(
        "Rollback this merge? Only the records listed in its immutable manifest will be restored to the source patient.",
      )
    )
      return;
    setMergeExecutionState({ status: "rolling-back", executionId });
    try {
      await rollbackPatientMerge(session.sessionId, executionId);
      setMergeExecutionState({ status: "rolled-back", executionId });
      showToast(
        "Merge rollback completed from the immutable manifest.",
        "success",
      );
      reload();
    } catch (error) {
      setMergeExecutionState({
        status: "error",
        message:
          error instanceof Error
            ? error.message
            : "Could not roll back the merge.",
      });
    }
  }

  async function changePortalAccess() {
    const enable = !patient.portalEnabled;
    if (
      !window.confirm(
        `${enable ? "Enable" : "Disable"} patient portal access for ${patient.displayName}?`,
      )
    )
      return;
    setPortalAction("access");
    try {
      await updatePatientPortalAccountAccess(
        session.sessionId,
        patientId,
        enable,
      );
      showToast(
        enable
          ? "Patient portal access enabled."
          : "Patient portal access disabled.",
        "success",
      );
      reload();
    } catch {
      showToast("Could not update patient portal access.", "error");
    } finally {
      setPortalAction(null);
    }
  }

  async function changePortalReset() {
    const issue = !patient.portalAccount?.oneTimeLinkPending;
    if (
      !window.confirm(
        issue
          ? "Mark a one-time portal reset link as pending? This local workflow does not deliver email or SMS."
          : "Clear the pending portal reset link?",
      )
    )
      return;
    setPortalAction("reset");
    try {
      await updatePatientPortalAccountReset(
        session.sessionId,
        patientId,
        issue,
      );
      showToast(
        issue
          ? "Portal reset link marked pending."
          : "Portal reset link cleared.",
        "success",
      );
      reload();
    } catch {
      showToast("Could not update portal reset state.", "error");
    } finally {
      setPortalAction(null);
    }
  }

  async function createRecordRequest() {
    setRecordRequestAction("create");
    try {
      await createPatientRecordRequest(session.sessionId, patientId);
      showToast("Patient record request recorded.", "success");
      await loadRecordRequests();
    } catch {
      showToast(
        "An open patient record request already exists or could not be recorded.",
        "error",
      );
    } finally {
      setRecordRequestAction(null);
    }
  }

  async function completeRecordRequest(request: PatientRecordRequest) {
    if (
      !window.confirm(
        "Mark this patient record request complete? This preserves its original request evidence.",
      )
    )
      return;
    setRecordRequestAction(request.requestId);
    try {
      await completePatientRecordRequest(
        session.sessionId,
        patientId,
        request.requestId,
      );
      showToast("Patient record request completed.", "success");
      await loadRecordRequests();
    } catch {
      showToast("Could not complete this patient record request.", "error");
    } finally {
      setRecordRequestAction(null);
    }
  }

  async function changePatientLifecycle(action: "retire" | "reactivate") {
    const reason = lifecycleReason.trim();
    if (!reason) {
      showToast("A lifecycle reason is required.", "error");
      return;
    }
    if (!window.confirm(`Do you want to ${action} this patient record?`)) {
      return;
    }

    setLifecycleAction(action);
    try {
      await transitionPatientLifecycle(
        session.sessionId,
        patientId,
        action,
        reason,
      );
      setLifecycleReason("");
      showToast(
        action === "retire"
          ? "Patient record retired. New scheduling is blocked."
          : "Patient record reactivated.",
        "success",
      );
      setLifecycleHistoryRetry((current) => current + 1);
      reload();
    } catch {
      showToast("The patient lifecycle change could not be saved.", "error");
    } finally {
      setLifecycleAction(null);
    }
  }

  async function saveDeceasedStatus(clearStatus = false) {
    const correctionReason = deceasedForm.correctionReason.trim();
    if (!correctionReason) {
      showToast("A deceased-status correction reason is required.", "error");
      return;
    }
    if (!clearStatus && !deceasedForm.deceasedDate) {
      showToast("Enter a deceased date or clear the status explicitly.", "error");
      return;
    }
    const action = clearStatus ? "clear" : patient.deceasedDate ? "correct" : "record";
    if (!window.confirm(`Do you want to ${action} this deceased status?`)) {
      return;
    }

    setDeceasedSaving(true);
    try {
      await updatePatientDeceasedStatus(session.sessionId, patientId, {
        deceasedDate: clearStatus ? null : deceasedForm.deceasedDate,
        deceasedReason: clearStatus ? null : deceasedForm.deceasedReason,
        correctionReason,
      });
      setDeceasedForm((current) => ({ ...current, correctionReason: "" }));
      showToast(
        clearStatus ? "Deceased status cleared." : "Deceased status saved.",
        "success",
      );
      setDeceasedHistoryRetry((current) => current + 1);
      reload();
    } catch {
      showToast("The deceased status could not be saved.", "error");
    } finally {
      setDeceasedSaving(false);
    }
  }

  const setIns = (patch: Partial<PatientInsuranceMutationInput>) =>
    setInsForm((f) => ({ ...f, ...patch }));

  const providerAssignmentChanged =
    providerId !== (patient.providerId ?? null);
  const currentProviderFacility =
    providerHistoryState.status === "ready"
      ? providerHistoryState.data.currentFacilityName
      : providerOptions?.providers.find(
          (provider) => provider.id === (patient.providerId ?? null),
        )?.facilityName;
  const administrationHistoryEvents =
    administrationHistoryState.status === "ready"
      ? administrationHistoryState.data.events.filter(
          (event) =>
            administrationArea === "all" || event.area === administrationArea,
        )
      : [];

  return (
    <div className="clinician-page">
      <div
        style={{
          display: "flex",
          justifyContent: "flex-end",
          marginBottom: 12,
        }}
      >
        <button
          className="cl-btn-secondary"
          type="button"
          onClick={() => window.print()}
        >
          <Printer size={14} /> Print summary
        </button>
      </div>

      {patient.lifecycleStatus === "retired" ? (
        <div className="error-banner" role="status">
          This patient record is retired. Existing chart history remains available,
          but new and rescheduled appointments are blocked until reactivation.
        </div>
      ) : null}

      <section className="cl-card" aria-labelledby="patient-lifecycle-heading">
        <div className="cl-card-header">
          <h2 className="cl-card-title" id="patient-lifecycle-heading">
            <ArchiveRestore size={15} /> Patient lifecycle
          </h2>
          <span className="status-badge">{patient.lifecycleStatus}</span>
        </div>
        <p className="muted-text">
          Retirement preserves the chart and its audit history while blocking new
          or rescheduled appointments. It does not delete clinical records.
        </p>
        {patient.lifecycleStatus === "retired" ? (
          <ul className="fact-list">
            {fact("Retired at", patient.retiredAt)}
            {fact("Retired by", patient.retiredBy)}
            {fact("Retirement reason", patient.retirementReason)}
          </ul>
        ) : null}
        <div className="field" style={{ marginTop: 12 }}>
          <label className="label" htmlFor="patient-lifecycle-reason">
            {patient.lifecycleStatus === "retired"
              ? "Reactivation reason"
              : "Retirement reason"}
          </label>
          <input
            id="patient-lifecycle-reason"
            className="input"
            value={lifecycleReason}
            maxLength={500}
            onChange={(event) => setLifecycleReason(event.target.value)}
            placeholder="Required, retained in the lifecycle history"
          />
        </div>
        <div className="cl-inline-form-actions" style={{ marginTop: 12 }}>
          <button
            className={
              patient.lifecycleStatus === "retired"
                ? "cl-btn-primary"
                : "cl-btn-secondary"
            }
            type="button"
            disabled={lifecycleAction !== null}
            onClick={() =>
              void changePatientLifecycle(
                patient.lifecycleStatus === "retired" ? "reactivate" : "retire",
              )
            }
          >
            {lifecycleAction !== null
              ? "Saving…"
              : patient.lifecycleStatus === "retired"
                ? "Reactivate patient"
                : "Retire patient"}
          </button>
        </div>
        {lifecycleHistoryState.status === "loading" ? (
          <p className="muted-text">Loading lifecycle history…</p>
        ) : lifecycleHistoryState.status === "error" ? (
          <button
            className="cl-link"
            type="button"
            onClick={() => setLifecycleHistoryRetry((current) => current + 1)}
          >
            Retry lifecycle history
          </button>
        ) : lifecycleHistoryState.data.events.length === 0 ? (
          <p className="muted-text">No lifecycle changes have been recorded.</p>
        ) : (
          <ol className="administration-history-list">
            {lifecycleHistoryState.data.events.slice(0, 10).map((event) => (
              <li key={event.eventId}>
                <div className="administration-history-event-heading">
                  <strong>{event.action}</strong>
                  <span>{new Date(event.occurredAt).toLocaleString()}</span>
                </div>
                <p className="administration-history-actor">
                  {event.priorStatus} → {event.resultingStatus} by {event.actor}
                </p>
                <p>{event.reason}</p>
              </li>
            ))}
          </ol>
        )}
      </section>

      <section className="cl-card" aria-labelledby="deceased-status-heading">
        <div className="cl-card-header">
          <h2 className="cl-card-title" id="deceased-status-heading">
            <ArchiveRestore size={15} /> Deceased status
          </h2>
          <span className="status-badge">
            {patient.deceasedDate ? "recorded" : "not recorded"}
          </span>
        </div>
        <p className="muted-text">
          Legacy behavior rejects future deceased dates. Every record, correction,
          or clearance here retains the authenticated actor, time, and reason.
          A recorded deceased status blocks new and rescheduled appointments.
        </p>
        <div className="form-row">
          <div className="field">
            <label className="label" htmlFor="patient-deceased-date">
              Deceased date
            </label>
            <input
              id="patient-deceased-date"
              type="date"
              className="input"
              max={new Date().toISOString().slice(0, 10)}
              value={deceasedForm.deceasedDate}
              onChange={(event) =>
                setDeceasedForm((current) => ({
                  ...current,
                  deceasedDate: event.target.value,
                }))
              }
            />
          </div>
          <div className="field">
            <label className="label" htmlFor="patient-deceased-reason">
              Clinical or administrative note
            </label>
            <input
              id="patient-deceased-reason"
              className="input"
              value={deceasedForm.deceasedReason}
              onChange={(event) =>
                setDeceasedForm((current) => ({
                  ...current,
                  deceasedReason: event.target.value,
                }))
              }
            />
          </div>
        </div>
        <div className="field" style={{ marginTop: 12 }}>
          <label className="label" htmlFor="patient-deceased-correction-reason">
            Record or correction reason
          </label>
          <input
            id="patient-deceased-correction-reason"
            className="input"
            maxLength={500}
            value={deceasedForm.correctionReason}
            onChange={(event) =>
              setDeceasedForm((current) => ({
                ...current,
                correctionReason: event.target.value,
              }))
            }
            placeholder="Required, retained in the correction history"
          />
        </div>
        <div className="cl-inline-form-actions" style={{ marginTop: 12 }}>
          <button
            className="cl-btn-primary"
            type="button"
            disabled={deceasedSaving}
            onClick={() => void saveDeceasedStatus()}
          >
            {deceasedSaving
              ? "Saving…"
              : patient.deceasedDate
                ? "Save correction"
                : "Record deceased status"}
          </button>
          {patient.deceasedDate ? (
            <button
              className="cl-btn-secondary"
              type="button"
              disabled={deceasedSaving}
              onClick={() => void saveDeceasedStatus(true)}
            >
              Clear deceased status
            </button>
          ) : null}
        </div>
        {deceasedHistoryState.status === "loading" ? (
          <p className="muted-text">Loading deceased-status history…</p>
        ) : deceasedHistoryState.status === "error" ? (
          <button
            className="cl-link"
            type="button"
            onClick={() => setDeceasedHistoryRetry((current) => current + 1)}
          >
            Retry deceased-status history
          </button>
        ) : deceasedHistoryState.data.events.length === 0 ? (
          <p className="muted-text">No deceased-status changes have been recorded.</p>
        ) : (
          <ol className="administration-history-list">
            {deceasedHistoryState.data.events.slice(0, 10).map((event) => (
              <li key={event.eventId}>
                <div className="administration-history-event-heading">
                  <strong>{event.action}</strong>
                  <span>{new Date(event.occurredAt).toLocaleString()}</span>
                </div>
                <p className="administration-history-actor">
                  {event.priorDeceasedDate ?? "not recorded"} → {event.resultingDeceasedDate ?? "cleared"} by {event.actor}
                </p>
                <p>{event.correctionReason}</p>
              </li>
            ))}
          </ol>
        )}
      </section>

      {/* Insurance modal */}
      {insMode.kind !== "none" && (
        <div
          className="modal-overlay"
          onClick={(e) => {
            if (e.target === e.currentTarget) setInsMode({ kind: "none" });
          }}
        >
          <div className="modal-panel" role="dialog" aria-modal="true">
            <div className="modal-header">
              <h2 className="modal-title">
                {insMode.kind === "add" ? "Add insurance" : "Edit insurance"}
              </h2>
              <button
                className="modal-close"
                type="button"
                onClick={() => setInsMode({ kind: "none" })}
                aria-label="Close"
              >
                ×
              </button>
            </div>
            <form onSubmit={handleSaveInsurance}>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="ins-type">
                    Coverage type
                  </label>
                  <select
                    id="ins-type"
                    className="select"
                    value={insForm.type}
                    onChange={(e) => setIns({ type: e.target.value })}
                  >
                    <option value="primary">Primary</option>
                    <option value="secondary">Secondary</option>
                    <option value="tertiary">Tertiary</option>
                  </select>
                </div>
                <div className="field">
                  <label className="label" htmlFor="ins-rel">
                    Relationship
                  </label>
                  <select
                    id="ins-rel"
                    className="select"
                    value={insForm.relationship}
                    onChange={(e) => setIns({ relationship: e.target.value })}
                  >
                    <option value="self">Self</option>
                    <option value="spouse">Spouse</option>
                    <option value="child">Child</option>
                    <option value="other">Other</option>
                  </select>
                </div>
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="ins-provider">
                    Insurance company
                  </label>
                  <input
                    id="ins-provider"
                    className="input"
                    value={insForm.provider}
                    onChange={(e) => setIns({ provider: e.target.value })}
                    required
                  />
                </div>
                <div className="field">
                  <label className="label" htmlFor="ins-plan">
                    Plan name
                  </label>
                  <input
                    id="ins-plan"
                    className="input"
                    value={insForm.planName}
                    onChange={(e) => setIns({ planName: e.target.value })}
                  />
                </div>
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="ins-policy">
                    Policy number
                  </label>
                  <input
                    id="ins-policy"
                    className="input"
                    value={insForm.policyNumber}
                    onChange={(e) => setIns({ policyNumber: e.target.value })}
                  />
                </div>
                <div className="field">
                  <label className="label" htmlFor="ins-group">
                    Group number
                  </label>
                  <input
                    id="ins-group"
                    className="input"
                    value={insForm.groupNumber}
                    onChange={(e) => setIns({ groupNumber: e.target.value })}
                  />
                </div>
              </div>
              <p className="cl-form-section-label">Subscriber</p>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="ins-sub-first">
                    First name
                  </label>
                  <input
                    id="ins-sub-first"
                    className="input"
                    value={insForm.subscriberFirstName}
                    onChange={(e) =>
                      setIns({ subscriberFirstName: e.target.value })
                    }
                  />
                </div>
                <div className="field">
                  <label className="label" htmlFor="ins-sub-last">
                    Last name
                  </label>
                  <input
                    id="ins-sub-last"
                    className="input"
                    value={insForm.subscriberLastName}
                    onChange={(e) =>
                      setIns({ subscriberLastName: e.target.value })
                    }
                  />
                </div>
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="ins-sub-dob">
                    Date of birth
                  </label>
                  <input
                    id="ins-sub-dob"
                    type="date"
                    className="input"
                    value={insForm.subscriberDateOfBirth}
                    onChange={(e) =>
                      setIns({ subscriberDateOfBirth: e.target.value })
                    }
                  />
                </div>
                <div className="field">
                  <label className="label" htmlFor="ins-sub-sex">
                    Sex
                  </label>
                  <select
                    id="ins-sub-sex"
                    className="select"
                    value={insForm.subscriberSex}
                    onChange={(e) => setIns({ subscriberSex: e.target.value })}
                  >
                    <option value="unknown">Unknown</option>
                    <option value="male">Male</option>
                    <option value="female">Female</option>
                  </select>
                </div>
              </div>
              <div className="button-row">
                <button
                  className="button-primary"
                  type="submit"
                  disabled={saving}
                >
                  {saving
                    ? "Saving…"
                    : insMode.kind === "add"
                      ? "Add insurance"
                      : "Save changes"}
                </button>
                <button
                  className="button-secondary"
                  type="button"
                  onClick={() => setInsMode({ kind: "none" })}
                  style={{ flex: "none", width: "auto" }}
                >
                  Cancel
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      <div className="cl-grid-two print-summary">
        {/* Demographics */}
        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title">
              <Phone size={15} /> Contact & demographics
            </h2>
            <button
              className="cl-link"
              type="button"
              onClick={() => setEditDemoOpen((o) => !o)}
            >
              {editDemoOpen ? "Cancel" : "Edit"}
            </button>
          </div>
          {editDemoOpen ? (
            <form onSubmit={handleSaveDemographics}>
              <p className="cl-form-section-label">Name</p>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="demo-first">
                    First name
                  </label>
                  <input
                    id="demo-first"
                    className="input"
                    value={demoForm.firstName}
                    onChange={(e) =>
                      setDemoForm((f) => ({ ...f, firstName: e.target.value }))
                    }
                    required
                  />
                </div>
                <div className="field">
                  <label className="label" htmlFor="demo-last">
                    Last name
                  </label>
                  <input
                    id="demo-last"
                    className="input"
                    value={demoForm.lastName}
                    onChange={(e) =>
                      setDemoForm((f) => ({ ...f, lastName: e.target.value }))
                    }
                    required
                  />
                </div>
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="demo-pref">
                    Preferred name
                  </label>
                  <input
                    id="demo-pref"
                    className="input"
                    value={demoForm.preferredName}
                    onChange={(e) =>
                      setDemoForm((f) => ({
                        ...f,
                        preferredName: e.target.value,
                      }))
                    }
                  />
                </div>
                <div className="field">
                  <label className="label" htmlFor="demo-sex">
                    Sex
                  </label>
                  <select
                    id="demo-sex"
                    className="select"
                    value={demoForm.sex}
                    onChange={(e) =>
                      setDemoForm((f) => ({ ...f, sex: e.target.value }))
                    }
                  >
                    <option value="">Select</option>
                    <option value="Male">Male</option>
                    <option value="Female">Female</option>
                    <option value="Unknown">Unknown</option>
                  </select>
                </div>
              </div>
              <div className="field">
                <label className="label" htmlFor="demo-dob">
                  Date of birth
                </label>
                <input
                  id="demo-dob"
                  type="date"
                  className="input"
                  value={demoForm.dateOfBirth}
                  onChange={(e) =>
                    setDemoForm((f) => ({ ...f, dateOfBirth: e.target.value }))
                  }
                  required
                />
              </div>
              <p className="cl-form-section-label" style={{ marginTop: 12 }}>
                Contact
              </p>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="demo-phone">
                    Home phone
                  </label>
                  <input
                    id="demo-phone"
                    type="tel"
                    className="input"
                    value={contactForm.phoneHome}
                    onChange={(e) =>
                      setContactForm((f) => ({
                        ...f,
                        phoneHome: e.target.value,
                      }))
                    }
                  />
                </div>
                <div className="field">
                  <label className="label" htmlFor="demo-cell">
                    Cell phone
                  </label>
                  <input
                    id="demo-cell"
                    type="tel"
                    className="input"
                    value={contactForm.phoneCell}
                    onChange={(e) =>
                      setContactForm((f) => ({
                        ...f,
                        phoneCell: e.target.value,
                      }))
                    }
                  />
                </div>
              </div>
              <div className="field">
                <label className="label" htmlFor="demo-email">
                  Email
                </label>
                <input
                  id="demo-email"
                  type="email"
                  className="input"
                  value={contactForm.email}
                  onChange={(e) =>
                    setContactForm((f) => ({ ...f, email: e.target.value }))
                  }
                />
              </div>
              <p className="cl-form-section-label" style={{ marginTop: 12 }}>
                Address
              </p>
              <div className="field">
                <label className="label" htmlFor="demo-street">
                  Street
                </label>
                <input
                  id="demo-street"
                  className="input"
                  value={demoForm.street}
                  onChange={(e) =>
                    setDemoForm((f) => ({ ...f, street: e.target.value }))
                  }
                />
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="demo-city">
                    City
                  </label>
                  <input
                    id="demo-city"
                    className="input"
                    value={demoForm.city}
                    onChange={(e) =>
                      setDemoForm((f) => ({ ...f, city: e.target.value }))
                    }
                  />
                </div>
                <div className="field">
                  <label className="label" htmlFor="demo-state">
                    State
                  </label>
                  <input
                    id="demo-state"
                    className="input"
                    maxLength={2}
                    value={demoForm.state}
                    onChange={(e) =>
                      setDemoForm((f) => ({ ...f, state: e.target.value }))
                    }
                  />
                </div>
                <div className="field">
                  <label className="label" htmlFor="demo-zip">
                    ZIP
                  </label>
                  <input
                    id="demo-zip"
                    className="input"
                    value={demoForm.postalCode}
                    onChange={(e) =>
                      setDemoForm((f) => ({ ...f, postalCode: e.target.value }))
                    }
                  />
                </div>
              </div>
              <p className="cl-form-section-label" style={{ marginTop: 12 }}>
                Additional
              </p>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="demo-marital">
                    Marital status
                  </label>
                  <select
                    id="demo-marital"
                    className="select"
                    value={demoForm.maritalStatus}
                    onChange={(e) =>
                      setDemoForm((f) => ({
                        ...f,
                        maritalStatus: e.target.value,
                      }))
                    }
                  >
                    <option value="">Select</option>
                    <option value="Single">Single</option>
                    <option value="Married">Married</option>
                    <option value="Divorced">Divorced</option>
                    <option value="Widowed">Widowed</option>
                    <option value="Separated">Separated</option>
                    <option value="Partner">Partner</option>
                  </select>
                </div>
                <div className="field">
                  <label className="label" htmlFor="demo-race">
                    Race
                  </label>
                  <input
                    id="demo-race"
                    className="input"
                    value={demoForm.race}
                    onChange={(e) =>
                      setDemoForm((f) => ({ ...f, race: e.target.value }))
                    }
                  />
                </div>
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="demo-ethnicity">
                    Ethnicity
                  </label>
                  <input
                    id="demo-ethnicity"
                    className="input"
                    value={demoForm.ethnicity}
                    onChange={(e) =>
                      setDemoForm((f) => ({ ...f, ethnicity: e.target.value }))
                    }
                  />
                </div>
                <div className="field">
                  <label className="label" htmlFor="demo-occupation">
                    Occupation
                  </label>
                  <input
                    id="demo-occupation"
                    className="input"
                    value={demoForm.occupation}
                    onChange={(e) =>
                      setDemoForm((f) => ({ ...f, occupation: e.target.value }))
                    }
                  />
                </div>
              </div>
              <div className="cl-inline-form-actions" style={{ marginTop: 16 }}>
                <button
                  className="cl-btn-primary"
                  type="submit"
                  disabled={saving}
                >
                  {saving ? "Saving…" : "Save changes"}
                </button>
                <button
                  className="cl-btn-secondary"
                  type="button"
                  onClick={() => setEditDemoOpen(false)}
                >
                  Cancel
                </button>
              </div>
            </form>
          ) : (
            <ul className="fact-list">
              {fact("Date of birth", patient.dateOfBirth)}
              {fact("Age", `${patient.age}y`)}
              {fact("Sex", patient.sex)}
              {fact("Phone", patient.phone ?? patient.phoneCell)}
              {fact("Email", patient.email)}
              {fact(
                "Address",
                [
                  patient.street,
                  patient.city,
                  patient.state,
                  patient.postalCode,
                ]
                  .filter(Boolean)
                  .join(", "),
              )}
              {fact("Marital status", patient.maritalStatus)}
              {fact(
                "Race / Ethnicity",
                [patient.race, patient.ethnicity].filter(Boolean).join(" / "),
              )}
              {fact("Occupation", patient.occupation)}
              {fact("Primary provider", patient.primaryProviderName)}
              {fact("Facility", patient.facilityName)}
              {fact("Patient since", patient.registrationDate)}
              {patient.deceasedDate && fact("Deceased", patient.deceasedDate)}
            </ul>
          )}
        </section>

        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title">
              <UserPlus size={15} /> Guardian or representative
            </h2>
            {relationshipEditor !== "guardian" && (
              <button
                className="cl-link"
                type="button"
                onClick={() => openRelationshipEditor("guardian")}
              >
                Edit
              </button>
            )}
          </div>
          {relationshipEditor === "guardian" ? (
            <form
              className="cl-relationship-form"
              onSubmit={(event) => void saveGuardian(event)}
            >
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="guardian-mother-name">
                    Mother name
                  </label>
                  <input
                    id="guardian-mother-name"
                    className="input"
                    value={guardianForm.motherName}
                    onChange={(event) =>
                      setGuardianForm((current) => ({
                        ...current,
                        motherName: event.target.value,
                      }))
                    }
                  />
                </div>
                <div className="field">
                  <label className="label" htmlFor="guardian-name">
                    Guardian or representative
                  </label>
                  <input
                    id="guardian-name"
                    className="input"
                    value={guardianForm.guardianName}
                    onChange={(event) =>
                      setGuardianForm((current) => ({
                        ...current,
                        guardianName: event.target.value,
                      }))
                    }
                  />
                </div>
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="guardian-relationship">
                    Relationship
                  </label>
                  <select
                    id="guardian-relationship"
                    className="select"
                    value={guardianForm.guardianRelationship}
                    onChange={(event) =>
                      setGuardianForm((current) => ({
                        ...current,
                        guardianRelationship: event.target.value,
                      }))
                    }
                  >
                    <option value="">Unspecified</option>
                    <option value="guardian">Guardian</option>
                    <option value="parent">Parent</option>
                    <option value="mother">Mother</option>
                    <option value="father">Father</option>
                    <option value="spouse">Spouse</option>
                    <option value="child">Child</option>
                    <option value="sibling">Sibling</option>
                    <option value="care_giver">Caregiver</option>
                    <option value="associate">Associate</option>
                  </select>
                </div>
                <div className="field">
                  <label className="label" htmlFor="guardian-sex">
                    Sex
                  </label>
                  <select
                    id="guardian-sex"
                    className="select"
                    value={guardianForm.guardianSex}
                    onChange={(event) =>
                      setGuardianForm((current) => ({
                        ...current,
                        guardianSex: event.target.value,
                      }))
                    }
                  >
                    <option value="">Unspecified</option>
                    <option value="Female">Female</option>
                    <option value="Male">Male</option>
                    <option value="UNK">Unknown</option>
                  </select>
                </div>
              </div>
              <div className="field">
                <label className="label" htmlFor="guardian-address">
                  Street address
                </label>
                <input
                  id="guardian-address"
                  className="input"
                  value={guardianForm.guardianAddress}
                  onChange={(event) =>
                    setGuardianForm((current) => ({
                      ...current,
                      guardianAddress: event.target.value,
                    }))
                  }
                />
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="guardian-city">
                    City
                  </label>
                  <input
                    id="guardian-city"
                    className="input"
                    value={guardianForm.guardianCity}
                    onChange={(event) =>
                      setGuardianForm((current) => ({
                        ...current,
                        guardianCity: event.target.value,
                      }))
                    }
                  />
                </div>
                <div className="field">
                  <label className="label" htmlFor="guardian-state">
                    State
                  </label>
                  <input
                    id="guardian-state"
                    className="input"
                    value={guardianForm.guardianState}
                    onChange={(event) =>
                      setGuardianForm((current) => ({
                        ...current,
                        guardianState: event.target.value,
                      }))
                    }
                  />
                </div>
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="guardian-postal-code">
                    Postal code
                  </label>
                  <input
                    id="guardian-postal-code"
                    className="input"
                    value={guardianForm.guardianPostalCode}
                    onChange={(event) =>
                      setGuardianForm((current) => ({
                        ...current,
                        guardianPostalCode: event.target.value,
                      }))
                    }
                  />
                </div>
                <div className="field">
                  <label className="label" htmlFor="guardian-country">
                    Country
                  </label>
                  <input
                    id="guardian-country"
                    className="input"
                    value={guardianForm.guardianCountry}
                    onChange={(event) =>
                      setGuardianForm((current) => ({
                        ...current,
                        guardianCountry: event.target.value,
                      }))
                    }
                  />
                </div>
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="guardian-phone">
                    Phone
                  </label>
                  <input
                    id="guardian-phone"
                    className="input"
                    type="tel"
                    value={guardianForm.guardianPhone}
                    onChange={(event) =>
                      setGuardianForm((current) => ({
                        ...current,
                        guardianPhone: event.target.value,
                      }))
                    }
                  />
                </div>
                <div className="field">
                  <label className="label" htmlFor="guardian-work-phone">
                    Work phone
                  </label>
                  <input
                    id="guardian-work-phone"
                    className="input"
                    type="tel"
                    value={guardianForm.guardianWorkPhone}
                    onChange={(event) =>
                      setGuardianForm((current) => ({
                        ...current,
                        guardianWorkPhone: event.target.value,
                      }))
                    }
                  />
                </div>
              </div>
              <div className="field">
                <label className="label" htmlFor="guardian-email">
                  Email
                </label>
                <input
                  id="guardian-email"
                  className="input"
                  type="email"
                  value={guardianForm.guardianEmail}
                  onChange={(event) =>
                    setGuardianForm((current) => ({
                      ...current,
                      guardianEmail: event.target.value,
                    }))
                  }
                />
              </div>
              {relationshipError && (
                <p className="cl-form-error" role="alert">
                  {relationshipError}
                </p>
              )}
              <div className="cl-inline-form-actions">
                <button
                  className="cl-btn-primary"
                  type="submit"
                  disabled={relationshipSaving === "guardian"}
                >
                  {relationshipSaving === "guardian"
                    ? "Saving…"
                    : "Save representative"}
                </button>
                <button
                  className="cl-btn-secondary"
                  type="button"
                  onClick={closeRelationshipEditor}
                >
                  Cancel
                </button>
              </div>
            </form>
          ) : (
            <>
              <ul className="fact-list">
                {fact("Mother name", patient.motherName)}
                {fact("Guardian", patient.guardianName)}
                {fact("Relationship", patient.guardianRelationship)}
                {fact("Sex", patient.guardianSex)}
                {fact(
                  "Address",
                  formatAddress(
                    patient.guardianAddress,
                    patient.guardianCity,
                    patient.guardianState,
                    patient.guardianPostalCode,
                    patient.guardianCountry,
                  ),
                )}
                {fact("Phone", patient.guardianPhone)}
                {fact("Work phone", patient.guardianWorkPhone)}
                {fact("Email", patient.guardianEmail)}
              </ul>
              {!patient.motherName && !patient.guardianName && (
                <p className="cl-empty-text">
                  No guardian or representative is recorded.
                </p>
              )}
            </>
          )}
        </section>

        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title">
              <Building2 size={15} /> Employer
            </h2>
            {relationshipEditor !== "employer" && (
              <button
                className="cl-link"
                type="button"
                onClick={() => openRelationshipEditor("employer")}
              >
                Edit
              </button>
            )}
          </div>
          {relationshipEditor === "employer" ? (
            <form
              className="cl-relationship-form"
              onSubmit={(event) => void saveEmployer(event)}
            >
              <div className="field">
                <label className="label" htmlFor="employer-name">
                  Employer name
                </label>
                <input
                  id="employer-name"
                  className="input"
                  value={employerForm.employerName}
                  onChange={(event) =>
                    setEmployerForm((current) => ({
                      ...current,
                      employerName: event.target.value,
                    }))
                  }
                />
              </div>
              <div className="field">
                <label className="label" htmlFor="employer-street">
                  Street address
                </label>
                <input
                  id="employer-street"
                  className="input"
                  value={employerForm.employerStreet}
                  onChange={(event) =>
                    setEmployerForm((current) => ({
                      ...current,
                      employerStreet: event.target.value,
                    }))
                  }
                />
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="employer-city">
                    City
                  </label>
                  <input
                    id="employer-city"
                    className="input"
                    value={employerForm.employerCity}
                    onChange={(event) =>
                      setEmployerForm((current) => ({
                        ...current,
                        employerCity: event.target.value,
                      }))
                    }
                  />
                </div>
                <div className="field">
                  <label className="label" htmlFor="employer-state">
                    State
                  </label>
                  <input
                    id="employer-state"
                    className="input"
                    value={employerForm.employerState}
                    onChange={(event) =>
                      setEmployerForm((current) => ({
                        ...current,
                        employerState: event.target.value,
                      }))
                    }
                  />
                </div>
              </div>
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="employer-postal-code">
                    Postal code
                  </label>
                  <input
                    id="employer-postal-code"
                    className="input"
                    value={employerForm.employerPostalCode}
                    onChange={(event) =>
                      setEmployerForm((current) => ({
                        ...current,
                        employerPostalCode: event.target.value,
                      }))
                    }
                  />
                </div>
                <div className="field">
                  <label className="label" htmlFor="employer-country">
                    Country
                  </label>
                  <input
                    id="employer-country"
                    className="input"
                    value={employerForm.employerCountry}
                    onChange={(event) =>
                      setEmployerForm((current) => ({
                        ...current,
                        employerCountry: event.target.value,
                      }))
                    }
                  />
                </div>
              </div>
              {relationshipError && (
                <p className="cl-form-error" role="alert">
                  {relationshipError}
                </p>
              )}
              <div className="cl-inline-form-actions">
                <button
                  className="cl-btn-primary"
                  type="submit"
                  disabled={relationshipSaving === "employer"}
                >
                  {relationshipSaving === "employer"
                    ? "Saving…"
                    : "Save employer"}
                </button>
                <button
                  className="cl-btn-secondary"
                  type="button"
                  onClick={closeRelationshipEditor}
                >
                  Cancel
                </button>
              </div>
            </form>
          ) : patient.employerName ? (
            <ul className="fact-list">
              {fact("Employer", patient.employerName)}
              {fact(
                "Address",
                formatAddress(
                  patient.employerStreet,
                  patient.employerCity,
                  patient.employerState,
                  patient.employerPostalCode,
                  patient.employerCountry,
                ),
              )}
            </ul>
          ) : (
            <p className="cl-empty-text">No employer is recorded.</p>
          )}
        </section>

        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title">
              <Stethoscope size={15} /> Primary provider
            </h2>
            {relationshipEditor !== "provider" && (
              <button
                className="cl-link"
                type="button"
                onClick={() => openRelationshipEditor("provider")}
                disabled={relationshipOptionsState !== "ready"}
              >
                Edit
              </button>
            )}
          </div>
          {relationshipOptionsState === "error" && (
            <div className="cl-inline-error" role="alert">
              <span>Provider options are unavailable.</span>
              <button
                className="cl-link"
                type="button"
                onClick={() => {
                  setRelationshipOptionsState("loading");
                  setRelationshipOptionsRetry((current) => current + 1);
                }}
              >
                Retry
              </button>
            </div>
          )}
          {relationshipEditor === "provider" ? (
            <form onSubmit={(event) => void saveProvider(event)}>
              <div className="field">
                <label className="label" htmlFor="patient-primary-provider">
                  Provider
                </label>
                <select
                  id="patient-primary-provider"
                  className="select"
                  value={providerId ?? ""}
                  onChange={(event) =>
                    setProviderId(
                      event.target.value ? Number(event.target.value) : null,
                    )
                  }
                >
                  <option value="">Unassigned</option>
                  {providerOptions?.providers.map((provider) => (
                    <option key={provider.id} value={provider.id}>
                      {provider.displayName}
                      {provider.facilityName
                        ? ` — ${provider.facilityName}`
                        : ""}
                    </option>
                  ))}
                </select>
              </div>
              {providerAssignmentChanged && (
                <div className="field">
                  <label
                    className="label"
                    htmlFor="patient-primary-provider-reason"
                  >
                    Change reason
                  </label>
                  <textarea
                    id="patient-primary-provider-reason"
                    className="textarea"
                    value={providerReason}
                    maxLength={250}
                    required
                    rows={3}
                    placeholder="Why is this assignment changing?"
                    onChange={(event) => setProviderReason(event.target.value)}
                  />
                  <span className="field-hint">
                    Required for a change · {providerReason.length}/250
                  </span>
                </div>
              )}
              {relationshipError && (
                <p className="cl-form-error" role="alert">
                  {relationshipError}
                </p>
              )}
              <div className="cl-inline-form-actions">
                <button
                  className="cl-btn-primary"
                  type="submit"
                  disabled={
                    relationshipSaving === "provider" ||
                    relationshipOptionsState !== "ready" ||
                    (providerAssignmentChanged && !providerReason.trim())
                  }
                >
                  {relationshipSaving === "provider"
                    ? "Saving…"
                    : "Save provider"}
                </button>
                <button
                  className="cl-btn-secondary"
                  type="button"
                  onClick={closeRelationshipEditor}
                >
                  Cancel
                </button>
              </div>
            </form>
          ) : (
            <ul className="fact-list">
              {fact(
                "Primary provider",
                patient.primaryProviderName ?? "Unassigned",
              )}
              {fact("Provider facility", currentProviderFacility)}
            </ul>
          )}
          <div className="provider-history">
            <div className="provider-history-heading">
              <h3>Assignment history</h3>
              {providerHistoryState.status === "ready" && (
                <span>
                  {providerHistoryState.data.returnedCount <
                  providerHistoryState.data.eventCount
                    ? `${providerHistoryState.data.returnedCount} newest of ${providerHistoryState.data.eventCount} changes`
                    : `${providerHistoryState.data.eventCount} ${
                        providerHistoryState.data.eventCount === 1
                          ? "change"
                          : "changes"
                      }`}
                </span>
              )}
            </div>
            {providerHistoryState.status === "loading" && (
              <p className="cl-empty-text" role="status">
                Loading assignment history…
              </p>
            )}
            {providerHistoryState.status === "error" && (
              <div className="cl-inline-error" role="alert">
                <span>Assignment history is unavailable.</span>
                <button
                  className="cl-link"
                  type="button"
                  onClick={() =>
                    setProviderHistoryRetry((current) => current + 1)
                  }
                >
                  Retry
                </button>
              </div>
            )}
            {providerHistoryState.status === "ready" &&
              providerHistoryState.data.events.length === 0 && (
                <p className="cl-empty-text">
                  No primary-provider changes have been recorded.
                </p>
              )}
            {providerHistoryState.status === "ready" &&
              providerHistoryState.data.events.length > 0 && (
                <ol className="provider-history-list">
                  {providerHistoryState.data.events.map((assignment) => (
                    <li key={assignment.eventId}>
                      <div>
                        <strong>
                          {assignment.fromProviderName ?? "Unassigned"}
                          {assignment.fromFacilityName
                            ? ` (${assignment.fromFacilityName})`
                            : ""}{" "}
                          →{" "}
                          {assignment.toProviderName ?? "Unassigned"}
                          {assignment.toFacilityName
                            ? ` (${assignment.toFacilityName})`
                            : ""}
                        </strong>
                        <time dateTime={assignment.occurredAt}>
                          {new Date(assignment.occurredAt).toLocaleString()}
                        </time>
                      </div>
                      <p>{assignment.reason}</p>
                      <p>
                        By {assignment.actor}
                      </p>
                    </li>
                  ))}
                </ol>
              )}
          </div>
        </section>

        <section className="cl-card cl-card-wide">
          <div className="cl-card-header">
            <div>
              <h2 className="cl-card-title">
                <UserPlus size={15} /> Care team
              </h2>
              <p className="cl-empty-text">
                Maintain provider and patient-contact members, roles, effective
                dates, and lifecycle status.
              </p>
            </div>
            {relationshipEditor !== "care-team" && (
              <button
                className="cl-link"
                type="button"
                onClick={() => openRelationshipEditor("care-team")}
                disabled={relationshipOptionsState !== "ready"}
              >
                Edit care team
              </button>
            )}
          </div>
          {relationshipEditor === "care-team" ? (
            <form
              className="cl-care-team-form"
              onSubmit={(event) => void saveCareTeam(event)}
            >
              <div className="form-row">
                <div className="field">
                  <label className="label" htmlFor="care-team-name">
                    Team name
                  </label>
                  <input
                    id="care-team-name"
                    className="input"
                    value={careTeamForm.teamName}
                    onChange={(event) =>
                      setCareTeamForm((current) => ({
                        ...current,
                        teamName: event.target.value,
                      }))
                    }
                  />
                </div>
                <div className="field">
                  <label className="label" htmlFor="care-team-status">
                    Team status
                  </label>
                  <select
                    id="care-team-status"
                    className="select"
                    value={careTeamForm.teamStatus}
                    onChange={(event) =>
                      setCareTeamForm((current) => ({
                        ...current,
                        teamStatus: event.target.value,
                      }))
                    }
                  >
                    {careTeamStatusOptions.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
              <div className="cl-care-team-editor-list">
                {careTeamForm.members.map((member, index) => (
                  <fieldset
                    className="cl-care-team-member-editor"
                    key={`care-team-member-${index}`}
                  >
                    <legend>Member {index + 1}</legend>
                    <button
                      className="cl-link cl-care-team-remove"
                      type="button"
                      onClick={() =>
                        setCareTeamForm((current) => ({
                          ...current,
                          members: current.members.filter(
                            (_, memberIndex) => memberIndex !== index,
                          ),
                        }))
                      }
                    >
                      Remove
                    </button>
                    <div className="cl-care-team-member-grid">
                      <div className="field">
                        <label
                          className="label"
                          htmlFor={`care-team-member-type-${index}`}
                        >
                          Member type
                        </label>
                        <select
                          id={`care-team-member-type-${index}`}
                          className="select"
                          value={member.memberType}
                          onChange={(event) =>
                            updateCareTeamMember(index, {
                              memberType: event.target.value as
                                | "provider"
                                | "contact",
                              userId: null,
                              contactId: null,
                              facilityId: null,
                            })
                          }
                        >
                          <option value="provider">Provider</option>
                          <option value="contact">Patient contact</option>
                        </select>
                      </div>
                      <div className="field">
                        <label
                          className="label"
                          htmlFor={`care-team-member-person-${index}`}
                        >
                          {member.memberType === "provider"
                            ? "Provider"
                            : "Patient contact"}
                        </label>
                        {member.memberType === "provider" ? (
                          <select
                            id={`care-team-member-person-${index}`}
                            className="select"
                            value={member.userId ?? ""}
                            onChange={(event) =>
                              selectCareTeamProvider(index, event.target.value)
                            }
                            required
                          >
                            <option value="">Choose provider</option>
                            {careTeamOptions?.providers.map((provider) => (
                              <option key={provider.id} value={provider.id}>
                                {provider.displayName}
                                {provider.facilityName
                                  ? ` — ${provider.facilityName}`
                                  : ""}
                              </option>
                            ))}
                          </select>
                        ) : (
                          <select
                            id={`care-team-member-person-${index}`}
                            className="select"
                            value={member.contactId ?? ""}
                            onChange={(event) =>
                              selectCareTeamContact(index, event.target.value)
                            }
                            required
                          >
                            <option value="">Choose patient contact</option>
                            {careTeamOptions?.contacts.map((contact) => (
                              <option key={contact.id} value={contact.id}>
                                {contact.displayName}
                                {contact.relationship
                                  ? ` — ${contact.relationship}`
                                  : ""}
                              </option>
                            ))}
                          </select>
                        )}
                      </div>
                      <div className="field">
                        <label
                          className="label"
                          htmlFor={`care-team-member-role-${index}`}
                        >
                          Role
                        </label>
                        <select
                          id={`care-team-member-role-${index}`}
                          className="select"
                          value={member.role}
                          onChange={(event) =>
                            updateCareTeamMember(index, {
                              role: event.target.value,
                            })
                          }
                        >
                          {careTeamRoleOptions.map((option) => (
                            <option key={option.value} value={option.value}>
                              {option.label}
                            </option>
                          ))}
                        </select>
                      </div>
                      <div className="field">
                        <label
                          className="label"
                          htmlFor={`care-team-member-since-${index}`}
                        >
                          Effective date
                        </label>
                        <input
                          id={`care-team-member-since-${index}`}
                          className="input"
                          type="date"
                          value={member.providerSince}
                          onChange={(event) =>
                            updateCareTeamMember(index, {
                              providerSince: event.target.value,
                            })
                          }
                        />
                      </div>
                      <div className="field">
                        <label
                          className="label"
                          htmlFor={`care-team-member-status-${index}`}
                        >
                          Status
                        </label>
                        <select
                          id={`care-team-member-status-${index}`}
                          className="select"
                          value={member.status}
                          onChange={(event) =>
                            updateCareTeamMember(index, {
                              status: event.target.value,
                            })
                          }
                        >
                          {careTeamStatusOptions.map((option) => (
                            <option key={option.value} value={option.value}>
                              {option.label}
                            </option>
                          ))}
                        </select>
                      </div>
                      <div className="field">
                        <label
                          className="label"
                          htmlFor={`care-team-member-note-${index}`}
                        >
                          Note
                        </label>
                        <input
                          id={`care-team-member-note-${index}`}
                          className="input"
                          value={member.note}
                          onChange={(event) =>
                            updateCareTeamMember(index, {
                              note: event.target.value,
                            })
                          }
                        />
                      </div>
                    </div>
                  </fieldset>
                ))}
              </div>
              <button
                className="cl-btn-secondary"
                type="button"
                onClick={() =>
                  setCareTeamForm((current) => ({
                    ...current,
                    members: [...current.members, buildCareTeamMemberDraft()],
                  }))
                }
              >
                <UserPlus size={14} /> Add member
              </button>
              {relationshipError && (
                <p className="cl-form-error" role="alert">
                  {relationshipError}
                </p>
              )}
              <div className="cl-inline-form-actions">
                <button
                  className="cl-btn-primary"
                  type="submit"
                  disabled={
                    relationshipSaving === "care-team" ||
                    relationshipOptionsState !== "ready"
                  }
                >
                  {relationshipSaving === "care-team"
                    ? "Saving…"
                    : "Save care team"}
                </button>
                <button
                  className="cl-btn-secondary"
                  type="button"
                  onClick={closeRelationshipEditor}
                >
                  Cancel
                </button>
              </div>
            </form>
          ) : (
            <>
              <ul className="fact-list">
                {fact("Team", patient.careTeam?.teamName ?? "Care Team")}
                {fact(
                  "Team status",
                  patient.careTeam?.teamStatusDisplay ?? "Active",
                )}
              </ul>
              {patient.careTeam?.members.length ? (
                <div className="cl-care-team-summary">
                  {patient.careTeam.members.map((member, index) => (
                    <article
                      className="cl-care-team-member-summary"
                      key={member.id || `care-team-member-${index}`}
                    >
                      <div>
                        <h3>{member.memberName ?? `Member ${index + 1}`}</h3>
                        <p>
                          {member.roleDisplay} · {member.statusDisplay}
                        </p>
                      </div>
                      <dl>
                        <div>
                          <dt>Type</dt>
                          <dd>
                            {member.memberType === "contact"
                              ? "Patient contact"
                              : "Provider"}
                          </dd>
                        </div>
                        <div>
                          <dt>Facility</dt>
                          <dd>{member.facilityName ?? "—"}</dd>
                        </div>
                        <div>
                          <dt>Effective</dt>
                          <dd>{member.providerSince ?? "—"}</dd>
                        </div>
                        <div>
                          <dt>Note</dt>
                          <dd>{member.note ?? "—"}</dd>
                        </div>
                      </dl>
                    </article>
                  ))}
                </div>
              ) : (
                <p className="cl-empty-text">
                  No care-team members are assigned.
                </p>
              )}
            </>
          )}
        </section>

        {/* Insurance */}
        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title">
              <Shield size={15} /> Insurance
            </h2>
            <button
              className="cl-btn-icon"
              type="button"
              aria-label="Add insurance"
              onClick={openAddInsurance}
            >
              <Plus size={15} />
            </button>
          </div>
          {patient.insurance.length === 0 ? (
            <p className="cl-empty-text">
              No insurance on file.{" "}
              <button
                className="cl-link"
                type="button"
                onClick={openAddInsurance}
              >
                Add insurance
              </button>
            </p>
          ) : (
            <ul className="fact-list">
              {patient.insurance.map((ins) => (
                <li
                  key={ins.id}
                  className="cl-insurance-item cl-insurance-item-actions"
                >
                  <div>
                    <p className="cl-insurance-type">{ins.type ?? "Primary"}</p>
                    <p className="cl-insurance-plan">
                      {ins.provider ?? "—"}
                      {ins.planName ? ` · ${ins.planName}` : ""}
                    </p>
                    {ins.policyNumber && (
                      <p className="cl-insurance-meta">
                        Policy: {ins.policyNumber}
                        {ins.groupNumber ? ` · Group: ${ins.groupNumber}` : ""}
                      </p>
                    )}
                    {ins.relationship && (
                      <p className="cl-insurance-meta">
                        Relationship: {ins.relationship}
                      </p>
                    )}
                  </div>
                  <div className="cl-insurance-btns">
                    <button
                      className="cl-link"
                      type="button"
                      onClick={() => openEditInsurance(ins.id)}
                    >
                      Edit
                    </button>
                    <button
                      className="cl-clinical-action"
                      type="button"
                      aria-label="Remove insurance"
                      disabled={deletingId === ins.id}
                      onClick={() => handleDeleteInsurance(ins.id)}
                    >
                      <Trash2 size={13} />
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="cl-card cl-card-wide">
          <div className="cl-card-header administration-history-header">
            <div>
              <h2 className="cl-card-title">
                <CalendarClock size={15} /> Administrative change history
              </h2>
              <p className="cl-empty-text">
                Recorded contact, demographic, and insurance mutations with
                their authenticated actor and before/after values.
              </p>
            </div>
            {administrationHistoryState.status === "ready" && (
              <span className="cl-badge cl-badge-muted">
                {administrationHistoryState.data.returnedCount <
                administrationHistoryState.data.eventCount
                  ? `${administrationHistoryState.data.returnedCount} newest of ${administrationHistoryState.data.eventCount}`
                  : `${administrationHistoryState.data.eventCount} ${
                      administrationHistoryState.data.eventCount === 1
                        ? "change"
                        : "changes"
                    }`}
              </span>
            )}
          </div>

          {administrationHistoryState.status === "loading" && (
            <p className="cl-empty-text" role="status">
              Loading administrative history…
            </p>
          )}

          {administrationHistoryState.status === "error" && (
            <div className="cl-inline-error" role="alert">
              <span>Administrative history is unavailable.</span>
              <button
                className="cl-link"
                type="button"
                onClick={() =>
                  setAdministrationHistoryRetry((current) => current + 1)
                }
              >
                Retry
              </button>
            </div>
          )}

          {administrationHistoryState.status === "ready" &&
            administrationHistoryState.data.events.length === 0 && (
              <p className="cl-empty-text">
                No administrative changes have been recorded.
              </p>
            )}

          {administrationHistoryState.status === "ready" &&
            administrationHistoryState.data.events.length > 0 && (
              <>
                <div className="administration-history-toolbar">
                  <label htmlFor="administration-history-area">
                    Show
                    <select
                      id="administration-history-area"
                      value={administrationArea}
                      onChange={(event) =>
                        setAdministrationArea(
                          event.target.value as PatientAdministrationArea,
                        )
                      }
                    >
                      <option value="all">All administrative areas</option>
                      <option value="demographics">Demographics</option>
                      <option value="contact">Contact</option>
                      <option value="insurance">Insurance</option>
                    </select>
                  </label>
                  <span>
                    Showing {administrationHistoryEvents.length} of{" "}
                    {administrationHistoryState.data.returnedCount} loaded
                  </span>
                </div>

                {administrationHistoryEvents.length === 0 ? (
                  <p className="cl-empty-text">
                    No changes match this administrative area.
                  </p>
                ) : (
                  <ol className="administration-history-list">
                    {administrationHistoryEvents.map((event) => (
                      <li key={event.eventId}>
                        <div className="administration-history-event-heading">
                          <div>
                            <strong>{administrationEventTitle(event)}</strong>
                            {event.entityId && (
                              <span>
                                Record <code>{event.entityId}</code>
                              </span>
                            )}
                          </div>
                          <time dateTime={event.occurredAt}>
                            {new Date(event.occurredAt).toLocaleString()}
                          </time>
                        </div>
                        <p className="administration-history-actor">
                          By {event.actor}
                        </p>
                        <details>
                          <summary>
                            {event.changedFields.length}{" "}
                            {event.changedFields.length === 1
                              ? "field"
                              : "fields"}{" "}
                            changed
                          </summary>
                          <dl className="administration-history-values">
                            {event.changedFields.map((field) => (
                              <div key={field}>
                                <dt>{administrationFieldLabel(field)}</dt>
                                <dd>
                                  <span>
                                    {administrationValue(
                                      event.beforeValues[field],
                                    )}
                                  </span>
                                  <span aria-hidden="true">→</span>
                                  <span>
                                    {administrationValue(
                                      event.afterValues[field],
                                    )}
                                  </span>
                                </dd>
                              </div>
                            ))}
                          </dl>
                        </details>
                      </li>
                    ))}
                  </ol>
                )}
              </>
            )}
        </section>

        <section className="cl-card">
          <div className="cl-card-header">
            <div>
              <h2 className="cl-card-title">Patient record requests</h2>
              <p className="cl-empty-text">
                Legacy-compatible request tracking: one request remains open
                until staff completes it.
              </p>
            </div>
            <span
              className={`cl-badge ${recordRequests.some((request) => request.status === "Open") ? "cl-badge-muted" : "cl-badge-green"}`}
            >
              {recordRequests.some((request) => request.status === "Open")
                ? "Open request"
                : "No open request"}
            </span>
          </div>
          {recordRequestLoading ? (
            <p className="cl-empty-text">Loading request history…</p>
          ) : recordRequests.length === 0 ? (
            <p className="cl-empty-text">
              No patient record requests have been recorded.
            </p>
          ) : (
            <ul className="cl-clinical-list">
              {recordRequests.map((request) => (
                <li key={request.requestId} className="cl-clinical-row">
                  <div>
                    <p className="cl-clinical-title">
                      {request.status === "Open"
                        ? "Open patient record request"
                        : "Completed patient record request"}
                    </p>
                    <p className="cl-clinical-meta">
                      Requested by {request.requestedBy} on{" "}
                      {new Date(request.requestedAt).toLocaleString()}
                      {request.completedAt
                        ? ` · Completed by ${request.completedBy ?? "staff"} on ${new Date(request.completedAt).toLocaleString()}`
                        : ""}
                    </p>
                  </div>
                  {request.status === "Open" && (
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      onClick={() => void completeRecordRequest(request)}
                      disabled={recordRequestAction === request.requestId}
                    >
                      {recordRequestAction === request.requestId
                        ? "Completing…"
                        : "Complete"}
                    </button>
                  )}
                </li>
              ))}
            </ul>
          )}
          {!recordRequests.some((request) => request.status === "Open") && (
            <div className="cl-inline-form-actions">
              <button
                className="cl-btn-secondary"
                type="button"
                onClick={() => void createRecordRequest()}
                disabled={recordRequestAction === "create"}
              >
                {recordRequestAction === "create"
                  ? "Recording…"
                  : "Record request"}
              </button>
            </div>
          )}
        </section>

        <PatientDisclosurePanel
          sessionId={session.sessionId}
          patientId={patientId}
        />

        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title">Patient portal access</h2>
            <span
              className={`cl-badge ${patient.portalEnabled ? "cl-badge-green" : "cl-badge-muted"}`}
            >
              {patient.portalAccount?.accessStatusLabel ??
                (patient.portalEnabled ? "Enabled" : "Disabled")}
            </span>
          </div>
          <ul className="fact-list">
            {fact(
              "Portal account",
              patient.portalAccount?.hasAccount
                ? (patient.portalAccount.portalUsername ?? "Provisioned")
                : "No account provisioned",
            )}
            {fact(
              "Password status",
              patient.portalAccount?.passwordStatusLabel,
            )}
            {fact("Reset status", patient.portalAccount?.resetStatusLabel)}
          </ul>
          <p className="cl-empty-text">
            Access and reset state are local account controls. No reset message
            is delivered from this workflow.
          </p>
          <div className="cl-inline-form-actions">
            <button
              className="cl-btn-secondary"
              type="button"
              disabled={portalAction !== null}
              onClick={changePortalAccess}
            >
              {portalAction === "access"
                ? "Saving…"
                : patient.portalEnabled
                  ? "Disable portal access"
                  : "Enable portal access"}
            </button>
            {patient.portalAccount?.hasAccount && (
              <button
                className="cl-btn-secondary"
                type="button"
                disabled={portalAction !== null}
                onClick={changePortalReset}
              >
                {portalAction === "reset"
                  ? "Saving…"
                  : patient.portalAccount.oneTimeLinkPending
                    ? "Clear reset link"
                    : "Issue reset link"}
              </button>
            )}
          </div>
        </section>

        <section className="cl-card cl-card-wide">
          <div className="cl-card-header">
            <div>
              <h2 className="cl-card-title">Potential duplicate records</h2>
              <p className="clinician-page-subtitle">
                Review-only match evidence. No patient records or clinical data
                are changed from this screen.
              </p>
            </div>
            <span className="cl-badge cl-badge-muted">
              {patient.duplicateCandidates.length} candidate
              {patient.duplicateCandidates.length === 1 ? "" : "s"}
            </span>
          </div>
          {patient.duplicateCandidates.length === 0 && (
            <p className="cl-empty-text">
              No likely duplicate records were identified from the available
              name, birth-date, phone, and email evidence.
            </p>
          )}
          {patient.duplicateCandidates.length > 0 && (
            <ul className="cl-clinical-list">
              {patient.duplicateCandidates.map((candidate) => (
                <li key={candidate.canonicalId} className="cl-clinical-row">
                  <div>
                    <p className="cl-clinical-title">
                      {candidate.displayName}{" "}
                      <span className="cl-badge cl-badge-muted">
                        {candidate.matchScore}% match
                      </span>
                    </p>
                    <p className="cl-clinical-meta">
                      {candidate.dateOfBirth} · #{candidate.pubpid} ·{" "}
                      {candidate.matchReasons.join(" · ")}
                    </p>
                  </div>
                  <button
                    className="cl-btn-secondary"
                    type="button"
                    onClick={() => previewMerge(candidate.canonicalId)}
                    disabled={mergePreviewState.status === "loading"}
                  >
                    {mergePreviewState.status === "loading" &&
                    mergePreviewState.sourcePatientId === candidate.canonicalId
                      ? "Loading…"
                      : "Preview merge"}
                  </button>
                </li>
              ))}
            </ul>
          )}
          {mergePreviewState.status === "error" && (
            <div className="error-banner" style={{ marginTop: 12 }}>
              {mergePreviewState.message}
            </div>
          )}
          {mergePreviewState.status === "ready" && (
            <div className="cl-soap-section" style={{ marginTop: 12 }}>
              <div className="cl-card-header">
                <p className="cl-soap-label">
                  Merge impact preview — {mergePreviewState.data.matchScore}%
                  match
                </p>
                <span className="cl-badge cl-badge-muted">Preview only</span>
              </div>
              <p className="cl-empty-text">
                Target: {mergePreviewState.data.targetPatient.displayName} (#
                {mergePreviewState.data.targetPatient.pubpid}) · Source:{" "}
                {mergePreviewState.data.sourcePatient.displayName} (#
                {mergePreviewState.data.sourcePatient.pubpid})
              </p>
              <div className="cl-counts-grid" style={{ marginTop: 10 }}>
                {mergeCountLabels.map(({ key, label }) => (
                  <div
                    key={key}
                    className="cl-count-tile"
                    style={{ cursor: "default" }}
                  >
                    <span className="cl-count-value">
                      {mergePreviewState.data.targetCounts[key]} +{" "}
                      {mergePreviewState.data.sourceCounts[key]} ={" "}
                      {mergePreviewState.data.combinedCounts[key]}
                    </span>
                    <span className="cl-count-label">{label}</span>
                  </div>
                ))}
              </div>
              <p className="cl-soap-label" style={{ marginTop: 12 }}>
                Safeguards
              </p>
              <ul className="fact-list">
                {mergePreviewState.data.safeguards.map((safeguard) => (
                  <li key={safeguard} className="fact-row">
                    <span>{safeguard}</span>
                  </li>
                ))}
              </ul>
              <div className="field" style={{ marginTop: 12 }}>
                <label className="label" htmlFor="merge-rationale">
                  Review rationale (optional)
                </label>
                <textarea
                  id="merge-rationale"
                  className="textarea"
                  rows={2}
                  value={mergeAuditRationale}
                  onChange={(event) =>
                    setMergeAuditRationale(event.target.value)
                  }
                  disabled={
                    mergeAuditState.status === "saving" ||
                    mergeAuditState.status === "ready"
                  }
                />
              </div>
              <div className="cl-inline-form-actions">
                <button
                  className="cl-btn-secondary"
                  type="button"
                  onClick={recordMergeReview}
                  disabled={
                    mergeAuditState.status === "saving" ||
                    mergeAuditState.status === "ready"
                  }
                >
                  {mergeAuditState.status === "saving"
                    ? "Recording…"
                    : mergeAuditState.status === "ready"
                      ? "Review recorded"
                      : "Record merge review"}
                </button>
                {mergeAuditState.status === "ready" && (
                  <span className="cl-empty-text">
                    Audit #{mergeAuditState.auditId}
                  </span>
                )}
              </div>
              {mergeAuditState.status === "error" && (
                <div className="error-banner" style={{ marginTop: 10 }}>
                  {mergeAuditState.message}
                </div>
              )}
              {mergeAuditState.status === "ready" &&
                mergeExecutionState.status !== "ready" &&
                mergeExecutionState.status !== "rolling-back" &&
                mergeExecutionState.status !== "rolled-back" && (
                  <div style={{ marginTop: 12 }}>
                    <label
                      className="cl-empty-text"
                      style={{
                        display: "flex",
                        gap: 8,
                        alignItems: "flex-start",
                      }}
                    >
                      <input
                        type="checkbox"
                        checked={mergeExecutionConfirmed}
                        onChange={(event) =>
                          setMergeExecutionConfirmed(event.target.checked)
                        }
                      />
                      <span>
                        I verified the target and source, understand that a
                        constrained merge moves supported records, and will
                        resolve any blocked dependencies before retrying.
                      </span>
                    </label>
                    <div
                      className="cl-inline-form-actions"
                      style={{ marginTop: 10 }}
                    >
                      <button
                        className="cl-btn-primary"
                        type="button"
                        onClick={executeMerge}
                        disabled={
                          !mergeExecutionConfirmed ||
                          mergeExecutionState.status === "saving"
                        }
                      >
                        {mergeExecutionState.status === "saving"
                          ? "Executing…"
                          : "Execute constrained merge"}
                      </button>
                    </div>
                  </div>
                )}
              {mergeExecutionState.status === "ready" && (
                <div className="cl-soap-section" style={{ marginTop: 12 }}>
                  <p className="cl-soap-label">Merge executed</p>
                  <p className="cl-empty-text">
                    Execution #{mergeExecutionState.executionId} recorded{" "}
                    {mergeExecutionState.movedCount} moved records in an
                    immutable manifest.
                  </p>
                  <div className="cl-inline-form-actions">
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      onClick={() =>
                        rollbackMerge(mergeExecutionState.executionId)
                      }
                    >
                      Rollback this merge
                    </button>
                  </div>
                </div>
              )}
              {mergeExecutionState.status === "rolling-back" && (
                <p className="cl-empty-text" style={{ marginTop: 10 }}>
                  Restoring manifest-recorded rows…
                </p>
              )}
              {mergeExecutionState.status === "rolled-back" && (
                <p className="cl-empty-text" style={{ marginTop: 10 }}>
                  Rollback completed for execution #
                  {mergeExecutionState.executionId}.
                </p>
              )}
              {mergeExecutionState.status === "error" && (
                <div className="error-banner" style={{ marginTop: 10 }}>
                  {mergeExecutionState.message}
                </div>
              )}
            </div>
          )}
        </section>

        {/* Timeline */}
        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title">
              <CalendarClock size={15} /> Next appointment
            </h2>
            <button
              className="cl-link"
              type="button"
              onClick={() =>
                navigate(`/clinician/patients/${patientId}/appointments`)
              }
            >
              All appointments
            </button>
          </div>
          {!patient.nextAppointment ? (
            <p className="cl-empty-text">No upcoming appointments.</p>
          ) : (
            <div className="cl-timeline-item">
              <p className="cl-timeline-title">
                {patient.nextAppointment.title}
              </p>
              <p className="cl-timeline-meta">
                {patient.nextAppointment.date}
                {patient.nextAppointment.time
                  ? ` at ${patient.nextAppointment.time.slice(0, 5)}`
                  : ""}
                {patient.nextAppointment.providerName
                  ? ` · ${patient.nextAppointment.providerName}`
                  : ""}
              </p>
            </div>
          )}
        </section>

        <section className="cl-card">
          <div className="cl-card-header">
            <h2 className="cl-card-title">
              <FileText size={15} /> Latest encounter
            </h2>
            <button
              className="cl-link"
              type="button"
              onClick={() =>
                navigate(`/clinician/patients/${patientId}/encounters`)
              }
            >
              All encounters
            </button>
          </div>
          {!patient.latestEncounter ? (
            <p className="cl-empty-text">No encounter history.</p>
          ) : (
            <div className="cl-timeline-item">
              <p className="cl-timeline-title">
                {patient.latestEncounter.title}
              </p>
              <p className="cl-timeline-meta">
                {patient.latestEncounter.date}
                {patient.latestEncounter.providerName
                  ? ` · ${patient.latestEncounter.providerName}`
                  : ""}
              </p>
            </div>
          )}
        </section>

        <section className="cl-card">
          <div className="cl-card-header">
            <div>
              <h2 className="cl-card-title">Billing and account</h2>
              <p className="cl-empty-text">
                Open this patient’s balance, aging, statement readiness, and
                ledger context.
              </p>
            </div>
            <span className="cl-badge cl-badge-muted">
              {patient.counts.billingItems} billing items
            </span>
          </div>
          <Link
            className="cl-btn-secondary"
            to={`/clinician/billing?patientId=${encodeURIComponent(patientId)}`}
          >
            View patient account
          </Link>
        </section>

        {/* Activity counts */}
        <section className="cl-card cl-card-wide">
          <div className="cl-card-header">
            <h2 className="cl-card-title">Activity summary</h2>
          </div>
          <div className="cl-counts-grid">
            {[
              {
                label: "Appointments",
                value: patient.counts.appointments,
                path: "appointments",
              },
              {
                label: "Encounters",
                value: patient.counts.encounters,
                path: "encounters",
              },
              {
                label: "Lab orders",
                value: patient.counts.labOrders,
                path: "labs",
              },
              {
                label: "Messages",
                value: patient.counts.messages,
                path: "messages",
              },
              {
                label: "Problems",
                value: patient.counts.problems,
                path: "chart",
              },
              {
                label: "Medications",
                value: patient.counts.medications,
                path: "chart",
              },
            ].map((c) => (
              <button
                key={c.label}
                className="cl-count-tile"
                type="button"
                onClick={() =>
                  navigate(`/clinician/patients/${patientId}/${c.path}`)
                }
              >
                <span className="cl-count-value">{c.value}</span>
                <span className="cl-count-label">{c.label}</span>
              </button>
            ))}
          </div>
        </section>
      </div>
    </div>
  );
}
