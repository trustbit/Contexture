<template>
  <div class="container mx-auto mt-4 px-4 pb-4 sm:px-0">
    <div class="mt-4 flex justify-center sm:mt-10" v-if="loading">{{ t("bounded_context_namespace.loading") }}</div>
    <div v-else-if="activeBoundedContext">
      <ContextureBreadcrumbs class="mb-4" />
      <ContextureNamespaces></ContextureNamespaces>
    </div>
  </div>
</template>

<script setup lang="ts">
import { storeToRefs } from "pinia";
import ContextureNamespaces from "~/components/bounded-context/namespace/ContextureNamespaces.vue";
import ContextureBreadcrumbs from "~/components/core/breadcrumbs/ContextureBreadcrumbs.vue";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
const { loading, activeBoundedContext } = storeToRefs(useBoundedContextsStore());
</script>
