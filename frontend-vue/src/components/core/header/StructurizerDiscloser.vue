<template>
  <div ref="container" class="w-100" v-show="structurizrConfigExist">
    <Disclosure
      v-slot="{ open }"
      as="div"
      class="hover:border-b hover:border-blue-100 ui-open:border-b ui-open:border-blue-100"
    >
      <DisclosureButton
        class="inline-flex w-full pt-4 pb-2 pl-2 text-left align-middle text-gray-900 hover:cursor-pointer"
      >
        <Icon:material-symbols:chevron-right class="h-6 w-6" :class="[{ 'rotate-90': open }]" />
        <div class="ml-1 w-full text-sm font-bold">
          <slot name="title">
            <span class="ml-1 text-sm font-bold">Structurizr visualization</span>
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
        <DisclosurePanel class="h-100 text-xs text-gray-900">
          <iframe
            ref="iframe"
            :id="structurizrKey"
            :src="structurizrUrl"
            :height="iframeHeight"
            :width="iframeWidth"
            marginwidth="0"
            marginheight="0"
            frameborder="0"
            scrolling="no"
            allowfullscreen="true"
          >
          </iframe>
        </DisclosurePanel>
      </transition>
    </Disclosure>
  </div>
</template>

<script setup lang="ts">
import { storeToRefs } from "pinia";
import { useStructurizrUrlsStore } from "~/stores/structurizr-urls";
import { Disclosure, DisclosureButton, DisclosurePanel } from "@headlessui/vue";
import { computed, ref } from "vue";

const container = ref(null);
const iframe = ref(null);

interface IframeEvent {
  aspectRatio: number;
  view: string;
  iframe: string;
}

const iframeWidth = computed(() => {
  return container.value?.clientWidth > 0
    ? container.value?.clientWidth
    : container.value?.parentElement?.clientWidth || 0;
});
const iframeHeight = ref<number>(0);
window.addEventListener("message", (event: MessageEvent<IframeEvent>) => {
  if ("aspectRatio" in event.data) {
    iframeHeight.value = iframeWidth.value / event.data?.aspectRatio;
  }
});

const { structurizrKey, structurizrUrl, structurizrConfigExist } = storeToRefs(useStructurizrUrlsStore());
</script>
