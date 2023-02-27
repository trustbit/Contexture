<template>
  <BCCConnection :collaborations="recipientCollaborations" type="initiator" />
</template>

<script setup lang="ts">
import { storeToRefs } from "pinia";
import { computed } from "vue";
import BCCConnection from "~/components/bounded-context/canvas/BCCConnection.vue";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import { useCollaborationsStore } from "~/stores/collaborations";
import { Collaboration } from "~/types/collaboration";

const { activeBoundedContext } = storeToRefs(useBoundedContextsStore());
const { collaborations } = storeToRefs(useCollaborationsStore());

const recipientCollaborations = computed<Collaboration[]>(() => {
  return collaborations.value.filter((c) => c.recipient?.boundedContext === activeBoundedContext.value?.id);
});
</script>
