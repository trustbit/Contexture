<template>
  <header>
    <ContextureNavbar />
  </header>
  <main>
    <RouterView />
  </main>

  <teleport to="body">
    <ContextureConfirmationModal
      :body="body"
      :confirm-button-text="confirmButtonText"
      :component="component"
      :component-props="componentProps"
      :is-open="isOpen"
      :title="title"
      @close="modal.cancel"
      @confirm="modal.confirm"
    />
  </teleport>
</template>

<script lang="ts" setup>
import { storeToRefs } from "pinia";
import ContextureNavbar from "~/components/core/navbar/ContextureNavbar.vue";
import ContextureConfirmationModal from "~/components/primitives/modal/ContextureConfirmationModal.vue";
import useConfirmationModalStore from "~/stores/confirmationModal";

const modal = useConfirmationModalStore();

const { isOpen, body, title, confirmButtonText, component, componentProps } = storeToRefs(modal);
</script>
