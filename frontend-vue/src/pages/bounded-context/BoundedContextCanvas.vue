<template>
  <div class="container mx-auto mt-5 px-2 pb-8 sm:px-0" v-if="loading">
    <div v-if="loading">{{ t("bounded_context_canvas.loading") }}</div>
  </div>

  <div v-else-if="activeBoundedContext">
    <ContextureHeroHeader>
      <div class="flex flex-col gap-4 px-4 lg:px-20">
        <transition enter-from-class="opacity-0" enter-active-class="transition-opacity" appear>
          <div v-if="editMode" class="flex justify-between">
            <div class="w-full">
              <ContextureHelpfulErrorAlert v-if="submitError" v-bind="submitError" class="mb-4" />
              <ContextureEditBoundedContextForm
                :initial-value="activeBoundedContext"
                :submit-error="submitError"
                :action="onSave"
              />
            </div>

            <div>
              <ContextureRoundedButton @click="editMode = false">
                <Icon:material-symbols:close :aria-label="t('bounded_context_canvas.close_edit')" />
              </ContextureRoundedButton>
            </div>
          </div>
          <div v-else class="flex justify-between">
            <div class="flex flex-col gap-4">
              <span data-testid="boundedContextKey" class="text-gray-700">{{ activeBoundedContext.shortName }}</span>
              <div>
                <h1 data-testid="boundedContextName" class="text-3xl font-bold text-gray-900">
                  {{ activeBoundedContext.name }}
                </h1>
              </div>
            </div>
            <div>
              <ContextureTooltip content="Edit bounded context" placement="left" v-if="canModify">
                <ContextureRoundedButton size="md" @click="editMode = true">
                  <Icon:material-symbols:drive-file-rename-outline
                    :aria-label="t('bounded_context_canvas.open_edit')"
                    class="h-4 w-4"
                  />
                </ContextureRoundedButton>
              </ContextureTooltip>
            </div>
          </div>
        </transition>
        <StructurizerDiscloser />
      </div>
    </ContextureHeroHeader>

    <div class="mx-auto mt-5 px-4 pb-8 text-gray-900 lg:px-20">
      <div class="mt-8">
        <TabGroup :default-index="1">
          <div class="flex items-center justify-between">
            <div>
              <ContextureBreadcrumbs />
            </div>
            <div>
              <TabList
                class="flex divide-x divide-blue-500 overflow-hidden rounded-2xl border border-blue-500 text-xs text-blue-500 sm:mr-10 sm:w-fit"
              >
                <Tab
                  v-for="version in BoundedContextVersion"
                  class="inline-flex flex-grow items-center justify-center px-3 py-1.5 hover:bg-blue-100 ui-selected:bg-blue-500 ui-selected:text-white"
                  :key="version"
                  >{{ version }}
                </Tab>
              </TabList>
            </div>
          </div>
          <TabPanels>
            <div class="mt-8 overflow-x-auto">
              <TabPanel>
                <BCCV3 />
              </TabPanel>
              <TabPanel>
                <BCCV4 />
              </TabPanel>
            </div>
          </TabPanels>
        </TabGroup>
      </div>
    </div>

    <div class="mx-auto mt-5 px-4 pb-8 lg:px-20">
      <ContextureNamespaces class="rounded bg-gray-100 p-4"></ContextureNamespaces>
    </div>
  </div>

  <div class="container mx-auto mt-5 px-2 pb-8 sm:px-0" v-else>
    <ContextureEntityNotFound :text="t('bounded_context_canvas.not_found', { boundedContextId })" />
  </div>
</template>

<script setup lang="ts">
import { Tab, TabGroup, TabList, TabPanel, TabPanels } from "@headlessui/vue";
import { useRouteParams } from "@vueuse/router";
import { storeToRefs } from "pinia";
import { ref } from "vue";
import { useI18n } from "vue-i18n";
import ContextureEditBoundedContextForm from "~/components/bounded-context/canvas/ContextureEditBoundedContextForm.vue";
import BCCV3 from "~/components/bounded-context/canvas/layouts/BCCV3.vue";
import BCCV4 from "~/components/bounded-context/canvas/layouts/BCCV4.vue";
import { BoundedContextVersion } from "~/components/bounded-context/canvas/layouts/version";
import ContextureBreadcrumbs from "~/components/core/breadcrumbs/ContextureBreadcrumbs.vue";
import ContextureEntityNotFound from "~/components/core/ContextureEntityNotFound.vue";
import ContextureHeroHeader from "~/components/core/header/ContextureHeroHeader.vue";
import ContextureHelpfulErrorAlert from "~/components/primitives/alert/ContextureHelpfulErrorAlert.vue";
import ContextureRoundedButton from "~/components/primitives/button/ContextureRoundedButton.vue";
import ContextureTooltip from "~/components/primitives/tooltip/ContextureTooltip.vue";
import StructurizerDiscloser from "~/components/core/header/StructurizerDiscloser.vue";
import { useAuthStore } from "~/stores/auth";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import ContextureNamespaces from "~/components/bounded-context/namespace/ContextureNamespaces.vue";

const store = useBoundedContextsStore();
const { loading } = storeToRefs(store);
const { t } = useI18n();
const editMode = ref(false);
const boundedContextId = useRouteParams("id");
const submitError = ref();
const { activeBoundedContext } = storeToRefs(store);
const { canModify } = useAuthStore();

async function onSave(values: { name: string; key?: string }) {
  const updateNameRes = await store.updateName(activeBoundedContext.value.id, values.name);
  const updateKeyRes = await store.updateKey(activeBoundedContext.value.id, values.key);

  if (updateNameRes.error.value) {
    submitError.value = {
      friendlyMessage: t("bounded_context_canvas.error.update"),
      error: updateNameRes.error.value,
      response: updateNameRes.data.value,
    };
  } else if (updateKeyRes.error.value) {
    submitError.value = {
      friendlyMessage: t("bounded_context_canvas.error.update"),
      error: updateKeyRes.error.value,
      response: updateKeyRes.data.value,
    };
  } else {
    editMode.value = false;
  }
}
</script>
