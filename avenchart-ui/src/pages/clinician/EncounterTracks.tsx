// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useState } from "react";
import { useOutletContext } from "react-router-dom";
import {
  ClipboardPlus,
  History,
  Pencil,
  Plus,
  Save,
  Search,
} from "lucide-react";
import {
  addEncounterTrackReading,
  createEncounterTrack,
  getEncounterTrack,
  getEncounterTracks,
  updateEncounterTrackReading,
  type EncounterTrackCatalog,
  type EncounterTrackRecordDetail,
} from "../../api.ts";
import { showToast } from "../../components/Toast.tsx";
import type { ClinicianOutletContext } from "./ClinicianShell.tsx";

const localNow = () => new Date().toISOString().slice(0, 16);

export default function EncounterTracks() {
  const { session } = useOutletContext<ClinicianOutletContext>();
  const [encounterText, setEncounterText] = useState("1000013");
  const [catalog, setCatalog] = useState<EncounterTrackCatalog>();
  const [selectedTrackId, setSelectedTrackId] = useState("");
  const [detail, setDetail] = useState<EncounterTrackRecordDetail>();
  const [recordedAt, setRecordedAt] = useState(localNow);
  const [values, setValues] = useState<Record<number, string>>({});
  const [editingReadingId, setEditingReadingId] = useState<string>();
  const [loading, setLoading] = useState(false);

  const encounter = Number(encounterText);
  const canLoad = Number.isInteger(encounter) && encounter > 0;

  async function loadCatalog(recordId?: string) {
    if (!canLoad) {
      showToast("Enter a valid encounter number.", "error");
      return;
    }
    setLoading(true);
    try {
      const nextCatalog = await getEncounterTracks(
        session.sessionId,
        encounter,
      );
      setCatalog(nextCatalog);
      const nextRecordId =
        recordId ?? detail?.record.recordId ?? nextCatalog.records[0]?.recordId;
      if (nextRecordId) await selectRecord(nextRecordId);
      else {
        setDetail(undefined);
        setValues({});
        setEditingReadingId(undefined);
      }
    } catch {
      setCatalog(undefined);
      setDetail(undefined);
      setValues({});
      showToast("Encounter tracks could not be loaded.", "error");
    } finally {
      setLoading(false);
    }
  }

  async function selectRecord(recordId: string) {
    if (!canLoad) return;
    try {
      const nextDetail = await getEncounterTrack(
        session.sessionId,
        encounter,
        recordId,
      );
      setDetail(nextDetail);
      setValues(
        Object.fromEntries(nextDetail.items.map((item) => [item.id, ""])),
      );
      setRecordedAt(localNow());
      setEditingReadingId(undefined);
    } catch {
      showToast("Track record history could not be loaded.", "error");
    }
  }

  async function createRecord() {
    if (!canLoad || !selectedTrackId) {
      showToast("Choose a configured track first.", "error");
      return;
    }
    try {
      const record = await createEncounterTrack(
        session.sessionId,
        encounter,
        Number(selectedTrackId),
      );
      setSelectedTrackId("");
      await loadCatalog(record.recordId);
      showToast("Track added to this encounter.", "success");
    } catch {
      showToast("The track could not be added to this encounter.", "error");
    }
  }

  async function saveReading() {
    if (!detail) return;
    try {
      const input = {
        recordedAt: new Date(recordedAt).toISOString(),
        values: detail.items.map((item) => ({
          itemTypeId: item.id,
          value: values[item.id] ?? "",
        })),
      };
      if (editingReadingId)
        await updateEncounterTrackReading(
          session.sessionId,
          encounter,
          detail.record.recordId,
          editingReadingId,
          input,
        );
      else
        await addEncounterTrackReading(
          session.sessionId,
          encounter,
          detail.record.recordId,
          input,
        );
      await selectRecord(detail.record.recordId);
      showToast(
        editingReadingId
          ? "Track reading updated."
          : "Timestamped track reading saved.",
        "success",
      );
    } catch {
      showToast(
        "Enter at least one value and include every captured item.",
        "error",
      );
    }
  }

  function editReading(readingId: string) {
    const reading = detail?.readings.find(
      (candidate) => candidate.readingId === readingId,
    );
    if (!reading) return;
    setEditingReadingId(reading.readingId);
    setRecordedAt(reading.recordedAt.slice(0, 16));
    setValues(
      Object.fromEntries(
        reading.values.map((value) => [value.itemTypeId, value.value]),
      ),
    );
  }

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <h1 className="clinician-page-title">Track entries</h1>
        <p className="clinician-page-subtitle">
          Capture timestamped clinical measurements against configured encounter
          tracks. Values remain local clinical documentation.
        </p>
      </div>
      <section className="cl-card">
        <div className="cl-actions">
          <input
            className="ne-input"
            inputMode="numeric"
            value={encounterText}
            onChange={(event) => setEncounterText(event.target.value)}
            placeholder="Encounter number"
            aria-label="Encounter number"
          />
          <button
            className="cl-btn-primary"
            disabled={loading || !canLoad}
            onClick={() => void loadCatalog()}
          >
            <Search size={15} /> Load encounter
          </button>
        </div>
      </section>
      {catalog ? (
        <>
          <section className="cl-card">
            <h2 className="cl-card-title">Add a track</h2>
            <div className="cl-actions">
              <select
                className="ne-input"
                value={selectedTrackId}
                onChange={(event) => setSelectedTrackId(event.target.value)}
                aria-label="Configured track"
              >
                <option value="">Select configured track</option>
                {catalog.availableTracks.map((track) => (
                  <option key={track.id} value={track.id}>
                    {track.name}
                    {track.items.length
                      ? ` (${track.items.length} items)`
                      : " (no active items)"}
                  </option>
                ))}
              </select>
              <button
                className="cl-btn-secondary"
                disabled={!selectedTrackId}
                onClick={() => void createRecord()}
              >
                <Plus size={15} /> Add track
              </button>
            </div>
            {catalog.availableTracks.length === 0 ? (
              <p className="cl-empty-text">
                No active tracks are configured. Configure a top-level track and
                child items first.
              </p>
            ) : null}
          </section>
          <section className="cl-card">
            <h2 className="cl-card-title">Encounter tracks</h2>
            {catalog.records.length === 0 ? (
              <p className="cl-empty-text">
                No tracks have been attached to this encounter.
              </p>
            ) : (
              <div className="cl-actions">
                {catalog.records.map((record) => (
                  <button
                    key={record.recordId}
                    className={
                      detail?.record.recordId === record.recordId
                        ? "cl-btn-primary"
                        : "cl-btn-secondary"
                    }
                    onClick={() => void selectRecord(record.recordId)}
                  >
                    <ClipboardPlus size={15} /> {record.trackName}
                  </button>
                ))}
              </div>
            )}
          </section>
          {detail ? (
            <>
              <section className="cl-card">
                <h2 className="cl-card-title">
                  {detail.record.trackName}{" "}
                  {editingReadingId ? "reading correction" : "reading"}
                </h2>
                <p className="cl-table-sub">
                  Attached by {detail.record.createdBy} on{" "}
                  {new Date(detail.record.createdAt).toLocaleString()}.
                </p>
                {detail.items.length === 0 ? (
                  <p className="cl-empty-text">
                    This track has no active items. Configure child items before
                    recording values.
                  </p>
                ) : (
                  <>
                    <div className="cl-admin-form-grid">
                      <label className="cl-admin-field">
                        <span>Date and time</span>
                        <input
                          className="ne-input"
                          type="datetime-local"
                          value={recordedAt}
                          onChange={(event) =>
                            setRecordedAt(event.target.value)
                          }
                        />
                      </label>
                      {detail.items.map((item) => (
                        <label className="cl-admin-field" key={item.id}>
                          <span>{item.name}</span>
                          <input
                            className="ne-input"
                            value={values[item.id] ?? ""}
                            onChange={(event) =>
                              setValues((current) => ({
                                ...current,
                                [item.id]: event.target.value,
                              }))
                            }
                          />
                        </label>
                      ))}
                    </div>
                    <button
                      className="cl-btn-primary"
                      onClick={() => void saveReading()}
                    >
                      <Save size={15} />{" "}
                      {editingReadingId ? "Update reading" : "Save reading"}
                    </button>
                  </>
                )}
              </section>
              <section className="cl-card">
                <h2 className="cl-card-title">
                  <History size={16} /> Reading history
                </h2>
                {detail.readings.length === 0 ? (
                  <p className="cl-empty-text">No readings recorded yet.</p>
                ) : (
                  <table className="cl-table">
                    <thead>
                      <tr>
                        <th>When</th>
                        <th>Recorded by</th>
                        <th>Values</th>
                        <th />
                      </tr>
                    </thead>
                    <tbody>
                      {detail.readings.map((reading) => (
                        <tr key={reading.readingId}>
                          <td>
                            {new Date(reading.recordedAt).toLocaleString()}
                            {reading.updatedAt ? (
                              <p className="cl-table-sub">
                                Updated by {reading.updatedBy} ·{" "}
                                {new Date(reading.updatedAt).toLocaleString()}
                              </p>
                            ) : null}
                          </td>
                          <td>{reading.recordedBy}</td>
                          <td>
                            {reading.values.map((value) => (
                              <p
                                className="cl-table-sub"
                                key={value.itemTypeId}
                              >
                                {value.itemName}: {value.value || "—"}
                              </p>
                            ))}
                          </td>
                          <td>
                            <button
                              className="cl-icon-button"
                              onClick={() => editReading(reading.readingId)}
                              aria-label="Edit reading"
                            >
                              <Pencil size={15} />
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </section>
            </>
          ) : null}
        </>
      ) : null}
    </div>
  );
}
