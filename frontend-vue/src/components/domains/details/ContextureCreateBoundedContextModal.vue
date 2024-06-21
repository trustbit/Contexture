<template>
  <ContextureModal :title="t('domains.modal.create_bounded_context.title')" :is-open="isOpen" @cancel="onCancel">
    <ContextureHelpfulErrorAlert v-bind="submitError" />
    <div class="pt-8 sm:w-96">
      <ContextureDynamicForm
        :action="onAddNewBoundedContext"
        :schema="form"
        :button-props="{
          label: t('domains.modal.create_bounded_context.form.submit'),
        }"
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
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import { Domain } from "~/types/domain";
import { CreateBoundedContext } from "~/types/boundedContext";
import ContextureChangeKey from "~/components/core/change-short-name/ContextureChangeShortName.vue";

interface Props {
  isOpen: boolean;
  parentDomain: Domain;
}

interface Emits {
  (e: "close"): void;
}

const props = defineProps<Props>();
const emit = defineEmits<Emits>();
const { t } = useI18n();
const router = useRouter();
const { createBoundedContext } = useBoundedContextsStore();

const form: DynamicFormSchema<CreateBoundedContext> = {
  fields: [
    {
      name: "name",
      component: ContextureInputText,
      componentProps: {
        label: t("domains.modal.create_bounded_context.form.fields.name.label"),
        description: t("bounded_context_canvas.edit.form.description.name"),
        required: true,
        rules: toFieldValidator(zod.string().min(1)),
      },
    },
    {
      name: "shortName",
      component: ContextureChangeKey,
      componentProps: {
        label: t("domains.modal.create_bounded_context.form.fields.short_name.label"),
        description: t("bounded_context_canvas.edit.form.description.key"),
      },
    },
    {
      name: "description",
      component: ContextureInputText,
      componentProps: {
        label: t("domains.modal.create_bounded_context.form.fields.description.label"),
        description: t("bounded_context_canvas.description.description"),
        required: true,
        rules: toFieldValidator(zod.string().min(1)),
      },
    },
  ],
};

const submitError = ref<HelpfulErrorProps>();

async function onAddNewBoundedContext(createDomain: CreateBoundedContext) {
  submitError.value = undefined;
  const { error, data } = await createBoundedContext(props.parentDomain.id, createDomain);

  if (error.value) {
    submitError.value = {
      friendlyMessage: t("domains.modal.create_bounded_context.error.submit"),
      error: error.value,
      response: data.value,
    };
  } else {
    close();
    await router.push(`/boundedContext/${data.value?.id}/canvas`);
  }
}

function onCancel() {
  close();
}

function close() {
  emit("close");
}
</script>
