<template>
  <nav :aria-label="t('breadcrumbs.accessibility.aria_label')">
    <ol class="flex flex-wrap sm:gap-x-2">
      <li class="inline-flex items-center text-xs text-gray-800">
        <RouterLink to="/" class="inline-flex items-center hover:text-gray-600 hover:underline">
          <Icon:material-symbols:home-outline aria-hidden="true" class="mr-1.5 text-gray-800" />
          <span>{{ t("breadcrumbs.domains") }} </span>
        </RouterLink>
        <div v-if="breadcrumbs.length >= 1" aria-hidden="true" class="ml-2">
          <Icon:material-symbols:chevron-right class="text-sm" />
        </div>
      </li>
      <li
        v-for="(breadcrumb, index) of breadcrumbs"
        :key="`${breadcrumb.text}-${index}`"
        class="inline-flex items-center text-xs text-gray-800"
      >
        <RouterLink
          v-if="breadcrumb.type === BreadcrumbType.DOMAIN"
          :aria-current="index === breadcrumbs.length - 1 ? t('breadcrumbs.accessibility.aria_current') : null"
          :to="`/domain/${breadcrumb.id}`"
          class="inline-flex items-center hover:text-gray-600 hover:underline"
        >
          <Icon:material-symbols:flip-to-front aria-hidden="true" class="mr-1.5 text-blue-500" />
          <span
            >{{ t("breadcrumbs.domain") }} <strong>{{ breadcrumb.text }}</strong></span
          >
        </RouterLink>
        <RouterLink
          v-if="breadcrumb.type === BreadcrumbType.SUBDOMAIN"
          :aria-current="index === breadcrumbs.length - 1 ? t('breadcrumbs.accessibility.aria_current') : null"
          :to="`/domain/${breadcrumb.id}`"
          class="inline-flex items-center hover:text-gray-600 hover:underline"
        >
          <Icon:material-symbols:flip-to-back aria-hidden="true" class="mr-1.5 text-purple-500" />
          <span
            >{{ t("breadcrumbs.sub_domain") }} <strong>{{ breadcrumb.text }}</strong></span
          >
        </RouterLink>
        <RouterLink
          v-if="breadcrumb.type === BreadcrumbType.BOUNDED_CONTEXT"
          :aria-current="index === breadcrumbs.length - 1 ? t('breadcrumbs.accessibility.aria_current') : null"
          :to="`/boundedContext/${breadcrumb.id}/canvas`"
          class="inline-flex items-center hover:text-gray-600 hover:underline"
        >
          <Icon:material-symbols:select-all aria-hidden="true" class="mr-1.5 text-yellow-500" />
          <span
            >{{ t("breadcrumbs.bounded_context") }} <strong>{{ breadcrumb.text }}</strong></span
          >
        </RouterLink>
        <div v-if="index < breadcrumbs.length - 1" aria-hidden="true" class="ml-2">
          <Icon:material-symbols:chevron-right class="text-sm" />
        </div>
      </li>
    </ol>
  </nav>
</template>

<script lang="ts" setup>
import { storeToRefs } from "pinia";
import { ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute } from "vue-router";
import { Breadcrumb } from "./breadcrumbs";
import { BreadcrumbType, buildBreadcrumbs } from "./breadcrumbs";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import { useDomainsStore } from "~/stores/domains";

const route = useRoute();
const { t } = useI18n();
const { allDomains } = storeToRefs(useDomainsStore());
const { boundedContexts } = storeToRefs(useBoundedContextsStore());

const breadcrumbs = ref<Breadcrumb[]>(
  buildBreadcrumbs(route.params.id as string, route.name as string, allDomains.value, boundedContexts.value)
);

watch([() => route.params.id, () => allDomains.value, () => boundedContexts.value], () => {
  breadcrumbs.value = buildBreadcrumbs(
    route.params.id as string,
    route.name as string,
    allDomains.value,
    boundedContexts.value
  );
});
</script>
