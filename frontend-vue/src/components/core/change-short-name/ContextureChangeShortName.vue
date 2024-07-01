<template>
  <ContextureInputText
    :model-value="modelValue"
    v-bind="props"
    name="key"
    :rules="shortNameValidationRules"
    @update:model-value="onChange"
    required
  />
</template>

<script setup lang="ts">
import { toFieldValidator } from "@vee-validate/zod";
import { storeToRefs } from "pinia";
import { shortNameValidationSchema } from "~/components/core/change-short-name/changeShortNameValidationSchema";
import ContextureInputText from "~/components/primitives/input/ContextureInputText.vue";
import { useDomainsStore } from "~/stores/domains";

interface Props {
  modelValue?: string;
}

const props = defineProps<Props>();
const emit = defineEmits(["update:modelValue"]);

const { allDomains } = storeToRefs(useDomainsStore());

const shortNameValidationRules = toFieldValidator(shortNameValidationSchema(props.modelValue, allDomains.value));

console.log(shortNameValidationRules);

function onChange(text: string) {
  emit("update:modelValue", text);
}
</script>
