// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useEffectEvent, useState } from "react";
import { useOutletContext } from "react-router-dom";
import { Plus, Trash2 } from "lucide-react";
import {
  deleteAddressBookContact,
  getAddressBook,
  saveAddressBookContact,
  type AddressBookEntry,
} from "../../api.ts";
import { showToast } from "../../components/Toast.tsx";
import type { ClinicianOutletContext } from "./ClinicianShell.tsx";
const blank = (): Omit<AddressBookEntry, "id" | "isInternal" | "username"> => ({
  organization: "",
  firstName: "",
  lastName: "",
  specialty: "",
  npi: "",
  type: "external_provider",
  phone: "",
  mobile: "",
  fax: "",
  email: "",
  street: "",
  city: "",
  state: "",
  postalCode: "",
  active: true,
});
export default function AddressBook() {
  const { session } = useOutletContext<ClinicianOutletContext>();
  const [entries, setEntries] = useState<AddressBookEntry[]>([]);
  const [q, setQ] = useState("");
  const [form, setForm] = useState(blank());
  const load = async () => {
    try {
      setEntries((await getAddressBook(session.sessionId, q)).entries);
    } catch {
      showToast("Could not load address book.", "error");
    }
  };
  const loadOnMount = useEffectEvent(load);
  useEffect(() => {
    void loadOnMount();
  }, [session.sessionId]);
  const save = async () => {
    try {
      await saveAddressBookContact(session.sessionId, form);
      setForm(blank());
      await load();
      showToast("External contact saved.", "success");
    } catch {
      showToast("Contact could not be saved.", "error");
    }
  };
  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <h1 className="clinician-page-title">Address Book</h1>
        <p className="clinician-page-subtitle">
          Search active internal staff and external practice contacts. External
          contacts are managed separately from staff accounts.
        </p>
      </div>
      <section className="cl-card">
        <div className="cl-inline-form">
          <input
            className="ne-input"
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="Organization or last name"
          />
          <button className="cl-btn-secondary" onClick={() => void load()}>
            Search
          </button>
        </div>
      </section>
      <section className="cl-card">
        <h2 className="cl-card-title">Add external contact</h2>
        <div className="cl-admin-form-grid">
          {(
            [
              ["organization", "Organization"],
              ["firstName", "First name"],
              ["lastName", "Last name"],
              ["specialty", "Specialty"],
              ["npi", "NPI"],
              ["phone", "Phone"],
              ["email", "Email"],
            ] as const
          ).map(([key, label]) => (
            <label className="cl-admin-field" key={key}>
              <span>{label}</span>
              <input
                className="ne-input"
                value={form[key] ?? ""}
                onChange={(e) => setForm({ ...form, [key]: e.target.value })}
              />
            </label>
          ))}
        </div>
        <button
          className="cl-btn-primary"
          disabled={!form.organization || !form.firstName || !form.lastName}
          onClick={() => void save()}
        >
          <Plus size={15} /> Save contact
        </button>
      </section>
      <section className="cl-card">
        <table className="cl-table">
          <thead>
            <tr>
              <th>Organization</th>
              <th>Name</th>
              <th>Type</th>
              <th>Specialty</th>
              <th>Contact</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {entries.map((e) => (
              <tr key={`${e.isInternal ? "s" : "e"}-${e.id}`}>
                <td>{e.organization || "—"}</td>
                <td>
                  {e.firstName} {e.lastName}
                  {e.isInternal && (
                    <p className="cl-table-sub">Internal: {e.username}</p>
                  )}
                </td>
                <td>{e.type}</td>
                <td>{e.specialty || "—"}</td>
                <td>{e.phone || e.email || "—"}</td>
                <td>
                  {!e.isInternal && (
                    <button
                      className="cl-icon-button cl-icon-button-danger"
                      onClick={() =>
                        void deleteAddressBookContact(
                          session.sessionId,
                          e.id,
                        ).then(load)
                      }
                      aria-label="Delete external contact"
                    >
                      <Trash2 size={15} />
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </div>
  );
}
