import { defineStore } from "pinia";
import { useFetch } from "~/composables/useFetch";
import { EventLogEntry } from "~/types/event-log";

export const useEventLogsStore = defineStore("event-logs", () => {
  async function fetchEventLogs(id: string) {
    const { data, error } = await useFetch<EventLogEntry[]>(`/api/event-log/${id}`).get();
    if (error.value) {
      console.error("Error fetching event logs:", error.value);
    }

    return data.value || [];
  }

  return {
    fetchEventLogs,
  };
});
