<template>
  <span :class="[classes.base, classes.size[size], className]">
    <slot />
  </span>
</template>

<script lang="ts" setup>
import { computed } from "vue";
import { ComputedRef } from "vue";

export interface ContextureBadgeProps {
  size: "sm" | "md";
  variant: "filled" | "outlined";
  mode: "dark" | "light";
  color: "blue" | "gray" | "purple" | "teal" | "green" | "yellow" | "orange" | "red";
}

const props = defineProps<ContextureBadgeProps>();

const classes = {
  base: "text-xs rounded-[20px] text-gray-900",
  size: {
    sm: "px-2 py-1",
    md: "px-2 py-2.5",
  },
  filled: {
    dark: {
      blue: "bg-blue-600 text-white",
      gray: "bg-gray-800 text-white",
      red: "bg-red-600 text-white",
      green: "bg-green-600 text-white",
      purple: "bg-purple-600 text-white",
      teal: "bg-teal-500 text-white",
      yellow: "bg-yellow-500 text-gray-900",
      orange: "bg-orange-600 text-white",
    },
    light: {
      blue: "bg-blue-50 text-gray-900",
      gray: "bg-gray-100 text-gray-900",
      red: "bg-red-200 text-gray-900",
      green: "bg-green-100 text-gray-900",
      purple: "bg-purple-100 text-gray-900",
      teal: "bg-teal-100 text-gray-900",
      yellow: "bg-yellow-100 text-gray-900",
      orange: "bg-orange-100 text-gray-900",
    },
  },
  outlined: {
    dark: {
      blue: "border border-blue-600 text-gray-900",
      gray: "border border-gray-800 text-gray-900",
      red: "border border-red-600 text-gray-900",
      green: "border border-green-600 text-gray-900",
      purple: "border border-purple-600 text-gray-900",
      teal: "border border-teal-500 text-gray-900",
      yellow: "border border-yellow-500 text-gray-900",
      orange: "border border-orange-500 text-gray-900",
    },
    light: {
      blue: "border border-blue-100 text-gray-900",
      gray: "border border-gray-300 text-gray-900",
      red: "border border-red-200 text-gray-900",
      green: "border border-green-200 text-gray-900",
      purple: "border border-purple-200 text-gray-900",
      teal: "border border-teal-200 text-gray-900",
      yellow: "border border-yellow-100 text-gray-900",
      orange: "border border-orange-200 text-gray-900",
    },
  },
};

const className: ComputedRef<string> = computed<string>(() => classes[props.variant][props.mode][props.color]);
</script>
