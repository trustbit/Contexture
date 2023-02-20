<template>
  <button :type="type" class="hover:text-blue-600" @click="onClick">
    <slot />
  </button>
</template>

<script lang="ts" setup>
interface Props {
  type?: "submit" | "reset" | "button";
  size?: "sm" | "md" | "lg" | "fit";
}

withDefaults(defineProps<Props>(), {
  type: "button",
  size: "md",
});

const emit = defineEmits<{
  (e: "click", value: MouseEvent): void;
}>();

function onClick(e: MouseEvent) {
  emit("click", e);
}
</script>
