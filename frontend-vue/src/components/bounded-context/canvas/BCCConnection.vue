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
          <p class="font-bold">{{ t("bounded_context_canvas.collaborators.connection.label") }}</p>

          <form class="flex flex-col gap-3" autocomplete="off" @submit="onSubmit">
            <ContextureRadioGroup :options="collaboratorOptions" name="collaborator" />

            <div v-if="values.collaborator === 'domain' || values.collaborator === 'boundedContext'">
              <ContextureAutocomplete
                :label="t('bounded_context_canvas.collaborators.collaborator')"
                name="collaboratorRef"
                :display-value="(d) => `${d.name}${d.domain?.name ? ` (in ${d.domain.name})` : ''}`"
                :suggestions="collaboratorSuggestions"
                :placeholder="t('bounded_context_canvas.collaborators.collaborator.dropdown.placeholder')"
                :description="t('bounded_context_canvas.collaborators.collaborator.description')"
                @complete="searchSuggestions"
              />
            </div>
            <div v-else>
              <ContextureInputText
                :label="t('bounded_context_canvas.collaborators.collaborator')"
                name="collaboratorRef"
                :placeholder="t('bounded_context_canvas.collaborators.collaborator.input.placeholder')"
                :description="t('bounded_context_canvas.collaborators.collaborator.description')"
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
          </form>
        </div>
      </ContextureCollapsable>
      <ContextureHelpfulErrorAlert
        v-if="submitError"
        :error="submitError.error"
        :friendly-message="submitError.friendlyMessage"
        :response="submitError.response"
      />
    </div>

    <teleport to="body" v-if="selectedRelationship">
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
import { Form, useForm } from "vee-validate";
import { Ref, ref, watch } from "vue";
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
import { BoundedContext } from "~/types/boundedContext";
import { Collaboration, CollaborationId, collaboratorKeys, CollaboratorKeys } from "~/types/collaboration";
import { Domain } from "~/types/domain";

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

const collaboratorSuggestions: Ref<BoundedContext[] | Domain[]> = ref(boundedContexts.value);

const schema = zod.object({
  collaborator: zod.enum(["boundedContext", "domain", "externalSystem", "frontend"], {
    errorMap: () => {
      return { message: t("bounded_context_canvas.collaborators.connection.required") };
    },
  }),
  collaboratorRef: zod.union([zod.string().min(1), zod.object({})]),
  description: zod.string(),
});
const validationSchema = toFormValidator(schema);

interface CollaboratorFormValue {
  collaborator: CollaboratorKeys | unknown;
  collaboratorRef: string | BoundedContext | Domain;
  description: string;
}

const initialValues: CollaboratorFormValue = {
  collaborator: "",
  collaboratorRef: "",
  description: "",
};

const { values, handleSubmit, resetForm, setFieldValue } = useForm({
  validationSchema: validationSchema,
  initialValues: initialValues,
});

const onSubmit = handleSubmit((formValue: CollaboratorFormValue) => {
  createCollaborator(formValue);
  resetForm();
});

function searchSuggestions(query: string): void {
  if (values.collaborator === "domain") {
    collaboratorSuggestions.value =
      query === ""
        ? allDomains.value
        : allDomains.value.filter((option) => {
            return option.name.toLowerCase().includes(query.toLowerCase());
          });
  }
  if (values.collaborator === "boundedContext") {
    collaboratorSuggestions.value =
      query === ""
        ? boundedContexts.value
        : boundedContexts.value.filter((option) => {
            return option.name.toLowerCase().includes(query.toLowerCase());
          });
  }
}

async function createCollaborator(formValues: CollaboratorFormValue): Promise<void> {
  let collaborator: string;
  if (formValues.collaborator === "frontend" || formValues.collaborator === "externalSystem") {
    collaborator = formValues.collaboratorRef as string;
  } else {
    collaborator = (formValues.collaboratorRef as BoundedContext | Domain).id;
  }
  if (props.type === "initiator") {
    const res = await createInboundConnection({
      recipient: {
        boundedContext: activeBoundedContext.value?.id,
      },
      initiator: {
        [formValues.collaborator as string]: collaborator,
      },
      description: formValues.description,
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
        [formValues.collaborator as string]: collaborator,
      },
      initiator: {
        boundedContext: activeBoundedContext.value?.id,
      },
      description: formValues.description,
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
  () => values.collaborator,
  () => {
    setFieldValue("collaboratorRef", "");
    if (values.collaborator === "boundedContext") {
      collaboratorSuggestions.value = boundedContexts.value;
    }
    if (values.collaborator === "domain") {
      collaboratorSuggestions.value = allDomains.value;
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
