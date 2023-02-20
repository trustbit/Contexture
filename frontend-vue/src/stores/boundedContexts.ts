import { UseFetchReturn } from "@vueuse/core";
import { useRouteParams } from "@vueuse/router";
import { defineStore } from "pinia";
import { computed, onMounted, Ref, ref } from "vue";
import { useFetch } from "~/composables/useFetch";
import {
  BoundedContext,
  BoundedContextId,
  BusinessDecision,
  Classification,
  CreateBoundedContext,
  DomainRole,
  Messages,
  UbiquitousLanguage,
  UbiquitousLanguageItem,
} from "~/types/boundedContext";
import { DomainId } from "~/types/domain";

export const useBoundedContextsStore = defineStore("bounded-contexts", () => {
  const boundedContexts: Ref<BoundedContext[]> = ref([]);
  const loading = ref(false);
  const error = ref();

  const boundedContextsByDomainId = computed<{
    [id: DomainId | string]: BoundedContext[];
  }>(() => {
    return boundedContexts.value.reduce((acc: { [id: DomainId]: BoundedContext[] }, curr: BoundedContext) => {
      if (!acc[curr.domain.id]) {
        acc[curr.domain.id] = [];
      }
      acc[curr.domain.id].push(curr);
      return acc;
    }, {});
  });

  const boundedContextsByBoundedContextId = computed<{
    [id: BoundedContextId | string]: BoundedContext;
  }>(() => {
    return boundedContexts.value.reduce((acc: { [id: DomainId]: BoundedContext }, curr: BoundedContext) => {
      if (!acc[curr.domain.id]) {
        acc[curr.id] = curr;
      }
      return acc;
    }, {});
  });

  const activeBoundedContextId = useRouteParams<string>("id");

  const activeBoundedContext = computed(() => {
    return boundedContextsByBoundedContextId.value[activeBoundedContextId.value];
  });

  async function fetchBoundedContexts(): Promise<void> {
    loading.value = true;
    const error = await fetch();
    loading.value = false;
    error.value = error;
  }

  async function fetch() {
    const { data, error } = await useFetch<BoundedContext[]>("/api/boundedContexts").get();

    boundedContexts.value = data.value ? data.value : [];
    return error;
  }

  async function createBoundedContext(domainId: DomainId, createBoundedContext: CreateBoundedContext) {
    const res = await useFetch<BoundedContext>(`/api/domains/${domainId}/boundedContexts`).post(createBoundedContext);

    if (res.response.value?.ok) {
      await fetchBoundedContexts();
    }

    return {
      data: res.data,
      error: res.error,
    };
  }

  async function deleteBoundedContext(boundedContextId: BoundedContextId) {
    const res = await useFetch<BoundedContext>(`/api/boundedContexts/${boundedContextId}`).delete();

    if (res.response.value?.ok) {
      await fetchBoundedContexts();
    }

    return res;
  }

  async function moveBoundedContext(boundedContextId: BoundedContextId, parentDomainId: DomainId) {
    const res = await useFetch<BoundedContext>(`/api/boundedContexts/${boundedContextId}/move`).post({
      parentDomainId: parentDomainId,
    });

    if (res.response.value?.ok) {
      await fetchBoundedContexts();
    }

    return res;
  }

  async function addBusinessDecision(id: BoundedContextId, businessDecision: BusinessDecision) {
    const activeBoundedContext = boundedContextsByBoundedContextId.value[id];
    const res = await useFetch<BoundedContext>(`/api/boundedContexts/${id}/businessDecisions`).post({
      businessDecisions: [...(activeBoundedContext.businessDecisions || []), businessDecision],
    });

    return handleResponse(res);
  }

  async function deleteBusinessDecision(id: BoundedContextId, businessDecision: BusinessDecision) {
    const activeBoundedContext = boundedContextsByBoundedContextId.value[id];
    const res = await useFetch<BoundedContext>(`/api/boundedContexts/${id}/businessDecisions`).post({
      businessDecisions: activeBoundedContext.businessDecisions?.filter((bc) => bc.name !== businessDecision.name),
    });

    return handleResponse(res);
  }

  async function addDomainRole(id: BoundedContextId, domainRole: DomainRole) {
    const activeBoundedContext = boundedContextsByBoundedContextId.value[id];
    const res = await useFetch<BoundedContext>(`/api/boundedContexts/${id}/domainRoles`).post({
      domainRoles: [...(activeBoundedContext.domainRoles || []), domainRole],
    });

    return handleResponse(res);
  }

  async function deleteDomainRole(id: BoundedContextId, domainRole: DomainRole) {
    const activeBoundedContext = boundedContextsByBoundedContextId.value[id];
    const res = await useFetch<BoundedContext>(`/api/boundedContexts/${id}/domainRoles`).post({
      domainRoles: activeBoundedContext.domainRoles?.filter((dr) => dr.name !== domainRole.name),
    });

    return handleResponse(res);
  }

  async function addUbiquitousLanguageItem(id: BoundedContextId, ubiquitousLanguageItem: UbiquitousLanguageItem) {
    const activeBoundedContext = boundedContextsByBoundedContextId.value[id];
    const res = await useFetch<BoundedContext>(`/api/boundedContexts/${id}/ubiquitousLanguage`).post({
      ubiquitousLanguage: {
        ...activeBoundedContext.ubiquitousLanguage,
        ...{
          [ubiquitousLanguageItem.term]: {
            term: ubiquitousLanguageItem.term,
            description: ubiquitousLanguageItem.description,
          },
        },
      },
    });

    return handleResponse(res);
  }

  async function deleteUbiquitousLanguage(id: BoundedContextId, ubiquitousLanguageKey: string) {
    const activeBoundedContext = boundedContextsByBoundedContextId.value[id];
    const newUbiquitousLanguage: UbiquitousLanguage = Object.assign({}, activeBoundedContext.ubiquitousLanguage);
    delete newUbiquitousLanguage[ubiquitousLanguageKey];

    const res = await useFetch<BoundedContext>(`/api/boundedContexts/${id}/ubiquitousLanguage`).post({
      ubiquitousLanguage: newUbiquitousLanguage,
    });

    return handleResponse(res);
  }

  async function updateDescription(id: BoundedContextId, newDescription: string | undefined) {
    const res = await useFetch<BoundedContext>(`/api/boundedContexts/${id}/description`).post({
      description: newDescription,
    });

    return handleResponse(res);
  }

  async function updateKey(id: BoundedContextId, newKey: string | undefined) {
    const res = await useFetch<BoundedContext>(`/api/boundedContexts/${id}/shortName`).post({ shortName: newKey });

    return handleResponse(res);
  }

  async function updateName(id: BoundedContextId, newName: string) {
    const res = await useFetch<BoundedContext>(`/api/boundedContexts/${id}/rename`).post({
      name: newName,
    });

    return handleResponse(res);
  }

  async function addMessage(id: BoundedContextId, key: keyof Messages, message: string) {
    const activeBoundedContext = boundedContextsByBoundedContextId.value[id];
    const currentMessages = activeBoundedContext.messages || {
      commandsHandled: [],
      commandsSent: [],
      eventsHandled: [],
      eventsPublished: [],
      queriesHandled: [],
      queriesInvoked: [],
    };

    const res = await useFetch<BoundedContext>(`/api/boundedContexts/${id}/messages`).post({
      messages: {
        ...currentMessages,
        [key]: currentMessages[key].includes(message) ? currentMessages[key] : [...currentMessages[key], message],
      },
    });

    return handleResponse(res);
  }

  async function deleteMessage(id: BoundedContextId, key: keyof Messages, message: string) {
    const activeBoundedContext = boundedContextsByBoundedContextId.value[id];
    const currentMessages = activeBoundedContext.messages || {
      commandsHandled: [],
      commandsSent: [],
      eventsHandled: [],
      eventsPublished: [],
      queriesHandled: [],
      queriesInvoked: [],
    };

    const res = await useFetch<BoundedContext>(`/api/boundedContexts/${id}/messages`).post({
      messages: {
        ...currentMessages,
        [key]: currentMessages[key].filter((item) => item !== message),
      },
    });

    return handleResponse(res);
  }

  async function reclassify(id: BoundedContextId, classification: Classification) {
    const res = await useFetch<BoundedContext>(`/api/boundedContexts/${id}/reclassify`).post({ classification });

    return handleResponse(res);
  }

  function handleResponse(res: UseFetchReturn<BoundedContext>) {
    if (res.response.value?.ok) {
      if (Array.isArray(res.data.value)) {
        // weird error when querying a single bounded context returning all bounded contexts
        return res;
      }
      const boundedContext = res.data.value;
      if (boundedContext) {
        const index = boundedContexts.value.findIndex((bc) => bc.id === boundedContext.id);
        boundedContexts.value[index] = boundedContext;
      }
    }

    return res;
  }

  onMounted(fetchBoundedContexts);

  return {
    boundedContexts,
    boundedContextsByDomainId,
    boundedContextsByBoundedContextId,
    activeBoundedContext,
    loading,
    error,
    createBoundedContext,
    deleteBoundedContext,
    moveBoundedContext,
    addBusinessDecision,
    addUbiquitousLanguageItem,
    addDomainRole,
    updateDescription,
    updateKey,
    updateName,
    reclassify,
    addMessage,
    deleteMessage,
    deleteBusinessDecision,
    deleteDomainRole,
    deleteUbiquitousLanguage,
  };
});
