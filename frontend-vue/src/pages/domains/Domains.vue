<template>
  <div class="mx-auto mt-5 px-3 pt-5 pb-8 lg:container lg:px-0">
    <div class="sm:flex sm:justify-between">
      <ContextureBlankHeader :title="t('domains.grid.title')" />

      <ContexturePrimaryButton
        :label="t('domains.grid.button.create_domain')"
        class="mt-4 sm:mt-0"
        @click="onCreateDomain"
        v-if="canModify"
      >
        <template #left>
          <Icon:material-symbols:flip-to-back aria-hidden="true" class="mr-2" />
        </template>
      </ContexturePrimaryButton>
    </div>

    <div class="mt-4 sm:mt-10">
      <div v-if="loading" class="text-sm">
        {{ t("domains.grid.loading") }}
      </div>
      <ContextureHelpfulErrorAlert
        v-else-if="loadingError"
        v-bind="loadingError"
        :friendly-message="t('domains.grid.error.loading')"
      />

      <div v-else>
        <div class="text-sm text-gray-700" v-if="parentDomains.length === 0">{{ $t("domains.empty") }}</div>
        <ContextureDomainCardGrid v-else :domains="parentDomains" />
      </div>
    </div>
  </div>

  <CreateDomainModal :is-open="createDomainModalOpen" @cancel="onCancelCreateDomain" />
</template>

<script lang="ts" setup>
import { storeToRefs } from "pinia";
import { ref } from "vue";
import { useI18n } from "vue-i18n";
import ContextureDomainCardGrid from "~/components/domains/ContextureDomainCardGrid.vue";
import CreateDomainModal from "~/components/domains/ContextureCreateDomainModal.vue";
import ContexturePrimaryButton from "~/components/primitives/button/ContexturePrimaryButton.vue";
import ContextureBlankHeader from "~/components/core/header/ContextureBlankHeader.vue";
import ContextureHelpfulErrorAlert from "~/components/primitives/alert/ContextureHelpfulErrorAlert.vue";
import { useDomainsStore } from "~/stores/domains";
import { useAuthStore } from "~/stores/auth";

const { t } = useI18n();
const { parentDomains, loading, loadingError } = storeToRefs(useDomainsStore());
const createDomainModalOpen = ref(false);
const { canModify } = useAuthStore()

function onCreateDomain() {
  createDomainModalOpen.value = true;
}

function onCancelCreateDomain() {
  createDomainModalOpen.value = false;
}
</script>
