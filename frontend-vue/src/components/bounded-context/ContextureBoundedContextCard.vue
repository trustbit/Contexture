<template>
  <div class="h-full rounded border border-blue-100">
    <div v-if="condensed" class="p-2 sm:flex">
      <div class="flex flex-col sm:w-2/12">
        <span class="text-sm font-bold">{{ boundedContext.name }}</span>
        <span class="text-xs text-gray-600">{{ boundedContext.shortName }}</span>
      </div>
      <div class="flex flex-col gap-y-4 sm:w-9/12 sm:flex-col">
        <div v-for="namespace of boundedContext.namespaces" :key="namespace.id">
          <span class="text-xs font-bold text-gray-900">{{ namespace.name }}</span>
          <div class="mt-2 flex flex-wrap gap-2">
            <template v-for="label of namespace.labels" :key="label.id">
              <div v-if="isLink(label.value)">
                <a :href="label.value" target="_blank">
                  <ContextureBadge color="blue" mode="light" size="sm" variant="filled" class="hover:bg-blue-200">
                    <div class="inline-flex items-center">
                      <b>{{ label.name }}</b>
                      <Icon:materialSymbols:open-in-new
                        class="ml-2"
                        aria-hidden="true"
                      ></Icon:materialSymbols:open-in-new>
                    </div>
                  </ContextureBadge>
                </a>
              </div>
              <div v-else>
                <ContextureBadge color="blue" mode="light" size="sm" variant="filled">
                  <b>{{ label.name }}&nbsp;</b>
                  <RouterLink
                    :to="`/search?Label.Name=${label.name}&Label.Value=${label.value}`"
                    class="hover:underline"
                  >
                    {{ label.value }}
                  </RouterLink>
                </ContextureBadge>
              </div>
            </template>
          </div>
        </div>
      </div>
      <div class="mt-4 flex items-end sm:mt-0 sm:w-1/12 sm:flex-col">
        <div class="p-2" v-if="showActions">
          <ContextureTooltip content="Move" placement="left">
            <button class="text-gray-400 hover:text-blue-600" @click="onMoveClick">
              <icon:material-symbols:backup-table
                :aria-label="t('bounded_context.card.move')"
                class="h-6 w-6 -scale-x-100"
              />
            </button>
          </ContextureTooltip>
        </div>

        <div class="p-2">
          <ContextureTooltip content="View namespaces" placement="left">
            <RouterLink
              :to="`/boundedContext/${boundedContext.id}/namespaces`"
              class="text-gray-400 hover:text-blue-600"
            >
              <icon:material-symbols:add-notes-outline-sharp
                :aria-label="t('bounded_context.card.view_namespaces')"
                class="h-6 w-6"
              />
            </RouterLink>
          </ContextureTooltip>
        </div>

        <div class="p-2" v-if="showActions">
          <ContextureTooltip content="Delete" placement="left">
            <button class="text-gray-400 hover:text-blue-600" @click="onDeleteClick">
              <icon:material-symbols:delete-outline-rounded
                :aria-label="t('bounded_context.card.delete_bounded_context')"
                class="h-6 w-6"
              />
            </button>
          </ContextureTooltip>
        </div>

        <div class="p-2">
          <ContextureTooltip content="View canvas" placement="left">
            <RouterLink
              :to="`/boundedContext/${boundedContext.id}/canvas`"
              class="inline-flex items-center text-sm font-bold text-gray-400 hover:text-blue-600 hover:underline"
            >
              <Icon:material-symbols:open-in-new
                class="h-6 w-6 -scale-x-100"
                :aria-label="t('bounded_context.card.view_bounded_context_canvas')"
              />
            </RouterLink>
          </ContextureTooltip>
        </div>
      </div>
    </div>
    <div v-else class="group-hover group flex h-full flex-col justify-between divide-y divide-blue-100">
      <div class="flex justify-between bg-blue-100 px-4 py-2 group-hover:rounded-t group-hover:bg-blue-500">
        <span class="font-bold text-blue-900 group-hover:text-white">{{ boundedContext.name }}</span>
        <span class="text-gray-700 group-hover:text-white">{{ boundedContext.shortName }}</span>
      </div>

      <div class="flex-grow divide-y divide-blue-100">
        <div class="h-32 overflow-auto p-4 text-gray-700">
          <div>
            <span v-if="boundedContext.description">{{ boundedContext.description }}</span>
            <span v-else class="italic"> {{ t("bounded_context.card.no_description") }}</span>
          </div>
        </div>

        <div v-if="showNamespaces" class="divide-y divide-blue-100">
          <div v-for="namespace of boundedContext.namespaces" :key="namespace.id" class="p-4">
            <span class="text-xs font-bold text-gray-900">{{ namespace.name }}</span>
            <div class="mt-2 flex flex-wrap gap-2">
              <template v-for="label of namespace.labels" :key="label.id">
                <div v-if="isLink(label.value)">
                  <a :href="label.value" target="_blank">
                    <ContextureBadge color="blue" mode="light" size="sm" variant="filled" class="hover:bg-blue-200">
                      <div class="inline-flex items-center">
                        <b>{{ label.name }}</b>
                        <Icon:materialSymbols:open-in-new
                          class="ml-2"
                          aria-hidden="true"
                        ></Icon:materialSymbols:open-in-new>
                      </div>
                    </ContextureBadge>
                  </a>
                </div>
                <div v-else>
                  <ContextureBadge color="blue" mode="light" size="sm" variant="filled">
                    <b>{{ label.name }}&nbsp;</b>
                    <RouterLink
                      :to="`/search?Label.Name=${label.name}&Label.Value=${label.value}`"
                      class="hover:underline"
                    >
                      {{ label.value }}
                    </RouterLink>
                  </ContextureBadge>
                </div>
              </template>
            </div>
          </div>
        </div>
      </div>

      <div class="flex items-center justify-between px-4 py-2">
        <div class="inline-flex items-center gap-x-1 text-gray-400">
          <div class="p-2" v-if="showActions">
            <ContextureTooltip content="Move" placement="top">
              <ContextureIconButton @click.prevent="onMoveClick">
                <span class="sr-only">{{ t("bounded_context.card.move") }}</span>
                <icon:material-symbols:backup-table class="h-6 w-6 -scale-x-100" />
              </ContextureIconButton>
            </ContextureTooltip>
          </div>

          <div class="p-2">
            <ContextureTooltip content="View namespaces" placement="top">
              <RouterLink :to="`/boundedContext/${boundedContext.id}/namespaces`" class="hover:text-blue-600">
                <icon:material-symbols:add-notes-outline-sharp
                  :aria-label="t('bounded_context.card.view_namespaces')"
                  class="h-6 w-6"
                />
              </RouterLink>
            </ContextureTooltip>
          </div>

          <div class="p-2" v-if="showActions">
            <ContextureTooltip content="Delete" placement="top">
              <ContextureIconButton @click.prevent="onDeleteClick">
                <span class="sr-only">{{ t("bounded_context.card.delete_bounded_context") }}</span>
                <icon:material-symbols:delete-outline-rounded
                  :aria-label="t('bounded_context.card.delete_bounded_context')"
                  class="h-6 w-6"
                />
              </ContextureIconButton>
            </ContextureTooltip>
          </div>
        </div>
        <RouterLink
          :to="`/boundedContext/${boundedContext.id}/canvas`"
          class="flex items-center text-sm font-bold text-blue-500 hover:text-blue-600 hover:underline group-hover:visible sm:invisible"
        >
          {{ t("bounded_context.card.view_bounded_context") }}
          <Icon:material-symbols:arrow-forward class="ml-1.5 h-4 w-4" />
        </RouterLink>
      </div>
    </div>
  </div>

  <teleport to="body" v-if="boundedContextToMove">
    <ContextureMoveBoundedContextModal
      :is-open="moveBoundedContextDialogOpen"
      :bounded-context="boundedContextToMove"
      @close="closeMoveDialog"
    />
  </teleport>
</template>

<script lang="ts" setup>
import { ref } from "vue";
import { useI18n } from "vue-i18n";
import { RouterLink } from "vue-router";
import ContextureMoveBoundedContextModal from "~/components/bounded-context/ContextureMoveBoundedContextModal.vue";
import ContextureBadge from "~/components/primitives/badge/ContextureBadge.vue";
import ContextureIconButton from "~/components/primitives/button/ContextureIconButton.vue";
import ContextureTooltip from "~/components/primitives/tooltip/ContextureTooltip.vue";
import { isLink } from "~/core";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import useConfirmationModalStore from "~/stores/confirmationModal";
import { BoundedContext } from "~/types/boundedContext";

interface Props {
  boundedContext: BoundedContext;
  showNamespaces: boolean;
  condensed?: boolean;
  showActions: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  showNamespaces: true,
});
const { t } = useI18n();
const confirmationModal = useConfirmationModalStore();
const boundedContextStore = useBoundedContextsStore();
const boundedContextToMove = ref<BoundedContext>();
const moveBoundedContextDialogOpen = ref(false);

function onDeleteClick(): void {
  confirmationModal.open(
    t("bounded_context.card.delete.confirm.title", {
      boundedContextName: props.boundedContext.name,
    }),
    t("bounded_context.card.delete.confirm.body"),
    t("bounded_context.card.delete.confirm.action"),
    () => boundedContextStore.deleteBoundedContext(props.boundedContext.id)
  );
}

function onMoveClick(): void {
  boundedContextToMove.value = props.boundedContext;
  moveBoundedContextDialogOpen.value = true;
}

function closeMoveDialog(): void {
  moveBoundedContextDialogOpen.value = false;
  setTimeout(() => {
    // wait for animation
    boundedContextToMove.value = undefined;
  }, 500);
}
</script>
