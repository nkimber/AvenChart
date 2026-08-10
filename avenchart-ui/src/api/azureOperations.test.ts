// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  changeAzureOperationsAccessCode,
  getAzureOperationsCapabilities,
  startAzureDeploymentExecution,
  unlockAzureOperations,
} from "./azureOperations.ts";

describe("Azure deployment operations API", () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => vi.unstubAllGlobals());

  it("loads the protected operator capability boundary", async () => {
    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify({ enabled: true, deploymentExecutionEnabled: false }), { status: 200, headers: { "content-type": "application/json" } }));
    const result = await getAzureOperationsCapabilities("staff-session", "operations-token");
    expect(result.enabled).toBe(true);
    expect(result.deploymentExecutionEnabled).toBe(false);
    expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining("/azure-operations/capabilities"), expect.objectContaining({ headers: {
      "X-AvenChart-Session": "staff-session",
      "X-AvenChart-Operations-Access": "operations-token",
    } }));
  });

  it("unlocks without putting the Operations code into a header or URL", async () => {
    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify({ accessToken: "short-lived", expiresAt: "2026-08-09T20:00:00Z", requiresCodeChange: true }), { status: 200, headers: { "content-type": "application/json" } }));
    await unlockAzureOperations("staff-session", "operator-code-value");
    expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining("/access/unlock"), expect.objectContaining({
      method: "POST",
      headers: { "X-AvenChart-Session": "staff-session", "content-type": "application/json" },
      body: JSON.stringify({ code: "operator-code-value" }),
    }));
  });

  it("changes the code through an already unlocked grant", async () => {
    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify({ changed: true, requiresUnlock: true }), { status: 200, headers: { "content-type": "application/json" } }));
    await changeAzureOperationsAccessCode("staff-session", "operations-token", "current-code-value", "replacement-code-value");
    expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining("/access/change-code"), expect.objectContaining({
      method: "POST",
      headers: {
        "X-AvenChart-Session": "staff-session",
        "X-AvenChart-Operations-Access": "operations-token",
        "content-type": "application/json",
      },
      body: JSON.stringify({ currentCode: "current-code-value", newCode: "replacement-code-value" }),
    }));
  });

  it("sends explicit confirmation with a version-pinned deployment", async () => {
    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify({ executionId: "execution-1", status: "queued" }), { status: 202, headers: { "content-type": "application/json" } }));
    await startAzureDeploymentExecution("staff-session", "operations-token", "profile-1", "deploy", 4, "DEPLOY rg-avenchart-demo");
    expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining("/profiles/profile-1/deploy"), expect.objectContaining({
      method: "POST",
      body: JSON.stringify({ expectedProfileVersion: 4, confirmation: "DEPLOY rg-avenchart-demo" }),
    }));
  });
});
