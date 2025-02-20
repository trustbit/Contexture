<template>
  <ContextureBoundedContextCanvasElement
    :title="t('bounded_context_canvas.key.title')"
    :title-icon="icon"
    :is-editable="canModify"
    :edit-mode="editMode"
    @close="onClose"
    @open="editMode = true"
  >
    <div v-if="editMode">
      <ContextureHelpfulErrorAlert v-if="submitError" v-bind="submitError" class="mb-4" />
      <Form @submit="onUpdate">
        <ContextureInputText v-model="key" :rules="boundedContextShortNameValidator" />

        <ContexturePrimaryButton type="submit" :label="t('common.save')" class="mt-4" size="sm">
          <template #left>
            <Icon:material-symbols:check class="mr-1" />
          </template>
        </ContexturePrimaryButton>
      </Form>
    </div>
    <div v-else>
      <span v-if="activeBoundedContext.shortName" class="text-gray-700">{{ activeBoundedContext.shortName }}</span>
      <span v-else class="italic text-gray-700">({{ t("bounded_context_canvas.key.empty") }})</span>
    </div>
  </ContextureBoundedContextCanvasElement>
</template>

<script setup lang="ts">
import { toFieldValidator } from "@vee-validate/zod";
import { storeToRefs } from "pinia";
import { Form } from "vee-validate";
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import ContextureBoundedContextCanvasElement from "~/components/bounded-context/canvas/ContextureBoundedContextCanvasElement.vue";
import { boundedContextShortNameValidationSchema } from "~/components/core/change-short-name/changeShortNameValidationSchema";
("~/components/core/change-short-name/changeShortNameValidationSchema");
import ContextureHelpfulErrorAlert, {
  HelpfulErrorProps,
} from "~/components/primitives/alert/ContextureHelpfulErrorAlert.vue";
import ContexturePrimaryButton from "~/components/primitives/button/ContexturePrimaryButton.vue";
import ContextureInputText from "~/components/primitives/input/ContextureInputText.vue";
import { useAuthStore } from "~/stores/auth";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import IconsMaterialSymbolsFormatKeyOutline from "~icons/material-symbols/key-outline";

const icon = IconsMaterialSymbolsFormatKeyOutline;
const store = useBoundedContextsStore();
const { activeBoundedContext, boundedContextsByDomainId } = storeToRefs(store);
const { canModify } = useAuthStore();
const { t } = useI18n();
const key = ref(activeBoundedContext.value.shortName);
const submitError = ref<HelpfulErrorProps | undefined>();
const editMode = ref(false);
const boundedContextShortNameValidator = computed(() =>
  toFieldValidator(
    boundedContextShortNameValidationSchema(
      boundedContextsByDomainId.value[activeBoundedContext.value.parentDomainId].filter(
        (bc) => bc.id !== activeBoundedContext.value.id
      )
    )
  )
);

async function onUpdate() {
  submitError.value = undefined;
  const res = await store.updateKey(activeBoundedContext.value.id, key.value);

  if (res.error.value) {
    submitError.value = {
      friendlyMessage: t("bounded_context_canvas.key.error.update"),
      error: res.error.value,
      response: res.data.value,
    };
  } else {
    editMode.value = false;
  }
}

function onClose() {
  submitError.value = undefined;
  editMode.value = false;
  key.value = activeBoundedContext.value?.shortName;
}
</script>
