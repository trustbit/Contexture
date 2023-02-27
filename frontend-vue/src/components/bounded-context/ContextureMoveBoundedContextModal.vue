<template>
  <ContextureModal
    :is-open="isOpen"
    :title="
      t('bounded_context.card.move_dialog.title', {
        boundedContextName: boundedContext.name,
      })
    "
    @close="onClose"
  >
    <div class="w-96 min-w-full">
      <ContextureHelpfulErrorAlert v-bind="submitError"></ContextureHelpfulErrorAlert>

      <div class="mt-4 text-sm">
        {{ t("bounded_context.card.move_dialog.description") }}
      </div>

      <ContextureAutocomplete
        v-model="selectedDomain"
        class="mt-4"
        :display-value="(d) => d.name"
        :suggestions="suggestions"
        placeholder="Search for a domain"
        @complete="onComplete"
      />

      <div class="mt-8">
        <ContexturePrimaryButton
          class="w-full justify-center"
          @click="onSubmit"
          :label="t('bounded_context.card.move_dialog.confirm_button')"
        />
      </div>
    </div>
  </ContextureModal>
</template>

<script lang="ts" setup>
import { Ref } from "vue";
import { ref } from "vue";
import { useI18n } from "vue-i18n";
import ContextureAutocomplete from "~/components/primitives/autocomplete/ContextureAutocomplete.vue";
import ContexturePrimaryButton from "~/components/primitives/button/ContexturePrimaryButton.vue";
import ContextureHelpfulErrorAlert, {
  HelpfulErrorProps,
} from "~/components/primitives/alert/ContextureHelpfulErrorAlert.vue";
import ContextureModal from "~/components/primitives/modal/ContextureModal.vue";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import { useDomainsStore } from "~/stores/domains";
import { BoundedContext } from "~/types/boundedContext";
import { Domain } from "~/types/domain";

interface Props {
  boundedContext: BoundedContext;
  isOpen: boolean;
}

interface Emits {
  (e: "close"): void;
}

const props = defineProps<Props>();
const emit = defineEmits<Emits>();

const { allDomains } = useDomainsStore();
const { moveBoundedContext } = useBoundedContextsStore();
const { t } = useI18n();
const selectedDomain = ref<Domain>();
const suggestions: Ref<Domain[]> = ref<Domain[]>(allDomains);
let submitError = ref<HelpfulErrorProps>();

async function onSubmit() {
  submitError.value = null;
  const res = await moveBoundedContext(props.boundedContext.id, selectedDomain.value!.id);

  if (res.error.value) {
    submitError.value = {
      friendlyMessage: t("bounded_context.card.move_dialog.error"),
      error: res.error.value,
      response: res.data.value,
    };
  } else {
    emit("close");
  }
}

function onComplete(query: string) {
  suggestions.value =
    query === ""
      ? allDomains
      : allDomains.filter((option) => {
          return option.name.toLowerCase().includes(query.toLowerCase());
        });
}

function onClose() {
  closeDialog();
}

function closeDialog() {
  emit("close");
}
</script>
