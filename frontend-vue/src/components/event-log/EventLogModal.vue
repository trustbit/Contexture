<template>
  <ContextureModal :title="t('event_log.title')" :isOpen="open" @cancel="emitCancel">
    <EventLog :events="events" />
  </ContextureModal>
</template>

<script setup lang="ts">
import { defineProps, defineEmits, watch, ref } from "vue";
import ContextureModal from "~/components/primitives/modal/ContextureModal.vue";
import EventLog from "~/components/event-log/EventLog.vue";
import { EventLogEntry } from "~/types/event-log";
import { useEventLogsStore } from "~/stores/eventLogs";
import { useI18n } from "vue-i18n";

const props = defineProps<{ isOpen: boolean; entityId: string }>();
const emit = defineEmits<{
  (e: "cancel"): void;
}>();
const { t } = useI18n();
const eventLogStore = useEventLogsStore();
const events = ref<EventLogEntry[]>([]);
const open = ref(props.isOpen);
const emitCancel = () => {
  emit("cancel");
  open.value = false;
};

watch(
  () => props.isOpen,
  async (newVal) => {
    if (newVal) {
      events.value = await eventLogStore.fetchEventLogs(props.entityId);
      open.value = true;
    }
  }
);
</script>
