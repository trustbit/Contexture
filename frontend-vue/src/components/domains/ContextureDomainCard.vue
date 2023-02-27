<template>
  <div class="h-full rounded border border-blue-100">
    <RouterLink :to="`/domain/${domain.id}`">
      <div class="group-hover group flex h-full flex-col justify-between divide-y divide-blue-100">
        <div class="flex justify-between bg-blue-100 px-4 py-2 group-hover:rounded-t group-hover:bg-blue-500">
          <span class="font-bold text-blue-900 group-hover:text-white">{{ domain.name }}</span>
          <span class="text-gray-700 group-hover:text-white">{{ domain.shortName }}</span>
        </div>

        <div class="flex-grow divide-y divide-blue-100">
          <div class="h-32 overflow-auto p-4 text-gray-700">
            <div>
              <span v-if="domain.vision">{{ domain.vision }}</span>
              <span v-else class="italic"> {{ t("domains.card.no_vision") }}</span>
            </div>
          </div>

          <div v-if="showBadges">
            <div v-if="hasSubdomainsOrBoundedContexts" class="flex flex-col">
              <div v-if="showSubdomains && subdomainNames.length > 0" class="overflow-auto p-4">
                <p class="text-xs font-bold text-gray-900">
                  {{ t("domains.card.subdomain") }} ({{ subdomainNames.length }})
                </p>
                <div class="mt-3 flex flex-wrap gap-1">
                  <ContextureBadge
                    v-for="(subdomainName, index) in subdomainNames"
                    :key="`${subdomainName}-${index}`"
                    color="purple"
                    mode="light"
                    size="sm"
                    variant="filled"
                  >
                    {{ subdomainName }}
                  </ContextureBadge>
                </div>
              </div>
              <div
                v-if="showBoundedContexts && boundedContextNames.length > 0"
                class="overflow-auto border-t border-blue-100 p-4"
              >
                <p class="text-xs font-bold text-gray-900">
                  {{ t("domains.card.bounded_context") }} ({{ boundedContextNames.length }})
                </p>
                <div class="mt-3 flex flex-wrap gap-1">
                  <ContextureBadge
                    v-for="(boundedContextName, index) in boundedContextNames"
                    :key="`${boundedContextName}-${index}`"
                    color="yellow"
                    mode="light"
                    size="sm"
                    variant="filled"
                  >
                    {{ boundedContextName }}
                  </ContextureBadge>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="flex items-center justify-between px-4 py-2">
          <div class="inline-flex items-center gap-x-1 text-gray-600">
            <div class="p-2">
              <ContextureTooltip :content="t('common.move')" placement="top">
                <ContextureIconButton @click.prevent="onMoveClick" :data-testId="`move-${domain.name}`">
                  <span class="sr-only">{{ t("domains.card.move_domain") }}</span>
                  <icon:material-symbols:flip-to-back v-if="!domain.parentDomainId" class="h-6 w-6" />
                  <icon:material-symbols:backup-table v-if="domain.parentDomainId" class="h-6 w-6 -scale-x-100" />
                </ContextureIconButton>
              </ContextureTooltip>
            </div>
            <div class="p-2">
              <ContextureTooltip :content="t('common.delete')" placement="top">
                <ContextureIconButton @click.prevent="onDeleteClick" :data-testId="`delete-${domain.name}`">
                  <span class="sr-only">{{ t("domains.card.delete_domain") }}</span>
                  <icon:material-symbols:delete-outline-rounded class="h-6 w-6" />
                </ContextureIconButton>
              </ContextureTooltip>
            </div>
          </div>
          <div
            class="inline-flex items-center text-sm font-bold text-blue-500 hover:text-blue-600 hover:underline group-hover:visible sm:invisible"
          >
            {{ t("domains.card.view_domain") }}
            <Icon:material-symbols:arrow-forward class="ml-1.5 h-4 w-4" />
          </div>
        </div>
      </div>
    </RouterLink>
  </div>

  <teleport to="body" v-if="domainToMove">
    <ContextureMoveDomainModal :is-open="moveDomainDialogOpen" :domain="domainToMove" @close="closeMoveDialog" />
  </teleport>
</template>

<script lang="ts" setup>
import { storeToRefs } from "pinia";
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { RouterLink } from "vue-router";
import ContextureDeleteDomainModalConfirmation from "~/components/domains/ContextureDeleteDomainModalConfirmation.vue";
import ContextureMoveDomainModal from "~/components/domains/ContextureMoveDomainModal.vue";
import ContextureBadge from "~/components/primitives/badge/ContextureBadge.vue";
import ContextureIconButton from "~/components/primitives/button/ContextureIconButton.vue";
import ContextureTooltip from "~/components/primitives/tooltip/ContextureTooltip.vue";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import useConfirmationModalStore from "~/stores/confirmationModal";
import { useDomainsStore } from "~/stores/domains";
import { Domain } from "~/types/domain";

interface Props {
  domain: Domain;
  showSubdomains: boolean;
  showBoundedContexts: boolean;
}

const props = defineProps<Props>();
const { t } = useI18n();
const confirmModal = useConfirmationModalStore();
const domainsStore = useDomainsStore();
const { boundedContextsByDomainId } = storeToRefs(useBoundedContextsStore());

const subdomainNames = computed(() => props.domain.subdomains.map((subdomain) => subdomain.name));
const boundedContextNames = computed(() =>
  (boundedContextsByDomainId.value[props.domain.id] || []).map((boundedContext) => boundedContext.name)
);
const showBadges = computed(() => props.showSubdomains || props.showBoundedContexts);
const hasSubdomainsOrBoundedContexts = computed(
  () => props.domain.subdomains.length > 0 || props.domain.boundedContexts.length > 0
);
const domainToMove = ref();
const moveDomainDialogOpen = ref(false);

function onDeleteClick(): void {
  confirmModal.openWithComponent(
    t("domains.card.delete_confirmation.title", {
      domainName: props.domain.name,
    }),
    ContextureDeleteDomainModalConfirmation,
    {
      subdomainNames,
      boundedContextNames,
    },
    t("domains.card.delete_confirmation.confirm_button"),
    () => domainsStore.deleteDomain(props.domain.id)
  );
}

function onMoveClick(): void {
  domainToMove.value = props.domain;
  moveDomainDialogOpen.value = true;
}

function closeMoveDialog(): void {
  moveDomainDialogOpen.value = false;
  setTimeout(() => {
    // wait for animation
    domainToMove.value = null;
  }, 500);
}
</script>
