<template>
  <div>
    <div
      :id="id"
      class="flex"
      ref="referenceRef"
      role="button"
      :aria-expanded="isVisible"
      @blur="hidePopover"
      @focus="showPopover"
      @click="showPopover"
    >
      <slot name="button" />
    </div>

    <div
      v-if="isVisible"
      ref="floatingRef"
      class="translate-3d-0-0-0 z-20 cursor-default rounded bg-gray-100 px-3 py-1.5 text-gray-900 shadow-lg"
      :style="{
        position: strategy,
        top: `${y ?? 0}px`,
        left: `${x ?? 0}px`,
      }"
      role="region"
      :aria-labelledby="id"
    >
      <FocusTrap>
        <slot name="content" />
      </FocusTrap>
    </div>
  </div>
</template>

<script setup lang="ts">
import { flip, offset, Placement, shift, useFloating } from "@floating-ui/vue";
import { FocusTrap } from "@headlessui/vue";
import { onClickOutside } from "@vueuse/core";

import { ref, watch } from "vue";
import { uniqueId } from "~/core";

interface Props {
  id?: string;
  placement: Placement;
  open?: boolean;
}

interface Emits {
  (e: "update:open", open: boolean): void;
}

const props = withDefaults(defineProps<Props>(), {
  id: uniqueId(),
});
const emit = defineEmits<Emits>();

const referenceRef = ref();
const floatingRef = ref();
const isVisible = ref(props.open);

const { x, y, strategy } = useFloating(referenceRef, floatingRef, {
  placement: props.placement,
  open: isVisible,
  middleware: [offset(8), flip(), shift({ padding: 5 })],
});

function hidePopover() {
  isVisible.value = false;
  emit("update:open", isVisible.value);
}

function showPopover() {
  isVisible.value = true;
  emit("update:open", isVisible.value);
}

onClickOutside(floatingRef, hidePopover);

watch(
  () => props.open,
  (value: boolean) => {
    isVisible.value = value;
  }
);
</script>
