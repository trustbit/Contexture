<template>
  <ContextureModal
    :is-open="isOpen"
    :title="
      t('domains.card.move_dialog.title', {
        domainName: domain.name,
      })
    "
    @close="onClose"
  >
    <div class="mt-4 sm:w-96">
      <ContextureHelpfulErrorAlert v-bind="submitError"></ContextureHelpfulErrorAlert>

      <div v-if="isParentDomain" class="mt-4 text-sm">
        {{ t("domains.card.move_dialog.already_parent_domain") }}
      </div>

      <div v-if="!isParentDomain" class="mt-4">
        <ContextureRadio
          v-model="selectedMoveOption"
          v-for="moveOption of moveOptions"
          name="moveOption"
          :label="moveOption.label"
          :value="moveOption.value"
          :key="moveOption.value"
        ></ContextureRadio>
      </div>

      <ContextureAutocomplete
        v-model="selectedDomain"
        v-if="showAutocomplete"
        class="mt-4"
        :display-value="(d) => d.name"
        :suggestions="suggestions"
        :placeholder="t('domains.card.move_dialog.search_placeholder')"
        @complete="onComplete"
      />

      <div class="mt-8">
        <ContexturePrimaryButton
          class="w-full justify-center"
          @click="onSubmit"
          :label="t('domains.card.move_dialog.confirm_button')"
        />
      </div>
    </div>
  </ContextureModal>
</template>

<script lang="ts" setup>
import { computed, Ref, ref } from "vue";
import { useI18n } from "vue-i18n";
import ContextureAutocomplete from "~/components/primitives/autocomplete/ContextureAutocomplete.vue";
import ContexturePrimaryButton from "~/components/primitives/button/ContexturePrimaryButton.vue";
import ContextureHelpfulErrorAlert, {
  HelpfulErrorProps,
} from "~/components/primitives/alert/ContextureHelpfulErrorAlert.vue";
import ContextureModal from "~/components/primitives/modal/ContextureModal.vue";
import ContextureRadio from "~/components/primitives/radio/ContextureRadio.vue";
import { useDomainsStore } from "~/stores/domains";
import { Domain } from "~/types/domain";

interface Props {
  domain: Domain;
  isOpen: boolean;
}

interface Emits {
  (e: "close"): void;
}

const props = defineProps<Props>();
const emit = defineEmits<Emits>();

enum MoveOption {
  PROMOTE_TO_ROOT,
  MOVE_TO_SUBDOMAIN,
}

const moveOptions = [
  {
    value: MoveOption.PROMOTE_TO_ROOT,
    label: "Promote to root domain",
  },
  {
    value: MoveOption.MOVE_TO_SUBDOMAIN,
    label: "Make a subdomain of",
  },
];
const { allDomains, moveDomain } = useDomainsStore();
const { t } = useI18n();
const selectedMoveOption = ref<MoveOption>(MoveOption.PROMOTE_TO_ROOT);
const selectedDomain = ref<Domain>();
const suggestions: Ref<Domain[]> = ref<Domain[]>(allDomains.filter((d) => d.id !== props.domain.id) || []);
let submitError = ref<HelpfulErrorProps>();
const isParentDomain = computed(() => props.domain && !props.domain.parentDomainId);
const showAutocomplete = computed(() => {
  return selectedMoveOption?.value !== MoveOption.PROMOTE_TO_ROOT || !props.domain.parentDomainId;
});

async function onSubmit() {
  submitError.value = undefined;
  const res = await moveDomain(props.domain.id, selectedDomain.value?.id);

  if (res.error.value) {
    submitError.value = {
      friendlyMessage: t("domains.card.move_dialog.error"),
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
      ? allDomains.filter((d) => d.id !== props.domain.id)
      : allDomains.filter((option) => {
          return option.id !== props.domain.id && option.name.toLowerCase().includes(query.toLowerCase());
        });
}

function onClose() {
  closeDialog();
}

function closeDialog() {
  emit("close");
}
</script>
