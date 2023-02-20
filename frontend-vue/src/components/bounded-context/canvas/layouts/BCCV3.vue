<template>
  <div class="flex w-fit flex-row gap-y-4 gap-x-4 overflow-x-auto sm:w-full">
    <div class="flex w-1/2 flex-col gap-y-4">
      <BCCDescription class="bg-gray-100" />

      <BCCKey class="bg-gray-100" />

      <BCCStrategicClassification class="bg-gray-100" />

      <BCCBusinessDecisions class="bg-gray-100" />

      <BCCUbiquitousLanguage class="bg-gray-100" />
    </div>

    <div class="flex w-1/2 flex-col gap-y-4">
      <BCCDomainRoles class="bg-gray-100" />

      <ContextureBoundedContextCanvasElement :title="t('bounded_context_canvas.messages.title')" class="bg-gray-100">
        <div class="flex gap-x-4 pt-4">
          <div class="flex w-1/2 flex-col gap-y-4 rounded p-4">
            <h3 class="text-base font-bold">
              {{ t("bounded_context_canvas.messages.consumed.title") }}
            </h3>
            <BCCMessagesConsumed version="V3" />
          </div>
          <div class="flex w-1/2 flex-col gap-y-4 rounded p-4">
            <h3 class="text-base font-bold">
              {{ t("bounded_context_canvas.messages.produced.title") }}
            </h3>
            <BCCMessagesProduced version="V3" />
          </div>
        </div>
      </ContextureBoundedContextCanvasElement>

      <ContextureBoundedContextCanvasElement
        :title="t('bounded_context_canvas.collaborators.title')"
        class="bg-gray-100"
      >
        <div class="flex gap-x-4 pt-4">
          <div class="flex w-1/2 flex-col gap-y-4 rounded bg-white p-4">
            <h3 class="text-base font-bold">
              {{ t("bounded_context_canvas.collaborators.inbound_connection.title") }}
            </h3>
            <div class="flex flex-col gap-y-2 bg-gray-100 p-2">
              <BCCInboundConnections class="rounded bg-gray-100" />
            </div>
          </div>

          <div class="flex w-1/2 flex-col gap-y-4 rounded bg-white p-4">
            <h3 class="text-base font-bold">
              {{ t("bounded_context_canvas.collaborators.outbound_connection.title") }}
            </h3>
            <div class="flex flex-col gap-y-2 bg-gray-100 p-2">
              <BCCOutboundConnections class="rounded bg-gray-100" />
            </div>
          </div>
        </div>
      </ContextureBoundedContextCanvasElement>
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
import ContextureBoundedContextCanvasElement from "~/components/bounded-context/canvas/ContextureBoundedContextCanvasElement.vue";

const { t } = useI18n();
</script>
