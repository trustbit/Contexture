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

  test("should not edit a domain if edit mode is closed", async ({ page }) => {
    const domainDetails = new DomainDetailsPage(page);

    await domainDetails.editDomainButton.click();
    await page.getByLabel("Short Key").fill("SUB");
    await page.getByLabel("Name (Required)").fill("domain new");
    await page.getByLabel("Vision").fill("This is an updated domain vision");
    await domainDetails.closeEditDomainButton.click();

    await expect(domainDetails.editDomainButton).toBeVisible();
    await expect(page.getByText("SUB", { exact: true })).not.toBeVisible();
    await expect(page.getByRole("heading", { name: "domain new" })).not.toBeVisible();
    await expect(page.getByText("This is an updated domain vision", { exact: true })).not.toBeVisible();
  });
});

test.describe("Not found", () => {
  test("should show error if domain does not exists", async ({ page }) => {
    await page.goto(`/domain/not-existing`);

    await expect(page.getByText("No such domain 'not-existing'")).toBeVisible();
    await expect(page.getByText("View all domains")).toBeVisible();
  });
});
