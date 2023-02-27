<template>
  <div>
    <div
      ref="referenceRef"
      class="flex"
      @blur="hideTooltip"
      @focus="showTooltip"
      @focusout="hideTooltip"
      @mouseenter="showTooltip"
      @mouseleave="hideTooltip"
    >
      <slot />
    </div>

    <div
      ref="floatingRef"
      class="z-50 rounded bg-blue-700 py-1 px-2 text-sm text-white"
      v-if="show"
      :style="{
        position: strategy,
        top: `${y ?? 0}px`,
        left: `${x ?? 0}px`,
        width: 'max-content',
      }"
    >
      <slot name="content">
        {{ props.content }}
      </slot>
      <div
        ref="arrowRef"
        class="absolute h-[8px] w-[8px] rotate-45 bg-blue-700"
        :style="{
          top: `${middlewareData.arrow?.y != null ? `${middlewareData.arrow?.y}px` : undefined}`,
          left: `${middlewareData.arrow?.x != null ? `${middlewareData.arrow?.x}px` : undefined}`,
          [arrowOpposite]: '-4px !important',
        }"
      ></div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { arrow, flip, offset, Placement, shift, useFloating } from "@floating-ui/vue";

import { computed, ref } from "vue";

interface Props {
  content: string;
  placement: Placement;
}

const props = defineProps<Props>();

const referenceRef = ref();
const floatingRef = ref();
const arrowRef = ref();
const show = ref(false);

function hideTooltip() {
  show.value = false;
}

const {
  x,
  y,
  strategy,
  middlewareData,
  update,
  placement: finalPlacement,
} = useFloating(referenceRef, floatingRef, {
  placement: props.placement,
  middleware: [offset(8), flip(), shift({ padding: 5 }), arrow({ element: arrowRef })],
});

const opposite: { [position: string]: string } = {
  top: "bottom",
  right: "left",
  bottom: "top",
  left: "right",
};

const arrowOpposite = computed(() => {
  if (finalPlacement.value) {
    return opposite[finalPlacement.value.split("-")[0]];
  }
  return null;
});

async function showTooltip() {
  show.value = true;
  update();
}
</script>
