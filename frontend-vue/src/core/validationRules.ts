import { toFieldValidator } from "@vee-validate/zod";
import * as zod from "zod";
import { i18n } from "~/main";

const { t } = i18n.global;

export const requiredStringRule = toFieldValidator(zod.string().min(1));

export const requiredObjectRule = toFieldValidator(zod.custom((v) => !!v, { message: t("common.required") }));
