// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Outlet, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  getPatientPortalMessageThread,
  getPatientPortalMessages,
  type PatientPortalMessageItem,
  type PatientPortalMessageThreadResponse,
  type PatientPortalMessagesResponse,
} from "../../api.ts";
import PortalMessages from "./PortalMessages.tsx";

vi.mock("../../api.ts", async (importOriginal) => {
  const original = await importOriginal<typeof import("../../api.ts")>();
  return {
    ...original,
    getPatientPortalMessages: vi.fn(),
    getPatientPortalMessageThread: vi.fn(),
  };
});

function message(id: string, title: string): PatientPortalMessageItem {
  return {
    id,
    date: "2026-08-21",
    title,
    body: `${title} body`,
    status: "Read",
    senderName: "Care Team",
    recipientName: "Portal Patient",
  };
}

function thread(item: PatientPortalMessageItem): PatientPortalMessageThreadResponse {
  return {
    authenticated: true,
    messageId: item.id,
    threadId: Number(item.id),
    anchorMessage: item,
    threadMessageCount: 1,
    threadMessages: [item],
  };
}

function inbox(items: PatientPortalMessageItem[]): PatientPortalMessagesResponse {
  return {
    authenticated: true,
    messageCount: items.length,
    messages: items,
  };
}

function TestOutlet() {
  return (
    <Outlet
      context={{
        session: {
          sessionId: "portal-session",
          username: "portal.patient",
          portalUsername: "portal-patient",
          displayName: "Portal Patient",
        },
        home: null,
        homeLoading: false,
        markReadOptimistic: vi.fn(),
        refreshHome: vi.fn(),
        signOut: vi.fn(),
      }}
    />
  );
}

function renderPortalMessages() {
  return render(
    <MemoryRouter initialEntries={["/portal/messages"]}>
      <Routes>
        <Route path="/portal" element={<TestOutlet />}>
          <Route path="messages" element={<PortalMessages />} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

describe("PortalMessages", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    Element.prototype.scrollIntoView = vi.fn();
    vi.mocked(getPatientPortalMessages).mockResolvedValue(
      inbox([message("1", "First conversation"), message("2", "Second conversation")]),
    );
  });

  it("does not let a cancelled thread response replace a newer selected conversation", async () => {
    const firstMessage = message("1", "First conversation");
    const secondMessage = message("2", "Second conversation");
    let resolveFirst!: (value: PatientPortalMessageThreadResponse) => void;
    let resolveSecond!: (value: PatientPortalMessageThreadResponse) => void;
    let firstSignal: AbortSignal | undefined;
    const first = new Promise<PatientPortalMessageThreadResponse>((resolve) => {
      resolveFirst = resolve;
    });
    const second = new Promise<PatientPortalMessageThreadResponse>((resolve) => {
      resolveSecond = resolve;
    });
    vi.mocked(getPatientPortalMessageThread).mockImplementation(
      (_sessionId, messageId, signal) => {
        if (messageId === firstMessage.id) {
          firstSignal = signal;
          return first;
        }
        return second;
      },
    );

    renderPortalMessages();

    expect(await screen.findByRole("button", { name: "First conversation, from Care Team" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "First conversation, from Care Team" }));
    await waitFor(() => expect(getPatientPortalMessageThread).toHaveBeenCalledTimes(1));

    fireEvent.click(screen.getByRole("button", { name: "Back to inbox" }));
    expect(firstSignal?.aborted).toBe(true);
    fireEvent.click(screen.getByRole("button", { name: "Second conversation, from Care Team" }));
    await waitFor(() => expect(getPatientPortalMessageThread).toHaveBeenCalledTimes(2));

    resolveSecond(thread(secondMessage));
    expect(await screen.findByRole("heading", { name: "Second conversation" })).toBeInTheDocument();

    resolveFirst(thread(firstMessage));
    await waitFor(() =>
      expect(screen.queryByRole("heading", { name: "First conversation" })).not.toBeInTheDocument(),
    );
    expect(screen.getByRole("heading", { name: "Second conversation" })).toBeInTheDocument();
  });
});
