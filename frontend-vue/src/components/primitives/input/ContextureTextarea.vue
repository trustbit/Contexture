<template>
  <div class="flex flex-col gap-1.5">
    <label class="block text-sm text-gray-900" :for="name">
      <span class="font-bold">{{ label }}</span>
      <span v-if="required"> ({{ t("common.required") }}) </span>
    </label>
    <textarea
      :id="name"
      :name="name"
      :placeholder="placeholder"
      :value="inputValue"
      class="block w-full rounded border border-blue-100 p-2 text-gray-900 focus-visible:border-blue-500 focus-visible:outline-none focus-visible:ring-blue-500"
      :class="[
        {
          'border-red-500': errorMessage && meta.touched,
          'border-green-500': meta.valid && meta.touched,
        },
      ]"
      type="text"
      @input="onInput"
      @blur="handleBlur"
    />
    <span v-if="errorMessage" class="block border-l-2 border-l-red-500 pl-2 text-sm text-red-500">
      {{ errorMessage }}
    </span>
    <span class="block border-l-2 border-l-blue-500 pl-2 text-sm text-gray-600">
      {{ description }}
    </span>
  </div>
</template>

<script lang="ts" setup>
import { useField } from "vee-validate";
import { toRef } from "vue";
import { useI18n } from "vue-i18n";

interface Props {
  label?: string;
  name: string;
  modelValue?: string;
  placeholder?: string;
  description?: string;
  required?: boolean;
  rules?: any;
}

const props = defineProps<Props>();

const emit = defineEmits<{
  (e: "update:modelValue", value: string): void;
}>();

const { t } = useI18n();

const {
  value: inputValue,
  handleChange,
  errorMessage,
  handleBlur,
  meta,
} = useField(toRef(props, "name"), props.rules, {
  initialValue: props.modelValue,
  valueProp: props.modelValue,
});

const onInput = (event: any) => {
  handleChange(event, true);
  emit("update:modelValue", event.target?.value);
};
</script>
