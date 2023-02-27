import { defineStore } from "pinia";
import { shallowRef } from "vue";

export interface ConfirmationModal {
  isOpen: boolean;
  title: string;
  body: string;
  component?: any;
  componentProps?: any;
  confirmButtonText: string;
  onConfirm: (props?: any) => void;
  onCancel: (props?: any) => void;
}

export const useConfirmationModalStore = defineStore("confirmation-modal", {
  state: (): ConfirmationModal => ({
    isOpen: false,
    title: "",
    body: "",
    confirmButtonText: "",
    componentProps: "",
    component: shallowRef(),
    onConfirm: () => {},
    onCancel: () => {},
  }),
  actions: {
    open(
      title: string,
      body: string,
      confirmButtonText: string,
      onConfirm: (props?: any) => void,
      onCancel?: (props?: any) => void
    ) {
      this.isOpen = true;
      this.title = title;
      this.body = body;
      this.confirmButtonText = confirmButtonText;
      this.onConfirm = onConfirm;
      this.onCancel = onCancel || (() => {});
    },
    openWithComponent(
      title: string,
      component: any,
      componentProps: any,
      confirmButtonText: string,
      onConfirm: (props?: any) => void,
      onCancel?: (props?: any) => void
    ) {
      this.isOpen = true;
      this.title = title;
      this.component = component;
      this.confirmButtonText = confirmButtonText;
      this.componentProps = componentProps;
      this.onConfirm = onConfirm;
      this.onCancel = onCancel || (() => {});
    },
    cancel() {
      this.onCancel();
      this.isOpen = false;
      setTimeout(() => {
        this.$reset();
      }, 500);
    },
    confirm() {
      this.onConfirm();
      this.isOpen = false;
      setTimeout(() => {
        this.$reset();
      }, 500);
    },
  },
});

export default useConfirmationModalStore;
