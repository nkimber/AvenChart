// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useEffectEvent, useState } from "react";
import { useOutletContext } from "react-router-dom";
import {
  addTherapyGroupMember,
  createTherapyGroup,
  createTherapyGroupSession,
  createTherapyGroupSessionEncounters,
  getTherapyGroupMembers,
  getTherapyGroups,
  getTherapyGroupSessions,
  updateTherapyGroupSessionStatus,
  type TherapyGroup,
  type TherapyGroupMember,
  type TherapyGroupSession,
} from "../../api.ts";
import type { ClinicianOutletContext } from "./ClinicianShell.tsx";

export default function TherapyGroups() {
  const { session } = useOutletContext<ClinicianOutletContext>();
  const [groups, setGroups] = useState<TherapyGroup[]>([]);
  const [name, setName] = useState("");
  const [capacity, setCapacity] = useState(12);
  const [selectedGroup, setSelectedGroup] = useState<TherapyGroup | null>(null);
  const [members, setMembers] = useState<TherapyGroupMember[]>([]);
  const [sessions, setSessions] = useState<TherapyGroupSession[]>([]);
  const [patientId, setPatientId] = useState("");
  const [sessionStart, setSessionStart] = useState("");
  const [durationMinutes, setDurationMinutes] = useState(60);
  const [topic, setTopic] = useState("");
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const load = () =>
    getTherapyGroups(session.sessionId)
      .then((data) => {
        setGroups(data.groups);
        setError("");
      })
      .catch((reason) =>
        setError(
          reason instanceof Error
            ? reason.message
            : "Unable to load therapy groups.",
        ),
      );
  const loadOnSessionChange = useEffectEvent(load);
  useEffect(() => {
    loadOnSessionChange();
  }, [session.sessionId]);
  async function create() {
    if (!name.trim()) return;
    await createTherapyGroup(session.sessionId, { name, capacity });
    setName("");
    load();
  }
  async function select(group: TherapyGroup) {
    setSelectedGroup(group);
    setError("");
    const [nextMembers, nextSessions] = await Promise.all([
      getTherapyGroupMembers(session.sessionId, group.id),
      getTherapyGroupSessions(session.sessionId, group.id),
    ]);
    setMembers(nextMembers);
    setSessions(nextSessions);
  }
  async function addMember() {
    if (!selectedGroup || !patientId.trim()) return;
    try {
      await addTherapyGroupMember(
        session.sessionId,
        selectedGroup.id,
        patientId,
      );
      setPatientId("");
      setMembers(
        await getTherapyGroupMembers(session.sessionId, selectedGroup.id),
      );
    } catch (reason) {
      setError(
        reason instanceof Error ? reason.message : "Unable to add member.",
      );
    }
  }
  async function createSession() {
    if (!selectedGroup || !sessionStart) return;
    try {
      await createTherapyGroupSession(session.sessionId, selectedGroup.id, {
        startsAt: sessionStart,
        durationMinutes,
        topic: topic.trim() || undefined,
      });
      setSessionStart("");
      setTopic("");
      setSessions(
        await getTherapyGroupSessions(session.sessionId, selectedGroup.id),
      );
    } catch (reason) {
      setError(
        reason instanceof Error
          ? reason.message
          : "Unable to schedule session.",
      );
    }
  }
  async function updateSession(
    sessionId: string,
    status: "completed" | "cancelled",
  ) {
    if (!selectedGroup) return;
    try {
      await updateTherapyGroupSessionStatus(
        session.sessionId,
        selectedGroup.id,
        sessionId,
        status,
      );
      setSessions(
        await getTherapyGroupSessions(session.sessionId, selectedGroup.id),
      );
    } catch (reason) {
      setError(
        reason instanceof Error ? reason.message : "Unable to update session.",
      );
    }
  }
  async function createEncounters(groupSessionId: string) {
    if (!selectedGroup) return;
    try {
      const result = await createTherapyGroupSessionEncounters(
        session.sessionId,
        selectedGroup.id,
        groupSessionId,
      );
      const created = result.encounters.filter(
        (entry) => entry.status === "created",
      ).length;
      const existing = result.encounters.filter(
        (entry) => entry.status === "existing",
      ).length;
      const failed = result.encounters.filter(
        (entry) => entry.status === "failed",
      ).length;
      setNotice(
        `Chart encounters: ${created} created, ${existing} already linked${failed ? `, ${failed} failed` : ""}.`,
      );
    } catch (reason) {
      setError(
        reason instanceof Error
          ? reason.message
          : "Unable to create chart encounters.",
      );
    }
  }
  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <h1 className="clinician-page-title">Therapy groups</h1>
        <p className="clinician-page-subtitle">
          Create group programs, assign known patients, and manage group-session
          lifecycle.
        </p>
      </div>
      <section className="cl-card">
        <div className="cl-inline-form">
          <label className="cl-admin-field">
            <span>Group name</span>
            <input
              className="ne-input"
              value={name}
              onChange={(event) => setName(event.target.value)}
            />
          </label>
          <label className="cl-admin-field">
            <span>Capacity</span>
            <input
              className="ne-input"
              type="number"
              min="1"
              max="200"
              value={capacity}
              onChange={(event) => setCapacity(Number(event.target.value))}
            />
          </label>
          <div className="cl-inline-form-actions">
            <button className="cl-btn-primary" type="button" onClick={create}>
              Create group
            </button>
          </div>
        </div>
      </section>
      <section className="cl-card">
        <table className="cl-table">
          <thead>
            <tr>
              <th>Group</th>
              <th>Status</th>
              <th>Capacity</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {groups.map((group) => (
              <tr key={group.id}>
                <td>{group.name}</td>
                <td>{group.status}</td>
                <td>{group.capacity}</td>
                <td>
                  <button
                    className="cl-btn-secondary"
                    type="button"
                    onClick={() => select(group)}
                  >
                    Manage
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {groups.length === 0 && (
          <p className="cl-empty-text">No therapy groups are defined.</p>
        )}
      </section>
      {selectedGroup && (
        <>
          <section className="cl-card">
            <h2 className="cl-card-title">{selectedGroup.name} members</h2>
            <div className="cl-inline-form">
              <label className="cl-admin-field">
                <span>Patient ID or public ID</span>
                <input
                  className="ne-input"
                  value={patientId}
                  onChange={(event) => setPatientId(event.target.value)}
                />
              </label>
              <div className="cl-inline-form-actions">
                <button
                  className="cl-btn-primary"
                  type="button"
                  onClick={addMember}
                >
                  Add member
                </button>
              </div>
            </div>
            {error && <p className="cl-error-text">{error}</p>}
            {notice && <p className="cl-empty-text">{notice}</p>}
            <table className="cl-table">
              <thead>
                <tr>
                  <th>Patient</th>
                  <th>Legacy ID</th>
                  <th>Joined</th>
                </tr>
              </thead>
              <tbody>
                {members.map((member) => (
                  <tr key={member.patientId}>
                    <td>{member.displayName}</td>
                    <td>{member.legacyPid}</td>
                    <td>{new Date(member.joinedAt).toLocaleDateString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            {members.length === 0 && (
              <p className="cl-empty-text">No members have been assigned.</p>
            )}
          </section>
          <section className="cl-card">
            <h2 className="cl-card-title">Sessions</h2>
            <div className="cl-inline-form">
              <label className="cl-admin-field">
                <span>Starts</span>
                <input
                  className="ne-input"
                  type="datetime-local"
                  value={sessionStart}
                  onChange={(event) => setSessionStart(event.target.value)}
                />
              </label>
              <label className="cl-admin-field">
                <span>Minutes</span>
                <input
                  className="ne-input"
                  type="number"
                  min="15"
                  max="480"
                  value={durationMinutes}
                  onChange={(event) =>
                    setDurationMinutes(Number(event.target.value))
                  }
                />
              </label>
              <label className="cl-admin-field">
                <span>Topic</span>
                <input
                  className="ne-input"
                  value={topic}
                  onChange={(event) => setTopic(event.target.value)}
                />
              </label>
              <div className="cl-inline-form-actions">
                <button
                  className="cl-btn-primary"
                  type="button"
                  onClick={createSession}
                >
                  Schedule session
                </button>
              </div>
            </div>
            <table className="cl-table">
              <thead>
                <tr>
                  <th>Starts</th>
                  <th>Topic</th>
                  <th>Minutes</th>
                  <th>Status</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {sessions.map((groupSession) => (
                  <tr key={groupSession.id}>
                    <td>{new Date(groupSession.startsAt).toLocaleString()}</td>
                    <td>{groupSession.topic || "—"}</td>
                    <td>{groupSession.durationMinutes}</td>
                    <td>{groupSession.status}</td>
                    <td>
                      {groupSession.status === "scheduled" && (
                        <>
                          <button
                            className="cl-btn-secondary"
                            type="button"
                            onClick={() =>
                              updateSession(groupSession.id, "completed")
                            }
                          >
                            Complete
                          </button>{" "}
                          <button
                            className="cl-btn-secondary"
                            type="button"
                            onClick={() =>
                              updateSession(groupSession.id, "cancelled")
                            }
                          >
                            Cancel
                          </button>
                        </>
                      )}
                      {groupSession.status === "completed" && (
                        <button
                          className="cl-btn-secondary"
                          type="button"
                          onClick={() => createEncounters(groupSession.id)}
                        >
                          Create chart encounters
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {sessions.length === 0 && (
              <p className="cl-empty-text">No sessions are scheduled.</p>
            )}
          </section>
        </>
      )}
    </div>
  );
}
