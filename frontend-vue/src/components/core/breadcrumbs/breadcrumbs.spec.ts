import { describe, expect, it } from "vitest";
import { Breadcrumb } from "./breadcrumbs";
import { BreadcrumbType, buildBreadcrumbs } from "./breadcrumbs";

describe("domain breadcrumbs", () => {
  it("no domains given", () => {
    const breadcrumbs = buildBreadcrumbs("1", "domain", [], []);

    expect(breadcrumbs).length(0);
  });

  it("domain has no parent domain", () => {
    const breadcrumbs: Breadcrumb[] = buildBreadcrumbs(
      "1",
      "domain",
      [
        {
          id: "1",
          name: "Domain",
          subdomains: [],
          boundedContexts: [],
        },
      ],
      []
    );

    expect(breadcrumbs).length(1);
    expect(breadcrumbs[0]).toEqual({
      type: BreadcrumbType.DOMAIN,
      text: "Domain",
      id: "1",
    });
  });

  it("domain has a parent domain", () => {
    const breadcrumbs: Breadcrumb[] = buildBreadcrumbs(
      "2",
      "domain",
      [
        {
          id: "1",
          name: "Domain",
          subdomains: [],
          boundedContexts: [],
        },
        {
          id: "2",
          name: "Subdomain",
          parentDomainId: "1",
          subdomains: [],
          boundedContexts: [],
        },
      ],
      []
    );

    expect(breadcrumbs).length(2);
    expect(breadcrumbs[0]).toEqual({
      type: BreadcrumbType.DOMAIN,
      text: "Domain",
      id: "1",
    });
    expect(breadcrumbs[1]).toEqual({
      type: BreadcrumbType.SUBDOMAIN,
      text: "Subdomain",
      id: "2",
    });
  });

  it("domain has multiple nested domains", () => {
    const breadcrumbs: Breadcrumb[] = buildBreadcrumbs(
      "3",
      "domain",
      [
        {
          id: "1",
          name: "Domain",
          subdomains: [],
          boundedContexts: [],
        },
        {
          id: "2",
          name: "Subdomain",
          parentDomainId: "1",
          subdomains: [],
          boundedContexts: [],
        },
        {
          id: "3",
          name: "SubSubDomain",
          parentDomainId: "2",
          subdomains: [],
          boundedContexts: [],
        },
      ],
      []
    );

    expect(breadcrumbs).length(3);
    expect(breadcrumbs[0]).toEqual({
      type: BreadcrumbType.DOMAIN,
      text: "Domain",
      id: "1",
    });
    expect(breadcrumbs[1]).toEqual({
      type: BreadcrumbType.SUBDOMAIN,
      text: "Subdomain",
      id: "2",
    });
    expect(breadcrumbs[2]).toEqual({
      type: BreadcrumbType.SUBDOMAIN,
      text: "SubSubDomain",
      id: "3",
    });
  });
});
