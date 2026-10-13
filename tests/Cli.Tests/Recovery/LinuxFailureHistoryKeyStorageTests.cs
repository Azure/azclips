// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Recovery;
using Core.Authentication.Recovery;
using Tmds.DBus.Protocol;

namespace Cli.Tests.Recovery;

public sealed class LinuxFailureHistoryKeyStorageTests
{
    [Fact]
    public void ExistingUnlockedKey_IsReadWithoutUnlockingOrWriting()
    {
        var client = new FakeClient { Search = new(["/item"], []) };
        var storage = CreateStorage(client);

        var result = storage.ReadData();

        Assert.Equal(client.Secret.Value, result);
        Assert.Equal(["SearchItems", "OpenSession", "GetSecret", "Dispose"], client.Calls);
        Assert.True(storage.RequiresMarker);
        Assert.Equal("purpose", client.Attributes!["application"]);
        Assert.Equal("purpose", client.Attributes["xdg:schema"]);
        Assert.Equal("home:12", client.Attributes["directory"]);
    }

    [Fact]
    public void MissingKey_DoesNotCreateACollectionOrSession()
    {
        var client = new FakeClient();
        Assert.Empty(CreateStorage(client).ReadData());
        Assert.Equal(["SearchItems", "Dispose"], client.Calls);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    public void LockedOrAmbiguousKeys_AreNotTreatedAsMissing(int unlocked, int locked)
    {
        var client = new FakeClient
        {
            Search = new(
                Enumerable.Repeat("/unlocked", unlocked).ToArray(),
                Enumerable.Repeat("/locked", locked).ToArray()),
        };
        AssertUnavailable(() => CreateStorage(client).ReadData());
        Assert.Equal(["SearchItems", "Dispose"], client.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(31)]
    [InlineData(33)]
    public void InvalidNativeKey_IsNotRegeneratedAndItsBufferIsCleared(int length)
    {
        var value = Enumerable.Repeat((byte)42, length).ToArray();
        var client = new FakeClient
        {
            Search = new(["/item"], []),
            Secret = new("/session", [], value, LinuxFailureHistoryKeyStorage.ContentType),
        };
        var error = Assert.Throws<FailureHistoryException>(() => CreateStorage(client).ReadData());
        Assert.Equal("failure_history_key_invalid", error.Code);
        Assert.All(value, octet => Assert.Equal(0, octet));
        Assert.DoesNotContain("CreateItem", client.Calls);
    }

    [Theory]
    [InlineData("/wrong_session", false)]
    [InlineData("/session", true)]
    public void InconsistentSecretReply_IsRejected(string session, bool hasParameters)
    {
        var value = Enumerable.Repeat((byte)42, 32).ToArray();
        var client = new FakeClient
        {
            Search = new(["/item"], []),
            Secret = new(session, hasParameters ? [1] : [], value, LinuxFailureHistoryKeyStorage.ContentType),
        };
        var error = Assert.Throws<FailureHistoryException>(() => CreateStorage(client).ReadData());
        Assert.Equal("failure_history_key_invalid", error.Code);
        Assert.All(value, octet => Assert.Equal(0, octet));
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/octet-stream")]
    public void StoredKey_IsReadRegardlessOfReturnedContentType(string contentType)
    {
        // GNOME Keyring returns text/plain for every item, whatever content type was written.
        var value = Enumerable.Range(0, 32).Select(number => (byte)number).ToArray();
        var client = new FakeClient
        {
            Search = new(["/item"], []),
            Secret = new("/session", [], value.ToArray(), contentType),
        };

        Assert.Equal(value, CreateStorage(client).ReadData());
    }

    [Fact]
    public void NewKey_UsesAnExistingUnlockedCollection()
    {
        var client = new FakeClient();
        var value = Enumerable.Range(0, 32).Select(number => (byte)number).ToArray();

        CreateStorage(client).WriteData(value);

        Assert.Equal(value, client.CreatedValue);
        Assert.Equal(
            ["SearchItems", "ReadDefaultCollection", "IsCollectionLocked", "OpenSession", "CreateItem", "Dispose"],
            client.Calls);
    }

    [Theory]
    [InlineData("/", false)]
    [InlineData("/collection", true)]
    public void UnavailableCollection_DoesNotRequestCreationOrUnlock(string collection, bool locked)
    {
        var client = new FakeClient { Collection = collection, Locked = locked };
        AssertUnavailable(() => CreateStorage(client).WriteData(new byte[32]));
        Assert.DoesNotContain("OpenSession", client.Calls);
        Assert.DoesNotContain("CreateItem", client.Calls);
        Assert.Equal("Dispose", client.Calls[^1]);
    }

    [Fact]
    public void ExistingKey_IsNeverOverwritten()
    {
        var client = new FakeClient { Search = new(["/existing"], []) };
        AssertUnavailable(() => CreateStorage(client).WriteData(new byte[32]));
        Assert.Equal(["SearchItems", "Dispose"], client.Calls);
    }

    [Theory]
    [InlineData("/", "/prompt")]
    [InlineData("/item", "/prompt")]
    [InlineData("/", "/")]
    public void PromptOrUnconfirmedCreation_IsRejectedWithoutFollowingThePrompt(string item, string prompt)
    {
        var client = new FakeClient { Created = new(item, prompt) };
        AssertUnavailable(() => CreateStorage(client).WriteData(new byte[32]));
        Assert.Equal("CreateItem", client.Calls[^2]);
        Assert.Equal("Dispose", client.Calls[^1]);
    }

    [Theory]
    [InlineData("SearchItems")]
    [InlineData("GetSecret")]
    [InlineData("CreateItem")]
    public void BackendErrors_AreSanitizedAndTheConnectionIsClosed(string failureAt)
    {
        var client = new FakeClient
        {
            Search = failureAt == "CreateItem" ? new([], []) : new(["/item"], []),
            FailureAt = failureAt,
        };
        var storage = CreateStorage(client);
        var error = Assert.Throws<FailureHistoryException>(() =>
        {
            if (failureAt == "CreateItem")
                storage.WriteData(new byte[32]);
            else
                storage.ReadData();
        });
        Assert.Equal("failure_history_key_unavailable", error.Code);
        Assert.DoesNotContain("private-native-detail", error.ToString());
        Assert.Equal("Dispose", client.Calls[^1]);
    }

    [Fact]
    public void ConnectionTimeout_IsAnExplicitUnavailableError()
    {
        var storage = new LinuxFailureHistoryKeyStorage("purpose", "home:12", () => throw new TimeoutException());
        AssertUnavailable(() => storage.ReadData());
    }

    [Theory]
    [InlineData("unix:path=/run/user/1000/bus")]
    [InlineData("unix:abstract=/tmp/dbus-test")]
    [InlineData("unix:path=/first;unix:abstract=/second")]
    public void OnlyLocalUnixBusAddressesAreAccepted(string address)
        => Assert.Equal(address, SecretServiceDbusClient.ResolveSessionAddress(address, null));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("autolaunch:")]
    [InlineData("tcp:host=example.test,port=1234")]
    [InlineData("unix:path=/local;tcp:host=example.test,port=1234")]
    [InlineData("unix:path=/local;")]
    public void MissingOrNonlocalBus_IsRejectedWithoutConnecting(string? address)
        => AssertUnavailable(() => SecretServiceDbusClient.ResolveSessionAddress(address, null));

    [Fact]
    public void RuntimeDirectoryFallback_IsEscapedWithoutX11Discovery()
    {
        Assert.Equal("unix:path=%2Frun%2Fuser%2F1000%2Fbus",
            SecretServiceDbusClient.ResolveSessionAddress(null, "/run/user/1000/"));
        AssertUnavailable(() => SecretServiceDbusClient.ResolveSessionAddress(null, "relative"));
        AssertUnavailable(() => SecretServiceDbusClient.ResolveSessionAddress("tcp:host=host,port=1", "/run/user/1000"));
    }

    [Fact]
    public void Calls_DoNotActivateServicesOrAllowInteractiveAuthorization()
    {
        Assert.True(SecretServiceDbusClient.CallFlags.HasFlag(MessageFlags.NoAutoStart));
        Assert.False(SecretServiceDbusClient.CallFlags.HasFlag(MessageFlags.AllowInteractiveAuthorization));
    }

    private static LinuxFailureHistoryKeyStorage CreateStorage(FakeClient client)
        => new("purpose", "home:12", () => client);

    private static void AssertUnavailable(Action action)
        => Assert.Equal("failure_history_key_unavailable", Assert.Throws<FailureHistoryException>(action).Code);

    private sealed class FakeClient : ISecretServiceClient
    {
        internal List<string> Calls { get; } = [];
        internal SecretServiceSearchResult Search { get; init; } = new([], []);
        internal SecretServiceValue Secret { get; init; } = new("/session", [], new byte[32], LinuxFailureHistoryKeyStorage.ContentType);
        internal string Collection { get; init; } = "/collection";
        internal bool Locked { get; init; }
        internal SecretServiceCreateResult Created { get; init; } = new("/item", "/");
        internal string? FailureAt { get; init; }
        internal IReadOnlyDictionary<string, string>? Attributes { get; private set; }
        internal byte[]? CreatedValue { get; private set; }

        public SecretServiceSearchResult SearchItems(IReadOnlyDictionary<string, string> attributes)
        {
            Record("SearchItems");
            Attributes = attributes;
            return Search;
        }

        public string ReadDefaultCollection()
        {
            Record("ReadDefaultCollection");
            return Collection;
        }

        public bool IsCollectionLocked(string collection)
        {
            Assert.Equal(Collection, collection);
            Record("IsCollectionLocked");
            return Locked;
        }

        public string OpenSession()
        {
            Record("OpenSession");
            return "/session";
        }

        public SecretServiceValue GetSecret(string item, string session)
        {
            Assert.Equal("/session", session);
            Record("GetSecret");
            return Secret;
        }

        public SecretServiceCreateResult CreateItem(
            string collection, string session, IReadOnlyDictionary<string, string> attributes, byte[] value)
        {
            Assert.Equal(Collection, collection);
            Assert.Equal("/session", session);
            Assert.Equal(Attributes, attributes);
            Record("CreateItem");
            CreatedValue = value.ToArray();
            return Created;
        }

        public void Dispose() => Calls.Add("Dispose");

        private void Record(string method)
        {
            Calls.Add(method);
            if (method == FailureAt)
                throw new DBusExceptionBase("private-native-detail");
        }
    }
}
