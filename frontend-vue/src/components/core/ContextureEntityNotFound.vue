<template>
  <div class="mt-6 flex flex-col items-center gap-y-1.5 text-gray-500">
    <div>
      <Icon:materialSymbols:warning-rounded class="h-10 w-10" />
    </div>
    <div>
      <span class="text-sm">{{ text }}</span>
    </div>
    <div>
      <ContextureWhiteButton
        :label="t('common.view_all_domains')"
        @click="navigateToAllDomains"
      ></ContextureWhiteButton>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";
import { useRouter } from "vue-router";
import ContextureWhiteButton from "~/components/primitives/button/ContextureWhiteButton.vue";

interface Props {
  text: string;
}

defineProps<Props>();
const router = useRouter();

const { t } = useI18n();

function navigateToAllDomains() {
  router.push("/");
}
</script>
