import { defineStore, storeToRefs } from "pinia";
import { computed, onMounted, Ref, ref } from "vue";
import { useFetch } from "~/composables/useFetch";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import { BoundedContextId } from "~/types/boundedContext";
import {
  CreateNamespace,
  CreateNamespaceLabel,
  Namespace,
  NamespaceId,
  NamespaceLabel,
  NamespaceLabelId,
} from "~/types/namespace";

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

  const namespaceLabelsByNamespaceName = computed<{
    [name: string]: NamespaceLabel[];
  }>(() => {
    return namespaces.value.reduce((acc: { [name: string]: NamespaceLabel[] }, curr: Namespace) => {
      curr.labels.forEach((l) => {
        if (!acc[curr.name]) {
          acc[curr.name] = [];
        }
        acc[curr.name].push(l);
      });
      return acc;
    }, {});
  });

  const namespaceLabelValuesByLabelName = computed<{
    [name: string]: string[];
  }>(() => {
    return namespaces.value.reduce((acc: { [name: string]: string[] }, curr: Namespace) => {
      curr.labels.forEach((l) => {
        if (!acc[l.name]) {
          acc[l.name] = [];
        }
        if (!acc[l.name].includes(l.value)) {
          acc[l.name].push(l.value);
        }
      });
      return acc;
    }, {});
  });

  const findNamespaceLabelValuesByLabelName = (labelName?: string): string[] => {
    if (!labelName) {
      return [];
    }
    return namespaceLabelValuesByLabelName.value[labelName] || [];
  };

  onMounted(fetchNamespaces);

  return {
    createNamespace,
    deleteNamespace,
    createNamespaceLabel,
    deleteNamespaceLabel,
    findNamespaceLabelValuesByLabelName,
    namespaceLabelsByNamespaceName,
    namespaces,
    loading,
    error,
  };
});
