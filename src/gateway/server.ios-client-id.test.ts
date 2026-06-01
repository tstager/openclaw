import { describe, expect, test } from "vitest";
import {
  GATEWAY_CLIENT_IDS,
  GATEWAY_CLIENT_MODES,
} from "../../packages/gateway-protocol/src/client-info.js";
import { validateConnectParams } from "../../packages/gateway-protocol/src/index.js";

function makeConnectParams(clientId: string) {
  return {
    minProtocol: 1,
    maxProtocol: 1,
    client: {
      id: clientId,
      version: "dev",
      platform: "ios",
      mode: GATEWAY_CLIENT_MODES.NODE,
    },
    role: "node",
    scopes: [],
    caps: ["canvas"],
    commands: ["system.notify"],
    permissions: {},
  };
}

describe("connect params client id validation", () => {
  test.each([GATEWAY_CLIENT_IDS.IOS_APP, GATEWAY_CLIENT_IDS.ANDROID_APP])(
    "accepts %s as a valid gateway client id",
    (clientId) => {
      const ok = validateConnectParams(makeConnectParams(clientId));
      expect(ok).toBe(true);
      expect(validateConnectParams.errors ?? []).toHaveLength(0);
    },
  );

  test("accepts Windows companion as a valid UI client", () => {
    const ok = validateConnectParams({
      ...makeConnectParams(GATEWAY_CLIENT_IDS.WINDOWS_APP),
      client: {
        ...makeConnectParams(GATEWAY_CLIENT_IDS.WINDOWS_APP).client,
        platform: "windows",
        mode: GATEWAY_CLIENT_MODES.UI,
      },
      role: "operator",
      scopes: ["operator.read"],
      caps: [],
      commands: [],
      permissions: {},
    });

    expect(ok).toBe(true);
    expect(validateConnectParams.errors ?? []).toHaveLength(0);
  });

  test("accepts Windows companion as a valid node client", () => {
    const ok = validateConnectParams({
      ...makeConnectParams(GATEWAY_CLIENT_IDS.WINDOWS_APP),
      client: {
        ...makeConnectParams(GATEWAY_CLIENT_IDS.WINDOWS_APP).client,
        platform: "windows",
        mode: GATEWAY_CLIENT_MODES.NODE,
      },
      role: "node",
      scopes: [],
      caps: ["canvas", "screen", "camera"],
      commands: ["canvas.a2ui.pushJSONL", "screen.record", "camera.snap"],
      permissions: {
        "canvas.a2ui": true,
        "screen.record": true,
        "camera.capture": true,
      },
    });

    expect(ok).toBe(true);
    expect(validateConnectParams.errors ?? []).toHaveLength(0);
  });

  test("rejects unknown client ids", () => {
    const ok = validateConnectParams(makeConnectParams("openclaw-mobile"));
    expect(ok).toBe(false);
  });
});
