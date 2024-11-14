import * as zod from "zod";
import { RefinementCtx } from "zod";
import { contains, endsWith, isUniqueIn, startsWith, startsWithNumber } from "~/core";
import { BoundedContext } from "~/types/boundedContext";
import { Domain } from "~/types/domain";

const allowedCharacters: string = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-";

export const shortNameValidationSchema = (currentShortName: string | undefined, domains: Domain[]) =>
  zod
    .string()
    .min(1)
    .max(50)
    .superRefine((arg: string, ctx: RefinementCtx) => {
      isUniqueIn<Domain>(arg, ctx, {
        in: domains.filter((d) => d.shortName !== currentShortName),
        field: "shortName",
        errorMessage: `The short name '${arg}' is already in use by domain '${mapDomain(arg, domains)}'`,
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

export const boundedContextShortNameValidationSchema = (boundedContexts: BoundedContext[]) =>
  zod
    .string()
    .min(1)
    .max(50)
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
    })
    .refine(
      (term: string) => {
        const bc = boundedContexts.find((bc) => bc.shortName?.toLocaleLowerCase() === term?.toLocaleLowerCase());
        if (bc) return false;
        else return true;
      },
      (term: string) => {
        const bc = boundedContexts.find((bc) => bc.shortName?.toLocaleLowerCase() === term?.toLocaleLowerCase());
        return {
          message: `The short name '${term}' is already in use by bounded context '${bc?.name}-${bc?.shortName}'`,
        };
      }
    );
