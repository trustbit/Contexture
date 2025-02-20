<template>
  <Form class="flex w-9/12 flex-col gap-8" @submit="submit">
    <ContextureInputText
      :description="t('bounded_context_canvas.edit.form.description.key')"
      :model-value="editModel.shortName"
      name="key"
      :label="t('bounded_context_canvas.edit.form.label.key')"
      :rules="boundedContextShortNameValidator"
    />

    <ContextureInputText
      :model-value="editModel.name"
      name="name"
      :description="t('bounded_context_canvas.edit.form.description.name')"
      :label="t('bounded_context_canvas.edit.form.label.name')"
      :rules="requiredString"
      required
    />

    <div>
      <LoadingWrapper :is-loading="isLoading">
        <ContexturePrimaryButton :label="t('common.save')" type="submit">
          <template #left>
            <Icon:material-symbols:check class="mr-1 h-6 w-6" />
          </template>
        </ContexturePrimaryButton>
      </LoadingWrapper>
    </div>
  </Form>
</template>

<script setup lang="ts">
import { toFieldValidator } from "@vee-validate/zod";
import { Form } from "vee-validate";
import { computed, Ref, toRef } from "vue";
import { useI18n } from "vue-i18n";
import * as zod from "zod";
import { boundedContextShortNameValidationSchema } from "~/components/core/change-short-name/changeShortNameValidationSchema";
import ContexturePrimaryButton from "~/components/primitives/button/ContexturePrimaryButton.vue";
import ContextureInputText from "~/components/primitives/input/ContextureInputText.vue";
import { BoundedContext } from "~/types/boundedContext";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import { storeToRefs } from "pinia";
import { ActionProps, useActionWithLoading } from "~/components/primitives/button/util/useActionWithLoading";
import LoadingWrapper from "~/components/primitives/button/util/LoadingWrapper.vue";

interface Props extends ActionProps {
  initialValue: BoundedContext;
}

const props = defineProps<Props>();
const emit = defineEmits(["submit"]);
const { t } = useI18n();
const editModel: Ref<BoundedContext> = toRef(props, "initialValue");
const { isLoading, handleAction } = useActionWithLoading(props);
const requiredString = toFieldValidator(zod.string().min(1, t("validation.required")));

const store = useBoundedContextsStore();
const { boundedContextsByDomainId } = storeToRefs(store);
const boundedContextShortNameValidator = computed(() =>
  toFieldValidator(
    boundedContextShortNameValidationSchema(
      boundedContextsByDomainId.value[props.initialValue.parentDomainId].filter((bc) => bc.id !== props.initialValue.id)
    )
  )
);

async function submit(values: any) {
  emit("submit", values);
  await handleAction(values);
}
</script>

<style scoped></style>
