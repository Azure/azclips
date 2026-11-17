// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

extern alias CmdletsCompute;

using System.Collections;
using System.Text.Json;
using VmCmdletArgumentConverter = CmdletsCompute::Microsoft.Azure.Azclips.Cmdlets.Compute.VmCmdletArgumentConverter;

namespace Cmdlets.Tests;

public class VmCmdletArgumentConverterTests
{
    [Fact]
    public void ToTagsInput_PacksTheHashtableAsOneJsonObject()
    {
        var tagsInput = VmCmdletArgumentConverter.ToTagsInput(new Hashtable
        {
            ["env"] = "prod",
            ["filter"] = "a=b c",
        });

        var json = Assert.Single(tagsInput);
        var tags = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
        Assert.Equal(2, tags.Count);
        Assert.Equal("prod", tags["env"]);
        Assert.Equal("a=b c", tags["filter"]);
    }

    [Fact]
    public void ToTagsInput_ConvertsNullValuesToEmptyStrings()
    {
        var json = Assert.Single(VmCmdletArgumentConverter.ToTagsInput(new Hashtable { ["env"] = null }));

        Assert.Equal(string.Empty, JsonSerializer.Deserialize<Dictionary<string, string>>(json)!["env"]);
    }

    [Fact]
    public void ToTagsInput_ConvertsOtherValuesWithToString()
    {
        var json = Assert.Single(VmCmdletArgumentConverter.ToTagsInput(new Hashtable { ["count"] = 3, ["enabled"] = true }));

        var tags = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
        Assert.Equal("3", tags["count"]);
        Assert.Equal("True", tags["enabled"]);
    }

    [Fact]
    public void ToTagsInput_EmptyHashtable_IsAnEmptyJsonObject()
    {
        Assert.Equal(["{}"], VmCmdletArgumentConverter.ToTagsInput(new Hashtable()));
    }
}
