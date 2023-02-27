<template>
  <button
    :class="[classes.base, classes.size[size]]"
    :type="type"
    class="box-border rounded bg-blue-500 text-gray-50 hover:bg-blue-400 focus:bg-blue-500 focus:shadow-[0px_0px_5px] focus:shadow-blue-300 active:bg-blue-700 disabled:bg-gray-400"
    @click="onClick"
  >
    <slot name="left" />
    {{ label }}
    <slot name="right" />
  </button>
</template>

<script lang="ts" setup>
export interface ContexturePrimaryButtonProps {
  label?: string;
  type?: "submit" | "reset" | "button";
  size?: "sm" | "md" | "lg" | "fit";
}

withDefaults(defineProps<ContexturePrimaryButtonProps>(), {
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
    sm: "px-2 py-1 text-xs",
    md: "px-2.5 py-1.5 text-base",
    lg: "px-3 py-1.5 text-lg",
    fit: "",
  },
};
</script>
