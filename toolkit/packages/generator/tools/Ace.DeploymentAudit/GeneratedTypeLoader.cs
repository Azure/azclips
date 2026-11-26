using Azure.Bicep.Types;
using Azure.Bicep.Types.Concrete;
using Azure.Bicep.Types.Index;
using Azure.Bicep.Types.Serialization;

namespace Ace.DeploymentAudit;

public sealed class GeneratedTypeLoader : TypeLoader
{
    private readonly byte[] definitions;
    private readonly byte[] index;

    public GeneratedTypeLoader(byte[] definitions)
    {
        this.definitions = definitions;
        using var stream = new MemoryStream(definitions, writable: false);
        TypeBase[] types = TypeSerializer.Deserialize(stream);
        var resources = new Dictionary<string, CrossFileTypeReference>(StringComparer.Ordinal);
        for (int position = 0; position < types.Length; position++)
        {
            if (types[position] is not ResourceType resource) continue;
            if (!resources.TryAdd(resource.Name, new("types.json", position)))
                throw new AuditInputException("generated-resource-ambiguous", $"Duplicate generated resource '{resource.Name}'.");
        }
        if (resources.Count == 0)
            throw new AuditInputException("generated-resource-missing", "Generated type JSON contains no resource definitions.");
        using var indexStream = new MemoryStream();
        TypeSerializer.SerializeIndex(indexStream, new TypeIndex(resources,
            new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<CrossFileTypeReference>>>(), [], null, null));
        index = indexStream.ToArray();
    }

    protected override Stream GetContentStreamAtPath(string path) => new MemoryStream(path switch
    {
        "types.json" => definitions,
        "index.json" => index,
        _ => throw new FileNotFoundException($"Unknown generated catalogue file '{path}'."),
    }, writable: false);
}
