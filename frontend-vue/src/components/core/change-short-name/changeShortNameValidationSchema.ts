import * as zod from "zod";
import { RefinementCtx } from "zod";
import { contains, endsWith, isUniqueIn, startsWith, startsWithNumber } from "~/core";
import { BoundedContext } from "~/types/boundedContext";
import { Domain } from "~/types/domain";

const allowedCharacters: string = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-";

export const shortNameValidationSchema = (
  currentShortName: string | undefined,
  domains: Domain[],
  boundedContexts: BoundedContext[]
) =>
  zod
    .string()
    .min(1)
    .max(16)
    .superRefine((arg: string, ctx: RefinementCtx) => {
      isUniqueIn<Domain>(arg, ctx, {
        in: domains.filter((d) => d.shortName !== currentShortName),
        field: "shortName",
        errorMessage: `The short name '${arg}' is already in use by domain '${mapDomain(arg, domains)}'`,
      });
    })
    .superRefine((arg: string, ctx: RefinementCtx) => {
      isUniqueIn<BoundedContext>(arg, ctx, {
        in: boundedContexts.filter((bc) => bc.shortName !== currentShortName),
        field: "shortName",
        errorMessage: `The short name '${arg}' is already in use by bounded context '${mapBoundedContext(
          arg,
          boundedContexts
        )}'`,
      });
    })
    .refine((term: string) => !startsWithNumber(term), {
      message: "Must not start with a number",
    })
    .refine((term: string) => !contains(term, " "), {
      message: "Must not contain whitespace",
    })
    .refine((term: string) => !startsWith(term, "-"), {
      message: "Must not start with hyphen",
    })
    .refine((term: string) => !endsWith(term, "-"), { message: "Must not end with hyphen" })
    .refine((term: string) => term.split("").every((c) => allowedCharacters.includes(c)), {
      message: "Must only contain alphanumeric characters and hyphens",
    });

function mapDomain(prop: string, domains: Domain[]): string {
  const domain = domains.find((d) => d.shortName?.toUpperCase() === prop.toUpperCase());
  return `${domain?.name}-${domain?.shortName}`;
}

function mapBoundedContext(prop: string, boundedContexts: BoundedContext[]): string {
  const bc = boundedContexts.find((bc) => bc.shortName?.toUpperCase() === prop.toUpperCase());
  return `${bc?.name}-${bc?.shortName}`;
}
