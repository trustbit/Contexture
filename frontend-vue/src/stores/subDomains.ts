/* export a nested subdomains store from the root domains store */
import { defineStore } from "pinia";
import { computed, ref } from "vue";
import { Domain, DomainId } from "~/types/domain";
import { useDomainsStore } from "~/stores/domains";

export const useSubdomainsStore = defineStore("subdomains", () => {
  const currentDomain = ref<Domain | undefined>(undefined);
  const maxSubdomainsLevel = ref(import.meta.env.CONTEXTURE_MAX_SUBDOMAINS_NESTING_LEVEL || 1);
  const domainLevel = ref(0);
  let init = true;

  function setCurrentDomain(domain: Domain) {
    currentDomain.value = domain;
    setSubdomainLevel(domain.id);
  }

  function setSubdomainLevel(domainId: DomainId) {
    const domainStore = useDomainsStore();
    const foundDomain = domainStore.allDomains.find((d: Domain) => d.id === domainId);
    const parentDomainId = foundDomain?.parentDomainId || undefined;

    if (!foundDomain) {
      console.error(`Domain with id ${domainId} not found.`);
      return;
    }

    if (init) domainLevel.value = 0;

    console.log("setSubdomainLevel", domainId, foundDomain, parentDomainId);

    if (parentDomainId && init) {
      init = false;
      setSubdomainLevel(parentDomainId);
    } else if (!parentDomainId && init) {
      init = false;
      domainLevel.value--;
    } else {
      init = true;
    }
    domainLevel.value++;

    return {
      isSubdomain,
      domainLevel,
    };
  }

  const isCreateSubdomainEnabled = computed(() => {
    return domainLevel.value >= maxSubdomainsLevel.value;
  });

  const isSubdomain = computed(() => {
    return domainLevel.value > 1;
  });

  return {
    currentDomain,
    maxSubdomainsLevel,
    domainLevel,
    isSubdomain,
    isCreateSubdomainEnabled,
    setCurrentDomain,
    setSubdomainLevel,
  };
});
