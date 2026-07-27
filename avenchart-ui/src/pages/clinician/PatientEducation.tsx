import { useEffect, useState } from "react";
import { useOutletContext } from "react-router-dom";
import { ExternalLink } from "lucide-react";
import {
  getPatientEducationResources,
  searchPatientEducation,
  type PatientEducationResource,
} from "../../api.ts";
import { showToast } from "../../components/Toast.tsx";
import type { ClinicianOutletContext } from "./ClinicianShell.tsx";
export default function PatientEducation() {
  const { session } = useOutletContext<ClinicianOutletContext>();
  const [resources, setResources] = useState<PatientEducationResource[]>([]);
  const [key, setKey] = useState("");
  const [text, setText] = useState("");
  useEffect(() => {
    getPatientEducationResources(session.sessionId)
      .then((x) => {
        setResources(x.resources);
        setKey(x.resources[0]?.key ?? "");
      })
      .catch(() => showToast("Could not load education resources.", "error"));
  }, [session.sessionId]);
  const search = async () => {
    try {
      const x = await searchPatientEducation(session.sessionId, key, text);
      window.open(x.url, "_blank", "noopener,noreferrer");
    } catch {
      showToast("Enter a topic and select an active resource.", "error");
    }
  };
  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <h1 className="clinician-page-title">Patient Education</h1>
        <p className="clinician-page-subtitle">
          Search a configured patient education resource. Results open in a
          separate browser window.
        </p>
      </div>
      <section className="cl-card">
        <div className="cl-admin-form-grid">
          <label className="cl-admin-field">
            <span>Patient resource</span>
            <select
              className="ne-input"
              value={key}
              onChange={(e) => setKey(e.target.value)}
            >
              {resources.map((x) => (
                <option value={x.key} key={x.key}>
                  {x.title}
                </option>
              ))}
            </select>
          </label>
          <label className="cl-admin-field">
            <span>Search topic</span>
            <input
              className="ne-input"
              value={text}
              onChange={(e) => setText(e.target.value)}
              placeholder="e.g. asthma"
            />
          </label>
        </div>
        <button
          className="cl-btn-primary"
          onClick={() => void search()}
          disabled={!key || !text.trim()}
        >
          <ExternalLink size={15} /> Search resource
        </button>
      </section>
    </div>
  );
}
