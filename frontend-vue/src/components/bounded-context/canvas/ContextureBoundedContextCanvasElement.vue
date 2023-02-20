<template>
  <div class="flex flex-col gap-y-2 rounded p-6">
    <div class="flex justify-between">
      <div>
        <div class="flex items-center">
          <component :is="titleIcon" aria-hidden="true" class="mr-1.5 h-5 w-5" />
          <h3 class="text-lg font-bold">
            {{ title }}
          </h3>
          <div class="ml-2">
            <ContextureTooltip :content="tooltip" placement="top" v-if="tooltip" class="flex items-center">
              <Icon:materialSymbols:info-outline class="h-5 w-5 text-gray-500"></Icon:materialSymbols:info-outline>
            </ContextureTooltip>
          </div>
        </div>
      </div>
      <div v-if="isEditable">
        <ContextureTextLinkButton v-if="isInEditMode" @click="onClose">
          <template #left>
            <icon:material-symbols:close />
          </template>
        </ContextureTextLinkButton>
        <ContextureTextLinkButton v-else @click="onOpen">
          <template #left>
            <icon:material-symbols:drive-file-rename-outline-outline />
          </template>
        </ContextureTextLinkButton>
      </div>
    </div>
    <div class="text-sm">
      <slot :edit-mode="isInEditMode" :close="onClose" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from "vue";
import ContextureTextLinkButton from "~/components/primitives/button/ContextureTextLinkButton.vue";
import ContextureTooltip from "~/components/primitives/tooltip/ContextureTooltip.vue";

interface Props {
  title?: string;
  titleIcon?: any;
  isEditable?: boolean;
  editMode?: boolean;
  tooltip?: string;
}

interface Emits {
  (e: "close"): void;

  (e: "open"): void;
}

const props = withDefaults(defineProps<Props>(), {
  editMode: false,
});
const emit = defineEmits<Emits>();

const isInEditMode = ref(props.editMode);

function onClose(): void {
  isInEditMode.value = false;
  emit("close");
}

function onOpen(): void {
  isInEditMode.value = true;
  emit("open");
}

watch(
  () => props.editMode,
  (value) => (isInEditMode.value = value)
);
</script>
