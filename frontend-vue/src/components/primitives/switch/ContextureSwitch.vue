<template>
  <SwitchGroup>
    <div class="flex items-center">
      <Switch
        :class="modelValue ? 'bg-blue-500' : 'bg-gray-300'"
        :default-checked="modelValue"
        class="relative inline-flex h-[18px] w-[33px] items-center rounded-full"
        @update:model-value="onChange"
      >
        <span
          :class="modelValue ? 'translate-x-[18px]' : 'translate-x-1'"
          class="inline-block h-3 w-3 transform rounded-full bg-white transition"
        />
      </Switch>
      <SwitchLabel class="text-md ml-2 text-gray-800">
        {{ label }}
      </SwitchLabel>
    </div>
  </SwitchGroup>
</template>

<script lang="ts" setup>
import { Switch, SwitchGroup, SwitchLabel } from "@headlessui/vue";

export interface ContextureSwitchProps {
  modelValue: boolean;
  label: string;
}

defineProps<ContextureSwitchProps>();

const emit = defineEmits<{
  (e: "update:modelValue", value: string): void;
}>();

function onChange(event: any): void {
  emit("update:modelValue", event);
}
</script>
