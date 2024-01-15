<template>
  <div class="flex flex-col gap-1.5">
    <Combobox v-model="value" :name="name">
      <div class="relative">
        <label class="mb-1.5 block text-sm text-gray-900" :for="name">
          <span class="font-bold">{{ label }}</span>
        </label>
        <div
          class="relative w-full cursor-default overflow-hidden rounded bg-white text-left focus-visible:border-blue-500 focus-visible:outline-none focus-visible:ring-blue-500 sm:text-sm"
        >
          <ComboboxInput
            :id="id"
            :display-value="display"
            :name="name"
            :placeholder="placeholder"
            class="block w-full rounded border border-blue-100 bg-white p-2 text-gray-900 focus-visible:border-blue-500 focus-visible:outline-none focus-visible:ring-blue-500"
            :class="[
              {
                'border-red-500': errorMessage && (meta.touched || meta.dirty),
                'border-green-500': meta.valid && (meta.touched || meta.dirty),
              },
            ]"
            @change="complete"
            @blur="onInputBlur"
            @focusout="onFocusOut"
          />
          <ComboboxButton class="absolute inset-y-0 right-0 flex items-center pr-2">
            <Icon:material-Symbols:unfold-more />
          </ComboboxButton>
        </div>
        <TransitionRoot
          leave="transition ease-in duration-100"
          leave-from="opacity-100"
          leave-to="opacity-0"
          @after-leave="query = ''"
        >
          <ComboboxOptions
            class="absolute mt-1 max-h-60 w-full overflow-auto rounded-md bg-white py-1 text-base shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none sm:text-sm z-10"
          >
            <div
              v-if="suggestions?.length === 0 && query !== '' && !allowCustomValues"
              class="relative cursor-default select-none py-2 px-4 text-gray-700"
            >
              Nothing found.
            </div>

            <ComboboxOption v-for="d in suggestions" :key="d.id" v-slot="{ selected, active }" :value="d" as="template">
              <li
                :class="{
                  'bg-blue-500 text-white': active,
                  'text-gray-900': !active,
                }"
                class="relative cursor-default select-none py-2 pl-10 pr-4"
              >
                <span :class="{ 'font-medium': selected, 'font-normal': !selected }" class="block truncate">
                  {{ display(d) }}
                </span>
                <span
                  v-if="selected"
                  :class="{ 'text-white': active, 'text-blue-600': !active }"
                  class="absolute inset-y-0 left-0 flex items-center pl-3"
                >
                  <Icon:material-symbols:check
                /></span>
              </li>
            </ComboboxOption>

            <ComboboxOption v-if="query && allowCustomValues" :value="query" v-slot="{ selected, active }">
              <li
                :class="{
                  'bg-blue-500 text-white': active,
                  'text-gray-900': !active,
                }"
                class="relative cursor-default select-none py-2 pl-10 pr-4 border-t"
              >
                <slot name="customValue">
                  <span :class="{ 'font-medium': selected, 'font-normal': !selected }" class="block truncate">
                    {{ query }}
                  </span>
                </slot>
                <span
                  v-if="selected"
                  :class="{ 'text-white': active, 'text-blue-600': !active }"
                  class="absolute inset-y-0 left-0 flex items-center pl-3"
                >
                  <Icon:material-symbols:check
                  /></span>
              </li>
            </ComboboxOption>
          </ComboboxOptions>
        </TransitionRoot>
      </div>
    </Combobox>
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
import {
  Combobox,
  ComboboxButton,
  ComboboxInput,
  ComboboxOption,
  ComboboxOptions,
  TransitionRoot
} from "@headlessui/vue";
import { useField } from "vee-validate";
import { ref, watch } from "vue";
import { uniqueId } from "~/core";

interface Props {
  id?: string;
  label?: string;
  modelValue?: any;
  placeholder?: string;
  description?: string;
  displayValue?: (obj: any) => {};
  suggestions?: any[];
  name?: string;
  allowCustomValues?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  id: uniqueId(),
  name: "",
});

const emit = defineEmits<{
  (e: "update:modelValue", value: string): void;
  (e: "complete", value: string): void;
}>();

const query = ref<string>("");

const { value, errorMessage, meta } = useField(props.name, undefined, {
  initialValue: props.modelValue,
});

watch(value, (newValue) => {
  if (newValue !== props.modelValue) {
    emit("update:modelValue", newValue);
  }
});

watch(
  () => props.modelValue,
  (newModel) => {
    if (newModel !== value.value) {
      value.value = newModel;
    }
  }
);

function complete(event: any) {
  query.value = event.target.value;
  if (event.target) {
    emit("complete", event.target.value);
  }
}

const display = (toDisplay: any) => {
  if (toDisplay && props.displayValue) {
    return props.displayValue(toDisplay);
  }
  return toDisplay;
};

function onInputBlur() {
  if (props.allowCustomValues) {
    emit("update:modelValue", value.value);
  }
}

function onFocusOut() {
  if (props.allowCustomValues) {
    emit("update:modelValue", value.value);
  }
}
</script>