using System.Collections.Generic;
using System.Management.Automation;
using Newtonsoft.Json;

namespace Accounts;

[Cmdlet(VerbsCommon.Get , "CirrusConfig")]
public class GetConfigCommand : PSCmdlet
{
    protected override void ProcessRecord()
    {
        var result = CoreWrapper.Run("crs://azure/config/get", "{}");
        var jsonObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(result);
        WriteObject(jsonObject);
    }
}
