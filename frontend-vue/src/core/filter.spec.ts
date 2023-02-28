import { describe, expect, it } from "vitest";
import { filter } from "~/core/filter";

describe("filter", () => {
  it("search in object property string", () => {
    const actualFound = filter({ a: "hit", b: "nothing" }, "hit");

    expect(actualFound).toBeTruthy();
  });

  it("search in object property array", () => {
    const actualFound = filter({ a: [{ a: "hit" }], b: [{ a: "nothing" }] }, "hit");

    expect(actualFound).toBeTruthy();
  });

  it("search in object property string not found", () => {
    const actualFound = filter({ a: "nothing", b: "nothing" }, "hit");

    expect(actualFound).toBeFalsy();
  });

  it("search in object property array not found", () => {
    const actualFound = filter({ a: [{ a: "nothing" }], b: [{ a: "nothing" }] }, "hit");

    expect(actualFound).toBeFalsy();
  });

  it("search in object array limit by key", () => {
    const actualFound = filter({ a: [{ a: "hit" }], b: [{ a: "nothing" }] }, "hit", "b");

    expect(actualFound).toBeFalsy();
  });

  it("search in object string limit by key", () => {
    const actualFound = filter({ a: "hit", b: "nothing" }, "hit", "b");

    expect(actualFound).toBeFalsy();
  });

});
