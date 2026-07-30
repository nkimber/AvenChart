import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

test.describe.configure({ mode: "serial" });
test.skip(
  process.env.MODERN_UI_RUN_STAFF_MESSAGE_LIFECYCLE !== "1",
  "Run through Test-StaffMessageWholeLifecycle.ps1 so the fixture remains isolated.",
);

async function signInClinician(page: Page) {
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
}

async function getSessionId(page: Page) {
  const sessionId = await page.evaluate(() => {
    const raw = sessionStorage.getItem("avenchart-ui.clinicianSession");
    return raw ? (JSON.parse(raw) as { sessionId?: string }).sessionId : null;
  });
  if (!sessionId) throw new Error("Clinician session ID was not persisted.");
  return sessionId;
}

function cleanupMessage(messageId: string, marker: string) {
  const safeMessageId = messageId.replaceAll("'", "''");
  const safeMarker = marker.replaceAll("'", "''");
  const sql = [
    "begin;",
    `delete from staff_message_attachments where message_id = '${safeMessageId}';`,
    `delete from message_correction_events where message_id = '${safeMessageId}';`,
    `delete from message_retention_events where message_id = '${safeMessageId}';`,
    `delete from message_escalation_events where message_id = '${safeMessageId}';`,
    `delete from message_assignment_events where message_id = '${safeMessageId}';`,
    `delete from messages where id = '${safeMessageId}' and title = '${safeMarker}';`,
    "commit;",
    `select (select count(*) from messages where id = '${safeMessageId}') + (select count(*) from staff_message_attachments where message_id = '${safeMessageId}') + (select count(*) from message_correction_events where message_id = '${safeMessageId}') + (select count(*) from message_retention_events where message_id = '${safeMessageId}') + (select count(*) from message_escalation_events where message_id = '${safeMessageId}') + (select count(*) from message_assignment_events where message_id = '${safeMessageId}');`,
  ].join(" ");
  const residue = execFileSync(
    "docker",
    [
      "compose",
      "exec",
      "-T",
      "postgres",
      "psql",
      "-X",
      "-U",
      "legacy-ehr",
      "-d",
      "legacy-ehr_modernized",
      "-v",
      "ON_ERROR_STOP=1",
      "-t",
      "-A",
      "-c",
      sql,
    ],
    {
      cwd: fileURLToPath(new URL("../../avenchart/", import.meta.url)),
      encoding: "utf8",
      stdio: ["ignore", "pipe", "pipe"],
    },
  )
    .trim()
    .split(/\r?\n/)
    .at(-1);
  expect(residue).toBe("0");
}

test("staff can complete the governed local message lifecycle", async ({
  page,
}, testInfo) => {
  test.setTimeout(120_000);
  test.skip(
    testInfo.project.name !== "desktop-chromium",
    "The cleanup-owned mutation proof runs once.",
  );

  await signInClinician(page);
  const sessionId = await getSessionId(page);
  const apiBaseUrl =
    process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
  const marker = `TMP-MESSAGE-BROWSER-${Date.now()}`;
  const correction = `Browser correction ${Date.now()}`;
  let messageId: string | null = null;

  try {
    const created = await page.request.post(`${apiBaseUrl}/api/messages`, {
      headers: { "X-Legacy EHR-Session": sessionId },
      data: {
        patientId: "MOD-PAT-0004",
        title: marker,
        body: "Browser-owned staff-message lifecycle.",
        assignedTo: "gold-provider-01",
      },
    });
    expect(created.ok()).toBeTruthy();
    messageId = ((await created.json()) as { id: string }).id;

    await page.goto(
      `/clinician/messages?patient=MOD-PAT-0004&subject=${encodeURIComponent(marker)}`,
    );
    const inboxItem = page.getByRole("button").filter({ hasText: marker });
    await expect(inboxItem).toBeVisible({ timeout: 30_000 });
    await inboxItem.click();

    let message = page.locator("article.msg-item").filter({
      has: page.getByRole("heading", { name: marker }),
    });
    await expect(message).toBeVisible({ timeout: 30_000 });

    await message.getByLabel("Assign to").selectOption("admin");
    await message
      .getByLabel("Reason (required to reassign or unassign)")
      .fill("Browser reasoned reassignment");
    await message.getByRole("button", { name: "Save assignment" }).click();
    await expect(message.getByText("Assigned to you")).toBeVisible();
    await message.getByRole("button", { name: "Assignment history" }).click();
    await expect(message.getByLabel("Assignment history")).toContainText(
      "reassigned",
    );

    await message.getByRole("button", { name: "Forward" }).click();
    const forwardPanel = message
      .locator(".msg-reply-form")
      .filter({ hasText: "Forward keeps this message" });
    await forwardPanel
      .getByLabel("Forward to")
      .selectOption("gold-frontdesk-01");
    await forwardPanel
      .getByLabel("Forwarding note (optional)")
      .fill("Browser forwarding evidence");
    await forwardPanel.getByRole("button", { name: "Forward message" }).click();
    await expect(message).toContainText("Assigned: gold-frontdesk-01");

    await message.getByRole("button", { name: "Append correction" }).click();
    const correctionPanel = message
      .locator(".msg-reply-form")
      .filter({ hasText: "A correction preserves" });
    await correctionPanel.getByLabel("Correction").fill(correction);
    await correctionPanel.getByLabel("Reason").fill("Browser correction proof");
    await correctionPanel
      .getByRole("button", { name: "Record correction" })
      .click();
    await expect(message).toContainText(correction);

    await message.locator('input[type="file"]').setInputFiles({
      name: `${marker}.txt`,
      mimeType: "text/plain",
      buffer: Buffer.from(`Browser attachment ${marker}`),
    });
    await message.getByRole("button", { name: "Add attachment" }).click();
    await expect(message).toContainText(`${marker}.txt`);

    await message.getByRole("button", { name: "Escalation" }).click();
    const escalationPanel = message
      .locator(".msg-reply-form")
      .filter({ hasText: "Escalation records a local urgency" });
    await escalationPanel.getByLabel("Reason").fill("Browser escalation proof");
    await escalationPanel.getByRole("button", { name: "Escalate" }).click();
    await expect(
      page
        .getByRole("status")
        .filter({ hasText: "Message escalation recorded." }),
    ).toBeVisible();

    await message.getByRole("button", { name: "Escalation" }).click();
    await escalationPanel
      .getByLabel("Reason")
      .fill("Browser escalation resolution");
    await expect(
      escalationPanel.getByRole("button", {
        name: "Resolve escalation",
      }),
    ).toBeEnabled();
    await escalationPanel
      .getByRole("button", { name: "Resolve escalation" })
      .click();
    await expect(
      page
        .getByRole("status")
        .filter({ hasText: "Message escalation resolved." }),
    ).toBeVisible();

    await message.getByRole("button", { name: "Reply", exact: true }).click();
    await message
      .getByLabel("Reply")
      .fill("Browser authenticated reply evidence");
    await message.getByRole("button", { name: "Reply", exact: true }).click();
    await expect(message).toContainText("Browser authenticated reply evidence");

    await message.getByRole("button", { name: "Archive" }).click();
    await message.getByLabel("Archive reason").fill("Browser archive proof");
    await message.getByRole("button", { name: "Archive message" }).click();
    await expect(message).toHaveCount(0);

    await page.getByRole("button", { name: "Show archived" }).click();
    message = page.locator("article.msg-item").filter({
      has: page.getByRole("heading", { name: marker }),
    });
    await expect(message).toContainText("This archived message is read-only.");
    await expect(message.locator('input[type="file"]')).toBeDisabled();
    await expect(
      message.getByRole("button", { name: "Forward" }),
    ).toBeDisabled();
    await expect(
      message.getByRole("button", { name: "Append correction" }),
    ).toBeDisabled();
    await expect(
      message.getByRole("button", { name: "Escalation" }),
    ).toBeDisabled();
    await expect(message.getByLabel("Assign to")).toBeDisabled();
    await expect(
      message.getByRole("button", { name: "Reply", exact: true }),
    ).toHaveCount(0);

    const accessibility = await new AxeBuilder({ page })
      .include("article.msg-item")
      .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
      .analyze();
    expect(
      accessibility.violations.filter(
        ({ impact }) => impact === "serious" || impact === "critical",
      ),
    ).toEqual([]);

    await message.getByRole("button", { name: "Restore" }).click();
    await message.getByLabel("Archive reason").fill("Browser restore proof");
    await message.getByRole("button", { name: "Restore message" }).click();
    await expect(message.getByText("Archived")).toHaveCount(0);
    await expect(
      message.getByRole("button", { name: "Archive" }),
    ).toBeEnabled();
  } finally {
    if (messageId) cleanupMessage(messageId, marker);
  }
});
