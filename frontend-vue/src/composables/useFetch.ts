import { AfterFetchContext, BeforeFetchContext, createFetch } from "@vueuse/core";
import { useAuthStore } from "~/stores/auth";

export const useFetch = createFetch({
  baseUrl: import.meta.env.VITE_CONTEXTURE_API_BASE_URL,
  fetchOptions: {
    redirect: "follow",
  },

  options: {
    async beforeFetch(ctx: BeforeFetchContext) {
      const authStore = useAuthStore();
      if (authStore.enabled) {
        const accessToken = await authStore.getAccessToken();
        const headers = {
          ...ctx.options.headers,
          Authorization: `Bearer ${accessToken}`,
        };

        ctx.options.headers = headers;
      }

      return ctx;
    },
    async afterFetch(ctx: AfterFetchContext) {
      return {
        response: ctx.response,
        data: typeof ctx.data === "object" ? ctx.data : JSON.parse(ctx.data),
      };
    },
    async onFetchError(ctx: { data: any; response: Response | null; error: any }) {
      console.warn(ctx);
      return ctx;
    },
  },
});
