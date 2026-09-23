using Ace.Generator.Generators;
using Ace.Generator.Model.Ir;
using Xunit;

namespace Ace.Generator.Tests;

/// <summary>
/// Identity keying and lookup. <see cref="IrIds.Key"/> exists because
/// <see cref="IrIds.Serialize"/> is a display form that flattens namespace segments, so an
/// index keyed on it would conflate ids <see cref="IrIds.Equal"/> treats as different.
/// </summary>
public sealed class IrIndexTests
{
    private static IRId Id(string name, IReadOnlyList<string>? ns = null, string? iface = null) => new()
    {
        Module = "<root>",
        Namespace = ns ?? ["Contoso", "Inventory"],
        Interface = iface,
        Name = name,
    };

    [Fact]
    public void Key_Separates_Namespace_Segments_That_Serialize_Flattens_Together()
    {
        IRId split = Id("Widget", ["Contoso", "Inventory"]);
        IRId joined = Id("Widget", ["Contoso.Inventory"]);

        // The display form cannot tell these apart — which is why it is not the key.
        Assert.Equal(IrIds.Serialize(split), IrIds.Serialize(joined));
        Assert.False(IrIds.Equal(split, joined));
        Assert.NotEqual(IrIds.Key(split), IrIds.Key(joined));
    }

    [Fact]
    public void Key_Agrees_With_Equal_On_Interface_And_Type_Arguments()
    {
        Assert.Equal(IrIds.Key(Id("Widget")), IrIds.Key(Id("Widget")));

        IRId scoped = Id("Widget", ["Contoso"], "Inventory");
        Assert.False(IrIds.Equal(scoped, Id("Widget")));
        Assert.NotEqual(IrIds.Key(scoped), IrIds.Key(Id("Widget")));

        IRId absent = Id("Page");
        IRId empty = Id("Page");
        empty.TypeArgs = [];
        Assert.False(IrIds.Equal(absent, empty));
        Assert.NotEqual(IrIds.Key(absent), IrIds.Key(empty));

        IRId ordered = Id("Page");
        ordered.TypeArgs = [Id("A"), Id("B")];
        IRId swapped = Id("Page");
        swapped.TypeArgs = [Id("B"), Id("A")];
        Assert.NotEqual(IrIds.Key(ordered), IrIds.Key(swapped));
    }

    [Fact]
    public void Index_Resolves_Models_Properties_And_Their_Owner()
    {
        IRId widget = Id("Widget");
        IRId size = Id("Widget.size");
        var ir = new TransformedIR
        {
            SchemaVersion = "0.12.0",
            Models =
            [
                new IRModel
                {
                    Id = widget,
                    Name = "Widget",
                    Source = "<root>",
                    Properties =
                    [
                        new IRPropertyPrimitive
                        {
                            Id = size,
                            Name = "Size",
                            WireName = "size",
                            Type = new IRTypeRefPrimitive { Name = IRTypeRefPrimitiveName.Int64 },
                        },
                    ],
                },
            ],
            Operations = [],
            Commands = new IRCommandCollection(),
        };

        var index = new IrIndex(ir);

        Assert.Equal("Widget", index.Model(widget)?.Name);
        Assert.Equal("Size", index.Property(size)?.Name);
        Assert.Equal("Widget", index.OwnerOf(size)?.Name);

        // Unresolved lookups degrade to null rather than throwing.
        Assert.Null(index.Model(Id("Missing")));
        // And an id that only differs in namespace segmentation is a different id.
        Assert.Null(index.Model(Id("Widget", ["Contoso.Inventory"])));
    }

    [Fact]
    public void Index_Records_Duplicate_Server_Definitions_As_Ambiguous()
    {
        IRId server = Id("server");
        var ir = new TransformedIR
        {
            SchemaVersion = "0.12.0",
            Servers =
            [
                new IRServerDefinition { Id = server, Endpoint = new IRServerLiteral { Url = "https://a.example.com" } },
                new IRServerDefinition { Id = server, Endpoint = new IRServerLiteral { Url = "https://b.example.com" } },
            ],
            Models = [],
            Operations = [],
            Commands = new IRCommandCollection(),
        };

        var index = new IrIndex(ir);

        // A plain dictionary would keep the last one and lose the conflict entirely.
        Assert.NotNull(index.Server(server));
        Assert.True(index.IsServerAmbiguous(server));
        Assert.False(index.IsServerAmbiguous(Id("other")));
    }

    [Fact]
    public void Index_Skips_Entries_Without_An_Id_Instead_Of_Failing()
    {
        var ir = new TransformedIR
        {
            SchemaVersion = "0.12.0",
            Models = [new IRModel { Name = "Anonymous", Source = "<root>", Properties = [] }],
            Operations = [new IROperation { Name = "get", Source = "<root>" }],
            Commands = new IRCommandCollection(),
        };

        var index = new IrIndex(ir);

        Assert.Null(index.Model(Id("Anonymous")));
        Assert.Null(index.Operation(Id("get")));
    }
}
