<template>
  <div class="sm:flex sm:h-[calc(100vh-56px)]">
    <div class="h-full border-r border-r-blue-100 sm:w-1/4">
      <div class="flex h-full flex-grow flex-col overflow-y-scroll sm:flex">
        <div class="max-h-60 overflow-y-scroll border sm:h-2/3 sm:max-h-full">
          <ContextureAccordionItem class="space-y-2" :default-open="true">
            <template #title>
              <div class="flex justify-between pr-2 align-middle sm:pr-4">
                <span>{{ t("search.namespaces.search.title") }}</span>
                <ContextureBadge
                  mode="light"
                  size="sm"
                  :color="namespaceSearchTerm ? 'orange' : 'teal'"
                  variant="filled"
                  >{{ uniqueNamespaceNames.size }}
                </ContextureBadge>
              </div>
            </template>
            <template #default>
              <div class="mr-2">
                <ContextureSearch
                  :placeholder="t('search.namespaces.search.placeholder')"
                  :aria-label="t('search.namespaces.search.aria_label')"
                  class="mb-4"
                  v-model="namespaceSearchTerm"
                />

                <ul v-for="(namespace, index) of uniqueNamespaceNames" :key="namespace">
                  <ContextureListItem>
                    <template #title>
                      <ContexturePopover placement="bottom-end" v-model:open="popoverShow[index]">
                        <template #button>
                          <button
                            class="mr-2 flex items-center hover:text-gray-800"
                            :aria-label="`Add filter for ${namespace}`"
                            tabindex="-1"
                          >
                            <Icon:materialSymbols:add class="mr-2" aria-hidden="true" />
                            <span class="text-sm leading-7">{{ namespace }}</span>
                          </button>
                        </template>
                        <template #content>
                          <div class="w-[200px] py-2 sm:w-[500px]">
                            <ContextureAddFilterPopoverContent
                              :namespace-name="namespace"
                              :labels="labelNamesForNamespace[namespace]"
                              @add="(label) => addFilter(index, label)"
                            />
                          </div>
                        </template>
                      </ContexturePopover>
                    </template>
                  </ContextureListItem>
                </ul>
              </div>
            </template>
          </ContextureAccordionItem>
        </div>
        <div class="flex-grow overflow-y-scroll border p-4">
          <div>
            <h2 class="text-base font-bold">{{ t("search.presentation_mode") }}</h2>
          </div>
          <div class="mt-4">
            <h3 class="text-sm font-bold">{{ t("search.text_based") }}</h3>
            <ContextureViewSwitcher
              :options="textBasePresentationModes"
              :model-value="options.selectedTextPresentationMode"
              @update:model-value="updateSelectedTextPresentationMode"
            />
          </div>

          <div class="mt-4">
            <h3 class="text-sm font-bold">{{ t("search.visualisations") }}</h3>
            <ContextureViewSwitcher
              :options="visualisationModes"
              :model-value="options.selectedVisualization"
              @update:model-value="updateSelectedVisualisation"
            />
          </div>
        </div>
      </div>
    </div>
    <div class="h-full overflow-y-scroll bg-gray-100 sm:w-3/4">
      <div class="text-center">
        <div v-if="options.selectedVisualization === 0" class="h-full">
          <div class="invisible">{{ t("search.sunburst") }}</div>
          <visualization-sunburst mode="filtered" />
        </div>
        <div v-if="options.selectedVisualization === 1" class="h-full">
          <div class="invisible">{{ t("search.sunburst_highlighted") }}</div>
          <visualization-sunburst mode="highlighted" />
        </div>
        <div v-if="options.selectedVisualization === 2" class="h-full">
          <div class="invisible">{{ t("search.hierarchical_edge") }}</div>
          <hierarchical-edge v-if="options.selectedVisualization === 2" />
        </div>
      </div>

      <div v-if="options.selectedTextPresentationMode > -1" class="h-full">
        <ContextureActiveFilters
          :active-filters="activeFilters"
          @clear-filters="onClearFilters"
          @delete-filter="onDeleteFilter"
        />

        <div v-if="options.selectedTextPresentationMode >= 0" class="h-full overflow-y-scroll p-1 sm:p-4">
          <div class="mb-4">
            <span class="font-bold">{{ domainCount }}&nbsp;</span>
            <span>{{ t("common.domain", domainCount) }}&nbsp;</span>
            <span>{{ t("common.with") }}&nbsp;</span>
            <span class="font-bold">{{ data?.length }}&nbsp;</span>
            <span>{{ t("common.bounded_context", domainCount) }}&nbsp;</span>
          </div>

          <div class="space-y-4">
            <div v-for="item in domainsWithBoundedContexts" :key="item.domainId">
              <div class="rounded bg-white p-1 sm:p-4">
                <h2 class="text-lg font-bold">
                  {{ domainIdToDomainName[item.domainId] }}
                </h2>
                <div
                  class="mt-4 flex flex-col gap-y-2 sm:grid sm:gap-2"
                  :class="[
                    {
                      'sm:grid-cols-2': options.selectedTextPresentationMode === 0,
                    },
                  ]"
                >
                  <div v-for="boundedContext of item.contexts" :key="boundedContext.id">
                    <ContextureBoundedContextCard
                      :bounded-context="boundedContext"
                      :show-namespaces="true"
                      :show-actions="false"
                      :condensed="options.selectedTextPresentationMode === 1"
                    />
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useLocalStorage } from "@vueuse/core";
import { storeToRefs } from "pinia";
import { computed, onMounted, Ref, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import ContextureBoundedContextCard from "~/components/bounded-context/ContextureBoundedContextCard.vue";
import ContextureAccordionItem from "~/components/primitives/accordion/ContextureAccordionItem.vue";
import ContextureBadge from "~/components/primitives/badge/ContextureBadge.vue";
import ContextureSearch from "~/components/primitives/input/ContextureSearch.vue";
import ContextureListItem from "~/components/primitives/list/ContextureListItem.vue";
import ContexturePopover from "~/components/primitives/popover/ContexturePopover.vue";
import ContextureViewSwitcher from "~/components/primitives/viewswitcher/ContextureViewSwitcher.vue";
import { ActiveFilter } from "~/components/search/activeFilter";
import ContextureActiveFilters from "~/components/search/ContextureActiveFilters.vue";
import ContextureAddFilterPopoverContent from "~/components/search/ContextureAddFilter.vue";
import { useFetch } from "~/composables/useFetch";
import { useDomainsStore } from "~/stores/domains";
import { useNamespaces } from "~/stores/namespaces";
import { BoundedContext } from "~/types/boundedContext";
import { Domain } from "~/types/domain";
import { Namespace } from "~/types/namespace";

interface SearchSettings {
  selectedTextPresentationMode: number;
  selectedVisualization: number;
}

const { t } = useI18n();
const { namespaces } = storeToRefs(useNamespaces());
const { allDomains } = storeToRefs(useDomainsStore());
const router = useRouter();
const route = useRoute();

const options: Ref<SearchSettings> = useLocalStorage<SearchSettings>("settings.search.presentation", {
  selectedTextPresentationMode: 0,
  selectedVisualization: -1,
});

const namespaceSearchTerm = ref<string>("");

const uniqueNamespaceNames = computed(
  () =>
    new Set(
      namespaces.value
        .map((n) => n.name)
        .filter((n) => n.toLowerCase().includes(namespaceSearchTerm.value.toLowerCase()))
        .sort((a, b) => a.localeCompare(b))
    )
);

const activeFilters = ref<ActiveFilter[]>([]);

onMounted(() => {
  for (const key in route.query) {
    const value = route.query[key];
    if (Array.isArray(value)) {
      value.forEach((v) => {
        activeFilters.value = [
          ...activeFilters.value,
          {
            key: key,
            value: v as string,
          },
        ];
      });
    } else {
      activeFilters.value = [
        ...activeFilters.value,
        {
          key: key,
          value: route.query[key] as string,
        },
      ];
    }
  }
});

function addFilter(index: number, event: { key?: string; value?: string }): void {
  if (event.key) {
    activeFilters.value = [
      ...activeFilters.value,
      {
        key: "Label.Name",
        value: event.key,
      },
    ];
  }
  if (event.value) {
    activeFilters.value = [
      ...activeFilters.value,
      {
        key: "Label.Value",
        value: event.value,
      },
    ];
  }

  popoverShow.value[index] = false;
}

function onDeleteFilter(index: number): void {
  activeFilters.value = [...activeFilters.value.slice(0, index), ...activeFilters.value.slice(index + 1)];
}

watch(activeFilters, (value) => {
  console.log("change");
  const queryParams = value.reduce((acc, { key, value }) => {
    if (!acc[key]) {
      acc[key] = [];
    }
    acc[key].push(value);
    return acc;
  }, {} as Record<string, string[]>);

  router.push({ query: queryParams });
});

const labelNamesForNamespace = computed<{
  [name: string]: string[];
}>(() => {
  return namespaces.value.reduce((acc: { [name: string]: string[] }, curr: Namespace) => {
    if (!acc[curr.id]) {
      acc[curr.name] = [];
    }
    acc[curr.name] = [...acc[curr.name], ...curr.labels.map((l) => l.name)];
    return acc;
  }, {});
});

function updateSelectedVisualisation(index: number) {
  options.value.selectedVisualization = index;
  options.value.selectedTextPresentationMode = -1;
}

function updateSelectedTextPresentationMode(index: number) {
  options.value.selectedTextPresentationMode = index;
  options.value.selectedVisualization = -1;
}

const textBasePresentationModes: string[] = ["full", "condensed"];
const visualisationModes = ["Sunburst Filtered", "Sunburst Highlighted", "Hierarchical Edges"];

const queryParams = computed(() => {
  const searchParams = new URLSearchParams();
  if (activeFilters.value) {
    activeFilters.value.forEach((ac) => {
      searchParams.append(ac.key, ac.value);
    });
  }
  return searchParams;
});

const url = computed(() => "/api/boundedContexts?" + queryParams.value);

const { data } = useFetch<BoundedContext[]>(url, {
  refetch: true,
}).get();

const boundedContextDomain = computed<{
  [domainId: string]: BoundedContext[];
}>(() => {
  return (
    data.value?.reduce((acc: { [domainId: string]: BoundedContext[] }, curr: BoundedContext) => {
      if (!acc[curr.domain.id]) {
        acc[curr.domain.id] = [];
      }
      acc[curr.domain.id] = [...acc[curr.domain.id], curr];
      return acc;
    }, {}) || {}
  );
});

const domainsWithBoundedContexts = computed(() => {
  return Object.entries(boundedContextDomain.value).map(([domainId, contexts]) => ({
    domainId,
    contexts,
  }));
});

const domainIdToDomainName = computed<{ [domainId: string]: string }>(() => {
  return (
    allDomains.value?.reduce((acc: { [domainId: string]: string }, curr: Domain) => {
      if (!acc[curr.id]) {
        acc[curr.id] = "";
      }
      acc[curr.id] = curr.name;
      return acc;
    }, {}) || {}
  );
});

const popoverShow = ref<boolean[]>([]);

const domainCount = computed(() => {
  return Object.keys(boundedContextDomain.value).length;
});

function onClearFilters() {
  activeFilters.value = [];
}
</script>