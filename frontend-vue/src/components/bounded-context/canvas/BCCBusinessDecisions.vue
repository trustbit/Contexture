<template>
  <ContextureBoundedContextCanvasElement
    :title="t('bounded_context_canvas.business_decisions.title')"
    :title-icon="icon"
    :tooltip="t('bounded_context_canvas.business_decisions.tooltip')"
  >
    <div>
      <ContextureHelpfulErrorAlert v-if="submitError" v-bind="submitError" />

      <div v-if="businessDecisions.length === 0">
        <span class="italic text-gray-700">({{ t("bounded_context_canvas.business_decisions.empty") }})</span>
      </div>

      <ContextureAccordionItem v-for="key in businessDecisions" :key="key.name">
        <template #title>
          <div class="flex w-full justify-between">
            {{ key.name }}
            <button @click.prevent="() => onBusinessDecisionDelete(key)">
              <Icon:material-symbols:delete-outline class="h-5 w-5 text-blue-500 hover:text-blue-600" />
            </button>
          </div>
        </template>
        <template #default>
          <span v-if="key.description">{{ key.description }}</span>
          <span v-else class="italic text-gray-700"
            >({{ t("bounded_context_canvas.business_decisions.description.empty") }})</span
          >
        </template>
      </ContextureAccordionItem>

      <ContextureCollapsable
        :label="t('bounded_context_canvas.business_decisions.actions.collapsed.add')"
        class="mt-8"
        :cancel-text="t('common.cancel')"
        :collapsed="addCollapsed"
        @update:collapsed="(collapsed) => (addCollapsed = collapsed)"
      >
        <ContextureDynamicForm
          :schema="businessDecisionSchema"
          :button-props="{
            label: t('bounded_context_canvas.business_decisions.actions.open.add'),
            size: 'sm',
          }"
          @submit="onBusinessDecisionAdd"
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
import ContextureAccordionItem from "~/components/primitives/accordion/ContextureAccordionItem.vue";
import ContextureBoundedContextCanvasElement from "~/components/bounded-context/canvas/ContextureBoundedContextCanvasElement.vue";
import ContextureCollapsable from "~/components/primitives/collapsable/ContextureCollapsable.vue";
import ContextureDynamicForm from "~/components/primitives/dynamic-form/ContextureDynamicForm.vue";
import { DynamicFormSchema } from "~/components/primitives/dynamic-form/dynamicForm";
import { HelpfulErrorProps } from "~/components/primitives/alert/ContextureHelpfulErrorAlert.vue";
import ContextureHelpfulErrorAlert from "~/components/primitives/alert/ContextureHelpfulErrorAlert.vue";
import ContextureInputText from "~/components/primitives/input/ContextureInputText.vue";
import { isUniqueIn } from "~/core/validation";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import useConfirmationModalStore from "~/stores/confirmationModal";
import { BusinessDecision } from "~/types/boundedContext";
import IconsMaterialSymbolsFormatGavel from "~icons/material-symbols/gavel";

const icon = IconsMaterialSymbolsFormatGavel;
const store = useBoundedContextsStore();
const confirmationModal = useConfirmationModalStore();
const { activeBoundedContext } = storeToRefs(store);
const { t } = useI18n();
const submitError = ref<HelpfulErrorProps>();
const addCollapsed = ref(true);
const businessDecisions = computed(() => activeBoundedContext.value?.businessDecisions || []);

const businessDecisionSchema: DynamicFormSchema<BusinessDecision> = {
  fields: [
    {
      name: "name",
      component: ContextureInputText,
      componentProps: {
        label: "Business decision name",
        description: "The business decision name that is used inside this bounded context.",
        required: true,
        rules: toFieldValidator(
          zod
            .string()
            .min(1)
            .superRefine((arg, ctx) =>
              isUniqueIn<BusinessDecision>(arg, ctx, {
                field: "name",
                in: businessDecisions.value,
                errorMessage: `The business decision with name '${arg}' has already been defined before. Please use a distinct, case insensitive name.`,
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
        description: "Define the meaning of this business decision inside this bounded context.",
      },
    },
  ],
};

async function onBusinessDecisionAdd(businessDecision: BusinessDecision) {
  submitError.value = null;
  const res = await store.addBusinessDecision(activeBoundedContext.value.id, businessDecision);

  if (res.error.value) {
    submitError.value = {
      friendlyMessage: t("bounded_context_canvas.business_decisions.error.add"),
      error: res.error.value,
      response: res.data.value,
    };
  } else {
    addCollapsed.value = true;
  }
}

async function onBusinessDecisionDelete(businessDecision: BusinessDecision) {
  confirmationModal.open(
    t("bounded_context_canvas.business_decisions.delete.confirm.title", {
      name: businessDecision.name,
    }),
    t("bounded_context_canvas.business_decisions.delete.confirm.body"),
    t("bounded_context_canvas.business_decisions.delete.confirm.confirm_button"),
    () => deleteBusinessDecision(businessDecision)
  );
}

async function deleteBusinessDecision(businessDecision: BusinessDecision) {
  submitError.value = null;
  const res = await store.deleteBusinessDecision(activeBoundedContext.value.id, businessDecision);

  if (res.error.value) {
    submitError.value = {
      friendlyMessage: t("bounded_context_canvas.business_decisions.error.delete"),
      error: res.error.value,
      response: res.data.value,
    };
  }
}
</script>
