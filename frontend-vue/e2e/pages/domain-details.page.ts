import { Locator, Page } from "@playwright/test";

export class DomainDetailsPage {
  readonly page: Page;
  readonly editDomainButton: Locator;
  readonly closeEditDomainButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.editDomainButton = page.getByRole("button", { name: "Edit Domain" });
    this.closeEditDomainButton = page.getByRole("button", { name: "Close edit domain" });
  }
}
