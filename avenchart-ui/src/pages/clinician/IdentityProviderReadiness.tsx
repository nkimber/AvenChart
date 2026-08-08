// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useEffect, useEffectEvent, useState } from "react";
import {
  getIdentityProviderReadiness,
  type IdentityProviderReadiness as IdentityProviderReadinessResponse,
} from "../../api/identityProvider.ts";

type AsyncState =
  | { status: "loading" }
  | { status: "ready"; data: IdentityProviderReadinessResponse }
  | { status: "error"; message: string };

function readable(value: string) {
  return value.replaceAll("-", " ");
}

function errorMessage(error: unknown) {
  return error instanceof Error
    ? error.message
    : "Identity-provider readiness could not be loaded.";
}

export default function IdentityProviderReadiness({
  sessionId,
}: {
  sessionId: string;
}) {
  const [state, setState] = useState<AsyncState>({ status: "loading" });
  const [reload, setReload] = useState(0);

  const load = useEffectEvent(async (signal: AbortSignal) => {
    setState({ status: "loading" });
    try {
      const data = await getIdentityProviderReadiness(sessionId, signal);
      setState({ status: "ready", data });
    } catch (error) {
      if (signal.aborted) return;
      setState({ status: "error", message: errorMessage(error) });
    }
  });

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [sessionId, reload]);

  return (
    <section
      className="identity-readiness"
      aria-labelledby="identity-readiness-heading"
    >
      <div className="identity-readiness-heading">
        <div>
          <p className="practice-governance-kicker">SEC-02 local foundation</p>
          <h3 id="identity-readiness-heading">Identity-provider readiness</h3>
          <p>
            Inspect the active staff identity adapter, covered identity types,
            service-boundary evidence, and decisions that still block production
            identity.
          </p>
        </div>
        {state.status === "ready" && (
          <code>{state.data.revision}</code>
        )}
      </div>

      {state.status === "loading" && (
        <div className="identity-readiness-message" role="status">
          Loading identity-provider readiness…
        </div>
      )}
      {state.status === "error" && (
        <div className="identity-readiness-message" role="alert">
          <p>{state.message}</p>
          <button
            className="cl-btn-secondary"
            type="button"
            onClick={() => setReload((value) => value + 1)}
          >
            Retry
          </button>
        </div>
      )}

      {state.status === "ready" && (
        <>
          <aside className="identity-readiness-boundary" role="note">
            <strong>Environment boundary:</strong>{" "}
            {state.data.environmentBoundary} Provider, tenant, MFA, device,
            recovery, claim, and facility rules remain owner-gated.
          </aside>

          <div
            className="identity-readiness-counts"
            aria-label="Identity readiness counts"
          >
            <article>
              <strong>{state.data.counts.identityTypes}</strong>
              <span>Identity types</span>
            </article>
            <article>
              <strong>{state.data.counts.routedThroughAdapter}</strong>
              <span>Behind adapter</span>
            </article>
            <article>
              <strong>{state.data.counts.productionApproved}</strong>
              <span>Production approved</span>
            </article>
            <article>
              <strong>{state.data.counts.cryptographicallyValidated}</strong>
              <span>Token validated</span>
            </article>
            <article>
              <strong>{state.data.counts.facilityScoped}</strong>
              <span>Facility scoped</span>
            </article>
            <article>
              <strong>{state.data.counts.emergencyEnabled}</strong>
              <span>Emergency enabled</span>
            </article>
            <article>
              <strong>{state.data.counts.blockingGaps}</strong>
              <span>Production blockers</span>
            </article>
          </div>

          <div className="identity-readiness-grid">
            <section className="identity-readiness-panel">
              <p className="practice-governance-kicker">Active adapter</p>
              <h4>{state.data.adapter.adapterId}</h4>
              <dl>
                <div>
                  <dt>Kind</dt>
                  <dd>{readable(state.data.adapter.adapterKind)}</dd>
                </div>
                <div>
                  <dt>Contract</dt>
                  <dd>
                    <code>{state.data.adapter.interface}</code>
                  </dd>
                </div>
                <div>
                  <dt>Credential source</dt>
                  <dd>{state.data.adapter.credentialSource}</dd>
                </div>
                <div>
                  <dt>Subject key</dt>
                  <dd>{state.data.adapter.subjectKey}</dd>
                </div>
              </dl>
              <div className="identity-chip-row">
                {state.data.adapter.sessionStates.map((item) => (
                  <span key={item}>{readable(item)}</span>
                ))}
              </div>
            </section>

            <section className="identity-readiness-panel">
              <p className="practice-governance-kicker">
                External-token controls
              </p>
              <h4>Not active in the local adapter</h4>
              <ul className="identity-control-flags">
                {[
                  ["Issuer", state.data.adapter.validatesIssuer],
                  ["Audience", state.data.adapter.validatesAudience],
                  ["Signature", state.data.adapter.validatesSignature],
                  ["MFA", state.data.adapter.enforcesMfa],
                  ["Device policy", state.data.adapter.enforcesDevicePolicy],
                  ["Facility scope", state.data.adapter.enforcesFacilityScope],
                ].map(([label, active]) => (
                  <li key={String(label)}>
                    <span>{label}</span>
                    <strong>{active ? "Enforced" : "Not configured"}</strong>
                  </li>
                ))}
              </ul>
            </section>
          </div>

          <div
            className="identity-readiness-table-wrap"
            role="region"
            aria-label="Scrollable identity-type adoption table"
            tabIndex={0}
          >
            <table className="cl-table">
              <caption>Identity-type adoption status</caption>
              <thead>
                <tr>
                  <th>Identity type</th>
                  <th>State</th>
                  <th>Resolution and lifecycle</th>
                  <th>Evidence</th>
                </tr>
              </thead>
              <tbody>
                {state.data.identityTypes.map((item) => (
                  <tr key={item.identityType}>
                    <td>
                      <strong>{item.identityType}</strong>
                    </td>
                    <td>
                      <span
                        className={`cl-badge ${
                          item.routedThroughAdapter
                            ? "cl-badge-green"
                            : "cl-badge-amber"
                        }`}
                      >
                        {readable(item.state)}
                      </span>
                    </td>
                    <td>
                      <p>{item.resolutionPath}</p>
                      <small>{item.lifecycleCoverage}</small>
                    </td>
                    <td>{item.evidence}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="identity-readiness-grid">
            <section className="identity-readiness-panel">
              <p className="practice-governance-kicker">
                Boundary verification
              </p>
              <h4>Expected fail-closed behavior</h4>
              <ul className="identity-verification-list">
                {state.data.verification.map((item) => (
                  <li key={item.scenario}>
                    <div>
                      <strong>{item.scenario}</strong>
                      <span>{readable(item.evidenceState)}</span>
                    </div>
                    <p>{item.expectedResult}</p>
                  </li>
                ))}
              </ul>
            </section>

            <section className="identity-readiness-panel">
              <p className="practice-governance-kicker">
                Owner decisions required
              </p>
              <h4>{state.data.counts.blockingGaps} production blockers</h4>
              <ul className="identity-gap-list">
                {state.data.gaps.map((gap) => (
                  <li key={gap.gapId}>
                    <div>
                      <strong>{readable(gap.gapId)}</strong>
                      <span>{gap.ownerRole}</span>
                    </div>
                    <p>{gap.requiredDecision}</p>
                    <small>Current: {readable(gap.currentState)}</small>
                  </li>
                ))}
              </ul>
            </section>
          </div>
        </>
      )}
    </section>
  );
}
