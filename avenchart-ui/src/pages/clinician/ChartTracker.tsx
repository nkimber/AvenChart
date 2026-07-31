// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useState } from "react";
import { useOutletContext } from "react-router-dom";
import { MapPin, Search, UserRound } from "lucide-react";
import {
  getChartTrackerHistory,
  getChartTrackerOptions,
  lookupChartTrackerPatient,
  recordChartTrackerEvent,
  type ChartTrackerEvent,
  type ChartTrackerOptions,
  type ChartTrackerPatient,
} from "../../api.ts";
import { showToast } from "../../components/Toast.tsx";
import type { ClinicianOutletContext } from "./ClinicianShell.tsx";
export default function ChartTracker() {
  const { session } = useOutletContext<ClinicianOutletContext>();
  const [id, setId] = useState("");
  const [patient, setPatient] = useState<ChartTrackerPatient>();
  const [options, setOptions] = useState<ChartTrackerOptions>({
    locations: [],
    users: [],
  });
  const [history, setHistory] = useState<ChartTrackerEvent[]>([]);
  const [location, setLocation] = useState("");
  const [userId, setUserId] = useState("");
  useEffect(() => {
    getChartTrackerOptions(session.sessionId)
      .then(setOptions)
      .catch(() => showToast("Could not load Chart Tracker options.", "error"));
  }, [session.sessionId]);
  const find = async () => {
    try {
      const p = await lookupChartTrackerPatient(session.sessionId, id);
      setPatient(p);
      setHistory(await getChartTrackerHistory(session.sessionId, p.patientId));
      setLocation("");
      setUserId("");
    } catch {
      setPatient(undefined);
      setHistory([]);
      showToast("Patient ID was not found.", "error");
    }
  };
  const save = async () => {
    if (!patient) return;
    try {
      await recordChartTrackerEvent(session.sessionId, patient.patientId, {
        location: location || undefined,
        userId: userId ? Number(userId) : undefined,
      });
      setHistory(
        await getChartTrackerHistory(session.sessionId, patient.patientId),
      );
      setLocation("");
      setUserId("");
      showToast("Chart location recorded.", "success");
    } catch {
      showToast("Select one active chart location or staff member.", "error");
    }
  };
  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <h1 className="clinician-page-title">Chart Tracker</h1>
        <p className="clinician-page-subtitle">
          Record where a paper chart is checked in or checked out. This is local
          staff evidence, not barcode or kiosk tracking.
        </p>
      </div>
      <section className="cl-card">
        <div className="cl-actions">
          <input
            className="ne-input"
            value={id}
            onChange={(e) => setId(e.target.value)}
            placeholder="Public patient ID or canonical ID"
          />
          <button
            className="cl-btn-primary"
            onClick={() => void find()}
            disabled={!id.trim()}
          >
            <Search size={15} /> Look up
          </button>
        </div>
      </section>
      {patient && (
        <>
          <section className="cl-card">
            <h2 className="cl-section-title">{patient.displayName}</h2>
            <p className="cl-table-sub">
              Public ID {patient.publicId} · DOB {patient.dateOfBirth} ·
              Current:{" "}
              {patient.current?.userName ||
                patient.current?.location ||
                "Unassigned"}
            </p>
            <div className="cl-admin-form-grid">
              <label className="cl-admin-field">
                <span>Check in to</span>
                <select
                  className="ne-input"
                  value={location}
                  onChange={(e) => {
                    setLocation(e.target.value);
                    setUserId("");
                  }}
                >
                  <option value="">Select location</option>
                  {options.locations.map((x) => (
                    <option key={x}>{x}</option>
                  ))}
                </select>
              </label>
              <label className="cl-admin-field">
                <span>Check out to</span>
                <select
                  className="ne-input"
                  value={userId}
                  onChange={(e) => {
                    setUserId(e.target.value);
                    setLocation("");
                  }}
                >
                  <option value="">Select staff member</option>
                  {options.users.map((x) => (
                    <option value={x.id} key={x.id}>
                      {x.displayName}
                    </option>
                  ))}
                </select>
              </label>
            </div>
            <button
              className="cl-btn-primary"
              onClick={() => void save()}
              disabled={!location && !userId}
            >
              {userId ? <UserRound size={15} /> : <MapPin size={15} />} Record
              chart location
            </button>
          </section>
          <section className="cl-card">
            <h2 className="cl-section-title">Location history</h2>
            <table className="cl-table">
              <thead>
                <tr>
                  <th>When</th>
                  <th>Location / staff</th>
                </tr>
              </thead>
              <tbody>
                {history.map((x) => (
                  <tr key={x.id}>
                    <td>{new Date(x.recordedAt).toLocaleString()}</td>
                    <td>{x.userName || x.location}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </section>
        </>
      )}
    </div>
  );
}
