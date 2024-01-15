<template>
  <ContextureAccordionItem :default-open="true">
    <template #title>
      <div class="flex items-center justify-between">
        <div>
          {{ namespace.name }}
        </div>
        <div class="space-x-4">
          <ContextureTextLinkButton @click.prevent="onDeleteNamespace" v-if="canModify">
            <template #left>
              <icon:material-symbols:delete-outline-rounded :aria-label="t('common.delete')" class="h-5 w-5" />
            </template>
          </ContextureTextLinkButton>

          <ContextureTextLinkButton v-if="canModify && !editMode" @click.prevent="onOpenEditMode">
            <template #left>
              <icon:material-symbols:drive-file-rename-outline-outline :aria-label="t('common.edit')" class="h-5 w-5" />
            </template>
          </ContextureTextLinkButton>
          <ContextureTextLinkButton v-if="editMode" @click.prevent="onCloseEditMode">
            <template #left>
              <icon:material-symbols:close :aria-label="t('common.close')" class="h-5 w-5" />
            </template>
          </ContextureTextLinkButton>
        </div>
      </div>
    </template>

    <template #default>
      <div v-if="showEmptyMessage" class="mt-4 text-sm italic">
        {{ t("bounded_context_namespace.labels.empty") }}
      </div>

      <div class="pt-5">
        <ul class="space-y-4">
          <ContextureListItem :text="label.value" v-for="(label, key) of namespace.labels" :key="`${label},${key}`">
            <template #title>
              <span class="text-sm font-bold text-gray-900">{{ label.name }}</span>
              <button @click="() => onDeleteLabel(label)">
                <Icon:material-symbols:delete-outline
                  v-if="editMode"
                  class="h-5 w-5 text-blue-500 hover:text-blue-600"
                />
              </button>
            </template>
          </ContextureListItem>
        </ul>
      </div>

      <div class="mt-2">
        <div class="flex items-center" v-for="(newLabel, index) of newLabels" :key="`newLabel-${index}`">
          <ContextureInputText
            v-model="newLabel.name"
            :name="`newLabelName-${index}`"
            :placeholder="t('common.name')"
            :skip-validation="true"
          ></ContextureInputText>
          <ContextureInputText
            v-model="newLabel.value"
            :name="`newLabelValue-${index}`"
            class="ml-2 grow"
            :placeholder="t('common.value')"
            :skip-validation="true"
          ></ContextureInputText>
          <div class="ml-4">
            <ContextureTextLinkButton @click.prevent="() => onDeleteNewLabel(index)">
              <template #left>
                <icon:material-symbols:delete-outline-rounded :aria-label="t('common.delete')" class="h-5 w-5" />
              </template>
            </ContextureTextLinkButton>
          </div>
        </div>
      </div>

      <div class="mt-4 flex justify-between border-t border-blue-100" v-if="editMode">
        <ContextureWhiteButton
          :label="t('bounded_context_namespace.button.add_another_label')"
          class="mt-4"
          size="sm"
          @click="addNewLabel"
        >
          <template #left>
            <Icon:material-symbols:add class="mr-2" />
          </template>
        </ContextureWhiteButton>
        <div>
          <ContextureWhiteButton :label="t('common.cancel')" class="mt-4" size="sm" @click="onCloseEditMode">
          </ContextureWhiteButton>
          <ContexturePrimaryButton :label="t('common.save')" class="mt-4" size="sm" @click="onSave">
            <template #left>
              <Icon:material-symbols:check class="mr-2" />
            </template>
          </ContexturePrimaryButton>
        </div>
      </div>
    </template>
  </ContextureAccordionItem>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import ContextureAccordionItem from "~/components/primitives/accordion/ContextureAccordionItem.vue";
import ContexturePrimaryButton from "~/components/primitives/button/ContexturePrimaryButton.vue";
import ContextureTextLinkButton from "~/components/primitives/button/ContextureTextLinkButton.vue";
import ContextureWhiteButton from "~/components/primitives/button/ContextureWhiteButton.vue";
import ContextureInputText from "~/components/primitives/input/ContextureInputText.vue";
import ContextureListItem from "~/components/primitives/list/ContextureListItem.vue";
import { useAuthStore } from "~/stores/auth";
import { CreateNamespaceLabel, Namespace, NamespaceLabel } from "~/types/namespace";

interface Props {
  namespace: Namespace;
}

interface Emits {
  (e: "save", namespaces: CreateNamespaceLabel[]): void;

  (e: "deleteNamespace", namespace: Namespace): void;

  (e: "deleteLabel", label: NamespaceLabel): void;
}

const props = defineProps<Props>();
const emit = defineEmits<Emits>();
const { t } = useI18n();
const { canModify } = useAuthStore()
const newLabels = ref<CreateNamespaceLabel[]>([]);
const editMode = ref(false);
const showEmptyMessage = computed(() => props.namespace.labels.length === 0 && !editMode.value);

function addNewLabel() {
  newLabels.value = [...newLabels.value, { name: "" }];
}

function onSave() {
  emit("save", newLabels.value);
  closeEditMode();
}

function onDeleteNamespace() {
  emit("deleteNamespace", props.namespace);
}

function onDeleteLabel(label: NamespaceLabel) {
  emit("deleteLabel", label);
}

function onDeleteNewLabel(index: number) {
  newLabels.value.splice(index, 1);
}

function onOpenEditMode() {
  if (!props.namespace.labels || props.namespace.labels.length === 0) {
    newLabels.value = [{ name: "", value: "" }];
  }
  editMode.value = true;
}

function closeEditMode() {
  editMode.value = false;
  newLabels.value = [];
}

function onCloseEditMode() {
  closeEditMode();
}
</script>
