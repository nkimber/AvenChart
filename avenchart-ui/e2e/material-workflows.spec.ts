// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

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
    .fill(process.env.MODERN_UI_PORTAL_USERNAME ?? "mod-pat-0004@example.test");
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
    await expect(page.getByRole("heading", { name: "Schedule" })).toBeVisible({
      timeout: 30_000,
    });
    await expect(page.getByLabel("Select date")).toHaveValue(expectedDate);
    if (dashboardAppointments && /^\d+$/.test(dashboardAppointments)) {
      await expect(
        page.getByText(
          new RegExp(`${dashboardAppointments} appointments on this date`),
        ),
      ).toBeVisible();
    }

    await page.goto("/clinician/dashboard");
    const labsLink = page.getByRole("link", { name: /Labs pending review/ });
    const dashboardLabs = (
      await labsLink.locator(".dash-stat-value").textContent()
    )?.trim();
    await labsLink.click();
    await expect(page).toHaveURL(/\/clinician\/labs\?status=pending$/);
    await expect(page.getByRole("heading", { name: "Lab queue" })).toBeVisible({
      timeout: 30_000,
    });
    await expect(
      page.getByText("Status: pending", { exact: true }),
    ).toBeVisible();
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
    await expect(
      page.getByText("Status: pending", { exact: true }),
    ).toBeVisible();

    await page.goto("/clinician/dashboard");
    await openNotifications(page);
    await page.getByRole("link", { name: /\d+ new patient messages/ }).click();
    await expect(page).toHaveURL(/\/clinician\/messages\?status=new$/);
    await expect(page.getByLabel("Status")).toHaveValue("new");
  });

  test("encounter alerts expose severity and acknowledgement evidence", async ({
    page,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "desktop-chromium",
      "The acknowledgement proof runs once to avoid concurrent writes to the shared synthetic encounter.",
    );
    await signInClinician(page);
    await page.goto("/clinician/patients/MOD-PAT-0901/encounters");
    await page
      .getByRole("button", {
        name: /Comprehensive new patient evaluation/,
      })
      .click();

    const alertPanel = page.getByRole("region", { name: "Clinical alerts" });
    const activeAlert = alertPanel.locator(".clinical-alert-card");
    await expect(activeAlert.getByText("Allergy review")).toBeVisible({
      timeout: 30_000,
    });
    await expect(activeAlert.getByText("Warning alert")).toHaveAttribute(
      "data-alert-severity",
      "warning",
    );
    await expect(
      activeAlert.getByText(
        "No active allergy records are documented for this patient.",
      ),
    ).toBeVisible();

    await alertPanel
      .getByRole("button", { name: "Acknowledge review" })
      .click();
    await expect(alertPanel.getByText("Acknowledged")).toBeVisible();
    await expect(
      alertPanel.getByText(/Acknowledged by admin at/),
    ).toBeVisible();

    await alertPanel.getByRole("button", { name: "Reopen alert" }).click();
    await expect(alertPanel.getByText("Warning alert")).toBeVisible();
    await expect(alertPanel.getByText("Reopened")).toBeVisible();
    await expect(alertPanel.getByText(/Reopened by admin at/)).toBeVisible();
  });

  test("lab report and order queues expose authoritative filters and row contracts", async ({
    page,
  }) => {
    await signInClinician(page);
    await page.goto("/clinician/labs?reportStatus=all");

    const reportTotals = page.getByRole("region", {
      name: "Report review totals",
    });
    await expect(reportTotals).toBeVisible({ timeout: 30_000 });
    await expect(reportTotals).toContainText("server status all");
    await expect(page.getByLabel("Review status")).toHaveValue("all");
    await expect(
      page.getByRole("region", { name: "Filtered report review queue" }),
    ).toBeVisible({ timeout: 30_000 });

    await page.getByRole("button", { name: /Order queue/ }).click();
    const orderTotals = page.getByRole("region", {
      name: "Procedure order totals",
    });
    await expect(orderTotals).toBeVisible({ timeout: 30_000 });
    await expect(orderTotals).toContainText("server status ready-to-send");
    await expect(page.getByLabel("Queue status")).toHaveValue("ready-to-send");

    await page.getByLabel("Patient ID").fill("MOD-PAT-0960");
    await page.getByRole("button", { name: "Apply filters" }).click();
    await expect(page).toHaveURL(/patientId=MOD-PAT-0960/);
    await expect(
      page.getByRole("button", { name: "Collins, Theo" }).first(),
    ).toBeVisible({ timeout: 30_000 });
    await expect(
      page.getByRole("region", { name: "Filtered procedure order queue" }),
    ).toContainText("order 5001260", { timeout: 30_000 });
  });

  test("patient relationships and care team use the protected mutation workflows", async ({
    page,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "desktop-chromium",
      "The mutation proof runs once to avoid cross-project writes to the shared synthetic patient.",
    );
    await signInClinician(page);
    await page.goto("/clinician/patients/MOD-PAT-0004/summary");

    const guardian = page.locator("section").filter({
      has: page.getByRole("heading", {
        name: "Guardian or representative",
      }),
    });
    await guardian.getByRole("button", { name: "Edit" }).click();
    await expect(
      guardian.getByLabel("Guardian or representative"),
    ).toBeVisible();
    await guardian.getByRole("button", { name: "Save representative" }).click();
    await expect(
      page
        .getByRole("status")
        .filter({ hasText: "Guardian and representative details saved." }),
    ).toBeVisible();

    const employer = page.locator("section").filter({
      has: page.getByRole("heading", { name: "Employer" }),
    });
    await employer.getByRole("button", { name: "Edit" }).click();
    await expect(employer.getByLabel("Employer name")).toBeVisible();
    await employer.getByRole("button", { name: "Save employer" }).click();
    await expect(
      page.getByRole("status").filter({ hasText: "Employer details saved." }),
    ).toBeVisible();

    const provider = page.locator("section").filter({
      has: page.getByRole("heading", { name: "Primary provider" }),
    });
    const editProvider = provider.getByRole("button", { name: "Edit" });
    await expect(editProvider).toBeEnabled({ timeout: 15_000 });
    await editProvider.click();
    await expect(provider.getByLabel("Provider")).toBeVisible();
    await provider.getByRole("button", { name: "Save provider" }).click();
    await expect(
      page
        .getByRole("status")
        .filter({ hasText: "Primary provider assignment saved." }),
    ).toBeVisible();

    const careTeam = page.locator("section").filter({
      has: page.getByRole("heading", { name: "Care team" }),
    });
    const editCareTeam = careTeam.getByRole("button", {
      name: "Edit care team",
    });
    await expect(editCareTeam).toBeEnabled({ timeout: 15_000 });
    await editCareTeam.click();
    await expect(careTeam.getByLabel("Team name")).toBeVisible();
    await careTeam.getByRole("button", { name: "Save care team" }).click();
    await expect(
      page.getByRole("status").filter({ hasText: "Care team saved." }),
    ).toBeVisible();
  });

  test("patient summary deep-links to account, aging, statement, and ledger context", async ({
    page,
  }) => {
    await signInClinician(page);
    await page.goto("/clinician/patients/MOD-PAT-0004/summary");
    await page.getByRole("link", { name: "View patient account" }).click();

    await expect(page).toHaveURL(
      /\/clinician\/billing\?patientId=MOD-PAT-0004$/,
    );
    await expect(page.getByLabel("Patient ID")).toHaveValue("MOD-PAT-0004");
    const account = page.getByRole("region", {
      name: "Patient account summary",
    });
    await expect(account).toBeVisible({ timeout: 15_000 });
    await expect(
      account.getByText("Account balance", { exact: true }),
    ).toBeVisible();
    await expect(account.getByRole("heading", { name: "Aging" })).toBeVisible();
    await expect(
      account.getByRole("heading", { name: "Statement readiness" }),
    ).toBeVisible();
    await expect(
      account.getByRole("heading", { name: "Account ledger" }),
    ).toBeVisible();
  });

  test("inventory lots expose searchable units, costs, metadata, and immutable ledger evidence", async ({
    page,
  }) => {
    await signInClinician(page);
    await page.goto("/clinician/inventory");

    await page.getByLabel("Search lots").fill("GLV-2026-01-A");
    await expect(page.getByText(/1 of \d+ lots/)).toBeVisible({
      timeout: 15_000,
    });
    await expect(
      page.getByText("$8.75 per box", { exact: true }),
    ).toBeVisible();
    await page.getByRole("button", { name: "Open lot GLV-2026-01-A" }).click();

    await expect(
      page.getByRole("heading", { name: "Lot GLV-2026-01-A" }),
    ).toBeVisible();
    await expect(
      page.getByRole("heading", { name: "Metadata history" }),
    ).toBeVisible({ timeout: 15_000 });
    await expect(
      page.getByText("No metadata changes recorded.", { exact: true }),
    ).toBeVisible();
    const ledger = page
      .getByRole("heading", { name: "Immutable transaction ledger" })
      .locator("xpath=ancestor::section");
    await expect(
      ledger.getByText("consumption", { exact: true }),
    ).toBeVisible();
    await expect(ledger.getByText("-24 box", { exact: true })).toBeVisible();
    await expect(
      ledger.getByText("gold-frontdesk-01", { exact: true }),
    ).toBeVisible();
    await expect(
      ledger.getByText("00000000-0000-0000-0000-000000010002", { exact: true }),
    ).toBeVisible();
  });

  test("inventory requisitions expose validated lifecycle controls and immutable events", async ({
    page,
  }) => {
    await signInClinician(page);
    await page.goto("/clinician/inventory");

    const requisitions = page
      .getByRole("heading", { name: "Purchase requisitions" })
      .locator("xpath=ancestor::section");
    await expect(requisitions.getByLabel("Search requisitions")).toBeVisible({
      timeout: 15_000,
    });
    await requisitions.getByRole("button", { name: "New requisition" }).click();
    await expect(requisitions.getByLabel("Requesting facility")).toBeVisible();
    await expect(requisitions.getByLabel("Item 1")).toBeVisible();
    await expect(requisitions.getByLabel("Quantity")).toHaveValue("1");
    await requisitions.getByRole("button", { name: "Add line" }).click();
    await expect(requisitions.getByLabel("Item 2")).toBeVisible();

    await requisitions
      .getByLabel("Search requisitions")
      .fill("definitely-no-requisition");
    await expect(
      requisitions.getByText(
        "No purchase requisitions match the active filters.",
        { exact: true },
      ),
    ).toBeVisible();
    await requisitions.getByLabel("Search requisitions").fill("");

    const openRequisition = requisitions.getByRole("button", {
      name: /Open requisition/,
    });
    if ((await openRequisition.count()) > 0) {
      await openRequisition.first().click();
      await expect(
        requisitions.getByRole("heading", {
          name: "Immutable lifecycle events",
        }),
      ).toBeVisible();
    }
  });

  test("inventory receiving distinguishes direct and requisition reconciliation", async ({
    page,
  }) => {
    await signInClinician(page);
    await page.goto("/clinician/inventory");

    const receiving = page
      .getByRole("heading", { name: "Receive inventory" })
      .locator("xpath=ancestor::section");
    await expect(receiving.getByLabel("Direct vendor receipt")).toBeChecked({
      timeout: 15_000,
    });
    await expect(receiving.getByLabel("Vendor", { exact: true })).toBeVisible();
    await expect(receiving.getByLabel("Receiving facility")).toBeVisible();
    await expect(receiving.getByLabel("Inventory item")).toBeVisible();
    await expect(receiving.getByLabel("Lot number")).toBeVisible();
    await expect(receiving.getByLabel(/Unit cost/)).toBeVisible();
    await receiving.getByLabel("Reconcile approved requisition").check();
    await expect(
      receiving.getByLabel("Approved requisition request"),
    ).toBeVisible();
    await expect(receiving.getByLabel("Outstanding line")).toBeVisible();
    await expect(
      receiving.getByLabel("Vendor", { exact: true }),
    ).toBeDisabled();
    await expect(receiving.getByLabel("Receiving facility")).toBeDisabled();
    await expect(receiving.getByLabel("Inventory item")).toBeDisabled();
  });

  test("inventory exposes named stock-control workflows instead of generic adjustments", async ({
    page,
  }) => {
    await signInClinician(page);
    await page.goto("/clinician/inventory");

    const actions = page
      .getByRole("heading", { name: "Authoritative stock actions" })
      .locator("xpath=ancestor::section");
    const workflow = actions.getByLabel("Stock workflow");
    await expect(workflow).toBeVisible({ timeout: 15_000 });
    await expect(workflow.locator("option")).toHaveText([
      "Consume or transfer stock",
      "Reconcile a physical count",
      "Disposition an expired lot",
      "Witness full-lot destruction",
    ]);
    await expect(actions).not.toContainText("Purchase receipt");
    await expect(actions).not.toContainText("Count adjustment");

    await workflow.selectOption("count");
    await expect(actions.getByLabel(/^Lot to count/)).toBeVisible();
    await expect(
      actions.getByLabel("Counted quantity", { exact: true }),
    ).toBeVisible();
    await expect(
      actions.getByLabel("Count notes", { exact: true }),
    ).toBeVisible();

    await workflow.selectOption("expiry");
    await expect(actions.getByLabel(/^Expired lot/)).toBeVisible();
    await expect(
      actions.getByLabel(/^Disposition/).locator("option"),
    ).toHaveText([
      "Quarantine pending decision",
      "Return expired stock",
      "Destroy expired stock",
    ]);

    await workflow.selectOption("destruction");
    await expect(actions.getByLabel(/^Lot to destroy/)).toBeVisible();
    await expect(
      actions.getByLabel("Destruction method", { exact: true }),
    ).toBeVisible();
    await expect(actions.getByLabel("Witness", { exact: true })).toBeVisible();
    await expect(
      actions.getByRole("button", {
        name: "Record witnessed destruction",
      }),
    ).toBeDisabled();
  });

  test("inventory medication links expose catalog, current mapping, and unmapped boundaries", async ({
    page,
  }) => {
    await signInClinician(page);
    await page.goto("/clinician/inventory");

    const links = page
      .getByRole("heading", { name: "Medication inventory links" })
      .locator("xpath=ancestor::section");
    await expect(links.getByText(/\d+ linked \/ \d+ unmapped/)).toBeVisible({
      timeout: 15_000,
    });
    await expect(links).toContainText(
      "does not expose link-history review or unlink contracts yet",
    );
    await expect(links.getByLabel("Link inventory item")).toBeVisible();
    await links.getByLabel("Link inventory item").selectOption({ index: 1 });
    await expect(links.getByLabel("Search local medications")).toBeVisible();
    await expect(links.getByLabel("Local RXCUI medication")).toBeVisible();
    await expect(
      links.getByRole("button", { name: "Save medication link" }),
    ).toBeDisabled();
    await expect(links.getByLabel("Search inventory mappings")).toBeVisible();
    await expect(links.getByRole("table")).toBeVisible();
  });

  test("inventory exposes patient, encounter, FEFO, and prescription dispensing context", async ({
    page,
  }) => {
    await signInClinician(page);
    await page.goto("/clinician/inventory");

    const dispensing = page
      .getByRole("heading", { name: "Patient sales and dispensing" })
      .locator("xpath=ancestor::section");
    await expect(dispensing.getByLabel("Find patient")).toBeVisible({
      timeout: 15_000,
    });
    await dispensing.getByLabel("Find patient").fill("MOD-PAT-0001");
    await dispensing.getByRole("button", { name: "Search patients" }).click();
    await expect(
      dispensing.getByLabel("Patient", { exact: true }),
    ).toBeVisible();
    await dispensing
      .getByLabel("Patient", { exact: true })
      .selectOption("MOD-PAT-0001");

    await expect(
      dispensing.getByText(/canonical ID\s+MOD-PAT-0001/),
    ).toBeVisible({ timeout: 15_000 });
    await expect(dispensing.getByLabel("Patient encounter")).toBeVisible();
    await expect(dispensing.getByLabel("Debit one selected lot")).toBeChecked();
    await expect(dispensing.getByLabel("Sale inventory lot")).toBeVisible();

    await dispensing.getByLabel("Allocate earliest expiry first").check();
    await expect(dispensing.getByLabel("Sale inventory item")).toBeVisible();

    await dispensing
      .getByRole("button", { name: "Prescription dispense" })
      .click();
    await expect(dispensing.getByLabel("Active prescription")).toBeVisible();
    await expect(dispensing).toContainText("never combines lots");
    await expect(dispensing).toContainText(
      "Fees below are local inventory-sale evidence and do not create a billing charge.",
    );
  });

  test("inventory activity exposes report metadata, bounded detail, and CSV output controls", async ({
    page,
  }) => {
    await signInClinician(page);
    await page.goto("/clinician/inventory");

    const activity = page
      .getByRole("heading", { name: "Activity report" })
      .locator("xpath=ancestor::section");
    await expect(activity.getByLabel("Activity from date")).toBeVisible({
      timeout: 15_000,
    });
    await expect(activity.getByLabel("Activity facility")).toBeVisible();
    await activity.getByRole("button", { name: "Run report" }).click();

    await expect(activity.getByText("Dataset", { exact: true })).toBeVisible({
      timeout: 15_000,
    });
    await expect(
      activity.getByText("Dataset version", { exact: true }),
    ).toBeVisible();
    await expect(
      activity.getByText("Server filters", { exact: true }),
    ).toBeVisible();
    await expect(
      activity.getByText(/\d+ matching \/ \d+ returned/),
    ).toBeVisible();
    await expect(activity.getByLabel("Search returned activity")).toBeVisible();
    await expect(
      activity.getByLabel("Returned transaction type"),
    ).toBeVisible();
    await expect(activity.getByRole("table")).toBeVisible();
    await expect(
      activity.getByRole("button", { name: "CSV export" }),
    ).toBeVisible();
  });

  test("inventory replenishment exposes read-only candidate evidence and policy gates", async ({
    page,
  }) => {
    await signInClinician(page);
    await page.goto("/clinician/inventory");

    const replenishment = page
      .getByRole("heading", { name: "Replenishment planning" })
      .locator("xpath=ancestor::section");
    await expect(
      replenishment.getByText(/^\d+ candidates?$/),
    ).toBeVisible({ timeout: 15_000 });
    await expect(
      replenishment.getByText("Candidate rule", { exact: true }),
    ).toBeVisible();
    await expect(replenishment).toContainText(
      "Aggregate on hand ≤ reorder point",
    );
    await expect(
      replenishment.getByLabel("Search replenishment candidates"),
    ).toBeVisible();
    await expect(replenishment.getByRole("table")).toBeVisible();
    await expect(
      replenishment.getByText("Requisition creation is policy-gated"),
    ).toBeVisible();
    await expect(
      replenishment.getByRole("button", { name: /create requisition/i }),
    ).toHaveCount(0);
  });

  test("portal appointments retain past appointment status history", async ({
    page,
  }) => {
    await signInPortal(page);
    await page.goto("/portal/appointments");

    const history = page
      .getByRole("heading", { name: "Appointment history" })
      .locator("xpath=ancestor::section");
    await expect(history.getByText("2 past", { exact: true })).toBeVisible({
      timeout: 15_000,
    });
    await expect(
      history.getByText("Established Patient", { exact: true }),
    ).toBeVisible();
    await expect(
      history.getByText("New Patient", { exact: true }),
    ).toBeVisible();
    await expect(
      history.getByText("Arrived", { exact: true }).first(),
    ).toBeVisible();
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
    await page
      .getByRole("button", { name: "Generate selected report" })
      .click();
    await expect(page.getByRole("alert")).toContainText(
      "Select at least one report item.",
    );

    await page.getByRole("button", { name: "Select all" }).click();
    await expect(encounterForm).toBeChecked();
    await page
      .getByRole("button", { name: "Generate selected report" })
      .click();
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
