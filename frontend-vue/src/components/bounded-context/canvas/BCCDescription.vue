<template>
  <ContextureBoundedContextCanvasElement
    :title="t('bounded_context_canvas.description.title')"
    :title-icon="icon"
    :is-editable="canModify"
    :edit-mode="editMode"
    @close="onClose"
    @open="editMode = true"
  >
    <div v-if="editMode">
      <ContextureHelpfulErrorAlert v-if="submitError" v-bind="submitError" class="mb-4" />
      <ContextureTextarea
        v-model="description"
        name="description"
        description="A few sentences describing the why and what of the context in business language. No technical details here."
      />
      <ContexturePrimaryButton :label="t('common.save')" class="mt-4" size="sm" @click="onUpdate">
        <template #left>
          <Icon:material-symbols:check class="mr-1" />
        </template>
      </ContexturePrimaryButton>
    </div>
    <div v-else>
      <div v-if="description">
        <div class="h-36 overflow-y-auto whitespace-pre-line text-gray-700">
          <p>{{ description }}</p>
        </div>
      </div>
      <span v-else class="italic text-gray-700">({{ t("bounded_context_canvas.description.empty") }})</span>
    </div>
  </ContextureBoundedContextCanvasElement>
</template>

<script setup lang="ts">
import { storeToRefs } from "pinia";
import { ref } from "vue";
import { useI18n } from "vue-i18n";
import ContextureBoundedContextCanvasElement from "~/components/bounded-context/canvas/ContextureBoundedContextCanvasElement.vue";
import ContextureHelpfulErrorAlert from "~/components/primitives/alert/ContextureHelpfulErrorAlert.vue";
import ContexturePrimaryButton from "~/components/primitives/button/ContexturePrimaryButton.vue";
import ContextureTextarea from "~/components/primitives/input/ContextureTextarea.vue";
import { useAuthStore } from "~/stores/auth";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import IconsMaterialSymbolsFormatQuoteOutline from "~icons/material-symbols/format-quote-outline";

const icon = IconsMaterialSymbolsFormatQuoteOutline;
const store = useBoundedContextsStore();
const { activeBoundedContext } = storeToRefs(store);

const { t } = useI18n();
const { canModify } = useAuthStore();
const description = ref(activeBoundedContext.value.description);
const submitError = ref();
const editMode = ref(false);

async function onUpdate() {
  submitError.value = null;
  const res = await store.updateDescription(activeBoundedContext.value.id, description.value);

  if (res.error.value) {
    submitError.value = {
      friendlyMessage: t("bounded_context_canvas.description.error.update"),
      error: res.error.value,
      response: res.data.value,
    };
  } else {
    editMode.value = false;
  }
}

function onClose() {
  submitError.value = null;
  editMode.value = false;
  description.value = activeBoundedContext.value?.description;
}
</script>
