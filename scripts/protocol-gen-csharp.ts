import { promises as fs } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  ErrorCodes,
  PROTOCOL_VERSION,
  ProtocolSchemas,
} from "../packages/gateway-protocol/src/schema.js";

type JsonSchema = {
  const?: string | number | boolean;
  type?: string | string[];
  properties?: Record<string, JsonSchema>;
  required?: string[];
  items?: JsonSchema;
  enum?: string[];
  patternProperties?: Record<string, JsonSchema>;
};

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, "..");
const outPath = path.join(
  repoRoot,
  "apps",
  "windows",
  "OpenClaw.Protocol",
  "Generated",
  "GatewayModels.g.cs",
);
const emittedSchemaNames = new Set(["ErrorShape", "RequestFrame", "ResponseFrame", "EventFrame"]);
const emittedSchemaOrder = ["ErrorShape", "RequestFrame", "ResponseFrame", "EventFrame"];

const csharpKeywords = new Set([
  "abstract",
  "as",
  "base",
  "bool",
  "break",
  "case",
  "catch",
  "class",
  "const",
  "continue",
  "decimal",
  "default",
  "delegate",
  "do",
  "double",
  "else",
  "enum",
  "event",
  "explicit",
  "extern",
  "false",
  "finally",
  "fixed",
  "float",
  "for",
  "foreach",
  "goto",
  "if",
  "implicit",
  "in",
  "int",
  "interface",
  "internal",
  "is",
  "lock",
  "long",
  "namespace",
  "new",
  "null",
  "object",
  "operator",
  "out",
  "override",
  "params",
  "private",
  "protected",
  "public",
  "readonly",
  "ref",
  "return",
  "sbyte",
  "sealed",
  "short",
  "sizeof",
  "stackalloc",
  "static",
  "string",
  "struct",
  "switch",
  "this",
  "throw",
  "true",
  "try",
  "typeof",
  "uint",
  "ulong",
  "unchecked",
  "unsafe",
  "ushort",
  "using",
  "virtual",
  "void",
  "volatile",
  "while",
]);

const schemaNameByObject = new Map<object, string>();
const schemaNameBySignature = new Map<string, string>();
const duplicateSchemaSignatures = new Set<string>();

function stableJson(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map(stableJson);
  }
  if (value && typeof value === "object") {
    const record = value as Record<string, unknown>;
    return Object.fromEntries(
      Object.keys(record)
        .toSorted()
        .map((key) => [key, stableJson(record[key])]),
    );
  }
  return value;
}

function schemaSignature(schema: JsonSchema): string {
  return JSON.stringify(stableJson(schema));
}

function registerNamedSchema(name: string, schema: JsonSchema): void {
  schemaNameByObject.set(schema as object, name);
  const signature = schemaSignature(schema);
  if (duplicateSchemaSignatures.has(signature)) {
    return;
  }
  if (schemaNameBySignature.has(signature)) {
    schemaNameBySignature.delete(signature);
    duplicateSchemaSignatures.add(signature);
    return;
  }
  schemaNameBySignature.set(signature, name);
}

function namedSchema(schema: JsonSchema, allowStructuralFallback = false): string | undefined {
  return (
    schemaNameByObject.get(schema as object) ??
    (allowStructuralFallback ? schemaNameBySignature.get(schemaSignature(schema)) : undefined)
  );
}

function pascalCase(input: string): string {
  const parts = input
    .toLowerCase()
    .replace(/[^a-zA-Z0-9]+/g, " ")
    .trim()
    .split(/\s+/)
    .filter(Boolean);
  const result = parts.map((part) => `${part[0]?.toUpperCase() ?? ""}${part.slice(1)}`).join("");
  return /^[0-9]/.test(result) ? `_${result}` : result || "Value";
}

function safeIdentifier(name: string): string {
  const candidate = pascalCase(name.replace(/-/g, "_"));
  return csharpKeywords.has(candidate) ? `_${candidate}` : candidate;
}

function stringLiteral(value: string): string {
  return JSON.stringify(value);
}

function csharpType(schema: JsonSchema, required: boolean, allowStructuralNamed = false): string {
  const named = namedSchema(schema, allowStructuralNamed);
  const schemaType = Array.isArray(schema.type)
    ? schema.type.find((entry) => entry !== "null")
    : schema.type;
  let base: string;
  if (named && named !== "GatewayFrame" && emittedSchemaNames.has(named)) {
    base = named;
  } else if (schema.const !== undefined) {
    base =
      typeof schema.const === "boolean"
        ? "bool"
        : typeof schema.const === "number"
          ? "double"
          : "string";
  } else if (schemaType === "string" || schema.enum) {
    base = "string";
  } else if (schemaType === "integer") {
    base = "long";
  } else if (schemaType === "number") {
    base = "double";
  } else if (schemaType === "boolean") {
    base = "bool";
  } else if (schemaType === "array") {
    base = `IReadOnlyList<${csharpType(schema.items ?? {}, true, true)}>`;
  } else if (schema.patternProperties) {
    base = "IReadOnlyDictionary<string, JsonElement>";
  } else if (schemaType === "object") {
    base = "JsonElement";
  } else {
    base = "JsonElement";
  }
  if (required || base.endsWith("?")) {
    return base;
  }
  return `${base}?`;
}

function defaultValueFor(type: string, schema: JsonSchema): string | null {
  if (schema.const !== undefined) {
    if (typeof schema.const === "boolean") {
      return ` = ${schema.const ? "true" : "false"};`;
    }
    if (typeof schema.const === "number") {
      return ` = ${schema.const};`;
    }
    return ` = ${stringLiteral(String(schema.const))};`;
  }
  if (type === "string") {
    return ' = "";';
  }
  if (type.startsWith("IReadOnlyList<")) {
    return " = Array.Empty<" + type.slice("IReadOnlyList<".length, -1) + ">();";
  }
  if (type.startsWith("IReadOnlyDictionary<")) {
    return " = new Dictionary<string, JsonElement>();";
  }
  if (/^[A-Z][A-Za-z0-9_]*$/.test(type)) {
    return " = new();";
  }
  return null;
}

function emitModel(name: string, schema: JsonSchema): string {
  const props = schema.properties ?? {};
  const required = new Set(schema.required ?? []);
  const lines: string[] = [`public sealed record ${name}`, "{"];
  if (Object.keys(props).length === 0) {
    lines.push("}");
    return lines.join("\n");
  }
  for (const [jsonName, propSchema] of Object.entries(props)) {
    const propName = safeIdentifier(jsonName);
    const propType = csharpType(propSchema, required.has(jsonName), true);
    const defaultValue = required.has(jsonName) ? defaultValueFor(propType, propSchema) : null;
    lines.push(`    [JsonPropertyName(${stringLiteral(jsonName)})]`);
    lines.push(`    public ${propType} ${propName} { get; init; }${defaultValue ?? ""}`);
    lines.push("");
  }
  if (lines.at(-1) === "") {
    lines.pop();
  }
  lines.push("}");
  return lines.join("\n");
}

function emitErrorCodes(): string {
  const lines = ["public static class ErrorCodes", "{"];
  for (const value of Object.values(ErrorCodes)) {
    lines.push(`    public const string ${safeIdentifier(value)} = ${stringLiteral(value)};`);
  }
  lines.push("}");
  return lines.join("\n");
}

function emitGatewayFrameReader(): string {
  return `public sealed record GatewayFrame
{
    public string Type { get; init; } = "";
    public RequestFrame? Request { get; init; }
    public ResponseFrame? Response { get; init; }
    public EventFrame? Event { get; init; }
    public JsonElement? Unknown { get; init; }
}

public static class GatewayFrameReader
{
    public static GatewayFrame Deserialize(string json, JsonSerializerOptions? options = null)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("type", out var typeElement))
        {
            throw new JsonException("Gateway frame is missing required property 'type'.");
        }

        var type = typeElement.GetString() ?? "";
        options ??= OpenClaw.Protocol.GatewayProtocolJson.SerializerOptions;

        return type switch
        {
            "req" => new GatewayFrame
            {
                Type = type,
                Request = JsonSerializer.Deserialize<RequestFrame>(json, options),
            },
            "res" => new GatewayFrame
            {
                Type = type,
                Response = JsonSerializer.Deserialize<ResponseFrame>(json, options),
            },
            "event" => new GatewayFrame
            {
                Type = type,
                Event = JsonSerializer.Deserialize<EventFrame>(json, options),
            },
            _ => new GatewayFrame
            {
                Type = type,
                Unknown = document.RootElement.Clone(),
            },
        };
    }
}`;
}

function generateContent(): string {
  const definitions = Object.entries(ProtocolSchemas) as Array<[string, JsonSchema]>;
  for (const [name, schema] of definitions) {
    registerNamedSchema(name, schema);
  }

  const parts = [
    "// Generated by scripts/protocol-gen-csharp.ts - do not edit by hand.",
    "#nullable enable",
    "using System;",
    "using System.Collections.Generic;",
    "using System.Text.Json;",
    "using System.Text.Json.Serialization;",
    "",
    "namespace OpenClaw.Protocol.Generated;",
    "",
    "public static class GatewayProtocol",
    "{",
    `    public const int Version = ${PROTOCOL_VERSION};`,
    "}",
    "",
    emitErrorCodes(),
  ];

  const definitionsByName = new Map(definitions);
  for (const name of emittedSchemaOrder) {
    const schema = definitionsByName.get(name);
    if (!schema) {
      throw new Error(`Missing protocol schema: ${name}`);
    }
    if (schema.type === "object") {
      parts.push("", emitModel(name, schema));
    }
  }

  parts.push("", emitGatewayFrameReader(), "");
  return parts.join("\n");
}

async function main(): Promise<void> {
  const content = generateContent();
  if (process.argv.includes("--check")) {
    const existing = await fs.readFile(outPath, "utf8").catch(() => "");
    if (existing !== content) {
      console.error(`${path.relative(repoRoot, outPath)} is stale. Run pnpm windows:protocol:gen.`);
      process.exit(1);
    }
    console.log(`${path.relative(repoRoot, outPath)} is up to date`);
    return;
  }
  await fs.mkdir(path.dirname(outPath), { recursive: true });
  await fs.writeFile(outPath, content);
  console.log(`wrote ${outPath}`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
