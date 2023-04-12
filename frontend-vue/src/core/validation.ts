import { RefinementCtx } from "zod";
import * as zod from "zod";

export const contains = (term: string, arg: string) => term.includes(arg);
export const startsWith = (term: string, arg: string) => term.startsWith(arg);
export const endsWith = (term: string, arg: string) => term.endsWith(arg);
export const startsWithNumber = (term: string) => term.match(/^\d/);
export const isAlpha = (term: string) => term.match(/^[a-zA-Z]+$/);

export const isUniqueIn = <T>(
  term: string,
  ctx: RefinementCtx,
  params: { field?: keyof T; in: T | T[] | undefined; errorMessage: string }
) => {
  const alreadyExists = checkExistenceIn(term, params);

  if (alreadyExists) {
    ctx.addIssue({
      code: zod.ZodIssueCode.custom,
      message: params.errorMessage,
    });
    return false;
  }

  return true;
};

function checkExistenceIn(arg: string, params: { field?: any; in: any | any[] }): boolean {
  if (!params.in) {
    return false;
  }
  if (Array.isArray(params.in)) {
    if (params.field) {
      return !!params.in.find((toFind) => toFind[params.field]?.toLowerCase() === arg.toLowerCase());
    } else {
      return !!params.in.find((toFind) => toFind.toLowerCase() === arg.toLowerCase());
    }
  }
  return !!Object.keys(params.in).find((key) => key.toLowerCase() === arg.toLowerCase());
}
