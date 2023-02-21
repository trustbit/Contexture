<template>
  <ContextureBoundedContextCanvasElement
    :title="t('bounded_context_canvas.strategic_classification.title')"
    :title-icon="icon"
    :is-editable="true"
    :edit-mode="editMode"
    @close="reset"
    @open="editMode = true"
  >
    <ContextureHelpfulErrorAlert v-if="submitError" v-bind="submitError" class="mb-4" />
    <div class="mt-2 flex gap-x-8">
      <div class="flex flex-col min-w-fit">
        <div class="mb-4 flex capitalize">
          {{ t("common.domain") }}
          <ContextureTooltip
            content="How important is this context to the success of your organisation?"
            placement="top"
          >
            <Icon:materialSymbols:info-outline class="ml-1 h-5 w-5 text-gray-500"></Icon:materialSymbols:info-outline>
          </ContextureTooltip>
        </div>
        <ContextureRadio
          v-for="value in domainTypes"
          :key="value.id"
          v-model="classification.domainType"
          name="domainType"
          :value="value.value"
          :label="value.label"
          :disabled="!editMode"
        />
      </div>
      <div class="flex flex-col min-w-fit">
        <div class="mb-4 flex">
          {{ t("bounded_context_canvas.strategic_classification.business_model") }}
          <ContextureTooltip content="What role does the context play in your business model?" placement="top">
            <Icon:materialSymbols:info-outline class="ml-1 h-5 w-5 text-gray-500"></Icon:materialSymbols:info-outline>
          </ContextureTooltip>
        </div>
        <ContextureCheckbox
          v-for="value in businessModels"
          :key="value.id"
          v-model="classification.businessModel"
          name="businessModel"
          :value="value.value"
          :label="value.label"
          :disabled="!editMode"
        />
      </div>
      <div class="flex flex-col min-w-fit">
        <div class="mb-4 flex">
          {{ t("bounded_context_canvas.strategic_classification.evolution") }}
          <ContextureTooltip content="How evolved is the concept (see Wardley Maps)" placement="top">
            <Icon:materialSymbols:info-outline class="ml-1 h-5 w-5 text-gray-500"></Icon:materialSymbols:info-outline>
          </ContextureTooltip>
        </div>
        <ContextureRadio
          v-for="value in evolutions"
          :key="value.id"
          v-model="classification.evolution"
          :disabled="!editMode"
          name="evolution"
          :value="value.value"
          :label="value.label"
        />
      </div>
    </div>

    <ContextureAccordionItem :title="t('bounded_context_canvas.strategic_classification.help_me_decide')">
      <template #default>
        <div>
          <p class="font-bold">{{ t("bounded_context_canvas.strategic_classification.domain") }}</p>
          <ul>
            <li>
              {{ t("bounded_context_canvas.strategic_classification.core") }}:
              <span class="font-light">{{
                t("bounded_context_canvas.strategic_classification.core_description")
              }}</span>
            </li>
            <li>
              {{ t("bounded_context_canvas.strategic_classification.supporting") }}:
              <span class="font-light">{{
                t("bounded_context_canvas.strategic_classification.supporting_description")
              }}</span>
            </li>
            <li>
              {{ t("bounded_context_canvas.strategic_classification.generic") }}:
              <span class="font-light">{{
                t("bounded_context_canvas.strategic_classification.generic_description")
              }}</span>
            </li>
          </ul>
        </div>

        <div class="mt-4">
          <p class="font-bold">{{ t("bounded_context_canvas.strategic_classification.business_model") }}</p>
          <ul>
            <li>
              {{ t("bounded_context_canvas.strategic_classification.revenue") }}:
              <span class="font-light">{{
                t("bounded_context_canvas.strategic_classification.revenue_description")
              }}</span>
            </li>
            <li>
              {{ t("bounded_context_canvas.strategic_classification.engagement") }}:
              <span class="font-light">{{
                t("bounded_context_canvas.strategic_classification.engagement_description")
              }}</span>
            </li>
            <li>
              {{ t("bounded_context_canvas.strategic_classification.compliance") }}:
              <span class="font-light">{{
                t("bounded_context_canvas.strategic_classification.compliance_description")
              }}</span>
            </li>
            <li>
              {{ t("bounded_context_canvas.strategic_classification.cost_reduction") }}:
              <span class="font-light">{{
                t("bounded_context_canvas.strategic_classification.cost_reduction_description")
              }}</span>
            </li>
          </ul>
        </div>

        <div class="mt-4">
          <p class="font-bold">{{ t("bounded_context_canvas.strategic_classification.evolution") }}</p>
          <ul>
            <li>
              {{ t("bounded_context_canvas.strategic_classification.genesis") }}:
              <span class="font-light">{{
                t("bounded_context_canvas.strategic_classification.genesis_description")
              }}</span>
            </li>
            <li>
              {{ t("bounded_context_canvas.strategic_classification.custom_built") }}::
              <span class="font-light">{{
                t("bounded_context_canvas.strategic_classification.custom_built_description")
              }}</span>
            </li>
            <li>
              {{ t("bounded_context_canvas.strategic_classification.product") }}::
              <span class="font-light">{{
                t("bounded_context_canvas.strategic_classification.product_description")
              }}</span>
            </li>
            <li>
              {{ t("bounded_context_canvas.strategic_classification.commodity") }}:
              <span class="font-light">{{
                t("bounded_context_canvas.strategic_classification.commodity_description")
              }}</span>
            </li>
          </ul>
        </div>
      </template>
    </ContextureAccordionItem>

    <div class="mt-4 flex align-middle">
      <ContexturePrimaryButton v-if="editMode" :label="t('common.save')" size="sm" @click="onSave">
        <template #left>
          <Icon:material-symbols:check class="mr-1" />
        </template>
      </ContexturePrimaryButton>
      <ContextureWhiteButton v-if="editMode" :label="t('common.cancel')" size="sm" class="ml-2" @click="reset">
        <template #left>
          <Icon:material-symbols:close class="mr-1" />
        </template>
      </ContextureWhiteButton>
    </div>
  </ContextureBoundedContextCanvasElement>
</template>

<script setup lang="ts">
import { storeToRefs } from "pinia";
import { ref } from "vue";
import { useI18n } from "vue-i18n";
import ContextureAccordionItem from "~/components/primitives/accordion/ContextureAccordionItem.vue";
import ContexturePrimaryButton from "~/components/primitives/button/ContexturePrimaryButton.vue";
import ContextureWhiteButton from "~/components/primitives/button/ContextureWhiteButton.vue";
import ContextureBoundedContextCanvasElement from "~/components/bounded-context/canvas/ContextureBoundedContextCanvasElement.vue";
import ContextureCheckbox from "~/components/primitives/checkbox/ContextureCheckbox.vue";
import ContextureHelpfulErrorAlert, {
  HelpfulErrorProps,
} from "~/components/primitives/alert/ContextureHelpfulErrorAlert.vue";
import ContextureRadio from "~/components/primitives/radio/ContextureRadio.vue";
import ContextureTooltip from "~/components/primitives/tooltip/ContextureTooltip.vue";
import { useBoundedContextsStore } from "~/stores/boundedContexts";
import { BusinessModel, Classification, DomainType, Evolution } from "~/types/boundedContext";
import IconsMaterialSymbolsFormatTag from "~icons/material-symbols/tag";

const icon = IconsMaterialSymbolsFormatTag;
const { t } = useI18n();
const store = useBoundedContextsStore();
const { activeBoundedContext } = storeToRefs(store);
const { reclassify } = store;
const classification = ref<Classification>({
  evolution: activeBoundedContext.value.classification?.evolution,
  businessModel: activeBoundedContext.value.classification?.businessModel,
  domainType: activeBoundedContext.value.classification?.domainType,
});
const submitError = ref<HelpfulErrorProps | undefined>();
const editMode = ref(false);

const domainTypes = [
  {
    id: "domain-type-core",
    value: DomainType.Core,
    label: "Core",
  },
  {
    id: "domain-type-supporting",
    value: DomainType.Supporting,
    label: "Supporting",
  },
  {
    id: "domain-type-generic",
    value: DomainType.Generic,
    label: "Generic",
  },
];
const businessModels = [
  {
    id: "business-model-revenue",
    value: BusinessModel.Revenue,
    label: "Revenue",
  },
  {
    id: "business-model-engagement",
    value: BusinessModel.Engagement,
    label: "Engagement",
  },
  {
    id: "business-model-compliance",
    value: BusinessModel.Compliance,
    label: "Compliance",
  },
  {
    id: "business-model-cost-reduction",
    value: BusinessModel.CostReduction,
    label: "Cost Reduction",
  },
];
const evolutions = [
  {
    id: "evolution-genesis",
    value: Evolution.Genesis,
    label: "Genesis",
  },
  {
    id: "evolution-custom-built",
    value: Evolution.CustomBuilt,
    label: "Custom Built",
  },
  {
    id: "evolution-product",
    value: Evolution.Product,
    label: "Product",
  },
  {
    id: "evolution-cost-commodity",
    value: Evolution.Commodity,
    label: "Commodity",
  },
];

async function onSave() {
  submitError.value = undefined;
  const res = await reclassify(activeBoundedContext.value.id, classification.value);

  if (res.error.value) {
    submitError.value = {
      friendlyMessage: t("bounded_context_canvas.strategic_classification.error.update"),
      error: res.error.value,
      response: res.data.value,
    };
  } else {
    editMode.value = false;
  }
}

function reset() {
  submitError.value = undefined;
  editMode.value = false;
  classification.value = {
    evolution: activeBoundedContext.value?.classification.evolution,
    businessModel: activeBoundedContext.value?.classification.businessModel,
    domainType: activeBoundedContext.value?.classification.domainType,
  };
}
</script>
