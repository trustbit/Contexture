<template>
  <ContextureModal :title="t('domains.modal.create.title')" :is-open="isOpen" @cancel="onCancel">
    <div class="mt-4 sm:w-96">
      <ContextureHelpfulErrorAlert v-bind="submitError" />
      <ContextureDynamicForm
        class="mt-4"
        @submit="onAddNewDomain"
        :schema="form"
        :button-props="{ label: t('domains.modal.create.form.submit') }"
        button-class="flex justify-center w-full"
      />
    </div>
  </ContextureModal>
</template>

<script setup lang="ts">
import { toFieldValidator } from "@vee-validate/zod";
import { ref } from "vue";
import { useI18n } from "vue-i18n";
import { useRouter } from "vue-router";
import * as zod from "zod";
import ContextureDynamicForm from "~/components/primitives/dynamic-form/ContextureDynamicForm.vue";
import { DynamicFormSchema } from "~/components/primitives/dynamic-form/dynamicForm";
import ContextureHelpfulErrorAlert, {
  HelpfulErrorProps,
} from "~/components/primitives/alert/ContextureHelpfulErrorAlert.vue";
import ContextureInputText from "~/components/primitives/input/ContextureInputText.vue";
import ContextureModal from "~/components/primitives/modal/ContextureModal.vue";
import { useDomainsStore } from "~/stores/domains";
import { CreateDomain } from "~/types/domain";

interface Props {
  isOpen: boolean;
}

interface Emits {
  (e: "close"): void;
}

defineProps<Props>();
const emit = defineEmits<Emits>();
const { t } = useI18n();
const router = useRouter();
const { createDomain } = useDomainsStore();

const form: DynamicFormSchema<CreateDomain> = {
  fields: [
    {
      name: "name",
      component: ContextureInputText,
      componentProps: {
        label: t("domains.modal.create.form.fields.name.label"),
        required: true,
        rules: toFieldValidator(zod.string().min(1)),
      },
    },
  ],
};

const submitError = ref<HelpfulErrorProps>();

async function onAddNewDomain(domain: CreateDomain) {
  submitError.value = null;
  const { error, data } = await createDomain(domain);

  if (error.value) {
    submitError.value = {
      friendlyMessage: t("domains.modal.create.error.submit"),
      error: error.value,
      response: data.value,
    };
  } else {
    close();
    await router.push(`/domain/${data.value?.id}`);
  }
}

function onCancel() {
  close();
}

function close() {
  emit("close");
}
</script>
