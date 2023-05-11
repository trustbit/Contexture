<template>
  <ContextureInputText
    :model-value="modelValue"
    v-bind="props"
    name="key"
    :rules="shortNameValidationRules"
    @update:model-value="onChange"
  />
</template>

<script setup lang="ts">
import { toFieldValidator } from "@vee-validate/zod";
import { storeToRefs } from "pinia";
import { shortNameValidationSchema } from "~/components/core/change-short-name/changeShortNameValidationSchema";
import ContextureInputText from "~/components/primitives/input/ContextureInputText.vue";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import { useDomainsStore } from "~/stores/domains";

interface Props {
  modelValue?: string;
}

const props = defineProps<Props>();
const emit = defineEmits(["update:modelValue"]);

const { allDomains } = storeToRefs(useDomainsStore());
const { boundedContexts } = storeToRefs(useBoundedContextsStore());

const shortNameValidationRules = toFieldValidator(
  shortNameValidationSchema(props.modelValue, allDomains.value, boundedContexts.value)
);

function onChange(text: string) {
  emit("update:modelValue", text);
}
</script>
