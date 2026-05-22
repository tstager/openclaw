import { Command } from "commander";
import { parseConfigValue } from "../auto-reply/reply/config-value.js";
import {
  listConfiguredMcpServers,
  setConfiguredMcpServer,
  unsetConfiguredMcpServer,
} from "../config/mcp-config.js";
import {
  closeMcpLoopbackServer,
  createUserFacingMcpLoopbackServerDetails,
  ensureMcpLoopbackServer,
  getActiveMcpLoopbackRuntime,
} from "../gateway/mcp-http.js";
import { formatErrorMessage } from "../infra/errors.js";
import { serveOpenClawChannelMcp } from "../mcp/channel-server.js";
import { defaultRuntime } from "../runtime.js";
import {
  normalizeLowercaseStringOrEmpty,
  normalizeStringifiedOptionalString,
} from "../shared/string-coerce.js";
import { formatCliCommand } from "./command-format.js";
import { formatInvalidPortOption } from "./error-format.js";
import { resolveGatewayAuthOptions } from "./gateway-secret-options.js";
import { applyParentDefaultHelpAction } from "./program/parent-default-help.js";
import { parsePort } from "./shared/parse-port.js";

function fail(message: string): never {
  defaultRuntime.error(message);
  defaultRuntime.exit(1);
  throw new Error(message);
}

function printJson(value: unknown): void {
  defaultRuntime.writeJson(value);
}

function waitForTerminationSignal(): Promise<void> {
  return new Promise((resolve) => {
    let resolved = false;
    const finish = () => {
      if (resolved) {
        return;
      }
      resolved = true;
      resolve();
    };
    process.once("SIGINT", finish);
    process.once("SIGTERM", finish);
  });
}

function printMcpHttpServerDetails(
  details: ReturnType<typeof createUserFacingMcpLoopbackServerDetails>,
) {
  defaultRuntime.log(`Local MCP HTTP server: ${details.url}`);
  defaultRuntime.log(`Bearer token (${details.tokenEnvVar}): ${details.token}`);
  defaultRuntime.log(
    `Treat this token like local shell access. Run ${formatCliCommand("openclaw mcp serve-http --json")} for a ready-to-paste client config.`,
  );
  defaultRuntime.log("Press Ctrl+C to stop.");
}

export function registerMcpCli(program: Command) {
  const mcp = program.command("mcp").description("Manage OpenClaw MCP config and channel bridge");

  mcp
    .command("serve")
    .description("Expose OpenClaw channels over MCP stdio")
    .option("--url <url>", "Gateway WebSocket URL (defaults to gateway.remote.url when configured)")
    .option("--token <token>", "Gateway token (if required)")
    .option("--token-file <path>", "Read gateway token from file")
    .option("--password <password>", "Gateway password (if required)")
    .option("--password-file <path>", "Read gateway password from file")
    .option(
      "--claude-channel-mode <mode>",
      "Claude channel notification mode: auto, on, or off",
      "auto",
    )
    .option("-v, --verbose", "Verbose logging to stderr", false)
    .action(async (opts) => {
      try {
        const { gatewayToken, gatewayPassword } = resolveGatewayAuthOptions(opts);
        const claudeChannelMode = normalizeLowercaseStringOrEmpty(
          normalizeStringifiedOptionalString(opts.claudeChannelMode) ?? "auto",
        );
        if (
          claudeChannelMode !== "auto" &&
          claudeChannelMode !== "on" &&
          claudeChannelMode !== "off"
        ) {
          throw new Error('Invalid --claude-channel-mode value. Use "auto", "on", or "off".');
        }
        await serveOpenClawChannelMcp({
          gatewayUrl: opts.url as string | undefined,
          gatewayToken,
          gatewayPassword,
          claudeChannelMode,
          verbose: Boolean(opts.verbose),
        });
      } catch (err) {
        defaultRuntime.error(
          `MCP server failed to start: ${formatErrorMessage(err)}. Run ${formatCliCommand("openclaw mcp list")} to inspect configured servers.`,
        );
        defaultRuntime.exit(1);
      }
    });

  mcp
    .command("serve-http")
    .description("Expose the local OpenClaw MCP HTTP server on loopback")
    .option("--port <port>", "Bind port (defaults to a random loopback port)")
    .option("--json", "Print machine-readable connection info before waiting", false)
    .action(async (opts: { port?: string; json?: boolean }) => {
      try {
        const port = parsePort(opts.port);
        if (opts.port !== undefined && port === null) {
          throw new Error(formatInvalidPortOption("--port"));
        }
        await ensureMcpLoopbackServer(port ?? 0);
        const runtime = getActiveMcpLoopbackRuntime();
        if (!runtime) {
          throw new Error("Local MCP HTTP server started without an active runtime");
        }
        const details = createUserFacingMcpLoopbackServerDetails(runtime);
        if (opts.json) {
          printJson(details);
        } else {
          printMcpHttpServerDetails(details);
        }
        await waitForTerminationSignal();
        await closeMcpLoopbackServer();
      } catch (err) {
        defaultRuntime.error(`MCP HTTP server failed to start: ${formatErrorMessage(err)}`);
        defaultRuntime.exit(1);
      }
    });

  mcp
    .command("list")
    .description("List configured MCP servers")
    .option("--json", "Print JSON")
    .action(async (opts: { json?: boolean }) => {
      const loaded = await listConfiguredMcpServers();
      if (!loaded.ok) {
        fail(loaded.error);
      }
      if (opts.json) {
        printJson(loaded.mcpServers);
        return;
      }
      const names = Object.keys(loaded.mcpServers).toSorted();
      if (names.length === 0) {
        defaultRuntime.log(
          `No MCP servers configured in ${loaded.path}. Add one with ${formatCliCommand('openclaw mcp set <name> \'{"command":"uvx","args":["context7-mcp"]}\'')}.`,
        );
        return;
      }
      defaultRuntime.log(`MCP servers (${loaded.path}):`);
      for (const name of names) {
        defaultRuntime.log(`- ${name}`);
      }
    });

  mcp
    .command("show")
    .description("Show one configured MCP server or the full MCP config")
    .argument("[name]", "MCP server name")
    .option("--json", "Print JSON")
    .action(async (name: string | undefined, opts: { json?: boolean }) => {
      const loaded = await listConfiguredMcpServers();
      if (!loaded.ok) {
        fail(loaded.error);
      }
      const value = name ? loaded.mcpServers[name] : loaded.mcpServers;
      if (name && !value) {
        fail(
          `No MCP server named "${name}" in ${loaded.path}. Run ${formatCliCommand("openclaw mcp list")} to see configured servers.`,
        );
      }
      if (opts.json) {
        printJson(value ?? {});
        return;
      }
      if (name) {
        defaultRuntime.log(`MCP server "${name}" (${loaded.path}):`);
      } else {
        defaultRuntime.log(`MCP servers (${loaded.path}):`);
      }
      printJson(value ?? {});
    });

  mcp
    .command("set")
    .description("Set one configured MCP server from a JSON object")
    .argument("<name>", "MCP server name")
    .argument("<value>", 'JSON object, for example {"command":"uvx","args":["context7-mcp"]}')
    .action(async (name: string, rawValue: string) => {
      const parsed = parseConfigValue(rawValue);
      if (parsed.error) {
        fail(parsed.error);
      }
      const result = await setConfiguredMcpServer({ name, server: parsed.value });
      if (!result.ok) {
        fail(result.error);
      }
      defaultRuntime.log(`Saved MCP server "${name}" to ${result.path}.`);
    });

  mcp
    .command("unset")
    .description("Remove one configured MCP server")
    .argument("<name>", "MCP server name")
    .action(async (name: string) => {
      const result = await unsetConfiguredMcpServer({ name });
      if (!result.ok) {
        fail(result.error);
      }
      if (!result.removed) {
        fail(
          `No MCP server named "${name}" in ${result.path}. Run ${formatCliCommand("openclaw mcp list")} to see configured servers.`,
        );
      }
      defaultRuntime.log(`Removed MCP server "${name}" from ${result.path}.`);
    });

  applyParentDefaultHelpAction(mcp);
}
