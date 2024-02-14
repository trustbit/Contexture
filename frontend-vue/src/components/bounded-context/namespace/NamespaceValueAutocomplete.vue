<template>
  <ContextureAutocomplete
    v-model="model"
    class="ml-2 grow"
    :placeholder="t('common.value')"
    :suggestions="suggestions"
    :display-value="(l: any) => l"
    :allow-custom-values="true"
    :nullable="true"
    @complete="searchKeySuggestions($event)"
  >
    <template #customValue>
      <div class="flex items-center justify-items-center align-middle">
        <Icon:material-symbols:add aria-hidden="true" class="mr-2" />
        <span>{{ t("common.create-new", { entityName: inputText }) }}</span>
      </div>
    </template>
  </ContextureAutocomplete>
</template>

<script setup lang="ts">
import ContextureAutocomplete from "~/components/primitives/autocomplete/ContextureAutocomplete.vue";
import { useNamespaces } from "~/stores/namespaces";
import { ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import Fuse, { IFuseOptions } from "fuse.js";

interface Props {
  namespaceLabelName?: string;
}

const props = defineProps<Props>();

const fuseOptions: IFuseOptions<string> = {
  includeScore: true,
  includeMatches: true,
  threshold: 0.5,
  location: 0,
  distance: 50,
  minMatchCharLength: 1,
  keys: ["name"],
};

const { findNamespaceLabelValuesByLabelName, namespaceLabelValues } = useNamespaces();
const { t } = useI18n();
const suggestions = ref<string[]>(getSuggestions(""));
const model = defineModel<string>();
const inputText = ref("");
const suggestionLimit = 10;

watch(props, () => {
  searchKeySuggestions("");
});

function getSuggestionsForSelectedLabel(query: string) {
  const fuse = new Fuse(findNamespaceLabelValuesByLabelName(props.namespaceLabelName), fuseOptions);
  return fuse.search(query).map((result: { item: string }) => result.item);
}

function getSuggestionsForAllValues(query: string) {
  const fuse = new Fuse(namespaceLabelValues, fuseOptions);
  return fuse.search(query).map((result: { item: string }) => result.item);
}

function getSuggestions(query?: string) {
  if (!query) {
    return findNamespaceLabelValuesByLabelName(props.namespaceLabelName);
  }

  const suggestionsForLabel = getSuggestionsForSelectedLabel(query).sort();
  if (suggestionsForLabel.length >= suggestionLimit) {
    return suggestionsForLabel;
  } else {
    const suggestionsForAllLabelsValues = getSuggestionsForAllValues(query);
    return [...new Set(suggestionsForLabel.concat(suggestionsForAllLabelsValues))].slice(0, suggestionLimit);
  }
}

function searchKeySuggestions(query: string) {
  suggestions.value = getSuggestions(query);
}
</script>
