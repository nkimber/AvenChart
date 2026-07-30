import { expect, test, type Page } from "@playwright/test";

test.skip(
  process.env.MODERN_UI_RUN_LAB_SPECIMEN !== "1",
  "Run explicitly against an isolated API and database.",
);

const apiBaseUrl =
  process.env.MODERN_UI_API_BASE_URL ?? "http://localhost:5001";

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

async function getClinicianSessionId(page: Page) {
  const sessionId = await page.evaluate(() => {
    const raw = sessionStorage.getItem("avenchart-ui.clinicianSession");
    return raw ? (JSON.parse(raw) as { sessionId?: string }).sessionId : null;
  });
  if (!sessionId) throw new Error("Clinician session ID was not persisted.");
  return sessionId;
}

test.describe("lab specimen lifecycle", () => {
  test("governs rejection and recollection with visible provenance", async ({
    page,
  }) => {
    await signInClinician(page);
    const sessionId = await getClinicianSessionId(page);
    const headers = { "X-Legacy EHR-Session": sessionId };
    const suffix = Date.now();
    const originalIdentifier = `BROWSER-SPEC-${suffix}-1`;
    const replacementIdentifier = `BROWSER-SPEC-${suffix}-2`;
    let orderId: number | null = null;

    try {
      const orderResponse = await page.request.post(
        `${apiBaseUrl}/api/procedures/orders`,
        {
          headers,
          data: {
            patientId: "MOD-PAT-0004",
            providerId: null,
            labId: null,
            encounterId: 1000043,
            dateOrdered: "2026-07-30T10:00:00",
            priority: "routine",
            status: "pending",
            procedureCode: `BROWSER-SPEC-${suffix}`,
            procedureName: `Browser specimen proof ${suffix}`,
            procedureType: "laboratory",
            diagnosis: "Z00.00",
            instructions: "Temporary browser specimen proof.",
          },
        },
      );
      expect(orderResponse.status()).toBe(201);
      orderId = ((await orderResponse.json()) as { id: number }).id;

      const specimenResponse = await page.request.post(
        `${apiBaseUrl}/api/procedures/specimens`,
        {
          headers,
          data: {
            orderId,
            specimenIdentifier: originalIdentifier,
            accessionIdentifier: `BROWSER-ACC-${suffix}-1`,
            specimenTypeCode: "SER",
            specimenType: "Serum",
            collectionMethodCode: "VEN",
            collectionMethod: "Venipuncture",
            specimenLocationCode: "LAB",
            specimenLocation: "Main laboratory",
            collectedDate: "2026-07-30T10:30:00",
            volumeValue: 2,
            volumeUnit: "mL",
            conditionCode: "SAT",
            specimenCondition: "Satisfactory",
            comments: "Temporary browser specimen proof.",
          },
        },
      );
      expect(specimenResponse.status()).toBe(201);

      await page.goto("/clinician/patients/MOD-PAT-0004/labs");
      let specimenCard = page
        .locator(".cl-specimen-card")
        .filter({ hasText: originalIdentifier });
      await expect(specimenCard).toContainText("collected · v1", {
        timeout: 30_000,
      });
      await specimenCard.getByText("1 lifecycle event").click();
      await expect(specimenCard).toContainText("Initial local specimen collection.");
      await expect(specimenCard).toContainText("admin");

      await specimenCard.getByRole("button", { name: "Label" }).click();
      await specimenCard
        .getByLabel("Reason")
        .fill("Verified the printed barcode against the local order.");
      await specimenCard
        .getByRole("button", { name: "Confirm Label" })
        .click();
      await expect(specimenCard).toContainText("labeled · v2", {
        timeout: 30_000,
      });

      await specimenCard.getByRole("button", { name: "Receive" }).click();
      await specimenCard
        .getByLabel("Reason")
        .fill("Laboratory intake verified the label and container.");
      await specimenCard
        .getByRole("button", { name: "Confirm Receive" })
        .click();
      await expect(specimenCard).toContainText("received · v3", {
        timeout: 30_000,
      });

      await specimenCard.getByRole("button", { name: "Reject" }).click();
      await specimenCard
        .getByLabel("Reason")
        .fill("Container integrity failed intake inspection.");
      await specimenCard
        .getByRole("button", { name: "Confirm Reject" })
        .click();
      await expect(specimenCard).toContainText("rejected · v4", {
        timeout: 30_000,
      });
      await expect(
        specimenCard.getByRole("button", { name: "Receive" }),
      ).toHaveCount(0);

      await specimenCard.getByRole("button", { name: "Recollect" }).click();
      await specimenCard
        .getByLabel("New specimen identifier")
        .fill(replacementIdentifier);
      await specimenCard
        .getByLabel("New accession identifier")
        .fill(`BROWSER-ACC-${suffix}-2`);
      await specimenCard.getByLabel("Recollected date").fill("2026-07-30");
      await specimenCard.getByLabel("Condition code").fill("SAT");
      await specimenCard
        .getByLabel("Specimen condition")
        .fill("Satisfactory replacement");
      await specimenCard
        .getByLabel("Recollection comments")
        .fill("Replacement container passed inspection.");
      await specimenCard
        .getByLabel("Reason")
        .fill("Replacement collected after the documented rejection.");
      await specimenCard
        .getByRole("button", { name: "Confirm Recollect" })
        .click();

      specimenCard = page
        .locator(".cl-specimen-card")
        .filter({ hasText: replacementIdentifier });
      await expect(specimenCard).toContainText("recollected · v5", {
        timeout: 30_000,
      });
      await specimenCard.getByText("5 lifecycle events").click();
      await expect(specimenCard).toContainText(
        "Replacement collected after the documented rejection.",
      );
      await expect(specimenCard).toContainText("rejected → recollected");
      await expect(specimenCard).toContainText("v5");
    } finally {
      if (orderId) {
        const deleteResponse = await page.request.delete(
          `${apiBaseUrl}/api/procedures/orders/${orderId}`,
          { headers },
        );
        expect([204, 404]).toContain(deleteResponse.status());
      }
    }
  });
});
