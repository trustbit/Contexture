<template>
  <div>
    <div class="inline-flex items-center">
      <input
        :id="checkboxUniqueId"
        v-model="checkboxValue"
        type="checkbox"
        :name="name"
        :value="value"
        :disabled="disabled"
        class="h-4 w-4 border-blue-500 text-blue-500 disabled:cursor-not-allowed disabled:border-gray-400 disabled:text-gray-400"
        @click="onClick"
      />
      <div class="ml-2 text-sm">
        <label :for="checkboxUniqueId">
          <span class="block text-base font-medium" :class="[{ 'cursor-not-allowed text-gray-400': disabled }]">{{
            label
          }}</span>
          <span
            class="text-xs font-normal"
            :class="[{ 'cursor-not-allowed text-gray-400': disabled, 'text-gray-700': !disabled }]"
            v-if="description"
            >{{ description }}</span
          >
        </label>
      </div>
    </div>
    <span
      v-if="errorMessage && (meta.touched || meta.dirty)"
      class="block border-l-2 border-l-red-500 pl-2 text-sm text-red-500"
    >
      {{ errorMessage }}
    </span>
  </div>
</template>

<script setup lang="ts">
import { useField } from "vee-validate";
import { computed, watch } from "vue";
import { uniqueId } from "~/core";

interface Props {
  value: any;
  modelValue?: any;
  label: string;
  name: string;
  description?: string;
  labelClass?: string;
  disabled?: boolean;
}

const props = defineProps<Props>();

const emit = defineEmits<{
  (e: "update:modelValue", value: any): void;
  (e: "click", value: any): void;
}>();

const {
  value: checkboxValue,
  errorMessage,
  meta,
} = useField(props.name, undefined, {
  initialValue: props.modelValue,
});

watch(checkboxValue, (newValue) => {
  emit("update:modelValue", newValue);
});

watch(
  () => props.modelValue,
  (newModel) => {
    if (newModel !== checkboxValue.value) {
      checkboxValue.value = newModel;
    }
  }
);

function onClick(event: Event): void {
  if (event.target) {
    emit("click", (event.target as HTMLInputElement).value);
  }
}

const checkboxUniqueId = computed(() => uniqueId());
</script>
