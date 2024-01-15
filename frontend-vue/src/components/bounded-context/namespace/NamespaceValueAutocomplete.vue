<template>
  <ContextureAutocomplete
    v-model="model"
    class="ml-2 grow"
    :placeholder="t('common.value')"
    :suggestions="suggestions"
    :display-value="(l: any) => l"
    :allow-custom-values="true"
    @complete="searchKeySuggestions($event)"
  >
    <template #customValue>
     <div class="flex justify-items-center items-center align-middle">
       <Icon:material-symbols:add aria-hidden="true" class="mr-2" />
       <span>{{ t("common.create-new", {entityName: inputText}) }}</span>
     </div>
    </template>
  </ContextureAutocomplete>
</template>

<script setup lang="ts">
import ContextureAutocomplete from "~/components/primitives/autocomplete/ContextureAutocomplete.vue";
import { useNamespaces } from "~/stores/namespaces";
import { FuseOptions } from "@vueuse/integrations/useFuse";
import { ref } from "vue";
import { useI18n } from "vue-i18n";
import Fuse from "fuse.js";

interface Props {
  namespaceLabelName?: string;
}

const fuseOptions: FuseOptions<{ name: string }> = {
  includeScore: true,
  includeMatches: true,
  threshold: 0.6,
  location: 0,
  distance: 100,
  maxPatternLength: 32,
  minMatchCharLength: 1,
  keys: ["name"]
};

const { findNamespaceLabelValuesByLabelName } = useNamespaces();
const { t } = useI18n();
const props = defineProps<Props>();
const suggestions = ref<string[]>(findNamespaceLabelValuesByLabelName(props.namespaceLabelName));
const fuse = new Fuse(findNamespaceLabelValuesByLabelName(props.namespaceLabelName), fuseOptions);
const model = defineModel<string>();
const inputText = ref("");

const searchKeySuggestions = (query: string) => {
  if (query == "") {
    suggestions.value = findNamespaceLabelValuesByLabelName(props.namespaceLabelName);
    model.value = undefined;
    return;
  }
  inputText.value = query
  const namespaceLabelValuesByLabelName = findNamespaceLabelValuesByLabelName(props.namespaceLabelName);
  fuse.setCollection(namespaceLabelValuesByLabelName);
  const results = fuse.search(query);

  suggestions.value = results.map((result: { item: string }) => {
    return result.item;
  });
};
</script>