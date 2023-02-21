import { expect, test } from "@playwright/test";
import { DomainDetailsPage } from "./pages/domain-details.page";

let parentDomainId: string = "";

test.beforeAll(async ({ request }) => {
  const parentDomain = await request.post("/api/domains", {
    data: {
      name: "Test Domain",
    },
  });
  parentDomainId = (await parentDomain.json()).id;
  expect(parentDomain.ok()).toBeTruthy();

  const response = await request.post(`/api/domains/${parentDomainId}/domains`, {
    data: {
      name: "Test Subdomain",
    },
  });

  expect(response.ok()).toBeTruthy();
});

test.afterAll(async ({ request }) => {
  await request.delete(`/api/domains/${parentDomainId}`);
});

test.beforeEach(async ({ page }) => {
  await page.goto(`/domain/${parentDomainId}`);
});

test.describe("Edit domain", () => {
  test("should edit a domain", async ({ page }) => {
    const domainDetails = new DomainDetailsPage(page);

    await domainDetails.editDomainButton.click();
    await page.getByLabel("Short Key").fill("SUB");
    await page.getByLabel("Name (Required)").fill("domain new");
    await page.getByLabel("Vision").fill("This is an updated domain vision");
    await page.getByRole("button", { name: "save" }).click();

    await expect(domainDetails.editDomainButton).toBeVisible();
    await expect(page.getByText("SUB", { exact: true })).toBeVisible();
    await expect(page.getByRole("heading", { name: "domain new" })).toBeVisible();
    await expect(page.getByText("This is an updated domain vision")).toBeVisible();

    await page.reload();

    await expect(page.getByText("SUB", { exact: true })).toBeVisible();
    await expect(page.getByRole("heading", { name: "domain new" })).toBeVisible();
    await expect(page.getByText("This is an updated domain vision")).toBeVisible();
  });

  test("should close edit mode and not change anything", async ({ page }) => {
    const domainDetails = new DomainDetailsPage(page);

    await domainDetails.editDomainButton.click();
    await page.getByLabel("Short Key").fill("DontSave");
    await page.getByLabel("Name (Required)").fill("Do Not Save");
    await page.getByLabel("Vision").fill("Vision which should not be saved");
    await domainDetails.closeEditDomainButton.click();

    await expect(domainDetails.editDomainButton).toBeVisible();
    await expect(page.getByText("DontSave", { exact: true })).not.toBeVisible();
    await expect(page.getByRole("heading", { name: "Name not saved" })).not.toBeVisible();
    await expect(page.getByText("Vision which should not be saved", { exact: true })).not.toBeVisible();
  });

  test("should validate short key", async ({ page }) => {
    const domainDetailsPage = new DomainDetailsPage(page);
    await domainDetailsPage.editDomainButton.click();

    const shortKey = await page.getByLabel("Short Key");

    await shortKey.fill("0");
    await expect(page.getByText("Must not start with a number")).toBeVisible();
    await shortKey.fill("-");
    await expect(page.getByText("Must not start with hyphen")).toBeVisible();
    await shortKey.fill("TestTestTestTestTestTest");
    await expect(page.getByText("String must contain at most 16 character(s)")).toBeVisible();
    await shortKey.fill("?");
    await expect(page.getByText("Must be valid alphabetic character")).toBeVisible();
  });

  test("should validate name", async ({ page }) => {
    const domainDetailsPage = new DomainDetailsPage(page);
    await domainDetailsPage.editDomainButton.click();

    await page.getByLabel("Name (Required)").fill("");
    await expect(page.getByText("is required")).toBeVisible();
  });
});

test.describe("Not found", () => {
  test("should show error if domain does not exists", async ({ page }) => {
    await page.goto(`/domain/not-existing`);

    await expect(page.getByText("No such domain 'not-existing'")).toBeVisible();
    await expect(page.getByText("View all domains")).toBeVisible();
  });
});
