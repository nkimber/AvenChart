// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useEffectEvent, useState } from "react";
import { useOutletContext } from "react-router-dom";
import { CheckCircle, RefreshCw } from "lucide-react";
import {
  getDuplicateReviewQueue,
  setDuplicateReviewDisposition,
  type DuplicateReviewItem,
} from "../../api.ts";
import { showToast } from "../../components/Toast.tsx";
import type { ClinicianOutletContext } from "./ClinicianShell.tsx";
export default function DuplicateReview() {
  const { session } = useOutletContext<ClinicianOutletContext>();
  const [items, setItems] = useState<DuplicateReviewItem[]>([]);
  const load = async () => {
    try {
      setItems((await getDuplicateReviewQueue(session.sessionId)).items);
    } catch {
      showToast("Could not load duplicate review candidates.", "error");
    }
  };
  const loadOnMount = useEffectEvent(load);
  useEffect(() => {
    void loadOnMount();
  }, [session.sessionId]);
  const set = async (x: DuplicateReviewItem, status: string) => {
    try {
      await setDuplicateReviewDisposition(session.sessionId, {
        targetPatientId: x.targetPatientId,
        sourcePatientId: x.sourcePatientId,
        status,
      });
      await load();
      showToast("Duplicate review disposition recorded.", "success");
    } catch {
      showToast("Could not record duplicate review.", "error");
    }
  };
  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <h1 className="clinician-page-title">Duplicate Patient Review</h1>
        <p className="clinician-page-subtitle">
          Review candidates before opening the constrained merge workflow.
          Marking unique never changes patient records.
        </p>
      </div>
      <section className="cl-card">
        <button className="cl-btn-secondary" onClick={() => void load()}>
          <RefreshCw size={15} /> Refresh queue
        </button>
        <table className="cl-table">
          <thead>
            <tr>
              <th>Potential target</th>
              <th>Potential duplicate</th>
              <th>Evidence</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {items.map((x) => (
              <tr key={`${x.targetPatientId}:${x.sourcePatientId}`}>
                <td>
                  {x.targetDisplayName}
                  <p className="cl-table-sub">{x.targetPatientId}</p>
                </td>
                <td>
                  {x.sourceDisplayName}
                  <p className="cl-table-sub">{x.sourcePatientId}</p>
                </td>
                <td>
                  {x.matchScore}% · {x.matchReasons.join(", ")}
                  <p className="cl-table-sub">DOB {x.dateOfBirth}</p>
                </td>
                <td>{x.status}</td>
                <td>
                  <button
                    className="cl-btn-secondary"
                    onClick={() => void set(x, "unique")}
                    disabled={x.status === "unique"}
                  >
                    <CheckCircle size={15} /> Mark unique
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </div>
  );
}
