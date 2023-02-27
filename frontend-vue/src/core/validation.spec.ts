import { describe, expect, it } from "vitest";
import { IssueData } from "zod";
import { contains, endsWith, isAlpha, isUniqueIn, startsWith, startsWithNumber } from "~/core/validation";

describe("validation", () => {
  describe("contains", () => {
    it("contains certain arg", () => {
      expect(contains("test new", " ")).toBeTruthy();
    });

    it("does not contain arg", () => {
      expect(contains("test", " ")).toBeFalsy();
    });
  });
  describe("startsWith", () => {
    it("starts with certain arg", () => {
      expect(startsWith("-test", "-")).toBeTruthy();
    });

    it("does not start with certain arg", () => {
      expect(startsWith("test", "-")).toBeFalsy();
    });
  });
  describe("endsWith", () => {
    it("ends with certain arg", () => {
      expect(endsWith("test-", "-")).toBeTruthy();
    });

    it("does not start with certain arg", () => {
      expect(endsWith("test", "-")).toBeFalsy();
    });
  });
  describe("startsWithNumber", () => {
    it("starts with number", () => {
      expect(startsWithNumber("1test")).toBeTruthy();
    });

    it("does not starts with number", () => {
      expect(startsWithNumber("tes1t1")).toBeFalsy();
    });
  });
  describe("isAlpha", () => {
    it("isAlpha with letter", () => {
      expect(isAlpha("A")).toBeTruthy();
    });

    it("isAlpha with number", () => {
      expect(isAlpha("1A")).toBeFalsy();
    });
  });
  describe("mustBeUnique", () => {
    it("unique in string array", () => {
      const issues: IssueData[] = [];

      const refinementCtx = {
        path: [],
        addIssue: (arg: IssueData) => issues.push(arg),
      };

      const result: boolean = isUniqueIn("A", refinementCtx, {
        in: ["A", "B", "C"],
        errorMessage: "Must be unique in array",
      });

      expect(result).toBeFalsy();
      expect(issues).length(1);
    });

    it("unique in complex object array", () => {
      const issues: IssueData[] = [];

      const refinementCtx = {
        path: [],
        addIssue: (arg: IssueData) => issues.push(arg),
      };

      const result: boolean = isUniqueIn<{ name: string }>("A", refinementCtx, {
        field: "name",
        in: [{ name: "A" }, { name: "B" }],
        errorMessage: "Must be unique in array",
      });

      expect(result).toBeFalsy();
      expect(issues).length(1);
    });

    it("unique in complex object", () => {
      const issues: IssueData[] = [];

      const refinementCtx = {
        path: [],
        addIssue: (arg: IssueData) => issues.push(arg),
      };

      const result: boolean = isUniqueIn<{ a: string; b: string }>("a", refinementCtx, {
        field: "a",
        in: {
          a: "test",
          b: "test",
        },
        errorMessage: "Must be unique in object",
      });

      expect(result).toBeFalsy();
      expect(issues).length(1);
    });

    it("unique in empty array", () => {
      const issues: IssueData[] = [];

      const refinementCtx = {
        path: [],
        addIssue: (arg: IssueData) => issues.push(arg),
      };

      const result: boolean = isUniqueIn("a", refinementCtx, {
        field: "a",
        in: [],
        errorMessage: "Must be unique in object",
      });

      expect(result).toBeTruthy();
      expect(issues).empty;
    });

    it("unique in empty object", () => {
      const issues: IssueData[] = [];

      const refinementCtx = {
        path: [],
        addIssue: (arg: IssueData) => issues.push(arg),
      };

      const result: boolean = isUniqueIn<{ a?: string }>("a", refinementCtx, {
        field: "a",
        in: {},
        errorMessage: "Must be unique in object",
      });

      expect(result).toBeTruthy();
      expect(issues).empty;
    });
  });
});
