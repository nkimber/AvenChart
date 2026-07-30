import { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate, useOutletContext } from "react-router-dom";
import {
  ArchiveRestore,
  ChevronRight,
  Download,
  ExternalLink,
  Eye,
  FileText,
  FileUp,
  History,
  Link2,
  Pencil,
  Plus,
  RefreshCw,
  TrendingUp,
} from "lucide-react";
import {
  archivePatientDocument,
  createPatientBinaryDocument,
  createPatientDocument,
  createPatientExternalLinkDocument,
  createEncounterVitals,
  downloadPatientDocument,
  downloadPatientDocumentVersion,
  getPatientDocumentArchiveHistory,
  getPatientDocumentCategoryOptions,
  getPatientDocumentMetadataHistory,
  getPatientDocumentReviewHistory,
  getPatientDocuments,
  getPatientDocumentVersionHistory,
  getEncounterSoapNoteTemplates,
  getEncounterAuditHistory,
  getEncounterDetail,
  getEncounterClinicalAlerts,
  getEncounterClinicalAlertHistory,
  acknowledgeEncounterClinicalAlert,
  reopenEncounterClinicalAlert,
  getEncounterLayoutForm,
  getEncounterLayoutForms,
  replacePatientDocumentBinaryContent,
  replacePatientDocumentContent,
  restorePatientDocument,
  reviewPatientDocument,
  searchEncounters,
  saveEncounterLayoutForm,
  updatePatientDocumentMetadata,
  updateEncounter,
  type EncounterDetail,
  type EncounterListItem,
  type EncounterSoapNoteTemplate,
  type EncounterVitals,
  type EncounterAuditHistory,
  type EncounterLayoutForm,
  type EncounterClinicalAlert,
  type EncounterClinicalAlertAcknowledgement,
  type PatientDocumentArchiveHistoryResponse,
  type PatientDocumentCategoryOptionsResponse,
  type PatientDocumentItem,
  type PatientDocumentMetadataHistoryResponse,
  type PatientDocumentReviewHistoryResponse,
  type PatientDocumentVersionHistoryResponse,
} from "../../api.ts";
import {
  archiveEncounterWithReason,
  asEncounterLifecycleDetail,
  EncounterLifecycleConflictError,
  LOCAL_ENCOUNTER_SIGNATURE_POLICY,
  restoreEncounterWithReason,
  signEncounterUnderLocalPolicy,
} from "../../api/encounterLifecycle.ts";
import {
  getEncounterSoapNoteConflict,
  getVersionedEncounterDetail,
  saveEncounterSoapNote,
  type EncounterSoapNoteConflict,
  type VersionedEncounterSoapNote,
} from "../../api/encounterSoapNotes.ts";
import { ClinicalAlertSeverityBadge } from "../../components/ClinicalAlertSeverityBadge.tsx";
import { showToast } from "../../components/Toast.tsx";
import { getClinicalAlertSeverity } from "../../domain/clinicalAlertSeverity.ts";
import EncounterCodingPanel from "./EncounterCodingPanel.tsx";
import type { PatientOutletContext } from "./PatientShell.tsx";

// Simple SVG sparkline for a series of numeric values
function Sparkline({
  values,
  color = "#0f6e56",
}: {
  values: number[];
  color?: string;
}) {
  if (values.length < 2) return null;
  const w = 80,
    h = 28;
  const min = Math.min(...values),
    max = Math.max(...values);
  const range = max - min || 1;
  const pts = values
    .map((v, i) => {
      const x = (i / (values.length - 1)) * (w - 4) + 2;
      const y = h - 2 - ((v - min) / range) * (h - 4);
      return `${x},${y}`;
    })
    .join(" ");
  return (
    <svg
      width={w}
      height={h}
      viewBox={`0 0 ${w} ${h}`}
      aria-hidden="true"
      className="vital-sparkline"
    >
      <polyline
        points={pts}
        fill="none"
        stroke={color}
        strokeWidth="1.5"
        strokeLinejoin="round"
        strokeLinecap="round"
      />
      {values.map((v, i) => {
        const x = (i / (values.length - 1)) * (w - 4) + 2;
        const y = h - 2 - ((v - min) / range) * (h - 4);
        return (
          <circle
            key={i}
            cx={x}
            cy={y}
            r={i === values.length - 1 ? 2.5 : 1.5}
            fill={color}
          />
        );
      })}
    </svg>
  );
}

type ListState =
  | { status: "loading" }
  | { status: "ready"; data: EncounterListItem[] }
  | { status: "error"; message: string };

type DetailState =
  | { status: "idle" }
  | { status: "loading"; id: number }
  | { status: "ready"; data: EncounterDetail }
  | { status: "error"; message: string };

function vitalRow(
  label: string,
  value?: string | number | null,
  unit?: string,
) {
  if (value === null || value === undefined) return null;
  return (
    <div className="cl-vital-item">
      <span className="cl-vital-value">
        {value}
        {unit ? ` ${unit}` : ""}
      </span>
      <span className="cl-vital-label">{label}</span>
    </div>
  );
}

function extractVitalSeries(
  encounters: EncounterListItem[],
  details: Map<number, EncounterDetail>,
) {
  const series: { date: string; vitals: EncounterVitals }[] = [];
  for (const enc of [...encounters].reverse()) {
    const d = details.get(enc.id);
    if (d?.vitals) series.push({ date: enc.date, vitals: d.vitals });
  }
  return series;
}

const BLANK_VITALS = {
  systolic: "",
  diastolic: "",
  pulse: "",
  temperature: "",
  respiration: "",
  oxygenSaturation: "",
  weight: "",
  height: "",
};
const BLANK_SOAP = { subjective: "", objective: "", assessment: "", plan: "" };
const today = () => new Date().toISOString().slice(0, 10);

type SoapConflictState = EncounterSoapNoteConflict & {
  latest?: VersionedEncounterSoapNote | null;
};

type DocumentIntakeMode = "text" | "file" | "link";
type DocumentReplacementMode = "text" | "file";
type DocumentReviewStatus = "pending" | "approved" | "denied";

type DocumentForm = {
  categoryId: string;
  name: string;
  docDate: string;
  notes: string;
  content: string;
  url: string;
  reason: string;
};

type EncounterDocumentHistory = {
  versions: PatientDocumentVersionHistoryResponse | null;
  metadata: PatientDocumentMetadataHistoryResponse | null;
  review: PatientDocumentReviewHistoryResponse | null;
  archive: PatientDocumentArchiveHistoryResponse | null;
};

type EncounterDocumentHistoryState =
  | { documentId: number; status: "loading" }
  | {
      documentId: number;
      status: "ready";
      data: EncounterDocumentHistory;
    }
  | { documentId: number; status: "error"; message: string };

type EncounterDocumentPreviewState =
  | { documentId: number; status: "loading" }
  | {
      documentId: number;
      status: "ready";
      kind: "text" | "image" | "pdf";
      fileName: string;
      contentType: string;
      sizeBytes: number;
      text?: string;
      objectUrl?: string;
      truncated: boolean;
    }
  | { documentId: number; status: "error"; message: string };

const ENCOUNTER_DOCUMENT_TEXT_PREVIEW_LIMIT = 512 * 1024;
const SAFE_ENCOUNTER_IMAGE_TYPES = new Set([
  "image/avif",
  "image/gif",
  "image/jpeg",
  "image/png",
  "image/webp",
]);

function blankDocumentForm(categoryId: string): DocumentForm {
  return {
    categoryId,
    name: "",
    docDate: today(),
    notes: "",
    content: "",
    url: "",
    reason: "",
  };
}

function readDocumentFileAsBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.addEventListener("load", () => {
      const value = typeof reader.result === "string" ? reader.result : "";
      const separator = value.indexOf(",");
      if (separator < 0) {
        reject(new Error("The selected file could not be encoded."));
        return;
      }
      resolve(value.slice(separator + 1));
    });
    reader.addEventListener("error", () =>
      reject(new Error("The selected file could not be read.")),
    );
    reader.readAsDataURL(file);
  });
}

function formatDocumentBytes(value?: number | null) {
  if (value === null || value === undefined) return "Size unavailable";
  if (value < 1024) return `${value} B`;
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`;
  return `${(value / (1024 * 1024)).toFixed(1)} MB`;
}

function normalizeDocumentReviewStatus(value: string): DocumentReviewStatus {
  const normalized = value.trim().toLowerCase();
  if (["approved", "signed", "reviewed"].includes(normalized))
    return "approved";
  if (["denied", "rejected"].includes(normalized)) return "denied";
  return "pending";
}

function documentTypeLabel(document: PatientDocumentItem) {
  if (document.storageMethod === "web_url") return "External link";
  if (document.mimetype?.startsWith("text/")) return "Text";
  if (document.mimetype === "application/pdf") return "PDF";
  if (document.mimetype?.startsWith("image/")) return "Image";
  return document.mimetype || "Stored file";
}

function EncounterDocuments({
  sessionId,
  detail,
  targetEncounters,
  onDetailChange,
}: {
  sessionId: string;
  detail: EncounterDetail;
  targetEncounters: EncounterListItem[];
  onDetailChange: (detail: EncounterDetail) => void;
}) {
  const initialCategoryId = String(
    detail.documents.find((document) => document.deleted === 0)?.categoryId ??
      1,
  );
  const [documents, setDocuments] = useState<PatientDocumentItem[]>(
    detail.documents as unknown as PatientDocumentItem[],
  );
  const [options, setOptions] =
    useState<PatientDocumentCategoryOptionsResponse | null>(null);
  const [workspaceLoading, setWorkspaceLoading] = useState(true);
  const [workspaceError, setWorkspaceError] = useState<string | null>(null);
  const [addOpen, setAddOpen] = useState(false);
  const [intakeMode, setIntakeMode] = useState<DocumentIntakeMode>("text");
  const [editingId, setEditingId] = useState<number | null>(null);
  const [reviewingId, setReviewingId] = useState<number | null>(null);
  const [replacingId, setReplacingId] = useState<number | null>(null);
  const [movingId, setMovingId] = useState<number | null>(null);
  const [archivingId, setArchivingId] = useState<number | null>(null);
  const [historyState, setHistoryState] =
    useState<EncounterDocumentHistoryState | null>(null);
  const [previewState, setPreviewState] =
    useState<EncounterDocumentPreviewState | null>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [fileInputKey, setFileInputKey] = useState(0);
  const [replacementMode, setReplacementMode] =
    useState<DocumentReplacementMode>("text");
  const [replacementContent, setReplacementContent] = useState("");
  const [replacementReason, setReplacementReason] = useState("");
  const [replacementFile, setReplacementFile] = useState<File | null>(null);
  const [replacementFileKey, setReplacementFileKey] = useState(0);
  const [targetEncounter, setTargetEncounter] = useState("");
  const [moveReason, setMoveReason] = useState("");
  const [archiveReason, setArchiveReason] = useState("");
  const [reviewForm, setReviewForm] = useState<{
    reviewStatus: DocumentReviewStatus;
    reason: string;
  }>({ reviewStatus: "approved", reason: "" });
  const [saving, setSaving] = useState(false);
  const [downloadingKey, setDownloadingKey] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [form, setForm] = useState<DocumentForm>(
    blankDocumentForm(initialCategoryId),
  );
  const previewObjectUrl = useRef<string | null>(null);
  const previewRequest = useRef(0);

  useEffect(() => {
    let active = true;
    setWorkspaceLoading(true);
    setWorkspaceError(null);
    setDocuments([]);
    Promise.all([
      getPatientDocuments(sessionId, detail.patientId, undefined, true),
      getPatientDocumentCategoryOptions(sessionId),
    ])
      .then(([register, categoryOptions]) => {
        if (!active) return;
        setDocuments(
          register.documents.filter(
            (document) => document.encounter === detail.encounter,
          ),
        );
        setOptions(categoryOptions);
        setForm((current) => ({
          ...current,
          categoryId: categoryOptions.categories.some(
            (category) => String(category.id) === current.categoryId,
          )
            ? current.categoryId
            : String(categoryOptions.categories[0]?.id ?? initialCategoryId),
        }));
      })
      .catch((error) => {
        if (!active) return;
        setWorkspaceError(
          error instanceof Error
            ? error.message
            : "Encounter attachments could not be loaded.",
        );
      })
      .finally(() => {
        if (active) setWorkspaceLoading(false);
      });
    return () => {
      active = false;
    };
  }, [detail.encounter, detail.patientId, initialCategoryId, sessionId]);

  useEffect(
    () => () => {
      previewRequest.current += 1;
      if (previewObjectUrl.current) {
        URL.revokeObjectURL(previewObjectUrl.current);
        previewObjectUrl.current = null;
      }
    },
    [],
  );

  function closePreview() {
    previewRequest.current += 1;
    if (previewObjectUrl.current) {
      URL.revokeObjectURL(previewObjectUrl.current);
      previewObjectUrl.current = null;
    }
    setPreviewState(null);
  }

  function closeDocumentPanels() {
    setEditingId(null);
    setReviewingId(null);
    setReplacingId(null);
    setMovingId(null);
    setArchivingId(null);
    setHistoryState(null);
    closePreview();
    setActionError(null);
  }

  async function refreshWorkspace() {
    const [register, refreshed] = await Promise.all([
      getPatientDocuments(sessionId, detail.patientId, undefined, true),
      getEncounterDetail(sessionId, detail.encounter, undefined, true),
    ]);
    setDocuments(
      register.documents.filter(
        (document) => document.encounter === detail.encounter,
      ),
    );
    onDetailChange(refreshed);
  }

  function openAdd() {
    closeDocumentPanels();
    setForm(
      blankDocumentForm(
        String(
          options?.categories[0]?.id ??
            documents.find((document) => document.deleted === 0)?.categoryId ??
            1,
        ),
      ),
    );
    setIntakeMode("text");
    setSelectedFile(null);
    setFileInputKey((current) => current + 1);
    setAddOpen(true);
  }

  function openEdit(document: PatientDocumentItem) {
    closeDocumentPanels();
    setAddOpen(false);
    setEditingId(document.id);
    setForm({
      categoryId: String(document.categoryId),
      name: document.name,
      docDate: document.docDate,
      notes: document.notes ?? "",
      content: "",
      url: "",
      reason: "",
    });
  }

  function handleFileSelection(file: File | null) {
    if (!file) {
      setSelectedFile(null);
      return;
    }
    if (options && file.size > options.maxFileSizeBytes) {
      setSelectedFile(null);
      setFileInputKey((current) => current + 1);
      setActionError(
        `${file.name} is ${formatDocumentBytes(file.size)}. The protected service accepts up to ${formatDocumentBytes(options.maxFileSizeBytes)}.`,
      );
      return;
    }
    setSelectedFile(file);
    setForm((current) => ({
      ...current,
      name: current.name.trim() ? current.name : file.name,
    }));
    setActionError(null);
  }

  async function saveDocument(event: React.FormEvent) {
    event.preventDefault();
    const categoryId = Number(form.categoryId);
    if (!Number.isInteger(categoryId) || categoryId <= 0) {
      setActionError("Choose a filing category.");
      return;
    }
    setSaving(true);
    setActionError(null);
    try {
      if (editingId !== null) {
        if (!form.reason.trim()) {
          throw new Error("Explain why the filing metadata is changing.");
        }
        await updatePatientDocumentMetadata(sessionId, editingId, {
          categoryId,
          name: form.name.trim(),
          docDate: form.docDate,
          encounter: detail.encounter,
          notes: form.notes.trim() || null,
          reason: form.reason.trim(),
        });
        showToast("Document filing updated with change evidence.", "success");
      } else {
        const shared = {
          patientId: detail.patientId,
          categoryId,
          name: form.name.trim(),
          docDate: form.docDate,
          encounter: detail.encounter,
          notes: form.notes.trim() || null,
        };
        if (intakeMode === "text") {
          if (!form.content.trim()) {
            throw new Error("Enter the attachment text.");
          }
          await createPatientDocument(sessionId, {
            ...shared,
            content: form.content.trim(),
          });
        } else if (intakeMode === "file") {
          if (!selectedFile) throw new Error("Choose a file to attach.");
          if (options && selectedFile.size > options.maxFileSizeBytes) {
            throw new Error(
              `Choose a file no larger than ${formatDocumentBytes(options.maxFileSizeBytes)}.`,
            );
          }
          await createPatientBinaryDocument(sessionId, {
            ...shared,
            fileName: selectedFile.name,
            mimetype: selectedFile.type.trim() || "application/octet-stream",
            contentBase64: await readDocumentFileAsBase64(selectedFile),
          });
        } else {
          let link: URL;
          try {
            link = new URL(form.url.trim());
          } catch {
            throw new Error("Enter a complete http or https URL.");
          }
          if (!["http:", "https:"].includes(link.protocol)) {
            throw new Error("External links must use http or https.");
          }
          await createPatientExternalLinkDocument(sessionId, {
            ...shared,
            url: link.toString(),
          });
        }
        showToast(
          intakeMode === "text"
            ? "Text attachment filed."
            : intakeMode === "file"
              ? "File attachment uploaded."
              : "External link attached.",
          "success",
        );
      }
      await refreshWorkspace();
      setAddOpen(false);
      setEditingId(null);
      setSelectedFile(null);
    } catch (error) {
      setActionError(
        error instanceof Error
          ? error.message
          : editingId === null
            ? "The attachment could not be filed."
            : "The filing metadata could not be updated.",
      );
    } finally {
      setSaving(false);
    }
  }

  function beginReplacement(document: PatientDocumentItem) {
    closeDocumentPanels();
    setReplacingId(document.id);
    setReplacementMode(
      document.mimetype?.startsWith("text/") ? "text" : "file",
    );
    setReplacementContent("");
    setReplacementReason("");
    setReplacementFile(null);
    setReplacementFileKey((current) => current + 1);
  }

  async function replaceContent(
    event: React.FormEvent,
    document: PatientDocumentItem,
  ) {
    event.preventDefault();
    if (!replacementReason.trim()) {
      setActionError("Explain why the protected content is changing.");
      return;
    }
    setSaving(true);
    setActionError(null);
    try {
      if (replacementMode === "text") {
        if (!replacementContent.trim()) {
          throw new Error("Enter replacement text.");
        }
        await replacePatientDocumentContent(sessionId, document.id, {
          fileName: document.fileName || `${document.name}.txt`,
          content: replacementContent.trim(),
          reason: replacementReason.trim(),
          expectedVersion: document.currentVersion,
        });
      } else {
        if (!replacementFile) throw new Error("Choose a replacement file.");
        if (options && replacementFile.size > options.maxFileSizeBytes) {
          throw new Error(
            `Choose a file no larger than ${formatDocumentBytes(options.maxFileSizeBytes)}.`,
          );
        }
        await replacePatientDocumentBinaryContent(sessionId, document.id, {
          fileName: replacementFile.name,
          mimetype: replacementFile.type.trim() || "application/octet-stream",
          contentBase64: await readDocumentFileAsBase64(replacementFile),
          reason: replacementReason.trim(),
          expectedVersion: document.currentVersion,
        });
      }
      await refreshWorkspace();
      setReplacingId(null);
      showToast("A new protected document version was filed.", "success");
    } catch (error) {
      setActionError(
        error instanceof Error
          ? error.message
          : "Document content could not be replaced.",
      );
      await refreshWorkspace().catch(() => undefined);
    } finally {
      setSaving(false);
    }
  }

  async function moveDocument(
    event: React.FormEvent,
    document: PatientDocumentItem,
  ) {
    event.preventDefault();
    const target = Number(targetEncounter);
    if (!Number.isInteger(target) || target === detail.encounter) {
      setActionError("Choose another encounter for this patient.");
      return;
    }
    if (!moveReason.trim()) {
      setActionError("Explain why the attachment is moving.");
      return;
    }
    setSaving(true);
    setActionError(null);
    try {
      await updatePatientDocumentMetadata(sessionId, document.id, {
        categoryId: document.categoryId,
        name: document.name,
        docDate: document.docDate,
        encounter: target,
        notes: document.notes ?? null,
        reason: moveReason.trim(),
      });
      await refreshWorkspace();
      setMovingId(null);
      showToast("Document moved with filing-history evidence.", "success");
    } catch (error) {
      setActionError(
        error instanceof Error
          ? error.message
          : "The document could not be moved.",
      );
    } finally {
      setSaving(false);
    }
  }

  async function saveReview(
    event: React.FormEvent,
    document: PatientDocumentItem,
  ) {
    event.preventDefault();
    if (!reviewForm.reason.trim()) {
      setActionError("Enter the reason for this review decision.");
      return;
    }
    const expectedStatus = normalizeDocumentReviewStatus(document.reviewStatus);
    setSaving(true);
    setActionError(null);
    try {
      await reviewPatientDocument(sessionId, document.id, {
        reviewStatus: reviewForm.reviewStatus,
        reason: reviewForm.reason.trim(),
        expectedReviewStatus: expectedStatus,
      });
      await refreshWorkspace();
      setReviewingId(null);
      showToast(
        reviewForm.reviewStatus === "pending"
          ? "Document review reopened."
          : `Document ${reviewForm.reviewStatus}.`,
        "success",
      );
    } catch (error) {
      setActionError(
        error instanceof Error
          ? error.message
          : "The review decision could not be recorded.",
      );
      await refreshWorkspace().catch(() => undefined);
    } finally {
      setSaving(false);
    }
  }

  async function changeArchive(
    event: React.FormEvent,
    document: PatientDocumentItem,
  ) {
    event.preventDefault();
    if (!archiveReason.trim()) {
      setActionError(
        `Enter a reason to ${document.deleted ? "restore" : "archive"} this document.`,
      );
      return;
    }
    const expectedArchived = document.deleted !== 0;
    setSaving(true);
    setActionError(null);
    try {
      const input = {
        reason: archiveReason.trim(),
        expectedArchived,
      };
      if (expectedArchived) {
        await restorePatientDocument(sessionId, document.id, input);
      } else {
        await archivePatientDocument(sessionId, document.id, input);
      }
      await refreshWorkspace();
      setArchivingId(null);
      showToast(
        expectedArchived ? "Document restored." : "Document archived.",
        "success",
      );
    } catch (error) {
      setActionError(
        error instanceof Error
          ? error.message
          : "The archive state could not be changed.",
      );
      await refreshWorkspace().catch(() => undefined);
    } finally {
      setSaving(false);
    }
  }

  async function loadHistory(document: PatientDocumentItem) {
    if (historyState?.documentId === document.id) {
      setHistoryState(null);
      return;
    }
    closeDocumentPanels();
    setHistoryState({ documentId: document.id, status: "loading" });
    const read = async <T,>(promise: Promise<T>): Promise<T | null> => {
      try {
        return await promise;
      } catch {
        return null;
      }
    };
    const [versions, metadata, review, archive] = await Promise.all([
      read(getPatientDocumentVersionHistory(sessionId, document.id)),
      read(getPatientDocumentMetadataHistory(sessionId, document.id)),
      read(getPatientDocumentReviewHistory(sessionId, document.id)),
      read(getPatientDocumentArchiveHistory(sessionId, document.id)),
    ]);
    if (!versions && !metadata && !review && !archive) {
      setHistoryState({
        documentId: document.id,
        status: "error",
        message: "Document lifecycle history could not be loaded.",
      });
      return;
    }
    setHistoryState({
      documentId: document.id,
      status: "ready",
      data: { versions, metadata, review, archive },
    });
  }

  function saveBrowserDownload(
    file: { blob: Blob; fileName: string },
    objectUrl?: string,
  ) {
    const url = objectUrl ?? URL.createObjectURL(file.blob);
    const anchor = window.document.createElement("a");
    anchor.href = url;
    anchor.download = file.fileName;
    anchor.style.display = "none";
    window.document.body.append(anchor);
    anchor.click();
    anchor.remove();
    if (!objectUrl) {
      window.setTimeout(() => URL.revokeObjectURL(url), 0);
    }
  }

  async function downloadDocument(document: PatientDocumentItem) {
    const key = `${document.id}-current`;
    setDownloadingKey(key);
    setActionError(null);
    try {
      const file = await downloadPatientDocument(
        sessionId,
        document.id,
        document.fileName || document.name,
      );
      saveBrowserDownload(file);
    } catch (error) {
      setActionError(
        error instanceof Error ? error.message : "Download failed.",
      );
    } finally {
      setDownloadingKey(null);
    }
  }

  async function downloadVersion(
    document: PatientDocumentItem,
    version: number,
    fileName?: string | null,
  ) {
    const key = `${document.id}-${version}`;
    setDownloadingKey(key);
    setActionError(null);
    try {
      const file = await downloadPatientDocumentVersion(
        sessionId,
        document.id,
        version,
        fileName || document.fileName || document.name,
      );
      saveBrowserDownload(file);
    } catch (error) {
      setActionError(
        error instanceof Error ? error.message : "Version download failed.",
      );
    } finally {
      setDownloadingKey(null);
    }
  }

  async function openPreview(document: PatientDocumentItem) {
    if (previewState?.documentId === document.id) {
      closePreview();
      return;
    }
    closeDocumentPanels();
    const requestId = previewRequest.current + 1;
    previewRequest.current = requestId;
    setPreviewState({ documentId: document.id, status: "loading" });
    try {
      const file = await downloadPatientDocument(
        sessionId,
        document.id,
        document.fileName || document.name,
      );
      if (requestId !== previewRequest.current) return;
      const contentType = file.contentType
        .split(";", 1)[0]
        .trim()
        .toLowerCase();
      if (contentType.startsWith("text/")) {
        const truncated =
          file.blob.size > ENCOUNTER_DOCUMENT_TEXT_PREVIEW_LIMIT;
        const text = await file.blob
          .slice(0, ENCOUNTER_DOCUMENT_TEXT_PREVIEW_LIMIT)
          .text();
        if (requestId !== previewRequest.current) return;
        setPreviewState({
          documentId: document.id,
          status: "ready",
          kind: "text",
          fileName: file.fileName,
          contentType,
          sizeBytes: file.blob.size,
          text,
          truncated,
        });
        return;
      }
      const kind =
        contentType === "application/pdf"
          ? "pdf"
          : SAFE_ENCOUNTER_IMAGE_TYPES.has(contentType)
            ? "image"
            : null;
      if (!kind) {
        throw new Error(
          `${contentType || "This file type"} is not safe for inline preview. Use protected download.`,
        );
      }
      const objectUrl = URL.createObjectURL(file.blob);
      previewObjectUrl.current = objectUrl;
      setPreviewState({
        documentId: document.id,
        status: "ready",
        kind,
        fileName: file.fileName,
        contentType,
        sizeBytes: file.blob.size,
        objectUrl,
        truncated: false,
      });
    } catch (error) {
      if (requestId !== previewRequest.current) return;
      setPreviewState({
        documentId: document.id,
        status: "error",
        message:
          error instanceof Error
            ? error.message
            : "The protected preview could not be loaded.",
      });
    }
  }

  return (
    <section
      className="cl-card encounter-document-workspace"
      aria-labelledby="encounter-attachments-title"
    >
      <div className="cl-card-header">
        <div>
          <h2 className="cl-card-title" id="encounter-attachments-title">
            Attachments
          </h2>
          <p className="cl-empty-text">
            Protected text, file, and external-link records for encounter #
            {detail.encounter}
          </p>
        </div>
        <div className="cl-inline-form-actions">
          <button
            className="cl-btn-secondary"
            type="button"
            onClick={() => void refreshWorkspace()}
            disabled={workspaceLoading || saving}
          >
            <RefreshCw size={14} /> Refresh
          </button>
          <button
            className="cl-btn-primary"
            type="button"
            aria-label="Add encounter attachment"
            onClick={openAdd}
            disabled={saving}
          >
            <Plus size={14} /> Add attachment
          </button>
        </div>
      </div>

      {workspaceError && (
        <div className="cl-inline-error" role="alert">
          {workspaceError}
        </div>
      )}
      {actionError && (
        <div className="cl-inline-error" role="alert">
          {actionError}
        </div>
      )}

      {(addOpen || editingId !== null) && (
        <form
          className="encounter-document-form"
          onSubmit={saveDocument}
          aria-label={
            editingId === null
              ? "Add encounter attachment"
              : "Edit attachment filing"
          }
        >
          {editingId === null && (
            <div
              className="encounter-document-mode-picker"
              aria-label="Attachment type"
            >
              {(
                [
                  ["text", "Text note", FileText],
                  ["file", "Upload file", FileUp],
                  ["link", "External link", Link2],
                ] as const
              ).map(([mode, label, Icon]) => (
                <button
                  key={mode}
                  className={
                    intakeMode === mode ? "cl-btn-primary" : "cl-btn-secondary"
                  }
                  type="button"
                  aria-pressed={intakeMode === mode}
                  onClick={() => {
                    setIntakeMode(mode);
                    setActionError(null);
                  }}
                >
                  <Icon size={14} /> {label}
                </button>
              ))}
            </div>
          )}
          <div className="form-row">
            <div className="field">
              <label className="label" htmlFor="attachment-name">
                Name
              </label>
              <input
                id="attachment-name"
                className="input"
                required
                value={form.name}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    name: event.target.value,
                  }))
                }
              />
            </div>
            <div className="field">
              <label className="label" htmlFor="attachment-date">
                Document date
              </label>
              <input
                id="attachment-date"
                className="input"
                type="date"
                required
                value={form.docDate}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    docDate: event.target.value,
                  }))
                }
              />
            </div>
            <div className="field">
              <label className="label" htmlFor="attachment-category">
                Filing category
              </label>
              {options ? (
                <select
                  id="attachment-category"
                  className="input"
                  required
                  value={form.categoryId}
                  onChange={(event) =>
                    setForm((current) => ({
                      ...current,
                      categoryId: event.target.value,
                    }))
                  }
                >
                  {options.categories.map((category) => (
                    <option key={category.id} value={category.id}>
                      {category.name}
                    </option>
                  ))}
                </select>
              ) : (
                <input
                  id="attachment-category"
                  className="input"
                  type="number"
                  min="1"
                  required
                  value={form.categoryId}
                  onChange={(event) =>
                    setForm((current) => ({
                      ...current,
                      categoryId: event.target.value,
                    }))
                  }
                />
              )}
            </div>
          </div>
          {editingId === null && intakeMode === "text" && (
            <div className="field">
              <label className="label" htmlFor="attachment-content">
                Attachment text
              </label>
              <textarea
                id="attachment-content"
                className="textarea"
                rows={4}
                required
                value={form.content}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    content: event.target.value,
                  }))
                }
              />
            </div>
          )}
          {editingId === null && intakeMode === "file" && (
            <div className="field">
              <label className="label" htmlFor="attachment-file">
                File
              </label>
              <input
                key={fileInputKey}
                id="attachment-file"
                className="input"
                type="file"
                required
                onChange={(event) =>
                  handleFileSelection(event.target.files?.[0] ?? null)
                }
              />
              {options && (
                <small>
                  Maximum protected upload:{" "}
                  {formatDocumentBytes(options.maxFileSizeBytes)}
                </small>
              )}
            </div>
          )}
          {editingId === null && intakeMode === "link" && (
            <div className="field">
              <label className="label" htmlFor="attachment-url">
                External http or https URL
              </label>
              <input
                id="attachment-url"
                className="input"
                type="url"
                required
                value={form.url}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    url: event.target.value,
                  }))
                }
              />
            </div>
          )}
          <div className="field">
            <label className="label" htmlFor="attachment-notes">
              Filing note
            </label>
            <input
              id="attachment-notes"
              className="input"
              value={form.notes}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  notes: event.target.value,
                }))
              }
            />
          </div>
          {editingId !== null && (
            <div className="field">
              <label className="label" htmlFor="attachment-change-reason">
                Change reason
              </label>
              <input
                id="attachment-change-reason"
                className="input"
                required
                maxLength={250}
                value={form.reason}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    reason: event.target.value,
                  }))
                }
              />
            </div>
          )}
          <p className="cl-empty-text">
            The patient, encounter, filing facts, protected content, and
            lifecycle evidence remain in the shared document register.
          </p>
          <div className="cl-inline-form-actions">
            <button className="cl-btn-primary" type="submit" disabled={saving}>
              {saving
                ? "Saving…"
                : editingId === null
                  ? "File attachment"
                  : "Save filing"}
            </button>
            <button
              className="cl-btn-secondary"
              type="button"
              disabled={saving}
              onClick={() => {
                setAddOpen(false);
                setEditingId(null);
                setActionError(null);
              }}
            >
              Cancel
            </button>
          </div>
        </form>
      )}

      {workspaceLoading && (
        <p className="cl-empty-text" role="status">
          Loading protected encounter attachments…
        </p>
      )}
      {!workspaceLoading && documents.length === 0 && !addOpen && (
        <p className="cl-empty-text">
          No active or archived attachments are filed to this encounter.
        </p>
      )}

      <div className="encounter-document-list">
        {documents.map((document) => {
          const reviewStatus = normalizeDocumentReviewStatus(
            document.reviewStatus,
          );
          const isExternal = document.storageMethod === "web_url";
          return (
            <article
              key={document.id}
              className={`encounter-document-card${
                document.deleted ? " is-archived" : ""
              }`}
              data-document-name={document.name}
            >
              <div className="cl-card-header">
                <div>
                  <p className="cl-soap-label">{document.name}</p>
                  <p className="cl-empty-text">
                    {document.categoryName} · {document.docDate} ·{" "}
                    {documentTypeLabel(document)}
                  </p>
                </div>
                <div className="encounter-document-badges">
                  <span className="cl-badge cl-badge-muted">
                    {document.versionLabel}
                  </span>
                  <span className="cl-badge cl-badge-muted">
                    {reviewStatus}
                  </span>
                  {document.deleted !== 0 && (
                    <span className="cl-badge">Archived</span>
                  )}
                </div>
              </div>
              <dl className="encounter-document-facts">
                <div>
                  <dt>File</dt>
                  <dd>{document.fileName || document.name}</dd>
                </div>
                <div>
                  <dt>Type and size</dt>
                  <dd>
                    {document.mimetype || documentTypeLabel(document)} ·{" "}
                    {formatDocumentBytes(document.sizeBytes)}
                  </dd>
                </div>
                <div>
                  <dt>Filed</dt>
                  <dd>{document.uploadedAt}</dd>
                </div>
                <div>
                  <dt>Latest revision</dt>
                  <dd>{document.revisionAt}</dd>
                </div>
              </dl>
              {document.notes && (
                <p className="cl-soap-text">{document.notes}</p>
              )}
              {document.contentPreview && !isExternal && (
                <p className="encounter-document-preview-copy">
                  {document.contentPreview}
                </p>
              )}
              {isExternal && document.url && (
                <p className="encounter-document-preview-copy">
                  {document.url}
                </p>
              )}

              <div className="cl-inline-form-actions">
                {!document.deleted && document.canPreviewInline && (
                  <button
                    className="cl-btn-secondary"
                    type="button"
                    onClick={() => void openPreview(document)}
                    disabled={saving}
                  >
                    <Eye size={14} /> Preview
                  </button>
                )}
                {!document.deleted && !isExternal && document.canDownload && (
                  <button
                    className="cl-btn-secondary"
                    type="button"
                    onClick={() => void downloadDocument(document)}
                    disabled={
                      saving || downloadingKey === `${document.id}-current`
                    }
                  >
                    <Download size={14} />
                    {downloadingKey === `${document.id}-current`
                      ? "Downloading…"
                      : "Download"}
                  </button>
                )}
                {!document.deleted && isExternal && document.url && (
                  <a
                    className="cl-btn-secondary"
                    href={document.url}
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    <ExternalLink size={14} /> Open external link
                  </a>
                )}
                <button
                  className="cl-btn-secondary"
                  type="button"
                  onClick={() => void loadHistory(document)}
                  disabled={saving}
                >
                  <History size={14} /> History
                </button>
                {!document.deleted && (
                  <>
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      onClick={() => openEdit(document)}
                      disabled={saving}
                    >
                      Edit filing
                    </button>
                    {!isExternal && (
                      <button
                        className="cl-btn-secondary"
                        type="button"
                        onClick={() => beginReplacement(document)}
                        disabled={saving}
                      >
                        Replace content
                      </button>
                    )}
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      onClick={() => {
                        closeDocumentPanels();
                        setMovingId(document.id);
                        setTargetEncounter("");
                        setMoveReason("");
                      }}
                      disabled={saving}
                    >
                      Move
                    </button>
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      onClick={() => {
                        closeDocumentPanels();
                        setReviewingId(document.id);
                        setReviewForm({
                          reviewStatus:
                            reviewStatus === "pending" ? "approved" : "pending",
                          reason: "",
                        });
                      }}
                      disabled={saving}
                    >
                      Review
                    </button>
                  </>
                )}
                <button
                  className="cl-btn-secondary"
                  type="button"
                  onClick={() => {
                    closeDocumentPanels();
                    setArchivingId(document.id);
                    setArchiveReason("");
                  }}
                  disabled={saving}
                >
                  <ArchiveRestore size={14} />{" "}
                  {document.deleted ? "Restore" : "Archive"}
                </button>
              </div>

              {previewState?.documentId === document.id && (
                <section
                  className="encounter-document-preview"
                  aria-label={`Preview of ${document.name}`}
                >
                  <div className="cl-card-header">
                    <p className="cl-soap-label">Protected preview</p>
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      onClick={closePreview}
                    >
                      Close
                    </button>
                  </div>
                  {previewState.status === "loading" && (
                    <p role="status">Loading protected bytes…</p>
                  )}
                  {previewState.status === "error" && (
                    <div className="cl-inline-error" role="alert">
                      {previewState.message}
                    </div>
                  )}
                  {previewState.status === "ready" && (
                    <>
                      <p className="cl-empty-text">
                        {previewState.fileName} · {previewState.contentType} ·{" "}
                        {formatDocumentBytes(previewState.sizeBytes)}
                      </p>
                      {previewState.kind === "text" && (
                        <pre
                          className="encounter-document-text-preview"
                          tabIndex={0}
                        >
                          {previewState.text}
                        </pre>
                      )}
                      {previewState.kind === "image" &&
                        previewState.objectUrl && (
                          <img
                            className="encounter-document-image-preview"
                            src={previewState.objectUrl}
                            alt={`Preview of ${document.name}`}
                          />
                        )}
                      {previewState.kind === "pdf" &&
                        previewState.objectUrl && (
                          <iframe
                            className="encounter-document-pdf-preview"
                            src={previewState.objectUrl}
                            title={`${document.name} PDF preview`}
                          />
                        )}
                      {previewState.truncated && (
                        <p className="cl-empty-text">
                          Preview is limited to the first{" "}
                          {formatDocumentBytes(
                            ENCOUNTER_DOCUMENT_TEXT_PREVIEW_LIMIT,
                          )}
                          . Download to read the complete file.
                        </p>
                      )}
                    </>
                  )}
                </section>
              )}

              {editingId === document.id && (
                <p className="cl-empty-text">
                  Editing this filing in the form above.
                </p>
              )}

              {replacingId === document.id && (
                <form
                  className="encounter-document-inline-form"
                  onSubmit={(event) => replaceContent(event, document)}
                >
                  <div className="encounter-document-mode-picker">
                    <button
                      className={
                        replacementMode === "text"
                          ? "cl-btn-primary"
                          : "cl-btn-secondary"
                      }
                      type="button"
                      aria-pressed={replacementMode === "text"}
                      onClick={() => setReplacementMode("text")}
                    >
                      Replacement text
                    </button>
                    <button
                      className={
                        replacementMode === "file"
                          ? "cl-btn-primary"
                          : "cl-btn-secondary"
                      }
                      type="button"
                      aria-pressed={replacementMode === "file"}
                      onClick={() => setReplacementMode("file")}
                    >
                      Replacement file
                    </button>
                  </div>
                  {replacementMode === "text" ? (
                    <div className="field">
                      <label
                        className="label"
                        htmlFor={`replacement-${document.id}`}
                      >
                        Replacement text
                      </label>
                      <textarea
                        id={`replacement-${document.id}`}
                        className="textarea"
                        rows={4}
                        required
                        value={replacementContent}
                        onChange={(event) =>
                          setReplacementContent(event.target.value)
                        }
                      />
                    </div>
                  ) : (
                    <div className="field">
                      <label
                        className="label"
                        htmlFor={`replacement-file-${document.id}`}
                      >
                        Replacement file
                      </label>
                      <input
                        key={replacementFileKey}
                        id={`replacement-file-${document.id}`}
                        className="input"
                        type="file"
                        required
                        onChange={(event) =>
                          setReplacementFile(event.target.files?.[0] ?? null)
                        }
                      />
                    </div>
                  )}
                  <div className="field">
                    <label
                      className="label"
                      htmlFor={`replacement-reason-${document.id}`}
                    >
                      Replacement reason
                    </label>
                    <input
                      id={`replacement-reason-${document.id}`}
                      className="input"
                      required
                      maxLength={250}
                      value={replacementReason}
                      onChange={(event) =>
                        setReplacementReason(event.target.value)
                      }
                    />
                  </div>
                  <p className="cl-empty-text">
                    Saving appends version {document.currentVersion + 1}; it
                    never overwrites version {document.currentVersion}.
                  </p>
                  <div className="cl-inline-form-actions">
                    <button
                      className="cl-btn-primary"
                      type="submit"
                      disabled={saving}
                    >
                      Save new version
                    </button>
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      disabled={saving}
                      onClick={() => setReplacingId(null)}
                    >
                      Cancel
                    </button>
                  </div>
                </form>
              )}

              {movingId === document.id && (
                <form
                  className="encounter-document-inline-form"
                  onSubmit={(event) => moveDocument(event, document)}
                >
                  <div className="field">
                    <label className="label" htmlFor={`move-${document.id}`}>
                      Target encounter
                    </label>
                    <select
                      id={`move-${document.id}`}
                      className="input"
                      required
                      value={targetEncounter}
                      onChange={(event) =>
                        setTargetEncounter(event.target.value)
                      }
                    >
                      <option value="">Choose encounter</option>
                      {targetEncounters
                        .filter(
                          (encounter) =>
                            encounter.encounter !== detail.encounter,
                        )
                        .map((encounter) => (
                          <option
                            key={encounter.encounter}
                            value={encounter.encounter}
                          >
                            #{encounter.encounter} · {encounter.date} ·{" "}
                            {encounter.reason ?? "Visit"}
                          </option>
                        ))}
                    </select>
                  </div>
                  <div className="field">
                    <label
                      className="label"
                      htmlFor={`move-reason-${document.id}`}
                    >
                      Move reason
                    </label>
                    <input
                      id={`move-reason-${document.id}`}
                      className="input"
                      required
                      maxLength={250}
                      value={moveReason}
                      onChange={(event) => setMoveReason(event.target.value)}
                    />
                  </div>
                  <div className="cl-inline-form-actions">
                    <button
                      className="cl-btn-primary"
                      type="submit"
                      disabled={saving}
                    >
                      Move attachment
                    </button>
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      disabled={saving}
                      onClick={() => setMovingId(null)}
                    >
                      Cancel
                    </button>
                  </div>
                </form>
              )}

              {reviewingId === document.id && (
                <form
                  className="encounter-document-inline-form"
                  onSubmit={(event) => saveReview(event, document)}
                >
                  <div className="form-row">
                    <div className="field">
                      <label
                        className="label"
                        htmlFor={`review-status-${document.id}`}
                      >
                        Review decision
                      </label>
                      <select
                        id={`review-status-${document.id}`}
                        className="input"
                        value={reviewForm.reviewStatus}
                        onChange={(event) =>
                          setReviewForm((current) => ({
                            ...current,
                            reviewStatus: event.target
                              .value as DocumentReviewStatus,
                          }))
                        }
                      >
                        {reviewStatus === "pending" ? (
                          <>
                            <option value="approved">Approve</option>
                            <option value="denied">Deny</option>
                          </>
                        ) : (
                          <option value="pending">Reopen review</option>
                        )}
                      </select>
                    </div>
                    <div className="field">
                      <label
                        className="label"
                        htmlFor={`review-reason-${document.id}`}
                      >
                        Decision reason
                      </label>
                      <input
                        id={`review-reason-${document.id}`}
                        className="input"
                        required
                        maxLength={250}
                        value={reviewForm.reason}
                        onChange={(event) =>
                          setReviewForm((current) => ({
                            ...current,
                            reason: event.target.value,
                          }))
                        }
                      />
                    </div>
                  </div>
                  <p className="cl-empty-text">
                    The reviewer identity comes from the authenticated session.
                  </p>
                  <div className="cl-inline-form-actions">
                    <button
                      className="cl-btn-primary"
                      type="submit"
                      disabled={saving}
                    >
                      Record decision
                    </button>
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      onClick={() => setReviewingId(null)}
                      disabled={saving}
                    >
                      Cancel
                    </button>
                  </div>
                </form>
              )}

              {archivingId === document.id && (
                <form
                  className="encounter-document-inline-form"
                  onSubmit={(event) => changeArchive(event, document)}
                >
                  <div className="field">
                    <label
                      className="label"
                      htmlFor={`archive-reason-${document.id}`}
                    >
                      {document.deleted ? "Restore reason" : "Archive reason"}
                    </label>
                    <input
                      id={`archive-reason-${document.id}`}
                      className="input"
                      required
                      maxLength={250}
                      value={archiveReason}
                      onChange={(event) => setArchiveReason(event.target.value)}
                    />
                  </div>
                  <div className="cl-inline-form-actions">
                    <button
                      className="cl-btn-primary"
                      type="submit"
                      disabled={saving}
                    >
                      {document.deleted
                        ? "Restore document"
                        : "Archive document"}
                    </button>
                    <button
                      className="cl-btn-secondary"
                      type="button"
                      onClick={() => setArchivingId(null)}
                      disabled={saving}
                    >
                      Cancel
                    </button>
                  </div>
                </form>
              )}

              {historyState?.documentId === document.id && (
                <section
                  className="encounter-document-history"
                  aria-label={`Lifecycle history for ${document.name}`}
                >
                  {historyState.status === "loading" && (
                    <p role="status">Loading immutable lifecycle evidence…</p>
                  )}
                  {historyState.status === "error" && (
                    <div className="cl-inline-error" role="alert">
                      {historyState.message}
                    </div>
                  )}
                  {historyState.status === "ready" && (
                    <>
                      <div className="cl-card-header">
                        <div>
                          <p className="cl-soap-label">Lifecycle evidence</p>
                          <p className="cl-empty-text">
                            Content, filing, review, and archive histories use
                            the shared protected document record.
                          </p>
                        </div>
                        <button
                          className="cl-btn-secondary"
                          type="button"
                          onClick={() => setHistoryState(null)}
                        >
                          Close history
                        </button>
                      </div>
                      <div className="encounter-document-history-grid">
                        <div>
                          <h4>Content versions</h4>
                          {historyState.data.versions ? (
                            <ol>
                              {historyState.data.versions.versions.map(
                                (version) => (
                                  <li key={version.version}>
                                    <div>
                                      <strong>{version.versionLabel}</strong> ·{" "}
                                      {version.versionStatus}
                                    </div>
                                    <p>
                                      {version.revisionReason ||
                                        "Original filing"}{" "}
                                      ·{" "}
                                      {version.revisionActor ||
                                        "Original actor unavailable"}{" "}
                                      · {version.revisionAt}
                                    </p>
                                    <button
                                      className="cl-link"
                                      type="button"
                                      disabled={
                                        !version.canDownload ||
                                        downloadingKey ===
                                          `${document.id}-${version.version}`
                                      }
                                      onClick={() =>
                                        void downloadVersion(
                                          document,
                                          version.version,
                                          version.fileName,
                                        )
                                      }
                                    >
                                      Download version {version.version}
                                    </button>
                                  </li>
                                ),
                              )}
                            </ol>
                          ) : (
                            <p>Version history unavailable.</p>
                          )}
                        </div>
                        <div>
                          <h4>Filing changes</h4>
                          {historyState.data.metadata?.events.length ? (
                            <ol>
                              {historyState.data.metadata.events.map(
                                (event) => (
                                  <li key={event.eventId}>
                                    <strong>{event.reason}</strong>
                                    <p>
                                      {event.changedFields.join(", ")} ·{" "}
                                      {event.actor} · {event.occurredAt}
                                    </p>
                                  </li>
                                ),
                              )}
                            </ol>
                          ) : (
                            <p>No filing changes retained.</p>
                          )}
                        </div>
                        <div>
                          <h4>Review decisions</h4>
                          {historyState.data.review?.events.length ? (
                            <ol>
                              {historyState.data.review.events.map((event) => (
                                <li key={event.eventId}>
                                  <strong>{event.action}</strong>
                                  <p>
                                    {event.reason} · {event.actor} ·{" "}
                                    {event.occurredAt}
                                  </p>
                                </li>
                              ))}
                            </ol>
                          ) : (
                            <p>No review decisions retained.</p>
                          )}
                        </div>
                        <div>
                          <h4>Archive events</h4>
                          {historyState.data.archive?.events.length ? (
                            <ol>
                              {historyState.data.archive.events.map((event) => (
                                <li key={event.eventId}>
                                  <strong>{event.action}</strong>
                                  <p>
                                    {event.reason} · {event.actor} ·{" "}
                                    {event.occurredAt}
                                  </p>
                                </li>
                              ))}
                            </ol>
                          ) : (
                            <p>No archive events retained.</p>
                          )}
                        </div>
                      </div>
                    </>
                  )}
                </section>
              )}
            </article>
          );
        })}
      </div>
    </section>
  );
}

function EncounterSignatures({
  sessionId,
  username,
  detail,
  onDetailChange,
}: {
  sessionId: string;
  username: string;
  detail: EncounterDetail;
  onDetailChange: (detail: EncounterDetail) => void;
}) {
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [mode, setMode] = useState<"signature" | "amendment">("signature");
  const [error, setError] = useState<string | null>(null);
  const [form, setForm] = useState({
    isLock: false,
    amendment: "",
  });

  function openForm(nextMode: "signature" | "amendment") {
    setMode(nextMode);
    setForm({
      isLock: nextMode === "amendment",
      amendment: "",
    });
    setError(null);
    setOpen(true);
  }

  async function saveSignature(event: React.FormEvent) {
    event.preventDefault();
    const amendment = form.amendment.trim();
    if (mode === "amendment" && !amendment) {
      setError("Enter the correction or amendment that must be preserved.");
      return;
    }
    if (
      !window.confirm(
        mode === "amendment"
          ? "Append and sign this amendment? Existing signed evidence will not change."
          : form.isLock
            ? "Record this signature and lock direct SOAP changes?"
            : "Record this encounter signature?",
      )
    )
      return;
    setSaving(true);
    setError(null);
    try {
      const result = await signEncounterUnderLocalPolicy(
        sessionId,
        detail.encounter,
        {
          isLock: mode === "amendment" ? true : form.isLock,
          amendment: mode === "amendment" ? amendment : null,
        },
      );
      onDetailChange(result.detail);
      setOpen(false);
      showToast(
        mode === "amendment"
          ? "Signed amendment appended."
          : "Encounter signature recorded.",
        "success",
      );
    } catch (caught) {
      const message =
        caught instanceof Error
          ? caught.message
          : "Could not record encounter signature.";
      setError(message);
      showToast("Could not record encounter signature.", "error");
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="cl-card" aria-labelledby="encounter-signatures-title">
      <div className="cl-card-header">
        <h2 className="cl-card-title" id="encounter-signatures-title">
          Signatures and amendments
        </h2>
        <div className="cl-inline-form-actions">
          <button
            className="cl-btn-secondary"
            type="button"
            onClick={() => openForm("signature")}
            disabled={saving}
          >
            Record signature
          </button>
          {detail.signatures.length > 0 && (
            <button
              className="cl-btn-secondary"
              type="button"
              onClick={() => openForm("amendment")}
              disabled={saving}
            >
              Add signed amendment
            </button>
          )}
        </div>
      </div>
      <p className="cl-empty-text">
        Local append-only policy {LOCAL_ENCOUNTER_SIGNATURE_POLICY}. The
        authenticated session ({username}) supplies signer identity, and the API
        supplies signed time. Existing signatures are immutable; corrections are
        appended as signed amendments.
      </p>
      {open && (
        <form onSubmit={saveSignature}>
          <h3 className="cl-soap-label">
            {mode === "amendment"
              ? "Append a signed amendment"
              : "Record encounter signature"}
          </h3>
          <div className="form-row">
            <div className="field">
              <span className="label">Signer</span>
              <p className="cl-empty-text">
                {username} (authenticated session)
              </p>
            </div>
            {mode === "signature" && (
              <div className="field">
                <label className="label" htmlFor="encounter-lock">
                  Direct SOAP changes
                </label>
                <select
                  id="encounter-lock"
                  className="input"
                  value={form.isLock ? "locked" : "open"}
                  onChange={(event) =>
                    setForm((current) => ({
                      ...current,
                      isLock: event.target.value === "locked",
                    }))
                  }
                >
                  <option value="open">Remain open</option>
                  <option value="locked">Lock after signature</option>
                </select>
              </div>
            )}
          </div>
          {mode === "amendment" && (
            <div className="field" style={{ marginBottom: 10 }}>
              <label className="label" htmlFor="encounter-amendment">
                Correction or amendment
              </label>
              <textarea
                id="encounter-amendment"
                className="textarea"
                rows={3}
                required
                value={form.amendment}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    amendment: event.target.value,
                  }))
                }
              />
            </div>
          )}
          {error && (
            <p className="cl-soap-save-error" role="alert">
              {error}
            </p>
          )}
          <div className="cl-inline-form-actions">
            <button className="cl-btn-primary" type="submit" disabled={saving}>
              {saving
                ? "Saving…"
                : mode === "amendment"
                  ? "Append signed amendment"
                  : "Record signature"}
            </button>
            <button
              className="cl-btn-secondary"
              type="button"
              disabled={saving}
              onClick={() => {
                setOpen(false);
                setError(null);
              }}
            >
              Cancel
            </button>
          </div>
        </form>
      )}
      {detail.signatures.length === 0 && !open && (
        <p className="cl-empty-text">No encounter signatures recorded.</p>
      )}
      {detail.signatures.map((signature) => (
        <div key={signature.id} className="cl-soap-section">
          <div className="cl-card-header">
            <p className="cl-soap-label">{signature.signerUsername}</p>
            <span className="cl-badge cl-badge-muted">
              {signature.isLock ? "Locked" : "Signed"}
            </span>
          </div>
          <p className="cl-empty-text">{signature.signedAt}</p>
          {signature.amendment && (
            <p className="cl-soap-text">Amendment: {signature.amendment}</p>
          )}
        </div>
      ))}
      {detail.amendmentHistory.length > 0 && (
        <div style={{ marginTop: 12 }}>
          <p className="cl-soap-label">Amendment history</p>
          {detail.amendmentHistory.map((amendment) => (
            <p key={amendment.signatureId} className="cl-empty-text">
              {amendment.signedAt} · {amendment.signerUsername}:{" "}
              {amendment.amendment}
            </p>
          ))}
        </div>
      )}
    </section>
  );
}

function EncounterAudit({
  sessionId,
  detail,
}: {
  sessionId: string;
  detail: EncounterDetail;
}) {
  const [history, setHistory] = useState<EncounterAuditHistory | null>(null);
  const [expanded, setExpanded] = useState(false);
  const [failed, setFailed] = useState(false);

  async function load() {
    try {
      setHistory(await getEncounterAuditHistory(sessionId, detail.encounter));
      setFailed(false);
    } catch {
      setFailed(true);
    }
  }

  return (
    <div className="cl-card">
      <div className="cl-card-header">
        <h2 className="cl-card-title">Encounter audit</h2>
        <button
          className="cl-btn-secondary"
          type="button"
          onClick={() => {
            setExpanded((current) => !current);
            if (!history) void load();
          }}
        >
          {expanded ? "Hide history" : "View history"}
        </button>
      </div>
      {!expanded && (
        <p className="cl-empty-text">
          Local summary changes retain actor, time, action, and changed-field
          evidence without duplicating clinical values.
        </p>
      )}
      {expanded && !history && !failed && (
        <p className="cl-empty-text">Loading audit historyâ€¦</p>
      )}
      {expanded && failed && (
        <p className="cl-empty-text">Audit history could not be loaded.</p>
      )}
      {expanded && history?.events.length === 0 && (
        <p className="cl-empty-text">
          No audited summary changes for this encounter.
        </p>
      )}
      {expanded &&
        history?.events.map((event) => (
          <div key={event.eventId} className="cl-soap-section">
            <div className="cl-card-header">
              <p className="cl-soap-label">{event.action}</p>
              <span className="cl-badge cl-badge-muted">{event.username}</span>
            </div>
            <p className="cl-empty-text">
              {new Date(event.occurredAt).toLocaleString()} Â·{" "}
              {event.changedFields.join(", ")}
            </p>
          </div>
        ))}
    </div>
  );
}

function EncounterLayoutFormPanel({
  sessionId,
  encounter,
}: {
  sessionId: string;
  encounter: number;
}) {
  const [forms, setForms] = useState<{ key: string; title: string }[]>([]);
  const [selectedKey, setSelectedKey] = useState("");
  const [form, setForm] = useState<EncounterLayoutForm | null>(null);
  const [values, setValues] = useState<Record<string, string>>({});
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [savingForm, setSavingForm] = useState(false);

  function initialValues(detail: EncounterLayoutForm) {
    const saved = detail.latestRecord?.values ?? {};
    return Object.fromEntries(
      detail.groups.flatMap((group) =>
        group.fields.map((field) => [
          field.key,
          saved[field.key] ??
            field.defaultValue ??
            field.options.find((option) => option.isDefault)?.key ??
            "",
        ]),
      ),
    );
  }

  useEffect(() => {
    let cancelled = false;
    getEncounterLayoutForms(sessionId, encounter)
      .then((catalog) => {
        if (cancelled) return;
        setForms(catalog.forms);
        setSelectedKey(catalog.forms[0]?.key ?? "");
      })
      .catch(() => {
        if (!cancelled) setForms([]);
      });
    return () => {
      cancelled = true;
    };
  }, [encounter, sessionId]);

  async function load() {
    if (!selectedKey) return;
    setLoading(true);
    try {
      const detail = await getEncounterLayoutForm(
        sessionId,
        encounter,
        selectedKey,
      );
      setForm(detail);
      setValues(initialValues(detail));
      setOpen(true);
    } catch {
      showToast("Could not load the configured form.", "error");
    } finally {
      setLoading(false);
    }
  }

  async function save(event: React.FormEvent) {
    event.preventDefault();
    if (!form) return;
    setSavingForm(true);
    try {
      const saved = await saveEncounterLayoutForm(
        sessionId,
        encounter,
        form.layoutKey,
        values,
      );
      setForm(saved);
      setValues(initialValues(saved));
      showToast(
        `${saved.title} saved as revision ${saved.latestRecord?.revision}.`,
        "success",
      );
    } catch {
      showToast(
        "Could not save the configured form. Complete required fields and use valid list values.",
        "error",
      );
    } finally {
      setSavingForm(false);
    }
  }

  if (forms.length === 0) return null;
  return (
    <section className="cl-card">
      <div className="cl-card-header">
        <div>
          <h2 className="cl-card-title">Configured encounter form</h2>
          <p className="cl-empty-text">
            Layout-backed values are saved as immutable revisions and do not
            modify core demographics.
          </p>
        </div>
        <div className="cl-inline-form-actions">
          <select
            className="input"
            value={selectedKey}
            onChange={(event) => {
              setSelectedKey(event.target.value);
              setOpen(false);
              setForm(null);
            }}
          >
            {forms.map((item) => (
              <option key={item.key} value={item.key}>
                {item.title}
              </option>
            ))}
          </select>
          <button
            className="cl-btn-secondary"
            type="button"
            onClick={() => void load()}
            disabled={loading}
          >
            {loading ? "Loading…" : open ? "Reload" : "Open form"}
          </button>
        </div>
      </div>
      {open && form && (
        <form onSubmit={save}>
          {form.groups.map((group) => (
            <fieldset key={group.key} className="cl-soap-section">
              <legend className="cl-soap-label">{group.title}</legend>
              {group.fields.map((field) => (
                <div
                  className="field"
                  key={field.key}
                  style={{ marginBottom: 10 }}
                >
                  <label
                    className="label"
                    htmlFor={`layout-${form.layoutKey}-${field.key}`}
                  >
                    {field.label}
                    {field.required ? " *" : ""}
                  </label>
                  {field.fieldType === "textarea" ? (
                    <textarea
                      id={`layout-${form.layoutKey}-${field.key}`}
                      className="textarea"
                      rows={3}
                      maxLength={field.maxLength || undefined}
                      value={values[field.key] ?? ""}
                      onChange={(event) =>
                        setValues((current) => ({
                          ...current,
                          [field.key]: event.target.value,
                        }))
                      }
                      required={field.required}
                    />
                  ) : field.fieldType === "select" ? (
                    <select
                      id={`layout-${form.layoutKey}-${field.key}`}
                      className="input"
                      value={values[field.key] ?? ""}
                      onChange={(event) =>
                        setValues((current) => ({
                          ...current,
                          [field.key]: event.target.value,
                        }))
                      }
                      required={field.required}
                    >
                      <option value="">Select…</option>
                      {field.options.map((option) => (
                        <option key={option.key} value={option.key}>
                          {option.title}
                        </option>
                      ))}
                    </select>
                  ) : field.fieldType === "checkbox" ? (
                    <label>
                      <input
                        id={`layout-${form.layoutKey}-${field.key}`}
                        type="checkbox"
                        checked={values[field.key] === "true"}
                        onChange={(event) =>
                          setValues((current) => ({
                            ...current,
                            [field.key]: event.target.checked
                              ? "true"
                              : "false",
                          }))
                        }
                      />{" "}
                      Yes
                    </label>
                  ) : (
                    <input
                      id={`layout-${form.layoutKey}-${field.key}`}
                      className="input"
                      type={
                        field.fieldType === "number"
                          ? "number"
                          : field.fieldType === "date"
                            ? "date"
                            : "text"
                      }
                      maxLength={field.maxLength || undefined}
                      value={values[field.key] ?? ""}
                      onChange={(event) =>
                        setValues((current) => ({
                          ...current,
                          [field.key]: event.target.value,
                        }))
                      }
                      required={field.required}
                    />
                  )}
                </div>
              ))}
            </fieldset>
          ))}
          <div className="cl-inline-form-actions">
            <button
              className="cl-btn-primary"
              type="submit"
              disabled={savingForm}
            >
              {savingForm
                ? "Saving…"
                : form.latestRecord
                  ? "Save new revision"
                  : "Save form"}
            </button>
            <button
              className="cl-btn-secondary"
              type="button"
              onClick={() => setOpen(false)}
              disabled={savingForm}
            >
              Close
            </button>
          </div>
          {form.latestRecord && (
            <p className="cl-empty-text">
              Latest revision {form.latestRecord.revision} saved by{" "}
              {form.latestRecord.savedBy} at{" "}
              {new Date(form.latestRecord.savedAt).toLocaleString()}.
            </p>
          )}
        </form>
      )}
    </section>
  );
}

function EncounterClinicalAlerts({
  sessionId,
  encounter,
}: {
  sessionId: string;
  encounter: number;
}) {
  const [alerts, setAlerts] = useState<EncounterClinicalAlert[]>([]);
  const [history, setHistory] = useState<
    EncounterClinicalAlertAcknowledgement[]
  >([]);
  const [state, setState] = useState<"loading" | "ready" | "error">("loading");
  const [loadAttempt, setLoadAttempt] = useState(0);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [historyRefreshError, setHistoryRefreshError] = useState<string | null>(
    null,
  );
  const [acknowledging, setAcknowledging] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setState("loading");
    setLoadError(null);
    setActionError(null);
    setHistoryRefreshError(null);
    Promise.all([
      getEncounterClinicalAlerts(sessionId, encounter),
      getEncounterClinicalAlertHistory(sessionId, encounter),
    ])
      .then(([activeResponse, historyResponse]) => {
        if (cancelled) return;
        setAlerts(activeResponse.alerts);
        setHistory(historyResponse.acknowledgements);
        setState("ready");
      })
      .catch((error: unknown) => {
        if (cancelled) return;
        setLoadError(
          error instanceof Error
            ? error.message
            : "Clinical alerts could not be loaded.",
        );
        setState("error");
      });
    return () => {
      cancelled = true;
    };
  }, [sessionId, encounter, loadAttempt]);

  async function acknowledge(key: string) {
    setAcknowledging(key);
    setActionError(null);
    setHistoryRefreshError(null);
    try {
      const response = await acknowledgeEncounterClinicalAlert(
        sessionId,
        encounter,
        key,
      );
      setAlerts(response.alerts);
      showToast(
        "Clinical alert acknowledgement recorded for this encounter.",
        "success",
      );
      try {
        setHistory(
          (await getEncounterClinicalAlertHistory(sessionId, encounter))
            .acknowledgements,
        );
      } catch {
        setHistoryRefreshError(
          "The acknowledgement was saved, but its updated history could not be loaded.",
        );
      }
    } catch (error: unknown) {
      setActionError(
        error instanceof Error
          ? error.message
          : "The acknowledgement could not be recorded.",
      );
      showToast("Could not acknowledge this clinical alert.", "error");
    } finally {
      setAcknowledging(null);
    }
  }

  async function reopen(key: string) {
    setAcknowledging(key);
    setActionError(null);
    setHistoryRefreshError(null);
    try {
      const response = await reopenEncounterClinicalAlert(
        sessionId,
        encounter,
        key,
      );
      setAlerts(response.alerts);
      showToast("Clinical alert reopened for this encounter.", "success");
      try {
        setHistory(
          (await getEncounterClinicalAlertHistory(sessionId, encounter))
            .acknowledgements,
        );
      } catch {
        setHistoryRefreshError(
          "The alert was reopened, but its updated history could not be loaded.",
        );
      }
    } catch (error: unknown) {
      setActionError(
        error instanceof Error
          ? error.message
          : "The alert could not be reopened.",
      );
      showToast("Could not reopen this clinical alert.", "error");
    } finally {
      setAcknowledging(null);
    }
  }

  if (state === "loading") {
    return (
      <section className="cl-card" aria-label="Clinical alerts">
        <h2 className="cl-card-title">Clinical alerts</h2>
        <p className="cl-empty-text" role="status">
          Loading clinical alerts and acknowledgement history...
        </p>
      </section>
    );
  }

  if (state === "error") {
    return (
      <section className="cl-card" aria-label="Clinical alerts">
        <h2 className="cl-card-title">Clinical alerts</h2>
        <div className="error-banner" role="alert">
          <strong>Clinical alerts are unavailable.</strong>
          <span>{loadError}</span>
          <button
            className="cl-btn-secondary"
            type="button"
            onClick={() => setLoadAttempt((attempt) => attempt + 1)}
          >
            Retry alerts
          </button>
        </div>
      </section>
    );
  }

  if (alerts.length === 0 && history.length === 0) return null;
  return (
    <section className="cl-card" aria-label="Clinical alerts">
      <div className="cl-card-header">
        <div>
          <h2 className="cl-card-title">Clinical alerts</h2>
          <p className="cl-empty-text">
            Active rule definitions evaluated for this encounter.
          </p>
        </div>
      </div>
      {actionError ? (
        <div className="error-banner" role="alert">
          <strong>Alert action was not saved.</strong>
          <span>{actionError}</span>
        </div>
      ) : null}
      {historyRefreshError ? (
        <div className="error-banner" role="status">
          <strong>Alert saved; history is stale.</strong>
          <span>{historyRefreshError}</span>
        </div>
      ) : null}
      {alerts.map((alert) => (
        <div
          key={alert.key}
          className="cl-soap-section clinical-alert-card"
          data-alert-severity={
            getClinicalAlertSeverity(alert.severity).severity
          }
        >
          <div className="clinical-alert-heading">
            <p className="cl-soap-label">{alert.title}</p>
            <ClinicalAlertSeverityBadge value={alert.severity} />
          </div>
          <p className="cl-soap-text">{alert.message}</p>
          <p className="cl-empty-text">{alert.reason}</p>
          <button
            className="cl-btn-secondary"
            type="button"
            onClick={() => void acknowledge(alert.key)}
            disabled={acknowledging === alert.key}
          >
            {acknowledging === alert.key
              ? "Recording..."
              : "Acknowledge review"}
          </button>
        </div>
      ))}
      {history.length > 0 && (
        <div className="cl-soap-section">
          <p className="cl-soap-label">
            Alert acknowledgement history ({history.length})
          </p>
          {history.map((entry) => (
            <div
              key={entry.ruleKey}
              className="cl-empty-text clinical-alert-history-entry"
            >
              <div className="clinical-alert-heading">
                <strong>{entry.title}</strong>
                <span
                  className={`cl-badge ${
                    entry.reopenedAt ? "cl-badge-amber" : "cl-badge-green"
                  }`}
                >
                  {entry.reopenedAt ? "Reopened" : "Acknowledged"}
                </span>
              </div>
              <br />
              Acknowledged by {entry.acknowledgedBy} at{" "}
              {new Date(entry.acknowledgedAt).toLocaleString()}.
              {entry.reopenedAt ? (
                <>
                  <br />
                  Reopened by {entry.reopenedBy} at{" "}
                  {new Date(entry.reopenedAt).toLocaleString()}.
                </>
              ) : (
                <>
                  <br />
                  <button
                    className="cl-btn-secondary"
                    type="button"
                    onClick={() => void reopen(entry.ruleKey)}
                    disabled={acknowledging === entry.ruleKey}
                  >
                    {acknowledging === entry.ruleKey
                      ? "Reopening..."
                      : "Reopen alert"}
                  </button>
                </>
              )}
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

export default function PatientEncounters() {
  const { session, patientId } = useOutletContext<PatientOutletContext>();
  const navigate = useNavigate();
  const [listState, setListState] = useState<ListState>({ status: "loading" });
  const [detailState, setDetailState] = useState<DetailState>({
    status: "idle",
  });
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [detailCache, setDetailCache] = useState<Map<number, EncounterDetail>>(
    new Map(),
  );
  const [showTrends, setShowTrends] = useState(false);
  const [addVitalsOpen, setAddVitalsOpen] = useState(false);
  const [addSoapOpen, setAddSoapOpen] = useState(false);
  const [vitalsForm, setVitalsForm] = useState(BLANK_VITALS);
  const [soapForm, setSoapForm] = useState(BLANK_SOAP);
  const [soapDraftVersion, setSoapDraftVersion] = useState(0);
  const [soapDraftDirty, setSoapDraftDirty] = useState(false);
  const [soapConflict, setSoapConflict] = useState<SoapConflictState | null>(
    null,
  );
  const [soapSaveError, setSoapSaveError] = useState<string | null>(null);
  const [soapTemplates, setSoapTemplates] = useState<
    EncounterSoapNoteTemplate[]
  >([]);
  const [soapTemplateError, setSoapTemplateError] = useState<string | null>(
    null,
  );
  const [selectedSoapTemplateId, setSelectedSoapTemplateId] = useState("");
  const [saving, setSaving] = useState(false);
  const [showArchived, setShowArchived] = useState(false);
  const [archiving, setArchiving] = useState(false);
  const [encounterArchiveAction, setEncounterArchiveAction] = useState<{
    restore: boolean;
    reason: string;
  } | null>(null);
  const [encounterArchiveError, setEncounterArchiveError] = useState<
    string | null
  >(null);
  const [editSummaryOpen, setEditSummaryOpen] = useState(false);
  const [summaryForm, setSummaryForm] = useState({
    reason: "",
    sensitivity: "",
    referralSource: "",
    externalId: "",
    posCode: "",
    billingNote: "",
  });

  useEffect(() => {
    setDetailCache(new Map());
    searchEncounters(session.sessionId, {
      patientId,
      fromDate: "1900-01-01",
      limit: 50,
      archived: showArchived,
    })
      .then((data) => setListState({ status: "ready", data: data.encounters }))
      .catch((err) =>
        setListState({
          status: "error",
          message: err instanceof Error ? err.message : "Failed to load.",
        }),
      );
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [patientId, showArchived]);

  useEffect(() => {
    let cancelled = false;
    getEncounterSoapNoteTemplates(session.sessionId)
      .then((catalog) => {
        if (cancelled) return;
        setSoapTemplates(catalog.templates);
        setSelectedSoapTemplateId(
          (current) =>
            current ||
            catalog.templates.find((template) => template.isDefault)
              ?.templateId ||
            "",
        );
      })
      .catch(() => {
        if (!cancelled)
          setSoapTemplateError(
            "SOAP templates are unavailable. You can still write a note manually.",
          );
      });
    return () => {
      cancelled = true;
    };
  }, [session.sessionId]);

  useEffect(() => {
    if (!soapDraftDirty) return;
    const warnBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
    };
    window.addEventListener("beforeunload", warnBeforeUnload);
    return () => window.removeEventListener("beforeunload", warnBeforeUnload);
  }, [soapDraftDirty]);

  async function changeArchiveState(
    detail: EncounterDetail,
    restore: boolean,
    reason: string,
  ) {
    const normalizedReason = reason.trim();
    if (!normalizedReason) {
      setEncounterArchiveError(
        `Enter the reason this encounter is being ${restore ? "restored" : "archived"}.`,
      );
      return;
    }
    const lifecycle = asEncounterLifecycleDetail(detail);
    setArchiving(true);
    setEncounterArchiveError(null);
    try {
      if (restore) {
        await restoreEncounterWithReason(
          session.sessionId,
          lifecycle.encounter,
          lifecycle.archiveVersion,
          normalizedReason,
        );
      } else {
        await archiveEncounterWithReason(
          session.sessionId,
          lifecycle.encounter,
          lifecycle.archiveVersion,
          normalizedReason,
        );
      }
      showToast(
        restore ? "Encounter restored." : "Encounter archived.",
        "success",
      );
      setEncounterArchiveAction(null);
      setSelectedId(null);
      setDetailState({ status: "idle" });
      if (restore) setShowArchived(false);
      const response = await searchEncounters(session.sessionId, {
        patientId,
        fromDate: "1900-01-01",
        limit: 50,
        archived: restore ? false : showArchived,
      });
      setListState({ status: "ready", data: response.encounters });
    } catch (caught) {
      const fallback = restore
        ? "Could not restore encounter."
        : "Could not archive encounter.";
      const message = caught instanceof Error ? caught.message : fallback;
      setEncounterArchiveError(message);
      showToast(fallback, "error");
      if (caught instanceof EncounterLifecycleConflictError) {
        try {
          const refreshed = await getEncounterDetail(
            session.sessionId,
            lifecycle.encounter,
            undefined,
            true,
          );
          setDetailState({ status: "ready", data: refreshed });
          setDetailCache((current) =>
            new Map(current).set(refreshed.id, refreshed),
          );
        } catch {
          // Keep the conflict visible if authoritative refresh also fails.
        }
      }
    } finally {
      setArchiving(false);
    }
  }

  function openSummaryEditor(enc: EncounterDetail) {
    setSummaryForm({
      reason: enc.reason ?? "",
      sensitivity: enc.sensitivity ?? "",
      referralSource: enc.referralSource ?? "",
      externalId: enc.externalId ?? "",
      posCode: enc.posCode?.toString() ?? "",
      billingNote: enc.billingNote ?? "",
    });
    setEditSummaryOpen(true);
  }

  async function saveSummary(event: React.FormEvent, encounter: number) {
    event.preventDefault();
    setSaving(true);
    try {
      const updated = await updateEncounter(session.sessionId, encounter, {
        reason: summaryForm.reason,
        sensitivity: summaryForm.sensitivity || null,
        referralSource: summaryForm.referralSource || null,
        externalId: summaryForm.externalId || null,
        posCode: summaryForm.posCode ? Number(summaryForm.posCode) : null,
        billingNote: summaryForm.billingNote || null,
      });
      setDetailState({ status: "ready", data: updated });
      setDetailCache((current) => new Map(current).set(updated.id, updated));
      setEditSummaryOpen(false);
      showToast("Encounter summary updated.", "success");
    } catch {
      showToast("Could not update encounter summary.", "error");
    } finally {
      setSaving(false);
    }
  }

  const vitalSeries = useMemo(() => {
    if (listState.status !== "ready") return [];
    return extractVitalSeries(listState.data, detailCache);
  }, [listState, detailCache]);
  const selectedSoapTemplate = soapTemplates.find(
    (template) => template.templateId === selectedSoapTemplateId,
  );

  function openEncounter(id: number) {
    if (
      addSoapOpen &&
      soapDraftDirty &&
      !window.confirm(
        "Discard the unsaved SOAP draft and open another encounter?",
      )
    )
      return;
    setSelectedId(id);
    setAddVitalsOpen(false);
    setAddSoapOpen(false);
    setEditSummaryOpen(false);
    setEncounterArchiveAction(null);
    setEncounterArchiveError(null);
    setVitalsForm(BLANK_VITALS);
    setSoapForm(BLANK_SOAP);
    setSoapDraftVersion(0);
    setSoapDraftDirty(false);
    setSoapConflict(null);
    setSoapSaveError(null);
    setDetailState({ status: "loading", id });
    getEncounterDetail(session.sessionId, id, undefined, true)
      .then((data) => {
        setDetailState({ status: "ready", data });
        setDetailCache((prev) => new Map(prev).set(id, data));
      })
      .catch((err) =>
        setDetailState({
          status: "error",
          message: err instanceof Error ? err.message : "Failed to load.",
        }),
      );
  }

  async function handleAddVitals(e: React.FormEvent) {
    e.preventDefault();
    if (selectedId == null) return;
    setSaving(true);
    try {
      await createEncounterVitals(session.sessionId, selectedId, {
        dateTime: new Date().toISOString().replace("T", " ").slice(0, 19),
        systolic: vitalsForm.systolic ? Number(vitalsForm.systolic) : undefined,
        diastolic: vitalsForm.diastolic
          ? Number(vitalsForm.diastolic)
          : undefined,
        pulse: vitalsForm.pulse ? Number(vitalsForm.pulse) : undefined,
        temperature: vitalsForm.temperature
          ? Number(vitalsForm.temperature)
          : undefined,
        respiration: vitalsForm.respiration
          ? Number(vitalsForm.respiration)
          : undefined,
        oxygenSaturation: vitalsForm.oxygenSaturation
          ? Number(vitalsForm.oxygenSaturation)
          : undefined,
        weight: vitalsForm.weight ? Number(vitalsForm.weight) : undefined,
        height: vitalsForm.height ? Number(vitalsForm.height) : undefined,
      });
      showToast("Vitals recorded.", "success");
      setAddVitalsOpen(false);
      setVitalsForm(BLANK_VITALS);
      openEncounter(selectedId);
    } catch {
      showToast("Could not record vitals.", "error");
    } finally {
      setSaving(false);
    }
  }

  async function handleAddSoap(e: React.FormEvent) {
    e.preventDefault();
    if (selectedId == null) return;
    if (!Object.values(soapForm).some((value) => value.trim())) {
      setSoapSaveError("Enter content in at least one SOAP section.");
      return;
    }
    setSaving(true);
    setSoapSaveError(null);
    try {
      const result = await saveEncounterSoapNote(
        session.sessionId,
        selectedId,
        {
          dateTime: new Date().toISOString().replace("T", " ").slice(0, 19),
          expectedVersion: soapDraftVersion,
          ...soapForm,
        },
      );
      const savedNote = result.detail.soapNote;
      setDetailState({ status: "ready", data: result.detail });
      setDetailCache((current) =>
        new Map(current).set(result.detail.id, result.detail),
      );
      showToast(
        `SOAP note version ${savedNote?.version ?? soapDraftVersion + 1} saved.`,
        "success",
      );
      setAddSoapOpen(false);
      setSoapForm(BLANK_SOAP);
      setSoapDraftVersion(savedNote?.version ?? soapDraftVersion + 1);
      setSoapDraftDirty(false);
      setSoapConflict(null);
    } catch (error) {
      const conflict = getEncounterSoapNoteConflict(error);
      if (conflict) {
        let latest: VersionedEncounterSoapNote | null | undefined;
        try {
          const refreshed = getVersionedEncounterDetail(
            await getEncounterDetail(
              session.sessionId,
              selectedId,
              undefined,
              true,
            ),
          );
          latest = refreshed.soapNote;
          setDetailState({ status: "ready", data: refreshed });
          setDetailCache((current) =>
            new Map(current).set(refreshed.id, refreshed),
          );
        } catch {
          latest = undefined;
        }
        setSoapConflict({ ...conflict, latest });
        setSoapSaveError(
          conflict.isLocked
            ? "The draft was not saved because this encounter is locked."
            : "The draft was not saved. Review the newer server version before choosing how to continue.",
        );
      } else {
        const message =
          error instanceof Error
            ? error.message
            : "Could not save the SOAP note.";
        setSoapSaveError(message);
        showToast("Could not save SOAP note.", "error");
      }
    } finally {
      setSaving(false);
    }
  }

  function openSoapDraft(encounter: EncounterDetail) {
    const current = getVersionedEncounterDetail(encounter).soapNote;
    const locked =
      current?.isLocked ??
      encounter.signatures.some((signature) => signature.isLock);
    if (locked) {
      showToast(
        "This encounter is locked. Use the governed amendment workflow for clinical changes.",
        "error",
      );
      return;
    }
    setSoapForm({
      subjective: current?.subjective ?? "",
      objective: current?.objective ?? "",
      assessment: current?.assessment ?? "",
      plan: current?.plan ?? "",
    });
    setSoapDraftVersion(current?.version ?? 0);
    setSoapDraftDirty(false);
    setSoapConflict(null);
    setSoapSaveError(null);
    setAddSoapOpen(true);
    setAddVitalsOpen(false);
  }

  function cancelSoapDraft() {
    if (soapDraftDirty && !window.confirm("Discard the unsaved SOAP draft?"))
      return;
    setAddSoapOpen(false);
    setSoapForm(BLANK_SOAP);
    setSoapDraftVersion(0);
    setSoapDraftDirty(false);
    setSoapConflict(null);
    setSoapSaveError(null);
  }

  function acceptLatestSoapVersion(keepDraft: boolean) {
    const latest = soapConflict?.latest;
    if (!latest) return;
    if (!keepDraft) {
      setSoapForm({
        subjective: latest.subjective ?? "",
        objective: latest.objective ?? "",
        assessment: latest.assessment ?? "",
        plan: latest.plan ?? "",
      });
      setSoapDraftDirty(false);
    }
    setSoapDraftVersion(latest.version);
    setSoapConflict(null);
    setSoapSaveError(null);
    if (keepDraft) {
      showToast(
        `Draft rebased on SOAP version ${latest.version}. Review it before saving.`,
        "success",
      );
    }
  }

  function applySoapTemplate() {
    const template = soapTemplates.find(
      (item) => item.templateId === selectedSoapTemplateId,
    );
    if (!template) return;
    const hasDraft = Object.values(soapForm).some(Boolean);
    if (
      hasDraft &&
      !window.confirm("Apply this template and replace the current SOAP draft?")
    )
      return;
    setSoapForm({
      subjective: template.subjective,
      objective: template.objective,
      assessment: template.assessment,
      plan: template.plan,
    });
    setSoapDraftDirty(true);
    setSoapConflict(null);
    setSoapSaveError(null);
  }

  return (
    <div className="clinician-page">
      {/* Vitals trend panel */}
      {vitalSeries.length >= 2 && (
        <section className="cl-card" style={{ marginBottom: 16 }}>
          <div className="cl-card-header">
            <h2 className="cl-card-title">
              <TrendingUp size={15} /> Vital trends ({vitalSeries.length}{" "}
              visits)
            </h2>
            <button
              className="cl-link"
              type="button"
              onClick={() => setShowTrends((s) => !s)}
            >
              {showTrends ? "Hide" : "Show"}
            </button>
          </div>
          {showTrends && (
            <div className="vital-trends-grid">
              {[
                {
                  label: "Systolic BP",
                  key: "systolic" as const,
                  color: "#993c1d",
                },
                {
                  label: "Diastolic BP",
                  key: "diastolic" as const,
                  color: "#d97706",
                },
                { label: "Pulse", key: "pulse" as const, color: "#0f6e56" },
                {
                  label: "Weight (lbs)",
                  key: "weight" as const,
                  color: "#7c3aed",
                },
                {
                  label: "O₂ Sat (%)",
                  key: "oxygenSaturation" as const,
                  color: "#0891b2",
                },
                {
                  label: "Temp (°F)",
                  key: "temperature" as const,
                  color: "#db2777",
                },
              ].map(({ label, key, color }) => {
                const vals = vitalSeries
                  .map((s) => s.vitals[key])
                  .filter((v): v is number => v != null);
                if (vals.length < 2) return null;
                const latest = vals[vals.length - 1];
                return (
                  <div key={key} className="vital-trend-item">
                    <div className="vital-trend-top">
                      <span className="vital-trend-label">{label}</span>
                      <span className="vital-trend-value">{latest}</span>
                    </div>
                    <Sparkline values={vals} color={color} />
                  </div>
                );
              })}
            </div>
          )}
        </section>
      )}

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
          onClick={() => {
            setSelectedId(null);
            setDetailState({ status: "idle" });
            setEncounterArchiveAction(null);
            setEncounterArchiveError(null);
            setShowArchived((value) => !value);
          }}
          style={{ marginRight: 8 }}
        >
          {showArchived ? "Show active" : "Show archived"}
        </button>
        <button
          className="cl-btn-primary"
          type="button"
          onClick={() =>
            navigate(`/clinician/patients/${patientId}/encounters/new`)
          }
        >
          <Plus size={14} /> New encounter
        </button>
      </div>

      <div className="cl-encounter-layout">
        {/* Encounter list */}
        <aside className="cl-encounter-list">
          {listState.status === "loading" && (
            <div className="skeleton-list">
              {[0, 1, 2, 3].map((i) => (
                <div key={i} className="skeleton-row" style={{ height: 64 }} />
              ))}
            </div>
          )}
          {listState.status === "error" && (
            <p className="cl-empty-text">{listState.message}</p>
          )}
          {listState.status === "ready" && listState.data.length === 0 && (
            <p className="cl-empty-text">No encounters on file.</p>
          )}
          {listState.status === "ready" &&
            listState.data.map((enc) => (
              <button
                key={enc.encounter}
                className={`cl-encounter-item${selectedId === enc.encounter ? " cl-encounter-item-active" : ""}`}
                type="button"
                data-encounter={enc.encounter}
                onClick={() => openEncounter(enc.encounter)}
              >
                <div className="cl-encounter-item-inner">
                  <div>
                    <p className="cl-encounter-date">{enc.date}</p>
                    <p className="cl-encounter-reason">
                      {enc.reason ?? "Visit"}
                    </p>
                    {enc.diagnosisText && (
                      <p className="cl-encounter-dx">{enc.diagnosisText}</p>
                    )}
                  </div>
                  <ChevronRight size={14} />
                </div>
                <div className="cl-encounter-badges">
                  {enc.hasSoapNote && (
                    <span className="cl-badge cl-badge-teal">SOAP</span>
                  )}
                  {enc.hasVitals && (
                    <span className="cl-badge cl-badge-blue">Vitals</span>
                  )}
                  {enc.billingLineCount > 0 && (
                    <span className="cl-badge cl-badge-muted">
                      {enc.billingLineCount} billing
                    </span>
                  )}
                </div>
              </button>
            ))}
        </aside>

        {/* Encounter detail */}
        <section className="cl-encounter-detail">
          {detailState.status === "idle" && (
            <div className="cl-encounter-empty">
              <FileText size={40} />
              <p>Select an encounter to view details.</p>
            </div>
          )}
          {detailState.status === "loading" && (
            <div className="skeleton-list">
              {[0, 1, 2].map((i) => (
                <div key={i} className="skeleton-row" style={{ height: 80 }} />
              ))}
            </div>
          )}
          {detailState.status === "error" && (
            <p className="cl-empty-text">{detailState.message}</p>
          )}
          {detailState.status === "ready" &&
            (() => {
              const { data: enc } = detailState;
              const versionedEncounter = getVersionedEncounterDetail(enc);
              const lifecycleEncounter = asEncounterLifecycleDetail(enc);
              const encounterArchived = Boolean(lifecycleEncounter.archivedAt);
              const soapNote = versionedEncounter.soapNote;
              const soapLocked =
                soapNote?.isLocked ??
                enc.signatures.some((signature) => signature.isLock);
              return (
                <>
                  <div className="cl-card">
                    <div className="cl-card-header">
                      <h2 className="cl-card-title">
                        {enc.date} — {enc.reason ?? "Visit"}
                      </h2>
                      <span className="cl-badge cl-badge-muted">
                        Enc #{enc.encounter}
                      </span>
                    </div>
                    <div
                      className="cl-inline-form-actions"
                      style={{ marginTop: 10 }}
                    >
                      <button
                        className="cl-btn-secondary"
                        type="button"
                        onClick={() => openSummaryEditor(enc)}
                        disabled={saving}
                      >
                        <Pencil size={14} /> Edit summary
                      </button>
                    </div>
                    {editSummaryOpen && (
                      <form
                        onSubmit={(event) => saveSummary(event, enc.encounter)}
                        style={{ marginTop: 14 }}
                      >
                        <div className="form-row">
                          <div className="field">
                            <label className="label" htmlFor="encounter-reason">
                              Reason
                            </label>
                            <input
                              id="encounter-reason"
                              className="input"
                              required
                              value={summaryForm.reason}
                              onChange={(event) =>
                                setSummaryForm((form) => ({
                                  ...form,
                                  reason: event.target.value,
                                }))
                              }
                            />
                          </div>
                          <div className="field">
                            <label className="label" htmlFor="encounter-pos">
                              Place of service
                            </label>
                            <input
                              id="encounter-pos"
                              className="input"
                              type="number"
                              min="0"
                              value={summaryForm.posCode}
                              onChange={(event) =>
                                setSummaryForm((form) => ({
                                  ...form,
                                  posCode: event.target.value,
                                }))
                              }
                            />
                          </div>
                        </div>
                        <div className="form-row">
                          <div className="field">
                            <label
                              className="label"
                              htmlFor="encounter-sensitivity"
                            >
                              Sensitivity
                            </label>
                            <input
                              id="encounter-sensitivity"
                              className="input"
                              value={summaryForm.sensitivity}
                              onChange={(event) =>
                                setSummaryForm((form) => ({
                                  ...form,
                                  sensitivity: event.target.value,
                                }))
                              }
                            />
                          </div>
                          <div className="field">
                            <label
                              className="label"
                              htmlFor="encounter-referral"
                            >
                              Referral source
                            </label>
                            <input
                              id="encounter-referral"
                              className="input"
                              value={summaryForm.referralSource}
                              onChange={(event) =>
                                setSummaryForm((form) => ({
                                  ...form,
                                  referralSource: event.target.value,
                                }))
                              }
                            />
                          </div>
                        </div>
                        <div className="form-row">
                          <div className="field">
                            <label
                              className="label"
                              htmlFor="encounter-external-id"
                            >
                              External reference
                            </label>
                            <input
                              id="encounter-external-id"
                              className="input"
                              value={summaryForm.externalId}
                              onChange={(event) =>
                                setSummaryForm((form) => ({
                                  ...form,
                                  externalId: event.target.value,
                                }))
                              }
                            />
                          </div>
                          <div className="field">
                            <label
                              className="label"
                              htmlFor="encounter-billing-note"
                            >
                              Billing note
                            </label>
                            <input
                              id="encounter-billing-note"
                              className="input"
                              value={summaryForm.billingNote}
                              onChange={(event) =>
                                setSummaryForm((form) => ({
                                  ...form,
                                  billingNote: event.target.value,
                                }))
                              }
                            />
                          </div>
                        </div>
                        <div className="cl-inline-form-actions">
                          <button
                            className="cl-btn-primary"
                            type="submit"
                            disabled={saving}
                          >
                            {saving ? "Saving…" : "Save summary"}
                          </button>
                          <button
                            className="cl-btn-secondary"
                            type="button"
                            onClick={() => setEditSummaryOpen(false)}
                            disabled={saving}
                          >
                            Cancel
                          </button>
                        </div>
                      </form>
                    )}
                    <div
                      className="cl-inline-form-actions"
                      style={{ marginTop: 10 }}
                    >
                      <button
                        className="cl-btn-secondary"
                        type="button"
                        disabled={archiving}
                        onClick={() => {
                          setEncounterArchiveError(null);
                          setEncounterArchiveAction({
                            restore: encounterArchived,
                            reason: "",
                          });
                        }}
                      >
                        {archiving
                          ? "Saving…"
                          : encounterArchived
                            ? "Restore encounter"
                            : "Archive encounter"}
                      </button>
                    </div>
                    {encounterArchiveAction && (
                      <form
                        className="cl-inline-edit-form"
                        onSubmit={(event) => {
                          event.preventDefault();
                          void changeArchiveState(
                            enc,
                            encounterArchiveAction.restore,
                            encounterArchiveAction.reason,
                          );
                        }}
                      >
                        <div className="field">
                          <label
                            className="label"
                            htmlFor="encounter-archive-reason"
                          >
                            {encounterArchiveAction.restore
                              ? "Restore reason"
                              : "Archive reason"}
                          </label>
                          <textarea
                            id="encounter-archive-reason"
                            className="textarea"
                            rows={3}
                            maxLength={500}
                            required
                            value={encounterArchiveAction.reason}
                            onChange={(event) =>
                              setEncounterArchiveAction((current) =>
                                current
                                  ? {
                                      ...current,
                                      reason: event.target.value,
                                    }
                                  : current,
                              )
                            }
                          />
                        </div>
                        <p className="cl-empty-text">
                          Loaded archive version{" "}
                          {lifecycleEncounter.archiveVersion}. Notes, vitals,
                          signatures, charges, orders, and attachments remain
                          intact.
                        </p>
                        {encounterArchiveError && (
                          <p className="cl-soap-save-error" role="alert">
                            {encounterArchiveError}
                          </p>
                        )}
                        <div className="cl-inline-form-actions">
                          <button
                            className="cl-btn-primary"
                            type="submit"
                            disabled={archiving}
                          >
                            {archiving
                              ? "Saving…"
                              : encounterArchiveAction.restore
                                ? "Restore encounter"
                                : "Archive encounter"}
                          </button>
                          <button
                            className="cl-btn-secondary"
                            type="button"
                            disabled={archiving}
                            onClick={() => {
                              setEncounterArchiveAction(null);
                              setEncounterArchiveError(null);
                            }}
                          >
                            Cancel
                          </button>
                        </div>
                      </form>
                    )}
                    <ul className="fact-list">
                      {lifecycleEncounter.archivedAt && (
                        <li className="fact-row">
                          <span>Archived</span>
                          <span>{lifecycleEncounter.archivedAt}</span>
                        </li>
                      )}
                      {enc.providerName && (
                        <li className="fact-row">
                          <span>Provider</span>
                          <span>{enc.providerName}</span>
                        </li>
                      )}
                      {enc.facilityName && (
                        <li className="fact-row">
                          <span>Facility</span>
                          <span>{enc.facilityName}</span>
                        </li>
                      )}
                      {enc.diagnosisText && (
                        <li className="fact-row">
                          <span>Diagnosis</span>
                          <span>
                            {enc.diagnosisCode} — {enc.diagnosisText}
                          </span>
                        </li>
                      )}
                    </ul>
                  </div>

                  <div className="cl-card">
                    <div className="cl-card-header">
                      <h2 className="cl-card-title">Vitals</h2>
                      <button
                        className="cl-btn-icon"
                        type="button"
                        aria-label="Record vitals"
                        onClick={() => {
                          setAddVitalsOpen((o) => !o);
                          setAddSoapOpen(false);
                        }}
                      >
                        <Plus size={14} />
                      </button>
                    </div>
                    {addVitalsOpen && (
                      <form
                        className="cl-vitals-form"
                        onSubmit={handleAddVitals}
                      >
                        <div className="cl-vitals-input-grid">
                          {[
                            {
                              id: "v-sys",
                              label: "Systolic",
                              key: "systolic" as const,
                              placeholder: "120",
                            },
                            {
                              id: "v-dia",
                              label: "Diastolic",
                              key: "diastolic" as const,
                              placeholder: "80",
                            },
                            {
                              id: "v-pulse",
                              label: "Pulse (bpm)",
                              key: "pulse" as const,
                              placeholder: "72",
                            },
                            {
                              id: "v-temp",
                              label: "Temp (°F)",
                              key: "temperature" as const,
                              placeholder: "98.6",
                            },
                            {
                              id: "v-resp",
                              label: "Resp (/min)",
                              key: "respiration" as const,
                              placeholder: "16",
                            },
                            {
                              id: "v-o2",
                              label: "O₂ Sat (%)",
                              key: "oxygenSaturation" as const,
                              placeholder: "99",
                            },
                            {
                              id: "v-wt",
                              label: "Weight (lbs)",
                              key: "weight" as const,
                              placeholder: "150",
                            },
                            {
                              id: "v-ht",
                              label: "Height (in)",
                              key: "height" as const,
                              placeholder: "68",
                            },
                          ].map(({ id, label, key, placeholder }) => (
                            <div key={key} className="field">
                              <label className="label" htmlFor={id}>
                                {label}
                              </label>
                              <input
                                id={id}
                                type="number"
                                step="0.1"
                                className="input"
                                placeholder={placeholder}
                                value={vitalsForm[key]}
                                onChange={(e) =>
                                  setVitalsForm((f) => ({
                                    ...f,
                                    [key]: e.target.value,
                                  }))
                                }
                              />
                            </div>
                          ))}
                        </div>
                        <div className="cl-inline-form-actions">
                          <button
                            className="cl-btn-primary"
                            type="submit"
                            disabled={saving}
                          >
                            {saving ? "Saving…" : "Record vitals"}
                          </button>
                          <button
                            className="cl-btn-secondary"
                            type="button"
                            onClick={() => setAddVitalsOpen(false)}
                          >
                            Cancel
                          </button>
                        </div>
                      </form>
                    )}
                    {enc.vitals ? (
                      <div className="cl-vitals-grid">
                        {vitalRow(
                          "BP",
                          enc.vitals.bloodPressure ??
                            (enc.vitals.systolic
                              ? `${enc.vitals.systolic}/${enc.vitals.diastolic}`
                              : null),
                        )}
                        {vitalRow("Pulse", enc.vitals.pulse, "bpm")}
                        {vitalRow("Temp", enc.vitals.temperature, "°F")}
                        {vitalRow("Resp", enc.vitals.respiration, "/min")}
                        {vitalRow("O₂ Sat", enc.vitals.oxygenSaturation, "%")}
                        {vitalRow("Weight", enc.vitals.weight, "lbs")}
                        {vitalRow("Height", enc.vitals.height, "in")}
                        {vitalRow("BMI", enc.vitals.bmi)}
                      </div>
                    ) : (
                      !addVitalsOpen && (
                        <p className="cl-empty-text">
                          No vitals recorded.{" "}
                          <button
                            className="cl-link"
                            type="button"
                            onClick={() => setAddVitalsOpen(true)}
                          >
                            Add vitals
                          </button>
                        </p>
                      )
                    )}
                  </div>

                  <div
                    className="cl-card"
                    aria-labelledby="encounter-soap-note-title"
                  >
                    <div className="cl-card-header">
                      <h2
                        className="cl-card-title"
                        id="encounter-soap-note-title"
                      >
                        SOAP note
                      </h2>
                      <button
                        className="cl-btn-icon"
                        type="button"
                        aria-label={
                          soapLocked
                            ? "SOAP note locked"
                            : soapNote
                              ? "Edit SOAP note draft"
                              : "Add SOAP note draft"
                        }
                        title={
                          soapLocked
                            ? "A locking signature prevents direct SOAP edits."
                            : undefined
                        }
                        disabled={soapLocked || saving}
                        onClick={() =>
                          addSoapOpen ? cancelSoapDraft() : openSoapDraft(enc)
                        }
                      >
                        {soapNote ? <Pencil size={14} /> : <Plus size={14} />}
                      </button>
                    </div>
                    {soapLocked && (
                      <p className="cl-soap-lock-notice" role="status">
                        This SOAP note is locked by an encounter signature.
                        Clinical changes must use the governed amendment
                        workflow.
                      </p>
                    )}
                    {addSoapOpen && (
                      <form onSubmit={handleAddSoap}>
                        <div className="cl-soap-draft-status" role="status">
                          <strong>Unsaved draft</strong>
                          <span>
                            Based on saved SOAP version {soapDraftVersion}.
                            Nothing changes in the chart until you save a new
                            version.
                          </span>
                        </div>
                        {soapSaveError && (
                          <p className="cl-soap-save-error" role="alert">
                            {soapSaveError}
                          </p>
                        )}
                        {soapConflict && (
                          <div className="cl-soap-conflict" role="alert">
                            <strong>
                              {soapConflict.isLocked
                                ? "Encounter locked"
                                : "A newer SOAP version was saved"}
                            </strong>
                            <p>{soapConflict.message}</p>
                            {soapConflict.latest && !soapConflict.isLocked && (
                              <>
                                <div className="cl-soap-conflict-latest">
                                  <span>
                                    Server version {soapConflict.latest.version}
                                  </span>
                                  <span>
                                    Saved {soapConflict.latest.savedAt}
                                    {soapConflict.latest.savedBy
                                      ? ` by ${soapConflict.latest.savedBy}`
                                      : ""}
                                  </span>
                                  {(
                                    [
                                      "subjective",
                                      "objective",
                                      "assessment",
                                      "plan",
                                    ] as const
                                  ).map((field) =>
                                    soapConflict.latest?.[field] ? (
                                      <p key={field}>
                                        <strong
                                          style={{
                                            textTransform: "capitalize",
                                          }}
                                        >
                                          {field}:
                                        </strong>{" "}
                                        {soapConflict.latest[field]}
                                      </p>
                                    ) : null,
                                  )}
                                </div>
                                <div className="cl-inline-form-actions">
                                  <button
                                    className="cl-btn-secondary"
                                    type="button"
                                    onClick={() =>
                                      acceptLatestSoapVersion(false)
                                    }
                                  >
                                    Use latest saved note
                                  </button>
                                  <button
                                    className="cl-btn-secondary"
                                    type="button"
                                    onClick={() =>
                                      acceptLatestSoapVersion(true)
                                    }
                                  >
                                    Keep draft after review
                                  </button>
                                </div>
                              </>
                            )}
                          </div>
                        )}
                        <div
                          className="form-row"
                          style={{ alignItems: "end", marginBottom: 12 }}
                        >
                          <div className="field">
                            <label className="label" htmlFor="soap-template">
                              SOAP template
                            </label>
                            <select
                              id="soap-template"
                              className="input"
                              value={selectedSoapTemplateId}
                              onChange={(event) =>
                                setSelectedSoapTemplateId(event.target.value)
                              }
                            >
                              <option value="">Manual SOAP note</option>
                              {soapTemplates.map((template) => (
                                <option
                                  key={template.templateId}
                                  value={template.templateId}
                                >
                                  {template.category}: {template.name}
                                </option>
                              ))}
                            </select>
                          </div>
                          <div className="field" style={{ flex: "0 0 auto" }}>
                            <button
                              className="cl-btn-secondary"
                              type="button"
                              onClick={applySoapTemplate}
                              disabled={!selectedSoapTemplate}
                            >
                              Apply template
                            </button>
                          </div>
                        </div>
                        {selectedSoapTemplate && (
                          <p
                            className="cl-empty-text"
                            style={{ marginTop: -4, marginBottom: 12 }}
                          >
                            {selectedSoapTemplate.description}
                          </p>
                        )}
                        {soapTemplateError && (
                          <p
                            className="cl-empty-text"
                            style={{ marginTop: -4, marginBottom: 12 }}
                          >
                            {soapTemplateError}
                          </p>
                        )}
                        {(
                          [
                            "subjective",
                            "objective",
                            "assessment",
                            "plan",
                          ] as const
                        ).map((field) => (
                          <div
                            key={field}
                            className="field"
                            style={{ marginBottom: 10 }}
                          >
                            <label
                              className="label"
                              htmlFor={`soap-${field}`}
                              style={{ textTransform: "capitalize" }}
                            >
                              {field}
                            </label>
                            <textarea
                              id={`soap-${field}`}
                              className="textarea"
                              rows={3}
                              value={soapForm[field]}
                              onChange={(e) => {
                                setSoapForm((f) => ({
                                  ...f,
                                  [field]: e.target.value,
                                }));
                                setSoapDraftDirty(true);
                                setSoapConflict(null);
                                setSoapSaveError(null);
                              }}
                            />
                          </div>
                        ))}
                        <div className="cl-inline-form-actions">
                          <button
                            className="cl-btn-primary"
                            type="submit"
                            disabled={saving}
                          >
                            {saving ? "Saving…" : "Save new version"}
                          </button>
                          <button
                            className="cl-btn-secondary"
                            type="button"
                            onClick={cancelSoapDraft}
                            disabled={saving}
                          >
                            Discard draft
                          </button>
                        </div>
                      </form>
                    )}
                    {soapNote && (
                      <div className="cl-soap-version-summary">
                        <span className="cl-badge cl-badge-muted">
                          Saved version {soapNote.version}
                        </span>
                        <span>
                          Saved {soapNote.savedAt}
                          {soapNote.savedBy ? ` by ${soapNote.savedBy}` : ""}
                        </span>
                        {soapNote.evidenceSource === "migration-backfill" && (
                          <span>
                            Existing note discovered during migration; no author
                            identity was invented.
                          </span>
                        )}
                      </div>
                    )}
                    {soapNote &&
                    (soapNote.subjective ??
                      soapNote.objective ??
                      soapNote.assessment ??
                      soapNote.plan)
                      ? [
                          {
                            label: "Subjective",
                            text: soapNote.subjective,
                          },
                          { label: "Objective", text: soapNote.objective },
                          {
                            label: "Assessment",
                            text: soapNote.assessment,
                          },
                          { label: "Plan", text: soapNote.plan },
                        ]
                          .filter((s) => s.text)
                          .map((s) => (
                            <div key={s.label} className="cl-soap-section">
                              <p className="cl-soap-label">{s.label}</p>
                              <p className="cl-soap-text">{s.text}</p>
                            </div>
                          ))
                      : !addSoapOpen && (
                          <p className="cl-empty-text">
                            No SOAP note.{" "}
                            <button
                              className="cl-link"
                              type="button"
                              onClick={() => openSoapDraft(enc)}
                              disabled={soapLocked}
                            >
                              {soapLocked ? "Note locked" : "Add note"}
                            </button>
                          </p>
                        )}
                    {soapNote && soapNote.versions.length > 0 && (
                      <details className="cl-soap-history">
                        <summary>
                          SOAP version history ({soapNote.versions.length})
                        </summary>
                        <div className="cl-soap-history-list">
                          {soapNote.versions.map((version) => (
                            <article
                              className="cl-soap-history-item"
                              key={version.id}
                            >
                              <div className="cl-card-header">
                                <strong>Version {version.version}</strong>
                                <span>
                                  {version.savedAt}
                                  {version.savedBy
                                    ? ` · ${version.savedBy}`
                                    : " · author unavailable"}
                                </span>
                              </div>
                              {version.evidenceSource ===
                                "migration-backfill" && (
                                <p className="cl-empty-text">
                                  Migration-discovered evidence
                                </p>
                              )}
                              {(
                                [
                                  "subjective",
                                  "objective",
                                  "assessment",
                                  "plan",
                                ] as const
                              ).map((field) =>
                                version[field] ? (
                                  <p key={field}>
                                    <strong
                                      style={{ textTransform: "capitalize" }}
                                    >
                                      {field}:
                                    </strong>{" "}
                                    {version[field]}
                                  </p>
                                ) : null,
                              )}
                            </article>
                          ))}
                        </div>
                      </details>
                    )}
                  </div>

                  <EncounterClinicalAlerts
                    sessionId={session.sessionId}
                    encounter={enc.encounter}
                  />

                  <EncounterLayoutFormPanel
                    sessionId={session.sessionId}
                    encounter={enc.encounter}
                  />

                  <EncounterSignatures
                    sessionId={session.sessionId}
                    username={session.username}
                    detail={enc}
                    onDetailChange={(updated) => {
                      setDetailState({ status: "ready", data: updated });
                      setDetailCache((current) =>
                        new Map(current).set(updated.id, updated),
                      );
                    }}
                  />

                  <EncounterCodingPanel
                    sessionId={session.sessionId}
                    detail={enc}
                    onDetailChange={(updated) => {
                      setDetailState({ status: "ready", data: updated });
                      setDetailCache((current) =>
                        new Map(current).set(updated.id, updated),
                      );
                    }}
                  />

                  <EncounterAudit sessionId={session.sessionId} detail={enc} />

                  <EncounterDocuments
                    sessionId={session.sessionId}
                    detail={enc}
                    targetEncounters={
                      listState.status === "ready" ? listState.data : []
                    }
                    onDetailChange={(updated) => {
                      setDetailState({ status: "ready", data: updated });
                      setDetailCache((current) =>
                        new Map(current).set(updated.id, updated),
                      );
                    }}
                  />
                </>
              );
            })()}
        </section>
      </div>
    </div>
  );
}
