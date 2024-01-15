<template>
  <div class="bg-white p-1 sm:p-4" v-if="activeFilters.length > 0">
    <div class="flex justify-between">
      <span class="text-sm font-bold">{{ t("search.active_filters.title") }}</span>
      <ContextureWhiteButton :label="t('search.remove_all_filters')" size="sm" @click="onClearFilters">
        <template #left>
          <Icon:materialSymbols:close class="mr-2" />
        </template>
      </ContextureWhiteButton>
    </div>
    <div class="mt-2 flex flex-wrap gap-2">
      <template v-for="(activeFilter, index) of activeFilters" :key="index">
        <ContextureBadge class="inline-flex items-center" mode="light" size="sm" color="teal" variant="filled">
          <span>
            {{ activeFilter.key }}: <span class="font-bold">{{ activeFilter.value }}</span></span
          >
          <button @click="() => onDeleteFilter(index)">
            <Icon:materialSymbols:close class="ml-1.5 text-gray-600"></Icon:materialSymbols:close>
          </button>
        </ContextureBadge>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";
import ContextureBadge from "~/components/primitives/badge/ContextureBadge.vue";
import ContextureWhiteButton from "~/components/primitives/button/ContextureWhiteButton.vue";
import { ActiveFilter } from "~/types/activeFilter";

interface Props {
  activeFilters: ActiveFilter[];
}

interface Emits {
  (e: "deleteFilter", index: number): void;

  (e: "clearFilters"): void;
}

defineProps<Props>();
const emit = defineEmits<Emits>();

const { t } = useI18n();

function onDeleteFilter(index: number) {
  emit("deleteFilter", index);
}

function onClearFilters() {
  emit("clearFilters");
}
</script>
