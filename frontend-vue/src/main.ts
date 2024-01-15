import { createPinia } from "pinia";
import { createApp } from "vue";
import { createI18n, I18nOptions } from "vue-i18n";
import { createRouter, createWebHistory } from "vue-router";
import App from "./App.vue";
import routes from "./routes";
import "./styles/main.css";
import { Bubble } from "~/visualisations/Bubble";
import { HierarchicalEdge } from "~/visualisations/HierarchicalEdge";
import { Sunburst } from "~/visualisations/Sunburst";
import { getOidcConfiguration } from "~/stores/auth";

const messages = Object.fromEntries(
  Object.entries(import.meta.glob<{ default: any }>("../locales/*.json", { eager: true })).map(([key, value]) => {
    return [key.slice(11, -5), value.default];
  })
) as I18nOptions["messages"];

export const i18n = createI18n({
  legacy: false,
  locale: "en",
  messages,
});


getOidcConfiguration().then(securityConfiguration => {
  const pinia = createPinia();
  const app = createApp(App);
  app.provide("oidcConfiguration", securityConfiguration)

  const router = createRouter({
    history: createWebHistory(import.meta.env.BASE_URL),
    routes,
    scrollBehavior() {
      // always scroll to top
      return { top: 0 };
    },
  });

  customElements.define("bubble-visualization", Bubble);
  customElements.define("hierarchical-edge", HierarchicalEdge);
  customElements.define("visualization-sunburst", Sunburst);

  app.use(router);
  app.use(i18n);
  app.use(pinia);
  app.mount("#app");
})
