<template>
  <div class="flex flex-col gap-3">
    <div class="rounded bg-white p-4">
      <div
        class="flex w-full items-center rounded p-1"
        :class="[version === BoundedContextVersion.V4 ? 'bg-blue-50' : '']"
      >
        <Icon:materialSymbols:exclamation class="mr-1 h-4 w-4" />
        <span class="text-sm font-bold leading-4">{{ t("bounded_context_canvas.messages.commands.sent") }}</span>
      </div>
      <ul class="mt-2 space-y-2">
        <ContextureListItem v-for="message in messages?.commandsSent" :key="message">
          <template #title>
            <span class="text-sm font-bold text-gray-900">{{ message }}</span>
            <button @click="() => onDeleteMessage('commandsSent', message)">
              <span class="sr-only">{{ t("bounded_context_canvas.messages.commands.delete") }}</span>
              <Icon:material-symbols:delete-outline
                class="h-5 w-5 text-blue-500 hover:text-blue-600"
                aria-hidden="true"
              />
            </button>
          </template>
        </ContextureListItem>
      </ul>

      <ContextureCollapsable
        :label="t('common.add')"
        class="mt-3"
        :cancel-text="t('common.cancel')"
        :collapsed="collapsedState.commandsSent"
        data-testId="addCommandSent"
        @update:collapsed="(collapsed) => updateCollapsed('commandsSent', collapsed)"
      >
        <Form @submit="(name) => onAddMessage('commandsSent', name)">
          <ContextureInputText :label="t('common.name')" name="name" :required="true" :rules="requiredStringRule" />
          <ContexturePrimaryButton type="submit" size="sm" :label="t('bounded_context_canvas.messages.commands.add')">
            <template #left>
              <Icon:material-symbols:add class="mr-2" />
            </template>
          </ContexturePrimaryButton>
        </Form>
      </ContextureCollapsable>
    </div>

    <div class="rounded bg-white p-4">
      <div
        class="flex w-full items-center rounded p-1"
        :class="[version === BoundedContextVersion.V4 ? 'bg-orange-100' : '']"
      >
        <Icon:materialSymbols:flash-on class="mr-1 h-4 w-4" />
        <span class="text-sm font-bold leading-4">{{ t("bounded_context_canvas.messages.events.published") }}</span>
      </div>
      <ul class="mt-2 space-y-2">
        <ContextureListItem v-for="message in messages?.eventsPublished" :key="message">
          <template #title>
            <span class="text-sm font-bold text-gray-900">{{ message }}</span>
            <button @click="() => onDeleteMessage('eventsPublished', message)">
              <span class="sr-only">{{ t("bounded_context_canvas.messages.events.delete") }}</span>
              <Icon:material-symbols:delete-outline
                class="h-5 w-5 text-blue-500 hover:text-blue-600"
                aria-hidden="true"
              />
            </button>
          </template>
        </ContextureListItem>
      </ul>
      <ContextureCollapsable
        :label="t('common.add')"
        class="mt-3"
        :cancel-text="t('common.cancel')"
        :collapsed="collapsedState.eventsPublished"
        data-testId="addEventPublished"
        @update:collapsed="(collapsed) => updateCollapsed('eventsPublished', collapsed)"
      >
        <Form @submit="(name) => onAddMessage('eventsPublished', name)">
          <ContextureInputText :label="t('common.name')" name="name" :required="true" :rules="requiredStringRule" />
          <ContexturePrimaryButton type="submit" size="sm" :label="t('bounded_context_canvas.messages.events.add')">
            <template #left>
              <Icon:material-symbols:add class="mr-2" />
            </template>
          </ContexturePrimaryButton>
        </Form>
      </ContextureCollapsable>
    </div>

    <div class="rounded bg-white p-4">
      <div
        class="flex w-full items-center rounded p-1"
        :class="[version === BoundedContextVersion.V4 ? 'bg-green-50' : '']"
      >
        <Icon:materialSymbols:question-mark class="mr-1 h-4 w-4" />
        <span class="text-sm font-bold leading-4">{{ t("bounded_context_canvas.messages.queries.invoked") }}</span>
      </div>
      <ul class="mt-2 space-y-2">
        <ContextureListItem v-for="message in messages?.queriesInvoked" :key="message">
          <template #title>
            <span class="text-sm font-bold text-gray-900">{{ message }}</span>
            <button @click="() => onDeleteMessage('queriesInvoked', message)">
              <span class="sr-only">{{ t("bounded_context_canvas.messages.queries.delete") }}</span>
              <Icon:material-symbols:delete-outline
                class="h-5 w-5 text-blue-500 hover:text-blue-600"
                aria-hidden="true"
              />
            </button>
          </template>
        </ContextureListItem>
      </ul>
      <ContextureCollapsable
        :label="t('common.add')"
        class="mt-3"
        :cancel-text="t('common.cancel')"
        :collapsed="collapsedState.queriesInvoked"
        data-testId="addQueryInvoked"
        @update:collapsed="(collapsed) => updateCollapsed('queriesInvoked', collapsed)"
      >
        <Form @submit="(name) => onAddMessage('queriesInvoked', name)">
          <ContextureInputText :label="t('common.name')" name="name" :required="true" :rules="requiredStringRule" />
          <ContexturePrimaryButton type="submit" size="sm" :label="t('bounded_context_canvas.messages.queries.add')">
            <template #left>
              <Icon:material-symbols:add class="mr-2" />
            </template>
          </ContexturePrimaryButton>
        </Form>
      </ContextureCollapsable>
    </div>

    <ContextureHelpfulErrorAlert
      v-if="submitError"
      :error="submitError.error"
      :friendly-message="submitError.friendlyMessage"
      :response="submitError.response"
    />
  </div>
</template>

<script setup lang="ts">
import { storeToRefs } from "pinia";
import { Form } from "vee-validate";
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { BoundedContextVersion } from "~/components/bounded-context/canvas/layouts/version";
import ContextureHelpfulErrorAlert, {
  HelpfulErrorProps,
} from "~/components/primitives/alert/ContextureHelpfulErrorAlert.vue";
import ContexturePrimaryButton from "~/components/primitives/button/ContexturePrimaryButton.vue";
import ContextureCollapsable from "~/components/primitives/collapsable/ContextureCollapsable.vue";
import ContextureInputText from "~/components/primitives/input/ContextureInputText.vue";
import ContextureListItem from "~/components/primitives/list/ContextureListItem.vue";
import { requiredStringRule } from "~/core/validationRules";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import useConfirmationModalStore from "~/stores/confirmationModal";
import { CreateMessage, MessageProduceKeys, Messages } from "~/types/boundedContext";

interface Props {
  version?: BoundedContextVersion;
}

withDefaults(defineProps<Props>(), {
  version: BoundedContextVersion.V4,
});
const { t } = useI18n();
const confirmationModal = useConfirmationModalStore();
const store = useBoundedContextsStore();
const { activeBoundedContext } = storeToRefs(store);
const collapsedState = ref<{ [key in MessageProduceKeys]: boolean }>({
  commandsSent: true,
  queriesInvoked: true,
  eventsPublished: true,
});
const submitError = ref<HelpfulErrorProps>();

const messages = computed(() => activeBoundedContext.value.messages);

async function onAddMessage(key: MessageProduceKeys, message: CreateMessage) {
  const res = await store.addMessage(activeBoundedContext.value.id, key, message.name);

  if (res.error.value) {
    submitError.value = {
      friendlyMessage: t("bounded_context_canvas.messages.error.add"),
      error: res.error.value,
      response: res.data.value,
    };
  } else {
    updateCollapsed(key, true);
  }
}

async function onDeleteMessage(key: keyof Messages, message: string) {
  confirmationModal.open(
    t("bounded_context_canvas.messages.delete.confirm.title", {
      name: message,
    }),
    t("bounded_context_canvas.messages.delete.confirm.body"),
    t("bounded_context_canvas.messages.delete.confirm.confirm_button"),
    () => deleteMessage(key, message)
  );
}

async function deleteMessage(key: keyof Messages, message: string) {
  const res = await store.deleteMessage(activeBoundedContext.value.id, key, message);

  if (res.error.value) {
    submitError.value = {
      friendlyMessage: t("bounded_context_canvas.messages.error.delete"),
      error: res.error.value,
      response: res.data.value,
    };
  }
}

function updateCollapsed(key: MessageProduceKeys, collapsed: boolean) {
  collapsedState.value[key] = collapsed;
}
</script>
