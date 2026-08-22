// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Outlet, MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  getBillingCollectionsWorkQueue,
  getBillingStatementBatch,
} from "../../api.ts";
import type { ClinicianOutletContext } from "./ClinicianShell.tsx";
import BillingWorkspace from "./BillingWorkspace.tsx";

vi.mock("../../api.ts", async (importOriginal) => {
  const original = await importOriginal<typeof import("../../api.ts")>();
  return {
    ...original,
    getBillingCollectionsWorkQueue: vi.fn(),
    getBillingStatementBatch: vi.fn(),
  };
});

const context: ClinicianOutletContext = {
  session: {
    sessionId: "staff-session",
    username: "alice",
    displayName: "Alice Example",
    role: "clinician",
  },
  signOut: vi.fn(),
};

function renderWorkspace() {
  return render(
    <MemoryRouter initialEntries={["/"]}>
      <Routes>
        <Route element={<Outlet context={context} />}>
          <Route path="/" element={<BillingWorkspace />} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

describe("BillingWorkspace collections queue", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(getBillingStatementBatch).mockResolvedValue({
      asOfDate: "2026-08-22",
      candidateCount: 0,
      totalBalanceAmount: 0,
      totalPastDueAmount: 0,
      totalCurrentDueAmount: 0,
      candidates: [],
    });
  });

  it("announces a collections failure and lets the user retry rather than hiding the queue", async () => {
    const user = userEvent.setup();
    vi.mocked(getBillingCollectionsWorkQueue)
      .mockRejectedValueOnce(new Error("Service unavailable."))
      .mockResolvedValueOnce({
        asOfDate: "2026-08-22",
        accountCount: 0,
        highPriorityCount: 0,
        totalBalanceAmount: 0,
        totalPastDueAmount: 0,
        totalOver90Amount: 0,
        items: [],
      });

    renderWorkspace();

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Could not load the collections queue",
    );
    await user.click(
      screen.getByRole("button", { name: "Retry collections queue" }),
    );

    await waitFor(() =>
      expect(
        screen.queryByRole("alert"),
      ).not.toBeInTheDocument(),
    );
    expect(
      screen.getByText("No accounts need collections follow-up."),
    ).toBeInTheDocument();
    expect(getBillingCollectionsWorkQueue).toHaveBeenCalledTimes(2);
  });
});
