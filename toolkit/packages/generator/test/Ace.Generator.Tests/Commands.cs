using Ace.Generator.Model.Ir;

namespace Ace.Generator.Tests;

/// <summary>
/// Compact builders for the command shapes the processor generator consumes.
/// </summary>
/// <remarks>
/// The generator reads <see cref="TransformedIR.Commands"/>, which a transform projects from
/// operations. These tests build that projection directly rather than reimplementing the
/// transform: what is under test is how a command is *spelled* in C#, not how it is derived.
/// </remarks>
internal static class Commands
{
    /// <summary>A literal URI segment.</summary>
    public static IRCommandUriSegment Literal(string text) => new IRCommandUriSegmentLiteral { Text = text };

    /// <summary>A URI segment that interpolates a command parameter by name.</summary>
    public static IRCommandUriSegment Param(string name) => new IRCommandUriSegmentParameter { Parameter = name };

    /// <summary>A query or header entry carrying a pinned value.</summary>
    public static IRCommandWireEntry Pinned(string wireName, string text) =>
        new() { WireName = wireName, Value = new IRCommandValueLiteral { Text = text } };

    /// <summary>A query or header entry supplied by a command parameter.</summary>
    public static IRCommandWireEntry Supplied(string wireName, string parameter) =>
        new() { WireName = wireName, Value = new IRCommandValueParameter { Parameter = parameter } };

    /// <summary>A path/query/header parameter.</summary>
    public static IRCommandParameter Parameter(
        string name,
        IRCommandParameterSource source,
        IRTypeRef? type = null,
        bool required = true,
        bool nullable = false,
        IRCommandParameterAccess? access = null) =>
        new()
        {
            Name = name,
            Source = source,
            Type = type ?? new IRTypeRefPrimitive { Name = IRTypeRefPrimitiveName.String },
            Required = required,
            Nullable = nullable,
            Access = access,
        };

    public static IRCommandParameterSource FromPath(IRId id) => new IRCommandParameterSourcePath { Parameter = id };

    public static IRCommandParameterSource FromQuery(IRId id) => new IRCommandParameterSourceQuery { Parameter = id };

    public static IRCommandParameterSource FromHeader(IRId id) => new IRCommandParameterSourceHeader { Parameter = id };

    public static IRCommandParameterSource FromBody(IRId property, params IRId[] via) =>
        new IRCommandParameterSourceBody { Property = property, Via = via };

    /// <summary>
    /// A single-call command. Defaults to a bodyless GET with no parameters, so a test states
    /// only the part it is about.
    /// </summary>
    public static IRCommand Atomic(
        string name,
        IRId server,
        IRId operation,
        IRCommandRequestMethod method = IRCommandRequestMethod.GET,
        IEnumerable<IRCommandUriSegment>? path = null,
        IEnumerable<IRCommandWireEntry>? query = null,
        IEnumerable<IRCommandWireEntry>? headers = null,
        IEnumerable<IRCommandParameter>? parameters = null,
        IRCommandRequestBody? body = null,
        IRCommandSurface? surface = null,
        IRLongRunningSemantics? longRunning = null,
        IRCommandPagination? pagination = null,
        string? subject = null,
        string? action = null,
        string? docs = null) =>
        new()
        {
            Id = new IRCommandId
            {
                Module = server.Module,
                Subject = subject ?? "Widgets",
                Action = action ?? name,
            },
            Name = name,
            Template = IRCommandTemplate.Atomic,
            Server = server,
            Steps =
            [
                new IRCommandStep
                {
                    Operation = operation,
                    Request = new IRCommandRequest
                    {
                        Method = method,
                        Path = [.. path ?? [Literal("/widgets")]],
                        Query = [.. query ?? []],
                        Headers = [.. headers ?? []],
                        Body = body,
                    },
                    Responses = [new IRCommandResponse { StatusCodes = [new IRStatusCodeExact { Code = 200 }] }],
                    LongRunning = longRunning,
                    Pagination = pagination,
                },
            ],
            Parameters = [.. parameters ?? []],
            Surface = surface ?? new IRCommandSurface(),
            Docs = docs,
        };

    /// <summary>A request body, projected (flat members cover it) or not.</summary>
    public static IRCommandRequestBody Body(IRId model, bool projected) =>
        new()
        {
            Type = new IRTypeRefModel { Id = model },
            ContentType = "application/json",
            Projected = projected,
        };

    /// <summary>The processor's generic surface.</summary>
    public static IRCommandSurface Surface(
        IRTypeRef? request = null,
        IRTypeRef? response = null,
        IRTypeRef? output = null,
        bool paginated = false) =>
        new()
        {
            Request = request,
            Response = response,
            Output = output,
            Paginated = paginated,
        };
}
