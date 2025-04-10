<template>
  <ol class="mt-3 divide-y divide-gray-200 dark:divide-gray-700">
    <li v-for="(event, index) in paginatedEvents" :key="index">
      <div class="flex flex-col p-3">
        <div class="flex items-center justify-between gap-6 text-base font-normal">
          <span class="font-medium text-blue-500">{{ translateEventTypeName(event.eventType) }}</span>
          <FormattedDate :date="event.timestamp"></FormattedDate>
        </div>
        <template v-if="event.eventType === 'BoundedContextImported'">
          <PropertyValue :name="t('event_log.boundedcontext.name')" :value="event.eventData.name"></PropertyValue>
          <PropertyValue
            :name="t('event_log.boundedcontext.shortname')"
            :value="event.eventData.shortName"
          ></PropertyValue>
        </template>
        <template v-if="event.eventType === 'BoundedContextCreated'">
          <PropertyValue :name="t('event_log.boundedcontext.name')" :value="event.eventData.name"></PropertyValue>
        </template>
        <template v-if="event.eventType === 'BoundedContextRenamed'">
          <ValueDiff :previousValue="event.eventData.previousName" :newValue="event.eventData.name"></ValueDiff>
        </template>
        <template v-if="event.eventType === 'ShortNameAssigned'">
          <ValueDiff
            :previousValue="event.eventData.previousShortName"
            :newValue="event.eventData.shortName"
          ></ValueDiff>
        </template>
        <template v-if="event.eventType === 'BoundedContextMovedToDomain'">
          <ValueDiff
            :previousValue="event.eventData.previousDomainName"
            :newValue="event.eventData.domainName"
          ></ValueDiff>
        </template>
        <template v-if="event.eventType === 'DomainImported'">
          <PropertyValue :name="t('event_log.domain.name')" :value="event.eventData.name"></PropertyValue>
          <PropertyValue :name="t('event_log.domain.shortname')" :value="event.eventData.name"></PropertyValue>
        </template>
        <template v-if="event.eventType === 'DomainCreated'">
          <PropertyValue :name="t('event_log.domain.name')" :value="event.eventData.name"></PropertyValue>
        </template>
        <template v-if="event.eventType === 'SubDomainCreated'">
          <PropertyValue :name="t('event_log.domain.name')" :value="event.eventData.name"></PropertyValue>
        </template>
        <template v-if="event.eventType === 'DomainRenamed'">
          <ValueDiff :previousValue="event.eventData.previousName" :newValue="event.eventData.name"></ValueDiff>
        </template>
      </div>
    </li>
  </ol>
  <div class="mt-4 flex justify-between">
    <button @click="prevPage" :disabled="currentPage === 1">Previous</button>
    <span>Page {{ currentPage }} of {{ totalPages }}</span>
    <button @click="nextPage" :disabled="currentPage === totalPages">Next</button>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { EventLogEntry } from "~/types/event-log";
import ValueDiff from "./ValueDiff.vue";
import PropertyValue from "./PropertyValue.vue";
import FormattedDate from "./FormattedDate.vue";

const { events } = defineProps<{ events: EventLogEntry[] }>();
const { t } = useI18n();
const translateEventTypeName = (eventType: string) => t(`event_log.eventype.${eventType.toLowerCase()}`);
const itemsPerPage = 5;
const currentPage = ref(1);

const paginatedEvents = computed(() => {
  const start = (currentPage.value - 1) * itemsPerPage;
  const end = start + itemsPerPage;
  return events.slice(start, end);
});

const totalPages = computed(() => {
  return Math.ceil(events.length / itemsPerPage);
});

const prevPage = () => {
  if (currentPage.value > 1) {
    currentPage.value--;
  }
};

const nextPage = () => {
  if (currentPage.value < totalPages.value) {
    currentPage.value++;
  }
};
</script>
