using Ace.Generator.Model.Ir;
using Microsoft.CodeAnalysis.CSharp;

namespace Ace.Generator.Generators;

/// <summary>
/// Shared resolution of the IR's service authentication to the OAuth2 values the generated
/// <c>ProcessorBase</c> carries as <c>AuthorizationUrl</c> and <c>AuthScopes</c>.
/// </summary>
/// <remarks>
/// Only OAuth2 is carried: it is the scheme a token credential consumes. A spec with no OAuth2
/// scheme (no auth, API key, Basic/Bearer) resolves to <c>null</c> and the base declares neither
/// member. Nothing here invents a value — a flow set with no authorization URL (client
/// credentials carries only a token URL) leaves <c>AuthorizationUrl</c> null.
/// </remarks>
internal static class AuthSupport
{
    /// <summary>Name of the base member carrying the OAuth2 authorization URL.</summary>
    public const string AuthorizationUrlMember = "AuthorizationUrl";

    /// <summary>Name of the base member carrying the OAuth2 scopes.</summary>
    public const string ScopesMember = "AuthScopes";

    /// <summary>The OAuth2 values a service declares.</summary>
    public sealed record ResolvedAuth(string? AuthorizationUrl, IReadOnlyList<string> Scopes)
    {
        /// <summary>Stable identity, so sources declaring the same values group together.</summary>
        public string Key => (AuthorizationUrl ?? string.Empty) + "\n" + string.Join("\n", Scopes);
    }

    /// <summary>
    /// The first OAuth2 scheme across the auth options: its first declared authorization URL and
    /// the de-duplicated union of its flows' scopes, in declaration order. <c>null</c> when the
    /// auth declares no OAuth2 scheme.
    /// </summary>
    public static ResolvedAuth? Resolve(IRAuth? auth)
    {
        IRAuthSchemeOauth2? scheme = auth?.Options
            .SelectMany(option => option.Schemes)
            .OfType<IRAuthSchemeOauth2>()
            .FirstOrDefault();
        if (scheme is null) return null;

        string? url = scheme.Flows.Select(AuthorizationUrlOf).FirstOrDefault(value => !string.IsNullOrEmpty(value));
        IReadOnlyList<string> scopes =
        [
            .. scheme.Flows
                .SelectMany(flow => flow.Scopes)
                .Select(scope => scope.Value)
                .Where(value => !string.IsNullOrEmpty(value))
                .Distinct(StringComparer.Ordinal),
        ];
        return new ResolvedAuth(url, scopes);
    }

    /// <summary>
    /// The auth the base carries: whichever the document's sources most agree on, ties broken on
    /// the values so the choice never depends on document order. <c>null</c> when no source
    /// declares OAuth2.
    /// </summary>
    public static ResolvedAuth? Dominant(TransformedIR ir) =>
        ir.Sources
            .Select(source => Resolve(source.Auth))
            .Where(auth => auth is not null)
            .GroupBy(auth => auth!.Key, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.First())
            .FirstOrDefault();

    /// <summary>C# for the scopes: a collection expression of string literals.</summary>
    public static string ScopesExpression(ResolvedAuth auth) =>
        "[" + string.Join(", ", auth.Scopes.Select(Quote)) + "]";

    /// <summary>C# for the authorization URL: a string literal, or <c>null</c>.</summary>
    public static string AuthorizationUrlExpression(ResolvedAuth auth) =>
        auth.AuthorizationUrl is null ? "null" : Quote(auth.AuthorizationUrl);

    private static string? AuthorizationUrlOf(IROAuth2Flow flow) => flow switch
    {
        IROAuth2FlowAuthorizationCode code => code.AuthorizationUrl,
        IROAuth2FlowImplicit implicitFlow => implicitFlow.AuthorizationUrl,
        IROAuth2FlowPassword password => password.AuthorizationUrl,
        _ => null,
    };

    private static string Quote(string value) => SyntaxFactory.Literal(value).ToString();
}
