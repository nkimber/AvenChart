import { useMemo, useState } from "react";
import { useOutletContext } from "react-router-dom";
import { ChartNoAxesCombined, History, Search } from "lucide-react";
import {
  getPatientTrackHistory,
  type PatientTrackHistoryTrack,
} from "../../api.ts";
import { showToast } from "../../components/Toast.tsx";
import type { ClinicianOutletContext } from "./ClinicianShell.tsx";

type NumericPoint = { recordedAt: string; value: number };

function Trend({ label, points }: { label: string; points: NumericPoint[] }) {
  const coordinates = useMemo(() => {
    const sorted = [...points].sort((left, right) =>
      left.recordedAt.localeCompare(right.recordedAt),
    );
    const timestamps = sorted.map((point) =>
      new Date(point.recordedAt).getTime(),
    );
    const values = sorted.map((point) => point.value);
    const minX = Math.min(...timestamps);
    const maxX = Math.max(...timestamps);
    const minY = Math.min(...values);
    const maxY = Math.max(...values);
    const x = (value: number) =>
      minX === maxX ? 160 : 34 + ((value - minX) / (maxX - minX)) * 278;
    const y = (value: number) =>
      minY === maxY ? 55 : 12 + ((maxY - value) / (maxY - minY)) * 86;
    return {
      sorted,
      minY,
      maxY,
      points: sorted
        .map(
          (point) =>
            `${x(new Date(point.recordedAt).getTime())},${y(point.value)}`,
        )
        .join(" "),
      x,
      y,
    };
  }, [points]);

  return (
    <article className="cl-card" style={{ minWidth: 0 }}>
      <h3 className="cl-card-title">{label}</h3>
      <svg
        viewBox="0 0 320 120"
        role="img"
        aria-label={`${label} numeric trend`}
        style={{ width: "100%", minWidth: 260, height: 150, display: "block" }}
      >
        <line
          x1="34"
          y1="12"
          x2="34"
          y2="98"
          stroke="currentColor"
          opacity=".25"
        />
        <line
          x1="34"
          y1="98"
          x2="312"
          y2="98"
          stroke="currentColor"
          opacity=".25"
        />
        <text x="2" y="18" fill="currentColor" opacity=".65" fontSize="10">
          {coordinates.maxY}
        </text>
        <text x="2" y="98" fill="currentColor" opacity=".65" fontSize="10">
          {coordinates.minY}
        </text>
        <polyline
          fill="none"
          stroke="var(--accent, #3076b9)"
          strokeWidth="2.5"
          points={coordinates.points}
        />
        {coordinates.sorted.map((point) => (
          <circle
            key={`${point.recordedAt}-${point.value}`}
            cx={coordinates.x(new Date(point.recordedAt).getTime())}
            cy={coordinates.y(point.value)}
            r="3.5"
            fill="var(--accent, #3076b9)"
          >
            <title>{`${new Date(point.recordedAt).toLocaleString()}: ${point.value}`}</title>
          </circle>
        ))}
        <text x="34" y="115" fill="currentColor" opacity=".65" fontSize="10">
          {new Date(coordinates.sorted[0].recordedAt).toLocaleDateString()}
        </text>
        <text
          x="312"
          y="115"
          textAnchor="end"
          fill="currentColor"
          opacity=".65"
          fontSize="10"
        >
          {new Date(coordinates.sorted.at(-1)!.recordedAt).toLocaleDateString()}
        </text>
      </svg>
      <p className="cl-table-sub">
        {points.length} numeric reading{points.length === 1 ? "" : "s"}; dates
        are measurement timestamps.
      </p>
    </article>
  );
}

export default function PatientTrackHistory() {
  const { session } = useOutletContext<ClinicianOutletContext>();
  const [patientId, setPatientId] = useState("MOD-PAT-0001");
  const [tracks, setTracks] = useState<PatientTrackHistoryTrack[]>([]);
  const [selectedTrackId, setSelectedTrackId] = useState<number>();
  const [loadedPatientId, setLoadedPatientId] = useState<string>();
  const [loading, setLoading] = useState(false);
  const selectedTrack =
    tracks.find((track) => track.trackTypeId === selectedTrackId) ?? tracks[0];
  const numericTrends = useMemo(() => {
    if (!selectedTrack) return new Map<string, NumericPoint[]>();
    const items = new Map<string, NumericPoint[]>();
    for (const encounter of selectedTrack.encounters)
      for (const reading of encounter.readings)
        for (const value of reading.values) {
          const parsed = Number(value.value);
          if (value.value.trim() && Number.isFinite(parsed))
            items.set(value.itemName, [
              ...(items.get(value.itemName) ?? []),
              { recordedAt: reading.recordedAt, value: parsed },
            ]);
        }
    return new Map([...items].filter(([, points]) => points.length > 0));
  }, [selectedTrack]);

  async function loadHistory() {
    const identifier = patientId.trim();
    if (!identifier) {
      showToast("Enter a patient identifier.", "error");
      return;
    }
    setLoading(true);
    try {
      const history = await getPatientTrackHistory(
        session.sessionId,
        identifier,
      );
      setTracks(history.tracks);
      setSelectedTrackId(history.tracks[0]?.trackTypeId);
      setLoadedPatientId(history.patientId);
      if (history.tracks.length === 0)
        showToast("No Track Anything readings are recorded for this patient.");
    } catch {
      setTracks([]);
      setSelectedTrackId(undefined);
      setLoadedPatientId(undefined);
      showToast(
        "Track history could not be loaded. Use the canonical patient ID.",
        "error",
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="clinician-page">
      <div className="clinician-page-header">
        <h1 className="clinician-page-title">Track history</h1>
        <p className="clinician-page-subtitle">
          Review configured Track Anything readings across the patient’s
          encounters, including recorded and correction evidence.
        </p>
      </div>
      <section className="cl-card">
        <div className="cl-actions">
          <input
            className="ne-input"
            value={patientId}
            onChange={(event) => setPatientId(event.target.value)}
            placeholder="Canonical patient ID"
            aria-label="Canonical patient ID"
          />
          <button
            className="cl-btn-primary"
            disabled={loading}
            onClick={() => void loadHistory()}
          >
            <Search size={15} /> Load history
          </button>
        </div>
      </section>
      {loadedPatientId ? (
        <>
          <section className="cl-card">
            <h2 className="cl-card-title">Patient {loadedPatientId}</h2>
            {tracks.length === 0 ? (
              <p className="cl-empty-text">
                No completed Track Anything readings are available for this
                patient.
              </p>
            ) : (
              <div className="cl-actions">
                {tracks.map((track) => (
                  <button
                    key={track.trackTypeId}
                    className={
                      selectedTrack?.trackTypeId === track.trackTypeId
                        ? "cl-btn-primary"
                        : "cl-btn-secondary"
                    }
                    onClick={() => setSelectedTrackId(track.trackTypeId)}
                  >
                    <History size={15} /> {track.trackName}
                  </button>
                ))}
              </div>
            )}
          </section>
          {selectedTrack ? (
            <>
              <section className="cl-card">
                <h2 className="cl-card-title">
                  <ChartNoAxesCombined size={17} /> Numeric trends
                </h2>
                {numericTrends.size === 0 ? (
                  <p className="cl-empty-text">
                    This track has no numeric values to graph. All captured
                    values remain available in the encounter history below.
                  </p>
                ) : (
                  <div
                    style={{
                      display: "grid",
                      gridTemplateColumns:
                        "repeat(auto-fit, minmax(270px, 1fr))",
                      gap: 12,
                    }}
                  >
                    {[...numericTrends].map(([label, points]) => (
                      <Trend key={label} label={label} points={points} />
                    ))}
                  </div>
                )}
              </section>
              <section className="cl-card">
                <h2 className="cl-card-title">
                  <History size={17} /> Encounter history
                </h2>
                {selectedTrack.encounters.map((encounter) => (
                  <article
                    key={encounter.recordId}
                    style={{ marginBottom: 24 }}
                  >
                    <h3 className="cl-card-title">
                      Encounter #{encounter.encounter}{" "}
                      <span className="cl-table-sub">
                        ·{" "}
                        {new Date(
                          `${encounter.encounterDate}T12:00:00`,
                        ).toLocaleDateString()}
                      </span>
                    </h3>
                    <table className="cl-table">
                      <thead>
                        <tr>
                          <th>Measurement time</th>
                          <th>Recorded by</th>
                          <th>Values</th>
                        </tr>
                      </thead>
                      <tbody>
                        {encounter.readings.map((reading) => (
                          <tr key={reading.readingId}>
                            <td>
                              {new Date(reading.recordedAt).toLocaleString()}
                              {reading.updatedAt ? (
                                <p className="cl-table-sub">
                                  Corrected by {reading.updatedBy} ·{" "}
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
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </article>
                ))}
              </section>
            </>
          ) : null}
        </>
      ) : null}
    </div>
  );
}
