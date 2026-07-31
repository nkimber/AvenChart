// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { describe, expect, it } from "vitest";
import {
  ApiRequestError,
  isRequestCancellation,
} from "./transport.ts";

describe("isRequestCancellation", () => {
  it("recognizes normalized caller cancellation", () => {
    expect(
      isRequestCancellation(
        new ApiRequestError("cancelled", undefined, undefined, "cancelled"),
      ),
    ).toBe(true);
  });

  it("recognizes native abort errors and rejects unrelated failures", () => {
    expect(
      isRequestCancellation(new DOMException("aborted", "AbortError")),
    ).toBe(true);
    expect(isRequestCancellation(new TypeError("network unavailable"))).toBe(
      false,
    );
  });
});
