import { ref } from "vue";

export type Action = (values?: any) => PromiseLike<unknown>;
export interface ActionProps {
  action: Action;
}

export function useActionWithLoading({ action }: ActionProps) {
  const isLoading = ref(false);

  const handleAction = async (values: any) => {
    if (action) {
      isLoading.value = true;
      try {
        await action(values);
      } catch (error) {
        console.error(error);
      } finally {
        isLoading.value = false;
      }
    }
  };

  return { isLoading, handleAction };
}
