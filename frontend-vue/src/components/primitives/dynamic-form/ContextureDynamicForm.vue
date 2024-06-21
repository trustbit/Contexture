<template>
  <Form class="space-y-6" @submit="handleAction">
    <div v-for="{ name, component, componentProps } in schema.fields" :key="name" class="flex flex-col gap-1.5">
      <component :is="component" :name="name" v-bind="componentProps" />
    </div>

    <LoadingWrapper as div :is-loading="isLoading">
      <ContexturePrimaryButton type="submit" v-bind="buttonProps" :class="buttonClass">
        <template #left>
          <icon:material-symbols:add class="mr-2" />
        </template>
      </ContexturePrimaryButton>
    </LoadingWrapper>
  </Form>
</template>

<script setup lang="ts">
import { Form } from "vee-validate";
import ContexturePrimaryButton, {
  ContexturePrimaryButtonProps,
} from "~/components/primitives/button/ContexturePrimaryButton.vue";
import { DynamicFormSchema } from "~/components/primitives/dynamic-form/dynamicForm";
import { ActionProps, useActionWithLoading } from "~/components/primitives/button/util/useActionWithLoading";
import LoadingWrapper from "~/components/primitives/button/util/LoadingWrapper.vue";

interface Props extends ActionProps {
  schema: DynamicFormSchema<any>;
  buttonProps?: ContexturePrimaryButtonProps;
  buttonClass?: string;
}
const props = defineProps<Props>();
const { isLoading, handleAction } = useActionWithLoading(props);
</script>
