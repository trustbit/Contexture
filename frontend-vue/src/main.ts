import { createPinia } from "pinia";
import { createApp } from "vue";
import { createI18n } from "vue-i18n";
import { createRouter, createWebHistory } from "vue-router";
import App from "./App.vue";
import routes from "./routes";
import "./styles/main.css";

const messages = Object.fromEntries(
  Object.entries(import.meta.glob<{ default: any }>("../locales/*.json", { eager: true })).map(([key, value]) => {
    return [key.slice(11, -5), value.default];
  })
);

const i18n = createI18n({
  legacy: false,
  locale: "en",
  messages,
});
const pinia = createPinia();

const app = createApp(App);
const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
  scrollBehavior() {
    // always scroll to top
    return { top: 0 };
  },
});
app.use(router);
app.use(i18n);
app.use(pinia);
app.mount("#app");
