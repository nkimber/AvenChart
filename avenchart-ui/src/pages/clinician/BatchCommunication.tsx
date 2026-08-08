// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useState } from "react";
import { useOutletContext } from "react-router-dom";
import { Download, Mail, Phone, Search } from "lucide-react";
import {
  createBatchCommunicationCampaign,
  downloadBatchCommunicationCampaign,
  previewBatchCommunication,
  type BatchCommunicationFilter,
  type BatchCommunicationRecipient,
} from "../../api.ts";
import { showToast } from "../../components/Toast.tsx";
import type { ClinicianOutletContext } from "./ClinicianShell.tsx";
const empty: BatchCommunicationFilter = {
  processType: "csv",
  gender: "any",
  requireConsent: false,
  sortBy: "lastName",
};
export default function BatchCommunication() {
  const { session } = useOutletContext<ClinicianOutletContext>();
  const [filter, setFilter] = useState<BatchCommunicationFilter>(empty);
  const [email, setEmail] = useState({
    sender: "",
    subject: "",
    body: "Dear ***NAME***,",
  });
  const [recipients, setRecipients] = useState<BatchCommunicationRecipient[]>(
    [],
  );
  const [campaign, setCampaign] = useState<string>();
  const preview = async () => {
    try {
      const r = await previewBatchCommunication(session.sessionId, filter);
      setRecipients(r.recipients);
      showToast(`${r.recipients.length} recipient(s) selected.`, "success");
    } catch {
      showToast("Could not validate the campaign filters.", "error");
    }
  };
  const create = async () => {
    try {
      const r = await createBatchCommunicationCampaign(session.sessionId, {
        filter,
        emailSender: email.sender,
        emailSubject: email.subject,
        emailBody: email.body,
      });
      setCampaign(r.campaign.id);
      setRecipients(r.recipients);
      showToast("Local campaign output was generated.", "success");
    } catch {
      showToast("Add all email fields when generating email output.", "error");
    }
  };
  const download = async () => {
    if (!campaign) return;
    try {
      const url = URL.createObjectURL(
        await downloadBatchCommunicationCampaign(session.sessionId, campaign),
      );
      const link = document.createElement("a");
      link.href = url;
      link.download = `batch-communication-${campaign}.csv`;
      link.click();
      URL.revokeObjectURL(url);
    } catch {
      showToast("Campaign output could not be downloaded.", "error");
    }
  };
  const set = (key: keyof BatchCommunicationFilter, value: string | boolean) =>
    setFilter({ ...filter, [key]: value || undefined });
  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <h1 className="clinician-page-title">Batch Communication</h1>
        <p className="clinician-page-subtitle">
          Create local CSV, phone-list, or personalized email output from a
          filtered patient set. Nothing is sent externally.
        </p>
      </div>
      <section className="cl-card">
        <div className="cl-admin-form-grid">
          <label className="cl-admin-field">
            <span>Process</span>
            <select
              className="ne-input"
              value={filter.processType}
              onChange={(e) => set("processType", e.target.value)}
            >
              <option value="csv">Download CSV</option>
              <option value="email">Generate email output</option>
              <option value="phone">Phone call list</option>
            </select>
          </label>
          <label className="cl-admin-field">
            <span>Gender</span>
            <select
              className="ne-input"
              value={filter.gender}
              onChange={(e) => set("gender", e.target.value)}
            >
              <option value="any">Any</option>
              <option value="male">Male</option>
              <option value="female">Female</option>
            </select>
          </label>
          <label className="cl-admin-field">
            <span>Sort by</span>
            <select
              className="ne-input"
              value={filter.sortBy}
              onChange={(e) => set("sortBy", e.target.value)}
            >
              <option value="lastName">Last name</option>
              <option value="zipCode">Zip code</option>
              <option value="appointmentDate">Appointment date</option>
            </select>
          </label>
          <label className="cl-admin-field">
            <span>Age from</span>
            <input
              className="ne-input"
              type="number"
              min="0"
              max="130"
              onChange={(e) => set("ageFrom", e.target.value)}
            />
          </label>
          <label className="cl-admin-field">
            <span>Age to</span>
            <input
              className="ne-input"
              type="number"
              min="0"
              max="130"
              onChange={(e) => set("ageTo", e.target.value)}
            />
          </label>
          <label className="cl-admin-field">
            <span>Appointment from</span>
            <input
              className="ne-input"
              type="date"
              onChange={(e) => set("appointmentStart", e.target.value)}
            />
          </label>
          <label className="cl-admin-field">
            <span>Appointment to</span>
            <input
              className="ne-input"
              type="date"
              onChange={(e) => set("appointmentEnd", e.target.value)}
            />
          </label>
          <label className="cl-admin-field">
            <span>Seen since</span>
            <input
              className="ne-input"
              type="date"
              onChange={(e) => set("seenSince", e.target.value)}
            />
          </label>
          <label className="cl-admin-field">
            <span>Seen before</span>
            <input
              className="ne-input"
              type="date"
              onChange={(e) => set("seenBefore", e.target.value)}
            />
          </label>
          <label className="cl-admin-field">
            <span>
              <input
                type="checkbox"
                checked={filter.requireConsent}
                onChange={(e) => set("requireConsent", e.target.checked)}
              />{" "}
              Require email consent
            </span>
          </label>
        </div>
        {filter.processType === "email" && (
          <div className="cl-admin-form-grid">
            <label className="cl-admin-field">
              <span>Email sender</span>
              <input
                className="ne-input"
                value={email.sender}
                onChange={(e) => setEmail({ ...email, sender: e.target.value })}
              />
            </label>
            <label className="cl-admin-field">
              <span>Email subject</span>
              <input
                className="ne-input"
                value={email.subject}
                onChange={(e) =>
                  setEmail({ ...email, subject: e.target.value })
                }
              />
            </label>
            <label className="cl-admin-field">
              <span>Email body (supports ***NAME***)</span>
              <textarea
                className="ne-input"
                value={email.body}
                onChange={(e) => setEmail({ ...email, body: e.target.value })}
              />
            </label>
          </div>
        )}
        <div className="cl-actions">
          <button className="cl-btn-secondary" onClick={() => void preview()}>
            <Search size={15} /> Preview recipients
          </button>
          <button className="cl-btn-primary" onClick={() => void create()}>
            {filter.processType === "phone" ? (
              <Phone size={15} />
            ) : filter.processType === "email" ? (
              <Mail size={15} />
            ) : (
              <Download size={15} />
            )}{" "}
            Generate local output
          </button>
          {campaign && (
            <button
              className="cl-btn-secondary"
              onClick={() => void download()}
            >
              <Download size={15} /> Download CSV
            </button>
          )}
        </div>
      </section>
      <section className="cl-card">
        <h2 className="cl-section-title">
          Recipient preview ({recipients.length})
        </h2>
        <table className="cl-table">
          <thead>
            <tr>
              <th>Patient</th>
              <th>Email</th>
              <th>Phone</th>
              <th>Next appointment</th>
            </tr>
          </thead>
          <tbody>
            {recipients.slice(0, 100).map((r) => (
              <tr key={r.patientId}>
                <td>
                  {r.displayName}
                  <p className="cl-table-sub">{r.patientId}</p>
                </td>
                <td>{r.email || "—"}</td>
                <td>{r.phoneCell || r.phoneHome || "—"}</td>
                <td>{r.nextAppointmentDate || "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </div>
  );
}
