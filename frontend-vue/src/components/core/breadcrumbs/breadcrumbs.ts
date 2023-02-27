import { isBoundedContextRoute } from "~/routes";
import { BoundedContext } from "~/types/boundedContext";
import { Domain, DomainId } from "~/types/domain";

export enum BreadcrumbType {
  DOMAIN,
  SUBDOMAIN,
  BOUNDED_CONTEXT,
}

export interface Breadcrumb {
  type: BreadcrumbType;
  text: string;
  id: string;
}

export function buildBreadcrumbs(
  routeId: string,
  routeName: string,
  allDomains: Domain[],
  boundedContexts: BoundedContext[]
): Breadcrumb[] {
  const boundedContextBreadcrumb: Breadcrumb[] = [];

  let domainId: DomainId | undefined;

  if (isBoundedContextRoute(routeName)) {
    const boundedContext: BoundedContext | undefined = boundedContexts.find((b) => b.id === routeId);

    if (!boundedContext) {
      return [];
    }

    boundedContextBreadcrumb.push({
      type: BreadcrumbType.BOUNDED_CONTEXT,
      text: boundedContext.name,
      id: boundedContext.id,
    });
    domainId = boundedContext.domain.id;
  } else {
    domainId = routeId;
  }

  const domainBreadcrumbs = buildDomainBreadcrumbs(domainId, allDomains, boundedContexts);

  return [...boundedContextBreadcrumb, ...domainBreadcrumbs].reverse();
}

function buildDomainBreadcrumbs(
  domainId: string | undefined,
  allDomains: Domain[],
  boundedContexts: BoundedContext[]
): Breadcrumb[] {
  if (!domainId) {
    return [];
  }

  const domain = allDomains.find((d) => d.id === domainId);

  if (!domain) {
    return [];
  }

  const breadcrumbs: Breadcrumb[] = [];

  if (domain.parentDomainId) {
    breadcrumbs.push({
      text: domain.name,
      type: BreadcrumbType.SUBDOMAIN,
      id: domain.id,
    });
    breadcrumbs.push(...buildDomainBreadcrumbs(domain.parentDomainId, allDomains, boundedContexts));
  } else {
    breadcrumbs.push({
      text: domain.name,
      type: BreadcrumbType.DOMAIN,
      id: domain.id,
    });
  }
  return breadcrumbs;
}
