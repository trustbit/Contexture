<template>
  <div class="mt-2" aria-orientation="horizontal" role="tablist">
    <div
      ref="tabs"
      class="flex divide-x divide-blue-500 overflow-hidden rounded-2xl border border-blue-500 text-xs text-blue-500 sm:mr-10 sm:w-fit"
    >
      <button
        v-for="(option, index) of options"
        :key="index"
        class="inline-flex flex-grow items-center justify-center px-3 py-1.5 text-center hover:cursor-pointer hover:bg-blue-100"
        @click="() => select(index)"
        :tabindex="index"
        role="tab"
        :aria-selected="modelValue === index"
        :aria-controls="`tab-${index}`"
        :class="[
          {
            'bg-blue-500 text-white hover:cursor-pointer hover:bg-blue-500': modelValue === index,
          },
        ]"
      >
        {{ option }}
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onKeyDown, useFocusWithin } from "@vueuse/core";
import { ref } from "vue";

interface Props {
  options: string[];
  modelValue?: number;
}

interface Emits {
  (e: "update:modelValue", index: number): void;
}

const props = defineProps<Props>();
const emit = defineEmits<Emits>();
const tabs = ref();

const { focused } = useFocusWithin(tabs);

onKeyDown("ArrowRight", () => {
  if (focused?.value) {
    if (props.modelValue != null && props.modelValue + 1 < props.options.length) {
      console.log("is less");
      select(props.modelValue + 1);
    }
  }
});

onKeyDown("ArrowLeft", () => {
  if (focused?.value) {
    if (props.modelValue != null && props.modelValue > 0) {
      select(props.modelValue - 1);
    }
  }
});

function select(index: number) {
  emit("update:modelValue", index);
}
</script>
