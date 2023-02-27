<template>
  <button
    :class="[classes.base, classes.size[size]]"
    :type="type"
    class="box-border rounded text-blue-500 hover:text-blue-400 focus:shadow-[0px_0px_5px] focus:shadow-blue-300 active:text-blue-700 disabled:border-gray-400 disabled:text-gray-700"
    @click="onClick"
  >
    <slot name="left" />
    {{ label }}
    <slot name="right" />
  </button>
</template>

<script lang="ts" setup>
interface Props {
  label?: string;
  type?: "submit" | "reset" | "button";
  size?: "sm" | "md" | "lg";
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

const classes = {
  base: "font-bold inline-flex items-center",
  size: {
    sm: "text-xs",
    md: "text-base",
    lg: "text-lg",
  },
};
</script>
