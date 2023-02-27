import { AfterFetchContext, createFetch } from "@vueuse/core";

export const useFetch = createFetch({
  baseUrl: import.meta.env.VITE_CONTEXTURE_API_BASE_URL,
  fetchOptions: {
    redirect: "follow",
  },

  options: {
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
