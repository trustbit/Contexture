<template>
  <div>
    <div class="flex flex-col gap-y-2 rounded bg-gray-100 p-4">
      <div v-if="collaborations.length === 0">
        <span class="text-sm italic text-gray-700">({{ t("bounded_context_canvas.collaborators.empty") }})</span>
      </div>

      <div
        v-for="collaboration in collaborations"
        :key="collaboration.id"
        class="pb-2 text-xs hover:border-b hover:border-blue-100"
      >
        <div class="flex justify-between align-middle">
          <div data-testId="collaborationDisplay">
            <div v-if="collaboration[type].domain">
              <h5 class="font-bold">
                {{ domainByDomainId[collaboration[type].domain]?.name }}
              </h5>
              <span class="capitalize">{{ t("common.domain") }}</span>
            </div>
            <div v-if="collaboration[type].boundedContext">
              <h5 class="font-bold">
                {{ boundedContextsByBoundedContextId[collaboration[type].boundedContext]?.name }}
              </h5>
              <div>
                <span
                  >In Domain
                  {{ boundedContextsByBoundedContextId[collaboration[type].boundedContext]?.domain.name }}</span
                >
              </div>
            </div>
            <div v-if="collaboration[type].externalSystem">
              <h5 class="font-bold">
                {{ collaboration[type].externalSystem }}
              </h5>
              <span>{{ t("bounded_context_canvas.collaborators.external_system") }}</span>
            </div>
            <div v-if="collaboration[type].frontend">
              <h5 class="font-bold">
                {{ collaboration[type].frontend }}
              </h5>
              <span>{{ t("bounded_context_canvas.collaborators.frontend") }}</span>
            </div>
            <div>
              <p class="text-xs">
                {{ collaboration.relationshipType?.symmetric }}
              </p>
              <p v-if="collaboration.relationshipType === 'unknown'" class="text-xs">
                {{ t("bounded_context_canvas.collaborators.unknown_relationship") }}
              </p>
              <p
                v-if="
                  collaboration.relationshipType?.upstreamDownstream?.downstreamType ||
                  collaboration.relationshipType?.upstreamDownstream?.upstreamType
                "
                class="text-xs"
              >
                {{ collaboration.relationshipType?.upstreamDownstream.downstreamType }}/{{
                  collaboration.relationshipType?.upstreamDownstream.upstreamType
                }}
              </p>
              <p v-if="collaboration.relationshipType?.upstreamDownstream?.role" class="text-xs">
                {{ collaboration.relationshipType?.upstreamDownstream?.role }}
              </p>
              <span
                v-if="collaboration.relationshipType?.upstreamDownstream"
                class="border-l-2 border-black pl-1 text-xs font-bold"
                >Upstream/Downstream</span
              >
              <span
                v-if="collaboration.relationshipType?.symmetric"
                class="border-l-2 border-black pl-1 text-xs font-bold"
                >{{ t("bounded_context_canvas.collaborators.symmetric") }}</span
              >
            </div>

            <div class="mt-4 max-w-full">
              <span class="truncate text-xs">{{ collaboration.description }}</span>
            </div>
          </div>
          <div>
            <button @click="() => onDeleteCollaboration(collaboration.id)">
              <span class="sr-only">{{ t("bounded_context_canvas.collaborators.delete.label") }}</span>
              <icon:material-symbols:delete-outline class="h-5 w-5 text-blue-500" />
            </button>
          </div>
        </div>

        <ContextureWhiteButton
          :label="collaboration?.relationshipType ? 'redefine relationship' : 'define relationship'"
          size="sm"
          class="mt-2"
          @click="openDefineRelationship(collaboration)"
        >
          <template #left>
            <Icon:material-symbols:arrowForward class="mr-2" />
          </template>
        </ContextureWhiteButton>
      </div>

      <ContextureCollapsable
        :label="t('bounded_context_canvas.collaborators.actions.collapsed.add')"
        :cancel-text="t('common.cancel')"
        class="mt-8"
        :collapsed="addCollapsed"
        @update:collapsed="(collapsed) => (addCollapsed = collapsed)"
      >
        <div class="flex flex-col gap-3">
          <p class="text-base font-bold">{{ t("bounded_context_canvas.collaborators.add") }}</p>
          <p class="font-bold">{{ t("bounded_context_canvas.collaborators.select_connection") }}</p>

          <Form
            class="flex flex-col gap-3"
            :validation-schema="validationSchema"
            autocomplete="off"
            @submit="onCreateCollaborator"
          >
            <ContextureRadioGroup v-model="selectedCollaborator" :options="collaboratorOptions" name="collaborator" />

            <div v-if="selectedCollaborator === 'domain' || selectedCollaborator === 'boundedContext'">
              <ContextureAutocomplete
                v-model="collaboratorRef"
                :label="t('bounded_context_canvas.collaborators.collaborator')"
                name="collaboratorRef"
                :placeholder="t('bounded_context_canvas.collaborators.select_placeholder')"
                :display-value="(d) => d.name"
                :suggestions="suggestions"
                :description="t('bounded_context_canvas.collaborators.select_description')"
                @complete="searchSuggestions"
              />
            </div>
            <div v-else>
              <ContextureInputText
                v-model="collaboratorRef"
                :label="t('bounded_context_canvas.collaborators.collaborator')"
                name="collaboratorRef"
                description="The collaborator name that is used inside this bounded context."
              />
            </div>

            <ContextureTextarea
              :label="t('common.description')"
              description="Optional: Add a description of the collaboration"
              name="description"
            />

            <div>
              <ContexturePrimaryButton
                type="submit"
                :label="t('bounded_context_canvas.collaborators.add_connection')"
                size="sm"
              >
                <template #left>
                  <Icon:material-symbols:add class="mr-2" />
                </template>
              </ContexturePrimaryButton>
            </div>
          </Form>
        </div>
      </ContextureCollapsable>
      <ContextureHelpfulErrorAlert
        v-if="submitError"
        :error="submitError.error"
        :friendly-message="submitError.friendlyMessage"
        :response="submitError.response"
      />
    </div>

    <teleport to="body">
      <BCCDefineRelationshipModal
        :open="!!selectedRelationship"
        :relationship="selectedRelationship"
        @close="onCloseRedefineModal"
      />
    </teleport>
  </div>
</template>

<script setup lang="ts">
import { toFormValidator } from "@vee-validate/zod";
import { storeToRefs } from "pinia";
import { Form } from "vee-validate";
import { ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import * as zod from "zod";
import BCCDefineRelationshipModal from "~/components/bounded-context/canvas/BCCDefineRelationshipModal.vue";
import ContextureHelpfulErrorAlert, {
  HelpfulErrorProps,
} from "~/components/primitives/alert/ContextureHelpfulErrorAlert.vue";
import ContextureAutocomplete from "~/components/primitives/autocomplete/ContextureAutocomplete.vue";
import ContexturePrimaryButton from "~/components/primitives/button/ContexturePrimaryButton.vue";
import ContextureWhiteButton from "~/components/primitives/button/ContextureWhiteButton.vue";
import ContextureCollapsable from "~/components/primitives/collapsable/ContextureCollapsable.vue";
import ContextureInputText from "~/components/primitives/input/ContextureInputText.vue";
import ContextureTextarea from "~/components/primitives/input/ContextureTextarea.vue";
import ContextureRadioGroup from "~/components/primitives/radio/ContextureRadioGroup.vue";
import { collaboratorOptions } from "~/constants/collaborators";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import { useCollaborationsStore } from "~/stores/collaborations";
import useConfirmationModalStore from "~/stores/confirmationModal";
import { useDomainsStore } from "~/stores/domains";
import { Collaboration, CollaborationId, CollaboratorKeys } from "~/types/collaboration";

interface Props {
  collaborations: Collaboration[];
  type: "initiator" | "recipient";
}

const props = defineProps<Props>();

const { t } = useI18n();
const confirmationModal = useConfirmationModalStore();
const { allDomains, domainByDomainId } = storeToRefs(useDomainsStore());
const { activeBoundedContext, boundedContexts, boundedContextsByBoundedContextId } = storeToRefs(
  useBoundedContextsStore()
);
const { createInboundConnection, createOutboundConnection, deleteCollaborationById } = useCollaborationsStore();
const addCollapsed = ref(true);
const submitError = ref<HelpfulErrorProps>();

const suggestions = ref();
const selectedCollaborator = ref<CollaboratorKeys>();
const collaboratorRef = ref();
const validationSchema = toFormValidator(
  zod.object({
    collaborator: zod.string(),
    collaboratorRef: zod.custom((data) => data, { message: "Required" }),
    description: zod.string().nullish(),
  })
);

function searchSuggestions(query: string): void {
  if (selectedCollaborator.value === "domain") {
    suggestions.value =
      query === ""
        ? allDomains.value
        : allDomains.value.filter((option) => {
            return option.name.toLowerCase().includes(query.toLowerCase());
          });
  }
  if (selectedCollaborator.value === "boundedContext") {
    suggestions.value =
      query === ""
        ? boundedContexts.value
        : boundedContexts.value.filter((option) => {
            return option.name.toLowerCase().includes(query.toLowerCase());
          });
  }
}

async function onCreateCollaborator(collaborator: any): Promise<void> {
  let ref: string;
  if (selectedCollaborator.value === "frontend" || selectedCollaborator.value === "externalSystem") {
    ref = collaborator.collaboratorRef;
  } else {
    ref = collaborator.collaboratorRef.id;
  }
  if (props.type === "initiator") {
    const res = await createInboundConnection({
      recipient: {
        boundedContext: activeBoundedContext.value?.id,
      },
      initiator: {
        [selectedCollaborator.value as string]: ref,
      },
      description: collaborator.description,
    });

    if (res.error.value) {
      submitError.value = {
        friendlyMessage: t("bounded_context_canvas.collaborators.error.add"),
        error: res.error.value,
        response: res.data.value,
      };
    } else {
      addCollapsed.value = true;
    }
  } else {
    const res = await createOutboundConnection({
      recipient: {
        [selectedCollaborator.value as string]: ref,
      },
      initiator: {
        boundedContext: activeBoundedContext.value?.id,
      },
      description: collaborator.description,
    });

    if (res.error.value) {
      submitError.value = {
        friendlyMessage: t("bounded_context_canvas.collaborators.error.add"),
        error: res.error.value,
        response: res.data.value,
      };
    } else {
      addCollapsed.value = true;
    }
  }
}

async function onDeleteCollaboration(collaborationId: CollaborationId) {
  confirmationModal.open(
    t("bounded_context_canvas.collaborators.delete.confirm.title"),
    t("bounded_context_canvas.collaborators.delete.confirm.body"),
    t("bounded_context_canvas.collaborators.delete.confirm.confirm_button"),
    () => deleteCollaboration(collaborationId)
  );
}

async function deleteCollaboration(id: CollaborationId) {
  const res = await deleteCollaborationById(id);

  if (res.error.value) {
    submitError.value = {
      friendlyMessage: t("bounded_context_canvas.collaborators.error.delete"),
      error: res.error.value,
      response: res.data.value,
    };
  }
}

watch(
  () => selectedCollaborator.value,
  () => {
    collaboratorRef.value = null;
    suggestions.value = [];
    if (selectedCollaborator.value === "boundedContext") {
      suggestions.value = boundedContexts.value;
    }
    if (selectedCollaborator.value === "domain") {
      suggestions.value = allDomains.value;
    }
  }
);

const selectedRelationship = ref<Collaboration | undefined>();

function openDefineRelationship(collaboration: Collaboration) {
  selectedRelationship.value = collaboration;
}

function onCloseRedefineModal() {
  selectedRelationship.value = undefined;
}
</script>
