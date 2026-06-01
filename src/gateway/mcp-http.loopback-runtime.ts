type McpLoopbackRuntime = {
  port: number;
  ownerToken: string;
  nonOwnerToken: string;
};

export const MCP_LOOPBACK_TOKEN_ENV_VAR = "OPENCLAW_MCP_TOKEN";

let activeRuntime: McpLoopbackRuntime | undefined;

export function getActiveMcpLoopbackRuntime(): McpLoopbackRuntime | undefined {
  return activeRuntime ? { ...activeRuntime } : undefined;
}

export function setActiveMcpLoopbackRuntime(runtime: McpLoopbackRuntime): void {
  activeRuntime = { ...runtime };
}

export function resolveMcpLoopbackBearerToken(
  runtime: McpLoopbackRuntime,
  senderIsOwner: boolean,
): string {
  return senderIsOwner ? runtime.ownerToken : runtime.nonOwnerToken;
}

export function clearActiveMcpLoopbackRuntimeByOwnerToken(ownerToken: string): void {
  if (activeRuntime?.ownerToken === ownerToken) {
    activeRuntime = undefined;
  }
}

export function createUserFacingMcpLoopbackServerDetails(runtime: McpLoopbackRuntime) {
  return {
    port: runtime.port,
    url: `http://127.0.0.1:${runtime.port}/mcp`,
    token: resolveMcpLoopbackBearerToken(runtime, true),
    tokenEnvVar: MCP_LOOPBACK_TOKEN_ENV_VAR,
    config: {
      mcpServers: {
        openclaw: {
          type: "http",
          url: `http://127.0.0.1:${runtime.port}/mcp`,
          headers: {
            Authorization: `Bearer \${${MCP_LOOPBACK_TOKEN_ENV_VAR}}`,
          },
        },
      },
    },
  };
}

export function createMcpLoopbackServerConfig(port: number) {
  return {
    mcpServers: {
      openclaw: {
        type: "http",
        url: `http://127.0.0.1:${port}/mcp`,
        headers: {
          Authorization: `Bearer \${${MCP_LOOPBACK_TOKEN_ENV_VAR}}`,
          "x-session-key": "${OPENCLAW_MCP_SESSION_KEY}",
          "x-openclaw-agent-id": "${OPENCLAW_MCP_AGENT_ID}",
          "x-openclaw-account-id": "${OPENCLAW_MCP_ACCOUNT_ID}",
          "x-openclaw-message-channel": "${OPENCLAW_MCP_MESSAGE_CHANNEL}",
          "x-openclaw-current-channel-id": "${OPENCLAW_MCP_CURRENT_CHANNEL_ID}",
          "x-openclaw-current-thread-ts": "${OPENCLAW_MCP_CURRENT_THREAD_TS}",
          "x-openclaw-current-message-id": "${OPENCLAW_MCP_CURRENT_MESSAGE_ID}",
          "x-openclaw-inbound-event-kind": "${OPENCLAW_MCP_INBOUND_EVENT_KIND}",
          "x-openclaw-source-reply-delivery-mode": "${OPENCLAW_MCP_SOURCE_REPLY_DELIVERY_MODE}",
        },
      },
    },
  };
}
