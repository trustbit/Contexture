import { expect, test } from "@playwright/test";
import { randomString } from "./util/test.utils";

test.beforeEach(async ({ page }) => {
  await page.goto(`/`);
});

test.describe("Grid", () => {
  test("show should domain cards", async ({ page }) => {
    await expect(page.getByRole("heading", { name: "All Domains Overview" })).toBeVisible();

    await expect(page.getByText("Restaurant Experience", { exact: true })).toBeVisible();
    await expect(page.getByText("Serving guests with the best meals in our restaurant")).toBeVisible();
    await expect(page.getByText("RES", { exact: true })).toBeVisible();

    await expect(page.getByText("Inventory", { exact: true })).toBeVisible();
    await expect(page.getByText("Manage inventory of our restaurants")).toBeVisible();

    await expect(await page.getByTestId("move-Restaurant Experience")).toBeVisible();
    await expect(await page.getByTestId("delete-Restaurant Experience")).toBeVisible();
    await expect(await page.getByTestId("move-Inventory")).toBeVisible();
    await expect(await page.getByTestId("delete-Inventory")).toBeVisible();
  });

  test("should show/hide namespaces", async ({ page }) => {
    await expect(page.getByText("Food Preparation")).not.toBeVisible();
    await page.getByRole("switch", { name: "show bounded contexts and subdomains" }).click();
    await expect(page.getByText("Food Preparation")).toBeVisible();
    await page.getByRole("switch", { name: "show bounded contexts and subdomains" }).click();
    await expect(page.getByText("Food Preparation")).not.toBeVisible();
  });

  test("should create and delete a domain", async ({ page }) => {
    const newDomainName = "New Test Domain" + randomString();
    await page.getByRole("button", { name: "create Domain" }).click();
    await page.getByLabel("Name of the Domain (Required)").fill(newDomainName);
    await page.getByRole("dialog").getByRole("button", { name: "create domain" }).click();

    await expect(page).toHaveURL(/domain/);
    await page.goBack();
    await expect(page.getByText(newDomainName)).toBeVisible();

    await expect(
      page.getByRole("heading", { name: `Do you really want to delete '${newDomainName}'` })
    ).not.toBeVisible();
    await page.getByTestId(`delete-${newDomainName}`).click();
    await expect(page.getByRole("heading", { name: `Do you really want to delete '${newDomainName}'` })).toBeVisible();
    await page.getByRole("button", { name: "Yes, delete domain" }).click();
    await expect(
      page.getByRole("heading", { name: `Do you really want to delete '${newDomainName}'` })
    ).not.toBeVisible();
    await expect(page.getByText(newDomainName)).toHaveCount(0);
  });
});

test.describe("List", () => {
  test("should show domain list", async ({ page }) => {
    await page.getByRole("tab", { name: "List" }).click();

    await expect(page.getByPlaceholder("Search bounded contexts")).toBeVisible();
    await expect(page.getByRole("switch", { name: "show description" })).toBeVisible();
    await expect(page.getByRole("switch", { name: "show namespaces" })).toBeVisible();
    await expect(page.getByRole("heading", { name: "All Domains List" })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Restaurant Experience" })).toBeVisible();
    await expect(page.getByText("RES", { exact: true })).toBeVisible();
    await expect(page.getByText("6 bounded contexts")).toBeVisible();
    await expect(page.getByText("Restaurant Experience Deliverydirect child of parent domain")).toBeVisible();
    await expect(page.getByRole("heading", { name: "Inventory" })).toBeVisible();
    await expect(page.getByText("3 bounded contexts")).toBeVisible();
  });

  test("should show description and namespaces", async ({ page }) => {
    await page.getByRole("tab", { name: "List" }).click();

    await expect(page.getByText("Serving guests with the best meals in our restaurants")).not.toBeVisible();
    await expect(page.getByText("In charge of delivering cooked food & drinks to the guests table")).not.toBeVisible();
    await expect(page.getByText("Namespaces", { exact: true })).not.toBeVisible();
    await page.getByRole("switch", { name: "show description" }).click();
    await page.getByRole("switch", { name: "show namespaces" }).click();
    await expect(page.getByText("Serving guests with the best meals in our restaurants")).toBeVisible();
    await expect(page.getByText("In charge of delivering cooked food & drinks to the guests table")).toBeVisible();
    await expect(page.getByText("Namespaces")).toHaveCount(9);
    await page.getByRole("switch", { name: "show description" }).click();
    await page.getByRole("switch", { name: "show namespaces" }).click();
    await expect(page.getByText("Serving guests with the best meals in our restaurants")).not.toBeVisible();
    await expect(page.getByText("In charge of delivering cooked food & drinks to the guests table")).not.toBeVisible();
    await expect(page.getByText("Namespaces", { exact: true })).not.toBeVisible();
  });
});
