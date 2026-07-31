// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import type { Page } from "@playwright/test";
import { expect, test } from "./support/fixtures.ts";
import {
  clinicianRoutes,
  patientChartRoutes,
  portalRoutes,
} from "./support/routes.ts";

async function expectAuthenticatedRoute(
  page: Page,
  path: string,
  loginPath: string,
) {
  await test.step(path, async () => {
    await page.evaluate((nextPath) => {
      window.history.pushState({}, "", nextPath);
      window.dispatchEvent(new PopStateEvent("popstate"));
    }, path);
    await expect(page).toHaveURL(new RegExp(`${path.replaceAll("/", "\\/")}$`));
    await page.waitForTimeout(100);
    await expect(page.locator(".route-loading")).toHaveCount(0, {
      timeout: 15_000,
    });
    await expect(page).not.toHaveURL(
      new RegExp(`${loginPath.replace("/", "\\/")}($|\\?)`),
    );
    await expect(page.locator("#main-content")).toBeVisible({
      timeout: 15_000,
    });
  });
}

async function signOutClinician(page: Page) {
  const openNavigation = page.getByRole("button", { name: "Open navigation" });
  if (await openNavigation.isVisible()) {
    await openNavigation.click();
    await page
      .getByRole("dialog", { name: "Main navigation" })
      .getByRole("button", { name: "Sign out" })
      .click();
  } else {
    await page
      .locator(".clinician-sidebar")
      .getByRole("button", { name: "Sign out" })
      .click();
  }
  await expect(page).toHaveURL(/\/login$/);
}

test.describe("route smoke", () => {
  test.describe.configure({ timeout: 420_000 });

  test("clinician navigation remains usable at every supported width", async ({
    page,
  }, testInfo) => {
    test.skip(testInfo.project.name !== "desktop-chromium");
    await page.goto("/login");
    await page.getByRole("button", { name: "Sign in" }).click();
    await expect(page).toHaveURL(/\/clinician\/dashboard$/, {
      timeout: 15_000,
    });

    for (const width of [320, 390, 768, 1024, 1440]) {
      await test.step(`${width}px`, async () => {
        await page.setViewportSize({ width, height: 900 });
        if (width <= 680) {
          const trigger = page.getByRole("button", { name: "Open navigation" });
          await expect(trigger).toBeVisible();
          await trigger.click();
          const drawer = page.getByRole("dialog", { name: "Main navigation" });
          const navigation = drawer.getByRole("navigation", {
            name: "Mobile navigation",
          });
          const destinations = await navigation
            .getByRole("link")
            .evaluateAll((links) =>
              links.map((link) => new URL((link as HTMLAnchorElement).href).pathname),
            );
          expect(destinations).toEqual(
            clinicianRoutes.filter((path) => path !== "/clinician/encounters/new"),
          );
          await expect(
            drawer.getByRole("link", { name: "Patients" }),
          ).toBeVisible();
          await expect(
            drawer.getByRole("button", { name: "Sign out" }),
          ).toBeVisible();
          await expect(
            drawer.getByRole("button", { name: "Notifications" }),
          ).toBeVisible();
          await expect(drawer.locator(".sidebar-user-name")).not.toHaveText("");
          await expect(drawer.locator(".sidebar-user-role")).not.toHaveText("");
          await expect(
            drawer.getByRole("button", { name: "Close navigation" }),
          ).toBeFocused();
          await expect
            .poll(() => page.evaluate(() => document.body.style.overflow))
            .toBe("hidden");
          await page.keyboard.press("Shift+Tab");
          await expect(
            drawer.getByRole("button", { name: "Sign out" }),
          ).toBeFocused();
          await page.keyboard.press("Tab");
          await expect(
            drawer.getByRole("button", { name: "Close navigation" }),
          ).toBeFocused();
          await page.keyboard.press("Escape");
          await expect(drawer).toBeHidden();
          await expect(trigger).toBeFocused();
          await expect
            .poll(() => page.evaluate(() => document.body.style.overflow))
            .toBe("");
        } else {
          await expect(
            page.getByRole("navigation", { name: "Main navigation" }),
          ).toBeVisible();
          await expect(
            page.getByRole("button", { name: "Sign out" }),
          ).toBeVisible();
        }
      });
    }

    await signOutClinician(page);
  });

  test("clinician navigation remains operable at 200 and 400 percent reflow equivalents", async ({
    page,
  }, testInfo) => {
    test.skip(testInfo.project.name !== "desktop-chromium");
    await page.goto("/login");
    await page.getByRole("button", { name: "Sign in" }).click();
    await expect(page).toHaveURL(/\/clinician\/dashboard$/, {
      timeout: 15_000,
    });

    for (const reflow of [
      { label: "200%", width: 640, height: 450 },
      { label: "400%", width: 320, height: 225 },
    ]) {
      await test.step(reflow.label, async () => {
        await page.setViewportSize({
          width: reflow.width,
          height: reflow.height,
        });
        const trigger = page.getByRole("button", { name: "Open navigation" });
        await expect(trigger).toBeVisible();
        await trigger.click();
        const drawer = page.getByRole("dialog", { name: "Main navigation" });
        await expect(drawer).toBeVisible();
        await expect(drawer.getByRole("link", { name: "Inventory" })).toBeVisible();
        await expect(
          drawer.getByRole("button", { name: "Notifications" }),
        ).toBeVisible();
        await expect(
          drawer.getByRole("button", { name: "Sign out" }),
        ).toBeVisible();
        const drawerBox = await drawer.boundingBox();
        expect(drawerBox).not.toBeNull();
        expect(drawerBox!.x).toBeGreaterThanOrEqual(0);
        expect(drawerBox!.x + drawerBox!.width).toBeLessThanOrEqual(reflow.width);

        await drawer.getByRole("link", { name: "Inventory" }).click();
        await expect(page).toHaveURL(/\/clinician\/inventory$/);
        await expect(drawer).toBeHidden();
        await expect(trigger).toBeVisible();
      });
    }

    await signOutClinician(page);
  });

  test("clinician navigation respects reduced-motion preference", async ({
    page,
  }, testInfo) => {
    test.skip(testInfo.project.name !== "desktop-chromium");
    await page.emulateMedia({ reducedMotion: "reduce" });
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("/login");
    await page.getByRole("button", { name: "Sign in" }).click();
    await expect(page).toHaveURL(/\/clinician\/dashboard$/, {
      timeout: 15_000,
    });

    const trigger = page.getByRole("button", { name: "Open navigation" });
    await trigger.click();
    await expect(
      page.getByRole("dialog", { name: "Main navigation" }),
    ).toBeVisible();

    const motion = await page.evaluate(() => {
      const durationMs = (value: string) =>
        value.split(",").map((part) => {
          const duration = part.trim();
          return duration.endsWith("ms")
            ? Number.parseFloat(duration)
            : Number.parseFloat(duration) * 1_000;
        });
      const styles = [...document.querySelectorAll<HTMLElement>("*")].map(
        (element) => getComputedStyle(element),
      );
      return {
        matches: matchMedia("(prefers-reduced-motion: reduce)").matches,
        scrollBehavior: getComputedStyle(document.documentElement).scrollBehavior,
        maximumAnimationMs: Math.max(
          0,
          ...styles.flatMap((style) => durationMs(style.animationDuration)),
        ),
        maximumTransitionMs: Math.max(
          0,
          ...styles.flatMap((style) => durationMs(style.transitionDuration)),
        ),
      };
    });

    expect(motion.matches).toBe(true);
    expect(motion.scrollBehavior).toBe("auto");
    expect(motion.maximumAnimationMs).toBeLessThanOrEqual(0.01);
    expect(motion.maximumTransitionMs).toBeLessThanOrEqual(0.01);

    await page.keyboard.press("Escape");
    await expect(trigger).toBeFocused();
    await signOutClinician(page);
  });

  test("clinician login and all clinician route groups render without page errors", async ({
    page,
  }) => {
    const pageErrors: Error[] = [];
    page.on("pageerror", (error) => pageErrors.push(error));

    await page.goto("/login");
    await page
      .getByLabel("Username")
      .fill(process.env.MODERN_UI_STAFF_USERNAME ?? "admin");
    await page
      .getByLabel("Password")
      .fill(process.env.MODERN_UI_STAFF_PASSWORD ?? "pass");
    await page.getByRole("button", { name: "Sign in" }).click();
    await expect(page).toHaveURL(/\/clinician\/dashboard$/, {
      timeout: 15_000,
    });

    for (const path of [...clinicianRoutes, ...patientChartRoutes]) {
      await expectAuthenticatedRoute(page, path, "/login");
    }

    expect(
      pageErrors,
      pageErrors.map((error) => error.stack ?? error.message).join("\n\n"),
    ).toEqual([]);

    await signOutClinician(page);
  });

  test("portal login and every portal section render without page errors", async ({
    page,
  }) => {
    const pageErrors: Error[] = [];
    page.on("pageerror", (error) => pageErrors.push(error));

    await page.goto("/portal/login");
    await page
      .getByLabel("Email or username")
      .fill(
        process.env.MODERN_UI_PORTAL_USERNAME ?? "mod-pat-0004@example.test",
      );
    await page
      .getByLabel("Password")
      .fill(process.env.MODERN_UI_PORTAL_PASSWORD ?? "PortalPass207!");
    await page.getByRole("button", { name: "Sign in" }).click();
    await expect(page).toHaveURL(/\/portal\/home$/, { timeout: 15_000 });

    for (const path of portalRoutes) {
      await expectAuthenticatedRoute(page, path, "/portal/login");
    }

    expect(
      pageErrors,
      pageErrors.map((error) => error.stack ?? error.message).join("\n\n"),
    ).toEqual([]);

    await page
      .getByRole("banner")
      .getByRole("button", { name: "Sign out" })
      .click();
    await expect(page).toHaveURL(/\/portal\/login$/);
  });
});
