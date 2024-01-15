/// <reference types="vitest" />

import VueI18n from "@intlify/unplugin-vue-i18n/vite";
import path from "path";
import IconsResolver from "unplugin-icons/resolver";
import Icons from "unplugin-icons/vite";
import Components from "unplugin-vue-components/vite";
import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";

export default defineConfig({
  server: {
    proxy: {
      "/api": {
        target: "http://localhost:3000",
        changeOrigin: true,
        secure: false,
      },
    },
  },
  resolve: {
    alias: {
      "~/": `${path.resolve(__dirname, "src")}/`,
    },
  },
  plugins: [
    vue({
      reactivityTransform: true,
      template: {
        compilerOptions: {
          isCustomElement: (tag) =>
            ["bubble-visualization", "hierarchical-edge", "visualization-sunburst"].includes(tag),
        },
      },
    }),

    // https://github.com/antfu/vite-plugin-components
    Components({
      dts: true,
      dirs: [],
      resolvers: [
        IconsResolver({
          prefix: "icon",
        }),
      ],
    }),

    // https://github.com/intlify/bundle-tools/tree/main/packages/unplugin-vue-i18n
    VueI18n({
      runtimeOnly: true,
      compositionOnly: true,
      fullInstall: true,
      include: [path.resolve(__dirname, "locales/**")],
    }),

    Icons({
      compiler: "vue3",
    }),
  ],

  // https://github.com/vitest-dev/vitest
  test: {
    environment: "jsdom",
  },
});
