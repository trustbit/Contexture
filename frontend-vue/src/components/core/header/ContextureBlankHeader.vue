<template>
  <div>
    <h1 class="text-2xl font-bold">
      {{ title }}
    </h1>
  </div>
</template>

<script lang="ts" setup>
interface Props {
  title: string;
}

defineProps<Props>();
</script>
