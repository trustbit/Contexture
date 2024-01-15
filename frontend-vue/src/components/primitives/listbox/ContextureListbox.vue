<template>
  <Listbox v-model="value" :by="keyProp">
    <div class="relative mt-1">
      <ListboxButton
        class="relative block w-full rounded border border-blue-100 bg-gray-100 p-2 text-left text-gray-900 focus-visible:border-blue-500 focus-visible:outline-none focus-visible:ring-blue-500"
      >
        <span class="block truncate">{{ display(value) || "Please select an option" }}</span>
        <span class="pointer-events-none absolute inset-y-0 right-0 flex items-center pr-2">
          <Icon:material-Symbols:unfold-more class="h-5 w-5 text-gray-400" aria-hidden="true" />
        </span>
      </ListboxButton>

      <transition
        leave-active-class="transition duration-100 ease-in"
        leave-from-class="opacity-100"
        leave-to-class="opacity-0"
      >
        <ListboxOptions
          class="absolute z-10 mt-1 max-h-60 w-full overflow-auto rounded bg-white py-1 text-base shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none sm:text-sm"
        >
          <div v-if="options?.length === 0" class="relative cursor-default select-none py-2 px-4 text-gray-700">
            No options available
          </div>

          <ListboxOption
            v-for="option in options"
            v-slot="{ active, selected }"
            :key="option[keyProp]"
            :value="option"
            as="template"
          >
            <li
              class="relative cursor-default select-none py-2 pl-10 pr-4"
              :class="[active ? 'bg-blue-100 text-blue-500' : 'text-gray-900']"
            >
              <span class="block truncate" :class="[selected ? 'font-medium' : 'font-normal']">
                {{ display(option) }}
              </span>
              <span class="text-xs font-light">{{ option.description }}</span>
              <!-- todo improve this with a configurable property -->
              <span v-if="selected" class="absolute inset-y-0 left-0 flex items-center pl-3 text-blue-600">
                <Icon:material-symbols:check class="h-5 w-5" aria-hidden="true" />
              </span>
            </li>
          </ListboxOption>
        </ListboxOptions>
      </transition>
    </div>

    <span v-if="errorMessage" class="block border-l-2 border-l-red-500 pl-2 text-sm text-red-500">
      {{ errorMessage }}
    </span>
    <span class="block border-l-2 border-l-blue-500 pl-2 text-sm text-gray-600">
      {{ description }}
    </span>
  </Listbox>
</template>

<script setup lang="ts">
import { watch } from "vue";
import { Listbox, ListboxButton, ListboxOption, ListboxOptions } from "@headlessui/vue";
import { useField } from "vee-validate";

interface Props {
  label?: string;
  modelValue?: any;
  options: any[]; // todo improve this typing
  name?: string;
  displayValue?: (obj: any) => {};
  rules?: any;
  description?: string;
  keyProp: string;
}

interface Emits {
  (e: "update:modelValue", value: any): void;
  (e: "selected", value: any): void;
}

const props = withDefaults(defineProps<Props>(), {
  name: "",
});

const emit = defineEmits<Emits>();

const { value, errorMessage } = useField(props.name, props.rules, {
  initialValue: props.modelValue,
});

watch(value, (newValue) => {
  if (newValue !== props.modelValue) {
    emit("update:modelValue", newValue);
    emit("selected", newValue);
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

function display(toDisplay: any) {
  if (toDisplay && props.displayValue) {
    return props.displayValue(toDisplay);
  }
  return "";
}
</script>
