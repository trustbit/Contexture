<template>
  <ContextureAlert class="mt-4 overflow-x-auto break-all" v-show="friendlyMessage || error || response">
    <p class="text-sm font-bold">
      {{ friendlyMessage }}
    </p>
    <p class="text-xs">
      {{ error }}
    </p>
    <p class="text-xs">
      {{ response }}
    </p>
  </ContextureAlert>
</template>

<script setup lang="ts">
import ContextureAlert from "~/components/primitives/alert/ContextureAlert.vue";

export interface HelpfulErrorProps {
  friendlyMessage?: string;
  error?: Error;
  response?: any;
}

defineProps<HelpfulErrorProps>();
</script>
