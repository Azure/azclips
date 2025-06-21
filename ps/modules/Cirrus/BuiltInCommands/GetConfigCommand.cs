using System.Collections.Generic;
using System.Management.Automation;
using Newtonsoft.Json;
using Cirrus.Core;

namespace Cirrus.BuiltInCommands;


[Cmdlet(VerbsCommon.Get, "CirrusConfig")]
public class GetConfigCommand : PSCmdlet
{
    protected override void ProcessRecord()
    {
        var result = PSWrapper.RunCommand("crs://azure/config/get", null);
        var jsonObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(result);
        WriteObject(jsonObject);
    }
}
