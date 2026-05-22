import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { Command } from "commander";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { withTempHome } from "../config/home-env.test-harness.js";
import { registerMcpCli } from "./mcp-cli.js";

const mocks = vi.hoisted(() => {
  const runtime = {
    log: vi.fn(),
    error: vi.fn(),
    exit: vi.fn((code: number) => {
      throw new Error(`__exit__:${code}`);
    }),
    writeJson: vi.fn((value: unknown, space = 2) => {
      runtime.log(JSON.stringify(value, null, space > 0 ? space : undefined));
    }),
  };
  return {
    runtime,
    serveOpenClawChannelMcp: vi.fn(),
    ensureMcpLoopbackServer: vi.fn(),
    closeMcpLoopbackServer: vi.fn(),
    getActiveMcpLoopbackRuntime: vi.fn(),
    createUserFacingMcpLoopbackServerDetails: vi.fn(),
  };
});

const defaultRuntime = mocks.runtime;
const mockLog = defaultRuntime.log;
const mockError = defaultRuntime.error;
const serveOpenClawChannelMcp = mocks.serveOpenClawChannelMcp;
const ensureMcpLoopbackServer = mocks.ensureMcpLoopbackServer;
const closeMcpLoopbackServer = mocks.closeMcpLoopbackServer;
const getActiveMcpLoopbackRuntime = mocks.getActiveMcpLoopbackRuntime;
const createUserFacingMcpLoopbackServerDetails = mocks.createUserFacingMcpLoopbackServerDetails;

vi.mock("../runtime.js", () => ({
  defaultRuntime: mocks.runtime,
}));

vi.mock("../mcp/channel-server.js", () => ({
  serveOpenClawChannelMcp: mocks.serveOpenClawChannelMcp,
}));

vi.mock("../gateway/mcp-http.js", () => ({
  ensureMcpLoopbackServer: mocks.ensureMcpLoopbackServer,
  closeMcpLoopbackServer: mocks.closeMcpLoopbackServer,
  getActiveMcpLoopbackRuntime: mocks.getActiveMcpLoopbackRuntime,
  createUserFacingMcpLoopbackServerDetails: mocks.createUserFacingMcpLoopbackServerDetails,
}));

const tempDirs: string[] = [];

async function createWorkspace(): Promise<string> {
  const dir = await fs.mkdtemp(path.join(os.tmpdir(), "openclaw-cli-mcp-"));
  tempDirs.push(dir);
  return dir;
}

let sharedProgram: Command;

async function runMcpCommand(args: string[]) {
  await sharedProgram.parseAsync(args, { from: "user" });
}

function lastLogLine(): string {
  return lastRuntimeLine(mockLog);
}

function lastErrorLine(): string {
  return lastRuntimeLine(mockError);
}

function lastRuntimeLine(mock: typeof mockLog): string {
  const call = mock.mock.calls[mock.mock.calls.length - 1];
  return String(call?.[0] ?? "");
}

describe("mcp cli", () => {
  if (!sharedProgram) {
    sharedProgram = new Command();
    sharedProgram.exitOverride();
    registerMcpCli(sharedProgram);
  }

  beforeEach(() => {
    vi.clearAllMocks();
    ensureMcpLoopbackServer.mockResolvedValue({ port: 23119 });
    closeMcpLoopbackServer.mockResolvedValue(undefined);
    getActiveMcpLoopbackRuntime.mockReturnValue({
      port: 23119,
      ownerToken: "owner-token",
      nonOwnerToken: "non-owner-token",
    });
    createUserFacingMcpLoopbackServerDetails.mockReturnValue({
      port: 23119,
      url: "http://127.0.0.1:23119/mcp",
      token: "owner-token",
      tokenEnvVar: "OPENCLAW_MCP_TOKEN",
      config: {
        mcpServers: {
          openclaw: {
            type: "http",
            url: "http://127.0.0.1:23119/mcp",
            headers: { Authorization: "Bearer ${OPENCLAW_MCP_TOKEN}" },
          },
        },
      },
    });
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    await Promise.all(
      tempDirs.splice(0).map((dir) => fs.rm(dir, { recursive: true, force: true })),
    );
  });

  it("sets and shows a configured MCP server", async () => {
    await withTempHome("openclaw-cli-mcp-home-", async (home) => {
      const workspaceDir = await createWorkspace();
      const configPath = path.join(home, ".openclaw", "openclaw.json");
      vi.spyOn(process, "cwd").mockReturnValue(workspaceDir);

      await runMcpCommand(["mcp", "set", "context7", '{"command":"uvx","args":["context7-mcp"]}']);
      expect(lastLogLine()).toBe(`Saved MCP server "context7" to ${configPath}.`);

      mockLog.mockClear();
      await runMcpCommand(["mcp", "show", "context7", "--json"]);
      expect(JSON.parse(lastLogLine())).toEqual({ command: "uvx", args: ["context7-mcp"] });
    });
  });

  it("fails when removing an unknown MCP server", async () => {
    await withTempHome("openclaw-cli-mcp-home-", async (home) => {
      const workspaceDir = await createWorkspace();
      const configPath = path.join(home, ".openclaw", "openclaw.json");
      vi.spyOn(process, "cwd").mockReturnValue(workspaceDir);

      await expect(runMcpCommand(["mcp", "unset", "missing"])).rejects.toThrow("__exit__:1");
      expect(lastErrorLine()).toBe(
        `No MCP server named "missing" in ${configPath}. Run openclaw mcp list to see configured servers.`,
      );
    });
  });

  it("starts the channel bridge with parsed serve options", async () => {
    await withTempHome("openclaw-cli-mcp-home-", async () => {
      const workspaceDir = await createWorkspace();
      const tokenFile = path.join(workspaceDir, "gateway.token");
      vi.spyOn(process, "cwd").mockReturnValue(workspaceDir);
      await fs.writeFile(tokenFile, "secret-token\n", "utf-8");

      await runMcpCommand([
        "mcp",
        "serve",
        "--url",
        "ws://127.0.0.1:18789",
        "--token-file",
        tokenFile,
        "--claude-channel-mode",
        "on",
        "--verbose",
      ]);

      expect(serveOpenClawChannelMcp).toHaveBeenCalledWith({
        gatewayUrl: "ws://127.0.0.1:18789",
        gatewayToken: "secret-token",
        gatewayPassword: undefined,
        claudeChannelMode: "on",
        verbose: true,
      });
    });
  });

  it("starts the local MCP HTTP server and reports connection details", async () => {
    await withTempHome("openclaw-cli-mcp-home-", async () => {
      const onceSpy = vi.spyOn(process, "once").mockImplementation(((
        event: string,
        listener: () => void,
      ) => {
        if (event === "SIGINT") {
          queueMicrotask(listener);
        }
        return process;
      }) as typeof process.once);

      await runMcpCommand(["mcp", "serve-http", "--port", "23119"]);

      expect(ensureMcpLoopbackServer).toHaveBeenCalledWith(23119);
      expect(createUserFacingMcpLoopbackServerDetails).toHaveBeenCalledWith({
        port: 23119,
        ownerToken: "owner-token",
        nonOwnerToken: "non-owner-token",
      });
      expect(closeMcpLoopbackServer).toHaveBeenCalledTimes(1);
      expect(mockLog.mock.calls.map(([line]) => line)).toEqual([
        "Local MCP HTTP server: http://127.0.0.1:23119/mcp",
        "Bearer token (OPENCLAW_MCP_TOKEN): owner-token",
        "Treat this token like local shell access. Run openclaw mcp serve-http --json for a ready-to-paste client config.",
        "Press Ctrl+C to stop.",
      ]);
      expect(onceSpy).toHaveBeenCalledWith("SIGINT", expect.any(Function));
      expect(onceSpy).toHaveBeenCalledWith("SIGTERM", expect.any(Function));
    });
  });

  it("prints machine-readable startup info for the local MCP HTTP server", async () => {
    await withTempHome("openclaw-cli-mcp-home-", async () => {
      vi.spyOn(process, "once").mockImplementation(((event: string, listener: () => void) => {
        if (event === "SIGINT") {
          queueMicrotask(listener);
        }
        return process;
      }) as typeof process.once);

      await runMcpCommand(["mcp", "serve-http", "--json"]);

      expect(ensureMcpLoopbackServer).toHaveBeenCalledWith(0);
      expect(JSON.parse(lastLogLine())).toEqual({
        port: 23119,
        url: "http://127.0.0.1:23119/mcp",
        token: "owner-token",
        tokenEnvVar: "OPENCLAW_MCP_TOKEN",
        config: {
          mcpServers: {
            openclaw: {
              type: "http",
              url: "http://127.0.0.1:23119/mcp",
              headers: { Authorization: "Bearer ${OPENCLAW_MCP_TOKEN}" },
            },
          },
        },
      });
    });
  });

  it("fails when the local MCP HTTP port is invalid", async () => {
    await withTempHome("openclaw-cli-mcp-home-", async () => {
      await expect(runMcpCommand(["mcp", "serve-http", "--port", "nope"])).rejects.toThrow(
        "__exit__:1",
      );
      expect(lastErrorLine()).toBe(
        "MCP HTTP server failed to start: Invalid --port. Use a port number from 1 to 65535, for example 18789.",
      );
    });
  });

  it("fails when the local MCP HTTP port exceeds the TCP range", async () => {
    await withTempHome("openclaw-cli-mcp-home-", async () => {
      await expect(runMcpCommand(["mcp", "serve-http", "--port", "65536"])).rejects.toThrow(
        "__exit__:1",
      );
      expect(lastErrorLine()).toBe(
        "MCP HTTP server failed to start: Invalid --port. Use a port number from 1 to 65535, for example 18789.",
      );
    });
  });
});
