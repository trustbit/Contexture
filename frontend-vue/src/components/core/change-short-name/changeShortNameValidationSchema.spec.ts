import { describe, expect, test } from "vitest";
import {
  shortNameValidationSchema,
  boundedContextShortNameValidationSchema,
} from "~/components/core/change-short-name/changeShortNameValidationSchema";

describe("change short name validation rules", () => {
  test.each(["", null, undefined])("'%s' can not be null or empty", (shortName) => {
    const validation = shortNameValidationSchema("", []);

    const { success } = validation.safeParse(shortName);

    expect(success).toBeFalsy();
  });

  test("cannot exceed 50 characters", () => {
    const validation = shortNameValidationSchema("", []);

    const { success } = validation.safeParse("a".repeat(51));

    expect(success).toBeFalsy();
  });

  test("cannot contain whitespace", () => {
    const validation = shortNameValidationSchema("", []);

    const { success } = validation.safeParse("a b");

    expect(success).toBeFalsy();
  });

  test("cannot start with a number", () => {
    const validation = shortNameValidationSchema("", []);

    const { success } = validation.safeParse("1a");

    expect(success).toBeFalsy();
  });

  test("cannot start with hyphen", () => {
    const validation = shortNameValidationSchema("", []);

    const { success } = validation.safeParse("-a");

    expect(success).toBeFalsy();
  });

  test("cannot end with hyphen", () => {
    const validation = shortNameValidationSchema("", []);

    const { success } = validation.safeParse("a-");

    expect(success).toBeFalsy();
  });

  test("cannot contain characters other than alphanumeric and hyphen", () => {
    const validation = shortNameValidationSchema("", []);

    const { success } = validation.safeParse("a/b");

    expect(success).toBeFalsy();
  });

  test("cannot be the same as another domain", () => {
    const validation = shortNameValidationSchema("", [
      {
        id: "1",
        name: "Domain",
        shortName: "d",
        subdomains: [],
        boundedContexts: [],
      },
    ]);

    const { success } = validation.safeParse("d");

    expect(success).toBeFalsy();
  });

  test("cannot be the same as another domain (case insensitive)", () => {
    const validation = shortNameValidationSchema("", [
      {
        id: "1",
        name: "Domain",
        shortName: "d",
        subdomains: [],
        boundedContexts: [],
      },
    ]);

    const { success } = validation.safeParse("D");

    expect(success).toBeFalsy();
  });
});

describe("bounded context short name validation rules", () => {
  test("cannot be the same as another bounded context (case insensitive)", () => {
    const validation = boundedContextShortNameValidationSchema([
      {
        id: "2",
        shortName: "bc",
        name: "Bounded Context",
        parentDomainId: "1",
        classification: {},
        businessDecisions: [],
        namespaces: [],
        ubiquitousLanguage: {},
        domain: {
          id: "1",
          name: "Domain",
          subdomains: [],
          boundedContexts: [],
        },
      },
    ]);

    const { success } = validation.safeParse("BC");

    expect(success).toBeFalsy();
  });

  test("cannot be the same as another bounded context", () => {
    const validation = boundedContextShortNameValidationSchema([
      {
        id: "2",
        shortName: "bc",
        name: "Bounded Context",
        parentDomainId: "1",
        classification: {},
        businessDecisions: [],
        namespaces: [],
        ubiquitousLanguage: {},
        domain: {
          id: "1",
          name: "Domain",
          subdomains: [],
          boundedContexts: [],
        },
      },
    ]);

    const { success } = validation.safeParse("bc");

    expect(success).toBeFalsy();
  });
});
