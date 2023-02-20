<template>
  <div v-if="boundedContexts.length" class="mb-4">
    <ContextureSwitch v-model="options.showNamespaces" :label="t('domains.details.filter.show_namespaces')" />
  </div>

  <div class="mt-6 grid gap-x-5 gap-y-6 sm:grid-cols-2">
    <div v-for="boundedContext of boundedContexts" :key="boundedContext.id">
      <ContextureBoundedContextCard
        :bounded-context="boundedContext"
        :show-namespaces="options.showNamespaces"
        :show-actions="true"
      />
    </div>
  </div>
</template>

<script lang="ts" setup>
import { RemovableRef, useLocalStorage } from "@vueuse/core";
import { useI18n } from "vue-i18n";
import ContextureBoundedContextCard from "~/components/bounded-context/ContextureBoundedContextCard.vue";
import ContextureSwitch from "~/components/primitives/switch/ContextureSwitch.vue";
import { BoundedContext } from "~/types/boundedContext";

interface Props {
  boundedContexts: BoundedContext[];
}

interface DomainCardGridOptions {
  showNamespaces: boolean;
}

defineProps<Props>();

const { t } = useI18n();

const options: RemovableRef<DomainCardGridOptions> = useLocalStorage<DomainCardGridOptions>(
  "settings.boundedContexts.grid",
  {
    showNamespaces: false,
  }
);
</script>
