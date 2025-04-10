<template>
  <div class="flex flex-col items-end">
    <span v-if="isRecentDate" class="font-medium text-blue-500">{{ toRelativeTime(date) }}</span>
    <span class="font-medium text-blue-500">{{ toFormattedDate(date) }}</span>
  </div>
</template>

<script setup lang="ts">
import { useDateUtils } from "~/composables/date-utls";
import { differenceInDays } from "date-fns";
import { computed } from "vue";
const { date } = defineProps<{ date: string }>();
const { toFormattedDate, toRelativeTime } = useDateUtils();
const isRecentDate = computed(() => differenceInDays(new Date(), date) < 7);
</script>
