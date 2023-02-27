<template>
  <li class="hover:border-b hover:border-blue-100">
    <div class="flex justify-between">
      <slot name="title">
        <span class="text-sm font-bold text-gray-900">{{ title }}</span>
        <button @click="onDelete">
          <Icon:material-symbols:delete-outline v-if="showDelete" class="h-5 w-5 text-blue-500 hover:text-blue-600" />
        </button>
      </slot>
    </div>
    <span class="text-xs text-gray-900">{{ text }}</span>
  </li>
</template>

<script setup lang="ts">
interface Props {
  title?: string;
  text?: string;
  showDelete?: boolean;
}

interface Emits {
  (e: "delete"): void;
}

defineProps<Props>();
const emit = defineEmits<Emits>();

function onDelete() {
  emit("delete");
}
</script>
