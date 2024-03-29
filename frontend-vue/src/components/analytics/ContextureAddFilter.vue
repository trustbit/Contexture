<template>
  <div>
    <span class="font-bold">{{ namespaceName }}</span>

    <form @submit="handleAddFilter" autocomplete="off">
      <div class="mt-4 sm:flex">
        <ContextureAutocomplete
          v-model="selected.key"
          :suggestions="keySuggestions"
          :label="t('search.field')"
          :display-value="(l) => l"
          :allow-custom-values="true"
          @complete="searchKeySuggestions"
          class="sm:w-1/2"
        />

        <ContextureAutocomplete
          v-model="selected.value"
          :suggestions="valueSuggestions"
          :label="t('search.value')"
          :display-value="(l) => l"
          :allow-custom-values="true"
          @complete="searchValueSuggestions"
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
import { computed } from "vue";
import { Ref, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import ContextureAutocomplete from "~/components/primitives/autocomplete/ContextureAutocomplete.vue";
import ContexturePrimaryButton from "~/components/primitives/button/ContexturePrimaryButton.vue";
import { NamespaceLabel } from "~/types/namespace";

interface Props {
  namespaceName: string;
  labels: NamespaceLabel[];
}

const props = defineProps<Props>();
const emit = defineEmits<{
  (e: "add", event: any): void;
}>();
const { t } = useI18n();

const selected: Ref<{ key?: string; value?: string }> = ref<{
  key?: string;
  value?: string;
}>({});

const labelNames = computed(() => [...new Set(props.labels.map((label) => label.name))]);
const keySuggestions = ref<string[]>(labelNames.value);
const valueSuggestions = ref<string[]>([]);

function searchKeySuggestions(query: string): void {
  keySuggestions.value = labelNames.value.filter((label) => label.toLowerCase().includes(query.toLowerCase()));
}

function searchValueSuggestions(query: string): void {
  valueSuggestions.value = [
    ...new Set(
      props.labels
        .filter((label) => label.name === selected.value.key)
        .map((label) => label.value)
        .filter((label) => label.toLowerCase().includes(query.toLowerCase()))
    ),
  ];
}

function handleAddFilter() {
  emit("add", {
    key: selected.value?.key,
    value: selected.value.value,
  });
  selected.value = {
    key: "",
    value: "",
  };
}

watch(
  () => selected.value.key,
  () => {
    valueSuggestions.value = [
      ...new Set(props.labels.filter((label) => label.name === selected.value.key).map((label) => label.value)),
    ];
  }
);
</script>
