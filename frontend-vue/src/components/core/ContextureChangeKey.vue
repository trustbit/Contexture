<template>
  <ContextureInputText
    :model-value="modelValue"
    v-bind="props"
    name="key"
    :rules="keyValidation"
    @update:model-value="onChange"
  />
</template>

<script setup lang="ts">
import { toFieldValidator } from "@vee-validate/zod";
import { storeToRefs } from "pinia";
import * as zod from "zod";
import { RefinementCtx } from "zod";
import ContextureInputText from "~/components/primitives/input/ContextureInputText.vue";
import { contains, endsWith, isAlpha, startsWith, startsWithNumber } from "~/core/validation";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import { useDomainsStore } from "~/stores/domains";

interface Props {
  modelValue?: string;
}

const props = defineProps<Props>();
const emit = defineEmits(["update:modelValue"]);

const { allDomains } = storeToRefs(useDomainsStore());
const { boundedContexts } = storeToRefs(useBoundedContextsStore());

const isUniqueShortName = (arg: string, ctx: RefinementCtx) => {
  const issue = findListContainingProperty(arg);

  if (!issue) {
    return false;
  }

  ctx.addIssue({
    code: zod.ZodIssueCode.custom,
    message: issue,
  });
};

function mapDomain(prop: string): string {
  return `${allDomains.value.find((d) => d.shortName?.toUpperCase() === prop)?.name}-${
    allDomains.value.find((d) => d.shortName?.toUpperCase() === prop)?.shortName
  }`;
}

function mapBoundedContext(prop: string): string {
  return `${boundedContexts.value.find((bc) => bc.shortName?.toUpperCase() === prop)?.name}-${
    boundedContexts.value.find((bc) => bc.shortName?.toUpperCase() === prop)?.shortName
  }`;
}

function findListContainingProperty(prop: any): string | null {
  const upperCaseProp = prop.toUpperCase();
  if (
    allDomains.value
      .filter((d) => d.shortName !== props.modelValue)
      .map((d) => d.shortName?.toUpperCase())
      .includes(upperCaseProp)
  ) {
    return `The short name '${upperCaseProp}' is already in use by domain '${mapDomain(upperCaseProp)}'`;
  }
  if (
    boundedContexts.value
      .filter((d) => d.shortName !== props.modelValue)
      .map((d) => d.shortName?.toUpperCase())
      .includes(upperCaseProp)
  ) {
    return `The short name '${upperCaseProp}' is already in use by bounded context '${mapBoundedContext(
      upperCaseProp
    )}'`;
  }
  return null;
}

const keyValidation = toFieldValidator(
  zod
    .string()
    .max(16)
    .superRefine(isUniqueShortName)
    .refine((ph) => !startsWithNumber(ph), {
      message: "Must not start with a number",
    })
    .refine((ph) => !contains(ph, " "), {
      message: "Must not contain whitespace",
    })
    .refine((ph) => !startsWith(ph, "-"), {
      message: "Must not start with hyphen",
    })
    .refine((ph) => !endsWith(ph, "-"), { message: "Must not end with hyphen" })
    .refine((ph) => isAlpha(ph), {
      message: "Must be valid alphabetic character",
    })
    .nullish()
);

function onChange(text: string) {
  emit("update:modelValue", text);
}
</script>
