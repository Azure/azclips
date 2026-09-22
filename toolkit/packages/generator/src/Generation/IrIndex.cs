using Ace.Generator.Model.Ir;

namespace Ace.Generator.Generators;

/// <summary>
/// Identity lookup over an <see cref="TransformedIR"/> — the C# counterpart of the IR package's
/// <c>indexIR</c>.
/// </summary>
/// <remarks>
/// The IR is flat collections plus <see cref="IRId"/> references, so resolving one would
/// otherwise scan. Entries are keyed by <see cref="IrIds.Key"/> so lookup agrees with
/// <see cref="IrIds.Equal"/> exactly; a <see cref="IrIds.Serialize"/>-keyed map does not,
/// because it flattens namespace segments.
/// <para>
/// A snapshot of the document as it stood when built. Generation does not mutate the IR, so
/// one instance per run is safe.
/// </para>
/// </remarks>
internal sealed class IrIndex
{
    private readonly Dictionary<string, IRModel> _models = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IREnum> _enums = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IROperation> _operations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IRServerDefinition> _servers = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ambiguousServers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IRProperty> _properties = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IRModel> _propertyOwners = new(StringComparer.Ordinal);

    public IrIndex(TransformedIR ir)
    {
        ArgumentNullException.ThrowIfNull(ir);

        // Ids are nullable on the POCOs (hand-built and partial IR is a real input), so an
        // entry without one is simply unreachable by id rather than a failure.
        foreach (IRModel model in ir.Models)
        {
            if (model.Id is not null)
            {
                _models[IrIds.Key(model.Id)] = model;
            }

            foreach (IRProperty property in model.Properties)
            {
                if (property.Id is null)
                {
                    continue;
                }

                // First declaration wins: inherited properties are denormalized onto every
                // subtype, so the same id legitimately appears on several models.
                string key = IrIds.Key(property.Id);
                if (_properties.TryAdd(key, property))
                {
                    _propertyOwners[key] = model;
                }
            }
        }

        foreach (IREnum value in ir.Enums)
        {
            if (value.Id is not null)
            {
                _enums[IrIds.Key(value.Id)] = value;
            }
        }

        foreach (IROperation operation in ir.Operations)
        {
            if (operation.Id is not null)
            {
                _operations[IrIds.Key(operation.Id)] = operation;
            }
        }

        foreach (IRServerDefinition server in ir.Servers)
        {
            if (server.Id is null)
            {
                continue;
            }

            // A dictionary would silently keep the last duplicate, so ambiguity is recorded
            // rather than collapsed — two definitions of one id is an error, not a last-wins.
            string key = IrIds.Key(server.Id);
            if (!_servers.TryAdd(key, server))
            {
                _ambiguousServers.Add(key);
            }
        }
    }

    public IRModel? Model(IRId? id) => Get(_models, id);

    public IREnum? Enum(IRId? id) => Get(_enums, id);

    public IROperation? Operation(IRId? id) => Get(_operations, id);

    public IRServerDefinition? Server(IRId? id) => Get(_servers, id);

    /// <summary>True when the document defines this server id more than once.</summary>
    public bool IsServerAmbiguous(IRId id) => _ambiguousServers.Contains(IrIds.Key(id));

    /// <summary>
    /// Property by id, across every model. Properties are not a top-level collection, so this
    /// is the only way to resolve one — which flattening paths need, since they are property ids.
    /// </summary>
    public IRProperty? Property(IRId? id) => Get(_properties, id);

    /// <summary>The model that declares a property, for walking a flattened path to its owner.</summary>
    public IRModel? OwnerOf(IRId? propertyId) => Get(_propertyOwners, propertyId);

    /// <summary>Every indexed property, for callers that need the whole table at once.</summary>
    public IReadOnlyDictionary<string, IRProperty> Properties => _properties;

    private static T? Get<T>(Dictionary<string, T> table, IRId? id)
        where T : class =>
        id is not null && table.TryGetValue(IrIds.Key(id), out T? value) ? value : null;
}
