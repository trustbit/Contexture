<template>
  <div class="flex flex-row flex-wrap gap-1">
    <span class="text-base font-normal text-red-500 line-through">{{ previousValue }}</span>
    <span class="text-base font-normal">{{ newValue }}</span>
  </div>
</template>

<script setup lang="ts">
const { previousValue, newValue } = defineProps<{ previousValue: string; newValue: string }>();
</script>
