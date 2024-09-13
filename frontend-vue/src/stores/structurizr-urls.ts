import { defineStore, storeToRefs } from "pinia";
import { useDomainsStore } from "~/stores/domains";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import { useRouteParams } from "@vueuse/router";
import { computed, ComputedRef, onMounted, ref } from "vue";
import { Domain } from "~/types/domain";
import { BoundedContext } from "~/types/boundedContext";
import { useFetch } from "~/composables/useFetch";

interface StructurizrConfigEntry {
  id: number;
  key: string;
  secret: string;
}

type StructurizrConfig = Map<string, StructurizrConfigEntry>;

export const useStructurizrUrlsStore = defineStore("structurizr-urls", () => {
  const structurizerConfig = ref<StructurizrConfig>(new Map<string, StructurizrConfigEntry>());
  const loadConfig = async () => {
    const jsonFilePath = import.meta.env.VITE_CONTEXTURE_STRUCTURIZR_MAPPINGS_URL || `/structurizr-urls.json`;
    try {
      const {data, error} = await useFetch<StructurizrConfig>(jsonFilePath).get();

      if(error.value){
        console.warn(`Failed to fetch JSON config from ${jsonFilePath}`);
        structurizerConfig.value = new Map<string, StructurizrConfigEntry>();
      }
      else {
        if(data.value){
          console.log(data.value);
          structurizerConfig.value = new Map<string, StructurizrConfigEntry>(Object.entries(data.value));
        }
          
      }

    } catch (error) {
      console.error(`Error loading JSON config: ${error}`);
      structurizerConfig.value = new Map<string, StructurizrConfigEntry>();
    }
  };

  const { domainByDomainId, subdomainsByDomainId, parentDomains } = storeToRefs(useDomainsStore());
  const { activeBoundedContext, boundedContextsByDomainId } = storeToRefs(useBoundedContextsStore());
  const currentDomainId = useRouteParams<string>("id");
  const domain: ComputedRef<Domain | undefined> = computed<Domain | undefined>(
    () => domainByDomainId.value[currentDomainId.value]
  );

  const structurizrKey = computed(() => {
    let shortKeyPath: string = domain?.value?.shortName ?? activeBoundedContext.value?.shortName;
    let parent = domain.value?.parentDomainId;
    if (!domain.value) {
      const bc: BoundedContext = activeBoundedContext.value;
      shortKeyPath = `${bc?.shortName}`;
      parent = bc?.parentDomainId;
    }
    while (parent) {
      const parentDomain = domainByDomainId.value[parent];
      if (parentDomain && parentDomain?.shortName) shortKeyPath = `${parentDomain.shortName}-${shortKeyPath}`;
      parent = parentDomain?.parentDomainId;
    }

    return shortKeyPath?.toLowerCase();
  });
  const rootDomain = computed(() => {
    let rootDomain = undefined;

    let parent = domain.value?.parentDomainId ?? domain.value?.id;
    if (!domain.value) {
      const bc: BoundedContext = activeBoundedContext.value;
      parent = bc?.parentDomainId;
    }

    while (parent) {
      const parentDomain = domainByDomainId.value[parent];
      if (parentDomain && parentDomain?.shortName) rootDomain = parentDomain.shortName;
      parent = parentDomain?.parentDomainId;
    }
    return rootDomain?.toLowerCase();
  });
  const structurizrConfigExist = computed(() => rootDomain.value && structurizerConfig.value.has(rootDomain.value));
  const structurizrConfigEntry = computed<StructurizrConfigEntry | undefined>(() => {
    if (!structurizrConfigExist.value) return null;
    return structurizerConfig.value?.get(rootDomain.value);
  });

  const structurizrUrl = computed(() => {
    const config = structurizrConfigEntry.value;
    if (!structurizrConfigExist.value) return null;

    return `https://structurizr.com/embed/${config.id}?apiKey=${config.key}&diagram=${structurizrKey.value}&diagramSelector=false&iframe=${structurizrKey.value}`;
  });
  onMounted(loadConfig);
  return { structurizrUrl, structurizrKey, structurizrConfigExist };

});
