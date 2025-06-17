using System.Collections.Generic;
using System.Management.Automation;
using Newtonsoft.Json;
using Accounts;

namespace Databricks;

[Cmdlet(VerbsCommon.Set , "CirrusConfig")]
public class SetConfigCommand : PSCmdlet
{
    [Parameter]
    public string Key { get; set; }

    [Parameter]
    public string Value { get; set; }

    protected override void ProcessRecord()
    {
        var obj = new Dictionary<string, object>();
        obj[$"value.{Key}"] = Value;
        var result = CoreWrapper.Run("crs://azure/config/set", JsonConvert.SerializeObject(obj));
        var jsonObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(result);
        WriteObject(jsonObject);
    }
}
