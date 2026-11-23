// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

extern alias CmdletsKeyVault;
extern alias CmdletsMsGraph;

using System.Collections;
using System.Management.Automation;
using System.Reflection;
using System.Security;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Engine;
using Moq;
using static Cmdlets.Tests.Commands.CommandTestHelpers;
using AccountsCmdlets = Microsoft.Azure.Azclips.Cmdlets.Accounts;
using AccountsIndex = Microsoft.Azure.Azclips.Extensions.Accounts.ExtensionIndex;
using KeyVaultCmdlets = CmdletsKeyVault::Microsoft.Azure.Azclips.Cmdlets.KeyVault;
using KeyVaultIndex = Microsoft.Azure.Azclips.Extensions.KeyVault.ExtensionIndex;
using MsGraphCmdlets = CmdletsMsGraph::Microsoft.Azure.Azclips.Cmdlets.MsGraph;
using ManagedServiceIdentityCmdlets = Microsoft.Azure.Azclips.Cmdlets.ManagedServiceIdentity;
using ManagedServiceIdentityIndex = Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.ExtensionIndex;
using MsGraphIndex = Microsoft.Azure.Azclips.Extensions.MsGraph.ExtensionIndex;
using ResourcesCmdlets = Microsoft.Azure.Azclips.Cmdlets.Resources;
using ResourcesIndex = Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex;

namespace Cmdlets.Tests.Commands;

/// <summary>
/// Runs each listed module's cmdlets in PowerShell through the real engine and checks the error PowerShell's binder or
/// the engine gives for each rejected input, with cases generated from the cmdlet types. The wire mock is strict with no
/// responses, so each test also proves no request was sent. Because the cases are read from the cmdlets' attributes, a
/// parameter that stops being mandatory or a changed limit removes or changes its case instead of failing; pin limits that
/// matter in the module's own tests.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public sealed class CmdletParameterTests : IDisposable
{
    private const string Value = "value";

    // Add a module here to give every cmdlet in its assembly every check in this file. Accounts is included even though
    // its CLI commands are not in the CLI version of this file: these errors come from PowerShell's binder, so they never
    // reach the Accounts processors that need services from AzclipsServiceProvider.
    private static readonly (string Module, Assembly Extension, Type[] Cmdlets)[] s_modules =
    [
        ("KeyVault", typeof(KeyVaultIndex).Assembly, CmdletsIn(typeof(KeyVaultCmdlets.GetAzKeyVaultSecretCmdlet).Assembly)),
        ("MsGraph", typeof(MsGraphIndex).Assembly, CmdletsIn(typeof(MsGraphCmdlets.GetAzMsGraphUserCmdlet).Assembly)),
        ("ManagedServiceIdentity", typeof(ManagedServiceIdentityIndex).Assembly, CmdletsIn(typeof(ManagedServiceIdentityCmdlets.GetAzUserAssignedIdentityCmdlet).Assembly)),
        ("Resources", typeof(ResourcesIndex).Assembly, CmdletsIn(typeof(ResourcesCmdlets.NewResourceGroupCmdlet).Assembly)),
        ("Accounts", typeof(AccountsIndex).Assembly, CmdletsIn(typeof(AccountsCmdlets.ConnectAccountCmdlet).Assembly)),
    ];

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private Engine? _engine;

    public void Dispose()
    {
        AzclipsServiceProvider.Dispose();
        _engine?.Dispose();
    }

    // One case per mandatory parameter in each parameter set of each cmdlet: that parameter is left out and every other
    // mandatory parameter of the set is given a value, plus any optional parameters needed to make PowerShell pick that
    // set, so the binder reports exactly the one that is missing. A set PowerShell cannot be made to pick is skipped.
    public static TheoryData<string, Type, string[], string> GetMissingMandatoryParameterCases()
    {
        var data = new TheoryData<string, Type, string[], string>();
        foreach ((string module, _, Type[] cmdlets) in s_modules)
        {
            foreach (Type cmdlet in cmdlets)
            {
                foreach (string set in ParameterSets(cmdlet))
                {
                    foreach (PropertyInfo missing in Parameters(cmdlet).Where(p => IsMandatory(p, set)))
                    {
                        if (SuppliedParameters(cmdlet, set, missing: missing) is { } supplied)
                        {
                            data.Add(module, cmdlet, supplied, missing.Name);
                        }
                    }
                }
            }
        }

        return data;
    }

    // The single-case checks below each use, per module, the first cmdlet in list order with a suitable parameter and
    // the first parameter set that has it. The set's other mandatory parameters are given a value, so PowerShell's binder
    // or the engine rejects the arguments before the extension sees them. A module with no suitable parameter is skipped.

    public static TheoryData<string, Type, string[]> GetUnknownParameterCases()
    {
        var data = new TheoryData<string, Type, string[]>();
        foreach ((string module, Type cmdlet, _, string set) in FirstPerModule(_ => true))
        {
            data.Add(module, cmdlet, SuppliedParameters(cmdlet, set)!);
        }

        return data;
    }

    public static TheoryData<string, Type, string[]> GetMissingProfileCases()
    {
        var data = new TheoryData<string, Type, string[]>();
        foreach ((string module, Type cmdlet, _, string set) in FirstPerModule(p => p.Name == "Profile" && p.PropertyType == typeof(string)))
        {
            data.Add(module, cmdlet, SuppliedParameters(cmdlet, set)!);
        }

        return data;
    }

    public static TheoryData<string, Type, string[], string> GetBlankValueForNotNullOrEmptyCases() =>
        CasesFor(p => p.PropertyType == typeof(string) && p.IsDefined(typeof(ValidateNotNullOrEmptyAttribute)));

    // int? too: PowerShell converts to the underlying System.Int32 and gives the same message.
    public static TheoryData<string, Type, string[], string> GetNonIntegerValueCases() =>
        CasesFor(p => (Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType) == typeof(int));

    public static TheoryData<string, Type, string[], string> GetNonBooleanValueCases() => CasesFor(p => p.PropertyType == typeof(bool));

    public static TheoryData<string, Type, string[], string> GetNonDateValueCases() => CasesFor(p => p.PropertyType == typeof(DateTimeOffset));

    public static TheoryData<string, Type, string[], string, int> GetValueAboveRangeCases()
    {
        var data = new TheoryData<string, Type, string[], string, int>();
        foreach ((string module, Type cmdlet, PropertyInfo parameter, string set) in FirstPerModule(p => p.IsDefined(typeof(ValidateRangeAttribute))))
        {
            data.Add(
                module,
                cmdlet,
                SuppliedParameters(cmdlet, set, tested: parameter)!,
                parameter.Name,
                Convert.ToInt32(parameter.GetCustomAttribute<ValidateRangeAttribute>()!.MaxRange));
        }

        return data;
    }

    public static TheoryData<string, Type, string[], string, string> GetValueNotInSetCases()
    {
        var data = new TheoryData<string, Type, string[], string, string>();
        foreach ((string module, Type cmdlet, PropertyInfo parameter, string set) in FirstPerModule(p => p.IsDefined(typeof(ValidateSetAttribute))))
        {
            data.Add(
                module,
                cmdlet,
                SuppliedParameters(cmdlet, set, tested: parameter)!,
                parameter.Name,
                string.Join(",", parameter.GetCustomAttribute<ValidateSetAttribute>()!.ValidValues));
        }

        return data;
    }

    // ── missing mandatory parameters ───────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetMissingMandatoryParameterCases))]
    public void Invoke_MissingMandatoryParameter_ThrowsParameterBindingException(
        string module, Type cmdlet, string[] suppliedParameters, string missingParameter)
    {
        UseEngineFor(module);
        using PowerShell shell = CreateShell(cmdlet);
        AddValidValues(shell, cmdlet, suppliedParameters);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            $"Cannot process command because of one or more missing mandatory parameters: {missingParameter}.",
            Assert.IsType<ParameterBindingException>(exception).Message);
    }

    // ── shared PowerShell binder and engine checks ─────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetUnknownParameterCases))]
    public void Invoke_UnknownParameter_ThrowsParameterBindingException(string module, Type cmdlet, string[] suppliedParameters)
    {
        UseEngineFor(module);
        using PowerShell shell = CreateShell(cmdlet);
        AddValidValues(shell, cmdlet, suppliedParameters);
        shell.AddParameter("NotAParameter", Value);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "A parameter cannot be found that matches parameter name 'NotAParameter'.",
            Assert.IsType<ParameterBindingException>(exception).Message);
    }

    [Theory]
    [MemberData(nameof(GetMissingProfileCases))]
    public void Invoke_MissingProfile_ThrowsAzclipsErrorException(string module, Type cmdlet, string[] suppliedParameters)
    {
        UseEngineFor(module);
        using PowerShell shell = CreateShell(cmdlet);
        AddValidValues(shell, cmdlet, suppliedParameters);
        shell.AddParameter("Profile", "missing");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Profile 'missing' does not exist.",
            Assert.IsType<AzclipsErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // The [Validate*] failures are PowerShell's internal ParameterBindingValidationException, so these accept any
    // ParameterBindingException.
    [Theory]
    [MemberData(nameof(GetBlankValueForNotNullOrEmptyCases))]
    public void Invoke_BlankValueForNotNullOrEmpty_ThrowsParameterBindingException(
        string module, Type cmdlet, string[] suppliedParameters, string parameter)
    {
        UseEngineFor(module);
        using PowerShell shell = CreateShell(cmdlet);
        AddValidValues(shell, cmdlet, suppliedParameters);
        shell.AddParameter(parameter, "");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            $"Cannot validate argument on parameter '{parameter}'. The argument is null or empty. Provide an argument that is not null or empty, and then try the command again.",
            Assert.IsAssignableFrom<ParameterBindingException>(exception).Message);
    }

    [Theory]
    [MemberData(nameof(GetValueAboveRangeCases))]
    public void Invoke_ValueAboveRange_ThrowsParameterBindingException(
        string module, Type cmdlet, string[] suppliedParameters, string parameter, int maximum)
    {
        int valueAboveMaximum = maximum + 1;
        UseEngineFor(module);
        using PowerShell shell = CreateShell(cmdlet);
        AddValidValues(shell, cmdlet, suppliedParameters);
        shell.AddParameter(parameter, valueAboveMaximum);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            $"Cannot validate argument on parameter '{parameter}'. The {valueAboveMaximum} argument is greater than the maximum allowed range of {maximum}. Supply an argument that is less than or equal to {maximum} and then try the command again.",
            Assert.IsAssignableFrom<ParameterBindingException>(exception).Message);
    }

    [Theory]
    [MemberData(nameof(GetValueNotInSetCases))]
    public void Invoke_ValueNotInSet_ThrowsParameterBindingException(
        string module, Type cmdlet, string[] suppliedParameters, string parameter, string allowedValues)
    {
        UseEngineFor(module);
        using PowerShell shell = CreateShell(cmdlet);
        AddValidValues(shell, cmdlet, suppliedParameters);
        shell.AddParameter(parameter, "Bogus");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            $"Cannot validate argument on parameter '{parameter}'. The argument \"Bogus\" does not belong to the set \"{allowedValues}\" specified by the ValidateSet attribute. Supply an argument that is in the set and then try the command again.",
            Assert.IsAssignableFrom<ParameterBindingException>(exception).Message);
    }

    // Only PowerShell's prefix is checked: the rest of the message is .NET's conversion text, which varies by culture.
    [Theory]
    [MemberData(nameof(GetNonIntegerValueCases))]
    public void Invoke_NonIntegerValue_ThrowsParameterBindingException(string module, Type cmdlet, string[] suppliedParameters, string parameter)
    {
        UseEngineFor(module);
        using PowerShell shell = CreateShell(cmdlet);
        AddValidValues(shell, cmdlet, suppliedParameters);
        shell.AddParameter(parameter, "ten");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.StartsWith(
            $"Cannot bind parameter '{parameter}'. Cannot convert value \"ten\" to type \"System.Int32\".",
            Assert.IsAssignableFrom<ParameterBindingException>(exception).Message,
            StringComparison.Ordinal);
    }

    // PowerShell's message ends with a space.
    [Theory]
    [MemberData(nameof(GetNonBooleanValueCases))]
    public void Invoke_NonBooleanValue_ThrowsParameterBindingException(string module, Type cmdlet, string[] suppliedParameters, string parameter)
    {
        UseEngineFor(module);
        using PowerShell shell = CreateShell(cmdlet);
        AddValidValues(shell, cmdlet, suppliedParameters);
        shell.AddParameter(parameter, "maybe");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            $"Cannot convert 'System.String' to the type 'System.Boolean' required by parameter '{parameter}'. ",
            Assert.IsType<ParameterBindingException>(exception).Message);
    }

    // Only PowerShell's prefix is checked: the rest of the message is .NET's conversion text, which varies by culture.
    [Theory]
    [MemberData(nameof(GetNonDateValueCases))]
    public void Invoke_NonDateValue_ThrowsParameterBindingException(string module, Type cmdlet, string[] suppliedParameters, string parameter)
    {
        UseEngineFor(module);
        using PowerShell shell = CreateShell(cmdlet);
        AddValidValues(shell, cmdlet, suppliedParameters);
        shell.AddParameter(parameter, "tomorrow");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.StartsWith(
            $"Cannot bind parameter '{parameter}'. Cannot convert value \"tomorrow\" to type \"System.DateTimeOffset\".",
            Assert.IsAssignableFrom<ParameterBindingException>(exception).Message,
            StringComparison.Ordinal);
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────

    // Registers a new engine for the module's extension, with the strict wire, as the engine the cmdlets resolve.
    private void UseEngineFor(string module)
    {
        _engine = CreateEngine(s_modules.Single(m => m.Module == module).Extension, _mockWire);
        RegisterEngine(_engine);
    }

    private void AddValidValues(PowerShell shell, Type cmdlet, string[] parameters)
    {
        foreach (string parameter in parameters)
        {
            shell.AddParameter(parameter, ValidValue(cmdlet.GetProperty(parameter)!));
        }
    }

    // A value PowerShell binds to the parameter without complaint: a [ValidateSet]'s first value, else one for its type.
    private object ValidValue(PropertyInfo parameter) =>
        parameter.GetCustomAttribute<ValidateSetAttribute>() is { } validateSet ? validateSet.ValidValues[0] : ValidValue(parameter.PropertyType);

    private object ValidValue(Type type) => (Nullable.GetUnderlyingType(type) ?? type) switch
    {
        Type t when t == typeof(string) || t == typeof(object) => Value,
        Type t when t == typeof(string[]) => new[] { Value },
        Type t when t == typeof(int) => 1,
        Type t when t == typeof(bool) || t == typeof(SwitchParameter) => true,
        Type t when t == typeof(DateTimeOffset) => DateTimeOffset.UnixEpoch.AddYears(10),
        Type t when t == typeof(Hashtable) => new Hashtable(),
        Type t when t == typeof(SecureString) => ToSecureString(Value),
        Type t => throw new NotSupportedException($"Add a valid value for parameters of type {t} to {nameof(ValidValue)}."),
    };

    // The cmdlet's parameters in declaration order: public properties with at least one [Parameter].
    private static IEnumerable<PropertyInfo> Parameters(Type cmdlet) =>
        cmdlet.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.IsDefined(typeof(ParameterAttribute)));

    // The named parameter sets, default first, or the single implicit set when the cmdlet names none.
    private static string[] ParameterSets(Type cmdlet)
    {
        string? defaultSet = cmdlet.GetCustomAttribute<CmdletAttribute>()!.DefaultParameterSetName;
        string[] named = Parameters(cmdlet)
            .SelectMany(p => p.GetCustomAttributes<ParameterAttribute>())
            .Select(a => a.ParameterSetName)
            .Where(s => s != ParameterAttribute.AllParameterSets)
            .Distinct()
            .OrderBy(s => s == defaultSet ? 0 : 1)
            .ToArray();
        return named.Length > 0 ? named : [ParameterAttribute.AllParameterSets];
    }

    private static bool IsInSet(PropertyInfo parameter, string set) =>
        parameter.GetCustomAttributes<ParameterAttribute>().Any(a => a.ParameterSetName == set || a.ParameterSetName == ParameterAttribute.AllParameterSets);

    private static bool IsMandatory(PropertyInfo parameter, string set) =>
        parameter.GetCustomAttributes<ParameterAttribute>()
            .Any(a => a.Mandatory && (a.ParameterSetName == set || a.ParameterSetName == ParameterAttribute.AllParameterSets));

    // The parameters to give a valid value so PowerShell binds the cmdlet to the set: every mandatory parameter of the
    // set except the missing one, then optional parameters of the set, in order, while another set still fits them. The
    // tested parameter is given by the test itself, so it narrows the sets but is not returned. Only a missing parameter
    // needs PowerShell to pick the set (the other checks fail while binding, and the default set is listed first). When
    // more than one set still fits, PowerShell picks the default set, so with a missing parameter this is null when no
    // combination picks the set alone and the set is not the default.
    // Example: for Get-AzKeyVaultSecret's Get set with Name missing, this returns [Version]. -Version exists only in the
    // Get set, so PowerShell picks Get and reports that -Name is missing; with nothing supplied it would pick the default
    // List set, which has no mandatory parameters.
    private static string[]? SuppliedParameters(Type cmdlet, string set, PropertyInfo? missing = null, PropertyInfo? tested = null)
    {
        bool isDefault = set == cmdlet.GetCustomAttribute<CmdletAttribute>()!.DefaultParameterSetName;
        List<PropertyInfo> given = Parameters(cmdlet).Where(p => p != missing && (p == tested || IsMandatory(p, set))).ToList();
        List<string> otherSets = ParameterSets(cmdlet).Where(s => s != set && given.All(p => IsInSet(p, s))).ToList();
        foreach (PropertyInfo optional in Parameters(cmdlet).Where(p => p != missing && IsInSet(p, set) && !given.Contains(p)))
        {
            if (otherSets.Count == 0)
            {
                break;
            }

            if (otherSets.Any(s => !IsInSet(optional, s)))
            {
                given.Add(optional);
                otherSets.RemoveAll(s => !IsInSet(optional, s));
            }
        }

        return otherSets.Count == 0 || isDefault || missing is null ? given.Where(p => p != tested).Select(p => p.Name).ToArray() : null;
    }

    // For each module, the first cmdlet in list order with a parameter matching isMatch, that parameter and the first
    // parameter set that has it.
    private static IEnumerable<(string Module, Type Cmdlet, PropertyInfo Parameter, string Set)> FirstPerModule(Func<PropertyInfo, bool> isMatch)
    {
        foreach ((string module, _, Type[] cmdlets) in s_modules)
        {
            (Type Cmdlet, PropertyInfo Parameter) match = cmdlets
                .SelectMany(c => Parameters(c).Where(isMatch).Select(p => (c, p)))
                .FirstOrDefault();
            if (match.Cmdlet is not null)
            {
                yield return (module, match.Cmdlet, match.Parameter, ParameterSets(match.Cmdlet).First(s => IsInSet(match.Parameter, s)));
            }
        }
    }

    // One case per module: the set's other mandatory parameters and the parameter under test.
    private static TheoryData<string, Type, string[], string> CasesFor(Func<PropertyInfo, bool> isMatch)
    {
        var data = new TheoryData<string, Type, string[], string>();
        foreach ((string module, Type cmdlet, PropertyInfo parameter, string set) in FirstPerModule(isMatch))
        {
            data.Add(module, cmdlet, SuppliedParameters(cmdlet, set, tested: parameter)!, parameter.Name);
        }

        return data;
    }

    // Every cmdlet in the assembly, in name order so the generated cases are stable.
    private static Type[] CmdletsIn(Assembly cmdletAssembly) =>
        cmdletAssembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(PSCmdlet).IsAssignableFrom(t) && t.IsDefined(typeof(CmdletAttribute)))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToArray();
}