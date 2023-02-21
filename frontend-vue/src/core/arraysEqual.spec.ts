import { describe, expect, it } from "vitest";
import { arrayContentEqual } from "~/core/arrayContentEqual";

describe("arraysEqual", () => {
  it("arraysEqual empty objects are equal", () => {
    const equal = arrayContentEqual([], []);

    expect(equal).toBeTruthy();
  });

  it("arraysEqual string objects arq equal", () => {
    const equal = arrayContentEqual(["a"], ["a"]);

    expect(equal).toBeTruthy();
  });

  it("arraysEqual string objects are not equal", () => {
    const equal = arrayContentEqual(["a"], ["b"]);

    expect(equal).toBeFalsy();
  });

  it("arraysEqual complex objects are equal", () => {
    const equal = arrayContentEqual([{ a: "a", b: "b" }], [{ a: "a", b: "b" }]);

    expect(equal).toBeTruthy();
  });

  it("arraysEqual complex objects are not equal", () => {
    const equal = arrayContentEqual([{ a: "a", b: "b" }], [{ a: "c", b: "d" }]);

    expect(equal).toBeFalsy();
  });
});
