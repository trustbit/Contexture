import { toFieldValidator } from "@vee-validate/zod";
import * as zod from "zod";

export const requiredRule = toFieldValidator(zod.string().min(1));
