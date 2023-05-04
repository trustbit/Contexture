<template>
  <ContextureBoundedContextCanvasElement
    :title="t('bounded_context_canvas.ubiquitous_language.title')"
    :title-icon="icon"
    :tooltip="t('bounded_context_canvas.ubiquitous_language.tooltip')"
  >
    <div>
      <ContextureHelpfulErrorAlert v-if="submitError" v-bind="submitError" />

      <div v-if="!ubiquitousLanguage || Object.keys(ubiquitousLanguage)?.length === 0">
        <span class="italic text-gray-700">({{ t("bounded_context_canvas.ubiquitous_language.empty") }})</span>
      </div>

      <ContextureAccordionItem v-for="key in ubiquitousLanguage" :key="key.term">
        <template #title>
          <div class="flex w-full justify-between">
            {{ key.term }}
            <button @click.prevent="() => onDeleteUbiquitousLanguage(key.term)">
              <Icon:material-symbols:delete-outline class="h-5 w-5 text-blue-500 hover:text-blue-600" />
            </button>
          </div>
        </template>
        <template #default>
          <span v-if="key.description">{{ key.description }}</span>
          <span v-else class="italic text-gray-700"
            >({{ t("bounded_context_canvas.ubiquitous_language.description.empty") }})</span
          >
        </template>
      </ContextureAccordionItem>

      <ContextureCollapsable
        :label="t('bounded_context_canvas.ubiquitous_language.actions.collapsed.add')"
        :cancel-text="t('common.cancel')"
        class="mt-8"
        :collapsed="addCollapsed"
        @update:collapsed="(collapsed) => (addCollapsed = collapsed)"
      >
        <ContextureDynamicForm
          :schema="ubiquitousLanguageSchema"
          :button-props="{
            label: t('bounded_context_canvas.ubiquitous_language.actions.open.add'),
            size: 'sm',
          }"
          @submit="onUbiquitousLanguageAdd"
        />
      </ContextureCollapsable>
    </div>
  </ContextureBoundedContextCanvasElement>
</template>

<script setup lang="ts">
import { toFieldValidator } from "@vee-validate/zod";
import { storeToRefs } from "pinia";
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import * as zod from "zod";
import ContextureBoundedContextCanvasElement from "~/components/bounded-context/canvas/ContextureBoundedContextCanvasElement.vue";
import ContextureAccordionItem from "~/components/primitives/accordion/ContextureAccordionItem.vue";
import ContextureHelpfulErrorAlert, {
  HelpfulErrorProps,
} from "~/components/primitives/alert/ContextureHelpfulErrorAlert.vue";
import ContextureCollapsable from "~/components/primitives/collapsable/ContextureCollapsable.vue";
import ContextureDynamicForm from "~/components/primitives/dynamic-form/ContextureDynamicForm.vue";
import { DynamicFormSchema } from "~/components/primitives/dynamic-form/dynamicForm";
import ContextureInputText from "~/components/primitives/input/ContextureInputText.vue";
import { isUniqueIn } from "~/core/validation";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import useConfirmationModalStore from "~/stores/confirmationModal";
import { UbiquitousLanguage, UbiquitousLanguageItem } from "~/types/boundedContext";
import IconsMaterialSymbolsFormatForumOutline from "~icons/material-symbols/forum-outline";

const icon = IconsMaterialSymbolsFormatForumOutline;
const store = useBoundedContextsStore();
const confirmationModal = useConfirmationModalStore();
const { activeBoundedContext } = storeToRefs(store);
const { t } = useI18n();
const submitError = ref<HelpfulErrorProps>();
const addCollapsed = ref(true);
const ubiquitousLanguage = computed(() => activeBoundedContext.value?.ubiquitousLanguage);

const ubiquitousLanguageSchema: DynamicFormSchema<UbiquitousLanguageItem> = {
  fields: [
    {
      name: "term",
      component: ContextureInputText,
      componentProps: {
        label: "Domain term",
        description: "The language term that is used inside this bounded context.",
        required: true,
        rules: toFieldValidator(
          zod
            .string()
            .min(1)
            .superRefine((arg, ctx) =>
              isUniqueIn<UbiquitousLanguage>(arg, ctx, {
                in: ubiquitousLanguage.value,
                errorMessage: `The term '${arg}' has already been defined before. Please use a distinct, case insensitive name.`,
              })
            )
        ),
      },
    },
    {
      name: "description",
      component: ContextureInputText,
      componentProps: {
        label: "Description",
        description: "Define the meaning of the term inside this bounded context.",
      },
    },
  ],
};

async function onUbiquitousLanguageAdd(ubiquitousLanguageItem: UbiquitousLanguageItem) {
  submitError.value = null;
  const res = await store.addUbiquitousLanguageItem(activeBoundedContext.value.id, ubiquitousLanguageItem);

  if (res.error.value) {
    submitError.value = {
      friendlyMessage: t("bounded_context_canvas.ubiquitous_language.error.add"),
      error: res.error.value,
      response: res.data.value,
    };
  } else {
    addCollapsed.value = true;
  }
}

async function onDeleteUbiquitousLanguage(ubiquitousLanguageKey: string) {
  confirmationModal.open(
    t("bounded_context_canvas.ubiquitous_language.delete.confirm.title", {
      name: ubiquitousLanguageKey,
    }),
    t("bounded_context_canvas.ubiquitous_language.delete.confirm.body"),
    t("bounded_context_canvas.ubiquitous_language.delete.confirm.confirm_button"),
    () => deleteUbiquitousLanguage(ubiquitousLanguageKey)
  );
}

async function deleteUbiquitousLanguage(ubiquitousLanguageKey: string) {
  submitError.value = null;
  const res = await store.deleteUbiquitousLanguage(activeBoundedContext.value.id, ubiquitousLanguageKey);

  if (res.error.value) {
    submitError.value = {
      friendlyMessage: t("bounded_context_canvas.ubiquitous_language.error.delete"),
      error: res.error.value,
      response: res.data.value,
    };
  }
}
</script>
