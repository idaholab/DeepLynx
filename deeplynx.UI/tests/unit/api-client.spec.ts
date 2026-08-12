import { expect, test } from "@playwright/test";
import { AxiosError } from "axios";
import api from "@/app/lib/client_service/api";

const originalAuthDisabled =
  process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION;
const originalFetch = globalThis.fetch;
const originalBroadcastChannel = globalThis.BroadcastChannel;

test.afterEach(() => {
  if (originalAuthDisabled === undefined) {
    delete process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION;
  } else {
    process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION =
      originalAuthDisabled;
  }
  globalThis.fetch = originalFetch;
  Object.defineProperty(globalThis, "BroadcastChannel", {
    configurable: true,
    writable: true,
    value: originalBroadcastChannel,
  });
});

test("adds the session token and deduplicates concurrent session requests", async () => {
  process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION = "false";
  Object.defineProperty(globalThis, "BroadcastChannel", {
    configurable: true,
    writable: true,
    value: undefined,
  });

  let sessionRequests = 0;
  let releaseSession: () => void = () => {};
  const sessionGate = new Promise<void>((resolve) => {
    releaseSession = resolve;
  });

  globalThis.fetch = async () => {
    sessionRequests += 1;
    await sessionGate;
    return new Response(
      JSON.stringify({ tokens: { access_token: "test-access-token" } }),
      { status: 200, headers: { "Content-Type": "application/json" } },
    );
  };

  const authorizationHeaders: Array<string | undefined> = [];
  const adapter = async (config: Parameters<NonNullable<typeof api.defaults.adapter>>[0]) => {
    authorizationHeaders.push(config.headers.get("Authorization")?.toString());
    return {
      data: null,
      status: 200,
      statusText: "OK",
      headers: {},
      config,
    };
  };

  const firstRequest = api.get("/first", { adapter });
  const secondRequest = api.get("/second", { adapter });

  await expect.poll(() => sessionRequests).toBe(1);
  releaseSession();
  await Promise.all([firstRequest, secondRequest]);

  expect(authorizationHeaders).toEqual([
    "Bearer test-access-token",
    "Bearer test-access-token",
  ]);
});

test("skips session lookup when frontend authentication is disabled", async () => {
  process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION = "true";
  let sessionRequests = 0;
  globalThis.fetch = async () => {
    sessionRequests += 1;
    throw new Error("Session lookup should not run");
  };

  let authorization: string | undefined;
  await api.get("/auth-disabled", {
    adapter: async (config) => {
      authorization = config.headers.get("Authorization")?.toString();
      return {
        data: null,
        status: 200,
        statusText: "OK",
        headers: {},
        config,
      };
    },
  });

  expect(sessionRequests).toBe(0);
  expect(authorization).toBeUndefined();
});

test("surfaces RFC 7807 details through the shared error interceptor", async () => {
  process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION = "true";

  const request = api.get("/problem", {
    adapter: async (config) => {
      throw new AxiosError("Request failed", "ERR_BAD_REQUEST", config, null, {
        data: {
          title: "Bad Request",
          detail: "The v2 request payload is invalid.",
        },
        status: 400,
        statusText: "Bad Request",
        headers: { "content-type": "application/problem+json" },
        config,
      });
    },
  });

  await expect(request).rejects.toThrow("The v2 request payload is invalid.");
});
