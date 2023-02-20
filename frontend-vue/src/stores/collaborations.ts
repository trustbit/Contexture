import { defineStore } from "pinia";
import { Ref } from "vue";
import { onMounted, ref } from "vue";
import { useFetch } from "~/composables/useFetch";
import { Collaboration, CollaborationId, CreateCollaborator, RelationshipType } from "~/types/collaboration";

export const useCollaborationsStore = defineStore("collaborations", () => {
  const collaborations: Ref<Collaboration[]> = ref([]);
  const loading = ref(false);
  const error = ref();

  async function fetchCollaborations(): Promise<void> {
    loading.value = true;
    const { data, error } = await useFetch("/api/collaborations").get().json<Collaboration[]>();
    loading.value = false;
    collaborations.value = data.value || [];
    error.value = error;
  }

  async function createInboundConnection(collaboration: CreateCollaborator) {
    const res = await useFetch<Collaboration>("/api/collaborations/inboundConnection").post(collaboration);

    if (res.response.value?.ok && res.data.value) {
      collaborations.value = [...collaborations.value, res.data.value];
    }

    return res;
  }

  async function createOutboundConnection(collaboration: CreateCollaborator) {
    const res = await useFetch<Collaboration>("/api/collaborations/outboundConnection").post(collaboration);

    if (res.response.value?.ok && res.data.value) {
      collaborations.value = [...collaborations.value, res.data.value];
    }

    return res;
  }

  async function deleteCollaborationById(id: CollaborationId) {
    const res = await useFetch<Collaboration>(`/api/collaborations/${id}`).delete();

    if (res.response.value?.ok && res.data.value) {
      collaborations.value = collaborations.value.filter((c) => c.id !== id);
    }

    return res;
  }

  async function refineRelationship(
    collaborationId: CollaborationId,
    relationship: RelationshipType | "unknown" | unknown
  ) {
    const res = await useFetch<Collaboration>(`/api/collaborations/${collaborationId}/relationship`).post({
      relationshipType: relationship,
    });

    if (res.response.value?.ok && res.data.value) {
      collaborations.value = collaborations.value.map((obj) => (obj.id === res.data.value?.id ? res.data.value : obj));
    }

    return res;
  }

  onMounted(fetchCollaborations);

  return {
    collaborations,
    loading,
    error,
    createInboundConnection,
    createOutboundConnection,
    deleteCollaborationById,
    refineRelationship,
  };
});
