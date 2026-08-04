import { expect, test } from "@playwright/test";
import {
  appendNexusApiPath,
  getDefaultNexusApiVersion,
  getNexusApiBaseUrl,
  getNexusScalarUrl,
  withNexusApiVersion,
} from "@/app/lib/api-version";

const originalVersion = process.env.NEXT_PUBLIC_API_VERSION;

test.afterEach(() => {
  if (originalVersion === undefined) {
    delete process.env.NEXT_PUBLIC_API_VERSION;
  } else {
    process.env.NEXT_PUBLIC_API_VERSION = originalVersion;
  }
});

test("falls back to v1 when the configured version is missing or empty", () => {
  delete process.env.NEXT_PUBLIC_API_VERSION;
  expect(getDefaultNexusApiVersion()).toBe("v1");

  process.env.NEXT_PUBLIC_API_VERSION = "";
  expect(getDefaultNexusApiVersion()).toBe("v1");
});

test("uses and normalizes the deployment-wide configured version", () => {
  process.env.NEXT_PUBLIC_API_VERSION = "V12";

  expect(getDefaultNexusApiVersion()).toBe("v12");
  expect(withNexusApiVersion("http://localhost:5095/api")).toBe(
    "http://localhost:5095/api/v12",
  );
});

test("rejects malformed API versions", () => {
  for (const invalidVersion of ["1", "v0", "version2", "v-1"]) {
    process.env.NEXT_PUBLIC_API_VERSION = invalidVersion;
    expect(() => getDefaultNexusApiVersion()).toThrow(
      'expected "v" followed by a positive integer',
    );
  }
});

test("normalizes API bases without duplicating the api segment", () => {
  process.env.NEXT_PUBLIC_API_VERSION = "v2";

  for (const baseUrl of [
    "http://localhost:5095",
    "http://localhost:5095/",
    "http://localhost:5095/api",
    "http://localhost:5095/api/",
    "http://localhost:5095/api/v1",
  ]) {
    expect(getNexusApiBaseUrl(baseUrl)).toBe("http://localhost:5095/api");
    expect(withNexusApiVersion(baseUrl)).toBe(
      "http://localhost:5095/api/v2",
    );
  }
});

test("builds Scalar and OAuth URLs with the configured version", () => {
  process.env.NEXT_PUBLIC_API_VERSION = "v2";
  const baseUrl = "http://localhost:5095/api";

  expect(getNexusScalarUrl(baseUrl)).toBe(
    "http://localhost:5095/api/scalar/v2",
  );
  expect(
    appendNexusApiPath(withNexusApiVersion(baseUrl), "/oauth/authorize"),
  ).toBe("http://localhost:5095/api/v2/oauth/authorize");
});
