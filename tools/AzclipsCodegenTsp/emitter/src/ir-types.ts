// Intermediate representation types. Field names are PascalCase and mirror the
// C# classes in tools/AzclipsCodegen/Ir/Ir.cs so System.Text.Json (with default
// PropertyNameCaseInsensitive=true on the reader side) round-trips cleanly.
//
// Do not add fields here without adding them on the C# side too. The two files
// are a paired schema.

export interface Param {
    PsName: string;
    CliName: string;
    CsType: string;
    PsTypeMetadata: string;
    Description: string;
    Required: boolean;
    BodySlot: string; // "path" | "body-required" | "body-optional"
    Hidden: boolean;
    IsPassword: boolean;
    /**
     * Dotted JSON path from the request body root. Used by the processor to
     * re-assemble a flat cmdlet parameter dictionary into the nested JSON
     * envelope ARM expects. Empty for path/header/query params.
     * Examples:
     *   "location"
     *   "properties.storageSku"
     *   "properties.virtualMachineProfile.osProfile.adminUsername"
     */
    JsonPath: string;
}

export interface PathSlot {
    PsName: string;
    CsType: string;
    Description: string;
}

export interface Operation {
    Kind: string;
    HttpMethod: string;
    Path: string;
    OperationId: string;
    /** doc-comment on the tsp method (from `@doc(...)`) */
    Description: string;

    IsLro: boolean;
    LroFinalVia: string;

    ActionName: string;
    Depth: number;
    PathSlots: PathSlot[];
    BodyParams: Param[];

    ProcessorClass: string;
    Verb: string;
    ResourceLabel: string;
    CmdletClass: string;
    PsInvocationName: string;
    PsPublicName: string;
    CliInvocationName: string;

    ResponseClass: string;
    ExtraUsings: string[];
    RequestClass: string;
    UrlTemplate: string;
    IsSimpleParentOp: boolean;
    HasRg: boolean;
    HasName: boolean;
}

export interface ModelProp {
    Name: string;
    CsType: string;
    Required: boolean;
    XmlSummary: string;
}

export interface ModelClass {
    Name: string;
    Props: ModelProp[];
    XmlSummary: string;
}

export interface Resource {
    Name: string;
    Plural: string;
    CliLeaf: string;
    KebabSingular: string;
    NameParam: string;
    Parent: Resource | null;
    SingletonValue: string | null;
    Operations: Operation[];
}

export interface RpModel {
    Name: string;
    CliVerb: string;
    PsPrefix: string;
    ProviderNs: string;
    ApiVersion: string;
    Description: string;
    /** Always "TypeSpec" for IR produced by this emitter — distinguishes from
     *  the legacy Swagger/OpenAPI codegen pipeline, which sets this to
     *  "Swagger (OpenAPI 2.0)" on the C# side (see SwaggerParser.cs). */
    SourceKind: string;
    /** Repo-relative path to the compiled TypeSpec entrypoint, e.g.
     *  "specification/storage/Storage.Management". Passed in via the
     *  `source-spec` emitter option (see TspCompile.ps1); falls back to the
     *  TypeSpec compiler's resolved entrypoint file if the option is omitted. */
    SourceSpec: string;
    /** Exact git commit SHA of the azure-rest-api-specs checkout compiled to
     *  produce this IR, e.g. "3f7e0c0fcd7e22d7c8dcda816258ae05eabb14f8". Empty
     *  string if unknown (e.g. the specs checkout had uncommitted changes at
     *  compile time). Passed in via the `source-commit` emitter option (see
     *  TspCompile.ps1); no fallback value — unset means unknown, not HEAD. */
    SourceCommit: string;
    Resources: Resource[];
    Models: ModelClass[];
    Config: Record<string, unknown>;
}
