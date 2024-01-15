<template>
  <nav class="bg-blue-500 py-3.5 px-3 text-white sm:px-0" role="navigation">
    <div class="flex flex-wrap items-center space-x-5 pl-0 sm:pl-10">
      <div>
        <img alt="Contexture Logo" class="h-7 w-7" src="../../../assets/logo/light/light.png" />
      </div>
      <ul class="flex space-x-4">
        <li v-for="item in items" :key="item.to">
          <RouterLink
            :class="{ 'router-link-active': isActiveRoute(item) }"
            :to="item.to"
            class="text-blue-200 hover:text-gray-100"
          >
            {{ item.title }}
          </RouterLink>
        </li>
      </ul>
      <div class="flex !ml-auto !mr-5"> 
        <SignIn />
      </div>
    </div>
  </nav>
</template>

<script lang="ts" setup>
import { useI18n } from "vue-i18n";
import { useRoute } from "vue-router";
import { routes } from "~/routes";
import SignIn from "~/components/core/SignIn.vue"

const { t } = useI18n();
const route = useRoute();

interface NavbarItem {
  title: string;
  to: string;
  additionalActiveRouteMatches: string[];
}

const items: NavbarItem[] = [
  {
    title: t("navigation.domains"),
    to: "/",
    additionalActiveRouteMatches: [
      routes.Grid,
      routes.DomainDetails,
      routes.BoundedContextCanvas,
      routes.BoundedContextNamespaces,
    ],
  },
  {
    title: t("navigation.analytics"),
    to: "/analytics",
    additionalActiveRouteMatches: [],
  },
];

function isActiveRoute(item: NavbarItem): boolean {
  return item.additionalActiveRouteMatches.includes(route.name as string);
}
</script>

<style scoped>
.router-link-active {
  @apply font-bold text-white;
}
</style>
