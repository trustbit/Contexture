<template>
  <ContextureAutocomplete
    v-model="model"
    class="ml-2 grow"
    :placeholder="t('common.name')"
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
import { storeToRefs } from "pinia";

const fuseOptions: IFuseOptions<string> = {
  includeScore: true,
  includeMatches: true,
  threshold: 0.5,
  location: 0,
  distance: 50,
  minMatchCharLength: 1,
  keys: ["name"],
};

const { labelNames } = storeToRefs(useNamespaces());
const { t } = useI18n();
const suggestions = ref(labelNames.value);
const fuse = new Fuse(labelNames.value, fuseOptions);
const model = defineModel<string>();
const inputText = ref("");

const searchKeySuggestions = (query: string) => {
  if (!query) {
    suggestions.value = labelNames.value;
    model.value = undefined;
    return;
  }

  inputText.value = query;
  fuse.setCollection(labelNames.value);
  const results = fuse.search(query);

  suggestions.value = results.map((result: { item: string }) => {
    return result.item;
  });
};
</script>
