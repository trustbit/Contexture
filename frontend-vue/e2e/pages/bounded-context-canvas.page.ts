import { Locator, Page } from "@playwright/test";

export class BoundedContextCanvasPage {
  readonly page: Page;
  readonly editBoundedContextButton: Locator;
  readonly addNewInboundCollaborator: Locator;
  readonly addNewOutboundCollaborator: Locator;
  readonly addNewBusinessDecision: Locator;
  readonly addNewTerm: Locator;
  readonly addNewDomainRole: Locator;
  readonly addDomainRoleFromTemplate: Locator;

  constructor(page: Page) {
    this.page = page;
    this.editBoundedContextButton = page.getByRole("button", {
      name: "Edit bounded context",
    });
    this.addNewInboundCollaborator = page.getByRole("button", { name: "add new collaborator" }).first();
    this.addNewOutboundCollaborator = page.getByRole("button", { name: "add new collaborator" }).last();
    this.addNewBusinessDecision = page.getByRole("button", { name: "add business decision" });
    this.addNewTerm = page.getByRole("button", { name: "add new term" });
    this.addNewDomainRole = page.getByRole("button", { name: "add new domain role" });
    this.addDomainRoleFromTemplate = page.getByRole("button", { name: "choose domain role from pre-defined list" });
  }
}
