import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import {
  clinicianRoutes,
  patientChartRoutes,
  portalRoutes,
} from "./support/routes.ts";

type AccessibilityFinding = {
  page: string;
  rule: string;
  help: string;
  targets: string[];
};

async function findSeriousAccessibilityViolations(
  page: Page,
  label: string,
): Promise<AccessibilityFinding[]> {
  const results = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
    .analyze();
  return results.violations
    .filter(
      ({ impact }) => impact === "serious" || impact === "critical",
    )
    .map(({ id, help, nodes }) => ({
      page: label,
      rule: id,
      help,
      targets: nodes.map(({ target }) => target.join(" ")),
    }));
}

function expectNoSeriousAccessibilityViolations(
  violations: AccessibilityFinding[],
) {
  expect(
    violations,
    violations
      .map(
        ({ page, rule, help, targets }) =>
          `${page}\n${rule}: ${help}\n${targets.map((target) => `  ${target}`).join("\n")}`,
      )
      .join("\n\n"),
  ).toEqual([]);
}

async function signInClinician(page: Page) {
  await page.goto("/login");
  await page
    .getByLabel("Username")
    .fill(process.env.MODERN_UI_STAFF_USERNAME ?? "admin");
  await page
    .getByLabel("Password")
    .fill(process.env.MODERN_UI_STAFF_PASSWORD ?? "pass");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/clinician\/dashboard$/);
}

async function signInPortal(page: Page) {
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
  await expect(page).toHaveURL(/\/portal\/home$/);
}

async function navigateWithinApplication(page: Page, path: string) {
  const loginPath = path.startsWith("/portal") ? "/portal/login" : "/login";
  const signIn = path.startsWith("/portal") ? signInPortal : signInClinician;

  if (new URL(page.url()).pathname === loginPath) {
    await signIn(page);
  }

  const navigate = async () => {
    await page.evaluate((nextPath) => {
      window.history.pushState({}, "", nextPath);
      window.dispatchEvent(new PopStateEvent("popstate"));
    }, path);
    await page.waitForTimeout(150);
  };

  await navigate();
  if (new URL(page.url()).pathname === loginPath) {
    await signIn(page);
    await navigate();
  }

  await expect(page).toHaveURL(new RegExp(`${path.replaceAll("/", "\\/")}$`));
  await expect(page.locator(".route-loading")).toHaveCount(0, {
    timeout: 15_000,
  });
  await expect(page.locator("#main-content"), path).toBeVisible({
    timeout: 15_000,
  });
}

test.describe("accessibility gate", () => {
  test("public entry and login surfaces have no serious WCAG violations", async ({
    page,
  }) => {
    const violations: AccessibilityFinding[] = [];
    for (const path of ["/", "/login", "/portal/login"]) {
      await page.goto(path);
      await expect(page.locator("body")).toBeVisible();
      violations.push(
        ...(await findSeriousAccessibilityViolations(page, path)),
      );
    }
    expectNoSeriousAccessibilityViolations(violations);
  });

  test("representative clinician workspaces have no serious WCAG violations", async ({
    page,
  }) => {
    await signInClinician(page);
    const violations: AccessibilityFinding[] = [];
    for (const path of [...clinicianRoutes, ...patientChartRoutes]) {
      await navigateWithinApplication(page, path);
      violations.push(
        ...(await findSeriousAccessibilityViolations(page, path)),
      );
    }
    await navigateWithinApplication(
      page,
      "/clinician/patients/MOD-PAT-0001/documents",
    );
    await page.getByRole("button", { name: "Add document" }).click();
    await expect(
      page.getByRole("heading", { name: "Choose how to file it" }),
    ).toBeVisible();
    violations.push(
      ...(await findSeriousAccessibilityViolations(
        page,
        "/clinician/patients/MOD-PAT-0001/documents#intake",
      )),
    );
    await page.getByRole("button", { name: "Close intake" }).click();
    await page.getByRole("button", { name: "Edit filing" }).first().click();
    await page.getByRole("button", { name: "Filing history" }).first().click();
    await expect(page.getByLabel("Change reason *")).toBeVisible();
    await expect(
      page.getByRole("heading", { name: "Filing history" }),
    ).toBeVisible();
    violations.push(
      ...(await findSeriousAccessibilityViolations(
        page,
        "/clinician/patients/MOD-PAT-0001/documents#metadata",
      )),
    );
    await page.getByRole("button", { name: "Close edit" }).first().click();
    await page.getByRole("button", { name: "Filing history" }).first().click();
    await page
      .getByRole("button", { name: "Content versions" })
      .first()
      .click();
    await page
      .getByRole("button", { name: "Replace content" })
      .first()
      .click();
    await expect(
      page.getByRole("heading", {
        name: "Create the next immutable version",
      }),
    ).toBeVisible();
    await expect(
      page.getByRole("heading", { name: "Content version history" }),
    ).toBeVisible();
    violations.push(
      ...(await findSeriousAccessibilityViolations(
        page,
        "/clinician/patients/MOD-PAT-0001/documents#content-versions",
      )),
    );
    await navigateWithinApplication(page, "/clinician/patients/new");
    await page.getByLabel("Chart number").fill("TMP-PAT-REG-AXE");
    await page.getByLabel("First name").fill("Nora");
    await page.getByLabel("Last name").fill("Kim");
    await page.getByLabel("Sex").selectOption("Female");
    await page.getByLabel("Date of birth").fill("2002-05-05");
    await page.getByLabel("Home phone").fill("(619) 555-1004");
    await page
      .getByLabel("Email", { exact: true })
      .fill("mod-pat-0004@example.test");
    await page
      .getByRole("button", { name: "Review and register" })
      .click();
    await expect(
      page.getByText("Review possible existing records before continuing."),
    ).toBeVisible({ timeout: 15_000 });
    violations.push(
      ...(await findSeriousAccessibilityViolations(
        page,
        "/clinician/patients/new#duplicate-check",
      )),
    );
    await navigateWithinApplication(page, "/clinician/renewals");
    await page.getByLabel("Patient name or ID").fill("MOD-PAT-0004");
    await page
      .getByRole("button", { name: "Apply patient scope" })
      .click();
    await page
      .getByRole("button", { name: "All active", exact: true })
      .click();
    const routeButton = page
      .locator("button:not([disabled])")
      .filter({ hasText: /Record pharmacy|Change local route/ })
      .first();
    await expect(routeButton).toBeVisible({ timeout: 15_000 });
    await routeButton.click();
    await expect(page.getByLabel("Local pharmacy")).toBeVisible();
    violations.push(
      ...(await findSeriousAccessibilityViolations(
        page,
        "/clinician/renewals#local-pharmacy-route",
      )),
    );
    await page.getByRole("button", { name: "Cancel" }).click();
    const editButton = page
      .locator("button:not([disabled])")
      .filter({ hasText: "Edit prescription" })
      .first();
    await expect(editButton).toBeVisible();
    await editButton.click();
    await expect(page.getByLabel("Edit reason")).toBeVisible();
    violations.push(
      ...(await findSeriousAccessibilityViolations(
        page,
        "/clinician/renewals#prescription-edit",
      )),
    );
    expectNoSeriousAccessibilityViolations(violations);
  });

  test("representative portal workspaces have no serious WCAG violations", async ({
    page,
  }) => {
    await signInPortal(page);
    const violations: AccessibilityFinding[] = [];
    for (const path of portalRoutes) {
      await navigateWithinApplication(page, path);
      violations.push(
        ...(await findSeriousAccessibilityViolations(page, path)),
      );
    }
    await navigateWithinApplication(page, "/portal/records");
    await page.getByRole("button", { name: "Health summary" }).click();
    await expect(page.getByRole("heading", { name: "Refill request history" }))
      .toBeVisible();
    await expect(page.locator(".refill-history-source")).toBeVisible({
      timeout: 15_000,
    });
    violations.push(
      ...(await findSeriousAccessibilityViolations(
        page,
        "/portal/records#health-refill-history",
      )),
    );
    expectNoSeriousAccessibilityViolations(violations);
  });
});
