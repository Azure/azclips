using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Reflection;
using System.Security.Cryptography;
using Ace.Generator.Generators;
using Ace.Generator.Model.Ir;
using Azure.Bicep.Types;
using Azure.Bicep.Types.Concrete;

namespace Ace.DeploymentAudit;

public sealed record AuditDiagnostic(string Code, string Path, string Message);
public sealed record AuditCoverage(IReadOnlyList<string> ComparedPaths, IReadOnlyList<string> RecursiveReferences, IReadOnlyList<string> UnsupportedPaths);
public sealed record AuditResult(string Selection, string Status, IRId? Operation, IReadOnlyList<AuditDiagnostic> Diagnostics, AuditCoverage Coverage);
public sealed record AuditToolIdentity(string Name, string Version, string AssemblySha256);
public sealed record AuditReport(string SchemaVersion, string Catalogue, string CatalogueSha256, string IrSha256,
    IRDeploymentProvenance? Source, IReadOnlyList<AuditToolIdentity> Tools, string CompilerAssociation, IReadOnlyList<string> Limitations,
    IReadOnlyList<AuditResult> Results, IReadOnlyList<string>? ExtractionSelection = null)
{
    public int ExitCode => Results.Count > 0 && Results.All(result => result.Status == "compatible") ? 0 : 1;
}

public sealed class AuditInputException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public static class CompatibilityAudit
{
    public static AuditReport Run(TransformedIR ir, TypeLoader loader, IReadOnlyList<string> selections,
        string catalogue, string catalogueSha256, string irSha256)
    {
        if (selections.Count == 0) throw new AuditInputException("selection-required", "Select at least one resourceType@apiVersion descriptor.");
        var results = new List<AuditResult>();
        foreach (string selection in selections.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var walker = new Walker(ir);
            IRId? operation = null;
            try
            {
                IRDeploymentContract contract = ir.Deployment
                    ?? throw new AuditInputException("metadata-disabled", "Generate IR with deploymentContract enabled.");
                if (contract.SchemaVersion != "1.0.0") throw new AuditInputException("metadata-version", "Unsupported deployment contract schema version.");
                if (contract.Selection is { } extracted &&
                    (extracted.Count == 0 ||
                     extracted.Any(value => contract.Resources.Count(resource => Matches(value, resource.ResourceType, resource.ApiVersion)) != 1) ||
                     contract.Resources.Any(resource => extracted.Count(value => Matches(value, resource.ResourceType, resource.ApiVersion)) != 1)))
                    throw new AuditInputException("extraction-selection-invalid", "Explicit extraction selection must match deployment descriptors exactly and unambiguously.");
                var descriptors = contract.Resources.Where(resource => Matches(selection, resource.ResourceType, resource.ApiVersion)).ToArray();
                if (descriptors.Length != 1) throw new AuditInputException("selection-unresolved", $"'{selection}' resolves to {descriptors.Length} descriptors; select an existing unambiguous descriptor.");
                IRDeploymentResource descriptor = descriptors[0];
                operation = descriptor.Operation;
                walker.Validate();
                var references = loader.LoadTypeIndex().Resources.Where(entry =>
                    entry.Key.LastIndexOf('@') is int separator && separator > 0 &&
                    string.Equals(entry.Key[..separator], descriptor.ResourceType, StringComparison.OrdinalIgnoreCase) &&
                    entry.Key[(separator + 1)..] == descriptor.ApiVersion).ToArray();
                if (references.Length != 1) throw new AuditInputException("catalogue-version-missing",
                    $"The pinned catalogue has no unambiguous '{descriptor.ResourceType}@{descriptor.ApiVersion}'. No alternate version was selected.");
                ResourceType resource = loader.LoadResourceType(references[0].Value);
                if (!Matches(resource.Name, descriptor.ResourceType, descriptor.ApiVersion))
                    throw new AuditInputException("catalogue-reference-stale", $"Catalogue reference resolved to '{resource.Name}'.");
                walker.Compare(descriptor, resource);
            }
            catch (AuditInputException error)
            {
                walker.Unsupported(error.Code, "$", error.Message);
            }
            catch (Exception error) when (error is IOException or JsonException or ArgumentException or IndexOutOfRangeException or InvalidCastException)
            {
                walker.Unsupported("catalogue-definition-unreadable", "$", $"Cannot resolve the selected catalogue definition: {error.Message}");
            }
            results.Add(walker.Result(selection, operation));
        }
        return new("1.0.0", catalogue, catalogueSha256, irSha256, ir.Deployment?.Provenance,
            [Identity(typeof(CompatibilityAudit).Assembly), Identity(typeof(TypeLoader).Assembly)], "unverified",
            ["No template compilation or runtime equivalence was checked.",
             "Permissions, deployment transport, query/header behavior, outputs, existing resources and live regression gates remain unchecked."],
            results, ir.Deployment?.Selection);
    }

    private static bool Matches(string selection, string resourceType, string version)
    {
        int separator = selection.LastIndexOf('@');
        return separator > 0 && string.Equals(selection[..separator], resourceType, StringComparison.OrdinalIgnoreCase) &&
            selection[(separator + 1)..] == version;
    }

    private static AuditToolIdentity Identity(Assembly assembly) => new(
        assembly.GetName().Name ?? throw new AuditInputException("tool-identity-missing", "Cannot identify the audit tool assembly."),
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? throw new AuditInputException("tool-version-missing", "Cannot identify the audit tool version."),
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location))));

    private sealed class Reference(TypeBase type) : ITypeReference
    {
        public TypeBase Type { get; } = type;
    }

    private sealed class Walker(TransformedIR ir)
    {
        private IrIndex? index;
        private readonly List<AuditDiagnostic> diagnostics = [];
        private readonly HashSet<string> compared = new(StringComparer.Ordinal);
        private readonly HashSet<string> recursive = new(StringComparer.Ordinal);
        private readonly HashSet<string> unsupported = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<TypeBase>> active = new(StringComparer.Ordinal);
        private readonly Dictionary<string, IRDeploymentFacet> facets = new(StringComparer.Ordinal);
        private bool incompatible;

        public void Unsupported(string code, string path, string message)
        {
            compared.Remove(path);
            unsupported.Add(path);
            diagnostics.Add(new(code, path, message));
        }

        private void Difference(string code, string path, string message)
        {
            incompatible = true;
            diagnostics.Add(new(code, path, message));
        }

        public AuditResult Result(string selection, IRId? operation) => new(
            selection, unsupported.Count > 0 ? "unsupported" : incompatible ? "incompatible" : "compatible", operation,
            diagnostics.OrderBy(item => item.Path, StringComparer.Ordinal).ThenBy(item => item.Code, StringComparer.Ordinal)
                .ThenBy(item => item.Message, StringComparer.Ordinal).ToArray(),
            new(compared.Order(StringComparer.Ordinal).ToArray(), recursive.Order(StringComparer.Ordinal).ToArray(),
                unsupported.Order(StringComparer.Ordinal).ToArray()));

        public void Validate()
        {
            var contract = ir.Deployment!;
            foreach (IEnumerable<IRId> collection in new[] {
                ir.Models.Select(model => model.Id), ir.Enums.Select(value => value.Id),
                ir.Operations.Select(operation => operation.Id), ir.DiscriminatedUnions?.Select(union => union.Id) ?? [] })
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (IRId id in collection)
                {
                    if (id is null || string.IsNullOrEmpty(id.Module) || string.IsNullOrEmpty(id.Name))
                        throw new AuditInputException("source-id-missing", "A canonical declaration has no valid identity.");
                    if (!seen.Add(IrIds.Key(id))) throw new AuditInputException("source-id-ambiguous", $"Duplicate canonical identity {IrIds.Serialize(id)}.");
                }
            }
            index = new(ir);
            var targets = ir.Models.Select(model => model.Id)
                .Concat(ir.Models.SelectMany(model => model.Properties.Select(property => property.Id)))
                .Concat(ir.Operations.SelectMany(operation => operation.PathParameters.Concat(operation.QueryParameters).Concat(operation.HeaderParameters).Select(parameter => parameter.Id)))
                .Where(id => id is not null).Select(IrIds.Key).ToHashSet(StringComparer.Ordinal);
            foreach (IRDeploymentFacet facet in contract.Facets)
            {
                if (facet.Target is null || !targets.Contains(IrIds.Key(facet.Target)))
                    throw new AuditInputException("facet-reference-missing", "A supplemental facet has no canonical target.");
                if (!facets.TryAdd(FacetKey(facet.Target, facet.TypePath), facet))
                    throw new AuditInputException("facet-reference-ambiguous", $"Duplicate supplemental facet for {IrIds.Serialize(facet.Target)}.");
            }
        }

        private static string FacetKey(IRId id, IReadOnlyList<IRDeploymentFacetTypePath> path) => $"{IrIds.Key(id)}:{string.Join("/", path)}";
        private IRDeploymentFacet? Facet(IRId id, IReadOnlyList<IRDeploymentFacetTypePath>? path = null) =>
            facets.GetValueOrDefault(FacetKey(id, path ?? []));

        private IRModel Model(IRId id) => index!.Model(id)
            ?? throw new AuditInputException("model-reference-missing", $"Missing canonical model {IrIds.Serialize(id)}.");

        private static IRTypeRef PropertyType(IRProperty property) => property switch
        {
            IRPropertyPrimitive value => value.Type,
            IRPropertyObject value => value.Type,
            IRPropertyCollection value => value.Type,
            IRPropertyEnum value => value.Type,
            IRPropertyUnion value => value.Type,
            _ => throw new AuditInputException("property-shape-unsupported", $"Unsupported canonical property {property.GetType().Name}.")
        };

        private static bool Writable(IRProperty property) =>
            property.Visibility is null || property.Visibility.Contains(IRVisibility.Create) || property.Visibility.Contains(IRVisibility.Update);
        private static TypeBase Resolve(ITypeReference reference) => reference?.Type
            ?? throw new AuditInputException("catalogue-reference-missing", "A catalogue type reference could not be resolved.");

        public void Compare(IRDeploymentResource descriptor, ResourceType resource)
        {
            IROperation operation = index!.Operation(descriptor.Operation)
                ?? throw new AuditInputException("operation-reference-missing", "The selected create operation has no canonical definition.");
            if (operation.Method != IROperationMethod.PUT || operation.RequestBody?.Type is not IRTypeRefModel body)
                throw new AuditInputException("operation-shape", "The descriptor must reference a PUT with a model body.");
            IRSource[] sources = ir.Sources.Where(source => source.Id == operation.Source).ToArray();
            if (sources.Length != 1) throw new AuditInputException("operation-source-unresolved", "The canonical operation source is missing or ambiguous.");
            if (operation.ApiVersion?.Value != descriptor.ApiVersion || sources[0].ApiVersion?.Value != descriptor.ApiVersion)
                throw new AuditInputException("operation-version-stale", "Descriptor, source and operation API versions must agree exactly.");
            ScopeType scope = descriptor.WritableScope switch
            {
                IRDeploymentResourceWritableScope.Tenant => ScopeType.Tenant,
                IRDeploymentResourceWritableScope.ManagementGroup => ScopeType.ManagementGroup,
                IRDeploymentResourceWritableScope.Subscription => ScopeType.Subscription,
                IRDeploymentResourceWritableScope.ResourceGroup => ScopeType.ResourceGroup,
                IRDeploymentResourceWritableScope.Resource => ScopeType.Extension,
                _ => throw new AuditInputException("scope-unsupported", "Unknown deployment scope.")
            };
            compared.Add("$scope");
            if (scope != resource.WritableScopes) Difference("writable-scope", "$scope", $"Source scope {scope} differs from catalogue scope {resource.WritableScopes}.");
            int expectedScopes = scope switch { ScopeType.Tenant => 0, ScopeType.ResourceGroup => 2, _ => 1 };
            if (descriptor.ScopeParameters.Count != expectedScopes)
                throw new AuditInputException("scope-bindings-unresolved", "The descriptor does not have the canonical inputs required by its scope.");
            foreach (IRId parameter in descriptor.ScopeParameters)
                Input(operation, parameter);
            IRModel model = Model(body.Id);
            if (model.Polymorphism is not null)
            {
                Unsupported("resource-polymorphism-unsupported", "$", "Polymorphic resource envelopes require additional declaration normalization.");
                return;
            }
            if (Resolve(resource.Body) is not ObjectType native)
            {
                Unsupported("resource-envelope-unsupported", "$", "A resource envelope must be an ordinary catalogue object.");
                return;
            }
            var properties = model.Properties.Where(property => property.WireName != "name").ToArray();
            foreach (IRProperty identity in properties.Where(property => property.WireName is "id" or "type" or "apiVersion"))
                if (Writable(identity)) Unsupported("writable-identity-envelope", $"$.{identity.WireName}",
                    $"Writable API body identity '{identity.WireName}' requires an explicit value-preserving declaration binding; it cannot be silently omitted.");
            properties = properties.Where(property => property.WireName is not ("id" or "type" or "apiVersion")).ToArray();
            CheckProperties(properties, native.Properties.Where(entry => entry.Key != "name").ToDictionary(), "$");
            CheckAdditional(model.AdditionalProperties, native.AdditionalProperties, model.Id, "$");
            if (!native.Properties.TryGetValue("name", out ObjectTypeProperty? nativeName) ||
                nativeName.Flags.HasFlag(ObjectTypePropertyFlags.ReadOnly) || !nativeName.Flags.HasFlag(ObjectTypePropertyFlags.Required))
            {
                Difference("declaration-name", "$.name", "The catalogue declaration has no required writable name.");
                return;
            }
            if (descriptor.Name.Count == 0) throw new AuditInputException("name-binding-missing", "The descriptor has no name bindings.");
            if (descriptor.Name.Count == 1 && descriptor.Name[0] is IRDeploymentBindingParameter parameterBinding)
            {
                IRParameter input = Input(operation, parameterBinding.Parameter);
                Check(input.Type, Resolve(nativeName.Type), "$.name", input.Nullable, input.Id, []);
            }
            else if (descriptor.Name.All(binding => binding is IRDeploymentBindingLiteral))
            {
                string value = string.Join("/", descriptor.Name.Cast<IRDeploymentBindingLiteral>().Select(binding => binding.Value));
                compared.Add("$.name");
                TypeBase nameType = Resolve(nativeName.Type);
                if (nameType is StringType text)
                {
                    if (text.Pattern is not null)
                        Unsupported("literal-name-pattern-unsupported", "$.name", "A fixed name needs explicit catalogue pattern validation.");
                    if (value.Length < (text.MinLength ?? 0) || text.MaxLength is { } maxLength && value.Length > maxLength)
                        Difference("literal-name-constraint", "$.name", "The fixed declaration name violates catalogue length constraints.");
                }
                else CheckLiteral(new IRLiteralValueString { Value = value }, nameType, "$.name");
            }
            else
            {
                foreach (IRDeploymentBinding binding in descriptor.Name)
                {
                    if (binding is IRDeploymentBindingParameter parameter)
                    {
                        IRParameter input = Input(operation, parameter.Parameter);
                        if (input.Type is not IRTypeRefPrimitive { Name: IRTypeRefPrimitiveName.String, Format: null } || Facet(input.Id) is not null)
                            Unsupported("composed-name-constraints", "$.name", "Constrained or non-string name components cannot yet be compared as a qualified declaration name.");
                    }
                }
                Check(new IRTypeRefPrimitive { Name = IRTypeRefPrimitiveName.String }, Resolve(nativeName.Type), "$.name", false, descriptor.Operation, []);
            }
        }

        private static IRParameter Input(IROperation operation, IRId id)
        {
            IRParameter[] matches = operation.PathParameters.Where(parameter => IrIds.Equal(parameter.Id, id)).ToArray();
            if (matches.Length != 1 || matches[0].Optional || matches[0].Nullable)
                throw new AuditInputException("input-binding-unresolved", "A name/scope binding must resolve to one required, non-nullable canonical path parameter.");
            return matches[0];
        }

        private void CheckProperties(IReadOnlyList<IRProperty> source, IReadOnlyDictionary<string, ObjectTypeProperty> native, string path)
        {
            IRProperty[] writable = source.Where(Writable).ToArray();
            if (writable.GroupBy(property => property.WireName, StringComparer.Ordinal).Any(group => group.Count() != 1))
                throw new AuditInputException("wire-property-ambiguous", $"Duplicate writable wire properties at {path}.");
            var sourceProperties = writable.ToDictionary(property => property.WireName, StringComparer.Ordinal);
            var nativeProperties = native.Where(entry => !entry.Value.Flags.HasFlag(ObjectTypePropertyFlags.ReadOnly))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
            foreach (string key in sourceProperties.Keys.Union(nativeProperties.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                string child = $"{path}.{key}";
                compared.Add(child);
                if (!sourceProperties.TryGetValue(key, out IRProperty? property) || !nativeProperties.TryGetValue(key, out ObjectTypeProperty? nativeProperty))
                {
                    Difference("writable-property-membership", child, "Writable field membership differs.");
                    continue;
                }
                if (!property.Optional != nativeProperty.Flags.HasFlag(ObjectTypePropertyFlags.Required))
                    Difference("requiredness", child, "Writable field requiredness differs.");
                Check(PropertyType(property), Resolve(nativeProperty.Type), child, property.Nullable, property.Id, []);
            }
        }

        private void Check(IRTypeRef source, TypeBase native, string path, bool nullable, IRId target, IReadOnlyList<IRDeploymentFacetTypePath> typePath)
        {
            compared.Add(path);
            if (native is AnyType or BuiltInType)
            {
                Unsupported("catalogue-type-unsupported", path, "The catalogue shape cannot establish structural parity.");
                return;
            }
            bool nativeNullable = native is NullType || native is UnionType unionWithNull && unionWithNull.Elements.Any(reference => Resolve(reference) is NullType);
            if (nullable != nativeNullable) Difference("nullability", path, "Source and catalogue nullability differ.");
            if (native is UnionType union)
            {
                TypeBase[] nonNull = union.Elements.Select(Resolve).Where(type => type is not NullType).ToArray();
                if (nonNull.Length == 1) native = nonNull[0];
                else if (OpenStringUnion(nonNull) is { } text) native = text;
            }
            if (native is UnionType && StringValues(native) is null)
            {
                Unsupported("catalogue-union-unsupported", path, "This catalogue union cannot be normalized without changing its allowed values.");
                return;
            }
            IRDeploymentFacet? facet = Facet(target, typePath);
            if (facet is not null &&
                ((facet.MinItems is not null || facet.MaxItems is not null) && source is not IRTypeRefArray ||
                 (facet.MinLength is not null || facet.MaxLength is not null || facet.Pattern is not null) &&
                    source is not IRTypeRefPrimitive { Name: IRTypeRefPrimitiveName.String } and not IRTypeRefEnum ||
                 (facet.MinValue is not null || facet.MaxValue is not null || facet.MinValueExclusive is not null || facet.MaxValueExclusive is not null) &&
                    source is not IRTypeRefPrimitive { Name: not IRTypeRefPrimitiveName.String and not IRTypeRefPrimitiveName.Boolean } ))
            {
                Unsupported("facet-type-unsupported", path, "Supplemental constraints do not have a supported canonical type.");
                return;
            }
            if (facet?.Sensitive == true && native is not (StringType or ObjectType))
                Unsupported("sensitivity-unsupported", path, "The catalogue type cannot represent the source sensitivity annotation.");
            if (facet?.LiteralValue is not null)
            {
                if (facet.MinLength is not null || facet.MaxLength is not null || facet.Pattern is not null ||
                    facet.MinValue is not null || facet.MaxValue is not null || facet.MinValueExclusive is not null || facet.MaxValueExclusive is not null)
                {
                    Unsupported("literal-constraints-unsupported", path, "Exact literals with supplemental constraints require additional allowed-value normalization.");
                    return;
                }
                CheckLiteral(facet.LiteralValue, native, path);
                return;
            }
            switch (source)
            {
                case IRTypeRefPrimitive primitive:
                    CheckPrimitive(primitive, native, facet, path);
                    break;
                case IRTypeRefEnum enumeration:
                    CheckEnum(enumeration, native, facet, path);
                    break;
                case IRTypeRefArray array when native is ArrayType nativeArray:
                    Bounds(facet?.MinItems, nativeArray.MinLength, "minItems", path);
                    Bounds(facet?.MaxItems, nativeArray.MaxLength, "maxItems", path);
                    Check(array.Element, Resolve(nativeArray.ItemType), $"{path}[]", false, target, [.. typePath, IRDeploymentFacetTypePath.Element]);
                    break;
                case IRTypeRefDictionary dictionary when native is ObjectType nativeDictionary:
                    if ((facet?.Sensitive ?? false) != (nativeDictionary.Sensitive ?? false))
                        Difference("sensitivity", path, "Dictionary sensitivity differs.");
                    if (nativeDictionary.Properties.Any(entry => !entry.Value.Flags.HasFlag(ObjectTypePropertyFlags.ReadOnly)))
                        Difference("dictionary-fixed-fields", path, "A dictionary cannot gain fixed writable fields.");
                    if (nativeDictionary.AdditionalProperties is null) Difference("dictionary-closed", path, "The catalogue closes an open dictionary.");
                    else Check(dictionary.Value, Resolve(nativeDictionary.AdditionalProperties), $"{path}.*", false, target, [.. typePath, IRDeploymentFacetTypePath.Value]);
                    break;
                case IRTypeRefModel modelRef:
                    CheckModel(modelRef, native, facet, path);
                    break;
                case IRTypeRefUnion or IRTypeRefDiscriminatedUnion:
                    Unsupported("union-shape-unsupported", path, "Structural/enveloped source unions require a faithful union mapping; no any substitute was used.");
                    break;
                default:
                    Difference("type-shape", path, $"Source {source.GetType().Name} differs from catalogue {native.GetType().Name}.");
                    break;
            }
        }

        private void CheckModel(IRTypeRefModel source, TypeBase native, IRDeploymentFacet? facet, string path)
        {
            string key = IrIds.Key(source.Id);
            if (!active.TryGetValue(key, out HashSet<TypeBase>? types)) active[key] = types = new(ReferenceEqualityComparer.Instance);
            if (!types.Add(native)) { recursive.Add(path); return; }
            try
            {
                IRModel model = Model(source.Id);
                if (model.Polymorphism is { } polymorphism)
                {
                    if (native is not DiscriminatedObjectType discriminated)
                    {
                        Difference("discriminator-shape", path, "The catalogue has no matching discriminator structure.");
                        return;
                    }
                    IRProperty? discriminator = model.Properties.SingleOrDefault(property => IrIds.Equal(property.Id, polymorphism.Discriminator));
                    if (discriminator is null) throw new AuditInputException("discriminator-unresolved", "The canonical discriminator property is missing.");
                    if (discriminator.WireName != discriminated.Discriminator) Difference("discriminator-wire-name", path, "Discriminator wire names differ.");
                    if (!polymorphism.Subtypes.Select(subtype => subtype.Value).ToHashSet(StringComparer.Ordinal).SetEquals(discriminated.Elements.Keys))
                        Difference("discriminator-tags", path, "Discriminator tag membership differs.");
                    CheckProperties(model.Properties.Where(property => property.WireName != discriminator.WireName).ToArray(),
                        discriminated.BaseProperties.Where(entry => entry.Key != discriminated.Discriminator).ToDictionary(), path);
                    foreach (var subtype in polymorphism.Subtypes)
                    {
                        if (!discriminated.Elements.TryGetValue(subtype.Value, out ITypeReference? reference)) continue;
                        if (Resolve(reference) is not ObjectType element) { Unsupported("discriminator-element-unsupported", path, "A discriminator variant is not an object."); continue; }
                        var merged = discriminated.BaseProperties.Concat(element.Properties).GroupBy(entry => entry.Key, StringComparer.Ordinal)
                            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.Ordinal);
                        merged[discriminated.Discriminator] = new(new Reference(new StringLiteralType(subtype.Value)), ObjectTypePropertyFlags.Required, null);
                        var variant = new ObjectType(element.Name, merged, element.AdditionalProperties, element.Sensitive);
                        CheckModel(new IRTypeRefModel { Id = subtype.Type }, variant, null, $"{path}<{subtype.Value}>");
                    }
                    return;
                }
                if (native is not ObjectType nativeModel) { Difference("object-type", path, "The catalogue has no matching ordinary object."); return; }
                if ((facet?.Sensitive ?? false) != (nativeModel.Sensitive ?? false)) Difference("sensitivity", path, "Object sensitivity differs.");
                CheckProperties(model.Properties, nativeModel.Properties, path);
                CheckAdditional(model.AdditionalProperties, nativeModel.AdditionalProperties, model.Id, path);
            }
            finally { types.Remove(native); }
        }

        private void CheckAdditional(IRTypeRef? source, ITypeReference? native, IRId model, string path)
        {
            compared.Add($"{path}.*");
            if ((source is null) != (native is null)) Difference("object-openness", path, "Additional-property openness differs.");
            else if (source is not null && native is not null)
                Check(source, Resolve(native), $"{path}.*", false, model, [IRDeploymentFacetTypePath.AdditionalProperties]);
        }

        private void CheckPrimitive(IRTypeRefPrimitive source, TypeBase native, IRDeploymentFacet? facet, string path)
        {
            if (source.Name is IRTypeRefPrimitiveName.Any or IRTypeRefPrimitiveName.Void or IRTypeRefPrimitiveName.Bytes or
                IRTypeRefPrimitiveName.Float32 or IRTypeRefPrimitiveName.Float64 or IRTypeRefPrimitiveName.Decimal or IRTypeRefPrimitiveName.Decimal128)
            {
                Unsupported("primitive-unsupported", path, $"No faithful catalogue comparison for {source.Name}; floating point was not narrowed and unknown was not widened to any.");
                return;
            }
            if (source.Format is not null && source.Format != IRTypeRefPrimitiveFormat.Password)
            {
                Unsupported("format-unsupported", path, $"Catalogue types cannot establish lexical-format parity for {source.Format}.");
                return;
            }
            if (native is AnyType or BuiltInType) { Unsupported("catalogue-type-unsupported", path, "The catalogue shape cannot establish structural parity."); return; }
            if (source.Name == IRTypeRefPrimitiveName.String && native is StringType text)
            {
                Bounds(facet?.MinLength, text.MinLength, "minLength", path);
                Bounds(facet?.MaxLength, text.MaxLength, "maxLength", path);
                if (facet?.Pattern != text.Pattern) Difference("pattern", path, "String patterns differ; regex equivalence was not guessed.");
                if (((facet?.Sensitive ?? false) || source.Format == IRTypeRefPrimitiveFormat.Password) != (text.Sensitive ?? false))
                    Difference("sensitivity", path, "String sensitivity differs.");
            }
            else if (source.Name == IRTypeRefPrimitiveName.Boolean && native is BooleanType) { }
            else if (native is IntegerType integer && IntegerRange(source.Name) is { } range)
            {
                try
                {
                    BigInteger min = Bound(facet?.MinValue) ?? range.Min;
                    BigInteger max = Bound(facet?.MaxValue) ?? range.Max;
                    if (facet?.MinValueExclusive is { } exclusiveMin) min = BigInteger.Max(min, Bound(exclusiveMin)!.Value + 1);
                    if (facet?.MaxValueExclusive is { } exclusiveMax) max = BigInteger.Min(max, Bound(exclusiveMax)!.Value - 1);
                    min = BigInteger.Max(min, range.Min);
                    max = BigInteger.Min(max, range.Max);
                    BigInteger nativeMin = integer.MinValue ?? long.MinValue;
                    BigInteger nativeMax = integer.MaxValue ?? long.MaxValue;
                    if (min != nativeMin || max != nativeMax)
                        Difference("integer-range", path, FormattableString.Invariant(
                            $"Source {source.Name} effective range [{min}, {max}] differs from catalogue effective range [{nativeMin}, {nativeMax}]; unspecified catalogue endpoints use Bicep Int64 limits."));
                }
                catch (AuditInputException error) { Unsupported(error.Code, path, error.Message); }
            }
            else Difference("primitive-type", path, $"Source {source.Name} differs from catalogue {native.GetType().Name}.");
        }

        private static (BigInteger Min, BigInteger Max)? IntegerRange(IRTypeRefPrimitiveName name) => name switch
        {
            IRTypeRefPrimitiveName.Int8 => (sbyte.MinValue, sbyte.MaxValue),
            IRTypeRefPrimitiveName.Int16 => (short.MinValue, short.MaxValue),
            IRTypeRefPrimitiveName.Int32 => (int.MinValue, int.MaxValue),
            IRTypeRefPrimitiveName.Int64 => (long.MinValue, long.MaxValue),
            IRTypeRefPrimitiveName.Uint8 => (byte.MinValue, byte.MaxValue),
            IRTypeRefPrimitiveName.Uint16 => (ushort.MinValue, ushort.MaxValue),
            IRTypeRefPrimitiveName.Uint32 => (uint.MinValue, uint.MaxValue),
            IRTypeRefPrimitiveName.Uint64 => (ulong.MinValue, ulong.MaxValue),
            _ => null
        };

        private static BigInteger? Bound(string? value)
        {
            if (value is null) return null;
            if (BigInteger.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger parsed)) return parsed;
            throw new AuditInputException("numeric-bound-unsupported", $"Cannot normalize numeric bound '{value}' as an exact integer.");
        }

        private void Bounds(long? source, long? native, string name, string path)
        {
            if (name.StartsWith("min", StringComparison.Ordinal)) { source ??= 0; native ??= 0; }
            if (source != native) Difference($"constraint-{name}", path, $"{name} constraints differ.");
        }

        private void CheckLiteral(IRLiteralValue source, TypeBase native, string path)
        {
            if (source is IRLiteralValueString text)
            {
                string[]? values = StringValues(native);
                if (values is null || values.Length != 1 || values[0] != text.Value) Difference("literal-value", path, "Exact string literal membership differs.");
            }
            else Unsupported("literal-shape-unsupported", path, "This exact literal cannot yet be compared without widening it.");
        }

        private static string[]? StringValues(TypeBase native) => native switch
        {
            StringLiteralType literal => [literal.Value],
            UnionType union when union.Elements.All(reference => Resolve(reference) is StringLiteralType) =>
                union.Elements.Select(reference => ((StringLiteralType)Resolve(reference)).Value).Distinct(StringComparer.Ordinal).ToArray(),
            _ => null
        };

        private static StringType? OpenStringUnion(IReadOnlyList<TypeBase> variants)
        {
            StringType[] strings = variants.OfType<StringType>().ToArray();
            if (strings.Length != 1 || variants.Any(type => type is not (StringType or StringLiteralType)))
                return null;
            StringType text = strings[0];
            return text.MinLength is null or 0 && text.MaxLength is null && text.Pattern is null && text.Sensitive != true
                ? text : null;
        }

        private void CheckEnum(IRTypeRefEnum reference, TypeBase native, IRDeploymentFacet? facet, string path)
        {
            IREnum enumeration = index!.Enum(reference.Id)
                ?? throw new AuditInputException("enum-reference-missing", $"Missing canonical enum {IrIds.Serialize(reference.Id)}.");
            if (enumeration.ValueType != IREnumValueType.String)
            {
                Unsupported("numeric-enum-unsupported", path, "Numeric enum membership cannot yet be certified against this catalogue representation.");
                return;
            }
            string[] source = enumeration.Members.Select(member => member.Value switch
            {
                string value => value,
                JsonElement { ValueKind: JsonValueKind.String } value => value.GetString()!,
                _ => throw new AuditInputException("enum-value-unresolved", "A string enum has a non-string canonical value.")
            }).ToArray();
            string[]? values = StringValues(native);
            if (enumeration.IsFixed)
            {
                if (values is null || !source.ToHashSet(StringComparer.Ordinal).SetEquals(values))
                    Difference("closed-enum-membership", path, "Closed enum membership differs or the catalogue opens the enum.");
            }
            else if (native is StringType text) CheckPrimitive(new() { Name = IRTypeRefPrimitiveName.String }, text, facet, path);
            else Difference("enum-narrowing", path, "The catalogue narrows an extensible enum to known values.");
        }
    }
}
