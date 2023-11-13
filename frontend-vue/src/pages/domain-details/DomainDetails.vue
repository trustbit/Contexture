<template>
  <ContextureHeroHeader v-if="domain">
    <div class="container mx-auto px-4 sm:px-0">
      <transition enter-from-class="opacity-0" enter-active-class="transition-opacity" appear>
        <div v-if="editMode">
          <div class="space-y-2">
            <ContextureHelpfulErrorAlert
              v-for="(error, index) of editSubmitErrors"
              :key="`error ${index}`"
              v-bind="error"
            />
          </div>
          <div class="flex justify-between gap-4">
            <ContextureEditDomainForm :domain="domain" @submit="onSave" />

            <div>
              <ContextureRoundedButton @click="onEditCloseClick">
                <span class="sr-only">{{ t("domains.details.edit.close") }}</span>
                <Icon:material-symbols:close class="h-4 w-4" />
              </ContextureRoundedButton>
            </div>
          </div>
        </div>
        <div v-else>
          <div class="flex w-full justify-between gap-4">
            <div class="flex w-full flex-col gap-4">
              <span class="text-sm text-gray-700">{{ domain.shortName }}</span>
              <div>
                <h1 class="text-3xl font-bold">
                  {{ domain.name }}
                </h1>
              </div>
              <div class="h-36 w-full overflow-y-auto whitespace-pre-line">
                <span class="text-lg text-gray-800">{{ domain.vision }}</span>
              </div>
            </div>
            <div>
              <ContextureTooltip content="Edit Domain" placement="left">
                <ContextureRoundedButton size="md" @click="onEditClick">
                  <span class="sr-only">{{ t("domains.details.edit.edit") }}</span>
                  <Icon:material-symbols:drive-file-rename-outline class="h-4 w-4" />
                </ContextureRoundedButton>
              </ContextureTooltip>
            </div>
          </div>
        </div>
      </transition>
    </div>
  </ContextureHeroHeader>

  <div class="container mx-auto px-4 pb-4 sm:px-0">
    <div class="mt-4 flex justify-center sm:mt-10" v-if="loading">{{ t("domains.details.loading") }}</div>

    <div class="mt-6" v-else-if="domain">
      <div class="mt-8">
        <ContextureBreadcrumbs />
      </div>
      <div class="mt-6">
        <TabGroup @change="onTabChange" :selected-index="selectedTab">
          <div class="flex justify-between">
            <TabList class="flex w-fit flex-col gap-4 text-lg text-gray-700 sm:flex-row">
              <Tab
                class="inline-flex items-center ui-selected:border-b-2 ui-selected:border-b-blue-500 ui-selected:pb-2 ui-selected:font-bold ui-selected:text-gray-900"
              >
                <Icon:material-symbols:flip-to-back aria-hidden="true" class="mr-1 h-4 w-4 text-purple-500" />
                <span>{{
                  t("domains.details.tabs.subdomains", {
                    count: subdomains.length,
                  })
                }}</span>
              </Tab>
              <Tab
                class="inline-flex items-center ui-selected:border-b-2 ui-selected:border-b-blue-500 ui-selected:pb-2 ui-selected:font-bold ui-selected:text-gray-900"
              >
                <Icon:material-symbols:select-all aria-hidden="true" class="mr-1 h-4 w-4 text-yellow-500" />
                <span>{{
                  t("domains.details.tabs.bounded_contexts", {
                    count: boundedContexts.length,
                  })
                }}</span>
              </Tab>
            </TabList>

            <Menu as="div" class="relative inline-block text-left">
              <div>
                <MenuButton
                  class="box-border inline-flex w-full items-center justify-center rounded bg-blue-500 px-4 py-2 text-sm text-gray-50 hover:bg-blue-400 focus:bg-blue-500 focus:shadow-[0px_0px_5px] focus:shadow-blue-300 active:bg-blue-700 disabled:bg-gray-400"
                >
                  <Icon:materialSymbols:add class="mr-1.5 h-5 w-5" />
                  {{ t("common.create") }}
                </MenuButton>
              </div>

              <transition
                enter-active-class="transition duration-100 ease-out"
                enter-from-class="transform scale-95 opacity-0"
                enter-to-class="transform scale-100 opacity-100"
                leave-active-class="transition duration-75 ease-in"
                leave-from-class="transform scale-100 opacity-100"
                leave-to-class="transform scale-95 opacity-0"
              >
                <MenuItems
                  class="absolute right-0 mt-2 w-56 origin-top-right divide-y divide-gray-100 rounded bg-white shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none"
                >
                  <div class="px-1 py-1">
                    <MenuItem v-if="subdomainsStore.isCreateSubdomainEnabled" v-slot="{ active }">
                      <button
                        class="group flex w-full items-center rounded-md px-2 py-2 text-sm capitalize"
                        :class="[active ? 'bg-blue-500 text-white' : 'text-gray-900']"
                        @click="onCreateSubdomain"
                      >
                        <Icon:material-symbols:flip-to-back
                          aria-hidden="true"
                          class="mr-2 h-5 w-5"
                          :class="[active ? 'text-white' : 'text-purple-500']"
                        />
                        {{ t("common.subdomain") }}
                      </button>
                    </MenuItem>

                    <MenuItem v-slot="{ active }">
                      <button
                        class="group flex w-full items-center rounded-md px-2 py-2 text-sm capitalize"
                        :class="[active ? 'bg-blue-500 text-white' : 'text-gray-900']"
                        @click="onCreateBoundedContext"
                      >
                        <Icon:material-symbols:select-all
                          aria-hidden="true"
                          class="mr-2 h-5 w-5"
                          :class="[active ? 'text-white' : 'text-yellow-500']"
                        />
                        {{ t("common.bounded_context") }}
                      </button>
                    </MenuItem>
                  </div>
                </MenuItems>
              </transition>
            </Menu>
          </div>

          <TabPanels class="mt-6">
            <TabPanel>
              <div class="mt-6">
                <div v-if="!subdomains.length" class="mb-4">
                  <div>
                    <span class="text-lg">{{ t("domains.details.no_subdomains") }}</span>
                  </div>
                  <ContexturePrimaryButton
                    v-if="subdomainsStore.isCreateSubdomainEnabled"
                    :label="t('domains.details.button.create_subdomain')"
                    class="mt-4"
                    @click="onCreateSubdomain"
                  >
                    <Icon:material-symbols:flip-to-back aria-hidden="true" class="mr-2 h-5 w-5" />
                  </ContexturePrimaryButton>
                </div>

                <ContextureDomainCardGrid :domains="subdomains" />
              </div>
            </TabPanel>
            <TabPanel>
              <div class="mt-6">
                <div v-if="!boundedContexts.length" class="mb-4">
                  <div>
                    <span class="text-lg">{{ t("domains.details.no_bounded_contexts") }}</span>
                  </div>
                  <ContexturePrimaryButton
                    :label="t('domains.details.button.create_bounded_context')"
                    class="mt-4"
                    @click="onCreateBoundedContext"
                  >
                    <Icon:material-symbols:flip-to-back aria-hidden="true" class="mr-2 h-5 w-5" />
                  </ContexturePrimaryButton>
                </div>

                <div class="mt-6">
                  <ContextureBoundedContextCardGrid :bounded-contexts="boundedContexts" />
                </div>
              </div>
            </TabPanel>
          </TabPanels>
        </TabGroup>
      </div>
    </div>

    <ContextureEntityNotFound :text="t('domains.details.empty', { currentDomainId })" v-else />

    <CreateSubdomainModal :is-open="createSubdomainOpen" :parent-domain="domain" @close="onCreateSubdomainClose" />
    <ContextureCreateBoundedContextModal
      :is-open="createBoundedContextOpen"
      :parent-domain="domain"
      @close="onCreateBoundedContextClose"
    />
  </div>
</template>

<script lang="ts" setup>
import { Menu, MenuButton, MenuItem, MenuItems, Tab, TabGroup, TabList, TabPanel, TabPanels } from "@headlessui/vue";
import { useRouteParams, useRouteQuery } from "@vueuse/router";
import { storeToRefs } from "pinia";
import { computed, ComputedRef, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import ContextureCreateBoundedContextModal from "~/components/domains/details/ContextureCreateBoundedContextModal.vue";
import CreateSubdomainModal from "~/components/domains/details/ContextureCreateSubdomainModal.vue";
import ContextureDomainCardGrid from "~/components/domains/ContextureDomainCardGrid.vue";
import ContextureHelpfulErrorAlert, {
  HelpfulErrorProps,
} from "~/components/primitives/alert/ContextureHelpfulErrorAlert.vue";
import ContextureBreadcrumbs from "~/components/core/breadcrumbs/ContextureBreadcrumbs.vue";
import ContexturePrimaryButton from "~/components/primitives/button/ContexturePrimaryButton.vue";
import ContextureRoundedButton from "~/components/primitives/button/ContextureRoundedButton.vue";
import ContextureBoundedContextCardGrid from "~/components/bounded-context/ContextureBoundedContextCardGrid.vue";
import ContextureEntityNotFound from "~/components/core/ContextureEntityNotFound.vue";
import ContextureEditDomainForm from "~/components/domains/details/ContextureEditDomainForm.vue";
import ContextureHeroHeader from "~/components/core/header/ContextureHeroHeader.vue";
import ContextureTooltip from "~/components/primitives/tooltip/ContextureTooltip.vue";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import { useDomainsStore } from "~/stores/domains";
import { useSubdomainsStore } from "~/stores/subDomains";
import { Domain, UpdateDomain } from "~/types/domain";

const { t } = useI18n();
const domainStore = useDomainsStore();
const subdomainsStore = useSubdomainsStore();
const { loading, subdomainsByDomainId, domainByDomainId } = storeToRefs(domainStore);
const { boundedContextsByDomainId } = storeToRefs(useBoundedContextsStore());
const currentDomainId = useRouteParams<string>("id");
const domain: ComputedRef<Domain | undefined> = computed<Domain | undefined>(
  () => domainByDomainId.value[currentDomainId.value]
);
const subdomains = computed(() => subdomainsByDomainId.value[currentDomainId.value] || []);
const editMode = ref<boolean>(false);
const createSubdomainOpen = ref<boolean>(false);
const createBoundedContextOpen = ref<boolean>(false);
const boundedContexts = computed(() => boundedContextsByDomainId.value[currentDomainId.value] || []);
const viewOptions = ["subdomain", "boundedContext"];
const selectedView = useRouteQuery<string>("view", "subdomain", { mode: "push" });
const selectedTab = computed<number>(() => viewOptions.indexOf(selectedView.value));

function onTabChange(newSelectedTab: number): void {
  selectedView.value = viewOptions[newSelectedTab];
}

function onCreateSubdomain() {
  createSubdomainOpen.value = true;
}

function onCreateSubdomainClose() {
  createSubdomainOpen.value = false;
}

function onCreateBoundedContext() {
  createBoundedContextOpen.value = true;
}

function onCreateBoundedContextClose() {
  createBoundedContextOpen.value = false;
}

function onEditClick() {
  editMode.value = true;
}

function onEditCloseClick() {
  editMode.value = false;
}

watch(
  () => domain.value,
  (newDomain) => {
    if (newDomain) {
      subdomainsStore.setCurrentDomain(newDomain);
    }
  },
  { immediate: true }
);

const editSubmitErrors = ref<HelpfulErrorProps[]>();

async function onSave(values: UpdateDomain) {
  editSubmitErrors.value = [];
  const res = await domainStore.updateDomain(domain.value!.id, values);

  if (res.find((r) => !r.response.value?.ok)) {
    editSubmitErrors.value = res
      .map((r) => {
        return {
          error: r.error.value,
          data: r.data.value,
        };
      })
      .map((error) => {
        return {
          friendlyMessage: t("domains.details.edit.error"),
          error: error.error,
          response: error.data,
        };
      });
  }

  if (editSubmitErrors.value?.length === 0) {
    editMode.value = false;
  }
}
</script>
