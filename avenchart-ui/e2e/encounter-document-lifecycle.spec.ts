import AxeBuilder from "@axe-core/playwright";
import type { APIRequestContext, Page } from "@playwright/test";
import { expect, test } from "./support/fixtures.ts";

const apiBaseUrl =
  process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";
const patientId = process.env.MODERN_UI_DOCUMENT_PATIENT_ID ?? "MOD-PAT-0001";
const encounter = Number(process.env.MODERN_UI_DOCUMENT_ENCOUNTER ?? "1000013");

async function signInClinician(page: Page) {
  await page.goto("/login");
  await page
    .getByLabel("Username")
    .fill(process.env.MODERN_UI_STAFF_USERNAME ?? "admin");
  await page
    .getByLabel("Password")
    .fill(process.env.MODERN_UI_STAFF_PASSWORD ?? "pass");
  await page.getByLabel("Password").press("Enter");
  await expect(page).toHaveURL(/\/clinician\/dashboard$/, {
    timeout: 20_000,
  });
}

async function createApiSession(request: APIRequestContext) {
  const response = await request.post(`${apiBaseUrl}/api/auth/login`, {
    data: {
      username: process.env.MODERN_UI_STAFF_USERNAME ?? "admin",
      password: process.env.MODERN_UI_STAFF_PASSWORD ?? "pass",
    },
  });
  expect(response.ok()).toBe(true);
  return (await response.json()) as { sessionId: string };
}

async function deleteMarkerDocuments(
  request: APIRequestContext,
  sessionId: string,
  marker: string,
) {
  const headers = { "X-Legacy EHR-Session": sessionId };
  const response = await request.get(
    `${apiBaseUrl}/api/documents/${patientId}?includeArchived=true`,
    { headers },
  );
  if (!response.ok()) return;
  const register = (await response.json()) as {
    documents: Array<{ id: number; name: string }>;
  };
  await Promise.all(
    register.documents
      .filter((document) => document.name.startsWith(marker))
      .map((document) =>
        request.delete(`${apiBaseUrl}/api/documents/${document.id}`, {
          headers,
        }),
      ),
  );
}

async function openEncounterAttachments(page: Page) {
  await page.goto(`/clinician/patients/${patientId}/encounters`);
  const encounterRow = page.locator(`[data-encounter="${encounter}"]`);
  await expect(encounterRow).toBeVisible({ timeout: 20_000 });
  await encounterRow.click();
  const workspace = page.locator(
    '[aria-labelledby="encounter-attachments-title"]',
  );
  await expect(workspace).toBeVisible({ timeout: 20_000 });
  await expect(
    workspace.getByText(`encounter #${encounter}`, { exact: false }),
  ).toBeVisible();
  return workspace;
}

test("encounter attachments expose the protected file, link, version, review, and archive lifecycle", async ({
  page,
  request,
}, testInfo) => {
  test.skip(
    !Number.isInteger(encounter),
    "A valid encounter number is required.",
  );
  const marker = `MUC-07-03 ${testInfo.project.name} ${Date.now()}`;
  const fileName = `${marker} file`;
  const linkName = `${marker} link`;
  const originalText = `${marker} original protected bytes`;
  const replacementText = `${marker} replacement protected bytes`;
  const replacementReason = `${marker} correct attachment content`;
  const denialReason = `${marker} insufficient source evidence`;
  const archiveReason = `${marker} move out of active view`;
  const restoreReason = `${marker} restore for continued review`;
  const apiSession = await createApiSession(request);

  try {
    await signInClinician(page);
    const workspace = await openEncounterAttachments(page);

    await workspace
      .getByRole("button", { name: "Add encounter attachment" })
      .click();
    await workspace.getByRole("button", { name: "Upload file" }).click();
    await workspace.getByLabel("Name").fill(fileName);
    await workspace.getByLabel("File").setInputFiles({
      name: `${marker}.txt`,
      mimeType: "text/plain",
      buffer: Buffer.from(originalText),
    });
    await workspace.getByLabel("Filing note").fill(`${marker} filing note`);
    await workspace.getByRole("button", { name: "File attachment" }).click();

    const fileCard = workspace
      .locator(".encounter-document-card")
      .filter({ hasText: fileName });
    await expect(fileCard).toBeVisible({ timeout: 20_000 });
    await expect(fileCard).toContainText("Version 1");
    await expect(fileCard).toContainText("text/plain");

    await fileCard.getByRole("button", { name: "Preview" }).click();
    await expect(fileCard.getByText(originalText)).toBeVisible({
      timeout: 20_000,
    });
    await fileCard.getByRole("button", { name: "Close" }).click();

    await fileCard.getByRole("button", { name: "Replace content" }).click();
    await fileCard.getByLabel("Replacement text").fill(replacementText);
    await fileCard.getByLabel("Replacement reason").fill(replacementReason);
    await fileCard.getByRole("button", { name: "Save new version" }).click();
    await expect(fileCard).toContainText("Version 2", { timeout: 20_000 });

    await fileCard.getByRole("button", { name: "History" }).click();
    const history = fileCard.getByLabel(`Lifecycle history for ${fileName}`);
    await expect(history).toContainText(replacementReason);
    await expect(history).toContainText("Version 2");
    await expect(history).toContainText("Version 1");
    await history.getByRole("button", { name: "Close history" }).click();

    await fileCard.getByRole("button", { name: "Review", exact: true }).click();
    await fileCard.getByLabel("Review decision").selectOption("denied");
    await fileCard.getByLabel("Decision reason").fill(denialReason);
    await fileCard.getByRole("button", { name: "Record decision" }).click();
    await expect(fileCard).toContainText("denied", { timeout: 20_000 });

    await fileCard
      .getByRole("button", { name: "Archive", exact: true })
      .click();
    await fileCard.getByLabel("Archive reason").fill(archiveReason);
    await fileCard.getByRole("button", { name: "Archive document" }).click();
    await expect(fileCard).toHaveClass(/is-archived/, { timeout: 20_000 });

    await fileCard
      .getByRole("button", { name: "Restore", exact: true })
      .click();
    await fileCard.getByLabel("Restore reason").fill(restoreReason);
    await fileCard.getByRole("button", { name: "Restore document" }).click();
    await expect(fileCard).not.toHaveClass(/is-archived/, {
      timeout: 20_000,
    });

    await workspace
      .getByRole("button", { name: "Add encounter attachment" })
      .click();
    await workspace.getByRole("button", { name: "External link" }).click();
    await workspace.getByLabel("Name").fill(linkName);
    await workspace
      .getByLabel("External http or https URL")
      .fill("https://example.com/clinical-reference");
    await workspace.getByRole("button", { name: "File attachment" }).click();

    const linkCard = workspace
      .locator(".encounter-document-card")
      .filter({ hasText: linkName });
    await expect(linkCard).toBeVisible({ timeout: 20_000 });
    const externalLink = linkCard.getByRole("link", {
      name: "Open external link",
    });
    await expect(externalLink).toHaveAttribute(
      "href",
      "https://example.com/clinical-reference",
    );
    await expect(externalLink).toHaveAttribute("target", "_blank");
    await expect(externalLink).toHaveAttribute(
      "rel",
      /noopener.*noreferrer|noreferrer.*noopener/,
    );

    const accessibility = await new AxeBuilder({ page })
      .include('[aria-labelledby="encounter-attachments-title"]')
      .withTags(["wcag2a", "wcag2aa"])
      .analyze();
    expect(
      accessibility.violations.filter((violation) =>
        ["serious", "critical"].includes(violation.impact ?? ""),
      ),
    ).toEqual([]);
  } finally {
    await deleteMarkerDocuments(request, apiSession.sessionId, marker);
  }
});
