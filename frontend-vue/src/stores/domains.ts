import { UseFetchReturn } from "@vueuse/core";
import { defineStore } from "pinia";
import { computed, onMounted, Ref, ref } from "vue";
import { useFetch } from "~/composables/useFetch";
import { CreateDomain, Domain, DomainId, UpdateDomain } from "~/types/domain";

export const useDomainsStore = defineStore("domains", () => {
  const allDomains: Ref<Domain[]> = ref([]);
  const loading = ref(false);
  const loadingError = ref();

  async function fetchDomains(): Promise<void> {
    loading.value = true;
    const { data, error } = await fetch();
    loading.value = false;

    if (error.value) {
      loadingError.value = {
        error: error.value,
        response: data.value,
      };
    }
  }

  async function deleteDomain(id: DomainId): Promise<UseFetchReturn<void>> {
    const res = await useFetch<void>(`/api/domains/${id}`).delete();

    if (res.response.value?.ok) {
      await fetch();
    }

    return res;
  }

  async function moveDomain(domainIdToMove: DomainId, newParentDomainId?: DomainId): Promise<UseFetchReturn<void>> {
    const res = await useFetch<void>(`/api/domains/${domainIdToMove}/move`).post({
      parentDomainId: newParentDomainId,
    });

    if (res.response.value?.ok) {
      await fetch();
    }

    return res;
  }

  async function createDomain(createDomain: CreateDomain) {
    const res = await useFetch<Domain>("/api/domains").post(createDomain);

    if (res.response.value?.ok) {
      allDomains.value = [...allDomains.value, res.data.value as Domain];
    }

    return {
      data: res.data,
      error: res.error,
    };
  }

  async function createSubDomain(parentDomainId: DomainId, createDomain: CreateDomain) {
    const res = await useFetch<Domain>(`/api/domains/${parentDomainId}/domains`).post(createDomain);

    if (res.response.value?.ok) {
      await fetchDomains();
    }

    return {
      data: res.data,
      error: res.error,
    };
  }

  async function updateDomain(domainId: DomainId, update: UpdateDomain): Promise<Awaited<UseFetchReturn<Domain>>[]> {
    const response = await Promise.all([
      useFetch<Domain>(`/api/domains/${domainId}/shortName`).post({
        shortName: update.key,
      }),
      useFetch<Domain>(`/api/domains/${domainId}/rename`).post({
        name: update.name,
      }),
      useFetch<Domain>(`/api/domains/${domainId}/vision`).post({
        vision: update.vision,
      }),
    ]);

    if (response.find((r) => !r.response.value?.ok)) {
      return response;
    }

    await fetch();
    return response;
  }

  async function fetch() {
    const { data, error } = await useFetch<Domain[]>("/api/domains").get();

    allDomains.value = data.value ? data.value : [];
    return {
      data,
      error,
    };
  }

  const parentDomains = computed(() => allDomains.value.filter((d: Domain) => !d.parentDomainId));

  /**
   * Provide a lookup to find a domain by its id.
   *
   * Consider fetching this from the API.
   * However, I see no reason at the moment as we have to load the domains most of the time anyway
   */
  const domainByDomainId = computed<{
    [id: DomainId]: Domain;
  }>(() => {
    return allDomains.value.reduce((acc: { [id: DomainId]: Domain }, curr: Domain) => {
      if (!acc[curr.id]) {
        acc[curr.id] = curr;
      }
      return acc;
    }, {});
  });

  const subdomainsByDomainId = computed<{
    [id: DomainId]: Domain[];
  }>(() => {
    return allDomains.value.reduce((acc: { [id: DomainId]: Domain[] }, curr: Domain) => {
      if (!acc[curr.id]) {
        acc[curr.id] = [];
      }
      if (curr.parentDomainId) {
        if (!acc[curr.parentDomainId]) {
          acc[curr.parentDomainId] = [];
        }
        acc[curr.parentDomainId].push(curr);
      }
      return acc;
    }, {});
  });

  onMounted(fetchDomains);

  return {
    allDomains,
    loading,
    loadingError,
    parentDomains,
    subdomainsByDomainId,
    domainByDomainId,
    deleteDomain,
    moveDomain,
    createDomain,
    createSubDomain,
    updateDomain,
  };
});
