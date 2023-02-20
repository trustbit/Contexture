<template>
  <ContextureModal
    :title="t('bounded_context_canvas.collaborators.define_modal.title')"
    :is-open="open"
    @close="onCloseDefineRelationship"
  >
    <div class="sm:w-[600px]">
      <p class="mt-2 border-b border-b-blue-100 pb-6">
        {{ t("bounded_context_canvas.collaborators.define_modal.description") }}
      </p>

      <ContextureHelpfulErrorAlert
        v-if="defineRelationshipError"
        :error="defineRelationshipError.error"
        :friendly-message="defineRelationshipError.friendlyMessage"
        :response="defineRelationshipError.response"
      />

      <Form class="flex flex-col gap-y-4 py-6" @submit="onSubmitRelationship">
        <ContextureListbox
          v-model="selectedOption"
          :display-value="(d) => d.name"
          key-prop="name"
          name="relationshipSelect"
          :options="selectOptions"
        />

        <div v-if="selectedOption">
          <div class="mb-4">
            <span class="border-l-2 border-l-blue-500 pl-2 text-sm text-gray-600">
              {{ selectedOption.description }}
            </span>
          </div>

          <div v-if="selectedOption.value === 'symmetric'" class="space-y-4">
            <ContextureRadioGroup
              v-model="relationship"
              :options="symmetricOptions"
              name="relationship"
              description-position="top"
            />
          </div>

          <div v-if="selectedOption.value === 'customer_supplier'" class="space-y-4">
            <ContextureRadioGroup
              v-model="relationship"
              :options="customerSupplierOptions"
              name="relationship"
              description-position="top"
            />
          </div>

          <div v-if="selectedOption.value === 'upstream'" class="space-y-4">
            <div class="flex">
              <ContextureRadioGroup
                v-model="upstreamDownstreamRelationship.relationship"
                :options="upstreamCollaborator"
                name="relationship_option_a"
                description="Describe the collaborator"
                class="w-1/2"
                description-position="top"
              />
              <ContextureRadioGroup
                v-model="upstreamDownstreamRelationship.collaborator"
                :options="upstreamCollaborationRelationship"
                name="relationship_option_b"
                description="Describe your relationship with the collaborator"
                class="w-1/2"
                description-position="top"
              />
            </div>
          </div>

          <div v-if="selectedOption?.value === 'downstream'" class="space-y-4">
            <div class="flex">
              <ContextureRadioGroup
                v-model="upstreamDownstreamRelationship.relationship"
                :options="downstreamCollaborator"
                name="relationship_option_a"
                description="Describe the collaborator"
                class="w-1/2"
                description-position="top"
              />
              <ContextureRadioGroup
                v-model="upstreamDownstreamRelationship.collaborator"
                :options="downstreamCollaborationRelationship"
                name="relationship_option_b"
                description="Describe your relationship with the collaborator"
                class="w-1/2"
                description-position="top"
              />
            </div>
          </div>
          <div class="mt-4">
            <ContexturePrimaryButton
              :label="t('bounded_context_canvas.collaborators.define_modal.submit')"
              type="submit"
            />
          </div>
        </div>
      </Form>
    </div>
  </ContextureModal>
</template>

<script setup lang="ts">
import { Form } from "vee-validate";
import { ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import ContextureHelpfulErrorAlert, {
  HelpfulErrorProps,
} from "~/components/primitives/alert/ContextureHelpfulErrorAlert.vue";
import ContexturePrimaryButton from "~/components/primitives/button/ContexturePrimaryButton.vue";
import ContextureListbox from "~/components/primitives/listbox/ContextureListbox.vue";
import ContextureModal from "~/components/primitives/modal/ContextureModal.vue";
import ContextureRadioGroup from "~/components/primitives/radio/ContextureRadioGroup.vue";
import {
  customerSupplierOptions,
  downstreamCollaborationRelationship,
  downstreamCollaborator,
  symmetricOptions,
  upstreamCollaborationRelationship,
  upstreamCollaborator,
} from "~/constants/collaborators";
import { useCollaborationsStore } from "~/stores/collaborations";
import { Collaboration, InitiatorRole, RelationshipType } from "~/types/collaboration";

interface SelectOption {
  name: string;
  value: "unknown" | "symmetric" | "customer_supplier" | "upstream" | "downstream";
  description: string;
}

interface Props {
  open: boolean;
  relationship: Collaboration;
}

interface Emits {
  (e: "close"): void;

  (e: "submit", value: RelationshipType | "unknown"): void;
}

const props = defineProps<Props>();
const emit = defineEmits<Emits>();
const { t } = useI18n();
const relationship = ref<"unknown" | RelationshipType | undefined>(props.relationship?.relationshipType);
const { refineRelationship } = useCollaborationsStore();
const selectedOption = ref<SelectOption>();
const defineRelationshipError = ref<HelpfulErrorProps>();
const upstreamDownstreamRelationship = ref({
  collaborator: undefined,
  relationship: undefined,
});

const selectOptions: SelectOption[] = [
  {
    name: "Unknown (?)",
    value: "unknown",
    description: "The exact description of the relationship unknown or you are not sure how to describe it.",
  },
  {
    name: "Symmetric",
    value: "symmetric",
    description: "The relationship between the collaborators is equal or symmetric.",
  },
  {
    name: "Customer / Supplier",
    value: "customer_supplier",
    description:
      "There is a cooperation with the collaborator that can be described as a customer/supplier relationship.",
  },
  {
    name: "Upstream",
    value: "upstream",
    description: "The collaborator is upstream and I depend on changes.",
  },
  {
    name: "Downstream",
    value: "downstream",
    description: "The collaborator is downstream and they depend on my changes.",
  },
];

watch(
  () => selectedOption.value?.value,
  (value) => {
    if (value === "unknown") {
      relationship.value = "unknown";
    } else {
      relationship.value = undefined;
    }
  }
);

function close() {
  emit("close");
  setTimeout(() => {
    selectedOption.value = undefined;
  }, 500);
}

function onCloseDefineRelationship() {
  close();
}

async function onSubmitRelationship() {
  defineRelationshipError.value = undefined;
  if (!selectedOption.value) {
    return;
  }

  let reqBody: RelationshipType | "unknown" | unknown;

  if (["unknown", "symmetric", "customer_supplier"].includes(selectedOption.value.value)) {
    reqBody = relationship.value;
  } else if (selectedOption.value.value === "downstream") {
    reqBody = {
      upstreamDownstream: {
        initiatorRole: InitiatorRole.Downstream,
        downstreamType: upstreamDownstreamRelationship.value.relationship,
        upstreamType: upstreamDownstreamRelationship.value.collaborator,
      },
    };
  } else if (selectedOption.value.value === "upstream") {
    reqBody = {
      upstreamDownstream: {
        initiatorRole: InitiatorRole.Upstream,
        downstreamType: upstreamDownstreamRelationship.value.collaborator,
        upstreamType: upstreamDownstreamRelationship.value.relationship,
      },
    };
  }

  const res = await refineRelationship(props.relationship.id, reqBody);

  if (res.error.value) {
    defineRelationshipError.value = {
      friendlyMessage: t("bounded_context_canvas.collaborators.error.refine"),
      error: res.error.value,
      response: res.data.value,
    };
  } else {
    close();
  }
}
</script>
