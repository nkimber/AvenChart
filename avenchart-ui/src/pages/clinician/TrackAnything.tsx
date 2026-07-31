// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useEffectEvent, useState } from "react";
import { useOutletContext } from "react-router-dom";
import { Plus, Trash2 } from "lucide-react";
import {
  deleteTrackAnything,
  getTrackAnything,
  saveTrackAnything,
  type TrackAnythingItem,
} from "../../api.ts";
import { showToast } from "../../components/Toast.tsx";
import type { ClinicianOutletContext } from "./ClinicianShell.tsx";
export default function TrackAnything() {
  const { session } = useOutletContext<ClinicianOutletContext>();
  const [items, setItems] = useState<TrackAnythingItem[]>([]);
  const [form, setForm] = useState({
    parentId: "",
    name: "",
    description: "",
    position: "0",
  });
  const load = async () => {
    try {
      setItems((await getTrackAnything(session.sessionId)).items);
    } catch {
      showToast("Could not load tracks.", "error");
    }
  };
  const loadOnMount = useEffectEvent(load);
  useEffect(() => {
    void loadOnMount();
  }, [session.sessionId]);
  const save = async () => {
    try {
      await saveTrackAnything(session.sessionId, {
        parentId: form.parentId ? Number(form.parentId) : null,
        name: form.name,
        description: form.description || null,
        position: Number(form.position),
        active: true,
      });
      setForm({ parentId: "", name: "", description: "", position: "0" });
      await load();
      showToast("Track item saved.", "success");
    } catch {
      showToast("Track item could not be saved.", "error");
    }
  };
  const parents = items.filter((x) => x.parentId == null);
  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <h1 className="clinician-page-title">Configure Tracks</h1>
        <p className="clinician-page-subtitle">
          Create and modify ordered tracks and their selectable child items.
        </p>
      </div>
      <section className="cl-card">
        <div className="cl-admin-form-grid">
          <label className="cl-admin-field">
            <span>Parent track</span>
            <select
              className="ne-input"
              value={form.parentId}
              onChange={(e) => setForm({ ...form, parentId: e.target.value })}
            >
              <option value="">New track</option>
              {parents.map((x) => (
                <option key={x.id} value={x.id}>
                  {x.name}
                </option>
              ))}
            </select>
          </label>
          {(["name", "description", "position"] as const).map((k) => (
            <label className="cl-admin-field" key={k}>
              <span>{k}</span>
              <input
                className="ne-input"
                value={form[k]}
                onChange={(e) => setForm({ ...form, [k]: e.target.value })}
              />
            </label>
          ))}
        </div>
        <button
          className="cl-btn-primary"
          disabled={!form.name.trim()}
          onClick={() => void save()}
        >
          <Plus size={15} /> Save
        </button>
      </section>
      <section className="cl-card">
        <table className="cl-table">
          <thead>
            <tr>
              <th>Track / item</th>
              <th>Description</th>
              <th>Position</th>
              <th>State</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {parents.map((p) => (
              <>
                <tr key={p.id}>
                  <td>
                    <strong>{p.name}</strong>
                  </td>
                  <td>{p.description}</td>
                  <td>{p.position}</td>
                  <td>{p.active ? "Active" : "Inactive"}</td>
                  <td>
                    <button
                      className="cl-icon-button cl-icon-button-danger"
                      onClick={() =>
                        void deleteTrackAnything(session.sessionId, p.id).then(
                          load,
                        )
                      }
                      aria-label="Delete track"
                    >
                      <Trash2 size={15} />
                    </button>
                  </td>
                </tr>
                {items
                  .filter((x) => x.parentId === p.id)
                  .map((c) => (
                    <tr key={c.id}>
                      <td>↳ {c.name}</td>
                      <td>{c.description}</td>
                      <td>{c.position}</td>
                      <td>{c.active ? "Active" : "Inactive"}</td>
                      <td>
                        <button
                          className="cl-icon-button cl-icon-button-danger"
                          onClick={() =>
                            void deleteTrackAnything(
                              session.sessionId,
                              c.id,
                            ).then(load)
                          }
                          aria-label="Delete item"
                        >
                          <Trash2 size={15} />
                        </button>
                      </td>
                    </tr>
                  ))}
              </>
            ))}
          </tbody>
        </table>
      </section>
    </div>
  );
}
