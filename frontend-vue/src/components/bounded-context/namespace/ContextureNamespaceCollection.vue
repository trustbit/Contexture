<template>
  <div class="mt-4 flex justify-center sm:mt-10" v-if="loading">{{ t("bounded_context_namespace.loading") }}</div>

  <div v-else-if="activeBoundedContext">
    <div class="flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold">
          {{ t("bounded_context_namespace.title") }}
        </h1>

        <p class="text-sm text-gray-700">
          {{ t("bounded_context_namespace.description") }}
        </p>
      </div>
      <Menu as="div" class="relative inline-block text-left">
        <div>
          <MenuButton
            v-if="canModify"
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
            class="absolute right-0 mt-2 w-64 origin-top-right divide-y divide-gray-100 rounded bg-white shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none"
          >
            <div class="px-1 py-1">
              <MenuItem v-slot="{ active }">
                <button
                  class="group w-full rounded-md px-2 py-2 text-left text-sm"
                  :class="[active ? 'bg-blue-500 text-white' : 'text-gray-900']"
                  @click="onOpenCreateNamespace"
                >
                  {{ t("bounded_context_namespace.button.create") }}
                </button>
              </MenuItem>
              <MenuItem v-slot="{ active }">
                <button
                  class="group w-full rounded-md px-2 py-2 text-left text-sm"
                  :class="[active ? 'bg-blue-500 text-white' : 'text-gray-900']"
                  @click="onOpenCreateNamespaceFromTemplate"
                >
                  {{ t("bounded_context_namespace.button.create_from_template") }}
                </button>
              </MenuItem>
            </div>
          </MenuItems>
        </transition>
      </Menu>
    </div>

    <div v-if="boundedContextNamespaces.length === 0" class="mt-4 text-sm italic text-gray-700">
      {{ t("bounded_context_namespace.empty") }}
    </div>

    <div v-for="namespace of boundedContextNamespaces" class="mt-4" :key="namespace.name">
      <ContextureNamespace
        :namespace="namespace"
        @save="(labels) => onSaveNamespaceLabels(namespace.id, labels)"
        @delete-label="(label) => onDeleteNamespaceLabel(namespace.id, label.id)"
        @delete-namespace="() => onDeleteNamespace(namespace)"
      ></ContextureNamespace>
    </div>

    <ContextureModal
      :title="t('bounded_context_namespace.dialog.add.title')"
      :is-open="isCreateNamespaceOpen"
      @cancel="onCloseCreateNamespace"
    >
      <div class="flex max-w-full flex-col gap-y-4 pt-4 sm:w-[400px]">
        <ContextureHelpfulErrorAlert v-if="submitError" v-bind="submitError" class="mb-4" />

        <Form @submit="onAddNamespace">
          <ContextureInputText
            name="namespace"
            class="ml-2 grow"
            :label="t('bounded_context_namespace.dialog.add.form.name')"
            :placeholder="t('common.namespaces')"
            v-model="createNamespaceVal.name"
            :rules="namespaceNameRule"
          ></ContextureInputText>

          <div class="mt-4">
            <ContexturePrimaryButton type="submit" :label="t('bounded_context_namespace.dialog.add.button')">
              <template #left>
                <Icon:materialSymbols:add class="mr-2" />
              </template>
            </ContexturePrimaryButton>
          </div>
        </Form>
      </div>
    </ContextureModal>

    <ContextureModal
      :title="t('bounded_context_namespace.dialog.add_from_template.title')"
      :is-open="isCreateNamespaceFromTemplateOpen"
      @cancel="onCloseCreateNamespaceFromTemplate"
    >
      <div class="flex max-w-full flex-col gap-y-4 pt-4 sm:w-[600px]">
        <ContextureHelpfulErrorAlert v-if="submitError" v-bind="submitError" class="mb-4" />

        <Form @submit="onAddNamespace" class="space-y-4">
          <ContextureListbox
            name="name"
            key-prop="name"
            :display-value="(d) => d.name"
            :options="selectableTemplates"
            v-model="selectedTemplate"
            @selected="onNamespaceTemplateChange"
            :rules="requiredObjectRule"
          />

          <div
            class="flex items-center"
            v-for="(templateItem, index) of selectedTemplate?.template"
            :key="`template-${index}`"
          >
            <NamespaceValueAutocomplete
              v-model="createNamespaceVal.labels[index].value"
              :name="`template-${templateItem.name}-${index}`"
              :label="templateItem.name"
              :placeholder="templateItem.placeholder"
              :description="templateItem.description"
              :namespace-label-name="templateItem.name"
            />
          </div>

          <div class="mt-4">
            <ContexturePrimaryButton type="submit" :label="t('bounded_context_namespace.dialog.add.button')">
              <template #left>
                <Icon:materialSymbols:add class="mr-2" />
              </template>
            </ContexturePrimaryButton>
          </div>
        </Form>
      </div>
    </ContextureModal>
  </div>

  <ContextureEntityNotFound :text="t('bounded_context_namespace.not_found', { boundedContextId })" v-else />
</template>

<script setup lang="ts">
import { Menu, MenuButton, MenuItem, MenuItems } from "@headlessui/vue";
import { toFieldValidator } from "@vee-validate/zod";
import { storeToRefs } from "pinia";
import { Form } from "vee-validate";
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import * as zod from "zod";
import ContextureNamespace from "~/components/bounded-context/namespace/ContextureNamespace.vue";
import ContextureEntityNotFound from "~/components/core/ContextureEntityNotFound.vue";
import ContextureHelpfulErrorAlert, {
  HelpfulErrorProps,
} from "~/components/primitives/alert/ContextureHelpfulErrorAlert.vue";
import ContexturePrimaryButton from "~/components/primitives/button/ContexturePrimaryButton.vue";
import ContextureInputText from "~/components/primitives/input/ContextureInputText.vue";
import ContextureListbox from "~/components/primitives/listbox/ContextureListbox.vue";
import ContextureModal from "~/components/primitives/modal/ContextureModal.vue";
import { isUniqueIn } from "~/core/validation";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import useConfirmationModalStore from "~/stores/confirmationModal";
import { useNamespaceTemplatesStore } from "~/stores/namespace-templates";
import { useNamespaces } from "~/stores/namespaces";
import { CreateNamespace, CreateNamespaceLabel, Namespace, NamespaceId, NamespaceLabelId } from "~/types/namespace";
import { NamespaceTemplate, NamespaceTemplateItem } from "~/types/namespace-templates";
import { useRouteParams } from "@vueuse/router";
import { requiredObjectRule } from "~/core/validationRules";
import { useAuthStore } from "~/stores/auth";
import NamespaceValueAutocomplete from "~/components/bounded-context/namespace/NamespaceValueAutocomplete.vue";

const { loading, activeBoundedContext } = storeToRefs(useBoundedContextsStore());
const { createNamespace, deleteNamespace, createNamespaceLabel, deleteNamespaceLabel } = useNamespaces();
const { namespaceTemplates } = storeToRefs(useNamespaceTemplatesStore());
const confirmationModal = useConfirmationModalStore();
const { t } = useI18n();
const { canModify } = useAuthStore();
const selectableTemplates = computed(() =>
  namespaceTemplates.value
    .filter((n) => !boundedContextNamespaces.value.map((n) => n.name).includes(n.name))
    .sortAlphabeticallyBy((n) => n.name)
);
const isCreateNamespaceOpen = ref(false);
const isCreateNamespaceFromTemplateOpen = ref(false);
const selectedTemplate = ref<NamespaceTemplate>();
const boundedContextNamespaces = computed(
  () =>
    activeBoundedContext.value?.namespaces
      .sortAlphabeticallyBy((n) => n.name)
      .map((n) => ({ ...n, labels: n.labels.sortAlphabeticallyBy((l) => l.name) })) || []
);
const submitError = ref<HelpfulErrorProps>();
const boundedContextId = useRouteParams("id");
const namespaceNameRule = toFieldValidator(
  zod
    .string()
    .min(1)
    .superRefine((arg, ctx) =>
      isUniqueIn<CreateNamespace>(arg, ctx, {
        field: "name",
        in: activeBoundedContext.value!.namespaces,
        errorMessage: `The namespace with name '${arg}' has already been defined before. Please use a distinct, case insensitive name.`,
      })
    )
);

async function onAddNamespace() {
  submitError.value = undefined;
  const { data, error } = await createNamespace(activeBoundedContext.value!.id, createNamespaceVal.value);

  if (error.value) {
    submitError.value = {
      friendlyMessage: t("bounded_context_namespace.error.create"),
      error: error.value,
      response: data.value,
    };
  } else {
    isCreateNamespaceOpen.value = false;
    isCreateNamespaceFromTemplateOpen.value = false;
    resetCreateNamespace();
  }
}

async function onDeleteNamespace(namespace: Namespace) {
  confirmationModal.open(
    t("bounded_context_namespace.delete.namespace.confirm.title", { namespaceName: namespace.name }),
    t("bounded_context_namespace.delete.namespace.confirm.body"),
    t("bounded_context_namespace.delete.namespace.confirm.button"),
    async () => {
      const { data, error } = await deleteNamespace(activeBoundedContext.value!.id, namespace.id);

      if (error.value) {
        submitError.value = {
          friendlyMessage: t("bounded_context_namespace.error.delete"),
          error: error.value,
          response: data.value,
        };
      }
    }
  );
}

async function onSaveNamespaceLabels(namespaceId: NamespaceId, namespaceLabels: CreateNamespaceLabel[]) {
  for (const label of namespaceLabels) {
    await createNamespaceLabel(activeBoundedContext.value!.id, namespaceId, label);
  }
}

async function onDeleteNamespaceLabel(namespaceId: NamespaceId, labelId: NamespaceLabelId) {
  await deleteNamespaceLabel(activeBoundedContext.value!.id, namespaceId, labelId);
}

function onOpenCreateNamespace() {
  isCreateNamespaceOpen.value = true;
}

function onCloseCreateNamespace() {
  resetCreateNamespace();
}

function onOpenCreateNamespaceFromTemplate() {
  isCreateNamespaceFromTemplateOpen.value = true;
}

function onCloseCreateNamespaceFromTemplate() {
  resetCreateNamespace();
}

const createNamespaceVal = ref<CreateNamespace>({
  labels: [],
  name: "",
  template: undefined,
});

function onNamespaceTemplateChange(value: NamespaceTemplate) {
  createNamespaceVal.value = {
    template: value.id,
    name: value.name,
    labels: value.template.map((labelTemplate: NamespaceTemplateItem) => {
      return {
        templateId: value.id,
        name: labelTemplate.name,
        value: "",
      };
    }),
  };
}

function resetCreateNamespace() {
  submitError.value = undefined;
  isCreateNamespaceOpen.value = false;
  isCreateNamespaceFromTemplateOpen.value = false;
  setTimeout(() => {
    selectedTemplate.value = undefined;
    createNamespaceVal.value = {
      labels: [],
      name: "",
      template: undefined,
    };
  }, 500);
}
</script>
