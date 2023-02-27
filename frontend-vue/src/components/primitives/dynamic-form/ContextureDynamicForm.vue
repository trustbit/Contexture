<template>
  <Form class="space-y-6" @submit="onSubmit">
    <div v-for="{ name, component, componentProps } in schema.fields" :key="name" class="flex flex-col gap-1.5">
      <component :is="component" :name="name" v-bind="componentProps" />
    </div>

    <div>
      <ContexturePrimaryButton type="submit" v-bind="buttonProps" :class="buttonClass">
        <template #left>
          <icon:material-symbols:add class="mr-2" />
        </template>
      </ContexturePrimaryButton>
    </div>
  </Form>
</template>

<script setup lang="ts">
import { Form } from "vee-validate";
import ContexturePrimaryButton, {
  ContexturePrimaryButtonProps,
} from "~/components/primitives/button/ContexturePrimaryButton.vue";
import { DynamicFormSchema } from "~/components/primitives/dynamic-form/dynamicForm";

interface Props {
  schema: DynamicFormSchema<any>;
  buttonProps?: ContexturePrimaryButtonProps;
  buttonClass?: string;
}

interface Emits {
  (e: "submit", value: any): void;
}

defineProps<Props>();

const emit = defineEmits<Emits>();

function onSubmit(values: any) {
  emit("submit", values);
}
</script>
