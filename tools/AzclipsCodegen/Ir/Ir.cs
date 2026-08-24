// Intermediate representation classes mirroring ir.py.
// Templates render from these; nothing beyond parsing reads swagger directly.
using System.Collections.Generic;

namespace AzclipsCodegen.Ir;

public sealed class Param
{
    public string PsName { get; set; } = "";
    public string CliName { get; set; } = "";
    public string CsType { get; set; } = "string?";
    public string PsTypeMetadata { get; set; } = "typeof(string)";
    public string Description { get; set; } = "";
    public bool Required { get; set; }
    public string BodySlot { get; set; } = "path";
    public bool Hidden { get; set; }
    public bool IsPassword { get; set; }
    public string JsonPath { get; set; } = "";

    // Scriban-friendly snake_case aliases so templates can use identical
    // property names to the Python originals.
    public string ps_name => PsName;
    public string cli_name => CliName;
    public string cs_type => CsType;
    public string ps_type_metadata => PsTypeMetadata;
    public string description => Description;
    public bool required => Required;
    public string body_slot => BodySlot;
    public bool hidden => Hidden;
    public bool is_password => IsPassword;
    public string json_path => JsonPath;
}

public sealed class PathSlot
{
    public string PsName { get; set; } = "";
    public string CsType { get; set; } = "string";
    public string Description { get; set; } = "";
    // The literal path segment PRECEDING this parameter (e.g. "redis" for
    // `.../redis/{name}`). Used post-hoc to disambiguate colliding "Name" slots.
    public string Segment { get; set; } = "";

    // Tuple-like indexer helpers so templates can write slot[0], slot[1], slot[2].
    public string this[int i] => i switch
    {
        0 => PsName,
        1 => CsType,
        2 => Description,
        _ => ""
    };
}

public sealed class Operation
{
    public string Kind { get; set; } = "";
    public string HttpMethod { get; set; } = "";
    public string Path { get; set; } = "";
    public string OperationId { get; set; } = "";

    public bool IsLro { get; set; }
    public string LroFinalVia { get; set; } = "";

    public string ActionName { get; set; } = "";
    public int Depth { get; set; } = 1;
    public List<PathSlot> PathSlots { get; set; } = new();
    public List<Param> BodyParams { get; set; } = new();

    public string ProcessorClass { get; set; } = "";
    public string Verb { get; set; } = "";
    public string ResourceLabel { get; set; } = "";
    // Raw resource C# class name (matches Resource.Name). Used by processors to call
    // <ResourceName>Uri() helpers on the ProcessorBase, since the base emits helpers
    // keyed by Resource.Name. Kept separate from ResourceLabel, which is the
    // user-visible PS noun (e.g. "RedisCache") and may diverge from the raw
    // resource name (e.g. "Redi") when ps_class_rename overrides are applied.
    public string ResourceName { get; set; } = "";
    public string CmdletClass { get; set; } = "";
    public string PsInvocationName { get; set; } = "";
    public string PsPublicName { get; set; } = "";
    public string CliInvocationName { get; set; } = "";

    public string ResponseClass { get; set; } = "";
    public List<string> ExtraUsings { get; set; } = new();
    public string RequestClass { get; set; } = "";
    public string UrlTemplate { get; set; } = "";
    /// <summary>
    /// True when the op is a parent-only CRUD (path shape:
    /// /subscriptions/{s}/resourceGroups/{rg}/providers/&lt;ns&gt;/&lt;plural&gt;/{name}).
    /// Only such ops use the base's &lt;Res&gt;Uri() helper; nested/singleton
    /// children fall through to inline url_template.
    /// </summary>
    public bool IsSimpleParentOp { get; set; }
    public bool HasRg { get; set; }
    public bool HasName { get; set; }

    // Scriban-friendly snake_case aliases.
    public string kind => Kind;
    public string http_method => HttpMethod;
    public string path => Path;
    public string operation_id => OperationId;
    public bool is_lro => IsLro;
    public string lro_final_via => LroFinalVia;
    public string action_name => ActionName;
    public int depth => Depth;
    public List<PathSlot> path_slots => PathSlots;
    public List<Param> body_params => BodyParams;
    public string processor_class => ProcessorClass;
    public string verb => Verb;
    public string resource_label => ResourceLabel;
    public string resource_name => ResourceName;
    public string cmdlet_class => CmdletClass;
    public string ps_invocation_name => PsInvocationName;
    public string ps_public_name => PsPublicName;
    public string cli_invocation_name => CliInvocationName;
    public string response_class => ResponseClass;
    public List<string> extra_usings => ExtraUsings;
    public string request_class => RequestClass;
    public string url_template => UrlTemplate;
    public bool is_simple_parent_op => IsSimpleParentOp;
    public bool has_rg => HasRg;
    public bool has_name => HasName;
}

public sealed class ModelProp
{
    public string Name { get; set; } = "";
    public string CsType { get; set; } = "";
    public bool Required { get; set; }
    public string XmlSummary { get; set; } = "";

    public string name => Name;
    public string cs_type => CsType;
    public bool required => Required;
    public string xml_summary => XmlSummary;
}

public sealed class ModelClass
{
    public string Name { get; set; } = "";
    public List<ModelProp> Props { get; set; } = new();
    public string XmlSummary { get; set; } = "";

    public string name => Name;
    public List<ModelProp> props => Props;
    public string xml_summary => XmlSummary;
}

public sealed class Resource
{
    public string Name { get; set; } = "";
    public string Plural { get; set; } = "";
    public string CliLeaf { get; set; } = "";
    public string KebabSingular { get; set; } = "";
    public string NameParam { get; set; } = "";
    public Resource? Parent { get; set; }
    public string? SingletonValue { get; set; }
    public List<Operation> Operations { get; set; } = new();

    public string name => Name;
    public string plural => Plural;
    public string cli_leaf => CliLeaf;
    public string kebab_singular => KebabSingular;
    public string name_param => NameParam;
    public string? singleton_value => SingletonValue;
    public List<Operation> operations => Operations;
}

public sealed class RpModel
{
    public string Name { get; set; } = "";
    public string CliVerb { get; set; } = "";
    public string PsPrefix { get; set; } = "";
    public string ProviderNs { get; set; } = "";
    public string ApiVersion { get; set; } = "";
    public string Description { get; set; } = "";
    // Provenance: which spec format produced this IR, and where the spec lives.
    // Populated by SwaggerParser.cs (SourceKind = "Swagger (OpenAPI 2.0)") or by
    // the @azclips/typespec-emitter (SourceKind = "TypeSpec"). Consumed by the
    // shared Scriban templates so generated file headers correctly attribute
    // their origin instead of hard-coding "swagger" regardless of pipeline.
    public string SourceKind { get; set; } = "";
    public string SourceSpec { get; set; } = "";
    // Exact git commit SHA of the azure-rest-api-specs checkout compiled to
    // produce this IR (empty for the legacy Swagger pipeline, or if the specs
    // checkout was not a clean git working tree — see TspCompile.ps1). This is
    // what makes SourceSpec reproducible/verifiable: SourceSpec alone only
    // names a directory, whose contents change over time; pairing it with
    // SourceCommit pins the exact spec revision that was actually compiled.
    public string SourceCommit { get; set; } = "";
    public List<Resource> Resources { get; set; } = new();
    public List<ModelClass> Models { get; set; } = new();
    public Dictionary<string, object?> Config { get; set; } = new();

    public string name => Name;
    public string cli_verb => CliVerb;
    public string ps_prefix => PsPrefix;
    public string provider_ns => ProviderNs;
    public string api_version => ApiVersion;
    public string description => Description;
    public string source_kind => SourceKind;
    public string source_spec => SourceSpec;
    public string source_commit => SourceCommit;
    public List<Resource> resources => Resources;
    public List<ModelClass> models => Models;
    public Dictionary<string, object?> config => Config;
}
