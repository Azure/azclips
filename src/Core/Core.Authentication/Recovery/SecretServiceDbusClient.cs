// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Tmds.DBus.Protocol;

namespace Core.Authentication.Recovery;

internal sealed class SecretServiceDbusClient : ISecretServiceClient
{
    private const string ServiceName = "org.freedesktop.secrets";
    private const string ServicePath = "/org/freedesktop/secrets";
    private const string ServiceInterface = "org.freedesktop.Secret.Service";
    private const string CollectionInterface = "org.freedesktop.Secret.Collection";
    private const string ItemInterface = "org.freedesktop.Secret.Item";
    internal const MessageFlags CallFlags = MessageFlags.NoAutoStart;
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(2);

    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly DBusConnection _connection;

    public SecretServiceDbusClient()
    {
        var address = ResolveSessionAddress(
            Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS"),
            Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR"));
        _connection = new DBusConnection(new DBusConnectionOptions(address) { AutoConnect = true });
    }

    public SecretServiceSearchResult SearchItems(IReadOnlyDictionary<string, string> attributes)
    {
        using var writer = CreateWriter(ServicePath, ServiceInterface, "SearchItems", "a{ss}");
        var start = writer.WriteDictionaryStart();
        foreach (var (name, value) in attributes)
        {
            writer.WriteDictionaryEntryStart();
            writer.WriteString(name);
            writer.WriteString(value);
        }
        writer.WriteDictionaryEnd(start);
        return Call(writer.CreateMessage(), static (message, _) =>
        {
            var reader = message.GetBodyReader();
            return new SecretServiceSearchResult(
                reader.ReadArrayOfObjectPath().Select(path => path.ToString()).ToArray(),
                reader.ReadArrayOfObjectPath().Select(path => path.ToString()).ToArray());
        });
    }

    public string ReadDefaultCollection()
    {
        using var writer = CreateWriter(ServicePath, ServiceInterface, "ReadAlias", "s");
        writer.WriteString("default");
        return Call(writer.CreateMessage(), static (message, _) => message.GetBodyReader().ReadObjectPath().ToString());
    }

    public bool IsCollectionLocked(string collection)
    {
        using var writer = CreateWriter(collection, "org.freedesktop.DBus.Properties", "Get", "ss");
        writer.WriteString(CollectionInterface);
        writer.WriteString("Locked");
        return Call(writer.CreateMessage(), static (message, _) => message.GetBodyReader().ReadVariantValue().GetBool());
    }

    public string OpenSession()
    {
        using var writer = CreateWriter(ServicePath, ServiceInterface, "OpenSession", "sv");
        writer.WriteString("plain");
        writer.WriteVariantString(string.Empty);
        var session = Call(writer.CreateMessage(), static (message, _) =>
        {
            var reader = message.GetBodyReader();
            _ = reader.ReadVariantValue();
            return reader.ReadObjectPath().ToString();
        });
        return session != "/" ? session : throw LinuxFailureHistoryKeyStorage.Unavailable();
    }

    public SecretServiceValue GetSecret(string item, string session)
    {
        using var writer = CreateWriter(item, ItemInterface, "GetSecret", "o");
        writer.WriteObjectPath(session);
        return Call(writer.CreateMessage(), static (message, _) =>
        {
            var reader = message.GetBodyReader();
            reader.AlignStruct();
            return new SecretServiceValue(
                reader.ReadObjectPath().ToString(),
                reader.ReadArrayOfByte(),
                reader.ReadArrayOfByte(),
                reader.ReadString());
        });
    }

    public SecretServiceCreateResult CreateItem(
        string collection, string session, IReadOnlyDictionary<string, string> attributes, byte[] value)
    {
        using var writer = CreateWriter(collection, CollectionInterface, "CreateItem", "a{sv}(oayays)b");
        writer.WriteDictionary(new Dictionary<string, VariantValue>
        {
            [$"{ItemInterface}.Label"] = "Azclips encrypted failure history",
            [$"{ItemInterface}.Attributes"] = new Dict<string, string>(
                attributes.ToDictionary(pair => pair.Key, pair => pair.Value)),
        });
        writer.WriteStructureStart();
        writer.WriteObjectPath(session);
        writer.WriteArray(System.Array.Empty<byte>());
        writer.WriteArray(value);
        writer.WriteString(LinuxFailureHistoryKeyStorage.ContentType);
        // A delayed request must not overwrite an established key after its caller times out.
        writer.WriteBool(false);
        return Call(writer.CreateMessage(), static (message, _) =>
        {
            var reader = message.GetBodyReader();
            return new SecretServiceCreateResult(reader.ReadObjectPath().ToString(), reader.ReadObjectPath().ToString());
        });
    }

    public string DeleteItem(string item)
    {
        using var writer = CreateWriter(item, ItemInterface, "Delete", signature: null);
        return Call(writer.CreateMessage(), static (message, _) => message.GetBodyReader().ReadObjectPath().ToString());
    }

    private MessageWriter CreateWriter(string path, string @interface, string member, string? signature)
    {
        var writer = _connection.GetMessageWriter();
        writer.WriteMethodCallHeader(
            destination: ServiceName, path: path, @interface: @interface, member: member,
            signature: signature, flags: CallFlags);
        return writer;
    }

    private T Call<T>(MessageBuffer request, MessageValueReader<T> read)
    {
        var response = _connection.CallMethodAsync(request, read);
        var remaining = OperationTimeout - _elapsed.Elapsed;
        return response.WaitAsync(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero)
            .GetAwaiter().GetResult();
    }

    internal static string ResolveSessionAddress(string? address, string? runtimeDirectory)
    {
        if (string.IsNullOrWhiteSpace(address) && runtimeDirectory?.StartsWith("/", StringComparison.Ordinal) == true)
            address = $"unix:path={Uri.EscapeDataString(runtimeDirectory.TrimEnd('/') + "/bus")}";

        // Do not use the library's X11 discovery or a TCP session bus for local-only history.
        if (string.IsNullOrWhiteSpace(address) ||
            address.Split(';').Any(endpoint => !endpoint.StartsWith("unix:", StringComparison.Ordinal)))
        {
            throw LinuxFailureHistoryKeyStorage.Unavailable();
        }
        return address;
    }

    // Disconnecting also closes Secret Service sessions and aborts pending local transport IO.
    public void Dispose() => _connection.Dispose();
}
