<template>
  <div>
    <span class="font-bold">{{ namespaceName }}</span>

    <form @submit="add" autocomplete="off">
      <div class="mt-4 sm:flex">
        <ContextureAutocomplete
          v-model="selected.key"
          :suggestions="keySuggestions"
          :label="t('search.field')"
          :display-value="(l) => l"
          :allow-custom-values="true"
          @complete="searchSuggestions"
          class="sm:w-1/2"
        />

        <ContextureInputText
          :label="t('search.value')"
          v-model="selected.value"
          :skip-validation="true"
          class="sm:ml-2 sm:w-1/2"
        />
      </div>

      <div class="mt-4 justify-end gap-x-4 sm:flex">
        <ContexturePrimaryButton type="submit" :label="t('search.add_filter')" size="md" />
      </div>
    </form>
  </div>
</template>

<script setup lang="ts">
import { Ref, ref } from "vue";
import ContextureAutocomplete from "~/components/primitives/autocomplete/ContextureAutocomplete.vue";
import ContexturePrimaryButton from "~/components/primitives/button/ContexturePrimaryButton.vue";
import ContextureInputText from "~/components/primitives/input/ContextureInputText.vue";
import { useI18n } from "vue-i18n";

interface Props {
  namespaceName: string;
  labels: string[];
}

const props = defineProps<Props>();
const emit = defineEmits<{
  (e: "add", event: any): void;
}>();
const {t} = useI18n();
const keyTerm = ref<string>("");

const keySuggestions = ref<string[]>(
  props.labels.filter((label) => label.toLowerCase().includes(keyTerm.value?.toLowerCase()))
);

function searchSuggestions(query: string): void {
  keySuggestions.value = props.labels.filter((l) => l.toLowerCase().includes(query.toLowerCase()));
}

function add() {
  emit("add", {
    key: selected.value?.key,
    value: selected.value.value,
  });
  selected.value = {
    key: "",
    value: "",
  };
}

const selected: Ref<{ key?: string; value?: string }> = ref<{
  key?: string;
  value?: string;
}>({});
</script>
