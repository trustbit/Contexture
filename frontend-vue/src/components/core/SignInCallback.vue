<template>
  <div class="mt-6 flex flex-col items-center gap-y-1.5 text-gray-500">
    <p>Signing you in</p>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from "vue-router";
import { useAuthStore } from "~/stores/auth";
import { onMounted } from "vue";

const authStore = useAuthStore();
const router = useRouter();

onMounted(()=>{
  authStore.signinCallback()
    .then(() => {
        router.push("/")
    })
    .catch((err) => {
      console.error(err)
    })
})
</script>
