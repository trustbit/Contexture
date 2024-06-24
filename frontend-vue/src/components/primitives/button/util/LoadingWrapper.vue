<template>
  <div v-if="isLoading">
    <div class="loader">
      <div v-for="i in 5" :key="i" class="dot" :style="`--delay: ${i}`" />
    </div>
  </div>
  <div v-else>
    <slot />
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";

interface Props {
  isLoading: boolean;
}

const props = defineProps<Props>();
const isLoading = computed(() => props.isLoading);
</script>

<style scoped>
.loader {
  display: inline-grid;
  grid-auto-flow: column;
  width: 100%;
  place-content: center;
  justify-self: center;
}

.dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  margin-left: 6px;
  display: block;
  --delay: 1;
  --duration: 0.7s;
  animation: dotSpin var(--duration) linear calc(var(--delay) * 0.155s) infinite alternate;
}

@keyframes dotSpin {
  0%,
  100% {
    transform: scale(1.4);
    opacity: 1;
    background-color: darkblue;
  }

  50% {
    transform: scale(1);
    opacity: 0;
    background-color: transparent;
  }
}
</style>
