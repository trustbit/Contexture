<template>
  <Form class="flex w-9/12 flex-col gap-8" @submit="submit">
    <ContextureChangeKey
      :description="t('bounded_context_canvas.edit.form.description.key')"
      :model-value="editModel.shortName"
      name="key"
      :label="t('bounded_context_canvas.edit.form.label.key')"
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
      <ContexturePrimaryButton :label="t('common.save')" type="submit">
        <template #left>
          <Icon:material-symbols:check class="mr-1 h-6 w-6" />
        </template>
      </ContexturePrimaryButton>
    </div>
  </Form>
</template>

<script setup lang="ts">
import { toFieldValidator } from "@vee-validate/zod";
import { Form } from "vee-validate";
import { Ref, toRef } from "vue";
import { useI18n } from "vue-i18n";
import * as zod from "zod";
import ContexturePrimaryButton from "~/components/primitives/button/ContexturePrimaryButton.vue";
import ContextureChangeKey from "~/components/core/change-short-name/ContextureChangeShortName.vue";
import ContextureInputText from "~/components/primitives/input/ContextureInputText.vue";
import { BoundedContext } from "~/types/boundedContext";

interface Props {
  initialValue: BoundedContext;
}

const props = defineProps<Props>();
const emit = defineEmits(["submit"]);
const { t } = useI18n();
const editModel: Ref<BoundedContext> = toRef(props, "initialValue");

const requiredString = toFieldValidator(zod.string().min(1, t("validation.required")));

function submit(values: any) {
  emit("submit", values);
}
</script>

<style scoped></style>
