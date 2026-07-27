import { expect, test, type Page } from "@playwright/test";

async function signInClinician(page: Page) {
  await page.goto("/login");
  await page
    .getByLabel("Username")
    .fill(process.env.MODERN_UI_STAFF_USERNAME ?? "admin");
  await page
    .getByLabel("Password")
    .fill(process.env.MODERN_UI_STAFF_PASSWORD ?? "pass");
  for (let attempt = 0; attempt < 3; attempt += 1) {
    await page.getByRole("button", { name: "Sign in" }).click();
    try {
      await expect(page).toHaveURL(/\/clinician\/dashboard$/, {
        timeout: 15_000,
      });
      return;
    } catch {
      if (attempt === 2) throw new Error("Clinician sign-in did not complete.");
    }
  }
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
  for (let attempt = 0; attempt < 3; attempt += 1) {
    await page.getByRole("button", { name: "Sign in" }).click();
    try {
      await expect(page).toHaveURL(/\/portal\/home$/, { timeout: 15_000 });
      return;
    } catch {
      if (attempt === 2) throw new Error("Portal sign-in did not complete.");
    }
  }
}

async function openNotifications(page: Page) {
  const openNavigation = page.getByRole("button", {
    name: "Open navigation",
  });
  if ((page.viewportSize()?.width ?? 1024) <= 680) {
    await expect(openNavigation).toBeVisible({ timeout: 30_000 });
    await openNavigation.click();
  }
  const notifications = page.getByRole("button", { name: "Notifications" });
  await expect(notifications).toBeVisible({ timeout: 30_000 });
  await notifications.click();
}

test.describe("material workflows", () => {
  test("dashboard counts deep-link to equivalent visible filters", async ({
    page,
  }) => {
    await signInClinician(page);

    const appointmentsLink = page.getByRole("link", {
      name: /Today's appointments/,
    });
    const dashboardAppointments = (
      await appointmentsLink.locator(".dash-stat-value").textContent()
    )?.trim();
    await appointmentsLink.click();
    const expectedDate = new Date().toISOString().slice(0, 10);
    await expect(page).toHaveURL(
      new RegExp(`/clinician/schedule\\?date=${expectedDate}$`),
    );
    await expect(
      page.getByRole("heading", { name: "Schedule" }),
    ).toBeVisible({ timeout: 30_000 });
    await expect(page.getByLabel("Select date")).toHaveValue(expectedDate);
    if (dashboardAppointments && /^\d+$/.test(dashboardAppointments)) {
      await expect(page.getByText(
        new RegExp(`${dashboardAppointments} appointments on this date`),
      )).toBeVisible();
    }

    await page.goto("/clinician/dashboard");
    const labsLink = page.getByRole("link", { name: /Labs pending review/ });
    const dashboardLabs = (
      await labsLink.locator(".dash-stat-value").textContent()
    )?.trim();
    await labsLink.click();
    await expect(page).toHaveURL(/\/clinician\/labs\?status=pending$/);
    await expect(
      page.getByRole("heading", { name: "Lab queue" }),
    ).toBeVisible({ timeout: 30_000 });
    await expect(page.getByText("Status: pending", { exact: true })).toBeVisible();
    if (dashboardLabs && /^\d+$/.test(dashboardLabs)) {
      await expect(
        page.getByText(
          dashboardLabs === "0"
            ? "All reports reviewed"
            : `${dashboardLabs} reports pending review`,
          { exact: false },
        ),
      ).toBeVisible();
    }

    await page.goto("/clinician/dashboard");
    const messagesLink = page.getByRole("link", { name: /New messages/ });
    const dashboardMessages = (
      await messagesLink.locator(".dash-stat-value").textContent()
    )?.trim();
    await messagesLink.click();
    await expect(page).toHaveURL(/\/clinician\/messages\?status=new$/);
    await expect(
      page.getByRole("heading", { name: "Message inbox" }),
    ).toBeVisible({ timeout: 30_000 });
    await expect(page.getByLabel("Status")).toHaveValue("new");
    if (dashboardMessages && /^\d+$/.test(dashboardMessages)) {
      await expect(
        page
          .getByRole("button", { name: /Unread/ })
          .getByText(dashboardMessages, { exact: true }),
      ).toBeVisible();
    }
  });

  test("alert count breakdown links to equivalent filters", async ({
    page,
  }) => {
    await signInClinician(page);
    await openNotifications(page);

    const labsAlert = page.getByRole("link", {
      name: /\d+ unreviewed lab reports/,
    });
    const messagesAlert = page.getByRole("link", {
      name: /\d+ new patient messages/,
    });
    await expect(labsAlert).toHaveAttribute(
      "href",
      "/clinician/labs?status=pending",
    );
    await expect(messagesAlert).toHaveAttribute(
      "href",
      "/clinician/messages?status=new",
    );

    await labsAlert.click();
    await expect(page).toHaveURL(/\/clinician\/labs\?status=pending$/);
    await expect(page.getByText("Status: pending", { exact: true })).toBeVisible();

    await page.goto("/clinician/dashboard");
    await openNotifications(page);
    await page
      .getByRole("link", { name: /\d+ new patient messages/ })
      .click();
    await expect(page).toHaveURL(/\/clinician\/messages\?status=new$/);
    await expect(page.getByLabel("Status")).toHaveValue("new");
  });

  test("portal report selections are identifiable and validated", async ({
    page,
  }) => {
    await signInPortal(page);
    await page.goto("/portal/records");
    await page.getByRole("button", { name: "Medical report" }).click();
    await page.getByRole("button", { name: "Choose report contents" }).click();

    const encounterForm = page
      .getByRole("checkbox", { name: /encounter #\d+/i })
      .first();
    await expect(encounterForm).toBeVisible();

    await page.getByRole("button", { name: "Select none" }).click();
    await page.getByRole("button", { name: "Generate selected report" }).click();
    await expect(page.getByRole("alert")).toContainText(
      "Select at least one report item.",
    );

    await page.getByRole("button", { name: "Select all" }).click();
    await expect(encounterForm).toBeChecked();
    await page.getByRole("button", { name: "Generate selected report" }).click();
    await expect(page.locator(".report-generated")).toBeVisible({
      timeout: 15_000,
    });
    await expect(page.locator(".report-generated")).toContainText("Generated");
    await expect(
      page.getByRole("heading", { name: "Report activity" }),
    ).toBeVisible();
    await expect(
      page.getByText("Generated report", { exact: true }).first(),
    ).toBeVisible();
  });
});
