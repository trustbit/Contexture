import { defineStore, storeToRefs } from "pinia";
import { onMounted, Ref, ref } from "vue";
import { useFetch } from "~/composables/useFetch";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import { BoundedContextId } from "~/types/boundedContext";
import { CreateNamespace, CreateNamespaceLabel, Namespace, NamespaceId, NamespaceLabelId } from "~/types/namespace";

export const useNamespaces = defineStore("namespaces", () => {
  const { activeBoundedContext } = storeToRefs(useBoundedContextsStore());
  const namespaces: Ref<Namespace[]> = ref([]);
  const loading = ref(false);
  const error = ref();

  async function fetchNamespaces(): Promise<void> {
    loading.value = true;
    const error = await fetch();
    loading.value = false;
    error.value = error;
  }

  async function fetch() {
    const { data, error } = await useFetch<Namespace[]>("/api/namespaces").get();

    namespaces.value = data.value ? data.value : [];
    return error;
  }

  async function createNamespace(boundedContextId: BoundedContextId, namespace: CreateNamespace) {
    const { data, error } = await useFetch<Namespace[]>(`/api/boundedContexts/${boundedContextId}/namespaces`).post(
      namespace
    );

    if (!error.value) {
      activeBoundedContext.value!.namespaces = data.value as Namespace[];
      await fetch();
    }

    return {
      data,
      error,
    };
  }

  async function deleteNamespace(boundedContextId: BoundedContextId, namespaceId: NamespaceId) {
    const { data, error } = await useFetch<Namespace>(
      `/api/boundedContexts/${boundedContextId}/namespaces/${namespaceId}`
    ).delete();

    if (!error.value) {
      activeBoundedContext.value!.namespaces = activeBoundedContext.value!.namespaces.filter(
        (n) => n.id !== namespaceId
      );
      namespaces.value = namespaces.value.filter((n) => n.id !== namespaceId);
    }

    return {
      data,
      error,
    };
  }

  async function createNamespaceLabel(
    boundedContextId: BoundedContextId,
    namespaceId: NamespaceId,
    label: CreateNamespaceLabel
  ) {
    const { data, error } = await useFetch<Namespace[]>(
      `/api/boundedContexts/${boundedContextId}/namespaces/${namespaceId}/labels`
    ).post(label);

    if (!error.value) {
      activeBoundedContext.value!.namespaces = data.value || [];
    }

    return {
      data,
      error,
    };
  }

  async function deleteNamespaceLabel(
    boundedContextId: BoundedContextId,
    namespaceId: NamespaceId,
    labelId: NamespaceLabelId
  ) {
    const { data, error } = await useFetch<Namespace[]>(
      `/api/boundedContexts/${boundedContextId}/namespaces/${namespaceId}/labels/${labelId}`
    ).delete();

    if (!error.value) {
      activeBoundedContext.value!.namespaces = data.value || [];
    }

    return {
      data,
      error,
    };
  }

  onMounted(fetchNamespaces);

  return {
    createNamespace,
    deleteNamespace,
    createNamespaceLabel,
    deleteNamespaceLabel,
    namespaces,
    loading,
    error,
  };
});
