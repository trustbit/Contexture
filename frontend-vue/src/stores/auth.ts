import { UserManager, UserManagerSettings, WebStorageStateStore } from "oidc-client-ts";
import { defineStore } from "pinia";
import { Ref, computed, inject, onMounted, ref } from "vue";
import { AfterFetchContext, createFetch } from "@vueuse/core";

export interface UserInfo {
  authenticated: boolean;
  name?: string;
  permissions: string[];
}

interface OidcConfiguration {
  securityType: "oidc";
  authority: string;
  clientId: string;
  clientSecret: string;
}

interface SecuirtyDisabled {
  securityType: "disabled";
}

type SecurityConfiguration = OidcConfiguration | SecuirtyDisabled;

const unauthenticatedUser: UserInfo = {
  authenticated: false,
  permissions: [],
};

const useFetch = createFetch({
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

export async function getSecurityConfiguration() {
  const { data } = await useFetch<SecurityConfiguration>("/meta/securityConfiguration").get();
  return data.value;
}

export const useAuthStore = defineStore("auth", () => {
  const user: Ref<UserInfo> = ref(unauthenticatedUser);
  const enabled: Ref<boolean> = ref(false);

  const securityConfiguration = inject<SecurityConfiguration>("securityConfiguration");

  let userManager: UserManager;

  if (securityConfiguration) {
    if (securityConfiguration.securityType === "oidc") {
      const settings: UserManagerSettings = {
        authority: securityConfiguration.authority,
        client_id: securityConfiguration.clientId,
        client_secret: securityConfiguration.clientSecret,
        redirect_uri: new URL("signinCallback", window.location.origin).href,
        post_logout_redirect_uri: window.location.origin,
        response_type: "code",
        scope: "openid profile email",
        userStore: new WebStorageStateStore({ store: localStorage }),
      };
      userManager = new UserManager(settings);
      enabled.value = true;
    }
  }

  function setUserInfo(u: UserInfo | null) {
    if (u) user.value = u;
    else
      user.value = {
        authenticated: false,
        name: "",
        permissions: [],
      };
  }

  function signinRedirect() {
    return userManager.signinRedirect();
  }

  async function signinCallback() {
    await userManager.signinCallback();
    await fetchUserInfo();
  }

  async function signinSilent() {
    await userManager.signinSilent();
    await fetchUserInfo();
  }

  async function signoutRedirect() {
    await userManager.signoutRedirect();
    await fetchUserInfo();
  }

  async function getAccessToken() {
    const u = await userManager.getUser();
    return u?.access_token;
  }

  async function fetchUserInfo() {
    const user = await userManager.getUser();
    
    const { data } = await useFetch<{permissions: string[]}>("/meta/userPermissions", {
      headers: {
        Authorization: `Bearer ${user?.access_token}`,
      },
    }).get();

    if (data.value) {
      const { permissions } = data.value
      if (user) {
        setUserInfo({
          authenticated: true,
          name: user.profile.name,
          permissions: permissions
        });
      }
      else {
        setUserInfo({
          authenticated: false,
          permissions: permissions
        });
      }
    }
  }

  const canModify = computed(() => !enabled.value || user.value.permissions.includes("modify"));

  onMounted(async () => {
    if (enabled.value) await fetchUserInfo();
  });

  return {
    signinRedirect,
    signinCallback,
    signinSilent,
    getAccessToken,
    signoutRedirect,
    user,
    enabled,
    canModify,
  };
});
