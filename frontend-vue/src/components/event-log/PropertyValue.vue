<template>
  <div class="flex flex-row gap-1">
    <span class="font-medium">{{ name }}</span>
    <span>{{ value }}</span>
  </div>
</template>

<script setup lang="ts">
const { name, value } = defineProps<{ name: string; value: string }>();
</script>
