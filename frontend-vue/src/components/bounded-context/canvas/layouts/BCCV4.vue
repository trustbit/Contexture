<template>
  <div class="flex flex-col gap-y-4">
    <div class="flex min-w-full gap-x-4">
      <div class="flex w-1/3 flex-col gap-y-4">
        <BCCDescription class="bg-gray-100" />
        <BCCKey class="bg-gray-100" />
      </div>
      <BCCStrategicClassification class="w-1/3 bg-gray-100" />

      <BCCDomainRoles class="w-1/3 bg-gray-100" />
    </div>
    <div class="flex gap-x-4 rounded bg-gray-100 p-4">
      <div class="flex w-1/3 flex-col gap-y-4 rounded bg-white p-4">
        <h3 class="text-lg font-bold">{{ t("bounded_context_canvas.collaborators.inbound_communication") }}</h3>
        <div class="flex flex-col gap-y-2 rounded bg-gray-100 p-4">
          <div class="flex items-center gap-x-1.5">
            <Icon:materialSymbols:extension-outline aria-hidden="true" class="h-5 w-5" />
            <h4 class="text-base font-bold">
              {{ t("bounded_context_canvas.collaborators.title") }}
            </h4>
            <ContextureTooltip
              :content="t('bounded_context_canvas.collaborators.tooltip')"
              placement="top"
              class="flex items-center"
            >
              <Icon:materialSymbols:info-outline class="h-5 w-5 text-gray-500"></Icon:materialSymbols:info-outline>
            </ContextureTooltip>
          </div>
          <h5 class="text-sm font-bold">{{ t("bounded_context_canvas.collaborators.inbound_connection.title") }}</h5>
          <BCCInboundConnections />
        </div>
        <div class="flex flex-col gap-y-2 rounded bg-gray-100 p-2">
          <div>
            <div class="flex items-center">
              <Icon:materialSymbols:mail-outline aria-hidden="true" class="mr-1.5 h-5 w-5" />
              <h4 class="text-base font-bold">
                {{ t("bounded_context_canvas.messages.consumed.title") }}
              </h4>
            </div>
          </div>
          <BCCMessagesConsumed />
        </div>
      </div>
      <div class="flex w-1/3 flex-col gap-y-2">
        <BCCUbiquitousLanguage class="bg-white" />
        <BCCBusinessDecisions class="bg-white" />
      </div>
      <div class="flex w-1/3 flex-col gap-y-4 rounded bg-white p-4">
        <h3 class="text-lg font-bold">{{ t("bounded_context_canvas.collaborators.outbound_communication") }}</h3>
        <div class="flex flex-col gap-y-2 rounded bg-gray-100 p-4">
          <div class="flex items-center gap-x-1.5">
            <Icon:materialSymbols:extension-outline aria-hidden="true" class="h-5 w-5" />
            <h4 class="text-base font-bold">
              {{ t("bounded_context_canvas.collaborators.title") }}
            </h4>
            <ContextureTooltip
              :content="t('bounded_context_canvas.collaborators.tooltip')"
              placement="top"
              class="flex items-center"
            >
              <Icon:materialSymbols:info-outline class="h-5 w-5 text-gray-500"></Icon:materialSymbols:info-outline>
            </ContextureTooltip>
          </div>
          <h5 class="text-sm font-bold">{{ t("bounded_context_canvas.collaborators.outbound_connection.title") }}</h5>
          <BCCOutboundConnections />
        </div>
        <div class="flex flex-col gap-y-2 rounded bg-gray-100 p-2">
          <div class="flex items-center">
            <Icon:materialSymbols:forward-to-inbox-outline aria-hidden="true" class="mr-1.5 h-5 w-5" />
            <h4 class="text-base font-bold">
              {{ t("bounded_context_canvas.messages.produced.title") }}
            </h4>
          </div>
          <BCCMessagesProduced />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";
import BCCBusinessDecisions from "~/components/bounded-context/canvas/BCCBusinessDecisions.vue";
import BCCDescription from "~/components/bounded-context/canvas/BCCDescription.vue";
import BCCDomainRoles from "~/components/bounded-context/canvas/BCCDomainRoles.vue";
import BCCInboundConnections from "~/components/bounded-context/canvas/BCCInboundConnections.vue";
import BCCKey from "~/components/bounded-context/canvas/BCCKey.vue";
import BCCMessagesConsumed from "~/components/bounded-context/canvas/BCCMessagesConsumed.vue";
import BCCMessagesProduced from "~/components/bounded-context/canvas/BCCMessagesProduced.vue";
import BCCOutboundConnections from "~/components/bounded-context/canvas/BCCOutboundConnections.vue";
import BCCStrategicClassification from "~/components/bounded-context/canvas/BCCStrategicClassification.vue";
import BCCUbiquitousLanguage from "~/components/bounded-context/canvas/BCCUbiquitousLanguage.vue";
import ContextureTooltip from "~/components/primitives/tooltip/ContextureTooltip.vue";

const { t } = useI18n();
</script>
