using System.Buffers.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;

internal static class CertificateJson
{
    internal static JsonObject NormalizePolicy(string json)
    {
        var policy = JsonNode.Parse(json, new JsonNodeOptions { PropertyNameCaseInsensitive = true }) as JsonObject
            ?? throw new JsonException("Certificate policy must be an object.");
        policy.Remove("id");
        Move(policy, "issuer", ("IssuerName", "name"), ("CertificateType", "cty"), ("CertificateTransparency", "cert_transparency"));
        Move(policy, "key_props", ("Kty", "kty"), ("KeySize", "key_size"), ("Curve", "crv"), ("Exportable", "exportable"), ("ReuseKeyOnRenewal", "reuse_key"));
        Move(policy, "secret_props", ("SecretContentType", "contentType"));
        Move(policy, "x509_props", ("SubjectName", "subject"), ("ValidityInMonths", "validity_months"), ("KeyUsage", "key_usage"), ("Ekus", "ekus"));
        if (new[] { "DnsNames", "Emails", "UserPrincipalNames" }.Any(policy.ContainsKey))
        {
            var names = new JsonObject();
            foreach (var (source, target) in new[] { ("DnsNames", "dns_names"), ("Emails", "emails"), ("UserPrincipalNames", "upns") })
            {
                if (!policy.TryGetPropertyValue(source, out var value)) continue;
                policy.Remove(source);
                if (value is not null) names[target] = value;
            }
            if (names.Count > 0)
            {
                var subjectProperties = policy["x509_props"] as JsonObject ?? new JsonObject();
                policy["x509_props"] = subjectProperties;
                if (subjectProperties["sans"] is not null) throw new JsonException("Conflicting certificate policy fields.");
                subjectProperties["sans"] = names;
            }
        }
        Move(policy, "attributes", ("Enabled", "enabled"), ("Created", "created"), ("Updated", "updated"));
        foreach (var (source, action, trigger) in new[]
        {
            ("RenewAtNumberOfDaysBeforeExpiry", "AutoRenew", "days_before_expiry"),
            ("RenewAtPercentageLifetime", "AutoRenew", "lifetime_percentage"),
            ("EmailAtNumberOfDaysBeforeExpiry", "EmailContacts", "days_before_expiry"),
            ("EmailAtPercentageLifetime", "EmailContacts", "lifetime_percentage")
        })
        {
            if (!policy.TryGetPropertyValue(source, out var value)) continue;
            policy.Remove(source);
            if (value is null) continue;
            var lifetimeActions = policy["lifetime_actions"] as JsonArray ?? new JsonArray();
            policy["lifetime_actions"] = lifetimeActions;
            lifetimeActions.Add(new JsonObject { ["action"] = new JsonObject { ["action_type"] = action }, ["trigger"] = new JsonObject { [trigger] = value } });
        }
        Rename(policy, ("issuer_parameters", "issuer"), ("issuerParameters", "issuer"),
            ("key_properties", "key_props"), ("keyProperties", "key_props"),
            ("secret_properties", "secret_props"), ("secretProperties", "secret_props"),
            ("x509_certificate_properties", "x509_props"), ("x509CertificateProperties", "x509_props"),
            ("lifetimeActions", "lifetime_actions"));
        if (policy["issuer"] is JsonObject issuer)
            Rename(issuer, ("certificate_type", "cty"), ("certificateType", "cty"),
                ("certificate_transparency", "cert_transparency"), ("certificateTransparency", "cert_transparency"));
        if (policy["key_props"] is JsonObject key)
            Rename(key, ("key_type", "kty"), ("keyType", "kty"), ("keySize", "key_size"),
                ("reuseKey", "reuse_key"), ("curve", "crv"));
        if (policy["secret_props"] is JsonObject secret)
            Rename(secret, ("content_type", "contentType"));
        if (policy["x509_props"] is JsonObject x509)
        {
            Rename(x509, ("subject_alternative_names", "sans"), ("subjectAlternativeNames", "sans"),
                ("keyUsage", "key_usage"), ("validity_in_months", "validity_months"), ("validityInMonths", "validity_months"));
            if (x509["sans"] is JsonObject names) Rename(names, ("dnsNames", "dns_names"));
        }
        if (policy["lifetime_actions"] is JsonArray actions)
        {
            foreach (var entry in actions.OfType<JsonObject>())
            {
                if (entry["action"] is JsonObject action) Rename(action, ("actionType", "action_type"));
                if (entry["trigger"] is JsonObject trigger)
                    Rename(trigger, ("lifetimePercentage", "lifetime_percentage"), ("daysBeforeExpiry", "days_before_expiry"));
            }
        }
        if (policy["attributes"] is JsonObject attributes)
        {
            Rename(attributes, ("not_before", "nbf"), ("notBefore", "nbf"), ("expires_on", "exp"), ("expires", "exp"));
            foreach (string name in new[] { "created", "updated", "recoveryLevel", "recoverableDays" }) attributes.Remove(name);
            foreach (string name in new[] { "nbf", "exp" })
            {
                if (attributes[name] is JsonValue value && value.TryGetValue<string>(out var text))
                    attributes[name] = SecretCommandInputs.ParseTime(text);
            }
        }
        RemoveEmptyFields(policy);
        return policy;
    }

    private static void RemoveEmptyFields(JsonObject value)
    {
        foreach (var property in value.ToArray())
        {
            if (property.Value is JsonObject child)
            {
                RemoveEmptyFields(child);
                if (child.Count == 0) value.Remove(property.Key);
            }
            else if (property.Value is null) value.Remove(property.Key);
            else if (property.Value is JsonArray array)
                foreach (var item in array.OfType<JsonObject>()) RemoveEmptyFields(item);
        }
    }

    private static void Move(JsonObject policy, string section, params (string Source, string Target)[] names)
    {
        foreach (var (source, target) in names)
        {
            if (!policy.TryGetPropertyValue(source, out var item)) continue;
            policy.Remove(source);
            if (item is null) continue;
            var value = policy[section] as JsonObject ?? new JsonObject();
            if (value.TryGetPropertyValue(target, out var existing) && !JsonNode.DeepEquals(existing, item))
                throw new JsonException("Conflicting certificate policy fields.");
            policy[section] = value;
            value[target] = item;
        }
    }

    private static void Rename(JsonObject value, params (string Source, string Target)[] names)
    {
        foreach (var (source, target) in names)
        {
            if (!value.TryGetPropertyValue(source, out var item)) continue;
            if (value.TryGetPropertyValue(target, out var existing) && !JsonNode.DeepEquals(item, existing))
                throw new JsonException("Conflicting certificate policy fields.");
            value.Remove(source);
            value[target] = item;
        }
    }

    public sealed class ThumbprintConverter : JsonConverter<byte[]>
    {
        public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            try { return Base64Url.DecodeFromChars(reader.GetString()!); }
            catch (Exception error) when (error is FormatException or ArgumentException)
            {
                throw new JsonException("Invalid certificate thumbprint.");
            }
        }

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options) =>
            writer.WriteStringValue(Base64Url.EncodeToString(value));
    }
}