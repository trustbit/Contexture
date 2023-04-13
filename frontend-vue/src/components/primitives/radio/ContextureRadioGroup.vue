<template>
  <div class="flex flex-col gap-y-3">
    <span v-if="descriptionPosition === 'top'" class="block border-l-2 border-l-blue-500 pl-2 text-sm text-gray-600">
      {{ description }}
    </span>
    <div v-for="(option, index) of options" :key="index" class="flex items-center">
      <ContextureRadio
        :model-value="value"
        :value="option.value"
        :label="option.label"
        :description="option.description"
        :name="name"
        :show-error="false"
        @click="onClick"
      />
    </div>
    <span v-if="errorMessage && meta.touched" class="block border-l-2 border-l-red-500 pl-2 text-sm text-red-500">
      {{ errorMessage }}
    </span>
    <span v-if="descriptionPosition === 'bottom'" class="block border-l-2 border-l-blue-500 pl-2 text-sm text-gray-600">
      {{ description }}
    </span>
  </div>
</template>

<script setup lang="ts">
import { useField } from "vee-validate";
import { watch } from "vue";
import ContextureRadio from "~/components/primitives/radio/ContextureRadio.vue";

interface Props {
  modelValue?: any;
  options: { value: any; label: string; description?: string }[];
  name: string;
  description?: string;
  descriptionPosition?: "top" | "bottom";
}

const props = withDefaults(defineProps<Props>(), {
  options: () => [],
  descriptionPosition: "bottom",
});

const emit = defineEmits<{
  (e: "update:modelValue", value: any): void;
  (e: "click", value: any): void;
}>();

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

function onClick(event: Event): void {
  if (event.target) {
    emit("click", (event.target as HTMLInputElement).value);
  }
}
</script>
