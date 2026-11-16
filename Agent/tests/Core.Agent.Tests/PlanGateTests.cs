using System.Text.Json;
using Core.Agent;
using Core.Agent.Hooks;
using Core.Agent.Hosting;
using GitHub.Copilot;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Unit tests for <see cref="PreToolUseHook.HandleAsync"/> — the OnPreToolUse
/// gate. Drives it with a real <see cref="SessionState"/> and asserts the
/// unified-mind contract: bash/powershell are denied without an approved plan;
/// submit_response renders and allows; a valid one-shot plan renders and ends the
/// turn; an approved plan unlocks execution; a rejected plan denies and leaves the
/// gate closed.
/// Console output is suppressed; the interactive y/n is fed via a redirected stdin
/// raced against the hook, the same way the host pumps approvals.
/// </summary>
[Collection("Console")]
public class PlanGateTests
{
    private const string az = "/fake/az";

    private static JsonElement Args(object obj) => JsonSerializer.SerializeToElement(obj);

    private static PreToolUseHookInput Input(string toolName, object? args) => new()
    {
        ToolName = toolName,
        ToolArgs = args is null ? null : Args(args),
    };

    private static object ValidPlan() => new
    {
        issues = new[] { "The resource group doesn't exist yet." },
        steps = new[] { new { id = 1, label = "Create RG", cmd = "az group create -n foo -l eastus" } },
    };

    // Runs the hook with output suppressed. For branches that don't read stdin
    // (gate denial, submit_response), no pump is needed.
    private static async Task<PreToolUseHookOutput?> RunHookAsync(
        PreToolUseHookInput input, SessionState state, ConsoleApprovalPrompt approval)
    {
        var prevOut = Console.Out;
        Console.SetOut(TextWriter.Null);
        try
        {
            return await PreToolUseHook.HandleAsync(input, az, approval, state, CancellationToken.None);
        }
        finally
        {
            Console.SetOut(prevOut);
        }
    }

    // Drives an interactive plan approval: the hook awaits RequestPlanAsync, so we
    // race it against PumpOneAsync with stdin preloaded with the user's y/n answer.
    private static async Task<PreToolUseHookOutput?> RunInteractivePlanAsync(SessionState state, string userInput)
    {
        var approval = new ConsoleApprovalPrompt();
        var prevIn = Console.In;
        var prevOut = Console.Out;
        Console.SetIn(new StringReader(userInput + Environment.NewLine));
        Console.SetOut(TextWriter.Null);
        try
        {
            var hookTask = PreToolUseHook.HandleAsync(
                Input("propose_plan", ValidPlan()), az, approval, state, CancellationToken.None);
            // The hook posts the plan to the approval channel; pump it to read y/n.
            await approval.PumpOneAsync(CancellationToken.None);
            return await hookTask;
        }
        finally
        {
            Console.SetIn(prevIn);
            Console.SetOut(prevOut);
        }
    }

    [Theory]
    [InlineData("bash")]
    [InlineData("powershell")]
    public async Task Denies_shell_without_approved_plan(string tool)
    {
        var state = new SessionState { PlanApproved = false };
        var approval = new ConsoleApprovalPrompt();

        var result = await RunHookAsync(Input(tool, new { command = "az group list" }), state, approval);

        Assert.NotNull(result);
        Assert.Equal("deny", result!.PermissionDecision);
        Assert.False(state.PlanApproved);
    }

    [Theory]
    [InlineData("bash")]
    [InlineData("powershell")]
    public async Task Allows_shell_once_plan_approved(string tool)
    {
        // Pre-approved gate: the rewrite branch only fires for a bare "az ..."
        // command, so a non-az command passes straight through (returns null).
        var state = new SessionState { PlanApproved = true };
        var approval = new ConsoleApprovalPrompt();

        var result = await RunHookAsync(Input(tool, new { command = "echo hi" }), state, approval);

        Assert.Null(result);
    }

    [Fact]
    public async Task Submit_response_renders_and_allows()
    {
        var state = new SessionState { IsOneShot = true };
        var approval = new ConsoleApprovalPrompt();

        var result = await RunHookAsync(
            Input("submit_response", new { status = "completed", message = "all set" }), state, approval);

        Assert.NotNull(result);
        Assert.Equal("allow", result!.PermissionDecision);
        Assert.True(state.SubmitRendered);
    }

    [Fact]
    public async Task Submit_response_with_bad_args_is_denied_with_correction()
    {
        var state = new SessionState();
        var approval = new ConsoleApprovalPrompt();

        var result = await RunHookAsync(Input("submit_response", new { status = "bogus" }), state, approval);

        Assert.NotNull(result);
        Assert.Equal("deny", result!.PermissionDecision);
        Assert.False(state.SubmitRendered);
        Assert.NotNull(result.AdditionalContext);
    }

    [Fact]
    public async Task OneShot_plan_renders_and_allows_without_unlocking_execution()
    {
        // In one-shot the first valid plan is the final outcome: it is shown right
        // away and the turn ends (the agent does not need submit_response). It does
        // NOT unlock bash — there is no execution stage.
        var state = new SessionState { IsOneShot = true };
        var approval = new ConsoleApprovalPrompt();

        var result = await RunHookAsync(Input("propose_plan", ValidPlan()), state, approval);

        Assert.NotNull(result);
        Assert.Equal("allow", result!.PermissionDecision);
        Assert.True(state.PlanShown);
        Assert.True(state.SubmitRendered);
        Assert.True(state.SubmittedThisTurn);
        Assert.False(state.PlanApproved);
        Assert.Null(state.LastPlanSteps);
    }

    [Fact]
    public async Task OneShot_plan_reports_the_shown_steps()
    {
        IReadOnlyList<ConsoleApprovalPrompt.PlanStep>? shown = null;
        var finalRendered = false;
        var state = new SessionState
        {
            IsOneShot = true,
            OnOutcomeShown = steps => shown = steps,
            OnFinalRendered = () => finalRendered = true,
        };

        await RunHookAsync(Input("propose_plan", ValidPlan()), state, new ConsoleApprovalPrompt());

        Assert.True(finalRendered);
        var step = Assert.Single(shown!);
        Assert.Equal("az group create -n foo -l eastus", step.Cmd);
    }

    [Fact]
    public async Task OneShot_submit_after_plan_is_denied()
    {
        // The model may call submit_response in the same turn as propose_plan; the
        // plan already closed the turn, so the extra call is denied, not re-rendered.
        var state = new SessionState { IsOneShot = true };
        var approval = new ConsoleApprovalPrompt();
        await RunHookAsync(Input("propose_plan", ValidPlan()), state, approval);

        var result = await RunHookAsync(Input("submit_response", new { status = "completed" }), state, approval);

        Assert.NotNull(result);
        Assert.Equal("deny", result!.PermissionDecision);
    }

    [Fact]
    public async Task OneShot_rejected_plan_does_not_end_the_turn()
    {
        var state = new SessionState { IsOneShot = true };

        var result = await RunHookAsync(
            Input("propose_plan", new { issues = new[] { "x" } }), state, new ConsoleApprovalPrompt());

        Assert.NotNull(result);
        Assert.Equal("deny", result!.PermissionDecision);
        Assert.False(state.SubmitRendered);
        Assert.False(state.SubmittedThisTurn);
    }

    // `az agent tool docs list` output that lists the given CLI command names.
    private static string Catalog(params string[] cliNames) => JsonSerializer.Serialize(new
    {
        groups = Array.Empty<object>(),
        commands = cliNames.Select(name => new { cliName = name, psName = "", verb = name.Split(' ')[0], summary = "" }),
    });

    private static object PlanWithCommands(params string[] commands) => new
    {
        issues = new[] { "The command failed." },
        steps = commands.Select((cmd, index) => new { id = index + 1, label = $"Step {index + 1}", cmd }).ToArray(),
    };

    [Fact]
    public async Task Cli_plan_step_without_az_gets_the_prefix()
    {
        IReadOnlyList<ConsoleApprovalPrompt.PlanStep>? shown = null;
        var state = new SessionState
        {
            IsOneShot = true,
            IsCliFlavor = true,
            ReadCommandCatalog = _ => Task.FromResult(Catalog("rest", "vm show")),
            OnOutcomeShown = steps => shown = steps,
        };
        var plan = new
        {
            issues = new[] { "fetch is not an HTTP method." },
            steps = new[] { new { id = 1, label = "Retry with GET", cmd = "rest --method GET --uri https://example" } },
        };

        await RunHookAsync(Input("propose_plan", plan), state, new ConsoleApprovalPrompt());

        Assert.Equal("az rest --method GET --uri https://example", Assert.Single(shown!).Cmd);
    }

    [Fact]
    public async Task Cli_plan_step_gets_the_prefix_when_the_catalog_lists_it()
    {
        // Only the catalog decides: the failed command (which may itself be
        // misspelled, for example "deploment group create") plays no part.
        IReadOnlyList<ConsoleApprovalPrompt.PlanStep>? shown = null;
        var state = new SessionState
        {
            IsOneShot = true,
            IsCliFlavor = true,
            ReadCommandCatalog = _ => Task.FromResult(Catalog("deployment group create", "deployment group show")),
            OnOutcomeShown = steps => shown = steps,
        };

        await RunHookAsync(
            Input("propose_plan", PlanWithCommands("deployment group create --resource-group g --template-file main.bicep")),
            state, new ConsoleApprovalPrompt());

        Assert.Equal("az deployment group create --resource-group g --template-file main.bicep", Assert.Single(shown!).Cmd);
    }

    [Fact]
    public async Task Cli_plan_prose_and_other_programs_are_left_unchanged()
    {
        IReadOnlyList<ConsoleApprovalPrompt.PlanStep>? shown = null;
        var state = new SessionState
        {
            IsOneShot = true,
            IsCliFlavor = true,
            ReadCommandCatalog = _ => Task.FromResult(Catalog("login", "network vnet create", "vm create")),
            OnOutcomeShown = steps => shown = steps,
        };
        var commands = new[]
        {
            "Use a strong password value for `--admin-password` that meets Azure VM password requirements.",
            "use a strong password value for --admin-password",
            "network security group must allow port 22",
            "login with an account that can read the subscription",
            "kubectl get pods",
        };

        await RunHookAsync(Input("propose_plan", PlanWithCommands(commands)), state, new ConsoleApprovalPrompt());

        Assert.Equal(commands, shown!.Select(step => step.Cmd));
    }

    [Fact]
    public async Task Cli_plan_is_unchanged_when_the_catalog_lookup_fails()
    {
        var lookups = new Func<CancellationToken, Task<string>>[]
        {
            _ => Task.FromResult("""{"error":"shell_out_timeout","timeoutSeconds":30}"""),
            _ => throw new InvalidOperationException("az could not start"),
        };
        foreach (var lookup in lookups)
        {
            IReadOnlyList<ConsoleApprovalPrompt.PlanStep>? shown = null;
            var state = new SessionState
            {
                IsOneShot = true,
                IsCliFlavor = true,
                ReadCommandCatalog = lookup,
                OnOutcomeShown = steps => shown = steps,
            };

            await RunHookAsync(Input("propose_plan", PlanWithCommands("rest --method GET")), state, new ConsoleApprovalPrompt());

            Assert.Equal("rest --method GET", Assert.Single(shown!).Cmd);
            Assert.Empty(state.CommandNames!);
        }
    }

    [Fact]
    public async Task Cli_plan_that_starts_every_step_with_az_does_not_read_the_catalog()
    {
        var reads = 0;
        var state = new SessionState
        {
            IsOneShot = true,
            IsCliFlavor = true,
            ReadCommandCatalog = _ => { reads++; return Task.FromResult(Catalog("rest")); },
        };

        await RunHookAsync(Input("propose_plan", ValidPlan()), state, new ConsoleApprovalPrompt());

        Assert.Equal(0, reads);
        Assert.Null(state.CommandNames);
    }

    [Fact]
    public async Task Cli_catalog_is_read_once_per_session()
    {
        var reads = 0;
        var shown = new List<string>();
        var state = new SessionState
        {
            IsOneShot = true,
            IsCliFlavor = true,
            ReadCommandCatalog = _ => { reads++; return Task.FromResult(Catalog("rest", "vm show")); },
            OnOutcomeShown = steps => shown.AddRange(steps!.Select(step => step.Cmd)),
        };

        await RunHookAsync(Input("propose_plan", PlanWithCommands("rest --method GET", "vm show --name x")), state, new ConsoleApprovalPrompt());
        // A later turn of the same session (SendAsync resets this flag) reuses the names.
        state.SubmittedThisTurn = false;
        await RunHookAsync(Input("propose_plan", PlanWithCommands("vm show --name y")), state, new ConsoleApprovalPrompt());

        Assert.Equal(1, reads);
        Assert.Equal(new[] { "az rest --method GET", "az vm show --name x", "az vm show --name y" }, shown);
    }

    [Fact]
    public async Task PowerShell_plan_steps_are_left_unchanged()
    {
        IReadOnlyList<ConsoleApprovalPrompt.PlanStep>? shown = null;
        var reads = 0;
        var state = new SessionState
        {
            IsOneShot = true,
            IsCliFlavor = false,
            ReadCommandCatalog = _ => { reads++; return Task.FromResult(Catalog("rest")); },
            OnOutcomeShown = steps => shown = steps,
        };
        var plan = new
        {
            issues = new[] { "fetch is not an HTTP method." },
            steps = new[] { new { id = 1, label = "Retry with GET", cmd = "rest --method GET" } },
        };

        await RunHookAsync(Input("propose_plan", plan), state, new ConsoleApprovalPrompt());

        Assert.Equal("rest --method GET", Assert.Single(shown!).Cmd);
        Assert.Equal(0, reads);
    }

    [Fact]
    public async Task Interactive_plan_approval_unlocks_execution()
    {
        var state = new SessionState { IsOneShot = false };

        var result = await RunInteractivePlanAsync(state, "y");

        Assert.NotNull(result);
        Assert.Equal("allow", result!.PermissionDecision);
        Assert.True(state.PlanApproved);
    }

    [Fact]
    public async Task Interactive_plan_rejection_keeps_gate_closed_and_asks_to_replan()
    {
        var state = new SessionState { IsOneShot = false, PlanApproved = false };

        var result = await RunInteractivePlanAsync(state, "n");

        Assert.NotNull(result);
        Assert.Equal("deny", result!.PermissionDecision);
        Assert.False(state.PlanApproved);
        Assert.Contains("rejected", result.AdditionalContext, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Submit_response_sets_turn_flag()
    {
        var state = new SessionState { IsOneShot = true };
        var approval = new ConsoleApprovalPrompt();

        await RunHookAsync(
            Input("submit_response", new { status = "completed", message = "all set" }), state, approval);

        Assert.True(state.SubmittedThisTurn);
    }

    [Theory]
    [InlineData("bash")]
    [InlineData("powershell")]
    [InlineData("propose_plan")]
    [InlineData("submit_response")]
    public async Task Any_tool_after_submit_is_denied_this_turn(string tool)
    {
        // The post-terminator gate: once submit_response rendered this turn, every
        // further tool call is denied (no exceptions — even another submit), so the
        // model can't keep acting past the terminator. PlanApproved=true proves the
        // denial comes from the gate, not the plan check.
        var state = new SessionState { IsOneShot = false, PlanApproved = true, SubmittedThisTurn = true };
        var approval = new ConsoleApprovalPrompt();

        var result = await RunHookAsync(Input(tool, new { command = "echo hi" }), state, approval);

        Assert.NotNull(result);
        Assert.Equal("deny", result!.PermissionDecision);
        Assert.Contains("submit_response", result.PermissionDecisionReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Plan_waiting_on_catalog_is_denied_when_submit_closes_the_turn_meanwhile()
    {
        // A one-shot plan whose first step lacks "az" waits on the catalog. While it
        // waits, submit_response closes the turn. The plan must not render a second outcome.
        var lookup = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var outcomes = 0;
        var state = new SessionState
        {
            IsOneShot = true,
            IsCliFlavor = true,
            ReadCommandCatalog = _ => lookup.Task,
            OnOutcomeShown = _ => outcomes++,
        };
        var approval = new ConsoleApprovalPrompt();
        var plan = new
        {
            issues = new[] { "x" },
            steps = new[] { new { id = 1, label = "Create RG", cmd = "group create -n foo -l eastus" } },
        };

        var prevOut = Console.Out;
        Console.SetOut(TextWriter.Null);
        try
        {
            var planTask = PreToolUseHook.HandleAsync(Input("propose_plan", plan), az, approval, state, CancellationToken.None);
            Assert.False(planTask.IsCompleted);

            var submit = await PreToolUseHook.HandleAsync(
                Input("submit_response", new { status = "completed", message = "done" }), az, approval, state, CancellationToken.None);
            Assert.Equal("allow", submit!.PermissionDecision);

            lookup.SetResult("[]");
            var result = await planTask;

            Assert.Equal("deny", result!.PermissionDecision);
            Assert.Equal(1, outcomes);
            Assert.Null(state.LastPlanSteps);
        }
        finally
        {
            Console.SetOut(prevOut);
        }
    }
}
