<template>
  <div>
    <div class="inline-flex items-center">
      <input
        :id="radioUniqueId"
        v-model="radioValue"
        type="radio"
        :name="name"
        :value="value"
        :disabled="disabled"
        class="h-4 w-4 border-blue-500 text-blue-500 disabled:cursor-not-allowed disabled:border-gray-400 disabled:text-gray-400"
        @click="onClick"
      />
      <div class="ml-2">
        <label :for="radioUniqueId">
          <span class="block font-medium" :class="[{ 'cursor-not-allowed text-gray-400': disabled }, labelClass]">{{
            label
          }}</span>
          <span
            class="text-xs font-normal text-gray-700"
            :class="[{ 'cursor-not-allowed': disabled, 'text-gray-400': !disabled }]"
            v-if="description"
            >{{ description }}</span
          >
        </label>
      </div>
    </div>
    <span v-if="errorMessage" class="block border-l-2 border-l-red-500 pl-2 text-sm text-red-500">
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

const { value: radioValue, errorMessage } = useField(props.name, undefined, {
  initialValue: props.modelValue,
});

watch(radioValue, (newValue) => {
  emit("update:modelValue", newValue);
});

watch(
  () => props.modelValue,
  (newModel) => {
    if (newModel !== radioValue.value) {
      radioValue.value = newModel;
    }
  }
);

function onClick(event: Event): void {
  if (event.target) {
    emit("click", (event.target as HTMLInputElement).value);
  }
}

const radioUniqueId = computed(() => uniqueId());
</script>
