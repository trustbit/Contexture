<template>
  <Disclosure
    v-slot="{ open }"
    :default-open="defaultOpen"
    as="div"
    class="hover:border-b hover:border-blue-100 ui-open:border-b ui-open:border-blue-100"
  >
    <DisclosureButton
      class="inline-flex w-full pt-4 pb-2 pl-2 text-left align-middle text-gray-900 hover:cursor-pointer"
    >
      <Icon:material-symbols:chevron-right class="h-6 w-6" :class="[{ 'rotate-90': open }]" />
      <div class="ml-1 w-full text-sm font-bold">
        <slot name="title">
          <span class="ml-1 text-sm font-bold">{{ title }}</span>
        </slot>
      </div>
    </DisclosureButton>
    <transition
      enter-active-class="overflow-hidden transition-max-height duration-200 ease-in"
      enter-from-class="max-h-0"
      enter-to-class="max-h-[1000px]"
      leave-from-class="max-h-[1000px]"
      leave-active-class="overflow-hidden transition-max-height duration-200 ease-out"
      leave-to-class="max-h-0"
    >
      <DisclosurePanel class="pb-2 pl-2 pr-2 text-xs text-gray-900 sm:pl-9 sm:pr-2">
        <slot />
      </DisclosurePanel>
    </transition>
  </Disclosure>
</template>

<script setup lang="ts">
import { Disclosure, DisclosureButton, DisclosurePanel } from "@headlessui/vue";

interface ContextureAccordionItemProps {
  defaultOpen?: boolean;
  title?: string;
}

defineProps<ContextureAccordionItemProps>();
</script>
