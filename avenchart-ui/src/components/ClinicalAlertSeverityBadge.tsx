import { getClinicalAlertSeverity } from "../domain/clinicalAlertSeverity.ts";

export function ClinicalAlertSeverityBadge({
  value,
}: {
  value?: string | null;
}) {
  const presentation = getClinicalAlertSeverity(value);
  return (
    <span
      className={`cl-badge clinical-alert-severity ${presentation.badgeClassName}`}
      data-alert-severity={presentation.severity}
    >
      {presentation.label}
    </span>
  );
}
