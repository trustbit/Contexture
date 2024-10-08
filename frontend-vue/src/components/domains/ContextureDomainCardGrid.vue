<template>
  <div v-if="sortedDomains.length" class="sticky top-0 mb-4 border border-x-0 border-t-0 border-blue-100 bg-white py-2">
    <ContextureSwitch
      v-model="options.showBadges"
      :label="t('domains.details.filter.show_bounded_contexts_and_subdomains')"
    />
  </div>

  <div class="mt-6 grid gap-x-5 gap-y-6 sm:grid-cols-2">
    <div v-for="domain of sortedDomains" :key="domain.id">
      <ContextureDomainCard
        :domain="domain"
        :show-bounded-contexts="options.showBadges"
        :show-subdomains="options.showBadges"
      />
    </div>
  </div>
</template>

<script lang="ts" setup>
import { RemovableRef, useLocalStorage } from "@vueuse/core";
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import ContextureDomainCard from "~/components/domains/ContextureDomainCard.vue";
import ContextureSwitch from "~/components/primitives/switch/ContextureSwitch.vue";
import { Domain } from "~/types/domain";

interface Props {
  domains: Domain[];
}

interface DomainCardGridOptions {
  showBadges: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  domains: () => [],
});

const { t } = useI18n();
const options: RemovableRef<DomainCardGridOptions> = useLocalStorage<DomainCardGridOptions>("settings.domains.grid", {
  showBadges: true,
});

const sortedDomains = computed(() => props.domains.sortAlphabeticallyBy((d) => d.name));
</script>
