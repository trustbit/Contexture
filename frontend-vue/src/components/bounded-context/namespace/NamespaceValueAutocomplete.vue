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
import { ref } from "vue";
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
const suggestions = ref<string[]>(findNamespaceLabelValuesByLabelName(props.namespaceLabelName));
const fuse = new Fuse(findNamespaceLabelValuesByLabelName(props.namespaceLabelName), fuseOptions);
const model = defineModel<string>();
const inputText = ref("");

const suggestionLimit = 10;

const searchKeySuggestions = (query: string) => {
  if (query == "") {
    suggestions.value = findNamespaceLabelValuesByLabelName(props.namespaceLabelName);
    model.value = undefined;
    return;
  }
  inputText.value = query;
  const namespaceLabelValuesByLabelName = findNamespaceLabelValuesByLabelName(props.namespaceLabelName);
  fuse.setCollection(namespaceLabelValuesByLabelName);
  const suggestionsForLabel = fuse.search(query);

  if (suggestionsForLabel.length >= suggestionLimit) {
    suggestions.value = suggestionsForLabel.map((result: { item: string }) => result.item).sort();
  } else {
    fuse.setCollection(namespaceLabelValues);
    const suggestionsFromAllLabels = fuse.search(query);
    const suggestionsForLabelValues = suggestionsForLabel.map((result: { item: string }) => result.item).sort();
    const suggestionsFromAllLabelsValues = suggestionsFromAllLabels.map((result: { item: string }) => result.item);

    suggestions.value = [...new Set(suggestionsForLabelValues.concat(suggestionsFromAllLabelsValues))].slice(
      0,
      suggestionLimit
    );
  }
};
</script>
