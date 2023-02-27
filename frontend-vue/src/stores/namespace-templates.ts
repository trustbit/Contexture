import { defineStore } from "pinia";
import { Ref } from "vue";
import { onMounted, ref } from "vue";
import { useFetch } from "~/composables/useFetch";
import { NamespaceTemplate } from "~/types/namespace-templates";

export const useNamespaceTemplatesStore = defineStore("namespace-templates", () => {
  const namespaceTemplates: Ref<NamespaceTemplate[]> = ref([]);
  const loading = ref(false);
  const error = ref();

  async function fetchNamespaceTemplates(): Promise<void> {
    loading.value = true;
    const error = await fetch();
    loading.value = false;
    error.value = error;
  }

  async function fetch() {
    const { data, error } = await useFetch<NamespaceTemplate[]>("/api/namespaces/templates").get();

    namespaceTemplates.value = data.value ? data.value : [];
    return error;
  }

  onMounted(fetchNamespaceTemplates);

  return {
    fetchNamespaceTemplates,
    namespaceTemplates,
    loading,
    error,
  };
});
