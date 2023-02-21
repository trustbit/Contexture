<template>
  <ContextureBoundedContextCanvasElement
    :title="t('bounded_context_canvas.domain_roles.title')"
    :title-icon="icon"
    tooltip="How can you characterise the behaviour of this bounded context?"
  >
    <div>
      <ContextureHelpfulErrorAlert v-if="submitError" v-bind="submitError" />

      <div v-if="domainRoles.length === 0">
        <span class="italic text-gray-700">({{ t("bounded_context_canvas.domain_roles.empty") }})</span>
      </div>

      <div class="max-h-28 overflow-y-scroll">
        <ContextureAccordionItem v-for="domainRole in domainRoles" :key="domainRole.name">
          <template #title>
            <div class="flex w-full justify-between">
              {{ domainRole.name }}
              <button @click.prevent="() => onDeleteDomainRole(domainRole)" data-testId="deleteDomainRole">
                <Icon:material-symbols:delete-outline class="h-5 w-5 text-blue-500 hover:text-blue-600" />
              </button>
            </div>
          </template>
          <template #default>
            {{ domainRole.description }}
          </template>
        </ContextureAccordionItem>
      </div>

      <ContextureCollapsable
        :label="t('bounded_context_canvas.domain_roles.actions.collapsed.add')"
        class="mt-8"
        :cancel-text="t('common.cancel')"
        :collapsed="addNewRoleCollapsed"
        @update:collapsed="(collapsed) => (addNewRoleCollapsed = collapsed)"
      >
        <ContextureDynamicForm
          :schema="domainRoleSchema"
          :button-props="{
            label: t('bounded_context_canvas.domain_roles.actions.open.add'),
            size: 'sm',
          }"
          @submit="onDomainRoleAdd"
        />
      </ContextureCollapsable>
      <ContextureCollapsable
        :label="t('bounded_context_canvas.domain_roles.actions.collapsed.choose')"
        class="mt-2"
        :cancel-text="t('common.cancel')"
        :collapsed="chooseRoleCollapsed"
        @update:collapsed="(collapsed) => (chooseRoleCollapsed = collapsed)"
      >
        <div class="space-y-6">
          <div class="pt-6">
            <h3 class="text-base font-bold text-gray-900">
              {{ t("bounded_context_canvas.domain_roles.actions.open.select") }}
            </h3>
          </div>
          <ul class="flex h-72 flex-col gap-y-4 overflow-y-scroll px-2">
            <li v-for="predefineDomainRole in predefinedDomainRoles" :key="predefineDomainRole.name">
              <ContextureRadio
                v-model="selectedPredefinedRole"
                :value="predefineDomainRole"
                name="predefineDomainRole"
                :disabled="!!activeBoundedContext?.domainRoles?.find((d) => d.name === predefineDomainRole.name)"
                :label="predefineDomainRole.name"
                :description="predefineDomainRole.description"
                label-class="text-sm"
              />
            </li>
          </ul>

          <ContexturePrimaryButton
            type="submit"
            size="sm"
            :label="t('bounded_context_canvas.domain_roles.actions.open.choose')"
            class="mt-2"
            @click="onDomainRoleAdd(selectedPredefinedRole)"
          >
            <template #left>
              <icon:material-symbols:add class="mr-2" />
            </template>
          </ContexturePrimaryButton>
        </div>
      </ContextureCollapsable>
    </div>
  </ContextureBoundedContextCanvasElement>
</template>

<script setup lang="ts">
import { toFieldValidator } from "@vee-validate/zod";
import { storeToRefs } from "pinia";
import { computed, Ref, ref } from "vue";
import { useI18n } from "vue-i18n";
import * as zod from "zod";
import ContextureBoundedContextCanvasElement from "~/components/bounded-context/canvas/ContextureBoundedContextCanvasElement.vue";
import ContextureAccordionItem from "~/components/primitives/accordion/ContextureAccordionItem.vue";
import ContextureHelpfulErrorAlert, {
  HelpfulErrorProps,
} from "~/components/primitives/alert/ContextureHelpfulErrorAlert.vue";
import ContexturePrimaryButton from "~/components/primitives/button/ContexturePrimaryButton.vue";
import ContextureCollapsable from "~/components/primitives/collapsable/ContextureCollapsable.vue";
import ContextureDynamicForm from "~/components/primitives/dynamic-form/ContextureDynamicForm.vue";
import { DynamicFormSchema } from "~/components/primitives/dynamic-form/dynamicForm";
import ContextureInputText from "~/components/primitives/input/ContextureInputText.vue";
import ContextureRadio from "~/components/primitives/radio/ContextureRadio.vue";
import { predefinedDomainRoles } from "~/constants/domainRoles";
import { isUniqueIn } from "~/core/validation";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import useConfirmationModalStore from "~/stores/confirmationModal";
import { DomainRole } from "~/types/boundedContext";
import IconsMaterialSymbolsFormatBookOutline from "~icons/material-symbols/book-outline";

const icon = IconsMaterialSymbolsFormatBookOutline;
const store = useBoundedContextsStore();
const { activeBoundedContext } = storeToRefs(store);
const { t } = useI18n();
const confirmationModal = useConfirmationModalStore();

const domainRoles: Ref<DomainRole[]> = computed(() => activeBoundedContext.value?.domainRoles || []);

const domainRoleSchema: DynamicFormSchema<DomainRole> = {
  fields: [
    {
      name: "name",
      component: ContextureInputText,
      componentProps: {
        label: "Domain role name",
        description: "The domain role name that is used inside this bounded context.",
        required: true,
        rules: toFieldValidator(
          zod
            .string()
            .min(1)
            .superRefine((arg, ctx) =>
              isUniqueIn<DomainRole>(arg, ctx, {
                field: "name",
                in: activeBoundedContext.value!.domainRoles,
                errorMessage: `The domain role with name '${arg}' has already been defined before. Please use a distinct, case insensitive name.`,
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
        description: "Define the meaning of the this domain role inside this bounded context.",
      },
    },
  ],
};

const submitError = ref<HelpfulErrorProps>();
const addNewRoleCollapsed = ref(true);
const chooseRoleCollapsed = ref(true);
const selectedPredefinedRole = ref<DomainRole>();

async function onDomainRoleAdd(domainRole: DomainRole) {
  submitError.value = undefined;
  const res = await store.addDomainRole(activeBoundedContext.value.id, domainRole);

  if (res.error.value) {
    submitError.value = {
      friendlyMessage: t("bounded_context_canvas.domain_roles.error.add"),
      error: res.error.value,
      response: res.data.value,
    };
  } else {
    chooseRoleCollapsed.value = true;
    addNewRoleCollapsed.value = true;
    selectedPredefinedRole.value = undefined;
  }
}

async function onDeleteDomainRole(domainRole: DomainRole): Promise<void> {
  confirmationModal.open(
    t("bounded_context_canvas.domain_roles.delete.confirm.title", {
      name: domainRole.name,
    }),
    t("bounded_context_canvas.domain_roles.delete.confirm.body"),
    t("bounded_context_canvas.domain_roles.delete.confirm.confirm_button"),
    () => deleteDomainRole(domainRole)
  );
}

async function deleteDomainRole(domainRole: DomainRole): Promise<void> {
  submitError.value = undefined;
  const res = await store.deleteDomainRole(activeBoundedContext.value.id, domainRole);

  if (res.error.value) {
    submitError.value = {
      friendlyMessage: t("bounded_context_canvas.domain_roles.error.delete"),
      error: res.error.value,
      response: res.data.value,
    };
  }
}
</script>
