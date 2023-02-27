<template>
  <button
    :class="[classes.base, classes.size[size]]"
    :type="type"
    class="box-border rounded border-2 border-blue-500 text-blue-500 hover:border-blue-400 hover:text-blue-400 focus:border-blue-500 focus:shadow-[0px_0px_5px] focus:shadow-blue-300 active:border-blue-600 active:text-blue-700 disabled:border-gray-400 disabled:text-gray-700"
    @click="onClick"
  >
    <slot name="left" />
    {{ label }}
    <slot name="right" />
  </button>
</template>

<script lang="ts" setup>
interface Props {
  label: string;
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
    sm: "px-2 py-1 text-xs",
    md: "px-2.5 py-1.5 text-base",
    lg: "px-3 py-1.5 text-lg",
  },
};
</script>
