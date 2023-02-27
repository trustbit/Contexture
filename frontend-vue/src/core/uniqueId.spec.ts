import { describe, expect, it } from "vitest";
import { uniqueId } from "~/core/uniqueId";

describe("uniqueId", () => {
  it("unique id without prefix", () => {
    expect(uniqueId()).toBe("contexture_id_1");
  });
  it("unique id with prefix", () => {
    expect(uniqueId("prefix_")).toBe("prefix_2");
  });
});
