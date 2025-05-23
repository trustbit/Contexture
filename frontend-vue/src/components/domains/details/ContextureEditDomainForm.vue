<template>
  <Form class="flex w-9/12 flex-col gap-8" @submit="handleAction">
    <ContextureChangeKey
      :model-value="editModel.shortName"
      name="key"
      :description="t('domains.details.edit.form.description.key')"
      :label="t('domains.details.edit.form.label.key')"
      :rules="requiredString"
      required
    />

    <ContextureInputText
      :model-value="editModel.name"
      name="name"
      :description="t('domains.details.edit.form.description.name')"
      :label="t('domains.details.edit.form.label.name')"
      :rules="requiredString"
      required
    />

    <ContextureTextarea
      :model-value="editModel.vision"
      name="vision"
      :description="t('domains.details.edit.form.description.vision')"
      :label="t('domains.details.edit.form.label.vision')"
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
import { Ref, toRef } from "vue";
import { useI18n } from "vue-i18n";
import * as zod from "zod";
import ContexturePrimaryButton from "~/components/primitives/button/ContexturePrimaryButton.vue";
import ContextureChangeKey from "~/components/core/change-short-name/ContextureChangeShortName.vue";
import ContextureInputText from "~/components/primitives/input/ContextureInputText.vue";
import ContextureTextarea from "~/components/primitives/input/ContextureTextarea.vue";
import { Domain } from "~/types/domain";
import LoadingWrapper from "~/components/primitives/button/util/LoadingWrapper.vue";
import { ActionProps, useActionWithLoading } from "~/components/primitives/button/util/useActionWithLoading";

interface Props extends ActionProps {
  domain: Domain;
}

const props = defineProps<Props>();
const { t } = useI18n();
const editModel: Ref<Domain> = toRef(props, "domain");
const { isLoading, handleAction } = useActionWithLoading(props);
const requiredString = toFieldValidator(zod.string().min(1, t("validation.required")));
</script>

<style scoped></style>
