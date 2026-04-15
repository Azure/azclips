using Core.Configuration.Abstractions;

namespace Core.Configuration.Implementation;

public sealed class ConfigurationService(
    IConfigurationProvider provider,
    IConfigurationSchema schema,
    IConfigurationValidator validator) : IConfigurationService
{
    private readonly IConfigurationProvider _provider = provider;
    private readonly IConfigurationSchema _schema = schema;
    private readonly IConfigurationValidator _validator = validator;

    public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new()
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            var keyValidation = _validator.ValidateKey(key, force: true);
            if (!keyValidation.IsValid)
            {
                throw new ArgumentException(keyValidation.ErrorMessage, nameof(key));
            }
        }

        return defaultValue ?? new T();
    }

    public Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default)
    {
        var keyValidation = _validator.ValidateKey(key, force);
        if (!keyValidation.IsValid)
        {
            throw new ArgumentException(keyValidation.ErrorMessage, nameof(key));
        }

        var valueValidation = _validator.ValidateValue(key, value);
        if (!valueValidation.IsValid)
        {
            throw new ArgumentException(valueValidation.ErrorMessage, nameof(value));
        }

        throw new NotSupportedException("SetConfigurationAsync will be fully implemented in Phase 2.");
    }

    public Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default)
    {
        var keyValidation = _validator.ValidateKey(key, force);
        if (!keyValidation.IsValid)
        {
            throw new ArgumentException(keyValidation.ErrorMessage, nameof(key));
        }

        throw new NotSupportedException("DeleteConfigurationAsync will be fully implemented in Phase 2.");
    }

    public Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default)
    {
        if (!includeDefaults)
        {
            return Task.FromResult(new Dictionary<string, object?>());
        }

        var defaults = _schema.GetAllKeys().ToDictionary(
            k => k.Key,
            v => (object?)v.DefaultValue,
            StringComparer.OrdinalIgnoreCase);

        return Task.FromResult(defaults);
    }

    public string GetConfigFilePath() => _provider.GetConfigFilePath();

    public IConfigurationSchema GetSchema() => _schema;
}
