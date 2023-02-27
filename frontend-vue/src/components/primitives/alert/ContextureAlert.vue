<template>
  <transition enter-from-class="opacity-0" enter-active-class="transition-opacity" appear>
    <div
      v-show="visible"
      class="flex rounded border-l-2 p-4"
      :class="classes[severity]"
      role="alert"
      aria-live="assertive"
      aria-atomic="true"
    >
      <div>
        <div>
          <slot />
        </div>
        <button
          v-if="closable"
          :aria-label="closeAriaLabel"
          type="button"
          v-bind="closeButtonProps"
          @click="close($event)"
        >
          <Icon:material-symbols:close />
        </button>
      </div>
    </div>
  </transition>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";

interface Props {
  severity?: "alert" | "success" | "warning";
  closable?: boolean;
  closeButtonProps?: any;
}

withDefaults(defineProps<Props>(), {
  severity: "alert",
});

const emit = defineEmits(["close"]);
const visible = ref(true);

const closeAriaLabel = computed(() => "close");

const classes = {
  alert: "border-red-300 bg-red-50 text-red-800",
  success: "border-green-300 bg-green-50 text-green-800",
  warning: "border-yellow-300 bg-yellow-50 text-yellow-800",
};

function close(event: any) {
  visible.value = false;
  emit("close", event);
}
</script>
