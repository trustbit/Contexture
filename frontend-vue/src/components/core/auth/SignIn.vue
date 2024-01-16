<template>
  <div v-if="enabled && !user.authenticated">
    <button @click="signIn">Sign in</button>
  </div>
  <div v-if="enabled && user.authenticated" class="space-x-4">
    <label>{{ user.name }}</label>
    <button @click="signOut">Sign out</button>
  </div>
</template>

<script setup lang="ts">
import { storeToRefs } from "pinia";
import { useAuthStore } from "~/stores/auth";

const authStore = useAuthStore();
const { enabled, user } = storeToRefs(authStore);

function signIn() {
  return authStore.signinRedirect();
}

function signOut() {
  return authStore.signoutRedirect();
}
</script>
