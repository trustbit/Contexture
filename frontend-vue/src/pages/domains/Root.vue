<template>
  <div class="px-3 pt-5 pb-8 lg:px-0">
    <TabGroup @change="onTabChange" :selected-index="selectedViewTab">
      <div class="justify-end sm:flex">
        <TabList
          class="flex divide-x divide-blue-500 overflow-hidden rounded-2xl border border-blue-500 text-xs text-blue-500 sm:w-fit lg:mr-10"
        >
          <Tab
            v-for="tabListOption of tabOptions"
            :key="tabListOption.text"
            class="inline-flex flex-grow items-center justify-center px-3 py-1.5 hover:bg-blue-100 ui-selected:bg-blue-500 ui-selected:text-white"
            :id="`tab-${tabListOption.id}`"
          >
            <component :is="tabListOption.icon" aria-hidden="true" class="mr-1 h-4 w-4" />
            <span>{{ tabListOption.text }}</span>
          </Tab>
        </TabList>
      </div>

      <TabPanels class="mx-auto mt-5 lg:container">
        <TabPanel v-for="tabPanel of tabPanelViews" :key="tabPanel.id">
          <Disclosure>
            <component :is="tabPanel.component"></component>
          </Disclosure>
        </TabPanel>
      </TabPanels>
    </TabGroup>
  </div>
</template>

<script lang="ts" setup>
import { Disclosure, Tab, TabGroup, TabList, TabPanel, TabPanels } from "@headlessui/vue";
import { useRouteQuery } from "@vueuse/router";
import { useRoute } from "vue-router";
import { computed, Ref, watchEffect } from "vue";
import { useI18n } from "vue-i18n";
import BubbleView from "~/pages/domains/BubbleView.vue";
import GridView from "~/pages/domains/GridView.vue";
import ListView from "~/pages/domains/ListView.vue";
import IconsMaterialSymbolsApps from "~icons/material-symbols/apps";
import IconsMaterialSymbolsCalendarViewWeekOutline from "~icons/material-symbols/calendar-view-week-outline";
import IconsMaterialSymbolsWorkspaceOutline from "~icons/material-symbols/workspaces-outline";

interface TabListOption {
  id: ViewOption;
  icon: any;
  text: string;
}

interface TabPanelOption {
  id: ViewOption;
  component: any;
}

enum ViewOption {
  GRID = "grid",
  BUBBLE = "bubble",
  LIST = "list",
}

const { t } = useI18n();
const route = useRoute();
const defaultViewOption = route.path === "/search" ? ViewOption.LIST : ViewOption.GRID;
const queryParamType: Ref<string> = useRouteQuery<ViewOption>("type", defaultViewOption, { mode: "push" });
const selectedViewTab = computed(() => tabOptions.findIndex((t) => t.id === queryParamType.value) || 0);

const tabOptions: TabListOption[] = [
  {
    id: ViewOption.GRID,
    icon: IconsMaterialSymbolsApps,
    text: t("domains.buttons.grid"),
  },
  {
    id: ViewOption.BUBBLE,
    icon: IconsMaterialSymbolsWorkspaceOutline,
    text: t("domains.buttons.bubble"),
  },
  {
    id: ViewOption.LIST,
    icon: IconsMaterialSymbolsCalendarViewWeekOutline,
    text: t("domains.buttons.list"),
  },
];

const tabPanelViews: TabPanelOption[] = [
  {
    id: ViewOption.GRID,
    component: GridView,
  },
  {
    id: ViewOption.BUBBLE,
    component: BubbleView,
  },
  {
    id: ViewOption.LIST,
    component: ListView,
  },
];

function onTabChange(newSelectedTab: number): void {
  queryParamType.value = tabOptions[newSelectedTab].id;
}
</script>
