<template>
  <div class="flex flex-col gap-1.5">
    <label class="block text-sm text-gray-900" :for="name">
      <span class="font-bold">{{ label }}</span>
      <span v-if="required"> ({{ t("common.required") }}) </span>
    </label>
    <input
      :id="name"
      :name="name"
      :placeholder="placeholder"
      :value="inputValue"
      class="block w-full rounded border border-blue-100 p-2 text-gray-900 focus-visible:border-blue-500 focus-visible:outline-none focus-visible:ring-blue-500"
      :class="[
        {
          'border-red-500': errorMessage && !skipValidation && (meta.touched || meta.dirty),
          'border-green-500': meta.valid && !skipValidation && (meta.touched || meta.dirty),
          'cursor-not-allowed': disabled,
        },
      ]"
      :disabled="disabled"
      type="text"
      @input="onInput"
      @blur="handleBlur"
    />
    <span
      v-if="errorMessage && (meta.touched || meta.dirty)"
      class="block border-l-2 border-l-red-500 pl-2 text-sm text-red-500"
    >
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
import { uniqueId } from "~/core";

export interface ContextureInputTextProps {
  label?: string;
  name?: string;
  modelValue?: string;
  placeholder?: string;
  description?: string;
  required?: boolean;
  disabled?: boolean;
  skipValidation?: boolean;
  rules?: any;
}

interface ContextureInputTextEmit {
  (e: "update:modelValue", value: string): void;
}

const props = withDefaults(defineProps<ContextureInputTextProps>(), {
  disabled: false,
  name: uniqueId(),
});

const emit = defineEmits<ContextureInputTextEmit>();

const { t } = useI18n();

const {
  value: inputValue,
  handleChange,
  errorMessage,
  handleBlur,
  meta,
} = useField(toRef(props, "name"), props.rules, {
  initialValue: props.modelValue,
});

const onInput = (event: any) => {
  handleChange(event, true);
  emit("update:modelValue", event.target?.value);
};
</script>
