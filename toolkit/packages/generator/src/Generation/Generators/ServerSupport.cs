using Ace.Generator.Model.Ir;

namespace Ace.Generator.Generators;

/// <summary>
/// Shared resolution of an IR server definition to the endpoint generated code builds URIs from.
/// </summary>
/// <remarks>
/// The endpoint is deliberately <em>not</em> baked into each processor's URI. The generated
/// <c>ProcessorBase</c> exposes it as an <c>Endpoint</c> member derived from the IR, and every
/// request URI interpolates that — so one override redirects the whole extension (a sovereign
/// cloud, a test host, a profile-resolved plane) instead of requiring a regeneration.
/// <para>
/// Nothing here invents a value. A server parameter the spec gives no default for stays a hole in
/// the endpoint and surfaces as a <em>public</em> input on the base, because only the caller can
/// supply it — that is the normal shape for data planes (<c>https://{vaultName}.vault.azure.net</c>).
/// </para>
/// </remarks>
internal static class ServerSupport
{
    /// <summary>Name of the base's endpoint seam, interpolated into every generated URI.</summary>
    public const string EndpointMember = "Endpoint";

    /// <summary>
    /// An endpoint resolved from an IR server: the text a URI interpolates, plus the server
    /// parameters left without a default, which the caller has to supply.
    /// </summary>
    public sealed record ResolvedEndpoint(string Text, IReadOnlyList<string> Inputs)
    {
        /// <summary>True when the spec pinned the endpoint outright.</summary>
        public bool IsConstant => Inputs.Count == 0;

        /// <summary>C# for the endpoint: a plain literal, or an interpolated string over its inputs.</summary>
        public string Expression => IsConstant ? Quote(Text) : "$" + Quote(Text);
    }

    /// <summary>
    /// Resolve a server to its endpoint, or <c>null</c> when it declares none. A missing endpoint
    /// is already an error that skips the operation, so callers never have to substitute one.
    /// </summary>
    public static ResolvedEndpoint? Resolve(IRServer? server) => server switch
    {
        IRServerLiteral literal when !string.IsNullOrEmpty(literal.Url) => new ResolvedEndpoint(literal.Url, []),
        IRServerParameterized parameterized => ResolveTemplate(parameterized),
        _ => null,
    };

    /// <summary>
    /// The endpoint <c>ProcessorBase.Endpoint</c> is derived from: whichever the document's servers
    /// most agree on, ties broken on the endpoint text so the choice never depends on document
    /// order. <c>null</c> when the document declares no usable server at all — then there is
    /// nothing to derive, and the base makes the endpoint itself a public input.
    /// </summary>
    public static ResolvedEndpoint? Dominant(TransformedIR ir) =>
        ir.Servers
            .Select(definition => Resolve(definition.Endpoint))
            .Where(endpoint => endpoint is not null)
            .GroupBy(endpoint => endpoint!.Text, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.First())
            .FirstOrDefault();

    /// <summary>
    /// Every server parameter across the document that the spec left without a default, as C#
    /// member names. These become public inputs on the base — shared, because a server's parameters
    /// mean the same thing for every operation that uses it.
    /// </summary>
    public static IReadOnlyList<string> Inputs(TransformedIR ir) =>
        [.. ir.Servers
            .Select(definition => Resolve(definition.Endpoint))
            .Where(endpoint => endpoint is not null)
            .SelectMany(endpoint => endpoint!.Inputs)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    private static ResolvedEndpoint ResolveTemplate(IRServerParameterized server)
    {
        string url = server.Template ?? string.Empty;
        var inputs = new List<string>();

        foreach (IRServerParameter parameter in server.Parameters)
        {
            string member = parameter.Name.Identifier();
            string? declaredDefault = (parameter.DefaultValue as IRLiteralValueString)?.Value;

            // A default is spec truth, so it is substituted; without one the parameter stays a
            // hole that the base fills from a public input.
            if (declaredDefault is null && !inputs.Contains(member, StringComparer.Ordinal))
            {
                inputs.Add(member);
            }

            url = url.Replace("{" + parameter.WireName + "}", declaredDefault ?? "{" + member + "}");
        }

        return new ResolvedEndpoint(url, inputs);
    }

    /// <summary>
    /// Quote for C# source. Braces are left alone: in an interpolated endpoint they are the holes.
    /// </summary>
    private static string Quote(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
