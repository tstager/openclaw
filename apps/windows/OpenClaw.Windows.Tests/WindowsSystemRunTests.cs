using System.Text.Json;
using OpenClaw.Windows;
using OpenClaw.Windows.Native;

namespace OpenClaw.Windows.Tests;

[TestClass]
public sealed class WindowsSystemRunTests
{
    private static WindowsCanvasInvokeRequest Invoke(string command, string? paramsJson) =>
        new("invoke-1", command, paramsJson, "node-1", 5000);

    private static WindowsExecutionPolicyPreferences Enabled(params string[] allowlist) =>
        new(AllowSystemExecution: true, AllowedCommands: allowlist, DefaultTimeoutMs: 5000);

    private sealed class FakeExecutor : IWindowsCommandExecutor
    {
        public WindowsCommandRunResult RunResult { get; set; } =
            new(0, false, true, "ok", string.Empty, null);

        public string? WhichResult { get; set; }

        public IReadOnlyList<string>? LastCommand { get; private set; }

        public int RunCalls { get; private set; }

        public Task<WindowsCommandRunResult> RunAsync(
            IReadOnlyList<string> command,
            string? cwd,
            IReadOnlyDictionary<string, string>? env,
            int? timeoutMs,
            CancellationToken cancellationToken)
        {
            this.RunCalls++;
            this.LastCommand = command;
            return Task.FromResult(this.RunResult);
        }

        public Task<string?> WhichAsync(string command, CancellationToken cancellationToken) =>
            Task.FromResult(this.WhichResult);
    }

    private sealed class RecordingSink : IWindowsNodeEventSink
    {
        public List<(string Event, JsonElement Payload)> Events { get; } = [];

        public Task SendAsync(string @event, string payloadJson, CancellationToken cancellationToken)
        {
            this.Events.Add((@event, JsonDocument.Parse(payloadJson).RootElement));
            return Task.CompletedTask;
        }
    }

    // ---- parser ----

    [TestMethod]
    public void ParserReadsCommandSessionAndRunIdDefaults()
    {
        var ok = WindowsSystemRunParser.TryParse(
            """{"command":["git","status"]}""",
            "fallback-run",
            out var request,
            out var error);

        Assert.IsTrue(ok);
        Assert.IsNull(error);
        Assert.AreEqual("git", request.Executable);
        Assert.AreEqual("node", request.SessionKey);
        Assert.AreEqual("fallback-run", request.RunId);
        Assert.AreEqual("git status", request.CommandText);
    }

    [TestMethod]
    public void ParserReadsCwdEnvTimeoutAndSession()
    {
        var ok = WindowsSystemRunParser.TryParse(
            """{"command":["where","git"],"cwd":"C:\\repo","env":{"FOO":"bar"},"timeoutMs":1234,"sessionKey":"agent:main","runId":"r1"}""",
            "fallback",
            out var request,
            out _);

        Assert.IsTrue(ok);
        Assert.AreEqual(@"C:\repo", request.Cwd);
        Assert.AreEqual(1234, request.TimeoutMs);
        Assert.AreEqual("agent:main", request.SessionKey);
        Assert.AreEqual("r1", request.RunId);
        Assert.IsNotNull(request.Env);
        Assert.AreEqual("bar", request.Env["FOO"]);
    }

    [TestMethod]
    public void ParserRejectsMissingCommand()
    {
        Assert.IsFalse(WindowsSystemRunParser.TryParse("{}", "fallback", out _, out var error));
        StringAssert.Contains(error!, "command");
    }

    // ---- policy ----

    [TestMethod]
    public void PolicyDeniesWhenSystemExecutionDisabled()
    {
        WindowsSystemRunParser.TryParse("""{"command":["git"]}""", "r", out var request, out _);

        var decision = WindowsSystemRunPolicy.Evaluate(WindowsExecutionPolicyPreferences.Default, request);

        Assert.AreEqual(WindowsSystemRunDecisionKind.Denied, decision.Kind);
        Assert.AreEqual("security=deny", decision.Reason);
    }

    [TestMethod]
    public void PolicyDeniesWhenExecutableNotInAllowlist()
    {
        WindowsSystemRunParser.TryParse("""{"command":["git"]}""", "r", out var request, out _);

        var decision = WindowsSystemRunPolicy.Evaluate(Enabled("where", "cmd"), request);

        Assert.AreEqual(WindowsSystemRunDecisionKind.Denied, decision.Kind);
        Assert.AreEqual("allowlist-miss", decision.Reason);
    }

    [TestMethod]
    public void PolicyAllowsAllowlistedExecutableIgnoringPathAndExtension()
    {
        WindowsSystemRunParser.TryParse(
            """{"command":["C:\\Windows\\System32\\where.exe","git"]}""",
            "r",
            out var request,
            out _);

        var decision = WindowsSystemRunPolicy.Evaluate(Enabled("where"), request);

        Assert.AreEqual(WindowsSystemRunDecisionKind.Allowed, decision.Kind);
    }

    [TestMethod]
    public void PolicyAllowsAnyCommandWhenAllowlistEmpty()
    {
        WindowsSystemRunParser.TryParse("""{"command":["git"]}""", "r", out var request, out _);

        Assert.AreEqual(
            WindowsSystemRunDecisionKind.Allowed,
            WindowsSystemRunPolicy.Evaluate(Enabled(), request).Kind);
    }

    // ---- service: run ----

    [TestMethod]
    public async Task RunEmitsExecDeniedAndReturnsUnavailableWhenDisabled()
    {
        var executor = new FakeExecutor();
        var sink = new RecordingSink();
        var service = new WindowsNodeSystemCommandService(
            WindowsExecutionPolicyPreferences.Default,
            executor,
            sink);

        var response = await service.RunAsync(
            Invoke("system.run", """{"command":["git"],"runId":"run-9","sessionKey":"agent:main"}"""),
            CancellationToken.None);

        Assert.IsFalse(response.Ok);
        Assert.AreEqual("UNAVAILABLE", response.Error!.Code);
        Assert.AreEqual(0, executor.RunCalls);
        Assert.HasCount(1, sink.Events);
        Assert.AreEqual("exec.denied", sink.Events[0].Event);
        Assert.AreEqual("run-9", sink.Events[0].Payload.GetProperty("runId").GetString());
        Assert.AreEqual("agent:main", sink.Events[0].Payload.GetProperty("sessionKey").GetString());
        Assert.AreEqual("security=deny", sink.Events[0].Payload.GetProperty("reason").GetString());
    }

    [TestMethod]
    public async Task RunExecutesAndEmitsExecFinishedOnSuccess()
    {
        var executor = new FakeExecutor { RunResult = new(0, false, true, "hello", string.Empty, null) };
        var sink = new RecordingSink();
        var service = new WindowsNodeSystemCommandService(Enabled(), executor, sink);

        var response = await service.RunAsync(
            Invoke("system.run", """{"command":["cmd","/c","echo hello"],"runId":"run-1"}"""),
            CancellationToken.None);

        Assert.IsTrue(response.Ok);
        var payload = JsonDocument.Parse(response.PayloadJson!).RootElement;
        Assert.AreEqual(0, payload.GetProperty("exitCode").GetInt32());
        Assert.IsTrue(payload.GetProperty("success").GetBoolean());
        Assert.AreEqual("hello", payload.GetProperty("stdout").GetString());
        Assert.HasCount(1, sink.Events);
        Assert.AreEqual("exec.finished", sink.Events[0].Event);
        Assert.IsTrue(sink.Events[0].Payload.GetProperty("success").GetBoolean());
        Assert.AreEqual("hello", sink.Events[0].Payload.GetProperty("output").GetString());
    }

    [TestMethod]
    public async Task RunReportsTimedOutResult()
    {
        var executor = new FakeExecutor { RunResult = new(null, true, false, string.Empty, string.Empty, "Command timed out.") };
        var sink = new RecordingSink();
        var service = new WindowsNodeSystemCommandService(Enabled(), executor, sink);

        var response = await service.RunAsync(
            Invoke("system.run", """{"command":["sleep"],"runId":"run-2"}"""),
            CancellationToken.None);

        Assert.IsTrue(response.Ok);
        var payload = JsonDocument.Parse(response.PayloadJson!).RootElement;
        Assert.IsTrue(payload.GetProperty("timedOut").GetBoolean());
        Assert.IsFalse(payload.GetProperty("success").GetBoolean());
        Assert.IsTrue(sink.Events[0].Payload.GetProperty("timedOut").GetBoolean());
    }

    [TestMethod]
    public async Task RunReportsFailedExitCode()
    {
        var executor = new FakeExecutor { RunResult = new(1, false, false, string.Empty, "boom", null) };
        var sink = new RecordingSink();
        var service = new WindowsNodeSystemCommandService(Enabled(), executor, sink);

        var response = await service.RunAsync(
            Invoke("system.run", """{"command":["git","bogus"],"runId":"run-3"}"""),
            CancellationToken.None);

        Assert.IsTrue(response.Ok);
        var payload = JsonDocument.Parse(response.PayloadJson!).RootElement;
        Assert.AreEqual(1, payload.GetProperty("exitCode").GetInt32());
        Assert.IsFalse(payload.GetProperty("success").GetBoolean());
        Assert.AreEqual("exec.finished", sink.Events[0].Event);
    }

    // ---- service: prepare + which ----

    [TestMethod]
    public async Task PrepareReturnsAllowedPlanWithoutEmittingEvents()
    {
        var executor = new FakeExecutor();
        var sink = new RecordingSink();
        var service = new WindowsNodeSystemCommandService(Enabled("git"), executor, sink);

        var response = await service.PrepareAsync(
            Invoke("system.run.prepare", """{"command":["git","status"],"cwd":"C:\\repo"}"""),
            CancellationToken.None);

        Assert.IsTrue(response.Ok);
        var payload = JsonDocument.Parse(response.PayloadJson!).RootElement;
        Assert.IsTrue(payload.GetProperty("allowed").GetBoolean());
        Assert.AreEqual(@"C:\repo", payload.GetProperty("cwd").GetString());
        Assert.IsEmpty(sink.Events);
        Assert.AreEqual(0, executor.RunCalls);
    }

    [TestMethod]
    public async Task PrepareReportsDeniedPlanWhenAllowlistMisses()
    {
        var service = new WindowsNodeSystemCommandService(Enabled("where"), new FakeExecutor(), new RecordingSink());

        var response = await service.PrepareAsync(
            Invoke("system.run.prepare", """{"command":["git"]}"""),
            CancellationToken.None);

        Assert.IsTrue(response.Ok);
        var payload = JsonDocument.Parse(response.PayloadJson!).RootElement;
        Assert.IsFalse(payload.GetProperty("allowed").GetBoolean());
        Assert.AreEqual("allowlist-miss", payload.GetProperty("reason").GetString());
    }

    [TestMethod]
    public async Task WhichReturnsResolvedPath()
    {
        var executor = new FakeExecutor { WhichResult = @"C:\Windows\System32\where.exe" };
        var service = new WindowsNodeSystemCommandService(Enabled(), executor, new RecordingSink());

        var response = await service.WhichAsync(
            Invoke("system.which", """{"command":["where"]}"""),
            CancellationToken.None);

        Assert.IsTrue(response.Ok);
        var payload = JsonDocument.Parse(response.PayloadJson!).RootElement;
        Assert.IsTrue(payload.GetProperty("found").GetBoolean());
        Assert.AreEqual(@"C:\Windows\System32\where.exe", payload.GetProperty("path").GetString());
    }

    [TestMethod]
    public async Task WhichIsUnavailableWhenExecutionDisabled()
    {
        var service = new WindowsNodeSystemCommandService(
            WindowsExecutionPolicyPreferences.Default,
            new FakeExecutor { WhichResult = "x" },
            new RecordingSink());

        var response = await service.WhichAsync(
            Invoke("system.which", """{"command":["where"]}"""),
            CancellationToken.None);

        Assert.IsFalse(response.Ok);
        Assert.AreEqual("UNAVAILABLE", response.Error!.Code);
    }

    // ---- surface ----

    [TestMethod]
    public void SurfaceAdvertisesSystemCommandsOnlyWhenExecutionEnabled()
    {
        var host = WindowsHostCapabilityProbe.Current;

        var disabled = WindowsNodeSurface.Build(host, AppPreferences.Default);
        CollectionAssert.DoesNotContain(disabled.Commands.ToArray(), "system.run");
        Assert.IsFalse(disabled.Permissions.ContainsKey("system.run"));

        var enabled = WindowsNodeSurface.Build(
            host,
            AppPreferences.Default with { Execution = Enabled() });
        CollectionAssert.Contains(enabled.Commands.ToArray(), "system.which");
        CollectionAssert.Contains(enabled.Commands.ToArray(), "system.run.prepare");
        CollectionAssert.Contains(enabled.Commands.ToArray(), "system.run");
        Assert.IsTrue(enabled.Permissions["system.run"]);
    }

    // ---- real process executor (Windows) ----

    [TestMethod]
    public async Task ProcessExecutorRunsCommandAndCapturesStdout()
    {
        var executor = new WindowsProcessCommandExecutor();

        var result = await executor.RunAsync(
            ["cmd.exe", "/c", "echo", "hello-openclaw"],
            cwd: null,
            env: null,
            timeoutMs: 10000,
            CancellationToken.None);

        Assert.IsTrue(result.Success, result.Error ?? result.Stderr);
        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(result.Stdout, "hello-openclaw");
    }

    [TestMethod]
    public async Task ProcessExecutorResolvesKnownExecutable()
    {
        var executor = new WindowsProcessCommandExecutor();

        var path = await executor.WhichAsync("cmd", CancellationToken.None);

        Assert.IsNotNull(path);
        StringAssert.Contains(path!.ToLowerInvariant(), "cmd.exe");
    }
}
