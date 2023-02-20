<template>
  <div>
    <div :id="`${title}_header`">
      <div v-if="buttonVariant === 'textLink'">
        <ContextureTextLinkButton
          :id="`${title}_header`"
          :label="isCollapsed ? label : cancelText"
          size="sm"
          @click="toggle"
        >
          <template #left>
            <icon:material-symbols:add v-if="isCollapsed" :class="[{ 'mr-2': label }]" />
            <icon:material-symbols:close v-else :class="[{ 'mr-2': cancelText }]" />
          </template>
        </ContextureTextLinkButton>
      </div>
      <div v-else>
        <ContextureWhiteButton
          :id="`${title}_header`"
          :label="isCollapsed ? label : cancelText"
          size="sm"
          @click="toggle"
        >
          <template #left>
            <icon:material-symbols:add v-if="isCollapsed" :class="[{ 'mr-2': label }]" />
            <icon:material-symbols:close v-else :class="[{ 'mr-2': cancelText }]" />
          </template>
        </ContextureWhiteButton>
      </div>
    </div>
    <div v-if="!isCollapsed" class="mt-2 text-xs text-gray-900" role="region" :aria-labelledby="`${title}_header`">
      <slot />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from "vue";
import ContextureTextLinkButton from "~/components/primitives/button/ContextureTextLinkButton.vue";
import ContextureWhiteButton from "~/components/primitives/button/ContextureWhiteButton.vue";

interface ContextureAccordionProps {
  title?: string;
  label?: string;
  collapsed?: boolean;
  cancelText?: string;
  buttonVariant?: "button" | "textLink";
}

const props = withDefaults(defineProps<ContextureAccordionProps>(), {
  buttonVariant: "button",
  collapsed: true,
});

const emit = defineEmits(["update:collapsed", "toggle"]);

const isCollapsed = ref(props.collapsed);

function toggle(event: Event) {
  isCollapsed.value = !isCollapsed.value;
  emit("update:collapsed", isCollapsed.value);
  emit("toggle", {
    originalEvent: event,
    value: isCollapsed.value,
  });
}

watch(
  () => props.collapsed,
  () => {
    isCollapsed.value = props.collapsed;
  }
);
</script>
