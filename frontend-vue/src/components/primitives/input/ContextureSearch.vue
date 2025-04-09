<template>
  <div class="relative">
    <div class="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3">
      <Icon:materialSymbols:search class="h-5 w-5 text-gray-300" aria-hidden="true" />
    </div>
    <label :for="id">
      <slot name="label"></slot>
    </label>
    <input
      type="search"
      class="block w-full rounded border border-blue-100 p-2 pl-10 text-gray-900 focus-visible:border-blue-500 focus-visible:outline-none focus-visible:ring-blue-500"
      :id="id"
      :name="name"
      :placeholder="placeholder ? placeholder : 'Search...'"
      :value="modelValue"
      @input="onInput"
    />
    <div v-if="modelValue" class="absolute inset-y-0 right-0 flex cursor-pointer items-center pr-3" @click="clear">
      <Icon:materialSymbols:close class="h-5 w-5 bg-white text-blue-500 hover:text-blue-400" />
    </div>
  </div>
</template>

<script lang="ts" setup>
import { uniqueId } from "~/core";

interface Props {
  id?: string;
  name?: string;
  modelValue?: string;
  placeholder?: string;
}

interface ContextureInputTextEmit {
  (e: "update:modelValue", value: string): void;
}

withDefaults(defineProps<Props>(), {
  disabled: false,
  id: uniqueId(),
});

const emit = defineEmits<ContextureInputTextEmit>();

const onInput = (event: any) => {
  emit("update:modelValue", event.target?.value);
};

const clear = () => {
  emit("update:modelValue", "");
};
</script>
