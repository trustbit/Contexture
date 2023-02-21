import { expect, test } from "@playwright/test";
import { BoundedContextCanvasPage } from "./pages/bounded-context-canvas.page";

let boundedContextId: string = "";
let domainId: string = "";

test.beforeAll(async ({ request }) => {
  const responseDomain = await request.post("/api/domains", {
    data: {
      name: "Test Domain",
    },
  });
  domainId = (await responseDomain.json()).id;
  expect(responseDomain.ok()).toBeTruthy();

  const response = await request.post(`/api/domains/${domainId}/boundedContexts`, {
    data: {
      name: "Test Bounded Context",
    },
  });

  boundedContextId = (await response.json()).id;
  expect(response.ok()).toBeTruthy();
});

test.afterAll(async ({ request }) => {
  await request.delete(`/api/boundedContexts/${boundedContextId}`);
  await request.delete(`/api/domains/${domainId}`);
});

test.beforeEach(async ({ page }) => {
  await page.goto(`/boundedContext/${boundedContextId}/canvas`);
});

test.describe("Edit Bounded Context", () => {
  test("should edit a bounded context", async ({ page }) => {
    const boundedContextCanvas = new BoundedContextCanvasPage(page);

    await boundedContextCanvas.editBoundedContextButton.click();
    await page.getByLabel("Short Key").fill("NEU");
    await page.getByLabel("Name (Required)").fill("Test Bounded Context Neu");
    await page.getByRole("button", { name: "save" }).click();

    await expect(boundedContextCanvas.editBoundedContextButton).toBeVisible();
    await expect(page.getByTestId("boundedContextKey")).toHaveText("NEU");
    await expect(page.getByTestId("boundedContextName")).toHaveText("Test Bounded Context Neu");

    await page.reload();

    await expect(page.getByTestId("boundedContextKey")).toBeVisible();
    await expect(page.getByTestId("boundedContextName")).toBeVisible();
  });

  test("should close edit mode and not change anything", async ({ page }) => {
    const boundedContextCanvas = new BoundedContextCanvasPage(page);

    const closeButton = page.getByRole("button", {
      name: "Close edit bounded context",
    });

    await boundedContextCanvas.editBoundedContextButton.click();
    await page.getByLabel("Short Key").fill("DontSave");
    await page.getByLabel("Name (Required)").fill("Do Not Save");
    await closeButton.click();

    await expect(boundedContextCanvas.editBoundedContextButton).toBeVisible();
    await page.reload();

    await expect(page.getByTestId("boundedContextKey")).not.toHaveText("DontSave");
    await expect(page.getByTestId("boundedContextName")).not.toHaveText("Do not save");
  });

  test("should validate short key", async ({ page }) => {
    const boundedContextCanvas = new BoundedContextCanvasPage(page);
    await boundedContextCanvas.editBoundedContextButton.click();

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
    const boundedContextCanvas = new BoundedContextCanvasPage(page);
    await boundedContextCanvas.editBoundedContextButton.click();

    await page.getByLabel("Name (Required)").fill("");
    await expect(page.getByText("is required")).toBeVisible();
  });
});

test.describe("Messages", () => {
  test("should be able to add and delete a message for each message consumed", async ({ page }) => {
    const addCommandLocator = page.getByTestId("addCommandHandled");
    const addEventHandledLocator = page.getByTestId("addEventHandled");
    const addQueryHandledLocator = page.getByTestId("addQueryHandled");

    await addCommandLocator.getByRole("button", { name: "add" }).click();
    await addCommandLocator.getByLabel("Name").fill("Test Command handled");
    await page.getByRole("button", { name: "add command" }).click();
    await expect(addCommandLocator).toBeVisible();
    await expect(page.getByText("Test Command handled")).toBeVisible();
    await page.getByRole("button", { name: "Delete command" }).click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();
    await expect(page.getByText("Test Command handled")).toHaveCount(0);

    await addEventHandledLocator.getByRole("button", { name: "add" }).click();
    await addEventHandledLocator.getByLabel("Name").fill("Test Event handled");
    await page.getByRole("button", { name: "add event" }).click();
    await expect(addEventHandledLocator).toBeVisible();
    await expect(page.getByText("Test Event handled")).toBeVisible();
    await page.getByRole("button", { name: "Delete event" }).click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();
    await expect(page.getByText("Test Event handled")).toHaveCount(0);

    await addQueryHandledLocator.getByRole("button", { name: "add" }).click();
    await addQueryHandledLocator.getByLabel("Name").fill("Test Query handled");
    await page.getByRole("button", { name: "add query" }).click();
    await expect(addQueryHandledLocator).toBeVisible();
    await expect(page.getByText("Test Query handled")).toBeVisible();
    await page.getByRole("button", { name: "Delete query" }).click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();
    await expect(page.getByText("Test Query handled")).toHaveCount(0);
  });

  test("should be able to add and delete a message for each message produced", async ({ page }) => {
    const addCommandLocator = page.getByTestId("addCommandSent");
    const addEventHandledLocator = page.getByTestId("addEventPublished");
    const addQueryHandledLocator = page.getByTestId("addQueryInvoked");

    await addCommandLocator.getByRole("button", { name: "add" }).click();
    await addCommandLocator.getByLabel("Name").fill("Test Command sent");
    await page.getByRole("button", { name: "add command" }).click();
    await expect(addCommandLocator).toBeVisible();
    await expect(page.getByText("Test Command sent")).toBeVisible();
    await page.getByRole("button", { name: "Delete command" }).click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();
    await expect(page.getByText("Test Command sent")).toHaveCount(0);

    await addEventHandledLocator.getByRole("button", { name: "add" }).click();
    await addEventHandledLocator.getByLabel("Name").fill("Test Event published");
    await page.getByRole("button", { name: "add event" }).click();
    await expect(addEventHandledLocator).toBeVisible();
    await expect(page.getByText("Test Event published")).toBeVisible();
    await page.getByRole("button", { name: "Delete event" }).click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();
    await expect(page.getByText("Test Event published")).toHaveCount(0);

    await addQueryHandledLocator.getByRole("button", { name: "add" }).click();
    await addQueryHandledLocator.getByLabel("Name").fill("Test Query invoked");
    await page.getByRole("button", { name: "add query" }).click();
    await expect(addQueryHandledLocator).toBeVisible();
    await expect(page.getByText("Test Query invoked")).toBeVisible();
    await page.getByRole("button", { name: "Delete query" }).click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();
    await expect(page.getByText("Test Query invoked")).toHaveCount(0);
  });
});

test.describe("Collaborators", () => {
  test("should be able to add and delete an inbound collaborator for bounded context", async ({ page }) => {
    const boundedContextCanvas = new BoundedContextCanvasPage(page);
    await boundedContextCanvas.addNewInboundCollaborator.click();
    await page.getByLabel("Bounded Context", { exact: true }).check();

    await page.getByPlaceholder("Select an option").fill("W");
    await page.getByText("Warehousing").click();
    await page.getByLabel("Description").fill("Test");
    await page.getByRole("button", { name: "add connection" }).click();

    await expect(page.getByRole("button", { name: "add new collaborator" }).first()).toBeVisible();
    await expect(page.getByTestId("collaborationDisplay").getByRole("heading", { name: "Warehousing" })).toBeVisible();
    await expect(page.getByTestId("collaborationDisplay").getByText("In Domain Inventory")).toBeVisible();

    await page.getByRole("button", { name: "Delete collaborator" }).click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();

    await expect(
      page.getByTestId("collaborationDisplay").getByRole("heading", { name: "Warehousing" })
    ).not.toBeVisible();
    await expect(page.getByTestId("collaborationDisplay").getByText("In Domain Inventory")).not.toBeVisible();
  });

  test("should be able to add and delete an inbound collaborator for domain", async ({ page }) => {
    const boundedContextCanvas = new BoundedContextCanvasPage(page);
    await boundedContextCanvas.addNewInboundCollaborator.click();
    await page.getByLabel("Domain", { exact: true }).check();

    await page.getByPlaceholder("Select an option").fill("Restaurant Experience");
    await page.getByText("Restaurant Experience").click();
    await page.getByRole("button", { name: "add connection" }).click();

    await expect(page.getByRole("button", { name: "add new collaborator" }).first()).toBeVisible();
    await expect(
      page.getByTestId("collaborationDisplay").getByRole("heading", { name: "Restaurant Experience" })
    ).toBeVisible();
    await expect(page.getByTestId("collaborationDisplay").getByText("Domain")).toBeVisible();

    await page.getByRole("button", { name: "Delete collaborator" }).click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();

    await expect(
      page.getByTestId("collaborationDisplay").getByRole("heading", { name: "Restaurant Experience" })
    ).not.toBeVisible();
    await expect(page.getByTestId("collaborationDisplay").getByText("Domain")).not.toBeVisible();
  });

  test("should be able to add and delete an inbound collaborator for external system", async ({ page }) => {
    const boundedContextCanvas = new BoundedContextCanvasPage(page);
    await boundedContextCanvas.addNewInboundCollaborator.click();
    await page.getByLabel("External System", { exact: true }).check();

    await page.getByLabel("Collaborator").fill("New external system");
    await page.getByRole("button", { name: "add connection" }).click();

    await expect(page.getByRole("button", { name: "add new collaborator" }).first()).toBeVisible();
    await expect(
      page.getByTestId("collaborationDisplay").getByRole("heading", { name: "New external system" })
    ).toBeVisible();
    await expect(page.getByTestId("collaborationDisplay").getByText("External System", { exact: true })).toBeVisible();

    await page.getByRole("button", { name: "Delete collaborator" }).click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();

    await expect(
      page.getByTestId("collaborationDisplay").getByRole("heading", { name: "New external system" })
    ).not.toBeVisible();
    await expect(
      page.getByTestId("collaborationDisplay").getByText("External System", { exact: true })
    ).not.toBeVisible();
  });

  test("should be able to add and delete an inbound collaborator for frontend", async ({ page }) => {
    const boundedContextCanvas = new BoundedContextCanvasPage(page);
    await boundedContextCanvas.addNewInboundCollaborator.click();
    await page.getByLabel("Frontend", { exact: true }).check();

    await page.getByLabel("Collaborator").fill("New Frontend");
    await page.getByRole("button", { name: "add connection" }).click();

    await expect(page.getByRole("button", { name: "add new collaborator" }).first()).toBeVisible();
    await expect(page.getByTestId("collaborationDisplay").getByRole("heading", { name: "New Frontend" })).toBeVisible();
    await expect(page.getByTestId("collaborationDisplay").getByText("Frontend", { exact: true })).toBeVisible();

    await page.getByRole("button", { name: "Delete collaborator" }).click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();

    await expect(
      page.getByTestId("collaborationDisplay").getByRole("heading", { name: "New Frontend" })
    ).not.toBeVisible();
    await expect(page.getByTestId("collaborationDisplay").getByText("Frontend", { exact: true })).not.toBeVisible();
  });

  test("should be able to add and delete an outbound collaborator for bounded context", async ({ page }) => {
    const boundedContextCanvas = new BoundedContextCanvasPage(page);
    await boundedContextCanvas.addNewOutboundCollaborator.click();
    await page.getByLabel("Bounded Context", { exact: true }).check();

    await page.getByPlaceholder("Select an option").fill("W");
    await page.getByText("Warehousing").click();
    await page.getByLabel("Description").fill("Test");
    await page.getByRole("button", { name: "add connection" }).click();

    await expect(page.getByRole("button", { name: "add new collaborator" }).last()).toBeVisible();
    await expect(page.getByTestId("collaborationDisplay").getByRole("heading", { name: "Warehousing" })).toBeVisible();
    await expect(page.getByTestId("collaborationDisplay").getByText("In Domain Inventory")).toBeVisible();

    await page.getByRole("button", { name: "Delete collaborator" }).click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();

    await expect(
      page.getByTestId("collaborationDisplay").getByRole("heading", { name: "Warehousing" })
    ).not.toBeVisible();
    await expect(page.getByTestId("collaborationDisplay").getByText("In Domain Inventory")).not.toBeVisible();
  });

  test("should be able to add and delete an outbound collaborator for domain", async ({ page }) => {
    const boundedContextCanvas = new BoundedContextCanvasPage(page);
    await boundedContextCanvas.addNewOutboundCollaborator.click();
    await page.getByLabel("Domain", { exact: true }).check();

    await page.getByPlaceholder("Select an option").fill("Restaurant Experience");
    await page.getByText("Restaurant Experience").click();
    await page.getByRole("button", { name: "add connection" }).click();

    await expect(page.getByRole("button", { name: "add new collaborator" }).last()).toBeVisible();
    await expect(
      page.getByTestId("collaborationDisplay").getByRole("heading", { name: "Restaurant Experience" })
    ).toBeVisible();
    await expect(page.getByTestId("collaborationDisplay").getByText("Domain")).toBeVisible();

    await page.getByRole("button", { name: "Delete collaborator" }).click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();

    await expect(
      page.getByTestId("collaborationDisplay").getByRole("heading", { name: "Restaurant Experience" })
    ).not.toBeVisible();
    await expect(page.getByTestId("collaborationDisplay").getByText("Domain")).not.toBeVisible();
  });

  test("should be able to add and delete an outbound collaborator for external system", async ({ page }) => {
    const boundedContextCanvas = new BoundedContextCanvasPage(page);
    await boundedContextCanvas.addNewOutboundCollaborator.click();
    await page.getByLabel("External System", { exact: true }).check();

    await page.getByLabel("Collaborator").fill("New external system");
    await page.getByRole("button", { name: "add connection" }).click();

    await expect(page.getByRole("button", { name: "add new collaborator" }).last()).toBeVisible();
    await expect(
      page.getByTestId("collaborationDisplay").getByRole("heading", { name: "New external system" })
    ).toBeVisible();
    await expect(page.getByTestId("collaborationDisplay").getByText("External System", { exact: true })).toBeVisible();

    await page.getByRole("button", { name: "Delete collaborator" }).click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();

    await expect(
      page.getByTestId("collaborationDisplay").getByRole("heading", { name: "New external system" })
    ).not.toBeVisible();
    await expect(
      page.getByTestId("collaborationDisplay").getByText("External System", { exact: true })
    ).not.toBeVisible();
  });

  test("should be able to add and delete an outbound collaborator for frontend", async ({ page }) => {
    await page.getByRole("button", { name: "add new collaborator" }).last().click();
    await page.getByLabel("Frontend", { exact: true }).check();

    await page.getByLabel("Collaborator").fill("New Frontend");
    await page.getByRole("button", { name: "add connection" }).click();

    await expect(page.getByRole("button", { name: "add new collaborator" }).last()).toBeVisible();
    await expect(page.getByTestId("collaborationDisplay").getByRole("heading", { name: "New Frontend" })).toBeVisible();
    await expect(page.getByTestId("collaborationDisplay").getByText("Frontend", { exact: true })).toBeVisible();

    await page.getByRole("button", { name: "Delete collaborator" }).click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();

    await expect(
      page.getByTestId("collaborationDisplay").getByRole("heading", { name: "New Frontend" })
    ).not.toBeVisible();
    await expect(page.getByTestId("collaborationDisplay").getByText("Frontend", { exact: true })).not.toBeVisible();
  });
});

test.describe("Ubiquitous language", () => {
  test("should be able to add and delete a term", async ({ page }) => {
    const boundedContextCanvas = new BoundedContextCanvasPage(page);

    await boundedContextCanvas.addNewTerm.click();
    await page.getByLabel("Domain term (Required)").fill("New Test Term");
    await page.getByLabel("Description").fill("This is a test term");
    await boundedContextCanvas.addNewTerm.click();

    await expect(page.getByRole("button", { name: "add new term" })).toBeVisible();
    await expect(page.getByText("New Test Term")).toBeVisible();
    await expect(page.getByText("This is a test term")).not.toBeVisible();
    await page.getByRole("button", { name: "New Test Term" }).click();
    await expect(page.getByText("This is a test term")).toBeVisible();

    await page.getByRole("button", { name: "Test" }).getByRole("button").click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();
    await expect(page.getByText("New Test Term")).toHaveCount(0);
    await expect(page.getByText("This is a test term")).not.toBeVisible();
  });
});

test.describe("Business Decisions", () => {
  test("should be able to add and delete a business decision", async ({ page }) => {
    const boundedContextCanvas = new BoundedContextCanvasPage(page);

    await boundedContextCanvas.addNewBusinessDecision.click();
    await page.getByLabel("Business decision name (Required)").fill("New Test business decisions");
    await page.getByLabel("Description").fill("This is a test business decision");
    await boundedContextCanvas.addNewBusinessDecision.click();

    await expect(page.getByRole("button", { name: "add new term" })).toBeVisible();
    await expect(page.getByText("New Test business decisions")).toBeVisible();
    await expect(page.getByText("This is a test business decision")).not.toBeVisible();
    await page.getByRole("button", { name: "New Test business decisions" }).click();
    await expect(page.getByText("This is a test business decision")).toBeVisible();

    await page.getByRole("button", { name: "Test" }).getByRole("button").click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();
    await expect(page.getByText("New Test business decisions")).toHaveCount(0);
    await expect(page.getByText("This is a test business decision")).not.toBeVisible();
  });
});

test.describe("Domain Role", () => {
  test("should be able to add and delete a domain role", async ({ page }) => {
    const boundedContextCanvas = new BoundedContextCanvasPage(page);

    await boundedContextCanvas.addNewDomainRole.click();
    await page.getByLabel("Domain role name (Required)").fill("Test domain role");
    await page.getByLabel("Description").fill("This is a new domain role");
    await page.getByRole("button", { name: "add domain role" }).click();
    await page.getByTestId("deleteDomainRole").click();
    await page.getByRole("button", { name: "Delete" }).click();
  });

  test("should be able to add and delete a domain role from a pre-defined list", async ({ page }) => {
    const boundedContextCanvas = new BoundedContextCanvasPage(page);

    await boundedContextCanvas.addDomainRoleFromTemplate.click();
    await page
      .getByLabel(
        "Specification ModelProduces a document describing a job/request that needs to be performed. Example: Advertising Campaign Builder"
      )
      .check();
    await page.getByRole("button", { name: "add this domain role" }).click();
    await page.getByRole("button", { name: "Specification Model" }).click();
    await expect(
      page.getByText("Produces a document describing a job/request that needs to be performed.")
    ).toBeVisible();
    await page.getByTestId("deleteDomainRole").click();
    await page.getByRole("button", { name: "Delete" }).click();
  });
});
