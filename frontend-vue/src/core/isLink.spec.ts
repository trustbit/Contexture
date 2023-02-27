import { describe, expect, it } from "vitest";
import { isLink } from "~/core/isLink";

describe("isLink", () => {
  it("is http link", () => {
    expect(isLink("http://localhost:8080")).toBeTruthy();
  });

  it("is https link", () => {
    expect(isLink("https://localhost:8080")).toBeTruthy();
  });

  it("is no link", () => {
    expect(isLink("no-link")).toBeFalsy();
  });

  it("valid url but no link", () => {
    expect(isLink("javascript:void(0)")).toBeFalsy();
  });

  it("valid url but no protocol", () => {
    expect(isLink("www.trustbit.tech")).toBeFalsy();
  });
});
