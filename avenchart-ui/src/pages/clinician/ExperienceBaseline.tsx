// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { useCallback, useEffect, useMemo, useState } from "react";
import { useOutletContext } from "react-router-dom";
import {
  Activity,
  BarChart3,
  Gauge,
  LockKeyhole,
  MonitorSmartphone,
  ShieldCheck,
} from "lucide-react";
import {
  getExperienceBaseline,
  type ExperienceBaseline as ExperienceBaselineResponse,
  type ExperienceTask,
} from "../../api/experienceBaseline.ts";
import type { ClinicianOutletContext } from "./ClinicianShell.tsx";

function titleCase(value: string) {
  return value
    .split("-")
    .map((part) => `${part.charAt(0).toUpperCase()}${part.slice(1)}`)
    .join(" ");
}

function criterionStateLabel(value: string) {
  if (value === "met-local") return "Met locally";
  if (value === "measured-local") return "Measured locally";
  if (value === "owner-gated") return "Owner gated";
  return titleCase(value);
}

function riskLabel(value: string) {
  return value === "safety-critical" ? "Safety critical" : titleCase(value);
}

export default function ExperienceBaseline() {
  const { session } = useOutletContext<ClinicianOutletContext>();
  const [baseline, setBaseline] = useState<ExperienceBaselineResponse | null>(
    null,
  );
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [retry, setRetry] = useState(0);
  const [category, setCategory] = useState("all");
  const [criterionState, setCriterionState] = useState("all");
  const [selectedTaskId, setSelectedTaskId] = useState<string | null>(null);

  const load = useCallback(
    async (signal?: AbortSignal) => {
      setLoading(true);
      setError("");
      try {
        const response = await getExperienceBaseline(
          session.sessionId,
          signal,
        );
        setBaseline(response);
        setSelectedTaskId((current) =>
          current && response.tasks.some((task) => task.id === current)
            ? current
            : (response.tasks[0]?.id ?? null),
        );
      } catch (reason) {
        if (signal?.aborted) return;
        setError(
          reason instanceof Error
            ? reason.message
            : "Unable to load the experience baseline.",
        );
      } finally {
        if (!signal?.aborted) setLoading(false);
      }
    },
    [session.sessionId],
  );

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load, retry]);

  const categories = useMemo(
    () =>
      Array.from(
        new Set(baseline?.criteria.map((criterion) => criterion.category)),
      ).sort(),
    [baseline],
  );

  const filteredCriteria = useMemo(
    () =>
      baseline?.criteria.filter(
        (criterion) =>
          (category === "all" || criterion.category === category) &&
          (criterionState === "all" ||
            criterion.lifecycleState === criterionState),
      ) ?? [],
    [baseline, category, criterionState],
  );

  const selectedTask = useMemo<ExperienceTask | null>(
    () =>
      baseline?.tasks.find((task) => task.id === selectedTaskId) ?? null,
    [baseline, selectedTaskId],
  );

  return (
    <div className="cl-page experience-baseline-page">
      <header className="cl-page-header experience-baseline-header">
        <div>
          <p className="cl-eyebrow">UX-01 / Acceptance foundation</p>
          <h1>Experience baseline</h1>
          <p>
            Inspect the proposed role, task, browser, accessibility,
            performance, safety, and privacy criteria for the Modern UI.
          </p>
        </div>
        {baseline && (
          <span className="experience-revision">
            {baseline.revision}
          </span>
        )}
      </header>

      <section
        className="experience-boundary"
        aria-labelledby="experience-boundary-title"
      >
        <LockKeyhole size={20} aria-hidden="true" />
        <div>
          <h2 id="experience-boundary-title">Proposed local baseline</h2>
          <p>
            This registry describes synthetic evidence and owner-gated gaps. It
            is not production acceptance, user research, an approved analytics
            policy, or permission to collect telemetry. Analytics collection is
            disabled.
          </p>
        </div>
      </section>

      {loading && !baseline && (
        <div className="experience-loading" role="status">
          <span className="sr-only">Loading experience baseline</span>
          <div className="skeleton-row" aria-hidden="true" />
          <div className="skeleton-row" aria-hidden="true" />
          <div className="skeleton-row" aria-hidden="true" />
        </div>
      )}

      {error && !baseline && (
        <div className="error-banner experience-error" role="alert">
          <span>{error}</span>
          <button
            className="cl-btn-secondary"
            type="button"
            onClick={() => setRetry((value) => value + 1)}
          >
            Reload baseline
          </button>
        </div>
      )}

      {baseline && (
        <>
          {error && (
            <div className="error-banner experience-error" role="alert">
              <span>{error}</span>
              <button
                className="cl-btn-secondary"
                type="button"
                onClick={() => setRetry((value) => value + 1)}
              >
                Reload baseline
              </button>
            </div>
          )}

          <section
            className="experience-metrics"
            aria-label="Experience baseline summary"
          >
            <article>
              <Activity size={18} aria-hidden="true" />
              <span>Critical tasks</span>
              <strong>{baseline.counts.tasks}</strong>
            </article>
            <article>
              <MonitorSmartphone size={18} aria-hidden="true" />
              <span>Environment profiles</span>
              <strong>{baseline.counts.environments}</strong>
            </article>
            <article>
              <ShieldCheck size={18} aria-hidden="true" />
              <span>Met or measured locally</span>
              <strong>
                {baseline.counts.metLocal + baseline.counts.measuredLocal}
              </strong>
            </article>
            <article>
              <Gauge size={18} aria-hidden="true" />
              <span>Owner-gated gaps</span>
              <strong>{baseline.counts.gaps}</strong>
            </article>
          </section>

          <section
            className="cl-card experience-criteria-card"
            aria-labelledby="experience-criteria-title"
          >
            <div className="cl-card-header experience-section-heading">
              <div>
                <p className="cl-eyebrow">Measurable criteria</p>
                <h2 id="experience-criteria-title">Acceptance criteria</h2>
              </div>
              <span>
                {filteredCriteria.length} of {baseline.criteria.length}
              </span>
            </div>

            <div className="experience-filter-grid">
              <label>
                Category
                <select
                  className="ne-input"
                  value={category}
                  onChange={(event) => setCategory(event.target.value)}
                >
                  <option value="all">All categories</option>
                  {categories.map((option) => (
                    <option key={option} value={option}>
                      {titleCase(option)}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Lifecycle state
                <select
                  className="ne-input"
                  value={criterionState}
                  onChange={(event) => setCriterionState(event.target.value)}
                >
                  <option value="all">All states</option>
                  <option value="met-local">Met locally</option>
                  <option value="measured-local">Measured locally</option>
                  <option value="proposed">Proposed</option>
                  <option value="owner-gated">Owner gated</option>
                </select>
              </label>
            </div>

            {filteredCriteria.length === 0 ? (
              <p className="cl-empty-text">
                No criteria match the selected category and lifecycle state.
              </p>
            ) : (
              <ul className="experience-criteria-list">
                {filteredCriteria.map((criterion) => (
                  <li key={criterion.id}>
                    <div className="experience-criterion-heading">
                      <div>
                        <span>{titleCase(criterion.category)}</span>
                        <h3>{criterion.label}</h3>
                      </div>
                      <span
                        className={`experience-state experience-state-${criterion.lifecycleState}`}
                      >
                        {criterionStateLabel(criterion.lifecycleState)}
                      </span>
                    </div>
                    <dl>
                      <div>
                        <dt>Target</dt>
                        <dd>{criterion.target}</dd>
                      </div>
                      <div>
                        <dt>Current measurement</dt>
                        <dd>{criterion.measurement}</dd>
                      </div>
                      <div>
                        <dt>Evidence</dt>
                        <dd>{criterion.evidence}</dd>
                      </div>
                      <div>
                        <dt>Owner role</dt>
                        <dd>{criterion.ownerRole}</dd>
                      </div>
                    </dl>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section
            className="cl-card experience-task-card"
            aria-labelledby="experience-task-title"
          >
            <div className="cl-card-header experience-section-heading">
              <div>
                <p className="cl-eyebrow">Role and task inventory</p>
                <h2 id="experience-task-title">Critical tasks</h2>
              </div>
              <span>{baseline.counts.tasks} catalogued</span>
            </div>

            <div className="experience-task-workspace">
              <div
                className="experience-task-list"
                aria-label="Critical task list"
              >
                {baseline.tasks.map((task) => (
                  <button
                    type="button"
                    key={task.id}
                    className={`experience-task-button${
                      task.id === selectedTaskId ? " is-selected" : ""
                    }`}
                    onClick={() => setSelectedTaskId(task.id)}
                    aria-pressed={task.id === selectedTaskId}
                  >
                    <span>{task.label}</span>
                    <small>{riskLabel(task.risk)}</small>
                  </button>
                ))}
              </div>

              {selectedTask && (
                <article
                  className="experience-task-detail"
                  aria-labelledby="experience-selected-task"
                >
                  <div>
                    <span
                      className={`experience-risk experience-risk-${selectedTask.risk}`}
                    >
                      {riskLabel(selectedTask.risk)}
                    </span>
                    <h3 id="experience-selected-task">
                      {selectedTask.label}
                    </h3>
                    <code>{selectedTask.route}</code>
                  </div>
                  <p>
                    <strong>Roles:</strong>{" "}
                    {selectedTask.roleIds
                      .map(
                        (roleId) =>
                          baseline.roles.find((role) => role.id === roleId)
                            ?.label ?? roleId,
                      )
                      .join(", ")}
                  </p>
                  <dl>
                    <div>
                      <dt>Success</dt>
                      <dd>{selectedTask.successCriterion}</dd>
                    </div>
                    <div>
                      <dt>Error</dt>
                      <dd>{selectedTask.errorCriterion}</dd>
                    </div>
                    <div>
                      <dt>Recovery</dt>
                      <dd>{selectedTask.recoveryCriterion}</dd>
                    </div>
                    <div>
                      <dt>Accessibility</dt>
                      <dd>{selectedTask.accessibilityCriterion}</dd>
                    </div>
                    <div>
                      <dt>Performance</dt>
                      <dd>{selectedTask.performanceCriterion}</dd>
                    </div>
                    <div>
                      <dt>Synthetic evidence</dt>
                      <dd>{selectedTask.evidence}</dd>
                    </div>
                  </dl>
                </article>
              )}
            </div>
          </section>

          <section
            className="cl-card experience-environment-card"
            aria-labelledby="experience-environment-title"
          >
            <div className="cl-card-header experience-section-heading">
              <div>
                <p className="cl-eyebrow">Supported local matrix</p>
                <h2 id="experience-environment-title">
                  Browser, device, and viewport evidence
                </h2>
              </div>
              <span>{baseline.counts.environments} profiles</span>
            </div>
            <ul className="experience-environment-list">
              {baseline.environments.map((environment) => (
                <li key={environment.id}>
                  <div>
                    <strong>
                      {environment.browser} / {environment.deviceClass}
                    </strong>
                    <span>{environment.viewport}</span>
                  </div>
                  <p>{environment.evidence}</p>
                  <div className="experience-chip-row">
                    {environment.testLevels.map((level) => (
                      <span key={level}>{titleCase(level)}</span>
                    ))}
                  </div>
                </li>
              ))}
            </ul>
          </section>

          <div className="experience-lower-grid">
            <section
              className="cl-card experience-analytics-card"
              aria-labelledby="experience-analytics-title"
            >
              <div className="cl-card-header experience-section-heading">
                <div>
                  <p className="cl-eyebrow">Privacy boundary</p>
                  <h2 id="experience-analytics-title">
                    Analytics vocabulary
                  </h2>
                </div>
                <span className="experience-collection-off">
                  Collection off
                </span>
              </div>
              <p>
                These event names are definitions only. All{" "}
                {baseline.counts.analyticsEvents} have collection disabled
                until privacy and product owners approve purpose, retention,
                access, and consent.
              </p>
              <ul className="experience-analytics-list">
                {baseline.analyticsEvents.map((event) => (
                  <li key={event.eventId}>
                    <code>{event.eventId}</code>
                    <p>{event.purpose}</p>
                    <span>{event.allowedProperties.join(" / ")}</span>
                  </li>
                ))}
              </ul>
              <details className="experience-forbidden-properties">
                <summary>Forbidden analytics properties</summary>
                <div className="experience-chip-row">
                  {baseline.forbiddenAnalyticsProperties.map((property) => (
                    <span key={property}>{property}</span>
                  ))}
                </div>
              </details>
            </section>

            <section
              className="cl-card experience-gap-card"
              aria-labelledby="experience-gap-title"
            >
              <div className="cl-card-header experience-section-heading">
                <div>
                  <p className="cl-eyebrow">Production blockers</p>
                  <h2 id="experience-gap-title">Open decisions and evidence</h2>
                </div>
                <span>{baseline.counts.gaps} open</span>
              </div>
              <ul className="experience-gap-list">
                {baseline.gaps.map((gap) => (
                  <li key={gap.id}>
                    <div>
                      <span>{titleCase(gap.area)}</span>
                      <strong>{titleCase(gap.state)}</strong>
                    </div>
                    <p>{gap.requiredDecision}</p>
                    <small>{gap.ownerRole}</small>
                  </li>
                ))}
              </ul>
            </section>
          </div>

          <section
            className="experience-standard"
            aria-label="Accessibility standard status"
          >
            <BarChart3 size={18} aria-hidden="true" />
            <div>
              <strong>{baseline.accessibilityStandard}</strong>
              <span>{baseline.scope}</span>
            </div>
          </section>
        </>
      )}
    </div>
  );
}
