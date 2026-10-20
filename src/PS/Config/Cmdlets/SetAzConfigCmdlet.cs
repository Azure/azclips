// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Config;

[Cmdlet(VerbsCommon.Set, "AzConfig", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium, DefaultParameterSetName = ValueParameterSet)]
[OutputType(typeof(string))]
public class SetAzConfigCmdlet : ConfigCmdletBase
{
    private const string ValueParameterSet = "Value";
    private const string AddParameterSet = "Add";
    private const string RemoveParameterSet = "Remove";

    [Parameter(Mandatory = true)]
    public string Key { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = ValueParameterSet)]
    [AllowEmptyString]
    public string Value { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = AddParameterSet, HelpMessage = "Entries to add to a list key (comma- or semicolon-separated).")]
    public string Add { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ParameterSetName = RemoveParameterSet, HelpMessage = "Entries to remove from a list key (comma- or semicolon-separated).")]
    public string Remove { get; set; } = string.Empty;

    [Parameter(Mandatory = false)]
    public SwitchParameter Force { get; set; }

    protected override void ProcessRecord()
    {
        var action = ParameterSetName switch
        {
            AddParameterSet => "add",
            RemoveParameterSet => "remove",
            _ => "set",
        };

        if (!ShouldProcess($"config key '{Key}'", action))
        {
            return;
        }

        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Key)))
        {
            parameters.Add("Key", Key);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Value)))
        {
            parameters.Add("Value", Value);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Add)))
        {
            parameters.Add("Add", Add);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Remove)))
        {
            parameters.Add("Remove", Remove);
        }

        if (MyInvocation.BoundParameters.ContainsKey(nameof(Force)))
        {
            parameters.Add("Force", Force.IsPresent);
        }

        ExecuteInvocation(parameters);
    }
}
