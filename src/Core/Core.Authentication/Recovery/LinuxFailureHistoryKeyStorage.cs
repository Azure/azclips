// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography;
using Core.Abstractions.Recovery;
using Tmds.DBus.Protocol;

namespace Core.Authentication.Recovery;

internal sealed class LinuxFailureHistoryKeyStorage : IProtectedHistoryKeyStorage
{
    internal const string ContentType = "application/octet-stream";
    private readonly IReadOnlyDictionary<string, string> _attributes;
    private readonly Func<ISecretServiceClient> _connect;

    public LinuxFailureHistoryKeyStorage(string purpose, string keyId, Func<ISecretServiceClient>? connect = null)
    {
        _attributes = new Dictionary<string, string>
        {
            ["xdg:schema"] = purpose,
            ["application"] = purpose,
            ["directory"] = keyId,
        };
        _connect = connect ?? (() => new SecretServiceDbusClient());
    }

    public byte[] ReadData() => WithClient(client =>
    {
        var items = client.SearchItems(_attributes);
        EnsureUnambiguousUnlockedItems(items);
        if (items.Unlocked.Length == 0)
        {
            // A locked keyring returns no items, so an empty search is only "missing" when it is unlocked.
            var collection = client.ReadDefaultCollection();
            if (collection == "/" || client.IsCollectionLocked(collection))
                throw Unavailable();
            return [];
        }

        var session = client.OpenSession();
        var secret = client.GetSecret(items.Unlocked[0], session);
        // Content type is not checked: GNOME Keyring does not store it and returns text/plain.
        if (secret.Session == session && secret.Parameters.Length == 0 && secret.Value.Length == 32)
        {
            return secret.Value;
        }

        CryptographicOperations.ZeroMemory(secret.Value);
        throw new FailureHistoryException("failure_history_key_invalid", "The protected failure-history key is invalid.");
    });

    public void WriteData(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length != 32)
            throw new ArgumentException("A failure-history key must contain exactly 32 bytes.", nameof(data));

        WithClient(client =>
        {
            var items = client.SearchItems(_attributes);
            EnsureUnambiguousUnlockedItems(items);
            if (items.Unlocked.Length != 0)
                throw Unavailable();

            var collection = client.ReadDefaultCollection();
            if (collection == "/" || client.IsCollectionLocked(collection))
                throw Unavailable();

            var session = client.OpenSession();
            var result = client.CreateItem(collection, session, _attributes, data);
            // A returned prompt is only a request. Never invoke Prompt or Unlock, including
            // when the collection became locked after the check above.
            if (result.Item == "/" || result.Prompt != "/")
                throw Unavailable();

            return true;
        });
    }

    private T WithClient<T>(Func<ISecretServiceClient, T> action)
    {
        try
        {
            using var client = _connect();
            return action(client);
        }
        catch (Exception exception) when (exception is DBusExceptionBase or TimeoutException)
        {
            throw Unavailable();
        }
    }

    private static void EnsureUnambiguousUnlockedItems(SecretServiceSearchResult items)
    {
        if (items.Locked.Length != 0 || items.Unlocked.Length > 1)
            throw Unavailable();
    }

    internal static FailureHistoryException Unavailable() => new(
        "failure_history_key_unavailable",
        "Noninteractive failure-history key storage requires an accessible local Secret Service and an unlocked keyring. No unlock or authorization prompt was requested.");
}

internal readonly record struct SecretServiceSearchResult(string[] Unlocked, string[] Locked);
internal readonly record struct SecretServiceCreateResult(string Item, string Prompt);
internal sealed record SecretServiceValue(string Session, byte[] Parameters, byte[] Value, string ContentType);

internal interface ISecretServiceClient : IDisposable
{
    SecretServiceSearchResult SearchItems(IReadOnlyDictionary<string, string> attributes);
    string ReadDefaultCollection();
    bool IsCollectionLocked(string collection);
    string OpenSession();
    SecretServiceValue GetSecret(string item, string session);
    SecretServiceCreateResult CreateItem(
        string collection, string session, IReadOnlyDictionary<string, string> attributes, byte[] value);
}
