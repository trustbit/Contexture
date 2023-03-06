<template>
  <ContextureBlankHeader :title="t('domains.bubble.title')" />

  <div class="mt-4 flex overflow-x-auto">
    <div class="flex flex-col">
      <ContexturePrimaryButton
        v-if="activeDomainBubble"
        :label="t('domains.bubble.home')"
        class="mb-2 w-fit"
        @click="onHomeClick"
      >
        <template #right>
          <Icon:material-symbols:home-outline class="ml-2" aria-hidden="true" />
        </template>
      </ContexturePrimaryButton>
      <ContextureSecondaryButton
        v-if="!activeDomainBubble"
        :label="t('domains.bubble.connections')"
        class="w-fit"
        @click="onShowAllConnectionsClick"
      >
        <template #right>
          <Icon:material-symbols:conversion-path class="ml-2" aria-hidden="true" />
        </template>
      </ContextureSecondaryButton>
      <RouterLink v-if="activeDomainBubble" :to="`domain/${activeDomainBubble.id}`">
        <ContextureSecondaryButton
          :label="t('domains.bubble.details')"
          class="w-fit"
          @click="onShowAllConnectionsClick"
        >
          <template #right>
            <Icon:material-symbols:open-in-new class="ml-2" aria-hidden="true" />
          </template>
        </ContextureSecondaryButton>
      </RouterLink>
    </div>

    <bubble-visualization />
  </div>
</template>

<script setup lang="ts">
import { storeToRefs } from "pinia";
import { ref } from "vue";
import { useI18n } from "vue-i18n";
import ContexturePrimaryButton from "~/components/primitives/button/ContexturePrimaryButton.vue";
import ContextureSecondaryButton from "~/components/primitives/button/ContextureSecondaryButton.vue";
import ContextureBlankHeader from "~/components/core/header/ContextureBlankHeader.vue";
import { useDomainsStore } from "~/stores/domains";
import { Domain } from "~/types/domain";

const { t } = useI18n();
const { allDomains } = storeToRefs(useDomainsStore());
const activeDomainBubble = ref<Domain>();

document.addEventListener("bubbleViewOnMoreInfoChanged", (e: any) => {
  const { Domain: parentDomainId, SubDomain: subdomainId } = JSON.parse(e.detail);

  if (subdomainId) {
    activeDomainBubble.value = allDomains.value.find((a) => a.id === subdomainId);
  } else if (parentDomainId) {
    activeDomainBubble.value = allDomains.value.find((a) => a.id === parentDomainId);
  }
});

function onShowAllConnectionsClick() {
  const bubble: any = document.querySelector("bubble-visualization");
  bubble.showAllConnections("a");
}

function onHomeClick() {
  const bubble: any = document.querySelector("bubble-visualization");
  bubble.showMain();
  activeDomainBubble.value = undefined;
}
</script>
