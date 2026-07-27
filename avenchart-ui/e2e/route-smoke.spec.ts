import type { Page } from "@playwright/test";
import { expect, test } from "./support/fixtures.ts";

const clinicianRoutes = [
  "/clinician/dashboard",
  "/clinician/schedule",
  "/clinician/calendar",
  "/clinician/flow",
  "/clinician/scheduling",
  "/clinician/patients",
  "/clinician/labs",
  "/clinician/messages",
  "/clinician/office-notes",
  "/clinician/address-book",
  "/clinician/tracks",
  "/clinician/track-entries",
  "/clinician/track-history",
  "/clinician/patient-education",
  "/clinician/recalls",
  "/clinician/batch-communication",
  "/clinician/chart-tracker",
  "/clinician/document-templates",
  "/clinician/duplicate-review",
  "/clinician/renewals",
  "/clinician/reports",
  "/clinician/groups",
  "/clinician/billing",
  "/clinician/inventory",
  "/clinician/admin",
  "/clinician/encounters/new",
] as const;

const patientChartRoutes = [
  "summary",
  "chart",
  "timeline",
  "encounters",
  "documents",
  "labs",
  "appointments",
  "messages",
  "referrals",
  "authorizations",
  "sdoh",
  "print",
].map((section) => `/clinician/patients/MOD-PAT-0004/${section}`);

const portalRoutes = [
  "/portal/home",
  "/portal/messages",
  "/portal/appointments",
  "/portal/records",
  "/portal/account",
] as const;

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
  const signOut = page.getByRole("button", { name: "Sign out" });
  if (!(await signOut.isVisible())) {
    await page.getByRole("button", { name: "Open navigation" }).click();
  }
  await signOut.click();
  await expect(page).toHaveURL(/\/login$/);
}

test.describe("route smoke", () => {
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
          await expect(
            drawer.getByRole("link", { name: "Patients" }),
          ).toBeVisible();
          await expect(
            drawer.getByRole("button", { name: "Sign out" }),
          ).toBeVisible();
          await page.keyboard.press("Escape");
          await expect(drawer).toBeHidden();
          await expect(trigger).toBeFocused();
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
