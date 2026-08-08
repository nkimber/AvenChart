// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { test as base } from "@playwright/test";
import { LifecycleFixtureRegistry } from "../../src/testSupport/lifecycleFixtures.ts";

type AvenChartUiFixtures = {
  lifecycle: LifecycleFixtureRegistry;
};

export const test = base.extend<AvenChartUiFixtures>({
  lifecycle: async ({ browserName: _browserName }, provide, testInfo) => {
    void _browserName
    const resetUrl = process.env.MODERN_UI_RESET_URL;
    const lifecycle = new LifecycleFixtureRegistry(
      resetUrl
        ? async () => {
            const response = await fetch(resetUrl, { method: "POST" });
            if (!response.ok) {
              throw new Error(
                `Clean-demo reset failed with ${response.status}.`,
              );
            }
          }
        : undefined,
    );

    await provide(lifecycle);
    await lifecycle.cleanup();
    await testInfo.attach("lifecycle-fixtures", {
      body: JSON.stringify(lifecycle.records, null, 2),
      contentType: "application/json",
    });
  },
});

export { expect } from "@playwright/test";
