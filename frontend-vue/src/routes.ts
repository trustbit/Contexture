import { RouteRecordName } from "vue-router";

export type ContextureRoutes = string;

export const boundedContextRoutes: Record<ContextureRoutes, string> = {
  BoundedContextCanvas: "BoundedContextCanvas",
  BoundedContextNamespaces: "BoundedContextNamespaces",
};

export const routes: Record<ContextureRoutes, string> = {
  Domains: "Domains",
  DomainDetails: "DomainDetails",
  Analytics: "Analytics",
  Search: "Search",
  ...boundedContextRoutes,
};

const DomainGrid = () => import("./pages/domains/Domains.vue");
const Search = () => import("./pages/search/Search.vue");
const DomainDetails = () => import("./pages/domain-details/DomainDetails.vue");
const BoundedContextCanvas = () => import("./pages/bounded-context/BoundedContextCanvas.vue");
const BoundedContextNamespaces = () => import("./pages/bounded-context/BoundedContextNamespaces.vue");
const Analytics = () => import("./pages/analytics/Analytics.vue");
const SignInCallback = () => import("./components/core/auth/SignInCallback.vue");

export default [
  {
    name: routes.Domains,
    path: "/",
    component: DomainGrid,
  },
  {
    name: routes.DomainDetails,
    path: "/domain/:id",
    component: DomainDetails,
  },
  {
    name: routes.BoundedContextCanvas,
    path: "/boundedContext/:id/canvas",
    component: BoundedContextCanvas,
  },
  {
    name: routes.BoundedContextNamespaces,
    path: "/boundedContext/:id/namespaces",
    component: BoundedContextNamespaces,
  },
  {
    name: routes.Analytics,
    path: "/analytics",
    component: Analytics,
  },
  {
    name: routes.Search,
    path: "/search",
    component: Search,
  },
  {
    name: "signinCallback",
    path: "/signinCallback",
    component: SignInCallback,
  },
  { path: "/:pathMatch(.*)*", redirect: { name: routes.Domains } },
];

export const isBoundedContextRoute = (route: RouteRecordName | null | undefined): boolean =>
  !!route && boundedContextRoutes && route in boundedContextRoutes;
