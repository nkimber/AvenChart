import { describe, expect, it } from "vitest";
import { getClinicalAlertSeverity } from "./clinicalAlertSeverity.ts";

describe("clinical alert severity model", () => {
  it.each([
    ["info", "info", "Information alert", "cl-badge-blue"],
    ["WARNING", "warning", "Warning alert", "cl-badge-amber"],
    [" critical ", "critical", "Critical alert", "cl-badge-red"],
  ])(
    "maps %s to color-independent %s semantics",
    (value, severity, label, badgeClassName) => {
      expect(getClinicalAlertSeverity(value)).toEqual({
        severity,
        label,
        badgeClassName,
      });
    },
  );

  it.each([undefined, null, "", "unexpected"])(
    "keeps unknown severity %s visible without exposing an unexplained code",
    (value) => {
      expect(getClinicalAlertSeverity(value)).toEqual({
        severity: "unknown",
        label: "Review alert",
        badgeClassName: "cl-badge-muted",
      });
    },
  );
});
