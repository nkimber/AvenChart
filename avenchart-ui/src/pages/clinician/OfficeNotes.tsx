import { useEffect, useEffectEvent, useState } from "react";
import { useOutletContext } from "react-router-dom";
import { Check, Pencil, Plus, Trash2, X } from "lucide-react";
import {
  createOfficeNote,
  deleteOfficeNote,
  getOfficeNotes,
  setOfficeNoteActivity,
  updateOfficeNote,
  type OfficeNoteItem,
} from "../../api.ts";
import { showToast } from "../../components/Toast.tsx";
import type { ClinicianOutletContext } from "./ClinicianShell.tsx";

type Activity = "active" | "inactive" | "all";

export default function OfficeNotes() {
  const { session } = useOutletContext<ClinicianOutletContext>();
  const [activity, setActivity] = useState<Activity>("active");
  const [notes, setNotes] = useState<OfficeNoteItem[]>([]);
  const [body, setBody] = useState("");
  const [editing, setEditing] = useState<string | null>(null);
  const [draft, setDraft] = useState("");

  async function refresh(next = activity) {
    try {
      const result = await getOfficeNotes(session.sessionId, next);
      setNotes(result.notes);
    } catch {
      showToast("Could not load office notes.", "error");
    }
  }
  const refreshOnFilterChange = useEffectEvent(refresh);
  useEffect(() => {
    void refreshOnFilterChange(activity);
  }, [activity, session.sessionId]);
  async function add() {
    try {
      await createOfficeNote(session.sessionId, body);
      setBody("");
      await refresh();
      showToast("Office note added.", "success");
    } catch {
      showToast("Office note could not be added.", "error");
    }
  }
  async function save(id: string) {
    try {
      await updateOfficeNote(session.sessionId, id, draft);
      setEditing(null);
      await refresh();
      showToast("Office note updated.", "success");
    } catch {
      showToast("Office note could not be updated.", "error");
    }
  }
  async function toggle(note: OfficeNoteItem) {
    try {
      await setOfficeNoteActivity(session.sessionId, note.id, !note.active);
      await refresh();
      showToast(
        note.active ? "Office note inactivated." : "Office note activated.",
        "success",
      );
    } catch {
      showToast("Office note activity could not be changed.", "error");
    }
  }
  async function remove(id: string) {
    if (!window.confirm("Permanently delete this office note?")) return;
    try {
      await deleteOfficeNote(session.sessionId, id);
      await refresh();
      showToast("Office note deleted.", "success");
    } catch {
      showToast("Office note could not be deleted.", "error");
    }
  }

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <h1 className="clinician-page-title">Office Notes</h1>
        <p className="clinician-page-subtitle">
          Shared, text-only practice notes. This mirrors legacy active-state,
          edit, and delete behavior.
        </p>
      </div>
      <section className="cl-card">
        <div className="cl-inline-form">
          <textarea
            className="ne-input"
            rows={3}
            maxLength={4000}
            value={body}
            onChange={(event) => setBody(event.target.value)}
            placeholder="Enter new office note here. Text only."
          />
          <div className="cl-inline-form-actions">
            <button
              className="cl-btn-primary"
              type="button"
              disabled={!body.trim()}
              onClick={() => void add()}
            >
              <Plus size={15} /> Add note
            </button>
          </div>
        </div>
      </section>
      <section className="cl-card">
        <div className="cl-tab-row">
          {(["active", "all", "inactive"] as Activity[]).map((value) => (
            <button
              key={value}
              className={`cl-tab${activity === value ? " cl-tab-active" : ""}`}
              type="button"
              onClick={() => setActivity(value)}
            >
              {value === "all" ? "All" : `Only ${value}`}
            </button>
          ))}
        </div>
        <table className="cl-table">
          <thead>
            <tr>
              <th>Active</th>
              <th>Date / author</th>
              <th>Office note</th>
              <th>
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {notes.map((note) => (
              <tr key={note.id}>
                <td>
                  <button
                    className="cl-icon-button"
                    title={note.active ? "Inactivate" : "Activate"}
                    aria-label={
                      note.active
                        ? "Inactivate office note"
                        : "Activate office note"
                    }
                    onClick={() => void toggle(note)}
                  >
                    <Check size={15} /> {note.active ? "Active" : "Inactive"}
                  </button>
                </td>
                <td>
                  {new Date(note.createdAt).toLocaleString()}
                  <p className="cl-table-sub">{note.author}</p>
                </td>
                <td>
                  {editing === note.id ? (
                    <textarea
                      className="ne-input"
                      rows={4}
                      maxLength={4000}
                      value={draft}
                      onChange={(event) => setDraft(event.target.value)}
                    />
                  ) : (
                    <span style={{ whiteSpace: "pre-wrap" }}>{note.body}</span>
                  )}
                </td>
                <td className="cl-admin-row-actions">
                  {editing === note.id ? (
                    <>
                      <button
                        className="cl-icon-button"
                        title="Save"
                        aria-label="Save office note"
                        onClick={() => void save(note.id)}
                        disabled={!draft.trim()}
                      >
                        <Check size={15} />
                      </button>
                      <button
                        className="cl-icon-button"
                        title="Cancel"
                        aria-label="Cancel office note edit"
                        onClick={() => setEditing(null)}
                      >
                        <X size={15} />
                      </button>
                    </>
                  ) : (
                    <>
                      <button
                        className="cl-icon-button"
                        title="Edit"
                        aria-label="Edit office note"
                        onClick={() => {
                          setEditing(note.id);
                          setDraft(note.body);
                        }}
                      >
                        <Pencil size={15} />
                      </button>
                      <button
                        className="cl-icon-button cl-icon-button-danger"
                        title="Delete"
                        aria-label="Delete office note"
                        onClick={() => void remove(note.id)}
                      >
                        <Trash2 size={15} />
                      </button>
                    </>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {notes.length === 0 && (
          <p className="cl-empty-text">
            No {activity === "all" ? "" : activity} office notes.
          </p>
        )}
      </section>
    </div>
  );
}
