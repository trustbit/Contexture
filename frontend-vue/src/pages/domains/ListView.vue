<template>
  <ContextureBlankHeader :title="t('domains.list.title')" />

  <div class="mt-4 sm:mt-10">
    <div v-if="loading" class="text-sm text-gray-700">
      {{ t("domains.list.loading") }}
    </div>
    <ContextureHelpfulErrorAlert
      v-else-if="loadingError"
      v-bind="loadingError"
      :friendly-message="t('domains.list.error.loading')"
    />

    <div v-else class="w-full">
      <div class="text-sm text-gray-700" v-if="parentDomains.length === 0">{{ $t("domains.empty") }}</div>
      <div v-else>
        <ContextureSearch v-model="searchQuery" :placeholder="t('domains.list.search.bounded_contexts')" />

        <div class="mt-4 flex gap-x-4">
          <ContextureSwitch v-model="options.showDescription" :label="t('domains.list.show_description')" />
          <ContextureSwitch v-model="options.showNamespaces" :label="t('domains.list.show_namespaces')" />
        </div>

        <div class="mt-8 flex flex-col gap-y-4">
          <template v-for="parentDomain of parentDomains" :key="parentDomain.id">
            <div class="grid-cols-3 gap-4 bg-gray-100 p-2 sm:grid sm:p-6" v-if="showParentDomain(parentDomain.id)">
              <!-- parent domains -->
              <!-- first column -->
              <div class="top-[40px] order-1 col-start-1 flex self-start rounded bg-white p-4 sm:sticky">
                <div>
                  <div class="flex">
                    <Icon:material-symbols:flip-to-back aria-hidden="true" class="h-6 w-6 text-blue-500" />
                    <div class="ml-3">
                      <h2 class="font-bold text-blue-900">
                        {{ parentDomain.name }}
                      </h2>
                      <p class="text-sm text-gray-700">
                        {{ parentDomain.shortName }}
                      </p>
                    </div>
                  </div>

                  <div class="mt-4 flex gap-x-2 pt-1">
                    <div class="flex flex-wrap gap-2">
                      <ContextureBadge
                        v-if="parentDomain.subdomains.length > 0"
                        mode="light"
                        size="sm"
                        color="purple"
                        variant="filled"
                        class="flex w-fit items-center"
                      >
                        <Icon:material-symbols:flip-to-back aria-hidden="true" class="mr-1.5 text-purple-500" />
                        <span class="font-bold text-blue-900">{{
                          t("domains.list.subdomains", {
                            count: parentDomain.subdomains.length,
                          })
                        }}</span>
                      </ContextureBadge>
                      <ContextureBadge
                        v-if="parentDomain.boundedContexts.length > 0"
                        mode="light"
                        size="sm"
                        color="yellow"
                        variant="filled"
                        class="flex w-fit items-center"
                      >
                        <Icon:material-symbols:select-all aria-hidden="true" class="mr-1.5 text-yellow-500" />
                        <span class="font-bold text-blue-900">{{
                          t("domains.list.bounded_contexts", {
                            count: parentDomain.boundedContexts.length,
                          })
                        }}</span>
                      </ContextureBadge>
                    </div>
                  </div>

                  <div v-if="options.showDescription && parentDomain.vision" class="mt-6">
                    <p class="text-xs font-bold">{{ t("common.description") }}</p>
                    <p class="mt-2 text-sm text-gray-800">
                      {{ parentDomain.vision }}
                    </p>
                  </div>
                </div>
              </div>

              <!-- second & third column -->
              <!-- Subdomains & bounded contexts -->
              <div class="order-3 col-span-2 col-start-2">
                <div v-for="subdomain of filteredSubdomains[parentDomain.id]" :key="subdomain.id" class="mb-8">
                  <div class="grid-cols-2 gap-x-4 sm:grid">
                    <!-- second column -->
                    <div class="top-[40px] flex flex-col self-start rounded bg-white p-4 sm:sticky">
                      <div class="text-gray-800">
                        <div class="mb-2 flex flex-wrap items-center gap-x-1 text-xs font-bold">
                          {{ parentDomain.name }}
                          <Icon:material-symbols:chevron-right aria-hidden="true" class="h-4 w-4" />
                          {{ subdomain.name }}
                        </div>
                      </div>

                      <div class="flex pt-4 pb-2">
                        <Icon:material-symbols:flip-to-back aria-hidden="true" class="h-6 w-6 text-purple-500" />
                        <div class="ml-3">
                          <h2 class="font-bold text-blue-900">
                            {{ subdomain.name }}
                          </h2>
                          <p class="text-sm text-gray-700">
                            {{ subdomain.shortName }}
                          </p>
                        </div>
                      </div>

                      <div class="flex gap-x-2 pt-2 pb-2">
                        <ContextureBadge
                          v-if="subdomainsByDomainId[subdomain.id]?.length > 0"
                          mode="light"
                          size="sm"
                          color="purple"
                          variant="filled"
                          class="flex w-fit items-center"
                        >
                          <Icon:material-symbols:flip-to-back aria-hidden="true" class="mr-1.5 text-purple-500" />
                          <span class="font-bold text-blue-900">
                            {{
                              t("domains.list.subdomains", {
                                count: subdomainsByDomainId[subdomain.id].length,
                              })
                            }}
                          </span>
                        </ContextureBadge>
                        <ContextureBadge
                          v-if="filteredBoundedContexts[subdomain.id]?.length > 0"
                          mode="light"
                          size="sm"
                          color="yellow"
                          variant="filled"
                          class="flex w-fit items-center"
                        >
                          <Icon:material-symbols:select-all aria-hidden="true" class="mr-1.5 text-yellow-500" />
                          <span class="font-bold text-blue-900">
                            {{
                              t("domains.list.bounded_contexts", {
                                count: boundedContextsByDomainId[subdomain.id]?.length,
                              })
                            }}
                          </span>
                        </ContextureBadge>
                      </div>

                      <div v-if="options.showDescription && subdomain.vision" class="pt-4 pb-4">
                        <p class="text-xs font-bold">{{ t("common.description") }}</p>
                        <p class="mt-2 text-sm text-gray-800">
                          {{ subdomain.vision }}
                        </p>
                      </div>
                    </div>

                    <!-- third column -->
                    <div class="flex flex-col gap-y-4">
                      <div
                        v-for="boundedContext of filteredBoundedContexts[subdomain.id]"
                        class="rounded bg-white p-4"
                        :key="boundedContext.id"
                      >
                        <div class="text-gray-800">
                          <div class="mb-2 flex flex-wrap items-center gap-x-1 text-xs font-bold">
                            {{ parentDomain.name }}
                            <Icon:material-symbols:chevron-right aria-hidden="true" class="h-4 w-4" />
                            {{ subdomain.name }}
                            <Icon:material-symbols:chevron-right aria-hidden="true" class="h-4 w-4" />
                            {{ boundedContext.name }}
                          </div>
                        </div>

                        <div class="flex pt-4 pb-2">
                          <Icon:material-symbols:select-all aria-hidden="true" class="h-6 w-6 text-yellow-500" />
                          <div class="ml-3">
                            <h2 class="font-bold text-blue-900">
                              {{ boundedContext.name }}
                            </h2>
                            <p class="text-sm text-gray-700">
                              {{ boundedContext.shortName }}
                            </p>
                          </div>
                        </div>

                        <div v-if="options.showDescription && boundedContext.description" class="pt-4 pb-4">
                          <p class="text-xs font-bold">{{ t("common.description") }}</p>
                          <p class="mt-2 text-sm text-gray-800">
                            {{ boundedContext.description }}
                          </p>
                        </div>

                        <div v-if="options.showNamespaces && boundedContext.namespaces?.length > 0" class="pt-4 pb-4">
                          <p class="text-xs font-bold">{{ t("common.namespaces") }}</p>
                          <div
                            v-for="namespace of boundedContext.namespaces"
                            class="mt-4 rounded bg-gray-100 p-4 text-gray-800"
                            :key="namespace.id"
                          >
                            <p class="text-sm font-bold">
                              {{ namespace.name }}
                            </p>
                            <div class="mt-3 space-y-4 px-4">
                              <div v-for="label of namespace.labels" class="text-xs" :key="label.id">
                                <p class="font-bold">
                                  {{ label.name }}
                                </p>
                                <a
                                  :href="label.value"
                                  v-if="isLink(label.value)"
                                  target="_blank"
                                  class="hover:underline"
                                >
                                  {{ label.value }}
                                </a>
                                <span v-else>{{ label.value }}</span>
                              </div>
                            </div>
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              <!-- third column -->
              <!-- Bounded Context belonging to a parent domain -->
              <div class="order-2 col-start-3 col-end-3">
                <div
                  v-for="boundedContext of filteredBoundedContexts[parentDomain.id]"
                  class="mt-2 rounded bg-white p-4"
                  :key="boundedContext.id"
                >
                  <div class="text-gray-800">
                    <div class="mb-2 flex flex-wrap items-center gap-x-1 text-xs font-bold">
                      {{ parentDomain.name }}
                      <Icon:material-symbols:chevron-right aria-hidden="true" class="h-4 w-4" />
                      {{ boundedContext.name }}
                    </div>
                    <div class="flex p-1 text-xs">
                      <Icon:material-symbols:subdirectory-arrow-right aria-hidden="true" class="mr-1 h-4 w-4" />
                      <span>{{ t("domains.list.direct_bounded_context_child") }}</span>
                    </div>
                  </div>

                  <div class="py-4">
                    <div class="flex">
                      <Icon:material-symbols:select-all aria-hidden="true" class="h-6 w-6 text-yellow-500" />
                      <div class="ml-3">
                        <h2 class="font-bold text-blue-900">
                          {{ boundedContext.name }}
                        </h2>
                        <p class="text-sm text-gray-700">
                          {{ boundedContext.shortName }}
                        </p>
                      </div>
                    </div>

                    <div v-if="options.showDescription && boundedContext.description" class="mt-6">
                      <p class="text-xs font-bold">{{ t("common.description") }}</p>
                      <p class="mt-2 text-sm text-gray-800">
                        {{ boundedContext.description }}
                      </p>
                    </div>

                    <div v-if="options.showNamespaces && boundedContext.namespaces?.length > 0" class="mt-6">
                      <p class="text-xs font-bold">{{ t("common.namespaces") }}</p>
                      <div
                        v-for="namespace of boundedContext.namespaces"
                        class="mt-4 rounded bg-gray-100 p-4 text-gray-800"
                        :key="namespace.id"
                      >
                        <p class="text-sm font-bold">
                          {{ namespace.name }}
                        </p>
                        <div class="mt-3 space-y-4 px-4">
                          <div v-for="label of namespace.labels" class="text-xs" :key="label.id">
                            <p class="font-bold">
                              {{ label.name }}
                            </p>
                            <a :href="label.value" v-if="isLink(label.value)" target="_blank" class="hover:underline">
                              {{ label.value }}
                            </a>
                            <span v-else>{{ label.value }}</span>
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </template>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { refDebounced, useLocalStorage } from "@vueuse/core";
import { useRouteQuery } from "@vueuse/router";
import { storeToRefs } from "pinia";
import { computed, Ref } from "vue";
import { useI18n } from "vue-i18n";
import ContextureBlankHeader from "~/components/core/header/ContextureBlankHeader.vue";
import ContextureHelpfulErrorAlert from "~/components/primitives/alert/ContextureHelpfulErrorAlert.vue";
import ContextureBadge from "~/components/primitives/badge/ContextureBadge.vue";
import ContextureSearch from "~/components/primitives/input/ContextureSearch.vue";
import ContextureSwitch from "~/components/primitives/switch/ContextureSwitch.vue";
import { isLink } from "~/core";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import { useDomainsStore } from "~/stores/domains";
import { BoundedContext } from "~/types/boundedContext";
import { DomainId } from "~/types/domain";

interface DomainListViewSettings {
  showDescription: boolean;
  showNamespaces: boolean;
}

const { t } = useI18n();
const { boundedContextsByDomainId } = storeToRefs(useBoundedContextsStore());
const { subdomainsByDomainId, parentDomains, loading, loadingError } = storeToRefs(useDomainsStore());

const options: Ref<DomainListViewSettings> = useLocalStorage<DomainListViewSettings>("settings.domains.listView", {
  showDescription: false,
  showNamespaces: false,
});
const searchQuery = useRouteQuery("query");
const searchQueryDebounced = refDebounced(searchQuery, 500);

const filteredBoundedContexts = computed<{ [domainId: string]: BoundedContext[] }>(() => {
  if (!searchQuery.value) {
    return boundedContextsByDomainId.value;
  }
  if (searchQueryDebounced.value) {
    return searchInBoundedContext(boundedContextsByDomainId.value, searchQueryDebounced.value as string);
  } else {
    return boundedContextsByDomainId.value;
  }
});

const filteredSubdomains = computed(() => {
  if (!searchQuery.value) {
    return subdomainsByDomainId.value;
  }
  if (searchQueryDebounced.value) {
    return Object.keys(subdomainsByDomainId.value).reduce((acc: any, domainId: any) => {
      const parentIds = parentDomains.value.map((p) => p.id);
      if (parentIds.includes(domainId)) {
        acc[domainId] = subdomainsByDomainId.value[domainId].filter(
          (subdomain) => filteredBoundedContexts.value[subdomain.id]?.length > 0
        );
      }
      return acc;
    }, {});
  } else {
    return subdomainsByDomainId.value;
  }
});

function filter(obj: any, query: string, key: string | undefined) {
  for (const prop in obj) {
    if (key && prop !== key) {
      continue;
    }
    if (Array.isArray(obj[prop])) {
      if (obj[prop].includes(query)) {
        return true;
      }
      const nestedResults = searchInArray(obj[prop] as any[], query);
      if (nestedResults.length) {
        return true;
      }
    } else {
      if (typeof obj[prop] === "string") {
        if (obj[prop].toLowerCase().includes(query?.toLowerCase())) {
          return true;
        }
      }
    }
  }
  return false;
}

const searchInArray = (array: any[], query: string, key?: string): any[] => {
  return array.filter((o) => filter(o, query, key));
};

const searchInBoundedContext = (
  objectToSearchIn: { [id: DomainId]: BoundedContext[] },
  query: string,
  keys?: string
): {} => {
  return Object.keys(objectToSearchIn).reduce((curr: any, key: any) => {
    const arr = objectToSearchIn[key];
    const filtered = arr.filter((a) => filter(a, query, keys));

    if (filtered.length === 0) {
      return curr;
    }

    if (!curr[key]) {
      curr[key] = [];
    }

    curr[key] = [...curr[key], ...filtered];
    return curr;
  }, {});
};

const showParentDomain = (parentDomainId: DomainId) => {
  return searchQuery.value
    ? filteredSubdomains.value[parentDomainId]?.length > 0 || filteredBoundedContexts.value[parentDomainId]?.length > 0
    : filteredSubdomains;
};
</script>
