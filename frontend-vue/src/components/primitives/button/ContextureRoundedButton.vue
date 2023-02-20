<template>
  <button
    :id="id"
    :class="[classes.size[size], classes.color[color]]"
    class="flex items-center justify-center rounded-full"
    @click="onClick"
  >
    <slot />
  </button>
</template>

<script lang="ts" setup>
import { uniqueId } from "~/core";

interface ContextureRoundedButtonProps {
  id?: string;
  size?: "fit" | "sm" | "md" | "lg";
  color?: "blue";
}

interface ContextureIconButtonEmits {
  (e: "click", value: MouseEvent): void;
}

withDefaults(defineProps<ContextureRoundedButtonProps>(), {
  id: uniqueId(),
  size: "md",
  color: "blue",
});

const emit = defineEmits<ContextureIconButtonEmits>();

function onClick(e: MouseEvent) {
  emit("click", e);
}

const classes = {
  size: {
    fit: "",
    sm: "h-8 w-8",
    md: "h-10 w-10",
    lg: "h-12 w-12",
  },
  color: {
    blue: "border-2 border-blue-500 text-blue-500 hover:border-blue-400 active:border-blue-600 disabled:border-gray-400",
  },
};
</script>

<style scoped></style>
