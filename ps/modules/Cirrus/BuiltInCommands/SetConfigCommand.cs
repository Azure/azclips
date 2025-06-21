using System.Collections.Generic;
using System.Management.Automation;
using Newtonsoft.Json;
using Cirrus.Core;

namespace Cirrus.BuiltInCommands;

[Cmdlet(VerbsCommon.Set, "CirrusConfig")]
public class SetConfigCommand : PSCmdlet
{
    [Parameter]
    public string Key { get; set; }

    [Parameter]
    public string Value { get; set; }

    protected override void ProcessRecord()
    {
        var result = PSWrapper.RunCommand("crs://azure/config/set", new KeyValuePair<string, object>[] { new KeyValuePair<string, object>($"value.{Key}", Value) });
        var jsonObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(result);
        WriteObject(jsonObject);
    }
}