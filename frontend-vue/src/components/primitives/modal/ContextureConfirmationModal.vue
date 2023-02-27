<template>
  <ContextureModal :title="title" :is-open="isOpen">
    <div class="mt-4">
      <div v-if="component">
        <component :is="component" v-bind="componentProps" />
      </div>
      <div v-else>
        <p class="text-sm text-gray-500">
          {{ body }}
        </p>
      </div>
    </div>

    <div class="mt-4">
      <ContexturePrimaryButton :label="confirmButtonText" @click="emit('confirm')" />
    </div>
  </ContextureModal>
</template>

<script lang="ts" setup>
import ContexturePrimaryButton from "~/components/primitives/button/ContexturePrimaryButton.vue";
import ContextureModal from "~/components/primitives/modal/ContextureModal.vue";

interface Props {
  isOpen: boolean;
  title: string;
  body?: string;
  component?: any;
  componentProps?: any;
  confirmButtonText: string;
}

defineProps<Props>();

const emit = defineEmits<{
  (e: "cancel"): void;
  (e: "confirm"): void;
}>();
</script>
