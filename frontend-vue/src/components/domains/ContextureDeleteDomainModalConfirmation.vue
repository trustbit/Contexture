<template>
  <div class="sm:w-[600px]">
    <div class="text-sm text-gray-800">
      {{ t("domains.card.delete_confirmation.sub_title") }}
    </div>

    <div class="mt-2 flex flex-col gap-y-4 p-4">
      <div v-if="subdomainNames.length > 0" class="max-h-48 overflow-auto">
        <span class="text-xs font-bold text-gray-800">{{
          t("domains.card.delete_confirmation.subdomains_to_delete", {
            count: subdomainNames.length,
          })
        }}</span>

        <div class="mt-2 flex flex-wrap gap-1">
          <ContextureBadge
            v-for="(subdomainName, index) of subdomainNames"
            :key="`${subdomainName}-${index}`"
            color="purple"
            mode="dark"
            size="sm"
            variant="outlined"
          >
            {{ subdomainName }}
          </ContextureBadge>
        </div>
      </div>
      <div v-else class="text-sm italic text-gray-700">
        {{ t("domains.card.delete_confirmation.subdomains_to_delete_empty") }}
      </div>

      <div v-if="boundedContextNames.length > 0" class="max-h-48 overflow-auto">
        <span class="text-xs font-bold text-gray-800">{{
          t("domains.card.delete_confirmation.bounded_contexts_to_delete", {
            count: boundedContextNames.length,
          })
        }}</span>

        <div class="mt-2 flex flex-wrap gap-1">
          <ContextureBadge
            v-for="(boundedContextName, index) of boundedContextNames"
            :key="`${boundedContextName}-${index}`"
            color="yellow"
            mode="dark"
            size="sm"
            variant="outlined"
          >
            {{ boundedContextName }}
          </ContextureBadge>
        </div>
      </div>
      <div v-else class="text-sm italic text-gray-700">
        {{ t("domains.card.delete_confirmation.bounded_contexts_to_delete_empty") }}
      </div>
    </div>

    <div class="mt-4 text-sm font-bold text-gray-800">
      {{ t("domains.card.delete_confirmation.confirmation_question") }}
    </div>
  </div>
</template>

<script lang="ts" setup>
import { useI18n } from "vue-i18n";
import ContextureBadge from "~/components/primitives/badge/ContextureBadge.vue";

interface Props {
  subdomainNames: string[];
  boundedContextNames: string[];
}

defineProps<Props>();

const { t } = useI18n();
</script>
